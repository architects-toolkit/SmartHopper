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
using SmartHopper.Infrastructure.AICall;
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
                            if (!TryAdd(inter))
                                continue; // dedup

                            if (inter is AIInteractionToolResult tr)
                            {
                                _dialog.AddToolResultMessage(tr);
                            }
                            else if (inter is AIInteractionToolCall tc)
                            {
                                _dialog.AddToolCallMessage(tc);
                            }
                            else if (inter is AIInteractionText tt)
                            {
                                // Only show assistant/user text; system text will be shown via normal flow
                                _dialog.AddInteraction(tt);
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
                        // Push any final interactions (deduped)
                        var interactions = result?.Body?.Interactions;
                        if (interactions != null)
                        {
                            foreach (var inter in interactions)
                            {
                                if (!TryAdd(inter))
                                    continue;

                                if (inter is AIInteractionToolResult tr)
                                    _dialog.AddToolResultMessage(tr);
                                else if (inter is AIInteractionToolCall tc)
                                    _dialog.AddToolCallMessage(tc);
                                else if (inter is AIInteractionText tt)
                                    _dialog.AddInteraction(tt);
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
