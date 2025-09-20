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
            private readonly HashSet<string> _seen = new HashSet<string>(StringComparer.Ordinal);

            // Centralized streaming state per run (role-agnostic)
            private sealed class StreamState
            {
                public bool Started;
                public IAIInteraction Aggregated;
            }

            // Tracks UI state for the temporary thinking bubble and whether we've already
            // appended an assistant message to the chat. This ensures correct ordering:
            // tool calls/results first, assistant response appended at the end.
            private bool _thinkingBubbleActive;
            private bool _assistantBubbleAdded;

        /// <summary>
        /// Aggregates assistant text across streaming chunks. Handles both cumulative and incremental providers and avoids trimming.
        /// Rules:
        /// - First chunk: start stream, set aggregated = first content.
        /// - Subsequent chunks:
        ///   - If incoming starts with current -> provider is cumulative: replace with incoming.
        ///   - Else if current starts with incoming -> regression/noise: ignore to prevent trimming flicker.
        ///   - Else -> treat as incremental delta: append incoming to current.
        /// </summary>
        private static void CoalesceAssistantTextChunk(AIInteractionText incoming, string key, ref StreamState state)
        {
            if (!state.Started || state.Aggregated is not AIInteractionText aggExisting)
            {
                state.Started = true;
                state.Aggregated = new AIInteractionText
                {
                    Agent = AIAgent.Assistant,
                    Content = incoming?.Content ?? string.Empty,
                    // Hide metrics while streaming to avoid showing 0/0 interim values.
                    // Final metrics will be applied in OnFinal when the response completes.
                    Metrics = null,
                    Time = incoming?.Time ?? DateTime.UtcNow,
                };
                return;
            }

            var current = aggExisting.Content ?? string.Empty;
            var incomingText = incoming?.Content ?? string.Empty;

            if (string.IsNullOrEmpty(incomingText))
            {
                // Nothing to add; keep existing
                return;
            }

            // Cumulative stream: incoming contains full text so far
            if (incomingText.Length >= current.Length && incomingText.StartsWith(current, StringComparison.Ordinal))
            {
                aggExisting.Content = incomingText;
                return;
            }

            // Regression/noise: ignore to avoid trimming visual content
            if (current.StartsWith(incomingText, StringComparison.Ordinal))
            {
                return;
            }

            // Incremental delta: append
            aggExisting.Content = current + incomingText;
        }

            private readonly Dictionary<string, StreamState> _streams = new Dictionary<string, StreamState>(StringComparer.Ordinal);

            public WebChatObserver(WebChatDialog dialog)
            {
                _dialog = dialog;
                _thinkingBubbleActive = false;
                _assistantBubbleAdded = false;
            }

            public void OnStart(AIRequestCall request)
            {
                Debug.WriteLine("[WebChatObserver] OnStart called");
                RhinoApp.InvokeOnUiThread(() =>
                {
                    Debug.WriteLine("[WebChatObserver] OnStart: executing UI updates");
                    _dialog.ExecuteScript("setStatus('Thinking...'); setProcessing(true);");
                    
                    // Insert a persistent generic loading bubble that remains until stop state
                    _dialog.ExecuteScript("addLoadingMessage('loading', 'Thinking…');");
                    _thinkingBubbleActive = true;
                    _assistantBubbleAdded = false;
                    Debug.WriteLine("[WebChatObserver] OnStart: UI updates completed");
                });
            }

            /// <summary>
            /// Removes the persistent thinking bubble if it is currently visible.
            /// Must be called on the UI thread.
            /// </summary>
            private void RemoveThinkingBubbleIfActive()
            {
                if (!_thinkingBubbleActive)
                {
                    return;
                }
                try
                {
                    // Remove the generic loader via JS helper
                    _dialog.ExecuteScript("removeThinkingMessage();");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[WebChatObserver] RemoveThinkingBubbleIfActive error: {ex.Message}");
                }
                finally
                {
                    _thinkingBubbleActive = false;
                }
            }

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
                            // For delta updates, only handle assistant text content
                            if (interaction is not AIInteractionText tt || tt.Agent != AIAgent.Assistant)
                            {
                                return;
                            }

                            var key = GetStreamKey(interaction);
                            if (!_streams.TryGetValue(key, out var state))
                            {
                                state = new StreamState { Started = false, Aggregated = null };
                            }

                            // Coalesce text: detect cumulative vs incremental chunks and avoid regressions
                            CoalesceAssistantTextChunk(tt, key, ref state);

                            this._streams[key] = state;
                            if (state.Aggregated is AIInteractionText aggText && !string.IsNullOrWhiteSpace(aggText.Content))
                            {
                                // Upsert by stream key to ensure single bubble across deltas
                                this._dialog.UpsertMessageByKey(key, aggText);
                                _assistantBubbleAdded = true;
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

            public void OnPartial(IAIInteraction interaction)
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

                            // Compute a stable stream key to isolate concurrent streams per kind (text/toolcall/toolresult)
                            var key = GetStreamKey(interaction);
                            if (!_streams.TryGetValue(key, out var state))
                            {
                                state = new StreamState { Started = false, Aggregated = null };
                            }

                            if (interaction is AIInteractionText tt && tt.Agent == AIAgent.Assistant)
                            {
                                // Coalesce text across partials
                                CoalesceAssistantTextChunk(tt, key, ref state);

                                _streams[key] = state;
                                if (state.Aggregated is AIInteractionText aggText && !string.IsNullOrWhiteSpace(aggText.Content))
                                {
                                    // Upsert assistant bubble by stream key
                                    _dialog.UpsertMessageByKey(key, aggText);
                                    _assistantBubbleAdded = true;
                                }
                            }

                            // For non-assistant interactions, only add them once they have been persisted
                            // to the session history (post-streaming final block). Ignore Context agent.
                            if (!(interaction is AIInteractionText at && at.Agent == AIAgent.Assistant))
                            {
                                if (interaction.Agent != AIAgent.Context && IsPersisted(interaction))
                                {
                                    if (TryAdd(interaction))
                                    {
                                        if (interaction is IAIKeyedInteraction keyedPersisted)
                                        {
                                            var dedupKey = keyedPersisted.GetDedupKey();
                                            if (!string.IsNullOrWhiteSpace(dedupKey))
                                            {
                                                _dialog.UpsertMessageByKey(dedupKey, interaction);
                                            }
                                            else
                                            {
                                                _dialog.AddInteractionToWebView(interaction);
                                            }
                                        }
                                        else
                                        {
                                            _dialog.AddInteractionToWebView(interaction);
                                        }
                                    }
                                }
                            }

                            // Optional UX: surface tool call name in status
                            if (interaction is AIInteractionToolCall call)
                            {
                                _dialog.ExecuteScript($"setStatus({Newtonsoft.Json.JsonConvert.SerializeObject($"Calling tool: {call.Name}")});");
                            }
                        }
                        catch (Exception innerEx)
                        {
                            Debug.WriteLine($"[WebChatObserver] OnPartial processing error: {innerEx.Message}");
                        }
                    });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[WebChatObserver] OnPartial error: {ex.Message}");
                }
            }

            public void OnToolCall(AIInteractionToolCall toolCall)
            {
                if (toolCall == null) return;
                // During streaming, do not append tool calls; just update status.
                RhinoApp.InvokeOnUiThread(() =>
                {
                    _dialog.ExecuteScript($"setStatus({Newtonsoft.Json.JsonConvert.SerializeObject($"Calling tool: {toolCall.Name}")});");
                });
            }

            public void OnToolResult(AIInteractionToolResult toolResult)
            {
                if (toolResult == null) return;
                // Do not append tool results during streaming; they will be added on partial when persisted.
            }

            public void OnFinal(AIReturn result)
            {
                Debug.WriteLine($"[WebChatObserver] OnFinal: {result?.Body?.Interactions?.Count ?? 0} interactions, {result?.Body?.GetNewInteractions().Count ?? 0} new ones");

                RhinoApp.InvokeOnUiThread(() =>
                {
                    try
                    {
                        // Delegate history to ConversationSession; UI only emits notifications.
                        var historySnapshot = this._dialog._currentSession.GetHistoryReturn();
                        var lastReturn = this._dialog._currentSession.GetReturn();

                        try
                        {
                            // Determine streaming state and final assistant data
                            var assistantKey = "text:" + AIAgent.Assistant.ToString();
                            AIInteractionText aggregated = null;
                            if (this._streams.TryGetValue(assistantKey, out var state) && state?.Aggregated is AIInteractionText agg && !string.IsNullOrWhiteSpace(agg.Content))
                            {
                                aggregated = agg;
                            }

                            var finalAssistant = result?.Body?.Interactions?
                                .OfType<AIInteractionText>()
                                .LastOrDefault(i => i.Agent == AIAgent.Assistant);

                            // Merge metrics/time and upsert by key to avoid duplicates
                            if (aggregated != null && finalAssistant != null)
                            {
                                aggregated.Metrics = finalAssistant.Metrics;
                                aggregated.Time = finalAssistant.Time != default ? finalAssistant.Time : aggregated.Time;
                            }

                            var toRender = aggregated ?? finalAssistant;
                            if (toRender != null)
                            {
                                this._dialog.UpsertMessageByKey(assistantKey, toRender);
                                _assistantBubbleAdded = true;
                            }
                        }
                        catch (Exception repEx)
                        {
                            Debug.WriteLine($"[WebChatObserver] OnFinal finalize UI error: {repEx.Message}");
                        }

                        // Now that final assistant is rendered, remove the thinking bubble and set status
                        RemoveThinkingBubbleIfActive();

                        // Notify listeners with session-managed snapshots
                        this._dialog.ChatUpdated?.Invoke(this._dialog, historySnapshot);

                        // Clear streaming state and finish
                        this._streams.Clear();
                        this._dialog.ResponseReceived?.Invoke(this._dialog, lastReturn);
                        this._dialog.ExecuteScript("setStatus('Ready'); setProcessing(false);");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[WebChatObserver] OnFinal UI error: {ex.Message}");
                    }
                });
            }

            public void OnError(Exception ex)
            {
                RhinoApp.InvokeOnUiThread(() =>
                {
                    try
                    {
                        // Ensure the persistent thinking bubble is removed on any terminal error/cancel
                        RemoveThinkingBubbleIfActive();

                        if (ex is OperationCanceledException)
                        {
                            this._dialog.AddSystemMessage("Cancelled.", "info");
                            this._dialog.ExecuteScript("setStatus('Cancelled'); setProcessing(false);");
                        }
                        else
                        {
                            this._dialog.AddSystemMessage($"Error: {ex.Message}", "error");
                            this._dialog.ExecuteScript("setStatus('Error'); setProcessing(false);");
                        }
                    }
                    catch (Exception uiEx)
                    {
                        Debug.WriteLine($"[WebChatObserver] OnError UI error: {uiEx.Message}");
                    }
                });
            }

            private bool TryAdd(IAIInteraction interaction)
            {
                var key = MakeKey(interaction);
                if (key == null) return false;
                return _seen.Add(key);
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

            private static string MakeKey(IAIInteraction interaction)
            {
                if (interaction is IAIKeyedInteraction keyed)
                {
                    return keyed.GetDedupKey();
                }
                var agent = interaction.Agent.ToString();
                var time = interaction.Time.ToString("o");
                return $"other:{interaction.GetType().Name}:{agent}:{time}";
            }

            /// <summary>
            /// Determines whether an interaction has been persisted into the session history.
            /// This is used to gate non-assistant partial UI updates so we only add interactions
            /// after they become part of the conversation history (post-streaming snapshot).
            /// </summary>
            private bool IsPersisted(IAIInteraction interaction)
            {
                try
                {
                    var history = this._dialog._currentSession?.GetHistoryInteractionList();
                    if (history == null || history.Count == 0)
                        return false;

                    switch (interaction)
                    {
                        case AIInteractionToolResult tr:
                            {
                                var id = !string.IsNullOrEmpty(tr.Id) ? tr.Id : tr.Name ?? string.Empty;
                                return history.OfType<AIInteractionToolResult>().Any(h => (!string.IsNullOrEmpty(h.Id) ? h.Id : h.Name ?? string.Empty) == id);
                            }
                        case AIInteractionToolCall tc:
                            {
                                var id = !string.IsNullOrEmpty(tc.Id) ? tc.Id : tc.Name ?? string.Empty;
                                return history.OfType<AIInteractionToolCall>().Any(h => (!string.IsNullOrEmpty(h.Id) ? h.Id : h.Name ?? string.Empty) == id);
                            }
                        case AIInteractionText t when t.Agent != AIAgent.Assistant:
                            {
                                var content = t.Content ?? string.Empty;
                                var agent = t.Agent;
                                return history.OfType<AIInteractionText>().Any(h => h.Agent == agent && string.Equals(h.Content ?? string.Empty, content, StringComparison.Ordinal));
                            }
                        default:
                            return history.Any(h => ReferenceEquals(h, interaction));
                    }
                }
                catch
                {
                    return false;
                }
            }
        }
    }
}

