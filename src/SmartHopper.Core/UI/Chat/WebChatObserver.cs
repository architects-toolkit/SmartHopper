/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Rhino;
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AICall.Core.Requests;
using SmartHopper.Infrastructure.AICall.Core.Returns;
using SmartHopper.Infrastructure.AICall.Sessions;
using SmartHopper.Infrastructure.AICall.Utilities;

namespace SmartHopper.Core.UI.Chat
{
    // Keep observer as a nested type inside the partial WebChatDialog for private member access
    internal partial class WebChatDialog
    {
        /// <summary>
        /// Conversation observer that updates the UI incrementally and de-duplicates interactions.
        /// One instance is created per conversation run.
        /// </summary>
        private sealed class WebChatObserver : IConversationObserver
        {
            private readonly WebChatDialog _dialog;

            // Centralized streaming state per run (role-agnostic)
            private sealed class StreamState
            {
                public bool Started;
                public IAIInteraction Aggregated;
            }

            [Conditional("DEBUG")]
            private static void DebugLog(string message)
            {
                Debug.WriteLine(message);
            }

            /// <summary>
            /// Returns the current segmented text key for a base key without creating a new segment.
            /// </summary>
            private string GetCurrentSegmentedKey(string baseKey)
            {
                if (string.IsNullOrWhiteSpace(baseKey)) return baseKey;
                if (!this._textInteractionSegments.TryGetValue(baseKey, out var seg))
                {
                    seg = 1;
                    this._textInteractionSegments[baseKey] = seg;
                }

                return $"{baseKey}:seg{seg}";
            }

            /// <summary>
            /// Peeks at what the next segment key would be for a base key, without mutating state.
            /// If boundary is pending and segment exists, returns next segment; otherwise returns current or initial.
            /// </summary>
            private string PeekSegmentKey(string baseKey, string turnKey)
            {
                if (string.IsNullOrWhiteSpace(baseKey)) return baseKey;

                int seg;
                if (!this._textInteractionSegments.TryGetValue(baseKey, out seg))
                {
                    // Not yet committed: would start at seg1
                    seg = 1;
                }
                else if (!string.IsNullOrWhiteSpace(turnKey) && this._pendingNewTextSegmentTurns.Contains(turnKey))
                {
                    // Boundary pending: would increment
                    seg = seg + 1;
                }

                // else: use current committed seg

                return $"{baseKey}:seg{seg}";
            }

            /// <summary>
            /// Commits the segment for a base key by consuming boundary and initializing/incrementing the segment counter.
            /// Call this only when you are about to render text for the first time or after a boundary.
            /// </summary>
            private void CommitSegment(string baseKey, string turnKey)
            {
                if (string.IsNullOrWhiteSpace(baseKey)) return;

                var beforeSegment = this._textInteractionSegments.TryGetValue(baseKey, out var seg) ? seg : 0;
                var hasBoundary = !string.IsNullOrWhiteSpace(turnKey) && this._pendingNewTextSegmentTurns.Contains(turnKey);
                DebugLog($"[WebChatObserver] CommitSegment: baseKey={baseKey}, turnKey={turnKey}, beforeSeg={beforeSegment}, hasBoundary={hasBoundary}");

                // Consume boundary flag and increment if applicable
                this.ConsumeBoundaryAndIncrementSegment(turnKey, baseKey);

                // Ensure segment counter is initialized (ConsumeBoundaryAndIncrementSegment requires existing entry)
                if (!this._textInteractionSegments.ContainsKey(baseKey))
                {
                    this._textInteractionSegments[baseKey] = 1;
                    DebugLog($"[WebChatObserver] CommitSegment: initialized baseKey={baseKey} to seg=1");
                }
                else
                {
                    var afterSegment = this._textInteractionSegments[baseKey];
                    this.LogDelta($"[WebChatObserver] CommitSegment: baseKey={baseKey} already exists, seg={afterSegment}");
                }
            }

            // Tracks UI state for the temporary thinking bubble.
            // We no longer track assistant-specific bubble state; ordering is handled by keys and upserts.
            private bool _thinkingBubbleActive;

            // Simple per-key throttling to reduce DOM churn during streaming
            private readonly Dictionary<string, DateTime> _lastUpsertAt = new Dictionary<string, DateTime>(StringComparer.Ordinal);
            private const int ThrottleMs = 50;
            private const int ThrottleDuringMoveResizeMs = 400;

            private DateTime _lastDeltaLogUtc = DateTime.MinValue;
            private const int DeltaLogThrottleMs = 250;

            [Conditional("DEBUG")]
            private void LogDelta(string message)
            {
#if DEBUG
                try
                {
                    var now = DateTime.UtcNow;
                    if ((now - this._lastDeltaLogUtc).TotalMilliseconds >= DeltaLogThrottleMs)
                    {
                        this._lastDeltaLogUtc = now;
                        DebugLog(message);
                    }
                }
                catch
                {
                }
#endif
            }

            private readonly Dictionary<string, (string? Content, string? Reasoning)> _lastRenderedTextByKey =
                new Dictionary<string, (string? Content, string? Reasoning)>(StringComparer.Ordinal);

            // Tracks per-turn text segments so multiple text messages in a single turn
            // are rendered as distinct bubbles. Keys are the base stream key (e.g., "turn:{TurnId}:{agent}").
            private readonly Dictionary<string, int> _textInteractionSegments = new Dictionary<string, int>(StringComparer.Ordinal);

            // Pending segmentation boundary flags per turn. When set, the next text interaction for that turn
            // starts a new segment (new bubble). Simplified rule: set boundary after ANY completed interaction.
            private readonly HashSet<string> _pendingNewTextSegmentTurns = new HashSet<string>(StringComparer.Ordinal);

            // Pre-commit aggregates: tracks text aggregates by base key before segment assignment.
            // This allows lazy segment commitment only when text becomes renderable.
            private readonly Dictionary<string, StreamState> _preStreamAggregates = new Dictionary<string, StreamState>(StringComparer.Ordinal);

            // Finalized turns: after OnFinal has rendered the assistant text for a turn, ignore any late
            // OnDelta/OnInteractionCompleted text updates for that same turn to prevent overriding final metrics/time.
            private readonly HashSet<string> _finalizedTextTurns = new HashSet<string>(StringComparer.Ordinal);

            /// <summary>
            /// Returns the generic turn base key for a given turn id (e.g., "turn:{TurnId}").
            /// </summary>
            private static string GetTurnBaseKey(string turnId)
            {
                return string.IsNullOrWhiteSpace(turnId) ? null : $"turn:{turnId}";
            }

            /// <summary>
            /// Aggregates text message across streaming chunks using the shared TextStreamCoalescer utility.
            /// Preserves metrics (null) during streaming; final metrics are applied in OnFinal.
            /// </summary>
            private static void CoalesceTextStreamChunk(AIInteractionText incoming, string key, ref StreamState state)
            {
                if (!state.Started || state.Aggregated is not AIInteractionText)
                {
                    state.Started = true;

                    // Initialize with null metrics - will be applied in OnFinal
                    state.Aggregated = TextStreamCoalescer.Coalesce(null, incoming, incoming?.TurnId, preserveMetrics: true);
                    if (state.Aggregated is AIInteractionText agg)
                    {
                        agg.Metrics = null; // Hide metrics while streaming
                    }

                    return;
                }

                // Use shared coalescer, preserving null metrics during streaming
                state.Aggregated = TextStreamCoalescer.Coalesce(
                    state.Aggregated as AIInteractionText,
                    incoming,
                    incoming?.TurnId,
                    preserveMetrics: true);
            }

            // Tracks active streaming states to distinguish streaming vs non-streaming paths
            private readonly Dictionary<string, StreamState> _streams = new Dictionary<string, StreamState>(StringComparer.Ordinal);

            /// <summary>
            /// Initializes a new instance of the <see cref="WebChatObserver"/> class that updates the
            /// associated <see cref="WebChatDialog"/> in response to conversation session events.
            /// </summary>
            /// <param name="dialog">The chat dialog instance to update.</param>
            public WebChatObserver(WebChatDialog dialog)
            {
                this._dialog = dialog;
                this._thinkingBubbleActive = false;
            }

            /// <summary>
            /// Handles the start of a conversation session.
            /// Resets per-run state and shows a persistent generic loading bubble.
            /// </summary>
            /// <param name="request">The request about to be executed.</param>
            public void OnStart(AIRequestCall request)
            {
                DebugLog("[WebChatObserver] OnStart called");
                RhinoApp.InvokeOnUiThread(() =>
                {
                    DebugLog("[WebChatObserver] OnStart: executing UI updates");

                    // Reset per-run state
                    this._streams.Clear();
                    this._preStreamAggregates.Clear();
                    this._textInteractionSegments.Clear();
                    this._pendingNewTextSegmentTurns.Clear();
                    this._finalizedTextTurns.Clear();
                    this._lastUpsertAt.Clear();
                    this._lastRenderedTextByKey.Clear();
                    this._dialog.ExecuteScript("setStatus('Thinking...'); setProcessing(true);");

                    // Insert a persistent generic loading bubble that remains until stop state
                    this._dialog.ExecuteScript("addLoadingMessage('loading', 'Thinking…');");
                    this._thinkingBubbleActive = true;

                    // No assistant-specific state to reset
                    DebugLog("[WebChatObserver] OnStart: UI updates completed");
                });
            }

            /// <summary>
            /// Removes the persistent thinking bubble if it is currently visible.
            /// Must be called on the UI thread.
            /// </summary>
            private void RemoveThinkingBubbleIfActive()
            {
                if (!this._thinkingBubbleActive)
                {
                    return;
                }

                try
                {
                    // Remove the generic loader via JS helper
                    this._dialog.ExecuteScript("removeThinkingMessage();");
                }
                catch (Exception ex)
                {
                    DebugLog($"[WebChatObserver] RemoveThinkingBubbleIfActive error: {ex.Message}");
                }
                finally
                {
                    this._thinkingBubbleActive = false;
                }
            }

            /// <summary>
            /// Handles streaming delta updates for partial interactions.
            /// Coalesces text chunks and throttles DOM updates for smooth rendering.
            /// </summary>
            /// <param name="interaction">The partial interaction being streamed.</param>
            public void OnDelta(IAIInteraction interaction)
            {
                if (interaction == null)
                    return;

                try
                {
                    RhinoApp.InvokeOnUiThread(() =>
                    {
                        try
                        {
                            // Handle text deltas (assistant/user/system) for live streaming
                            if (interaction is AIInteractionText tt)
                            {
                                var baseKey = GetStreamKey(interaction);
                                var turnKey = GetTurnBaseKey(tt?.TurnId);

                                // If this turn is finalized, ignore any late deltas to avoid overriding final metrics/time
                                if (!string.IsNullOrWhiteSpace(turnKey) && this._finalizedTextTurns.Contains(turnKey))
                                {
                                    return;
                                }

                                // Check if we already have a committed segment for this base key
                                bool isCommitted = this._textInteractionSegments.ContainsKey(baseKey);
                                bool hasBoundary = !string.IsNullOrWhiteSpace(turnKey) && this._pendingNewTextSegmentTurns.Contains(turnKey);
                                this.LogDelta($"[WebChatObserver] OnDelta: baseKey={baseKey}, turnKey={turnKey}, isCommitted={isCommitted}, hasBoundary={hasBoundary}");

                                // Determine the target key. If a boundary is pending while already committed,
                                // roll over to a NEW segment now so subsequent deltas do not append to the previous bubble.
                                string targetKey;
                                if (isCommitted && hasBoundary)
                                {
                                    this.LogDelta($"[WebChatObserver] OnDelta: boundary pending -> rolling over to next segment for baseKey={baseKey}");
                                    this.CommitSegment(baseKey, turnKey); // consumes boundary and increments segment
                                    var segKey = this.GetCurrentSegmentedKey(baseKey);

                                    // Initialize fresh stream state for the new segment
                                    if (!this._streams.ContainsKey(segKey))
                                    {
                                        this._streams[segKey] = new StreamState { Started = false, Aggregated = null };
                                    }

                                    targetKey = segKey;
                                }
                                else
                                {
                                    // Not committed yet -> use baseKey in pre-commit; else use current segment key
                                    targetKey = isCommitted ? this.GetCurrentSegmentedKey(baseKey) : baseKey;
                                }

                                // Retrieve or create pre-commit aggregate
                                StreamState state;
                                if (isCommitted)
                                {
                                    // Already committed: use _streams with segmented key
                                    if (!this._streams.TryGetValue(targetKey, out state))
                                    {
                                        state = new StreamState { Started = false, Aggregated = null };
                                    }
                                }
                                else
                                {
                                    // Not yet committed: use _preStreamAggregates with baseKey
                                    if (!this._preStreamAggregates.TryGetValue(baseKey, out state))
                                    {
                                        state = new StreamState { Started = false, Aggregated = null };
                                    }
                                }

                                // Coalesce text: detect cumulative vs incremental chunks and avoid regressions
                                CoalesceTextStreamChunk(tt, targetKey, ref state);

                                // Check if text is now renderable
                                var aggregatedText = state.Aggregated as AIInteractionText;
                                bool isRenderable = HasRenderableText(aggregatedText);
                                this.LogDelta($"[WebChatObserver] OnDelta: isRenderable={isRenderable}, isCommitted={isCommitted}");

                                if (isRenderable && !isCommitted)
                                {
                                    // First renderable delta: commit the segment now
                                    this.LogDelta($"[WebChatObserver] OnDelta: FIRST RENDER - committing segment");
                                    this.CommitSegment(baseKey, turnKey);
                                    var segKey = this.GetCurrentSegmentedKey(baseKey);
                                    this.LogDelta($"[WebChatObserver] OnDelta: committed segKey={segKey}");

                                    // Move from pre-commit to committed storage
                                    this._streams[segKey] = state;
                                    this._preStreamAggregates.Remove(baseKey);

                                    // Upsert to DOM
                                    if (this.ShouldUpsertNow(segKey))
                                    {
                                        if (aggregatedText != null && this.ShouldRenderDelta(segKey, aggregatedText))
                                        {
                                            this._dialog.UpsertMessageByKey(segKey, aggregatedText, source: "OnDelta:FirstRender");
                                        }
                                    }
                                }
                                else if (isRenderable && isCommitted)
                                {
                                    // Already committed: update in place
                                    this._streams[targetKey] = state;
                                    if (this.ShouldUpsertNow(targetKey))
                                    {
                                        if (aggregatedText != null && this.ShouldRenderDelta(targetKey, aggregatedText))
                                        {
                                            this._dialog.UpsertMessageByKey(targetKey, aggregatedText, source: "OnDelta");
                                        }
                                    }
                                }
                                else
                                {
                                    // Not renderable yet: keep in pre-commit storage
                                    if (!isCommitted)
                                    {
                                        this._preStreamAggregates[baseKey] = state;
                                    }
                                    else
                                    {
                                        this._streams[targetKey] = state;
                                    }
                                }

                                return;
                            }
                        }
                        catch (Exception innerEx)
                        {
                            DebugLog($"[WebChatObserver] OnDelta processing error: {innerEx.Message}");
                        }
                    });
                }
                catch (Exception ex)
                {
                    DebugLog($"[WebChatObserver] OnDelta error: {ex.Message}");
                }
            }

            /// <summary>
            /// Handles completion of an interaction within the current turn.
            /// Persists results and updates the corresponding DOM bubble deterministically.
            /// </summary>
            /// <param name="interaction">The completed interaction.</param>
            public void OnInteractionCompleted(IAIInteraction interaction)
            {
                if (interaction == null)
                    return;

                try
                {
                    RhinoApp.InvokeOnUiThread(() =>
                    {
                        try
                        {
                            // Keep the thinking bubble during processing; do not remove on partials

                            // Text interactions (any agent): handle non-streaming preview or finalize existing streaming aggregate.
                            if (interaction is AIInteractionText tt)
                            {
                                var baseKey = GetStreamKey(interaction);
                                var turnKey = GetTurnBaseKey(tt?.TurnId);

                                // If this turn is finalized, ignore any late partials to avoid overriding final metrics/time
                                if (!string.IsNullOrWhiteSpace(turnKey) && this._finalizedTextTurns.Contains(turnKey))
                                {
                                    return;
                                }

                                // Check if segment is committed
                                bool isCommitted = this._textInteractionSegments.ContainsKey(baseKey);
                                var activeSegKey = isCommitted ? this.GetCurrentSegmentedKey(baseKey) : null;
                                var hasBoundary = !string.IsNullOrWhiteSpace(turnKey) && this._pendingNewTextSegmentTurns.Contains(turnKey);
#if DEBUG
                                DebugLog($"[WebChatObserver] OnInteractionCompleted(Text): baseKey={baseKey}, turnKey={turnKey}, isCommitted={isCommitted}, hasBoundary={hasBoundary}, contentLen={tt.Content?.Length ?? 0}");
#endif

                                // Check for existing streaming aggregate (either committed or pre-commit)
                                StreamState existingState = null;
                                if (isCommitted && !hasBoundary && this._streams.TryGetValue(activeSegKey, out existingState))
                                {
                                    // Streaming completion: update the existing committed aggregate (only if no boundary)
                                    if (existingState.Aggregated is AIInteractionText agg)
                                    {
                                        agg.Content = tt.Content;
                                        agg.Reasoning = tt.Reasoning;
                                        agg.Time = tt.Time;
                                        this._dialog.UpsertMessageByKey(activeSegKey, agg, source: "OnInteractionCompletedStreamingFinal");

                                        // Mark boundary: next text in this turn gets a new segment
                                        this.SetBoundaryFlag(turnKey);
                                        return;
                                    }
                                }
                                else if (!isCommitted && this._preStreamAggregates.TryGetValue(baseKey, out existingState))
                                {
                                    // Had pre-commit aggregate but never rendered (empty deltas): commit now
                                    if (existingState.Aggregated is AIInteractionText agg)
                                    {
                                        agg.Content = tt.Content;
                                        agg.Reasoning = tt.Reasoning;
                                        agg.Time = tt.Time;

                                        // Commit segment and move to committed storage
                                        this.CommitSegment(baseKey, turnKey);
                                        var segKey = this.GetCurrentSegmentedKey(baseKey);
                                        this._streams[segKey] = existingState;
                                        this._preStreamAggregates.Remove(baseKey);

                                        this._dialog.UpsertMessageByKey(segKey, agg, source: "OnInteractionCompletedPreCommitFinal");

                                        // Mark boundary: next text in this turn gets a new segment
                                        this.SetBoundaryFlag(turnKey);
                                        return;
                                    }
                                }

                                // True non-streaming completion path: no prior aggregate
                                // Commit segment immediately since we have renderable content
                                DebugLog($"[WebChatObserver] OnInteractionCompleted(Text): NON-STREAMING path - committing segment");
                                this.CommitSegment(baseKey, turnKey);
                                var finalSegKey = this.GetCurrentSegmentedKey(baseKey);
                                DebugLog($"[WebChatObserver] OnInteractionCompleted(Text): finalSegKey={finalSegKey}");

                                var state = new StreamState { Started = true, Aggregated = tt };
                                this._streams[finalSegKey] = state;
                                this._dialog.UpsertMessageByKey(finalSegKey, tt, source: "OnInteractionCompletedNonStreaming");

                                // Mark boundary: next text in this turn gets a new segment
                                this.SetBoundaryFlag(turnKey);

                                return;
                            }

                            // Stream-update non-text interactions immediately using their provided keys.
                            if (interaction is not AIInteractionText)
                            {
                                var streamKey = GetStreamKey(interaction);
                                var turnKey = GetTurnBaseKey(interaction?.TurnId);
#if DEBUG
                                DebugLog($"[WebChatObserver] OnInteractionCompleted(Non-Text): type={interaction.GetType().Name}, streamKey={streamKey}, turnKey={turnKey}");
#endif

                                if (string.IsNullOrWhiteSpace(streamKey))
                                {
                                    // Fallback: append if keyless (should be rare)
                                    this._dialog.AddInteractionToWebView(interaction);
                                }
                                else
                                {
                                    if (interaction is AIInteractionToolResult tr)
                                    {
                                        var followKey = GetFollowKeyForToolResult(tr);
                                        this._dialog.UpsertMessageAfter(followKey, streamKey, tr, source: "OnInteractionCompletedToolResult");
                                    }
                                    else
                                    {
                                        if (this.ShouldUpsertNow(streamKey))
                                        {
                                            this._dialog.UpsertMessageByKey(streamKey, interaction, source: "OnInteractionCompleted");
                                        }
                                    }
                                }

                                // Mark boundary: next text in this turn gets a new segment
                                this.SetBoundaryFlag(turnKey);
                            }
                        }
                        catch (Exception innerEx)
                        {
                            DebugLog($"[WebChatObserver] OnInteractionCompleted processing error: {innerEx.Message}");
                        }
                    });
                }
                catch (Exception ex)
                {
                    DebugLog($"[WebChatObserver] OnInteractionCompleted error: {ex.Message}");
                }
            }

            /// <summary>
            /// Notifies that a tool call is being executed.
            /// Updates the status bar to reflect the ongoing tool name.
            /// </summary>
            /// <param name="toolCall">The tool call interaction.</param>
            public void OnToolCall(AIInteractionToolCall toolCall)
            {
                if (toolCall == null) return;

                // During streaming, do not append tool calls; just update status.
                RhinoApp.InvokeOnUiThread(() =>
                {
                    this._dialog.ExecuteScript($"setStatus({Newtonsoft.Json.JsonConvert.SerializeObject($"Calling tool: {toolCall.Name}")});");

                    // Mark a boundary so the next assistant text begins a new segment.
                    var turnKey = GetTurnBaseKey(toolCall?.TurnId);
                    DebugLog($"[WebChatObserver] OnToolCall: name={toolCall?.Name}, turnKey={turnKey} -> SetBoundaryFlag");
                    this.SetBoundaryFlag(turnKey);
                });
            }

            /// <summary>
            /// Notifies that a tool result was obtained.
            /// During streaming, rendering is deferred until the interaction is persisted.
            /// </summary>
            /// <param name="toolResult">The tool result interaction.</param>
            public void OnToolResult(AIInteractionToolResult toolResult)
            {
                if (toolResult == null) return;

                // Do not append tool results during streaming; they will be added on partial when persisted.

                // Mark a boundary immediately so subsequent assistant text starts a new segment (seg rollover happens on next delta).
                var turnKey = GetTurnBaseKey(toolResult?.TurnId);
                DebugLog($"[WebChatObserver] OnToolResult: name={toolResult?.Name}, id={toolResult?.Id}, turnKey={turnKey} -> SetBoundaryFlag");
                this.SetBoundaryFlag(turnKey);
            }

            /// <summary>
            /// Handles the final stable result after a conversation turn completes.
            /// Renders the final assistant message, removes the thinking bubble, and emits notifications.
            /// </summary>
            /// <param name="result">The final <see cref="AIReturn"/> for this turn.</param>
            public void OnFinal(AIReturn result)
            {
                RhinoApp.InvokeOnUiThread(() =>
                {
                    // Delegate history to ConversationSession; UI only emits notifications.
                    var historySnapshot = this._dialog._currentSession.GetHistoryReturn();
                    var lastReturn = this._dialog._currentSession.LastReturn;

                    try
                    {
                        // Determine final assistant item and its base stream key (turn:{TurnId}:assistant)
                        var finalAssistant = result?.Body?.Interactions?
                            .OfType<AIInteractionText>()
                            .LastOrDefault(i => i.Agent == AIAgent.Assistant);

                        string streamKey = null;
                        if (finalAssistant is IAIKeyedInteraction keyedFinal)
                        {
                            streamKey = keyedFinal.GetStreamKey();
                        }

                        // Mark this turn as finalized to prevent late partial/delta overrides
                        var turnKey = GetTurnBaseKey(finalAssistant?.TurnId);
                        if (!string.IsNullOrWhiteSpace(turnKey))
                        {
                            this._finalizedTextTurns.Add(turnKey);
                        }

                        // Prefer the aggregated streaming content for visual continuity
                        AIInteractionText aggregated = null;

                        // Use the current segmented key for the assistant stream
                        var segKey = !string.IsNullOrWhiteSpace(streamKey) ? this.GetCurrentSegmentedKey(streamKey) : null;
                        if (!string.IsNullOrWhiteSpace(segKey)
                            && this._streams.TryGetValue(segKey, out var st)
                            && st?.Aggregated is AIInteractionText agg
                            && !string.IsNullOrWhiteSpace(agg.Content))
                        {
                            aggregated = agg;
                        }

                        // Do not fallback to arbitrary previous streams to avoid cross-turn duplicates

                        // Merge final metrics/time/content into aggregated for the last render
                        if (aggregated != null && finalAssistant != null)
                        {
                            // CRITICAL: Update content to ensure final complete text is rendered (fixes missing last chunk issue)
                            if (!string.IsNullOrWhiteSpace(finalAssistant.Content))
                            {
                                aggregated.Content = finalAssistant.Content;
                            }

                            // Use aggregated turn metrics (includes tool calls, tool results, and assistant messages)
                            // This gives users an accurate picture of total token consumption for the turn
                            var turnId = finalAssistant.TurnId;
                            aggregated.Metrics = !string.IsNullOrWhiteSpace(turnId)
                                ? this._dialog._currentSession?.GetTurnMetrics(turnId) ?? finalAssistant.Metrics
                                : finalAssistant.Metrics;

                            aggregated.Time = finalAssistant.Time != default ? finalAssistant.Time : aggregated.Time;

                            // Ensure reasoning present on final render: prefer the provider's final reasoning
                            if (!string.IsNullOrWhiteSpace(finalAssistant.Reasoning))
                            {
                                aggregated.Reasoning = finalAssistant.Reasoning;
                            }
                        }

                        var toRender = aggregated ?? finalAssistant;

                        // For non-aggregated renders, also apply turn metrics if available
                        if (toRender != null && aggregated == null && finalAssistant != null)
                        {
                            var turnId = finalAssistant.TurnId;
                            toRender.Metrics = !string.IsNullOrWhiteSpace(turnId)
                                ? this._dialog._currentSession?.GetTurnMetrics(turnId) ?? finalAssistant.Metrics
                                : toRender.Metrics;
                        }

                        if (toRender != null)
                        {
                            // Prefer the segmented key only when a streaming aggregate exists.
                            // Otherwise (e.g., greetings or non-streamed finals), use the dedup key to avoid duplicates
                            // with the history replay that uses dedup keys.
                            string upsertKey;
                            if (!string.IsNullOrWhiteSpace(segKey) && aggregated != null)
                            {
                                upsertKey = segKey;
                            }
                            else if (toRender is IAIKeyedInteraction keyed)
                            {
                                // Use dedup key for non-streamed interactions
                                upsertKey = keyed.GetDedupKey() ?? keyed.GetStreamKey() ?? GetStreamKey(toRender);
                            }
                            else
                            {
                                upsertKey = GetStreamKey(toRender);
                            }

                            // Single final debug log for this interaction
                            var turnId = (toRender as AIInteractionText)?.TurnId ?? finalAssistant?.TurnId;
                            var length = (toRender as AIInteractionText)?.Content?.Length ?? 0;
                            DebugLog($"[WebChatObserver] Final render: turn={turnId}, key={upsertKey}, len={length}");
                            this._dialog.UpsertMessageByKey(upsertKey, toRender, source: "OnFinal");
                        }
                    }
                    catch (Exception repEx)
                    {
                        DebugLog($"[WebChatObserver] OnFinal finalize UI error: {repEx.Message}");
                    }

                    // Now that final assistant is rendered, remove the thinking bubble and set status
                    this.RemoveThinkingBubbleIfActive();

                    // Notify listeners with session-managed snapshots
                    this._dialog.ChatUpdated?.Invoke(this._dialog, historySnapshot);

                    // Clear streaming and per-turn state and finish
                    this._streams.Clear();
                    this._preStreamAggregates.Clear();
                    this._textInteractionSegments.Clear();
                    this._dialog.ResponseReceived?.Invoke(this._dialog, lastReturn);
                    this._dialog.ExecuteScript("setStatus('Ready'); setProcessing(false);");
                });
            }

            /// <summary>
            /// Handles an error raised during the conversation session.
            /// Renders an error message and updates the status bar accordingly.
            /// </summary>
            /// <param name="ex">The error that occurred.</param>
            public void OnError(Exception ex)
            {
                RhinoApp.InvokeOnUiThread(() =>
                {
                    try
                    {
                        // For all errors (including cancellations), render as an AIInteractionError (red-styled)
                        var isCancel = ex is OperationCanceledException;
                        var errInteraction = new AIInteractionError
                        {
                            // Agent is AIAgent.Error by default; content carries message
                            Content = isCancel ? "Cancelled." : (ex?.Message ?? "Unknown error"),
                        };

                        // Prefer keyed upsert for idempotent rendering and replay reliability
                        if (errInteraction is IAIKeyedInteraction keyed)
                        {
                            var key = keyed.GetDedupKey();
                            if (!string.IsNullOrWhiteSpace(key))
                            {
                                this._dialog.UpsertMessageByKey(key, errInteraction, source: "OnError");
                            }
                            else
                            {
                                this._dialog.AddInteractionToWebView(errInteraction);
                            }
                        }
                        else
                        {
                            this._dialog.AddInteractionToWebView(errInteraction);
                        }

                        // After rendering the error, update status and stop processing (removes loader via JS helper)
                        var status = isCancel ? "Cancelled" : "Error";
                        this._dialog.ExecuteScript($"setStatus('{status}'); setProcessing(false);");
                    }
                    catch (Exception uiEx)
                    {
                        DebugLog($"[WebChatObserver] OnError UI error: {uiEx.Message}");
                    }
                });
            }

            /// <summary>
            /// Computes a stream key to track independent streaming flows.
            /// Groups by interaction kind and identity to avoid collisions.
            /// </summary>
            private static string GetStreamKey(IAIInteraction interaction)
            {
                if (interaction is IAIKeyedInteraction keyed)
                {
                    return keyed.GetStreamKey();
                }

                return $"other:{interaction.GetType().Name}:{interaction.Agent}";
            }

            /// <summary>
            /// Returns true if enough time has elapsed since the last upsert for this key.
            /// </summary>
            private bool ShouldUpsertNow(string key)
            {
                try
                {
                    var now = DateTime.UtcNow;

                    var effectiveThrottleMs = now < this._dialog._deferDomUpdatesUntilUtc
                        ? ThrottleDuringMoveResizeMs
                        : ThrottleMs;

                    if (!this._lastUpsertAt.TryGetValue(key, out var last))
                    {
                        this._lastUpsertAt[key] = now;
                        return true;
                    }

                    if ((now - last).TotalMilliseconds >= effectiveThrottleMs)
                    {
                        this._lastUpsertAt[key] = now;
                        return true;
                    }
                }
                catch { }
                return false;
            }

            private bool ShouldRenderDelta(string domKey, AIInteractionText text)
            {
                if (string.IsNullOrWhiteSpace(domKey) || text == null)
                {
                    return false;
                }

                try
                {
                    var content = text.Content;
                    var reasoning = text.Reasoning;

                    if (this._lastRenderedTextByKey.TryGetValue(domKey, out var last)
                        && string.Equals(last.Content, content, StringComparison.Ordinal)
                        && string.Equals(last.Reasoning, reasoning, StringComparison.Ordinal))
                    {
                        return false;
                    }

                    this._lastRenderedTextByKey[domKey] = (content, reasoning);
                }
                catch
                {
                }

                return true;
            }

            /// <summary>
            /// Returns true when there is something to render (either answer content or reasoning).
            /// This allows live updates even when only reasoning has been streamed so far.
            /// </summary>
            private static bool HasRenderableText(AIInteractionText t)
            {
                return t != null && (!string.IsNullOrWhiteSpace(t.Content) || !string.IsNullOrWhiteSpace(t.Reasoning));
            }

            /// <summary>
            /// Sets the boundary flag for the next text interaction in the given turn.
            /// </summary>
            private void SetBoundaryFlag(string turnKey)
            {
                if (!string.IsNullOrWhiteSpace(turnKey))
                {
                    var wasAdded = this._pendingNewTextSegmentTurns.Add(turnKey);
                    DebugLog($"[WebChatObserver] SetBoundaryFlag: turnKey={turnKey}, wasNew={wasAdded}");
                }
            }

            /// <summary>
            /// Consumes the boundary flag and increments the segment counter if applicable.
            /// </summary>
            private void ConsumeBoundaryAndIncrementSegment(string turnKey, string baseKey)
            {
                if (!string.IsNullOrWhiteSpace(turnKey))
                {
                    var hadBoundary = this._pendingNewTextSegmentTurns.Remove(turnKey);
                    var hasSegment = this._textInteractionSegments.ContainsKey(baseKey);

                    if (hadBoundary && hasSegment)
                    {
                        var oldSeg = this._textInteractionSegments[baseKey];
                        this._textInteractionSegments[baseKey] = oldSeg + 1;
                        DebugLog($"[WebChatObserver] ConsumeBoundaryAndIncrementSegment: turnKey={turnKey}, baseKey={baseKey}, {oldSeg} -> {oldSeg + 1}");
                    }

#if DEBUG
                    else
                    {
                        DebugLog($"[WebChatObserver] ConsumeBoundaryAndIncrementSegment: turnKey={turnKey}, baseKey={baseKey}, hadBoundary={hadBoundary}, hasSegment={hasSegment}, NO INCREMENT");
                    }

#endif
                }
            }

            /// <summary>
            /// Computes the follow key for a tool result, which should appear immediately after its corresponding tool call.
            /// </summary>
            private static string GetFollowKeyForToolResult(AIInteractionToolResult tr)
            {
                try
                {
                    var id = !string.IsNullOrEmpty(tr?.Id) ? tr.Id : (tr?.Name ?? string.Empty);
                    if (!string.IsNullOrWhiteSpace(tr?.TurnId))
                    {
                        return $"turn:{tr.TurnId}:tool.call:{id}";
                    }

                    return $"tool.call:{id}";
                }
                catch { return null; }
            }
        }
    }
}
