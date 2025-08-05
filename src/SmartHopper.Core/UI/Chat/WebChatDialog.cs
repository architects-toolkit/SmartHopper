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
using Newtonsoft.Json.Linq;
using SmartHopper.Core.Messaging;
using SmartHopper.Infrastructure.Managers.AIProviders;
using SmartHopper.Infrastructure.Managers.AITools;
using SmartHopper.Infrastructure.Models;
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

        // Chat state
        private readonly List<ChatMessageModel> _chatHistory;
        private bool _isProcessing;
        private readonly Func<List<ChatMessageModel>, Task<AIResponse>> _getResponse;
        private readonly HtmlChatRenderer _htmlRenderer;
        private readonly string _providerName;
        private bool _webViewInitialized = false;
        private readonly TaskCompletionSource<bool> _webViewInitializedTcs = new TaskCompletionSource<bool>();
        private readonly Action<string>? _progressReporter;
        private readonly string? _systemPrompt;

        /// <summary>
        /// Event raised when a new AI response is received.
        /// </summary>
        public event EventHandler<AIResponse> ResponseReceived;

        private static readonly Assembly ConfigAssembly = typeof(providersResources).Assembly;
        private const string IconResourceName = "SmartHopper.Infrastructure.Resources.smarthopper.ico";

        /// <summary>
        /// Creates a new web chat dialog.
        /// </summary>
        /// <param name="getResponse">Function to get responses from the AI provider.</param>
        /// <param name="providerName">The name of the AI provider to use for default model operations.</param>
        /// <param name="systemPrompt">Optional system prompt to provide to the AI assistant.</param>
        /// <param name="progressReporter">Optional callback to report progress updates.</param>
        public WebChatDialog(Func<List<ChatMessageModel>, Task<AIResponse>> getResponse, string providerName, string? systemPrompt = null, Action<string>? progressReporter = null)
        {
            Debug.WriteLine("[WebChatDialog] Initializing WebChatDialog");
            this._progressReporter = progressReporter;
            this._systemPrompt = systemPrompt;
            this._providerName = providerName ?? throw new ArgumentNullException(nameof(providerName));

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

            // Wrap the incoming getResponse delegate with logging for entry and exit
            if (getResponse == null) throw new ArgumentNullException(nameof(getResponse));
            Debug.WriteLine($"[WebChatDialog] getResponse delegate passed in: {getResponse.Method.DeclaringType.FullName}.{getResponse.Method.Name}");
            var originalGetResponse = getResponse;
            this._getResponse = async messages =>
            {
                Debug.WriteLine($"[WebChatDialog] Calling getResponse delegate ({originalGetResponse.Method.DeclaringType.FullName}.{originalGetResponse.Method.Name}) with {messages.Count} messages");
                var resp = await originalGetResponse(messages);
                Debug.WriteLine($"[WebChatDialog] getResponse completed: ToolCalls count = {resp?.ToolCalls?.Count ?? 0}");
                return resp;
            };

            this._chatHistory = new List<ChatMessageModel>();
            this._htmlRenderer = new HtmlChatRenderer();

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
        /// Adds a user message to the chat history and displays it in the WebView.
        /// </summary>
        /// <param name="response">The AI response object containing the message.</param>
        private void AddUserMessage(AIResponse response)
        {
            this._chatHistory.Add(new ChatMessageModel
            {
                Author = "user",
                Body = response.Response,
                Inbound = false,
                Read = false,
                Time = DateTime.Now,
            });

            this.AddMessageToWebView("user", response);
        }

        /// <summary>
        /// Adds a user message to the chat history and displays it in the WebView.
        /// </summary>
        /// <param name="message">The message text.</param>
        private void AddUserMessage(string message)
        {
            var response = new AIResponse()
            {
                Response = message,
            };

            this.AddUserMessage(response);
        }

        /// <summary>
        /// Adds an assistant (AI) message with metrics information.
        /// </summary>
        /// <param name="message">The message text.</param>
        /// <param name="response">The AI response object containing metrics.</param>
        private void AddAssistantMessage(AIResponse response)
        {
            this._chatHistory.Add(new ChatMessageModel
            {
                Author = "assistant",
                Body = response.Response,
                Inbound = true,
                Read = false,
                Time = DateTime.Now,
                ToolCalls = new List<AIToolCall>(response.ToolCalls),
            });

            this.AddMessageToWebView("assistant", response);
        }

        /// <summary>
        /// Removes the last message of a specific role from both chat history and WebView.
        /// </summary>
        /// <param name="role">The role of the message to remove (user, assistant, system).</param>
        /// <returns>True if a message was removed, false otherwise.</returns>
        private bool RemoveLastMessage(string role)
        {
            if (string.IsNullOrEmpty(role))
            {
                Debug.WriteLine("[WebChatDialog] Cannot remove message with empty role");
                return false;
            }

            try
            {
                // Find the last message of the specified role in chat history
                var lastMessage = this._chatHistory.LastOrDefault(m => m.Author == role);
                if (lastMessage == null)
                {
                    Debug.WriteLine($"[WebChatDialog] No {role} messages found to remove");
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
                        string sanitizedRole = JsonConvert.SerializeObject(role);
                        string script = $"if (typeof removeLastMessageByRole === 'function') {{ return removeLastMessageByRole({sanitizedRole}); }} else {{ return false; }}";
                        string result = this._webView.ExecuteScript(script);
                        removedFromUI = bool.TryParse(result, out bool jsResult) && jsResult;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[WebChatDialog] Error removing {role} message from UI: {ex.Message}");
                    }
                }

                Debug.WriteLine($"[WebChatDialog] Removed last {role} message - History: {removedFromHistory}, UI: {removedFromUI}");
                return removedFromHistory; // Return true if removed from history, even if UI removal failed
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebChatDialog] Error removing last {role} message: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Removes the last assistant message from both chat history and WebView.
        /// </summary>
        /// <returns>True if a message was removed, false otherwise.</returns>
        private bool RemoveLastAssistantMessage()
        {
            return this.RemoveLastMessage("assistant");
        }

        /// <summary>
        /// Adds an assistant message to the chat history and displays it in the WebView.
        /// </summary>
        /// <param name="message">The message text.</param>
        private void AddAssistantMessage(string message)
        {
            var response = new AIResponse()
            {
                Response = message,
            };

            this.AddAssistantMessage(response);
        }

        /// <summary>
        /// Adds a system message with an optional subtype (e.g., "error").
        /// </summary>
        /// <param name="message">The message text.</param>
        /// <param name="type">Optional subtype for styling (e.g., "error").</param>
        private void AddSystemMessage(AIResponse response, string type = null)
        {
            this._chatHistory.Add(new ChatMessageModel
            {
                Author = "system",
                Body = response.Response,
                Inbound = true,
                Read = false,
                Time = DateTime.Now,
            });

            // In the web view, use the combined role with optional type
            var role = "system" + (string.IsNullOrEmpty(type) ? "" : " " + type);
            this.AddMessageToWebView(role, response);
        }

        private void AddSystemMessage(string message, string type = null)
        {
            var response = new AIResponse()
            {
                Response = message,
            };

            this.AddSystemMessage(response, type);
        }

        /// <summary>
        /// Adds a message to the WebView with the specified role.
        /// </summary>
        /// <param name="role">The role of the message (e.g., "user", "assistant", "system").</param>
        /// <param name="response">The AI response object containing metrics.</param>
        private void AddMessageToWebView(string role, AIResponse response)
        {
            if (string.IsNullOrEmpty(response.Response))
            {
                Debug.WriteLine($"[WebChatDialog] Skipping empty message for role: {role}");
                return;
            }

            if (!this._webViewInitialized)
            {
                // Queue the message to be added after initialization
                Debug.WriteLine($"[WebChatDialog] WebView not initialized yet, queueing message: {role}");
                Task.Run(async () =>
                {
                    try
                    {
                        await this._webViewInitializedTcs.Task;
                        Debug.WriteLine($"[WebChatDialog] WebView now initialized, adding queued message: {role}");
                        Application.Instance.AsyncInvoke(() => this.AddMessageToWebView(role, response));
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
                // Automatically add loading class for loading messages (any role)
                string finalRole = role;
                if (response?.FinishReason == "loading")
                {
                    finalRole = role + " loading";
                }

                // Generate HTML for the message
                Debug.WriteLine($"[WebChatDialog] Generating HTML for message: {finalRole}");
                string messageHtml = this._htmlRenderer.GenerateMessageHtml(finalRole, response);

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
                await this.GetAIResponseAndProcessToolCalls();
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
        /// Gets a response from the AI provider and processes any tool calls in the response.
        /// </summary>
        private async Task GetAIResponseAndProcessToolCalls()
        {
            try
            {
                // Create a copy of the chat history for the API call
                var messages = this._chatHistory.ToList();

                Debug.WriteLine("[WebChatDialog] Getting response from AI provider");
                // Get response from AI provider using the provided function
                var response = await this._getResponse(messages);

                if (response == null)
                {
                    Debug.WriteLine("[WebChatDialog] No response received from AI provider");
                    this.AddSystemMessage("Error: Failed to get response from AI provider.", "error");
                    Application.Instance.AsyncInvoke(() =>
                    {
                        this._statusLabel.Text = "Error: No response received";
                        this._progressReporter?.Invoke("Error :(");
                    });
                    return;
                }

                // If AI finished with error reason, display error message with red background
                if (!string.IsNullOrEmpty(response.FinishReason) && response.FinishReason.Equals("error", StringComparison.OrdinalIgnoreCase))
                {
                    Debug.WriteLine("[WebChatDialog] Response finishReason is error; showing error message");
                    this.AddSystemMessage(response.Response, "error");
                    Application.Instance.AsyncInvoke(() =>
                    {
                        this._statusLabel.Text = "Error in response";
                        this._progressReporter?.Invoke("Error :(");
                    });
                    return;
                }

                // Check for tool calls in the response
                if (response.ToolCalls != null && response.ToolCalls.Count > 0)
                {
                    // Add the assistant response with tool calls to chat history
                    this.AddAssistantMessage(response);

                    foreach (var toolCall in response.ToolCalls)
                    {
                        Debug.WriteLine($"[WebChatDialog] Tool call detected: {toolCall.Name}");

                        // Show UI-only tool_call entry
                        // AddToolCallMessage(response, toolCall);

                        // Process the tool call (pass along toolCallId)
                        await this.ProcessToolCall(response, toolCall);
                    }
                }
                else
                {
                    Debug.WriteLine("[WebChatDialog] Regular response received, adding to chat");
                    // Add response to chat history as a regular message
                    this.AddAssistantMessage(response);

                    // Notify listeners
                    this.ResponseReceived?.Invoke(this, response);

                    Application.Instance.AsyncInvoke(() =>
                    {
                        // _statusLabel.Text = $"Response received ({response.InTokens} in, {response.OutTokens} out)";
                        this._statusLabel.Text = $"Ready";
                        this._progressReporter?.Invoke("Ready");
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebChatDialog] Error in GetAIResponseAndProcessToolCalls: {ex.Message}");
                throw; // Rethrow to be handled by SendMessage
            }
        }

        /// <summary>
        /// Processes a tool call by executing the tool and getting a new response.
        /// </summary>
        /// <param name="parentResponse">Parent response that triggered the tool call.</param>
        /// <param name="toolCall">Tool call details.</param>
        private async Task ProcessToolCall(AIResponse parentResponse, AIToolCall toolCall)
        {
            try
            {
                // Parse tool arguments
                JObject parameters = JObject.Parse(toolCall.Arguments);

                Debug.WriteLine($"[WebChatDialog] Processing tool call: {toolCall.Name}");
                Application.Instance.AsyncInvoke(() =>
                {
                    this._statusLabel.Text = $"Executing tool: {toolCall.Name}...";
                    this._progressReporter?.Invoke("Executing a tool...");
                });

                // Execute the tool
                var result = await AIToolManager.ExecuteTool(
                    toolCall.Name,
                    JObject.Parse(toolCall.Arguments),
                    new JObject { ["provider"] = parentResponse.Provider, ["model"] = parentResponse.Model }
                );
                var resultJson = JsonConvert.SerializeObject(result, Formatting.Indented);

                // wrap the tool result in an AIResponse
                var toolResponse = new AIResponse
                {
                    Response = $"⚙️ **Tool Result**:\n```json\n{resultJson}\n```",
                    Provider = parentResponse.Provider,
                    Model = parentResponse.Model,
                    FinishReason = null,
                    ToolCalls = new List<AIToolCall> { toolCall },
                };

                // Add tool result to chat history
                this.AddToolResultMessage(toolResponse);

                // Add tool result to chat history for the AI to see
                this._chatHistory.Add(new ChatMessageModel
                {
                    Author = "tool",
                    Body = resultJson,
                    Inbound = true,
                    Read = false,
                    Time = DateTime.Now,
                    ToolCalls = new List<AIToolCall> { toolCall },
                });

                // Get a new response from the AI with the tool result
                await this.GetAIResponseAndProcessToolCalls();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebChatDialog] Error processing tool call: {ex.Message}");
                this.AddSystemMessage($"Error executing tool '{toolCall.Name}': {ex.Message}", "error");
            }
        }

        /// <summary>
        /// Adds a tool call message to the chat display.
        /// </summary>
        /// <param name="parentResponse">Parent response that triggered the tool call.</param>
        /// <param name="toolCall">Tool call details.</param>
        private void AddToolCallMessage(AIResponse parentResponse, AIToolCall toolCall)
        {
            try
            {
                // Create a formatted message
                var formatted = JsonConvert.SerializeObject(JObject.Parse(toolCall.Arguments), Formatting.Indented);

                Debug.WriteLine($"[WebChatDialog] Adding tool call {toolCall.Id}: {toolCall.Name} ({toolCall.Arguments})");

                // Add to chat history
                this._chatHistory.Add(new ChatMessageModel
                {
                    Author = "tool_call",
                    Body = "Calling tool: " + toolCall.Name,
                    Inbound = true,
                    Read = false,
                    Time = DateTime.Now,
                    ToolCalls = new List<AIToolCall> { toolCall },
                });
                this.AddMessageToWebView("tool_call", parentResponse);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebChatDialog] Error formatting tool call: {ex.Message}");
                this.AddSystemMessage($"Tool Call: {toolCall.Name} (Error formatting arguments: {ex.Message})", "error");
            }
        }

        /// <summary>
        /// Adds a tool result message to the chat display.
        /// </summary>
        /// <param name="toolResponse">Result from the tool execution.</param>
        private void AddToolResultMessage(AIResponse toolResponse)
        {
            try
            {
                // Pretty-print JSON and render a tool bubble
                this.AddMessageToWebView("tool", toolResponse);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebChatDialog] Error formatting tool result: {ex.Message}");
                this.AddSystemMessage($"Tool Result: (Error formatting result: {ex.Message})", "error");
            }
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
                // Display system message as collapsible if provided
                if (!string.IsNullOrEmpty(this._systemPrompt))
                {
                    this.AddSystemMessage(this._systemPrompt);
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
                var loadingMessage = new ChatMessageModel
                {
                    Author = "assistant",
                    Body = "💬 Loading message...",
                    Inbound = true,
                    Read = false,
                    Time = DateTime.Now,
                };

                // Display the loading message immediately
                var loadingResponse = new AIResponse
                {
                    Response = loadingMessage.Body,
                    FinishReason = "loading",
                };

                await Application.Instance.InvokeAsync(() =>
                {
                    this.AddAssistantMessage(loadingResponse);
                    this._statusLabel.Text = "Generating greeting...";
                    this._progressReporter?.Invoke("Generating greeting...");
                });

                // Generate AI greeting message using a context-aware custom prompt
                string greetingPrompt;
                if (!string.IsNullOrEmpty(this._systemPrompt))
                {
                    greetingPrompt = $"You are a chat assistant with specialized knowledge and capabilities. The user has provided the following system instructions that define your role and expertise:\n\n{this._systemPrompt}\n\nBased on these instructions, generate a brief, friendly greeting message that welcomes the user to the chat and naturally guides the conversation toward your area of expertise. Be warm and professional, highlighting your unique capabilities without overwhelming the user with technical details. Keep it concise and engaging. One or two sentences maximum.";
                }
                else
                {
                    greetingPrompt = "You are SmartHopper AI, an AI assistant for Grasshopper3D and computational design. Generate a brief, friendly greeting message that welcomes the user and offers assistance. Keep it concise, professional, and inviting.";
                }

                var greetingMessages = new List<ChatMessageModel>
                {
                    new ChatMessageModel
                    {
                        Author = "system",
                        Body = greetingPrompt,
                        Inbound = true,
                        Read = false,
                        Time = DateTime.Now,
                    },
                };

                AIResponse greetingResponse = null;

                try
                {
                    // Use AIUtils.GetResponse with the specific provider name and no model (defaults to provider's default model)
                    greetingResponse = await AIUtils.GetResponse(
                        this._providerName,
                        "", // Empty model string will trigger default model usage (a fast and cheap model for general purpose)
                        greetingMessages,
                        jsonSchema: "",
                        endpoint: "",
                        toolFilter: "-*").ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[WebChatDialog] Error generating greeting: {ex.Message}");
                    greetingResponse = null;
                }

                // Replace loading message with actual greeting or fallback
                await Application.Instance.InvokeAsync(() =>
                {
                    // Remove loading message from both history and UI
                    this.RemoveLastAssistantMessage();

                    // Add the actual greeting message
                    if (greetingResponse != null && !string.IsNullOrEmpty(greetingResponse.Response))
                    {
                        // Add the AI-generated greeting as an assistant message
                        this.AddAssistantMessage(greetingResponse);
                    }
                    else
                    {
                        // Fallback to default greeting if AI generation fails or times out
                        this.AddAssistantMessage(defaultGreeting);
                    }

                    this._statusLabel.Text = "Ready";
                    this._progressReporter?.Invoke("Ready");
                });
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
                });
            }
        }
    }
}
