/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

/*
 * WebChatDialog.cs
 * Provides a dialog-based chat interface using WebView for rendering HTML content.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Eto.Drawing;
using Eto.Forms;
using Newtonsoft.Json;
using Rhino;
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AICall.Core.Requests;
using SmartHopper.Infrastructure.AICall.Core.Returns;
using SmartHopper.Infrastructure.AICall.Execution;
using SmartHopper.Infrastructure.AICall.Metrics;
using SmartHopper.Infrastructure.AICall.Sessions;
using SmartHopper.Infrastructure.Settings;
using SmartHopper.Infrastructure.Streaming;

namespace SmartHopper.Core.UI.Chat
{
    /// <summary>
    /// Dialog-based chat interface using WebView for rendering HTML content.
    /// </summary>
    internal partial class WebChatDialog : Form
    {
        // UI Component: full WebView-based UI
        private readonly WebView _webView;

        // Chat Dialog
        private bool _isProcessing;
        private readonly Action<string>? _progressReporter;
        private readonly HtmlChatRenderer _htmlRenderer = new HtmlChatRenderer();
        private bool _webViewInitialized = false;
        private TaskCompletionSource<bool> _webViewInitializedTcs = new TaskCompletionSource<bool>();
        private ConversationSession _currentSession;
        private System.Threading.CancellationTokenSource _currentCts;
        private string _pendingUserMessage;

        // ConversationSession manages all history and requests
        // WebChatDialog is now a pure UI consumer

        // DOM update reentrancy guard/queue to avoid nested ExecuteScript calls causing recursion
        private bool _isDomUpdating = false;
        private readonly Queue<Action> _domUpdateQueue = new Queue<Action>();

        // Status text to apply after the document is fully loaded
        private string _pendingStatusAfter = "Ready";



        /// <summary>
        /// Creates a new WebChatDialog bound to an initial AI request and optional progress reporter.
        /// </summary>
        internal WebChatDialog(AIRequestCall request, Action<string>? progressReporter)
        {
            try
            {
                this._progressReporter = progressReporter;

                // Create session with attached observer from the start
                this._currentSession = new ConversationSession(request, new WebChatObserver(this));

                // Window basics
                this.ClientSize = new Size(720, 640);
                this.MinimumSize = new Size(560, 420);
                this.Padding = new Padding(6);

                // WebView-only content
                this._webView = new WebView();
                this._webView.DocumentLoaded += this.WebView_DocumentLoaded;
                this._webView.DocumentLoading += this.WebView_DocumentLoading;
                this.Content = this._webView;

                // Initialize web view and optionally start greeting
                _ = this.InitializeWebViewAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebChatDialog] Constructor error: {ex.Message}");
            }
        }

        /// <summary>
        /// Ensures the dialog is visible on screen.
        /// </summary>
        internal void EnsureVisibility()
        {
            try
            {
                if (!this.Visible)
                {
                    this.Show();
                }
                else
                {
                    this.BringToFront();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebChatDialog] EnsureVisibility error: {ex.Message}");
            }
        }

        /// <summary>
        /// Minimal query string parser (avoids System.Web dependency).
        /// </summary>
        private static Dictionary<string, string> ParseQueryString(string query)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(query)) return dict;
            var q = query.StartsWith("?") ? query.Substring(1) : query;
            foreach (var pair in q.Split('&'))
            {
                if (string.IsNullOrEmpty(pair)) continue;
                var kv = pair.Split(new[] { '=' }, 2);
                var key = Uri.UnescapeDataString(kv[0] ?? string.Empty);
                var val = kv.Length > 1 ? Uri.UnescapeDataString(kv[1]) : string.Empty;
                if (!string.IsNullOrEmpty(key)) dict[key] = val;
            }
            return dict;
        }

        /// <summary>
        /// Runs the given action when the WebView is initialized. Always marshals to Rhino UI thread.
        /// Also serializes DOM updates to avoid reentrancy.
        /// </summary>
        private void RunWhenWebViewReady(Action action)
        {
            if (action == null) return;

            void ExecuteSerialized()
            {
                if (this._isDomUpdating)
                {
                    this._domUpdateQueue.Enqueue(action);
                    return;
                }
                this._isDomUpdating = true;
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[WebChatDialog] RunWhenWebViewReady action error: {ex.Message}");
                }
                finally
                {
                    this._isDomUpdating = false;
                    while (this._domUpdateQueue.Count > 0)
                    {
                        var next = this._domUpdateQueue.Dequeue();
                        try { next?.Invoke(); } catch (Exception qex) { Debug.WriteLine($"[WebChatDialog] DOM queued action error: {qex.Message}"); }
                    }
                }
            }

            if (this._webViewInitialized)
            {
                RhinoApp.InvokeOnUiThread(ExecuteSerialized);
            }
            else
            {
                this._webViewInitializedTcs.Task.ContinueWith(_ => RhinoApp.InvokeOnUiThread(ExecuteSerialized));
            }
        }

        /// <summary>
        /// Executes JavaScript in the WebView on Rhino's UI thread.
        /// </summary>
        private void ExecuteScript(string script)
        {
            if (string.IsNullOrWhiteSpace(script)) return;
            try
            {
                RhinoApp.InvokeOnUiThread(() =>
                {
                    try { this._webView.ExecuteScript(script); } catch (Exception ex) { Debug.WriteLine($"[WebChatDialog] ExecuteScript error: {ex.Message}"); }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebChatDialog] ExecuteScript marshal error: {ex.Message}");
            }
        }

        /// <summary>
        /// Adds a new interaction bubble to the WebView.
        /// </summary>
        private void AddInteractionToWebView(IAIInteraction interaction)
        {
            if (interaction == null) return;
            this.RunWhenWebViewReady(() =>
            {
                var html = this._htmlRenderer.RenderInteraction(interaction);
                this.ExecuteScript($"addMessage({JsonConvert.SerializeObject(html)});");
            });
        }

        /// <summary>
        /// Marks the WebView as initialized only after the document is fully loaded.
        /// Ensures CoreWebView2 is ready before any ExecuteScript calls run.
        /// </summary>
        private void WebView_DocumentLoaded(object sender, WebViewLoadedEventArgs e)
        {
            try
            {
                RhinoApp.InvokeOnUiThread(() =>
                {
                    this._webViewInitialized = true;
                    try { this._webViewInitializedTcs.TrySetResult(true); } catch { }
                    // Reflect status in web UI
                    try { this.ExecuteScript($"setStatus({JsonConvert.SerializeObject(this._pendingStatusAfter ?? "Ready")}); setProcessing(false);"); } catch { }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebChatDialog] WebView_DocumentLoaded error: {ex.Message}");
            }
        }

        /// <summary>
        /// Replaces the last message for a given role with updated content.
        /// </summary>
        private void ReplaceLastMessageByRole(AIAgent agent, IAIInteraction interaction)
        {
            if (interaction == null) return;
            this.RunWhenWebViewReady(() =>
            {
                var html = this._htmlRenderer.RenderInteraction(interaction);
                var role = agent.ToString().ToLower();
                this.ExecuteScript($"replaceLastMessageByRole('{role}', {JsonConvert.SerializeObject(html)});");
            });
        }

        /// <summary>
        /// Adds a tool call message to the WebView.
        /// </summary>
        private void AddToolCallMessage(AIInteractionToolCall toolCall)
        {
            if (toolCall == null) return;
            this.AddInteractionToWebView(toolCall);
        }

        /// <summary>
        /// Adds a tool result message to the WebView.
        /// </summary>
        private void AddToolResultMessage(AIInteractionToolResult toolResult)
        {
            if (toolResult == null) return;
            this.AddInteractionToWebView(toolResult);
        }

        /// <summary>
        /// Adds a system message to the WebView.
        /// </summary>
        private void AddSystemMessage(string text, string level = "info")
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            var msg = new AIInteractionText { Agent = AIAgent.System, Content = text };
            this.AddInteractionToWebView(msg);
        }

        /// <summary>
        /// Emits a snapshot of the current conversation state via ChatUpdated.
        /// </summary>
        private void BuildAndEmitSnapshot()
        {
            try
            {
                var snapshot = this._currentSession != null ? this._currentSession.GetHistoryReturn() : new AIReturn();
                this.ChatUpdated?.Invoke(this, snapshot);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebChatDialog] BuildAndEmitSnapshot error: {ex.Message}");
            }
        }

        /// <summary>
        /// Emits a reset/empty snapshot to subscribers.
        /// </summary>
        private void EmitResetSnapshot()
        {
            try { this.ChatUpdated?.Invoke(this, new AIReturn()); } catch { }
        }

        /// <summary>
        /// Handles sending a user message from the input box.
        /// </summary>
        // No no-arg SendMessage: messages come from WebView events and call SendMessage(string)

        /// <summary>
        /// Event raised when a new AI response is received.
        /// </summary>
        public event EventHandler<AIReturn> ResponseReceived;

        /// <summary>
        /// Event raised whenever the chat state is updated (partial streams, tool events, user messages, or final result).
        /// Carries a snapshot AIReturn reflecting the current conversation state.
        /// </summary>
        public event EventHandler<AIReturn> ChatUpdated;

        /// <summary>
        /// Gets the last AI return received from the chat dialog.
        /// </summary>
        public AIReturn GetLastReturn() => this._currentSession?.GetReturn() ?? new AIReturn();

        /// <summary>
        /// Gets the combined metrics from interactions in the conversation.
        /// </summary>
        /// <param name="newInteractionsOnly">When true, returns metrics from new interactions only; when false, returns metrics from all history.</param>
        /// <returns>Combined AI metrics from conversation interactions.</returns>
        public AIMetrics GetCombinedMetrics(bool newInteractionsOnly = false)
        {
            return this._currentSession?.GetCombinedMetrics(newInteractionsOnly) ?? new AIMetrics();
        }

        /// <summary>
        /// Loads the initial chat HTML into the WebView and updates UI status/progress safely on Rhino's UI thread.
        /// </summary>
        /// <param name="showProgress">Whether to show the progress bar during load.</param>
        /// <param name="setWebViewInitialized">Whether to set the WebView as initialized and complete the TCS.</param>
        /// <param name="statusBefore">Optional status text to set before loading. Pass null to skip.</param>
        /// <param name="statusAfter">Status text to set after loading completes.</param>
        private void LoadInitialHtmlIntoWebView(bool showProgress, bool setWebViewInitialized, string statusBefore = "Loading chat UI...", string statusAfter = "Ready")
        {
            var html = this._htmlRenderer.GetInitialHtml();
            // Each time we (re)load HTML, reset readiness and TCS; readiness will be set on DocumentLoaded
            this._webViewInitialized = false;
            this._webViewInitializedTcs = new TaskCompletionSource<bool>();
            this._pendingStatusAfter = statusAfter ?? "Ready";

            RhinoApp.InvokeOnUiThread(() =>
            {
                try
                {
                    // Load the HTML into the WebView
                    this._webView.LoadHtml(html, new Uri("https://smarthopper.local/"));

                    // Do not mark initialized here; wait for DocumentLoaded to ensure CoreWebView2 is ready
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[WebChatDialog] LoadInitialHtmlIntoWebView UI error: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// Initializes the WebView with the initial HTML and starts optional greeting.
        /// </summary>
        private async Task InitializeWebViewAsync()
        {
            try
            {
                this.LoadInitialHtmlIntoWebView(showProgress: true, setWebViewInitialized: true);

                // Optionally generate greeting after UI is ready
                await Task.Run(() => this.InitializeNewConversation()).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebChatDialog] InitializeWebViewAsync error: {ex.Message}");
                try { this._webViewInitializedTcs.TrySetException(ex); } catch { }
            }
        }

        /// <summary>
        /// Handles the Clear button click event.
        /// </summary>
        private void ClearChat()
        {
            try
            {
                this.CancelCurrentRun();
                // Clear messages in-place without reloading the WebView
                this.RunWhenWebViewReady(() => this.ExecuteScript("clearMessages(); setStatus('Ready'); setProcessing(false);"));
                // Emit a reset snapshot to notify listeners (no greeting on clear)
                this.EmitResetSnapshot();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebChatDialog] ClearChat error: {ex.Message}");
            }
        }

        /// <summary>
        /// Send on Enter, allow newline with Shift+Enter.
        /// </summary>
        // No local key handling: input is fully handled in WebView HTML/JS

        /// <summary>
        /// Processes an AI interaction using the new AICall models and handles tool calls automatically.
        /// </summary>
        private async Task ProcessAIInteraction()
        {
            try
            {
                Debug.WriteLine("[WebChatDialog] Processing AI interaction with existing session reuse");

                // Observer already attached at construction time
                this._currentCts = new CancellationTokenSource();

                // Reuse existing session if available, otherwise create new one
                if (this._currentSession == null)
                {
                    throw new Exception("[WebChatDialog] No existing conversation session found");
                }
                else
                {
                    Debug.WriteLine("[WebChatDialog] Reusing existing ConversationSession");
                }

                // Add the pending user message to the session
                if (!string.IsNullOrWhiteSpace(this._pendingUserMessage))
                {
                    this._currentSession.AddInteraction(this._pendingUserMessage);
                    this._pendingUserMessage = null; // Clear after adding
                }

                var options = new SessionOptions { ProcessTools = true, CancellationToken = this._currentCts.Token };

                // Decide whether streaming should be attempted first using the session's request
                var sessionRequest = this._currentSession.Request;
                bool shouldTryStreaming = false;
                try
                {
                    sessionRequest.WantsStreaming = true;
                    var validation = sessionRequest.IsValid();
                    shouldTryStreaming = validation.IsValid;
                    Debug.WriteLine($"[WebChatDialog] Request validation: IsValid={validation.IsValid}, Errors={validation.Errors?.Count ?? 0}");
                    if (validation.Errors != null)
                    {
                        try
                        {
                            var msgs = string.Join(" | ", validation.Errors.Select(err => $"{err.Severity}:{err.Message}"));
                            Debug.WriteLine($"[WebChatDialog] Validation messages: {msgs}");
                        }
                        catch { /* ignore logging errors */ }
                    }
                    if (!shouldTryStreaming)
                    {
                        Debug.WriteLine("[WebChatDialog] Streaming validation failed, will fallback to non-streaming.");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[WebChatDialog] Streaming validation threw: {ex.Message}. Falling back.");
                    shouldTryStreaming = false;
                }
                finally
                {
                    // Ensure we don't carry the streaming intent into non-streaming execution
                    sessionRequest.WantsStreaming = false;
                }

                AIReturn lastStreamReturn = null;
                if (shouldTryStreaming)
                {
                    Debug.WriteLine("[WebChatDialog] Starting streaming path");
                    var streamingOptions = new StreamingOptions();

                    // Consume the stream to drive incremental UI updates via observer
                    // Track the last streamed return so we can decide about fallback.
                    await foreach (var r in this._currentSession.Stream(options, streamingOptions, this._currentCts.Token))
                    {
                        lastStreamReturn = r;

                        // No-op: observer handles partial/final UI updates.
                    }
                }

                // If streaming finished with an error or yielded nothing or no streaming was attempted, fallback to non-streaming.
                if (lastStreamReturn == null || !lastStreamReturn.Success || !shouldTryStreaming)
                {
                    Debug.WriteLine("[WebChatDialog] Streaming ended with error or no result. Falling back to non-streaming path");
                    // Ensure streaming flag is not set for non-streaming execution
                    sessionRequest.WantsStreaming = false;
                    var fallbackResult = await this._currentSession.RunToStableResult(options).ConfigureAwait(false);

                    // Merge: append the provider-tracked new interactions
                    var newOnes = fallbackResult?.Body?.GetNewInteractions() ?? new List<IAIInteraction>();
                    foreach (var inter in newOnes)
                    {
                        this.AddInteractionToWebView(inter);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebChatDialog] Error in ProcessAIInteraction: {ex.Message}");
                try
                {
                    this.AddSystemMessage($"Error: {ex.Message}", "error");
                    this.RunWhenWebViewReady(() => this.ExecuteScript("setStatus('Error'); setProcessing(false);") );
                    this.BuildAndEmitSnapshot();
                }
                catch { /* ignore secondary errors */ }
            }
            finally
            {
                try { this._currentCts?.Cancel(); } catch { }
                this._currentCts?.Dispose();
                this._currentCts = null;
                // Keep the session alive for reuse - do not set to null
            }
        }

        /// <summary>
        /// Cancels the current running session, if any.
        /// </summary>
        private void CancelCurrentRun()
        {
            try
            {
                this._currentCts?.Cancel();
                this._currentSession?.Cancel();
                Debug.WriteLine("[WebChatDialog] Cancellation requested");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebChatDialog] Error requesting cancellation: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles the cancel button click event.
        /// </summary>
        private void CancelChat()
        {
            this.CancelCurrentRun();
        }

        /// <summary>
        /// Initializes a new conversation with a system message and uses ConversationSession to generate greeting.
        /// </summary>
        private async void InitializeNewConversation()
        {
            try
            {
                // Ensure the system prompt from the initial request is added to chat history and UI
                // 1) Prefer any existing system message already in the current interactions
                // 2) Otherwise, pick the first system message from the initial request body
                var systemMessageText = this._currentSession?.GetReturn()?.Body?.Interactions?
                    .OfType<AIInteractionText>()
                    .Where(i => i.Agent == AIAgent.System)
                    .Select(i => i.Content)
                    .FirstOrDefault();

                if (!string.IsNullOrWhiteSpace(systemMessageText))
                {
                    // Render system prompt to UI once and emit a snapshot
                    var sysInter = new AIInteractionText { Agent = AIAgent.System, Content = systemMessageText };
                    this.AddInteractionToWebView(sysInter);
                    this.BuildAndEmitSnapshot();
                }

                // Check if AI greeting is enabled in settings
                var settings = SmartHopperSettings.Instance;
                if (!settings.SmartHopperAssistant.EnableAIGreeting)
                {
                    // Skip greeting entirely if disabled
                    this.RunWhenWebViewReady(() => this.ExecuteScript("setStatus('Ready'); setProcessing(false);") );
                    return;
                }

                // Do not show processing here; OnStart from the session/observer will manage status/spinner during real runs
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebChatDialog] Error in InitializeNewConversation: {ex.Message}");

                // Let session/observer handle any fallback; only ensure status is not left in a loading state
                this.RunWhenWebViewReady(() => this.ExecuteScript("setStatus('Ready'); setProcessing(false);") );
            }
            finally
            {
                // No-op: do not cancel any CTS here; conversation runs are managed elsewhere
            }
        }

        /// <summary>
        /// Intercepts navigation events from the WebView to handle sh:// commands from JS.
        /// </summary>
        private void WebView_DocumentLoading(object sender, WebViewLoadingEventArgs e)
        {
            try
            {
                Debug.WriteLine($"[WebChatDialog] WebView_DocumentLoading called");
                var uri = e?.Uri;
                if (uri == null) 
                {
                    Debug.WriteLine($"[WebChatDialog] Navigation URI is null");
                    return;
                }
                
                Debug.WriteLine($"[WebChatDialog] Navigation URI: {uri} (scheme: {uri.Scheme})");
                
                if (uri.Scheme.Equals("sh", StringComparison.OrdinalIgnoreCase))
                {
                    Debug.WriteLine($"[WebChatDialog] Intercepting sh:// scheme, cancelling navigation");
                    e.Cancel = true;
                    var query = ParseQueryString(uri.Query);
                    var type = (query.TryGetValue("type", out var t) ? t : string.Empty).ToLowerInvariant();
                    Debug.WriteLine($"[WebChatDialog] sh:// event type: '{type}', query params: {string.Join(", ", query.Keys)}");
                    
                    switch (type)
                    {
                        case "send":
                            {
                                var text = query.TryGetValue("text", out var txt) ? txt : string.Empty;
                                Debug.WriteLine($"[WebChatDialog] Handling send event, text length: {text.Length}");
                                // Defer to next UI tick to avoid executing scripts during navigation event
                                Application.Instance.AsyncInvoke(() =>
                                {
                                    try { this.SendMessage(text); }
                                    catch (Exception ex) { Debug.WriteLine($"[WebChatDialog] Deferred SendMessage error: {ex.Message}"); }
                                });
                                break;
                            }
                        case "clear":
                            Debug.WriteLine($"[WebChatDialog] Handling clear event");
                            // Defer to next UI tick to avoid executing scripts during navigation event
                            Application.Instance.AsyncInvoke(() =>
                            {
                                try { this.ClearChat(); }
                                catch (Exception ex) { Debug.WriteLine($"[WebChatDialog] Deferred ClearChat error: {ex.Message}"); }
                            });
                            break;
                        case "cancel":
                            Debug.WriteLine($"[WebChatDialog] Handling cancel event");
                            // Defer to next UI tick to avoid executing scripts during navigation event
                            Application.Instance.AsyncInvoke(() =>
                            {
                                try { this.CancelChat(); }
                                catch (Exception ex) { Debug.WriteLine($"[WebChatDialog] Deferred CancelChat error: {ex.Message}"); }
                            });
                            break;
                        default:
                            Debug.WriteLine($"[WebChatDialog] Unknown sh:// event type: '{type}'");
                            break;
                    }
                }
                else if (uri.Scheme.Equals("clipboard", StringComparison.OrdinalIgnoreCase))
                {
                    Debug.WriteLine($"[WebChatDialog] Intercepting clipboard:// scheme");
                    // Handle copy-to-clipboard from JS
                    e.Cancel = true;
                    var query = ParseQueryString(uri.Query);
                    var text = query.TryGetValue("text", out var t) ? t : string.Empty;
                    Debug.WriteLine($"[WebChatDialog] Clipboard text length: {text.Length}");
                    try
                    {
                        RhinoApp.InvokeOnUiThread(() =>
                        {
                            try
                            {
                                var cb = new Clipboard();
                                cb.Text = text;
                                Debug.WriteLine($"[WebChatDialog] Text copied to clipboard successfully");
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"[WebChatDialog] Clipboard set failed: {ex.Message}");
                            }
                        });
                        this.RunWhenWebViewReady(() => this.ExecuteScript("showToast('Copied to clipboard');"));
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[WebChatDialog] Clipboard handling error: {ex.Message}");
                    }
                }
                else
                {
                    Debug.WriteLine($"[WebChatDialog] Allowing normal navigation to: {uri}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebChatDialog] WebView_DocumentLoading error: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles a user message submitted from the WebView.
        /// </summary>
        private void SendMessage(string text)
        {
            try
            {
                Debug.WriteLine($"[WebChatDialog] SendMessage called with text length: {text?.Length ?? 0}");
                if (string.IsNullOrWhiteSpace(text)) 
                {
                    Debug.WriteLine($"[WebChatDialog] SendMessage: text is null or whitespace, returning");
                    return;
                }
                var trimmed = (text ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(trimmed)) return;

                // Store the user message before processing
                this._pendingUserMessage = trimmed;

                // Append to UI immediately
                var userInter = new AIInteractionText { Agent = AIAgent.User, Content = trimmed };
                this.AddInteractionToWebView(userInter);

                // Kick off processing asynchronously
                Debug.WriteLine("[WebChatDialog] Scheduling ProcessAIInteraction task");
                Task.Run(async () =>
                {
                    try
                    {
                        Debug.WriteLine("[WebChatDialog] ProcessAIInteraction task starting");
                        await this.ProcessAIInteraction().ConfigureAwait(false);
                        Debug.WriteLine("[WebChatDialog] ProcessAIInteraction task finished");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[WebChatDialog] ProcessAIInteraction task error: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebChatDialog] SendMessage(text) error: {ex.Message}");
            }
        }
    }
}
