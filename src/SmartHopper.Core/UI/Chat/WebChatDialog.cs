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
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Eto.Drawing;
using Eto.Forms;
using Newtonsoft.Json;
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AICall.Core.Requests;
using SmartHopper.Infrastructure.AICall.Core.Returns;
using SmartHopper.Infrastructure.AICall.Metrics;
using SmartHopper.Infrastructure.AICall.Sessions;
using SmartHopper.Infrastructure.AIModels;
using SmartHopper.Infrastructure.Properties;
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
        private readonly TaskCompletionSource<bool> _webViewInitializedTcs = new TaskCompletionSource<bool>();
        private ConversationSession _currentSession;
        private System.Threading.CancellationTokenSource _currentCts;

        // Chat history and request management
        private readonly AIRequestCall _initialRequest;
        private readonly List<IAIInteraction> _chatHistory = new List<IAIInteraction>();
        private AIReturn _lastReturn = new AIReturn();

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
        public AIReturn GetLastReturn() => this._lastReturn;

        /// <summary>
        /// Gets the combined metrics from all interactions in the chat history.
        /// </summary>
        public AIMetrics GetCombinedMetrics()
        {
            var combinedMetrics = new AIMetrics();
            foreach (var interaction in this._chatHistory)
            {
                if (interaction.Metrics != null)
                {
                    combinedMetrics.Combine(interaction.Metrics);
                }
            }
            return combinedMetrics;
        }

        private static readonly Assembly ConfigAssembly = typeof(providersResources).Assembly;
        private const string IconResourceName = "SmartHopper.Infrastructure.Resources.smarthopper.ico";

        /// <summary>
        /// Creates a new web chat dialog.
        /// </summary>
        /// <param name="getResponse">Function to get responses from the AI provider.</param>
        /// <param name="providerName">The name of the AI provider to use for default model operations.</param>
        /// <param name="systemPrompt">Optional system prompt to provide to the AI assistant.</param>
        /// <param name="progressReporter">Optional callback to report progress updates.</param>
        public WebChatDialog(AIRequestCall request, Action<string>? progressReporter = null)
        {
            Debug.WriteLine("[WebChatDialog] Initializing WebChatDialog");
            this._progressReporter = progressReporter;
            this._initialRequest = request;

            this.Title = "SmartHopper AI Chat";
            this.MinimumSize = new Size(600, 700);
            this.Size = new Size(700, 800);
            this.Resizable = true;
            this.ShowInTaskbar = true;
            this.Owner = null; // Ensure we don't block the parent window

            // Set window icon from embedded resource
            using (var stream = ConfigAssembly.GetManifestResourceStream(IconResourceName))
            {
                if (stream != null)
                {
                    this.Icon = new Eto.Drawing.Icon(stream);
                }
            }

            Debug.WriteLine("[WebChatDialog] Creating WebView");
            // Initialize WebView
            this._webView = new WebView
            {
                Height = 500,
            };

            // Add WebView event handlers for debugging
            this._webView.DocumentLoaded += (sender, e) => Debug.WriteLine("[WebChatDialog] WebView document loaded");
            this._webView.DocumentLoading += (sender, e) => Debug.WriteLine("[WebChatDialog] WebView document loading");
            // Intercept custom clipboard URIs to copy via host and show toast
            this._webView.DocumentLoading += (sender, e) =>
            {
                try
                {
                    var uri = e.Uri;
                    if (uri.Scheme == "clipboard")
                    {
                        e.Cancel = true;
                        // Extract encoded text after 'text='
                        var query = uri.Query;
                        var prefix = "?text=";
                        var encoded = query.StartsWith(prefix) ? query.Substring(prefix.Length) : query.TrimStart('?');
                        var text = Uri.UnescapeDataString(encoded);
                        // Copy to host clipboard
                        Clipboard.Instance.Text = text;
                        Debug.WriteLine($"[WebChatDialog] Copied to clipboard via host: {text}");
                        // Trigger JS toast
                        this._webView.ExecuteScriptAsync("showToast('Code copied to clipboard :)');");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[WebChatDialog] Clipboard intercept error: {ex.Message}");
                }
            };

            this._userInputTextArea = new TextArea
            {
                Height = 60,
                AcceptsReturn = true,
                AcceptsTab = false,
                Wrap = true,
            };
            this._userInputTextArea.KeyDown += this.UserInputTextArea_KeyDown;

            this._sendButton = new Button
            {
                Text = "Send",
                Enabled = true,
            };
            this._sendButton.Click += this.SendButton_Click;

            this._clearButton = new Button
            {
                Text = "Clear Chat",
                Enabled = true,
            };
            this._clearButton.Click += this.ClearButton_Click;

            this._cancelButton = new Button
            {
                Text = "Cancel",
                Enabled = false,
            };
            this._cancelButton.Click += this.CancelButton_Click;

            this._progressBar = new ProgressBar
            {
                Indeterminate = true,
                Visible = false,
            };

            this._statusLabel = new Label
            {
                Text = "Initializing WebView...",
                TextAlignment = TextAlignment.Center
            };

            // Layout
            var mainLayout = new DynamicLayout();

            // WebView area
            mainLayout.Add(this._webView, yscale: true);

            // Input area
            var inputLayout = new DynamicLayout();
            inputLayout.BeginHorizontal();
            inputLayout.Add(this._userInputTextArea, xscale: true);
            inputLayout.Add(this._sendButton);
            inputLayout.Add(this._cancelButton);
            inputLayout.EndHorizontal();
            mainLayout.Add(inputLayout);

            // Controls area
            mainLayout.Add(this._clearButton);
            mainLayout.Add(this._progressBar);
            mainLayout.Add(this._statusLabel);

            this.Content = mainLayout;
            this.Padding = new Padding(10);

            Debug.WriteLine("[WebChatDialog] WebChatDialog initialized, starting WebView initialization");

            // Initialize WebView after the dialog is shown, but don't block the UI thread
            this.Shown += (sender, e) =>
            {
                Debug.WriteLine("[WebChatDialog] Dialog shown, starting WebView initialization");

                // Start initialization in a background thread to avoid UI blocking
                Task.Run(() => this.InitializeWebViewAsync())
                    .ContinueWith(t =>
                    {
                        if (t.IsFaulted)
                        {
                            Debug.WriteLine($"[WebChatDialog] WebView initialization failed: {t.Exception?.InnerException?.Message}");
                        }
                    }, TaskScheduler.Default);
            };

            // Handle window focus events
            this.GotFocus += (sender, e) =>
            {
                Debug.WriteLine("[WebChatDialog] Window got focus");
                this.EnsureVisibility();
            };

            // Also ensure visibility when the window is shown
            this.Shown += (sender, e) =>
            {
                Debug.WriteLine("[WebChatDialog] Window shown");
                this.EnsureVisibility();
            };
        }

        /// <summary>
        /// Ensures the dialog is visible and in the foreground using cross-platform methods.
        /// </summary>
        public void EnsureVisibility()
        {
            Application.Instance.AsyncInvoke(() =>
            {
                Debug.WriteLine("[WebChatDialog] Ensuring window visibility");

                // Restore window if minimized
                if (this.WindowState == WindowState.Minimized)
                {
                    this.WindowState = WindowState.Normal;
                }

                // Use Eto's built-in methods to bring window to front
                this.BringToFront();
                this.Focus();

                Debug.WriteLine("[WebChatDialog] Window visibility ensured");
            });
        }

        /// <summary>
        /// Raises a final ChatUpdated snapshot when the dialog closes so listeners can flush state.
        /// </summary>
        /// <param name="e">Event args.</param>
        protected override void OnClosed(EventArgs e)
        {
            try
            {
                var snapshot = this._lastReturn;
                // If we never had a return with interactions, synthesize one from the current history
                if (snapshot == null || snapshot.Body?.Interactions == null)
                {
                    snapshot = new AIReturn();
                    snapshot.CreateSuccess(new List<IAIInteraction>(this._chatHistory), this._initialRequest);
                }

                this.ChatUpdated?.Invoke(this, snapshot);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebChatDialog] OnClosed ChatUpdated error: {ex.Message}");
            }

            base.OnClosed(e);
        }

        /// <summary>
        /// Initializes the WebView control asynchronously.
        /// </summary>
        private async Task InitializeWebViewAsync()
        {
            try
            {
                Debug.WriteLine("[WebChatDialog] Starting WebView initialization from background thread");

                // Prepare initial HTML (handled by helper on UI thread)
                Debug.WriteLine("[WebChatDialog] Preparing initial HTML via helper");

                // Create a task completion source for tracking document loading
                var loadCompletionSource = new TaskCompletionSource<bool>();

                // Switch to UI thread to load HTML and set up event handlers
                await Application.Instance.InvokeAsync(() =>
                {
                    try
                    {
                        Debug.WriteLine("[WebChatDialog] Loading HTML into WebView on UI thread");

                        // Set up document loaded event handler before loading HTML
                        EventHandler<WebViewLoadedEventArgs> loadHandler = null;
                        loadHandler = (s, e) =>
                        {
                            Debug.WriteLine("[WebChatDialog] WebView document loaded");
                            this._webView.DocumentLoaded -= loadHandler;
                            loadCompletionSource.TrySetResult(true);
                        };

                        this._webView.DocumentLoaded += loadHandler;

                        // Load initial HTML and start conversation via helper
                        this.LoadInitialHtmlAndSystemMessage();

                        Debug.WriteLine("[WebChatDialog] HTML loaded into WebView, waiting for load completion");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[WebChatDialog] Error in UI thread during WebView initialization: {ex.Message}");
                        loadCompletionSource.TrySetException(ex);
                    }
                }).ConfigureAwait(false);

                // Set up a timeout task that won't block the UI thread
                var timeoutTask = Task.Delay(5000);

                // Wait for either the document to load or the timeout to occur
                // Using ConfigureAwait(false) to avoid deadlocks
                var completedTask = await Task.WhenAny(loadCompletionSource.Task, timeoutTask).ConfigureAwait(false);

                if (completedTask == timeoutTask)
                {
                    Debug.WriteLine("[WebChatDialog] WebView document loading timed out");
                }
                else
                {
                    Debug.WriteLine("[WebChatDialog] WebView document loaded successfully");
                }

                // Mark initialization as complete
                this._webViewInitialized = true;
                this._webViewInitializedTcs.TrySetResult(true);

                // Welcome message already handled by LoadInitialHtmlAndSystemMessage

                Debug.WriteLine("[WebChatDialog] WebView initialization completed successfully");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebChatDialog] Error initializing WebView: {ex.Message}");
                Debug.WriteLine($"[WebChatDialog] Error stack trace: {ex.StackTrace}");

                if (ex.InnerException != null)
                {
                    Debug.WriteLine($"[WebChatDialog] Inner exception: {ex.InnerException.Message}");
                    Debug.WriteLine($"[WebChatDialog] Inner exception stack trace: {ex.InnerException.StackTrace}");
                }

                // Ensure we don't leave initialization hanging
                if (!this._webViewInitialized)
                {
                    this._webViewInitialized = true;
                    this._webViewInitializedTcs.TrySetException(ex);
                }

                // Update the status label on the UI thread
                await Application.Instance.InvokeAsync(() =>
                {
                    this._statusLabel.Text = $"Error initializing WebView: {ex.Message}";
                }).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Handles key down events in the user input text area.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event arguments.</param>
        private void UserInputTextArea_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Keys.Enter && e.Modifiers.HasFlag(Keys.Shift))
            {
                // Shift+Enter adds a new line
                this._userInputTextArea.Text += Environment.NewLine;
                e.Handled = true;
            }
            else if (e.Key == Keys.Enter && !this._isProcessing && !e.Modifiers.HasFlag(Keys.Shift))
            {
                // Enter sends the message
                this.SendMessage();
                e.Handled = true;
            }
        }

        /// <summary>
        /// Handles the send button click event.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event arguments.</param>
        private void SendButton_Click(object sender, EventArgs e)
        {
            if (!this._isProcessing && this._currentSession == null)
            {
                this.SendMessage();
            }
        }

        /// <summary>
        /// Handles the clear button click event.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event arguments.</param>
        private void ClearButton_Click(object sender, EventArgs e)
        {
            this._chatHistory.Clear();

            // Emit an empty/reset snapshot so listeners can clear their state immediately
            this.EmitResetSnapshot();

            // Reset the WebView with initial HTML
            try
            {
                Debug.WriteLine("[WebChatDialog] Clearing chat and reloading HTML");
                // Use centralized helper and queue if needed until WebView is ready
                this.RunWhenWebViewReady(() =>
                {
                    this.LoadInitialHtmlAndSystemMessage();
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebChatDialog] Error clearing chat: {ex.Message}");
            }
        }

        /// <summary>
        /// Emits an empty AIReturn snapshot to propagate a cleared chat state.
        /// </summary>
        private void EmitResetSnapshot()
        {
            try
            {
                var snapshot = new AIReturn();
                snapshot.CreateSuccess(new List<IAIInteraction>(), this._initialRequest);
                this._lastReturn = snapshot;
                this.ChatUpdated?.Invoke(this, snapshot);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebChatDialog] ChatUpdated (reset) error: {ex.Message}");
            }
        }

        /// <summary>
        /// Adds an interaction to the chat history and displays it in the WebView.
        /// </summary>
        /// <param name="interaction">The AI interaction object containing the message.</param>
        private void AddInteraction(IAIInteraction interaction)
        {
            try
            {
                var metrics = interaction?.Metrics;
                var contentLen = (interaction as AIInteractionText)?.Content?.Length ?? 0;
                Debug.WriteLine($"[WebChatDialog] AddInteraction -> agent={interaction?.Agent}, type={interaction?.GetType().Name}, contentLen={contentLen}, metrics={(metrics != null ? $"in={metrics.InputTokens}, out={metrics.OutputTokens}, provider='{metrics.Provider}', model='{metrics.Model}', reason='{metrics.FinishReason}'" : "null")}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebChatDialog] AddInteraction log error: {ex.Message}");
            }

            this._chatHistory.Add(interaction);
            this.AddInteractionToWebView(interaction);

            // Raise incremental update snapshot
            this.BuildAndEmitSnapshot();
        }

        /// <summary>
        /// Adds a user message to the chat history and displays it in the WebView.
        /// </summary>
        /// <param name="message">The message text.</param>
        private void AddUserMessage(string message)
        {
            var interaction = new AIInteractionText
            {
                Agent = AIAgent.User,
                Content = message,
            };

            this.AddInteraction(interaction);
        }

        /// <summary>
        /// Adds an assistant message to the chat history and displays it in the WebView.
        /// </summary>
        /// <param name="content">The message content.</param>
        /// <param name="metrics">The AI metrics associated with this response.</param>
        private void AddAssistantMessage(string content, AIMetrics metrics = null)
        {
            var interaction = new AIInteractionText
            {
                Agent = AIAgent.Assistant,
                Content = content,
                Metrics = metrics ?? new AIMetrics(),
            };

            this.AddInteraction(interaction);
        }

        /// <summary>
        /// Adds a system message to the chat history and displays it in the WebView.
        /// </summary>
        /// <param name="message">The message text.</param>
        /// <param name="messageType">The type of system message (info, error, etc.).</param>
        private void AddSystemMessage(string message, string messageType = "info")
        {
            // TODO: Pass message type
            
            var interaction = new AIInteractionText
            {
                Agent = AIAgent.System,
                Content = message,
            };

            this.AddInteraction(interaction);
        }

        /// <summary>
        /// Adds a tool call message to the chat display.
        /// </summary>
        /// <param name="toolCall">Tool call interaction.</param>
        private void AddToolCallMessage(AIInteractionToolCall toolCall)
        {
            this.AddInteraction(toolCall);
        }

        /// <summary>
        /// Adds a tool result message to the chat display.
        /// </summary>
        /// <param name="toolResult">Result from the tool execution.</param>
        private void AddToolResultMessage(AIInteractionToolResult toolResult)
        {
            this.AddInteraction(toolResult);
        }

        /// <summary>
        /// Removes the last message of a specific role from both chat history and WebView.
        /// </summary>
        /// <param name="agent">The role of the message to remove (user, assistant, system).</param>
        /// <returns>True if a message was removed, false otherwise.</returns>
        private bool RemoveLastMessage(AIAgent agent)
        {
            if (agent == null)
            {
                Debug.WriteLine("[WebChatDialog] Cannot remove message with empty role");
                return false;
            }

            try
            {
                // Find the last message of the specified role in chat history
                var lastMessage = this._chatHistory.LastOrDefault(m => m.Agent == agent);
                if (lastMessage == null)
                {
                    Debug.WriteLine($"[WebChatDialog] No {agent.ToString()} messages found to remove");
                    return false;
                }

                // Remove from chat history
                bool removedFromHistory = this._chatHistory.Remove(lastMessage);

                // Remove from WebView using JavaScript
                bool removedFromUI = false;
                if (this._webViewInitialized)
                {
                    try
                    {
                        // Role class in HTML is lowercase (e.g., 'assistant', 'user', 'system')
                        string sanitizedRole = JsonConvert.SerializeObject(agent.ToString().ToLower());
                        string script = $"if (typeof removeLastMessageByRole === 'function') {{ return removeLastMessageByRole({sanitizedRole}); }} else {{ return false; }}";
                        string result = this._webView.ExecuteScript(script);
                        removedFromUI = bool.TryParse(result, out bool jsResult) && jsResult;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[WebChatDialog] Error removing {agent.ToString()} message from UI: {ex.Message}");
                    }
                }

                Debug.WriteLine($"[WebChatDialog] Removed last {agent.ToString()} message - History: {removedFromHistory}, UI: {removedFromUI}");
                return removedFromHistory; // Return true if removed from history, even if UI removal failed
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebChatDialog] Error removing last {agent.ToString()} message: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Removes the last assistant message from both chat history and WebView.
        /// </summary>
        /// <returns>True if a message was removed, false otherwise.</returns>
        private bool RemoveLastAssistantMessage()
        {
            return this.RemoveLastMessage(AIAgent.Assistant);
        }

        /// <summary>
        /// Escapes a string so it can be safely embedded as a JavaScript string literal.
        /// Centralizes all escaping used by DOM update methods.
        /// </summary>
        /// <param name="s">Input string.</param>
        /// <returns>Escaped string safe for JS string literal.</returns>
        private static string EscapeForJsString(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return string.Empty;
            }

            return s
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
        }

        /// <summary>
        /// Runs the given UI action once the WebView has completed initialization.
        /// If already initialized, invokes immediately on the UI thread.
        /// </summary>
        private void RunWhenWebViewReady(Action uiAction)
        {
            if (uiAction == null) return;

            if (this._webViewInitialized)
            {
                Application.Instance.AsyncInvoke(uiAction);
                return;
            }

            // Queue until WebView is ready
            Task.Run(async () =>
            {
                try
                {
                    await this._webViewInitializedTcs.Task.ConfigureAwait(false);
                    Application.Instance.AsyncInvoke(uiAction);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[WebChatDialog] RunWhenWebViewReady error: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// Converts an agent to the lowercase JS role string and JSON-encodes it for safe embedding.
        /// </summary>
        private static string RoleToJs(AIAgent agent)
        {
            return JsonConvert.SerializeObject(agent.ToString().ToLower());
        }

        /// <summary>
        /// Executes a JS snippet safely and optionally scrolls to the bottom.
        /// </summary>
        private string ExecuteScriptSafe(string js, bool scroll = true)
        {
            try
            {
                var result = this._webView.ExecuteScript(js);
                if (scroll)
                {
                    this._webView.ExecuteScript("if (typeof scrollToBottom === 'function') { scrollToBottom(); }");
                }
                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebChatDialog] ExecuteScriptSafe error: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// Updates the status label and progress reporter.
        /// </summary>
        private void SetStatus(string labelText, string progressText)
        {
            Application.Instance.AsyncInvoke(() =>
            {
                this._statusLabel.Text = labelText ?? string.Empty;
                this._progressReporter?.Invoke(progressText ?? string.Empty);
            });
        }

        /// <summary>
        /// Builds a snapshot AIReturn from current chat history and emits ChatUpdated.
        /// </summary>
        private void BuildAndEmitSnapshot()
        {
            try
            {
                var snapshot = new AIReturn();
                snapshot.CreateSuccess(new List<IAIInteraction>(this._chatHistory), this._initialRequest);
                this._lastReturn = snapshot;
                this.ChatUpdated?.Invoke(this, snapshot);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebChatDialog] BuildAndEmitSnapshot error: {ex.Message}");
            }
        }

        /// <summary>
        /// Types of DOM updates supported by the renderer.
        /// </summary>
        private enum DomUpdateKind { Add, ReplaceRole, AppendRole }

        /// <summary>
        /// Centralized DOM update: renders/escapes and executes the proper JS function.
        /// For AppendRole, provide htmlChunk (already HTML) and it will be escaped for JS.
        /// </summary>
        private void RenderAndUpdateDom(IAIInteraction interaction, DomUpdateKind kind, AIAgent? agent = null, string htmlChunk = null)
        {
            if (interaction == null && kind != DomUpdateKind.AppendRole)
            {
                Debug.WriteLine("[WebChatDialog] RenderAndUpdateDom skipped: interaction is null");
                return;
            }

            this.RunWhenWebViewReady(() =>
            {
                try
                {
                    string js;
                    switch (kind)
                    {
                        case DomUpdateKind.Add:
                        {
                            string messageHtml = this._htmlRenderer.GenerateMessageHtml(interaction);
                            string escapedHtml = EscapeForJsString(messageHtml);
                            js = $"if (typeof addMessage === 'function') {{ addMessage(\"{escapedHtml}\"); return 'Message added'; }} else {{ return 'addMessage function not found'; }}";
                            this.ExecuteScriptSafe(js, scroll: true);
                            break;
                        }
                        case DomUpdateKind.ReplaceRole:
                        {
                            if (agent == null) { Debug.WriteLine("[WebChatDialog] ReplaceRole missing agent"); return; }
                            string messageHtml = this._htmlRenderer.GenerateMessageHtml(interaction);
                            string escapedHtml = EscapeForJsString(messageHtml);
                            string roleJs = RoleToJs(agent.Value);
                            js = $"if (typeof replaceLastMessageByRole === 'function') {{ replaceLastMessageByRole({roleJs}, \"{escapedHtml}\"); return 'Message replaced'; }} else if (typeof replaceLastAssistantMessage === 'function' && {roleJs} === 'assistant') {{ replaceLastAssistantMessage(\"{escapedHtml}\"); return 'Assistant message replaced (fallback)'; }} else {{ return 'replace function not found'; }}";
                            this.ExecuteScriptSafe(js, scroll: true);
                            break;
                        }
                        case DomUpdateKind.AppendRole:
                        {
                            if (agent == null) { Debug.WriteLine("[WebChatDialog] AppendRole missing agent"); return; }
                            if (string.IsNullOrEmpty(htmlChunk)) { Debug.WriteLine("[WebChatDialog] AppendRole empty chunk"); return; }
                            string escapedChunk = EscapeForJsString(htmlChunk);
                            string roleJs = RoleToJs(agent.Value);
                            js = $"if (typeof appendToLastMessageByRole === 'function') {{ appendToLastMessageByRole({roleJs}, \"{escapedChunk}\"); return 'Chunk appended'; }} else if (typeof appendToLastAssistantMessage === 'function' && {roleJs} === 'assistant') {{ appendToLastAssistantMessage(\"{escapedChunk}\"); return 'Assistant chunk appended (fallback)'; }} else {{ return 'append function not found'; }}";
                            this.ExecuteScriptSafe(js, scroll: true);
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[WebChatDialog] RenderAndUpdateDom error: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// Loads initial HTML into the WebView and starts a new conversation (system + greeting).
        /// Assumes UI thread.
        /// </summary>
        private void LoadInitialHtmlAndSystemMessage()
        {
            try
            {
                string html = this._htmlRenderer.GetInitialHtml();
                this._webView.LoadHtml(html);
                this.InitializeNewConversation();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebChatDialog] LoadInitialHtmlAndSystemMessage error: {ex.Message}");
            }
        }

        /// <summary>
        /// Adds an interaction to the WebView with the specified role.
        /// </summary>
        /// <param name="interaction">The AI interaction object containing metrics.</param>
        private void AddInteractionToWebView(IAIInteraction interaction)
        {
            if (interaction is null)
            {
                Debug.WriteLine("[WebChatDialog] Skipping empty interaction (null)");
                return;
            }

            Debug.WriteLine($"[WebChatDialog] AddInteractionToWebView -> {interaction.Agent}");
            this.RenderAndUpdateDom(interaction, DomUpdateKind.Add);
        }

        /// <summary>
        /// Updates chat history with the provided assistant interaction (mutates the last assistant entry if present,
        /// otherwise adds a new one) and replaces the last assistant message in the WebView.
        /// </summary>
        /// <param name="interaction">Assistant text interaction to persist and render.</param>
        private void ReplaceLastAssistantMessage(AIInteractionText interaction)
        {
            if (interaction == null)
            {
                Debug.WriteLine("[WebChatDialog] ReplaceLastAssistantMessage skipped: interaction is null");
                return;
            }

            // Don't manage _chatHistory here since this is used by Streaming. Only on final state will _chatHistory be updated.

            // Only used by greeting for now. Will be removed in future.

            // Update history by mutating the last assistant entry if present (e.g., replacing a loading message)
            var lastAssistant = this._chatHistory.LastOrDefault(x => x.Agent == AIAgent.Assistant) as AIInteractionText;
            if (lastAssistant != null)
            {
                lastAssistant.Content = interaction.Content;
                lastAssistant.Reasoning = interaction.Reasoning;
                lastAssistant.Metrics = interaction.Metrics ?? lastAssistant.Metrics;
            }
            else
            {
                // If not present for any reason, add to history
                this._chatHistory.Add(interaction);
            }

            // Update UI
            this.ReplaceLastMessageByRole(AIAgent.Assistant, interaction);
        }

        /// <summary>
        /// Replaces the last message for the given role with the provided interaction's rendered HTML.
        /// Falls back to appending if no message exists for that role yet.
        /// </summary>
        /// <param name="agent">Message role (user, assistant, system, tool).</param>
        /// <param name="interaction">Interaction to render.</param>
        private void ReplaceLastMessageByRole(AIAgent agent, IAIInteraction interaction)
        {
            if (interaction is null)
            {
                Debug.WriteLine("[WebChatDialog] ReplaceLastMessageByRole skipped: interaction is null");
                return;
            }

            this.RenderAndUpdateDom(interaction, DomUpdateKind.ReplaceRole, agent);
        }

        /// <summary>
        /// Appends an HTML chunk to the last message content for the given role.
        /// If no message exists for that role, it creates one.
        /// </summary>
        /// <param name="agent">Message role (user, assistant, system, tool).</param>
        /// <param name="htmlChunk">HTML chunk to append (already sanitized/escaped HTML).</param>
        private void AppendToLastMessageByRole(AIAgent agent, string htmlChunk)
        {
            if (string.IsNullOrEmpty(htmlChunk))
            {
                Debug.WriteLine("[WebChatDialog] AppendToLastMessageByRole skipped: empty chunk");
                return;
            }

            this.RenderAndUpdateDom(null, DomUpdateKind.AppendRole, agent, htmlChunk);
        }

        /// <summary>
        /// Sends the user message to the AI provider and processes the response.
        /// </summary>
        private async void SendMessage()
        {
            if (!this._webViewInitialized)
            {
                Debug.WriteLine("[WebChatDialog] Cannot send message, WebView not initialized");
                this.SetStatus("WebView is still initializing. Please wait...", "Please wait...");
                return;
            }

            if (this._currentSession != null)
            {
                Debug.WriteLine("[WebChatDialog] A session is already running. Ignoring Send.");
                return;
            }

            string userMessage = this._userInputTextArea.Text.Trim();
            if (string.IsNullOrEmpty(userMessage))
            {
                Debug.WriteLine("[WebChatDialog] Empty message, not sending");
                return;
            }

            // Clear input and add message to history
            this._userInputTextArea.Text = string.Empty;
            this.AddUserMessage(userMessage);

            // Update UI state
            this._isProcessing = true;
            this._sendButton.Enabled = false;
            this._progressBar.Visible = true;
            this._cancelButton.Enabled = true;
            this.SetStatus("Thinking...", "Thinking...");

            try
            {
                await this.ProcessAIInteraction();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebChatDialog] Error getting response: {ex.Message}");
                this.AddSystemMessage($"Error: {ex.Message}", "error");
                this.SetStatus("Error occurred", "Error :(");
            }
            finally
            {
                // Restore UI state
                this._isProcessing = false;
                this._sendButton.Enabled = true;
                this._progressBar.Visible = false;
                this._cancelButton.Enabled = false;
            }
        }

        /// <summary>
        /// Processes an AI interaction using the new AICall models and handles tool calls automatically.
        /// </summary>
        private async Task ProcessAIInteraction()
        {
            try
            {
                // Make a copy and update chat history
                var request = this._initialRequest;
                request.OverrideInteractions(new System.Collections.Generic.List<IAIInteraction>(this._chatHistory));

                Debug.WriteLine("[WebChatDialog] Preparing ConversationSession with streaming fallback");
                var observer = new WebChatObserver(this);
                this._currentCts = new CancellationTokenSource();
                this._currentSession = new ConversationSession(request, observer);
                var options = new SessionOptions { ProcessTools = true, CancellationToken = this._currentCts.Token };

                // Decide whether streaming should be attempted first
                bool shouldTryStreaming = false;
                try
                {
                    request.WantsStreaming = true;
                    var validation = request.IsValid();
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
                    request.WantsStreaming = false;
                }

                if (shouldTryStreaming)
                {
                    Debug.WriteLine("[WebChatDialog] Starting streaming path");
                    var streamingOptions = new StreamingOptions();

                    // Consume the stream to drive incremental UI updates via observer
                    // Track the last streamed return so we can decide about fallback.
                    AIReturn lastStreamReturn = null;
                    await foreach (var r in this._currentSession.Stream(options, streamingOptions, this._currentCts.Token))
                    {
                        lastStreamReturn = r;
                        // No-op: observer handles partial/final UI updates and _lastReturn.
                    }

                    // If streaming finished with an error or yielded nothing, fallback to non-streaming.
                    if (lastStreamReturn == null || !lastStreamReturn.Success)
                    {
                        Debug.WriteLine("[WebChatDialog] Streaming ended with error or no result. Falling back to non-streaming path");
                        // Ensure streaming flag is not set for non-streaming execution
                        request.WantsStreaming = false;
                        var fallbackResult = await this._currentSession.RunToStableResult(options).ConfigureAwait(false);
                        this._lastReturn = fallbackResult;
                    }
                }
                else
                {
                    Debug.WriteLine("[WebChatDialog] Starting non-streaming path");
                    var result = await this._currentSession.RunToStableResult(options).ConfigureAwait(false);

                    // Store the last return for external access (observer also sets this on final)
                    this._lastReturn = result;
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
                this._currentSession = null;
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
        /// Initializes a new conversation with a collapsible system message and an AI-generated greeting message.
        /// Keeps the chat in loading state until greeting is received or 30s timeout occurs.
        /// </summary>
        private async void InitializeNewConversation()
        {
            var defaultGreeting = "Hello! I'm your AI assistant. How can I help you today?";

            try
            {
                // Ensure the system prompt from the initial request is added to chat history and UI
                // 1) Prefer any existing system message already in the in-memory chat history
                // 2) Otherwise, pick the first system message from the initial request body
                // 3) Add it to the chat so it shows in the UI and is part of the first AI turn context
                var systemMessageText = this._chatHistory
                    .OfType<AIInteractionText>()
                    .FirstOrDefault(x => x.Agent == AIAgent.System);

                if (systemMessageText == null)
                {
                    var requestSystem = this._initialRequest?.Body?.Interactions?
                        .OfType<AIInteractionText>()
                        .FirstOrDefault(x => x.Agent == AIAgent.System);

                    if (requestSystem != null && !string.IsNullOrWhiteSpace(requestSystem.Content))
                    {
                        // Insert into history and render
                        this.AddSystemMessage(requestSystem.Content);
                        systemMessageText = requestSystem;
                    }
                }
                else
                {
                    // Already in history: render to UI without duplicating and emit a snapshot
                    this.AddInteractionToWebView(systemMessageText);
                    this.BuildAndEmitSnapshot();
                }

                // Check if AI greeting is enabled in settings
                var settings = SmartHopperSettings.Instance;

                // TODO: Generate greeting only for SmartHopper Assistant, not all chats
                if (!settings.SmartHopperAssistant.EnableAIGreeting)
                {
                    // Skip greeting entirely if disabled
                    this.SetStatus("Ready", "Ready");
                    return;
                }

                // Show loading message immediately in the chat
                await Application.Instance.InvokeAsync(() =>
                {
                    this.AddAssistantMessage("💬 Loading message...");
                    this.SetStatus("Generating greeting...", "Generating greeting...");
                });

                // Generate AI greeting message using a context-aware custom prompt
                string greetingPrompt;
                if (systemMessageText != null && !string.IsNullOrEmpty(systemMessageText.Content))
                {
                    greetingPrompt = $"You are a chat assistant. The user has provided the following instructions:\n---\n{systemMessageText.Content}\n---\nBased on the instructions, generate a brief, friendly greeting message that welcomes the user to the chat and naturally guides the conversation toward your area of expertise. Be warm and professional, highlighting your unique capabilities without overwhelming the user with technical details. Keep it concise and engaging. One or two sentences maximum.";
                }
                else
                {
                    greetingPrompt = "Your job is to generate a brief, friendly greeting message that welcomes the user to the chat. This is a generic purpose chat. Keep the greeting concise: one or two sentences maximum.";
                }

                var greetingInteractions = new List<IAIInteraction>();
                // Keep instructions as system message
                greetingInteractions.Add(new AIInteractionText
                {
                    Agent = AIAgent.System,
                    Content = greetingPrompt,
                });
                // Add a minimal user turn to trigger an assistant reply across providers
                greetingInteractions.Add(new AIInteractionText
                {
                    Agent = AIAgent.User,
                    Content = "Please send a short friendly greeting to start the chat. Keep it to one or two sentences.",
                });

                AIReturn greetingResult = null;

                try
                {
                    // Create request for greeting generation using new AICall models
                    var greetingRequest = new AIRequestCall();
                    greetingRequest.Initialize(
                        this._initialRequest.Provider,
                        this._initialRequest.Model,
                        greetingInteractions,
                        this._initialRequest.Endpoint,
                        AICapability.Text2Text,
                        "-*"); // Disable all tools for greeting

                    // Log provider/model for diagnostics
                    Debug.WriteLine($"[WebChatDialog] Generating greeting with provider='{this._initialRequest.Provider}', model='{this._initialRequest.Model}', interactions={greetingInteractions?.Count ?? 0}");

                    // Execute with tools processing disabled (tool filter set to "-*") and enforce a timeout
                    var execTask = greetingRequest.Exec();
                    var timeoutTask = Task.Delay(30000);
                    var completed = await Task.WhenAny(execTask, timeoutTask).ConfigureAwait(false);
                    if (completed == execTask)
                    {
                        greetingResult = execTask.Result;
                    }
                    else
                    {
                        Debug.WriteLine("[WebChatDialog] Greeting generation timed out after 30s");
                        greetingResult = null;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[WebChatDialog] Error generating greeting: {ex.Message}");
                    greetingResult = null;
                }

                // Diagnostic logging for greeting result
                try
                {
                    if (greetingResult == null)
                    {
                        Debug.WriteLine("[WebChatDialog] Greeting result is null (timeout or exception). Using fallback.");
                    }
                    else
                    {
                        Debug.WriteLine($"[WebChatDialog] GreetingResult.Success={greetingResult.Success}, Error='{greetingResult.ErrorMessage ?? string.Empty}'");
                        var interactions = greetingResult.Body?.Interactions;
                        Debug.WriteLine($"[WebChatDialog] GreetingResult.Interactions.Count={interactions?.Count ?? 0}");
                        if (interactions != null)
                        {
                            foreach (var inter in interactions)
                            {
                                var text = (inter as AIInteractionText)?.Content ?? string.Empty;
                                var preview = text.Length > 200 ? text.Substring(0, 200) + "..." : text;
                                Debug.WriteLine($"[WebChatDialog] -> {inter.Agent}: {preview}");
                            }
                        }
                    }
                }
                catch (Exception diagEx)
                {
                    Debug.WriteLine($"[WebChatDialog] Error while logging greeting diagnostics: {diagEx.Message}");
                }

                // Replace loading message with actual greeting or fallback using streaming-style replace
                await Application.Instance.InvokeAsync(() =>
                {
                    AIInteractionText finalGreeting = null;
                    if (greetingResult != null && greetingResult.Success &&
                        greetingResult.Body.Interactions?.Count > 0)
                    {
                        finalGreeting = greetingResult.Body.Interactions
                            .OfType<AIInteractionText>()
                            .LastOrDefault(i => i.Agent == AIAgent.Assistant);
                    }

                    if (finalGreeting == null || string.IsNullOrWhiteSpace(finalGreeting.Content))
                    {
                        finalGreeting = new AIInteractionText
                        {
                            Agent = AIAgent.Assistant,
                            Content = defaultGreeting,
                            Metrics = new AIMetrics()
                        };
                    }

                    // Persist and update UI via the helper
                    this.ReplaceLastAssistantMessage(finalGreeting);

                    this.SetStatus("Ready", "Ready");
                }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebChatDialog] Error in InitializeNewConversation: {ex.Message}");

                // Ensure we clean up and show fallback greeting on any error
                await Application.Instance.InvokeAsync(() =>
                {
                    // Clear any loading messages and add fallback greeting
                    this.RemoveLastAssistantMessage();
                    this.AddAssistantMessage(defaultGreeting);
                    this.SetStatus("Ready", "Ready");
                }).ConfigureAwait(false);
            }
        }
    }
}

