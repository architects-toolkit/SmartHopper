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
        // UI Components
        private readonly WebView _webView;
        private readonly TextArea _userInputTextArea;
        private readonly Button _sendButton;
        private readonly Button _clearButton;
        private readonly Button _cancelButton;
        private readonly ProgressBar _progressBar;
        private readonly Label _statusLabel;

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

                this._currentSession = new ConversationSession(request);

                // Window basics
                this.ClientSize = new Size(720, 640);
                this.MinimumSize = new Size(560, 420);
                this.Padding = new Padding(6);

                // Controls
                this._webView = new WebView();
                this._webView.DocumentLoaded += this.WebView_DocumentLoaded;
                this._userInputTextArea = new TextArea
                {
                    Wrap = true,
                    AcceptsTab = true,
                    SpellCheck = false,
                    Height = 80
                };
                this._userInputTextArea.KeyDown += this.UserInputTextArea_KeyDown;

                this._sendButton = new Button { Text = "Send" };
                this._sendButton.Click += (s, e) => this.SendMessage();

                this._clearButton = new Button { Text = "Clear" };
                this._clearButton.Click += this.ClearButton_Click;

                this._cancelButton = new Button { Text = "Cancel", Enabled = false };
                this._cancelButton.Click += this.CancelButton_Click;

                this._progressBar = new ProgressBar { Indeterminate = true, Visible = false };
                this._statusLabel = new Label { Text = "Initializing..." };

                // Layout
                var buttonsRow = new StackLayout
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 6,
                    Items =
                    {
                        this._sendButton,
                        this._clearButton,
                        this._cancelButton,
                        new StackLayoutItem(this._progressBar, HorizontalAlignment.Stretch, true),
                        this._statusLabel,
                    }
                };

                this.Content = new StackLayout
                {
                    Orientation = Orientation.Vertical,
                    Spacing = 6,
                    Items =
                    {
                        new StackLayoutItem(this._webView, HorizontalAlignment.Stretch, true),
                        this._userInputTextArea,
                        buttonsRow
                    }
                };

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
                    try { this._statusLabel.Text = this._pendingStatusAfter ?? "Ready"; } catch { }
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
        private void SendMessage()
        {
            try
            {
                var text = (this._userInputTextArea?.Text ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(text)) return;

                // Store the user message before clearing the input
                this._pendingUserMessage = text;

                // Clear input early for UX
                this._userInputTextArea.Text = string.Empty;

                // Append to UI immediately
                var userInter = new AIInteractionText { Agent = AIAgent.User, Content = text };
                this.AddInteractionToWebView(userInter);

                // Kick off processing asynchronously
                Task.Run(() => this.ProcessAIInteraction());
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebChatDialog] SendMessage error: {ex.Message}");
            }
        }

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
                    if (!string.IsNullOrEmpty(statusBefore))
                    {
                        this._statusLabel.Text = statusBefore;
                    }

                    this._progressBar.Visible = showProgress;

                    // Load the HTML into the WebView
                    this._webView.LoadHtml(html, new Uri("https://smarthopper.local/"));

                    // Do not mark initialized here; wait for DocumentLoaded to ensure CoreWebView2 is ready
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[WebChatDialog] LoadInitialHtmlIntoWebView UI error: {ex.Message}");
                }
                finally
                {
                    if (showProgress)
                    {
                        this._progressBar.Visible = false;
                    }
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
        private void ClearButton_Click(object sender, EventArgs e)
        {
            try
            {
                this.CancelCurrentRun();

                // Reload initial HTML to clear the chat UI
                this.RunWhenWebViewReady(() =>
                {
                    this.LoadInitialHtmlIntoWebView(showProgress: false, setWebViewInitialized: false, statusBefore: null, statusAfter: "Ready");
                });

                // Emit a reset snapshot to notify listeners
                this.EmitResetSnapshot();

                // Optionally start greeting again
                Task.Run(() => this.InitializeNewConversation());
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebChatDialog] ClearButton_Click error: {ex.Message}");
            }
        }

        /// <summary>
        /// Send on Enter, allow newline with Shift+Enter.
        /// </summary>
        private void UserInputTextArea_KeyDown(object sender, KeyEventArgs e)
        {
            try
            {
                if (e.Key == Keys.Enter && (e.Modifiers & Keys.Shift) == 0)
                {
                    e.Handled = true;
                    this.SendMessage();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebChatDialog] UserInputTextArea_KeyDown error: {ex.Message}");
            }
        }

        /// <summary>
        /// Processes an AI interaction using the new AICall models and handles tool calls automatically.
        /// </summary>
        private async Task ProcessAIInteraction()
        {
            try
            {
                Debug.WriteLine("[WebChatDialog] Processing AI interaction with existing session reuse");
                var observer = new WebChatObserver(this);
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
                throw; // Rethrow to be handled by SendMessage
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
                this._cancelButton.Enabled = false;
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
        private void CancelButton_Click(object sender, EventArgs e)
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
                    RhinoApp.InvokeOnUiThread(() => { this._statusLabel.Text = "Ready"; });
                    return;
                }

                // Optional: update status while greeting is generated; UI message will come via session observer events
                RhinoApp.InvokeOnUiThread(() => { this._statusLabel.Text = "Starting..."; });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebChatDialog] Error in InitializeNewConversation: {ex.Message}");

                // Let session/observer handle any fallback; only ensure status is not left in a loading state
                RhinoApp.InvokeOnUiThread(() => { this._statusLabel.Text = "Ready"; });
            }
            finally
            {
                try { this._currentCts?.Cancel(); } catch { }
                this._currentCts?.Dispose();
                this._currentCts = null;
            }
        }
    }
}
