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
using Eto.Forms;
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
                Application.Instance.AsyncInvoke(() =>
                {
                    _dialog._statusLabel.Text = "Thinking...";
                    _dialog._progressBar.Visible = true;
                    _dialog._cancelButton.Enabled = true;
                });
            }

            public void OnPartial(AIReturn delta)
            {
                try
                {
                    var interactions = delta?.Body?.Interactions;
                    if (interactions == null || interactions.Count == 0)
                        return;

                    Application.Instance.AsyncInvoke(() =>
                    {
                        foreach (var inter in interactions)
                        {
                            try
                            {
                                // Compute a stable stream key to isolate concurrent streams per kind (text/toolcall/toolresult)
                                var key = GetStreamKey(inter);
                                if (!_streams.TryGetValue(key, out var state))
                                {
                                    state = new StreamState { Started = false, Aggregated = null };
                                }

                                if (!state.Started)
                                {
                                    // First chunk: pre-aggregate and create the initial bubble with that content
                                    state.Started = true;
                                    state.Aggregated = inter;
                                    _streams[key] = state;
                                    _dialog.AddInteractionToWebView(state.Aggregated);
                                }
                                else
                                {
                                    // Subsequent chunks: merge and replace existing bubble
                                    state.Aggregated = inter;
                                    _streams[key] = state;
                                    _dialog.ReplaceLastMessageByRole(inter.Agent, state.Aggregated);
                                }

                                // Optional UX: surface tool call name in status
                                if (inter is AIInteractionToolCall call)
                                {
                                    _dialog._statusLabel.Text = $"Calling tool: {call.Name}";
                                }
                            }
                            catch (Exception innerEx)
                            {
                                Debug.WriteLine($"[WebChatObserver] OnPartial item error: {innerEx.Message}");
                            }
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

                Application.Instance.AsyncInvoke(() =>
                {
                    _dialog.AddToolCallMessage(toolCall);
                    _dialog._statusLabel.Text = $"Calling tool: {toolCall.Name}";
                });
            }

            public void OnToolResult(AIInteractionToolResult toolResult)
            {
                if (toolResult == null) return;
                if (!TryAdd(toolResult)) return;

                Application.Instance.AsyncInvoke(() =>
                {
                    _dialog.AddToolResultMessage(toolResult);
                });
            }

            public void OnFinal(AIReturn result)
            {
                Application.Instance.AsyncInvoke(() =>
                {
                    try
                    {
                        // Final interactions and metrics
                        var interactions = result?.Body?.Interactions;
                        var finalMetrics = result?.Metrics;

                        // 1) Use the provider's final assistant snapshot directly (no concatenation)
                        AIInteractionText finalAssistant = null;
                        try
                        {
                            if (interactions != null && interactions.Count > 0)
                            {
                                for (int i = interactions.Count - 1; i >= 0; i--)
                                {
                                    var it = interactions[i];
                                    if (it is AIInteractionText tt && tt.Agent == AIAgent.Assistant)
                                    {
                                        finalAssistant = new AIInteractionText
                                        {
                                            Agent = AIAgent.Assistant,
                                            Content = tt.Content,
                                            Reasoning = tt.Reasoning,
                                            Metrics = tt.Metrics,
                                        };
                                        break;
                                    }
                                }
                            }
                        }
                        catch (Exception buildEx)
                        {
                            Debug.WriteLine($"[WebChatObserver] OnFinal consolidation error: {buildEx.Message}");
                        }

                        // 2) Attach metrics to the final assistant or to the last assistant in history if no text was present
                        if (finalAssistant != null)
                        {
                            if (finalMetrics != null)
                            {
                                try
                                {
                                    if (finalAssistant.Metrics != null)
                                        finalAssistant.Metrics.Combine(finalMetrics);
                                    else
                                        finalAssistant.Metrics = finalMetrics;
                                }
                                catch (Exception mergeEx)
                                {
                                    Debug.WriteLine($"[WebChatObserver] OnFinal metrics merge error: {mergeEx.Message}");
                                }
                            }

                            // Render full assistant text and persist only once
                            _dialog.ReplaceLastMessageByRole(AIAgent.Assistant, finalAssistant);

                            // Persist into conversation state via _lastReturn snapshot
                            var current = _dialog._lastReturn?.Body?.Interactions ?? new List<IAIInteraction>();
                            var updated = new List<IAIInteraction>(current);

                            int lastIdx = -1;
                            for (int i = updated.Count - 1; i >= 0; i--)
                            {
                                if (updated[i]?.Agent == AIAgent.Assistant)
                                {
                                    lastIdx = i;
                                    break;
                                }
                            }

                            if (lastIdx >= 0)
                                updated[lastIdx] = finalAssistant;
                            else
                                updated.Add(finalAssistant);

                            var snapshot = new AIReturn();
                            snapshot.CreateSuccess(updated, _dialog._initialRequest);
                            _dialog._lastReturn = snapshot;
                        }
                        else if (finalMetrics != null)
                        {
                            // No assistant text in final: merge metrics into last assistant already shown
                            try
                            {
                                var current = _dialog._lastReturn?.Body?.Interactions ?? new List<IAIInteraction>();
                                var updated = new List<IAIInteraction>(current);
                                for (int i = updated.Count - 1; i >= 0; i--)
                                {
                                    var inter = updated[i];
                                    if (inter != null && inter.Agent == AIAgent.Assistant)
                                    {
                                        if (inter.Metrics != null)
                                            inter.Metrics.Combine(finalMetrics);
                                        else
                                            inter.Metrics = finalMetrics;

                                        _dialog.ReplaceLastMessageByRole(AIAgent.Assistant, inter);
                                        updated[i] = inter; // persist metrics

                                        var snapshot = new AIReturn();
                                        snapshot.CreateSuccess(updated, _dialog._initialRequest);
                                        _dialog._lastReturn = snapshot;
                                        break;
                                    }
                                }
                            }
                            catch (Exception mergeLastEx)
                            {
                                Debug.WriteLine($"[WebChatObserver] OnFinal fallback metrics merge error: {mergeLastEx.Message}");
                            }
                        }

                        // 3) Do not re-add tool calls/results here; they were handled via OnToolCall/OnToolResult

                        // 4) Clear streaming state and finish
                        _streams.Clear();

                        // If we haven't produced a snapshot above, fall back to building from provider result interactions
                        if (_dialog._lastReturn == null || _dialog._lastReturn.Body?.Interactions == null)
                        {
                            var fromProvider = result?.Body?.Interactions ?? new List<IAIInteraction>();
                            var snapshot = new AIReturn();
                            snapshot.CreateSuccess(new List<IAIInteraction>(fromProvider), _dialog._initialRequest);
                            _dialog._lastReturn = snapshot;
                        }
                        _dialog.ResponseReceived?.Invoke(_dialog, _dialog._lastReturn);
                        _dialog._statusLabel.Text = "Ready";
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[WebChatObserver] OnFinal UI error: {ex.Message}");
                    }
                });
            }

            public void OnError(Exception ex)
            {
                Application.Instance.AsyncInvoke(() =>
                {
                    try
                    {
                        if (ex is OperationCanceledException)
                        {
                            _dialog.AddSystemMessage("Cancelled.", "info");
                            _dialog._statusLabel.Text = "Cancelled";
                        }
                        else
                        {
                            _dialog.AddSystemMessage($"Error: {ex.Message}", "error");
                            _dialog._statusLabel.Text = "Error";
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

