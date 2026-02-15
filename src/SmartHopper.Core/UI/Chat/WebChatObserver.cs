/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024-2026 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 *
 * This library is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this library; if not, see <https://www.gnu.org/licenses/lgpl-3.0.html>.
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

            /// <summary>
            /// Encapsulates all render state for a single turn, reducing scattered dictionary lookups.
            /// </summary>
            private sealed class TurnRenderState
            {
                public string TurnId { get; }

                public bool IsFinalized { get; set; }

                public bool HasPendingBoundary { get; set; }

                public Dictionary<string, SegmentState> Segments { get; } = new Dictionary<string, SegmentState>(StringComparer.Ordinal);

                public TurnRenderState(string turnId)
                {
                    this.TurnId = turnId;
                }

                public SegmentState GetOrCreateSegment(string baseKey)
                {
                    if (!this.Segments.TryGetValue(baseKey, out var seg))
                    {
                        seg = new SegmentState { SegmentNumber = 1 };
                        this.Segments[baseKey] = seg;
                    }

                    return seg;
                }
            }

            /// <summary>
            /// State for a single segment within a turn.
            /// </summary>
            private sealed class SegmentState
            {
                public int SegmentNumber { get; set; } = 1;

                public bool IsCommitted { get; set; }

                public StreamState StreamState { get; set; }

                public DateTime LastUpsertAt { get; set; }

                public (string? Content, string? Reasoning) LastRenderedText { get; set; }
            }

            // Turn-level state management (replaces scattered dictionaries for new turns)
            private readonly Dictionary<string, TurnRenderState> _turnStates = new Dictionary<string, TurnRenderState>(StringComparer.Ordinal);

            /// <summary>
            /// Gets or creates the render state for a turn.
            /// </summary>
            private TurnRenderState GetOrCreateTurnState(string turnKey)
            {
                if (string.IsNullOrWhiteSpace(turnKey))
                {
                    return null;
                }

                if (!this._turnStates.TryGetValue(turnKey, out var state))
                {
                    state = new TurnRenderState(turnKey);
                    this._turnStates[turnKey] = state;
                }

                return state;
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

                // Extract turn key from base key (e.g., "turn:abc123:assistant" -> "turn:abc123")
                var turnKey = this.ExtractTurnKeyFromBaseKey(baseKey);

                var turnState = this.GetOrCreateTurnState(turnKey);
                var segmentState = turnState.GetOrCreateSegment(baseKey);
                return $"{baseKey}:seg{segmentState.SegmentNumber}";
            }

            /// <summary>
            /// Extracts turn key from a base stream key (e.g., "turn:abc:assistant" -> "turn:abc").
            /// </summary>
            private string ExtractTurnKeyFromBaseKey(string baseKey)
            {
                if (string.IsNullOrWhiteSpace(baseKey))
                {
                    return null;
                }

                if (!baseKey.StartsWith("turn:", StringComparison.Ordinal))
                {
                    // Not a turn-keyed stream key; treat the key itself as a turn bucket.
                    return baseKey;
                }

                var parts = baseKey.Split(':');
                if (parts.Length >= 2)
                {
                    return $"{parts[0]}:{parts[1]}";
                }

                return baseKey;
            }

            /// <summary>
            /// Peeks at what the next segment key would be for a base key, without mutating state.
            /// If boundary is pending and segment exists, returns next segment; otherwise returns current or initial.
            /// </summary>
            private string PeekSegmentKey(string baseKey, string turnKey)
            {
                if (string.IsNullOrWhiteSpace(baseKey)) return baseKey;

                var extractedTurnKey = this.ExtractTurnKeyFromBaseKey(baseKey);
                var turnState = this.GetOrCreateTurnState(extractedTurnKey);
                var segment = turnState.GetOrCreateSegment(baseKey);
                int seg = segment.SegmentNumber;

                if (turnState.HasPendingBoundary)
                {
                    seg = seg + 1;
                }

                return $"{baseKey}:seg{seg}";
            }

            /// <summary>
            /// Commits the segment for a base key by consuming boundary and initializing/incrementing the segment counter.
            /// Call this only when you are about to render text for the first time or after a boundary.
            /// </summary>
            private void CommitSegment(string baseKey, string turnKey)
            {
                if (string.IsNullOrWhiteSpace(baseKey)) return;

                var extractedTurnKey = this.ExtractTurnKeyFromBaseKey(baseKey);
                var turnState = this.GetOrCreateTurnState(extractedTurnKey);
                var segment = turnState.GetOrCreateSegment(baseKey);
                var beforeSegment = segment.SegmentNumber;
                var hasBoundary = turnState.HasPendingBoundary;

                DebugLog($"[WebChatObserver] CommitSegment: baseKey={baseKey}, turnKey={turnKey}, beforeSeg={beforeSegment}, hasBoundary={hasBoundary}");

                // Consume boundary and increment if applicable
                if (hasBoundary)
                {
                    segment.SegmentNumber++;
                    turnState.HasPendingBoundary = false;
                    DebugLog($"[WebChatObserver] CommitSegment: incremented segment {baseKey} from {beforeSegment} to {segment.SegmentNumber}");
                }

                segment.IsCommitted = true;
            }

            // Tracks UI state for the temporary thinking bubble.
            // We no longer track assistant-specific bubble state; ordering is handled by keys and upserts.
            private bool _thinkingBubbleActive;

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

            // Pre-commit aggregates: tracks text aggregates by base key before segment assignment.
            // This allows lazy segment commitment only when text becomes renderable.
            private readonly Dictionary<string, StreamState> _preStreamAggregates = new Dictionary<string, StreamState>(StringComparer.Ordinal);

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
                    this._turnStates.Clear();
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

                                var turnState = this.GetOrCreateTurnState(turnKey);
                                var segState = turnState.GetOrCreateSegment(baseKey);

                                // If this turn is finalized, ignore any late deltas to avoid overriding final metrics/time
                                if (turnState.IsFinalized)
                                {
                                    return;
                                }

                                // Check if we already have a committed segment for this base key
                                bool isCommitted = segState.IsCommitted;
                                bool hasBoundary = turnState.HasPendingBoundary;
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

                                var turnState = this.GetOrCreateTurnState(turnKey);
                                var segState = turnState.GetOrCreateSegment(baseKey);

                                // If this turn is finalized, ignore any late partials to avoid overriding final metrics/time
                                if (turnState.IsFinalized)
                                {
                                    return;
                                }

                                // Check if segment is committed
                                bool isCommitted = segState.IsCommitted;
                                var activeSegKey = isCommitted ? this.GetCurrentSegmentedKey(baseKey) : null;
                                var hasBoundary = turnState.HasPendingBoundary;
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

                                // Flush any pending text state for this turn before processing non-text interaction.
                                // This ensures throttled text deltas are rendered before tool calls appear.
                                this.FlushPendingTextStateForTurn(turnKey);
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
            /// Renders the final message, removes the thinking bubble, and emits notifications.
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
                        // Determine the final renderable interaction (can be assistant text, tool, image, error, etc.)
                        var finalRenderable = historySnapshot?.Body?.Interactions?
                            .LastOrDefault(i => i is IAIRenderInteraction && i.Agent != AIAgent.Context);

                        string streamKey = null;
                        if (finalRenderable is IAIKeyedInteraction keyedFinal)
                        {
                            streamKey = keyedFinal.GetStreamKey();
                        }

                        // Mark this turn as finalized to prevent late partial/delta overrides
                        var turnKey = GetTurnBaseKey(finalRenderable?.TurnId);
                        var turnState = this.GetOrCreateTurnState(turnKey);
                        if (turnState != null)
                        {
                            turnState.IsFinalized = true;
                        }

                        // Prefer the aggregated streaming content for visual continuity (when available)
                        IAIInteraction aggregated = null;
                        string segKey = null;
                        if (!string.IsNullOrWhiteSpace(streamKey))
                        {
                            segKey = this.GetCurrentSegmentedKey(streamKey);
                            if (!string.IsNullOrWhiteSpace(segKey) && this._streams.TryGetValue(segKey, out var st))
                            {
                                aggregated = st?.Aggregated;
                            }
                        }

                        // Merge final metrics/time/content into aggregated for the last render (only for assistant text)
                        if (aggregated is AIInteractionText aggregatedText && finalRenderable is AIInteractionText finalText)
                        {
                            if (!string.IsNullOrWhiteSpace(finalText.Content))
                            {
                                aggregatedText.Content = finalText.Content;
                            }

                            var turnId = finalText.TurnId;
                            aggregatedText.Metrics = !string.IsNullOrWhiteSpace(turnId)
                                ? this._dialog._currentSession?.GetTurnMetrics(turnId) ?? finalText.Metrics
                                : finalText.Metrics;

                            aggregatedText.Time = finalText.Time != default ? finalText.Time : aggregatedText.Time;

                            if (!string.IsNullOrWhiteSpace(finalText.Reasoning))
                            {
                                aggregatedText.Reasoning = finalText.Reasoning;
                            }
                        }

                        var toRender = aggregated ?? finalRenderable;

                        // For non-aggregated renders, apply turn metrics only when the final bubble is assistant text
                        if (toRender is AIInteractionText toRenderText && aggregated == null && toRenderText.Agent == AIAgent.Assistant)
                        {
                            var turnId = toRenderText.TurnId;
                            toRenderText.Metrics = !string.IsNullOrWhiteSpace(turnId)
                                ? this._dialog._currentSession?.GetTurnMetrics(turnId) ?? toRenderText.Metrics
                                : toRenderText.Metrics;
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
                            var turnId = toRender?.TurnId;
                            var length = (toRender as AIInteractionText)?.Content?.Length ?? 0;
                            DebugLog($"[WebChatObserver] Final render: type={toRender?.GetType().Name}, turn={turnId}, key={upsertKey}, len={length}");
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

                    var turnKey = this.ExtractTurnKeyFromBaseKey(key);
                    var turnState = this.GetOrCreateTurnState(turnKey);
                    var segment = turnState.GetOrCreateSegment(key);

                    if (segment.LastUpsertAt == default || (now - segment.LastUpsertAt).TotalMilliseconds >= effectiveThrottleMs)
                    {
                        segment.LastUpsertAt = now;
                        return true;
                    }

                    return false;
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

                    var turnKey = this.ExtractTurnKeyFromBaseKey(domKey);
                    var turnState = this.GetOrCreateTurnState(turnKey);
                    var segment = turnState.GetOrCreateSegment(domKey);

                    var last = segment.LastRenderedText;
                    if (string.Equals(last.Content, content, StringComparison.Ordinal)
                        && string.Equals(last.Reasoning, reasoning, StringComparison.Ordinal))
                    {
                        return false;
                    }

                    segment.LastRenderedText = (content, reasoning);
                    return true;
                }
                catch (Exception ex)
                {
                    DebugLog($"[WebChatObserver] ShouldRenderDelta error: {ex.Message}");
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
            /// Flushes any pending (throttled) text state for a turn to ensure final content is rendered.
            /// Called before processing non-text interactions to prevent losing throttled text deltas.
            /// </summary>
            private void FlushPendingTextStateForTurn(string turnKey)
            {
                if (string.IsNullOrWhiteSpace(turnKey))
                {
                    return;
                }

                try
                {
                    // Find all streams for this turn and force-render any that have dirty state
                    var turnPrefix = turnKey + ":";
                    foreach (var kv in this._streams)
                    {
                        var streamKey = kv.Key;
                        if (string.IsNullOrWhiteSpace(streamKey) || !streamKey.StartsWith(turnPrefix, StringComparison.Ordinal))
                        {
                            continue;
                        }

                        if (kv.Value?.Aggregated is AIInteractionText aggregatedText && HasRenderableText(aggregatedText) && this.ShouldRenderDelta(streamKey, aggregatedText))
                        {
                            DebugLog($"[WebChatObserver] FlushPendingTextStateForTurn: flushing streamKey={streamKey}");

                            this._dialog.UpsertMessageByKey(streamKey, aggregatedText, source: "FlushPendingText");
                        }
                    }
                }
                catch (Exception ex)
                {
                    DebugLog($"[WebChatObserver] FlushPendingTextStateForTurn error: {ex.Message}");
                }
            }

            /// <summary>
            /// Sets the boundary flag for the next text interaction in the given turn.
            /// </summary>
            private void SetBoundaryFlag(string turnKey)
            {
                if (!string.IsNullOrWhiteSpace(turnKey))
                {
                    var turnState = this.GetOrCreateTurnState(turnKey);
                    turnState.HasPendingBoundary = true;
                    DebugLog($"[WebChatObserver] SetBoundaryFlag: turnKey={turnKey}");
                }
            }

            /// <summary>
            /// Consumes the boundary flag and increments the segment counter if applicable.
            /// </summary>
            private void ConsumeBoundaryAndIncrementSegment(string turnKey, string baseKey)
            {
                if (!string.IsNullOrWhiteSpace(turnKey))
                {
                    var turnState = this.GetOrCreateTurnState(turnKey);
                    var segment = turnState.GetOrCreateSegment(baseKey);

                    if (turnState.HasPendingBoundary && segment.IsCommitted)
                    {
                        var oldSeg = segment.SegmentNumber;
                        segment.SegmentNumber = oldSeg + 1;
                        turnState.HasPendingBoundary = false;
                        DebugLog($"[WebChatObserver] ConsumeBoundaryAndIncrementSegment: turnKey={turnKey}, baseKey={baseKey}, {oldSeg} -> {oldSeg + 1}");
                    }
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
