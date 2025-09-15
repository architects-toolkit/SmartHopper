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

            private readonly Dictionary<string, StreamState> _streams = new Dictionary<string, StreamState>(StringComparer.Ordinal);

            public WebChatObserver(WebChatDialog dialog)
            {
                _dialog = dialog;
            }

            public void OnStart(AIRequestCall request)
            {
                Debug.WriteLine("[WebChatObserver] OnStart called");
                RhinoApp.InvokeOnUiThread(() =>
                {
                    Debug.WriteLine("[WebChatObserver] OnStart: executing UI updates");
                    _dialog.ExecuteScript("setStatus('Thinking...'); setProcessing(true);");

                    // Insert a temporary loading bubble for assistant to be replaced on first content
                    _dialog.ExecuteScript("addLoadingMessage('assistant', 'Thinking…');");
                    Debug.WriteLine("[WebChatObserver] OnStart: UI updates completed");
                });
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

                            // Update the streaming content
                            var key = GetStreamKey(interaction);
                            if (!_streams.TryGetValue(key, out var state))
                            {
                                state = new StreamState { Started = false, Aggregated = null };
                            }

                            if (!state.Started)
                            {
                                // First delta: replace the temporary loading bubble
                                state.Started = true;
                                state.Aggregated = interaction;
                                this._streams[key] = state;
                                this._dialog.ReplaceLastMessageByRole(interaction.Agent, state.Aggregated);
                            }
                            else
                            {
                                // Subsequent deltas: update existing bubble
                                state.Aggregated = interaction;
                                this._streams[key] = state;
                                this._dialog.ReplaceLastMessageByRole(interaction.Agent, state.Aggregated);
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
                            // Compute a stable stream key to isolate concurrent streams per kind (text/toolcall/toolresult)
                            var key = GetStreamKey(interaction);
                            if (!_streams.TryGetValue(key, out var state))
                            {
                                state = new StreamState { Started = false, Aggregated = null };
                            }

                            if (interaction is AIInteractionText tt && tt.Agent == AIAgent.Assistant)
                            {
                                if (!state.Started)
                                {
                                    // First assistant chunk: replace the temporary loading bubble
                                    state.Started = true;
                                    state.Aggregated = interaction;
                                    _streams[key] = state;
                                    _dialog.ReplaceLastMessageByRole(interaction.Agent, state.Aggregated);
                                }
                                else
                                {
                                    // Subsequent assistant chunks: update existing bubble
                                    state.Aggregated = interaction;
                                    _streams[key] = state;
                                    _dialog.ReplaceLastMessageByRole(interaction.Agent, state.Aggregated);
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
                if (!TryAdd(toolCall)) return;

                RhinoApp.InvokeOnUiThread(() =>
                {
                    _dialog.AddToolCallMessage(toolCall);
                    _dialog.ExecuteScript($"setStatus({Newtonsoft.Json.JsonConvert.SerializeObject($"Calling tool: {toolCall.Name}")});");
                });
            }

            public void OnToolResult(AIInteractionToolResult toolResult)
            {
                if (toolResult == null) return;
                if (!TryAdd(toolResult)) return;

                RhinoApp.InvokeOnUiThread(() =>
                {
                    _dialog.AddToolResultMessage(toolResult);
                });
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

                        // Replace the loading bubble with final assistant content if available
                        try
                        {
                            var finalAssistant = result?.Body?.Interactions?
                                .OfType<AIInteractionText>()
                                .LastOrDefault(i => i.Agent == AIAgent.Assistant);
                            if (finalAssistant != null)
                            {
                                this._dialog.ReplaceLastMessageByRole(AIAgent.Assistant, finalAssistant);
                            }
                            else
                            {
                                // No assistant text produced: remove any remaining assistant loading bubble
                                this._dialog.ExecuteScript("removeLastLoadingMessageByRole('assistant');");
                            }
                        }
                        catch (Exception repEx)
                        {
                            Debug.WriteLine($"[WebChatObserver] OnFinal replace/remove loading bubble error: {repEx.Message}");
                        }

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
                switch (interaction)
                {
                    case AIInteractionToolResult tr:
                        {
                            var id = !string.IsNullOrEmpty(tr.Id) ? tr.Id : tr.Name ?? string.Empty;
                            return $"tool.result:{id}";
                        }

                    case AIInteractionToolCall tc:
                        {
                            var id = !string.IsNullOrEmpty(tc.Id) ? tc.Id : tc.Name ?? string.Empty;
                            return $"tool.call:{id}";
                        }

                    case AIInteractionText tt:
                        {
                            // One active text stream per agent
                            return $"text:{tt.Agent}";
                        }

                    default:
                        {
                            return $"other:{interaction.GetType().Name}:{interaction.Agent}";
                        }
                }
            }

            private static string MakeKey(IAIInteraction interaction)
            {
                switch (interaction)
                {
                    case AIInteractionToolResult tr:
                        {
                            var id = !string.IsNullOrEmpty(tr.Id) ? tr.Id : tr.Name ?? string.Empty;
                            var res = (tr.Result != null ? tr.Result.ToString() : string.Empty).Trim();
                            return $"tool.result:{id}:{res}";
                        }
                    case AIInteractionToolCall tc:
                        {
                            var id = !string.IsNullOrEmpty(tc.Id) ? tc.Id : tc.Name ?? string.Empty;
                            var args = (tc.Arguments != null ? tc.Arguments.ToString() : string.Empty).Trim();
                            return $"tool.call:{id}:{args}";
                        }
                    case AIInteractionText tt:
                        {
                            var agent = tt.Agent.ToString();
                            var content = (tt.Content ?? string.Empty).Trim();
                            return $"text:{agent}:{content}";
                        }
                    default:
                        {
                            var agent = interaction.Agent.ToString();
                            var time = interaction.Time.ToString("o");
                            return $"other:{interaction.GetType().Name}:{agent}:{time}";
                        }
                }
            }
        }
    }
}

