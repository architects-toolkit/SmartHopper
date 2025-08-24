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
                                    state.Aggregated = MergeForStreaming(state.Aggregated, inter);
                                    _streams[key] = state;
                                    _dialog.AddMessageUIOnly(state.Aggregated);
                                }
                                else
                                {
                                    // Subsequent chunks: merge and replace existing bubble
                                    state.Aggregated = MergeForStreaming(state.Aggregated, inter);
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
                        // Reset centralized streaming state at finalization
                        _streams.Clear();

                        // Show final interactions and persist to history once
                        var interactions = result?.Body?.Interactions;
                        if (interactions != null)
                        {
                            foreach (var inter in interactions)
                            {
                                try
                                {
                                    // Update UI via replacement (appends if none exists for the role)
                                    _dialog.ReplaceLastMessageByRole(inter.Agent, inter);

                                    // Persist to history exactly once per final item
                                    _dialog._chatHistory.Add(inter);
                                }
                                catch (Exception innerEx)
                                {
                                    Debug.WriteLine($"[WebChatObserver] OnFinal item error: {innerEx.Message}");
                                }
                            }
                        }

                        _dialog._lastReturn = result ?? new AIReturn();
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

            /// <summary>
            /// Merges an incoming partial interaction into an aggregated one for UI display.
            /// Text interactions are accumulated; other types default to replacement.
            /// </summary>
            private static IAIInteraction MergeForStreaming(IAIInteraction current, IAIInteraction incoming)
            {
                if (incoming is AIInteractionText ttIn)
                {
                    var prev = current as AIInteractionText;
                    return new AIInteractionText
                    {
                        Agent = ttIn.Agent,
                        Content = (prev?.Content ?? string.Empty) + (ttIn.Content ?? string.Empty),
                        Reasoning = string.IsNullOrEmpty(ttIn.Reasoning)
                            ? prev?.Reasoning
                            : (prev?.Reasoning ?? string.Empty) + ttIn.Reasoning,
                        Metrics = ttIn.Metrics ?? prev?.Metrics,
                    };
                }

                // For tool calls/results and others, replace with the latest partial by default
                return incoming;
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

