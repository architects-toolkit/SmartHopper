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

            // Tracks UI state for the temporary thinking bubble.
            // We no longer track assistant-specific bubble state; ordering is handled by keys and upserts.
            private bool _thinkingBubbleActive;

            // Simple per-key throttling to reduce DOM churn during streaming
            private readonly Dictionary<string, DateTime> _lastUpsertAt = new Dictionary<string, DateTime>(StringComparer.Ordinal);
            private const int ThrottleMs = 10;

            // Tracks per-turn text segments so multiple text messages in a single turn
            // are rendered as distinct bubbles. Keys are the base stream key (e.g., "turn:{TurnId}:{agent}").
            private readonly Dictionary<string, int> _textInteractionSegments = new Dictionary<string, int>(StringComparer.Ordinal);

            // Per-turn state to implement segmentation boundaries (generic to any role)
            // - We only start a new text segment when:
            //   1) A new AIInteractionText arrives after another type of interaction in the same turn, or
            //   2) The role (agent) of the AIInteractionText differs from the last text role seen in the same turn.
            // Keys are generic turn keys in the form "turn:{TurnId}"; values track last seen type/agent.
            private readonly Dictionary<string, Type> _lastInteractionTypeByTurn = new Dictionary<string, Type>(StringComparer.Ordinal);
            private readonly Dictionary<string, AIAgent> _lastTextAgentByTurn = new Dictionary<string, AIAgent>();

            // Pending segmentation boundary flags per turn. When set, the next text interaction for that turn
            // starts a new segment (new bubble). These flags are set by OnInteractionCompleted when an interaction
            // (text or non-text) is finalized and persisted to history.
            private readonly HashSet<string> _pendingNewTextSegmentTurns = new HashSet<string>(StringComparer.Ordinal);

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
            /// Aggregates text message across streaming chunks. Handles both cumulative and incremental providers and avoids trimming.
            /// Rules:
            /// - First chunk: start stream, set aggregated = first content.
            /// - Subsequent chunks:
            ///   - If incoming starts with current -> provider is cumulative: replace with incoming.
            ///   - Else if current starts with incoming -> regression/noise: ignore to prevent trimming flicker.
            ///   - Else -> treat as incremental delta: append incoming to current.
            /// </summary>
            private static void CoalesceTextStreamChunk(AIInteractionText incoming, string key, ref StreamState state)
            {
                if (!state.Started || state.Aggregated is not AIInteractionText aggExisting)
                {
                    state.Started = true;
                    state.Aggregated = new AIInteractionText
                    {
                        Agent = incoming?.Agent ?? AIAgent.Assistant,
                        Content = incoming?.Content ?? string.Empty,

                        // Also capture any streamed reasoning content from the first chunk
                        Reasoning = incoming?.Reasoning ?? string.Empty,

                        // Preserve stream identity so GetStreamKey() stays as turn:{TurnId}
                        TurnId = incoming?.TurnId,

                        // Hide metrics while streaming to avoid showing 0/0 interim values.
                        // Final metrics will be applied in OnFinal when the response completes.
                        Metrics = null,
                        Time = incoming?.Time ?? DateTime.UtcNow,
                    };
                    return;
                }

                var current = aggExisting.Content ?? string.Empty;
                var incomingText = incoming?.Content ?? string.Empty;

                // Cumulative stream: incoming contains full text so far
                if (incomingText.Length >= current.Length && incomingText.StartsWith(current, StringComparison.Ordinal))
                {
                    aggExisting.Content = incomingText;
                    // Even if content is cumulative, we still want to coalesce reasoning below
                }
                else if (current.StartsWith(incomingText, StringComparison.Ordinal))
                {
                    // Regression/noise: ignore to avoid trimming visual content
                }
                else
                {
                    // Incremental delta: append
                    aggExisting.Content = current + incomingText;
                }

                // Now coalesce reasoning similarly (when providers stream thinking separately)
                var currentR = aggExisting.Reasoning ?? string.Empty;
                var incomingR = incoming?.Reasoning ?? string.Empty;
                if (!string.IsNullOrEmpty(incomingR))
                {
                    if (incomingR.Length >= currentR.Length && incomingR.StartsWith(currentR, StringComparison.Ordinal))
                    {
                        aggExisting.Reasoning = incomingR;
                    }
                    else if (currentR.StartsWith(incomingR, StringComparison.Ordinal))
                    {
                        // Regression/noise: keep existing
                    }
                    else
                    {
                        aggExisting.Reasoning = currentR + incomingR;
                    }
                }
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
                Debug.WriteLine("[WebChatObserver] OnStart called");
                RhinoApp.InvokeOnUiThread(() =>
                {
                    Debug.WriteLine("[WebChatObserver] OnStart: executing UI updates");
                    // Reset per-run state
                    this._streams.Clear();
                    this._textInteractionSegments.Clear();
                    this._lastInteractionTypeByTurn.Clear();
                    this._lastTextAgentByTurn.Clear();
                    this._pendingNewTextSegmentTurns.Clear();
                    this._finalizedTextTurns.Clear();
                    this._dialog.ExecuteScript("setStatus('Thinking...'); setProcessing(true);");

                    // Insert a persistent generic loading bubble that remains until stop state
                    this._dialog.ExecuteScript("addLoadingMessage('loading', 'Thinking…');");
                    this._thinkingBubbleActive = true;
                    // No assistant-specific state to reset
                    Debug.WriteLine("[WebChatObserver] OnStart: UI updates completed");
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
                    Debug.WriteLine($"[WebChatObserver] RemoveThinkingBubbleIfActive error: {ex.Message}");
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
                                // Determine segmented key for ANY text agent (assistant, user, system).
                                // Consume pending boundary set by OnInteractionCompleted to advance to a new segment.
                                var baseKey = GetStreamKey(interaction);
                                var turnKey = GetTurnBaseKey(tt?.TurnId);

                                // If this turn is finalized, ignore any late deltas to avoid overriding final metrics/time
                                if (!string.IsNullOrWhiteSpace(turnKey) && this._finalizedTextTurns.Contains(turnKey))
                                {
                                    return;
                                }

                                if (!string.IsNullOrWhiteSpace(turnKey) && this._pendingNewTextSegmentTurns.Remove(turnKey) && this._textInteractionSegments.ContainsKey(baseKey))
                                {
                                    this._textInteractionSegments[baseKey] = this._textInteractionSegments[baseKey] + 1;
                                }

                                var key = this.GetCurrentSegmentedKey(baseKey);
                                if (!this._streams.TryGetValue(key, out var state))
                                {
                                    state = new StreamState { Started = false, Aggregated = null };
                                }

                                // Coalesce text: detect cumulative vs incremental chunks and avoid regressions
                                CoalesceTextStreamChunk(tt, key, ref state);
                                this._streams[key] = state;

                                if (state.Aggregated is AIInteractionText aggText && HasRenderableText(aggText))
                                {
                                    if (this.ShouldUpsertNow(key))
                                    {
                                        this._dialog.UpsertMessageByKey(key, aggText, source: "OnDelta");
                                    }
                                }

                                return;
                            }
                        }
                        catch (Exception innerEx)
                        {
                            Debug.WriteLine($"[WebChatObserver] OnDelta processing error: {innerEx.Message}");
                        }
                    });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[WebChatObserver] OnDelta error: {ex.Message}");
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

                                if (!string.IsNullOrWhiteSpace(turnKey))
                                {
                                    this._pendingNewTextSegmentTurns.Add(turnKey);
                                }

                                // Use the current segmented key to match the streaming aggregate stored by OnDelta
                                var activeSegKey = this.GetCurrentSegmentedKey(baseKey);

                                // Streaming completion: update the existing aggregate and upsert in-place
                                if (this._streams.TryGetValue(activeSegKey, out var existingState) && existingState.Aggregated is AIInteractionText agg)
                                {
                                    agg.Content = tt.Content;
                                    agg.Reasoning = tt.Reasoning;
                                    agg.Time = tt.Time;
                                    this._dialog.UpsertMessageByKey(activeSegKey, agg, source: "OnInteractionCompletedStreamingFinal");
                                }
                                else
                                {
                                    // True non-streaming preview: create or reuse current segment and upsert once
                                    if (!this._textInteractionSegments.ContainsKey(baseKey))
                                    {
                                        this._textInteractionSegments[baseKey] = 1;
                                    }

                                    var segKey = this.GetCurrentSegmentedKey(baseKey);
                                    if (!this._streams.TryGetValue(segKey, out var state))
                                    {
                                        state = new StreamState { Started = false, Aggregated = null };
                                    }

                                    state.Aggregated = tt;
                                    this._streams[segKey] = state;
                                    this._dialog.UpsertMessageByKey(segKey, tt, source: "OnInteractionCompletedNonStreaming");
                                }

                                return;
                            }

                            // Stream-update non-text interactions immediately using their provided keys.
                            if (interaction is not AIInteractionText)
                            {
                                var streamKey = GetStreamKey(interaction);
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

                                // Record last interaction type for this turn to support segmentation
                                try
                                {
                                    var turnKey = GetTurnBaseKey(interaction?.TurnId);
                                    if (!string.IsNullOrWhiteSpace(turnKey))
                                    {
                                        this._lastInteractionTypeByTurn[turnKey] = interaction.GetType();

                                        // Mark boundary so the next text in this turn starts a new segment
                                        this._pendingNewTextSegmentTurns.Add(turnKey);
                                    }
                                }
                                catch { }
                            }
                        }
                        catch (Exception innerEx)
                        {
                            Debug.WriteLine($"[WebChatObserver] OnInteractionCompleted processing error: {innerEx.Message}");
                        }
                    });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[WebChatObserver] OnInteractionCompleted error: {ex.Message}");
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
                    var lastReturn = this._dialog._currentSession.GetReturn();

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
                        else
                        {
                            // Fallback: pick any aggregated assistant stream if key not found
                            foreach (var kv in this._streams)
                            {
                                if (kv.Value?.Aggregated is AIInteractionText agg2 && !string.IsNullOrWhiteSpace(agg2.Content))
                                {
                                    aggregated = agg2;
                                    if (string.IsNullOrWhiteSpace(segKey)) segKey = kv.Key;
                                    break;
                                }
                            }
                        }

                        // Merge final metrics/time into aggregated for the last render
                        if (aggregated != null && finalAssistant != null)
                        {
                            aggregated.Metrics = finalAssistant.Metrics;
                            aggregated.Time = finalAssistant.Time != default ? finalAssistant.Time : aggregated.Time;

                            // Ensure reasoning present on final render: prefer the provider's final reasoning
                            if (!string.IsNullOrWhiteSpace(finalAssistant.Reasoning))
                            {
                                aggregated.Reasoning = finalAssistant.Reasoning;
                            }
                        }

                        var toRender = aggregated ?? finalAssistant;
                        if (toRender != null)
                        {
                            // Prefer the segmented key to replace the streaming bubble deterministically
                            var upsertKey = segKey ?? (toRender as IAIKeyedInteraction)?.GetStreamKey() ?? GetStreamKey(toRender);
                            // Single final debug log for this interaction
                            var turnId = (toRender as AIInteractionText)?.TurnId ?? finalAssistant?.TurnId;
                            var length = (toRender as AIInteractionText)?.Content?.Length ?? 0;
                            Debug.WriteLine($"[WebChatObserver] Final render: turn={turnId}, key={upsertKey}, len={length}");
                            this._dialog.UpsertMessageByKey(upsertKey, toRender, source: "OnFinal");
                        }
                    }
                    catch (Exception repEx)
                    {
                        Debug.WriteLine($"[WebChatObserver] OnFinal finalize UI error: {repEx.Message}");
                    }

                    // Now that final assistant is rendered, remove the thinking bubble and set status
                    this.RemoveThinkingBubbleIfActive();

                    // Notify listeners with session-managed snapshots
                    this._dialog.ChatUpdated?.Invoke(this._dialog, historySnapshot);

                    // Clear streaming and per-turn state and finish
                    this._streams.Clear();
                    this._textInteractionSegments.Clear();
                    this._lastInteractionTypeByTurn.Clear();
                    this._lastTextAgentByTurn.Clear();
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
                        Debug.WriteLine($"[WebChatObserver] OnError UI error: {uiEx.Message}");
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
                    if (!this._lastUpsertAt.TryGetValue(key, out var last))
                    {
                        this._lastUpsertAt[key] = now;
                        return true;
                    }

                    if ((now - last).TotalMilliseconds >= ThrottleMs)
                    {
                        this._lastUpsertAt[key] = now;
                        return true;
                    }
                }
                catch { }
                return false;
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
