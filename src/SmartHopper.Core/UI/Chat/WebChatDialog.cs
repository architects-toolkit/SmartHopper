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
using System.Threading.Tasks;
using Eto.Drawing;
using Eto.Forms;
using Newtonsoft.Json;
using SmartHopper.Infrastructure.AICall;
using SmartHopper.Infrastructure.AIModels;
using SmartHopper.Infrastructure.Properties;
using SmartHopper.Infrastructure.Settings;

namespace SmartHopper.Core.UI.Chat
{
    /// <summary>
    /// Dialog-based chat interface using WebView for rendering HTML content.
    /// </summary>
    internal class WebChatDialog : Form
    {
        // UI Components
        private readonly WebView _webView;
        private readonly TextArea _userInputTextArea;
        private readonly Button _sendButton;
        private readonly Button _clearButton;
        private readonly ProgressBar _progressBar;
        private readonly Label _statusLabel;

        // Chat Dialog
        private bool _isProcessing;
        private readonly Action<string>? _progressReporter;
        private readonly HtmlChatRenderer _htmlRenderer = new HtmlChatRenderer();
        private bool _webViewInitialized = false;
        private readonly TaskCompletionSource<bool> _webViewInitializedTcs = new TaskCompletionSource<bool>();

        // Chat history and request management
        private readonly AIRequestCall _initialRequest;
        private readonly List<IAIInteraction> _chatHistory = new List<IAIInteraction>();
        private AIReturn _lastReturn = new AIReturn();

        /// <summary>
        /// Event raised when a new AI response is received.
        /// </summary>
        public event EventHandler<AIReturn> ResponseReceived;

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
        /// Initializes the WebView control asynchronously.
        /// </summary>
        private async Task InitializeWebViewAsync()
        {
            try
            {
                Debug.WriteLine("[WebChatDialog] Starting WebView initialization from background thread");

                // Get the HTML content on the background thread
                string html = this._htmlRenderer.GetInitialHtml();
                Debug.WriteLine($"[WebChatDialog] HTML prepared, length: {html?.Length ?? 0}");

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

                        // Load the HTML content
                        this._webView.LoadHtml(html);

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

                // Add the welcome message on a background thread
                await Task.Run(async () =>
                {
                    try
                    {
                        await Application.Instance.InvokeAsync(() =>
                        {
                            this.InitializeNewConversation();
                        }).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[WebChatDialog] Error adding welcome message: {ex.Message}");
                    }
                }).ConfigureAwait(false);

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
            if (!this._isProcessing)
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

            // Reset the WebView with initial HTML
            try
            {
                Debug.WriteLine("[WebChatDialog] Clearing chat and reloading HTML");

                // Get the HTML content directly
                string html = this._htmlRenderer.GetInitialHtml();

                // Load HTML into WebView
                this._webView.LoadHtml(html);

                // Add system message to start the conversation
                this.InitializeNewConversation();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebChatDialog] Error clearing chat: {ex.Message}");
            }
        }

        /// <summary>
        /// Adds an interaction to the chat history and displays it in the WebView.
        /// </summary>
        /// <param name="interaction">The AI interaction object containing the message.</param>
        private void AddInteraction(IAIInteraction interaction)
        {
            this._chatHistory.Add(interaction);
            this.AddInteractionToWebView(interaction);
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
                        string sanitizedRole = JsonConvert.SerializeObject(agent.ToString());
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
        /// Adds an interaction to the WebView with the specified role.
        /// </summary>
        /// <param name="interaction">The AI interaction object containing metrics.</param>
        private void AddInteractionToWebView(IAIInteraction interaction)
        {
            if (interaction is null)
            {
                Debug.WriteLine($"[WebChatDialog] Skipping empty message for role: {interaction.Agent.ToString()}");
                return;
            }

            if (!this._webViewInitialized)
            {
                // Queue the message to be added after initialization
                Debug.WriteLine($"[WebChatDialog] WebView not initialized yet, queueing message: {interaction.Agent}");
                Task.Run(async () =>
                {
                    try
                    {
                        await this._webViewInitializedTcs.Task.ConfigureAwait(false);
                        Debug.WriteLine($"[WebChatDialog] WebView now initialized, adding queued message: {interaction.Agent}");
                        Application.Instance.AsyncInvoke(() => this.AddInteractionToWebView(interaction));
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[WebChatDialog] Error waiting for WebView initialization to add message: {ex.Message}");
                    }
                });
                return;
            }

            try
            {
                // Generate HTML for the message
                Debug.WriteLine($"[WebChatDialog] Generating HTML for message: {interaction.Agent.ToString()}");
                string messageHtml = this._htmlRenderer.GenerateMessageHtml(interaction);

                // Execute JavaScript to add the message to the WebView
                Debug.WriteLine("[WebChatDialog] Executing JavaScript to add message");

                // Escape special characters in the message HTML
                string escapedHtml = messageHtml
                    .Replace("\\", "\\\\")
                    .Replace("\"", "\\\"")
                    .Replace("\n", "\\n")
                    .Replace("\r", "\\r")
                    .Replace("\t", "\\t");

                // Try a different approach for executing JavaScript
                string script = $"if (typeof addMessage === 'function') {{ addMessage(\"{escapedHtml}\"); return 'Message added'; }} else {{ return 'addMessage function not found'; }}";
                string result = this._webView.ExecuteScript(script);

                Debug.WriteLine($"[WebChatDialog] JavaScript execution result: {result}");

                // Scroll to bottom
                this._webView.ExecuteScript("if (typeof scrollToBottom === 'function') { scrollToBottom(); }");
                Debug.WriteLine("[WebChatDialog] Message added successfully");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebChatDialog] Error adding message to WebView: {ex.Message}");
                Debug.WriteLine($"[WebChatDialog] Error stack trace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Sends the user message to the AI provider and processes the response.
        /// </summary>
        private async void SendMessage()
        {
            if (!this._webViewInitialized)
            {
                Debug.WriteLine("[WebChatDialog] Cannot send message, WebView not initialized");
                Application.Instance.AsyncInvoke(() =>
                {
                    this._statusLabel.Text = "WebView is still initializing. Please wait...";
                });
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
            Application.Instance.AsyncInvoke(() =>
            {
                this._statusLabel.Text = "Thinking...";
                this._progressReporter?.Invoke("Thinking...");
            });

            try
            {
                await this.ProcessAIInteraction();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebChatDialog] Error getting response: {ex.Message}");
                this.AddSystemMessage($"Error: {ex.Message}", "error");
                Application.Instance.AsyncInvoke(() =>
                {
                    this._statusLabel.Text = "Error occurred";
                    this._progressReporter?.Invoke("Error :(");
                });
            }
            finally
            {
                // Restore UI state
                this._isProcessing = false;
                this._sendButton.Enabled = true;
                this._progressBar.Visible = false;
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
                request.OverrideInteractions(this._chatHistory);

                Debug.WriteLine("[WebChatDialog] Getting response from AI provider");
                var result = await request.Exec(processTools: true).ConfigureAwait(false);

                // Store the last return for external access
                this._lastReturn = result;

                if (!result.Success)
                {
                    Debug.WriteLine($"[WebChatDialog] AI request failed: {result.ErrorMessage}");
                    this.AddSystemMessage($"Error: {result.ErrorMessage}", "error");
                    Application.Instance.AsyncInvoke(() =>
                    {
                        this._statusLabel.Text = "Error in response";
                        this._progressReporter?.Invoke("Error :(");
                    });
                    return;
                }

                // Add returned interactions to chat history and display them
                if (result.Body.Interactions?.Count > 0)
                {
                    foreach (var interaction in result.Body.Interactions)
                    {
                        this._chatHistory.Add(interaction);
                        await Application.Instance.InvokeAsync(() => this.AddInteractionToWebView(interaction));
                    }
                }

                // Process any pending tool calls
                // await this.ProcessPendingToolCalls(result);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebChatDialog] Error in ProcessAIInteraction: {ex.Message}");
                throw; // Rethrow to be handled by SendMessage
            }
        }

        // /// <summary>
        // /// Processes any pending tool calls in the AI return using the new AIToolCall.Exec() method.
        // /// </summary>
        // /// <param name="result">The AI return containing potential tool calls.</param>
        // private async Task ProcessPendingToolCalls(AIReturn result)
        // {
        //     // Check if there are pending tool calls in the result
        //     if (result.Body?.PendingToolCallsCount() > 0)
        //     {
        //         foreach (var pendingTool in result.Body.PendingToolCallsList())
        //         {
        //             await Application.Instance.InvokeAsync(() =>
        //             {
        //                 this._statusLabel.Text = $"Executing tool: {pendingTool.Name}...";
        //                 this._progressReporter?.Invoke("Executing tool...");
        //             });

        //             Debug.WriteLine($"[WebChatDialog] Processing tool call: {pendingTool.Name}");

        //             try
        //             {
        //                 // Create tool call request using the new architecture
        //                 var toolCall = new AIToolCall();
        //                 toolCall.Initialize(
        //                     this._initialRequest.Provider,
        //                     this._initialRequest.Model,
        //                     result.Body,
        //                     this._initialRequest.Endpoint,
        //                     this._initialRequest.Capability);

        //                 // Execute tool using AIToolCall.Exec()
        //                 var toolResult = await toolCall.Exec().ConfigureAwait(false);

        //                 if (toolResult.Success && toolResult.Body.Interactions?.Count > 0)
        //                 {
        //                     // Add tool result interactions to history and display them
        //                     foreach (var interaction in toolResult.Body.Interactions)
        //                     {
        //                         this._chatHistory.Add(interaction);
        //                         await Application.Instance.InvokeAsync(() => this.AddInteractionToWebView(interaction));
        //                     }

        //                     // Continue conversation with tool result
        //                     await this.ProcessAIInteraction();
        //                 }
        //                 else
        //                 {
        //                     this.AddSystemMessage($"Tool execution failed: {toolResult.ErrorMessage}", "error");
        //                     await Application.Instance.InvokeAsync(() =>
        //                     {
        //                         this._statusLabel.Text = "Tool execution failed";
        //                         this._progressReporter?.Invoke("Error :(");
        //                     });
        //                 }
        //             }
        //             catch (Exception ex)
        //             {
        //                 Debug.WriteLine($"[WebChatDialog] Error processing tool call: {ex.Message}");
        //                 this.AddSystemMessage($"Error executing tool '{pendingTool.Name}': {ex.Message}", "error");
        //             }
        //         }
        //     }
        //     else
        //     {
        //         // No tool calls, conversation complete
        //         this.ResponseReceived?.Invoke(this, result);
        //         await Application.Instance.InvokeAsync(() =>
        //         {
        //             this._statusLabel.Text = "Ready";
        //             this._progressReporter?.Invoke("Ready");
        //         });
        //     }
        // }

        /// <summary>
        /// Initializes a new conversation with a collapsible system message and an AI-generated greeting message.
        /// Keeps the chat in loading state until greeting is received or 30s timeout occurs.
        /// </summary>
        private async void InitializeNewConversation()
        {
            var defaultGreeting = "Hello! I'm your AI assistant. How can I help you today?";

            try
            {
                // Display system message as collapsible if provided

                // TODO: Why is systemMessageText always null? Is _chatHistory empty?

                var systemMessageText = this._chatHistory
                    .OfType<AIInteractionText>()
                    .FirstOrDefault(x => x.Agent == AIAgent.System);

                if (systemMessageText != null)
                {
                    this.AddSystemMessage(systemMessageText.Content);
                }

                // Check if AI greeting is enabled in settings
                var settings = SmartHopperSettings.Instance;

                // TODO: Generate greeting only for SmartHopper Assistant, not all chats
                if (!settings.SmartHopperAssistant.EnableAIGreeting)
                {
                    // Skip greeting entirely if disabled
                    await Application.Instance.InvokeAsync(() =>
                    {
                        this._statusLabel.Text = "Ready";
                        this._progressReporter?.Invoke("Ready");
                    });
                    return;
                }

                // Show loading message immediately in the chat
                await Application.Instance.InvokeAsync(() =>
                {
                    this.AddAssistantMessage("💬 Loading message...");
                    this._statusLabel.Text = "Generating greeting...";
                    this._progressReporter?.Invoke("Generating greeting...");
                });

                // Generate AI greeting message using a context-aware custom prompt
                string greetingPrompt;
                if (systemMessageText != null && !string.IsNullOrEmpty(systemMessageText.Content))
                {
                    greetingPrompt = $"You are a chat assistant with specialized knowledge and capabilities. The user has provided the following system instructions that define your role and expertise:\n\n{systemMessageText.Content}\n\nBased on these instructions, generate a brief, friendly greeting message that welcomes the user to the chat and naturally guides the conversation toward your area of expertise. Be warm and professional, highlighting your unique capabilities without overwhelming the user with technical details. Keep it concise and engaging. One or two sentences maximum.";
                }
                else
                {
                    greetingPrompt = "You are SmartHopper AI, an AI assistant for Grasshopper3D and computational design. Generate a brief, friendly greeting message that welcomes the user and offers assistance. Keep it concise, professional, and inviting.";
                }

                var greetingInteractions = new List<IAIInteraction>
                {
                    new AIInteractionText
                    {
                        Agent = AIAgent.System,
                        Content = greetingPrompt,
                    },
                };

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
                        AICapability.TextOutput,
                        "-*"); // Disable all tools for greeting

                    greetingResult = await greetingRequest.Exec().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[WebChatDialog] Error generating greeting: {ex.Message}");
                    greetingResult = null;
                }

                // Replace loading message with actual greeting or fallback
                await Application.Instance.InvokeAsync(() =>
                {
                    // Remove loading message from both history and UI
                    this.RemoveLastAssistantMessage();

                    // Add the actual greeting message
                    if (greetingResult != null && greetingResult.Success &&
                        greetingResult.Body.Interactions?.Count > 0)
                    {
                        // Find the assistant response in the interactions
                        var assistantInteraction = greetingResult.Body.Interactions
                            .OfType<AIInteractionText>()
                            .LastOrDefault(i => i.Agent == AIAgent.Assistant);

                        if (assistantInteraction != null && !string.IsNullOrEmpty(assistantInteraction.Content))
                        {
                            this.AddAssistantMessage(assistantInteraction.Content, assistantInteraction.Metrics);
                        }
                        else
                        {
                            this.AddAssistantMessage(defaultGreeting);
                        }
                    }
                    else
                    {
                        // Fallback to default greeting if AI generation fails or times out
                        this.AddAssistantMessage(defaultGreeting);
                    }

                    this._statusLabel.Text = "Ready";
                    this._progressReporter?.Invoke("Ready");
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
                    this._statusLabel.Text = "Ready";
                    this._progressReporter?.Invoke("Ready");
                }).ConfigureAwait(false);
            }
        }
    }
}
