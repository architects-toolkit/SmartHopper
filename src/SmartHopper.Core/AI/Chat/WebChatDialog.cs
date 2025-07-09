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
using SmartHopper.Config.Managers;
using SmartHopper.Config.Models;
using SmartHopper.Config.Properties;

namespace SmartHopper.Core.AI.Chat
{
    /// <summary>
    /// Dialog-based chat interface using WebView for rendering HTML content.
    /// </summary>
    public class WebChatDialog : Form
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
        private bool _webViewInitialized = false;
        private readonly TaskCompletionSource<bool> _webViewInitializedTcs = new TaskCompletionSource<bool>();
        private readonly Action<string>? _progressReporter;
        private readonly string? _systemPrompt;

        /// <summary>
        /// Event raised when a new AI response is received.
        /// </summary>
        public event EventHandler<AIResponse> ResponseReceived;

        private static readonly Assembly ConfigAssembly = typeof(providersResources).Assembly;
        private const string IconResourceName = "SmartHopper.Config.Resources.smarthopper.ico";

        /// <summary>
        /// Creates a new web chat dialog.
        /// </summary>
        /// <param name="getResponse">Function to get responses from the AI provider</param>
        /// <param name="systemPrompt">Optional system prompt to provide to the AI assistant</param>
        /// <param name="progressReporter">Optional callback to report progress updates</param>
        public WebChatDialog(Func<List<ChatMessageModel>, Task<AIResponse>> getResponse, string? systemPrompt = null, Action<string>? progressReporter = null)
        {
            Debug.WriteLine("[WebChatDialog] Initializing WebChatDialog");
            this._progressReporter = progressReporter;
            this._systemPrompt = systemPrompt;

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
                    Icon = new Eto.Drawing.Icon(stream);
                }
            }

            // Wrap the incoming getResponse delegate with logging for entry and exit
            if (getResponse == null) throw new ArgumentNullException(nameof(getResponse));
            Debug.WriteLine($"[WebChatDialog] getResponse delegate passed in: {getResponse.Method.DeclaringType.FullName}.{getResponse.Method.Name}");
            var originalGetResponse = getResponse;
            _getResponse = async messages =>
            {
                Debug.WriteLine($"[WebChatDialog] Calling getResponse delegate ({originalGetResponse.Method.DeclaringType.FullName}.{originalGetResponse.Method.Name}) with {messages.Count} messages");
                var resp = await originalGetResponse(messages);
                Debug.WriteLine($"[WebChatDialog] getResponse completed: ToolCalls count = {resp?.ToolCalls?.Count ?? 0}");
                return resp;
            };

            _chatHistory = new List<ChatMessageModel>();
            _htmlRenderer = new HtmlChatRenderer();

            Debug.WriteLine("[WebChatDialog] Creating WebView");
            // Initialize WebView
            _webView = new WebView
            {
                Height = 500
            };

            // Add WebView event handlers for debugging
            _webView.DocumentLoaded += (sender, e) => Debug.WriteLine("[WebChatDialog] WebView document loaded");
            _webView.DocumentLoading += (sender, e) => Debug.WriteLine("[WebChatDialog] WebView document loading");
            // Intercept custom clipboard URIs to copy via host and show toast
            _webView.DocumentLoading += (sender, e) =>
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
                        _webView.ExecuteScriptAsync("showToast('Code copied to clipboard :)');");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[WebChatDialog] Clipboard intercept error: {ex.Message}");
                }
            };

            _userInputTextArea = new TextArea
            {
                Height = 60,
                AcceptsReturn = true,
                AcceptsTab = false,
                Wrap = true
            };
            _userInputTextArea.KeyDown += UserInputTextArea_KeyDown;

            _sendButton = new Button
            {
                Text = "Send",
                Enabled = true
            };
            _sendButton.Click += SendButton_Click;

            _clearButton = new Button
            {
                Text = "Clear Chat",
                Enabled = true
            };
            _clearButton.Click += ClearButton_Click;

            _progressBar = new ProgressBar
            {
                Indeterminate = true,
                Visible = false
            };

            _statusLabel = new Label
            {
                Text = "Initializing WebView...",
                TextAlignment = TextAlignment.Center
            };

            // Layout
            var mainLayout = new DynamicLayout();

            // WebView area
            mainLayout.Add(_webView, yscale: true);

            // Input area
            var inputLayout = new DynamicLayout();
            inputLayout.BeginHorizontal();
            inputLayout.Add(_userInputTextArea, xscale: true);
            inputLayout.Add(_sendButton);
            inputLayout.EndHorizontal();
            mainLayout.Add(inputLayout);

            // Controls area
            mainLayout.Add(_clearButton);
            mainLayout.Add(_progressBar);
            mainLayout.Add(_statusLabel);

            Content = mainLayout;
            Padding = new Padding(10);

            Debug.WriteLine("[WebChatDialog] WebChatDialog initialized, starting WebView initialization");

            // Initialize WebView after the dialog is shown, but don't block the UI thread
            this.Shown += (sender, e) =>
            {
                Debug.WriteLine("[WebChatDialog] Dialog shown, starting WebView initialization");

                // Start initialization in a background thread to avoid UI blocking
                Task.Run(() => InitializeWebViewAsync())
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
                EnsureVisibility();
            };

            // Also ensure visibility when the window is shown
            this.Shown += (sender, e) =>
            {
                Debug.WriteLine("[WebChatDialog] Window shown");
                EnsureVisibility();
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
                if (WindowState == WindowState.Minimized)
                {
                    WindowState = WindowState.Normal;
                }

                // Use Eto's built-in methods to bring window to front
                BringToFront();
                Focus();

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
                string html = _htmlRenderer.GetInitialHtml();
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
                            Debug.WriteLine("[WebChatDialog] WebView document loaded event fired");
                            _webView.DocumentLoaded -= loadHandler;
                            loadCompletionSource.TrySetResult(true);
                        };

                        _webView.DocumentLoaded += loadHandler;

                        // Load the HTML content
                        _webView.LoadHtml(html);

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
                _webViewInitialized = true;
                _webViewInitializedTcs.TrySetResult(true);

                // Add the welcome message on a background thread
                await Task.Run(async () =>
                {
                    try
                    {
                        await Application.Instance.InvokeAsync(() =>
                        {
                            InitializeNewConversation();
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
                if (!_webViewInitialized)
                {
                    _webViewInitialized = true;
                    _webViewInitializedTcs.TrySetException(ex);
                }

                // Update the status label on the UI thread
                await Application.Instance.InvokeAsync(() =>
                {
                    _statusLabel.Text = $"Error initializing WebView: {ex.Message}";
                }).ConfigureAwait(false);
            }
        }

        private void UserInputTextArea_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Keys.Enter && e.Modifiers.HasFlag(Keys.Shift))
            {
                // Shift+Enter adds a new line
                _userInputTextArea.Text += Environment.NewLine;
                e.Handled = true;
            }
            else if (e.Key == Keys.Enter && !_isProcessing && !e.Modifiers.HasFlag(Keys.Shift))
            {
                // Enter sends the message
                SendMessage();
                e.Handled = true;
            }
        }

        private void SendButton_Click(object sender, EventArgs e)
        {
            if (!_isProcessing)
            {
                SendMessage();
            }
        }

        private void ClearButton_Click(object sender, EventArgs e)
        {
            _chatHistory.Clear();

            // Reset the WebView with initial HTML
            try
            {
                Debug.WriteLine("[WebChatDialog] Clearing chat and reloading HTML");

                // Get the HTML content directly
                string html = _htmlRenderer.GetInitialHtml();

                // Load HTML into WebView
                _webView.LoadHtml(html);

                // Add system message to start the conversation
                InitializeNewConversation();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebChatDialog] Error clearing chat: {ex.Message}");
            }
        }

        private void AddUserMessage(AIResponse response)
        {
            _chatHistory.Add(new ChatMessageModel {
                Author   = "user",
                Body     = response.Response,
                Inbound  = false,
                Read     = false,
                Time     = DateTime.Now
            });
            this.AddMessageToWebView("user", response);
        }
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
        /// <param name="message">The message text</param>
        /// <param name="response">The AI response object containing metrics</param>
        private void AddAssistantMessage(AIResponse response)
        {
            _chatHistory.Add(new ChatMessageModel {
                Author    = "assistant",
                Body      = response.Response,
                Inbound   = true,
                Read      = false,
                Time      = DateTime.Now,
                ToolCalls = new List<AIToolCall>(response.ToolCalls)
            });
            
            this.AddMessageToWebView("assistant", response);
        }

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
            _chatHistory.Add(new ChatMessageModel {
                Author   = "system",
                Body     = response.Response,
                Inbound  = true,
                Read     = false,
                Time     = DateTime.Now
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

        /// <param name="role">The role of the message (e.g., "user", "assistant", "system").</param>
        /// <param name="response">The AI response object containing metrics.</param>
        private void AddMessageToWebView(string role, AIResponse response)
        {
            if(string.IsNullOrEmpty(response.Response))
            {
                Debug.WriteLine($"[WebChatDialog] Skipping empty message for role: {role}");
                return;
            }

            if (!_webViewInitialized)
            {
                // Queue the message to be added after initialization
                Debug.WriteLine($"[WebChatDialog] WebView not initialized yet, queueing message: {role}");
                Task.Run(async () =>
                {
                    try
                    {
                        await _webViewInitializedTcs.Task;
                        Debug.WriteLine($"[WebChatDialog] WebView now initialized, adding queued message: {role}");
                        Application.Instance.AsyncInvoke(() => AddMessageToWebView(role, response));
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
                Debug.WriteLine($"[WebChatDialog] Generating HTML for message: {role}");
                string messageHtml = _htmlRenderer.GenerateMessageHtml(role, response);

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
                string result = _webView.ExecuteScript(script);

                Debug.WriteLine($"[WebChatDialog] JavaScript execution result: {result}");

                // Scroll to bottom
                _webView.ExecuteScript("if (typeof scrollToBottom === 'function') { scrollToBottom(); }");
                Debug.WriteLine("[WebChatDialog] Message added successfully");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebChatDialog] Error adding message to WebView: {ex.Message}");
                Debug.WriteLine($"[WebChatDialog] Error stack trace: {ex.StackTrace}");
            }
        }

        private async void SendMessage()
        {
            if (!_webViewInitialized)
            {
                Debug.WriteLine("[WebChatDialog] Cannot send message, WebView not initialized");
                Application.Instance.AsyncInvoke(() =>
                {
                    _statusLabel.Text = "WebView is still initializing. Please wait...";
                });
                return;
            }

            string userMessage = _userInputTextArea.Text.Trim();
            if (string.IsNullOrEmpty(userMessage))
            {
                Debug.WriteLine("[WebChatDialog] Empty message, not sending");
                return;
            }

            // Clear input and add message to history
            _userInputTextArea.Text = string.Empty;
            AddUserMessage(userMessage);

            // Update UI state
            _isProcessing = true;
            _sendButton.Enabled = false;
            _progressBar.Visible = true;
            Application.Instance.AsyncInvoke(() =>
            {
                _statusLabel.Text = "Thinking...";
                _progressReporter?.Invoke("Thinking...");
            });

            try
            {
                await GetAIResponseAndProcessToolCalls();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebChatDialog] Error getting response: {ex.Message}");
                AddSystemMessage($"Error: {ex.Message}", "error");
                Application.Instance.AsyncInvoke(() =>
                {
                    _statusLabel.Text = "Error occurred";
                    _progressReporter?.Invoke("Error :(");
                });
            }
            finally
            {
                // Restore UI state
                _isProcessing = false;
                _sendButton.Enabled = true;
                _progressBar.Visible = false;
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
                var messages = _chatHistory.ToList();

                Debug.WriteLine("[WebChatDialog] Getting response from AI provider");
                // Get response from AI provider using the provided function
                var response = await _getResponse(messages);

                if (response == null)
                {
                    Debug.WriteLine("[WebChatDialog] No response received from AI provider");
                    AddSystemMessage("Error: Failed to get response from AI provider.", "error");
                    Application.Instance.AsyncInvoke(() =>
                    {
                        _statusLabel.Text = "Error: No response received";
                        _progressReporter?.Invoke("Error :(");
                    });
                    return;
                }

                // If AI finished with error reason, display error message with red background
                if (!string.IsNullOrEmpty(response.FinishReason) && response.FinishReason.Equals("error", StringComparison.OrdinalIgnoreCase))
                {
                    Debug.WriteLine("[WebChatDialog] Response finishReason is error; showing error message");
                    AddSystemMessage(response.Response, "error");
                    Application.Instance.AsyncInvoke(() =>
                    {
                        _statusLabel.Text = "Error in response";
                        _progressReporter?.Invoke("Error :(");
                    });
                    return;
                }

                // Check for tool calls in the response
                if (response.ToolCalls != null && response.ToolCalls.Count > 0)
                {
                        // Add the assistant response with tool calls to chat history
                        AddAssistantMessage(response);
                    
                    foreach (var toolCall in response.ToolCalls)
                    {
                        Debug.WriteLine($"[WebChatDialog] Tool call detected: {toolCall.Name}");

                        // Show UI-only tool_call entry
                        // AddToolCallMessage(response, toolCall);

                        // Process the tool call (pass along toolCallId)
                        await ProcessToolCall(response, toolCall);
                    }
                }
                else
                {
                    Debug.WriteLine("[WebChatDialog] Regular response received, adding to chat");
                    // Add response to chat history as a regular message
                    AddAssistantMessage(response);

                    // Notify listeners
                    ResponseReceived?.Invoke(this, response);

                    Application.Instance.AsyncInvoke(() =>
                    {
                        // _statusLabel.Text = $"Response received ({response.InTokens} in, {response.OutTokens} out)";
                        _statusLabel.Text = $"Ready";
                        _progressReporter?.Invoke("Ready");
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
                    _statusLabel.Text = $"Executing tool: {toolCall.Name}...";
                    _progressReporter?.Invoke("Executing a tool...");
                });

                // Execute the tool
                var result = await AIToolManager.ExecuteTool(
                    toolCall.Name,
                    JObject.Parse(toolCall.Arguments),
                    new JObject { ["provider"]=parentResponse.Provider, ["model"]=parentResponse.Model }
                );
                var resultJson = JsonConvert.SerializeObject(result, Formatting.Indented);

                // wrap the tool result in an AIResponse
                var toolResponse = new AIResponse {
                    Response    = $"⚙️ **Tool Result**:\n```json\n{resultJson}\n```",
                    Provider    = parentResponse.Provider,
                    Model       = parentResponse.Model,
                    FinishReason= null,
                    ToolCalls   = new List<AIToolCall> { toolCall },

                };

                // Add tool result to chat history
                AddToolResultMessage(toolResponse);

                // Add tool result to chat history for the AI to see
                _chatHistory.Add(new ChatMessageModel {
                    Author    = "tool",
                    Body      = resultJson,
                    Inbound   = true,
                    Read      = false,
                    Time      = DateTime.Now,
                    ToolCalls = new List<AIToolCall> { toolCall }
                });

                // Get a new response from the AI with the tool result
                await GetAIResponseAndProcessToolCalls();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebChatDialog] Error processing tool call: {ex.Message}");
                AddSystemMessage($"Error executing tool '{toolCall.Name}': {ex.Message}", "error");
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
                _chatHistory.Add(new ChatMessageModel {
                    Author    = "tool_call",
                    Body      = "Calling tool: " + toolCall.Name,
                    Inbound   = true,
                    Read      = false,
                    Time      = DateTime.Now,
                    ToolCalls = new List<AIToolCall> { toolCall }
                });
                AddMessageToWebView("tool_call", parentResponse);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebChatDialog] Error formatting tool call: {ex.Message}");
                AddSystemMessage($"Tool Call: {toolCall.Name} (Error formatting arguments: {ex.Message})", "error");
            }
        }

        /// <summary>
        /// Adds a tool result message to the chat display
        /// </summary>
        /// <param name="toolResponse">Result from the tool execution</param>
        private void AddToolResultMessage(AIResponse toolResponse)
        {
            try
            {
                // Pretty-print JSON and render a tool bubble
                AddMessageToWebView("tool", toolResponse);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebChatDialog] Error formatting tool result: {ex.Message}");
                AddSystemMessage($"Tool Result: (Error formatting result: {ex.Message})", "error");
            }
        }

        /// <summary>
        /// Initializes a new conversation with a welcome message.
        /// </summary>
        private void InitializeNewConversation()
        {
            if (!string.IsNullOrEmpty(_systemPrompt))
            {
                AddSystemMessage(_systemPrompt);
            }
            else
            {
                AddSystemMessage("I'm an AI assistant. How can I help you today?");
            }
            Application.Instance.AsyncInvoke(() =>
            {
                _statusLabel.Text = "Ready";
                _progressReporter?.Invoke("Ready");
            });
        }
    }
}
