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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using Eto.Forms;
using Eto.Drawing;
using SmartHopper.Config.Models;
using SmartHopper.Config.Managers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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
        private readonly List<KeyValuePair<string, string>> _chatHistory;
        private bool _isProcessing;
        private readonly Func<List<KeyValuePair<string, string>>, Task<AIResponse>> _getResponse;
        private readonly HtmlChatRenderer _htmlRenderer;
        private bool _webViewInitialized = false;
        private readonly TaskCompletionSource<bool> _webViewInitializedTcs = new TaskCompletionSource<bool>();

        /// <summary>
        /// Event raised when a new AI response is received.
        /// </summary>
        public event EventHandler<AIResponse> ResponseReceived;

        /// <summary>
        /// Creates a new web chat dialog.
        /// </summary>
        /// <param name="getResponse">Function to get responses from the AI provider</param>
        public WebChatDialog(Func<List<KeyValuePair<string, string>>, Task<AIResponse>> getResponse)
        {
            Debug.WriteLine("[WebChatDialog] Initializing WebChatDialog");
            
            Title = "SmartHopper AI Chat";
            MinimumSize = new Size(600, 700);
            Size = new Size(700, 800);
            Resizable = true;
            ShowInTaskbar = true;
            Owner = null; // Ensure we don't block the parent window
                        
            _getResponse = getResponse ?? throw new ArgumentNullException(nameof(getResponse));
            _chatHistory = new List<KeyValuePair<string, string>>();
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
            this.GotFocus += (sender, e) => {
                Debug.WriteLine("[WebChatDialog] Window got focus");
                EnsureVisibility();
            };
            
            // Also ensure visibility when the window is shown
            this.Shown += (sender, e) => {
                Debug.WriteLine("[WebChatDialog] Window shown");
                EnsureVisibility();
            };
        }

        /// <summary>
        /// Ensures the dialog is visible and in the foreground using cross-platform methods.
        /// </summary>
        public void EnsureVisibility()
        {
            Application.Instance.AsyncInvoke(() => {
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

        private void AddSystemMessage(string message)
        {
            _chatHistory.Add(new KeyValuePair<string, string>("system", message));
            AddMessageToWebView("system", message);
        }

        private void AddUserMessage(string message)
        {
            _chatHistory.Add(new KeyValuePair<string, string>("user", message));
            AddMessageToWebView("user", message);
        }

        private void AddAssistantMessage(string message)
        {
            _chatHistory.Add(new KeyValuePair<string, string>("assistant", message));
            AddMessageToWebView("assistant", message);
        }

        private void AddMessageToWebView(string role, string content)
        {
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
                        Application.Instance.AsyncInvoke(() => AddMessageToWebView(role, content));
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
                string messageHtml = _htmlRenderer.GenerateMessageHtml(role, content);
                
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

        private void ShowTemporaryStatusMessage(string message, int seconds = 2)
        {
            _statusLabel.Text = message;
            
            // Reset status after specified seconds using a Timer
            var statusResetTimer = new System.Threading.Timer(_ =>
            {
                Application.Instance.AsyncInvoke(() =>
                {
                    _statusLabel.Text = "Ready";
                });
            }, null, seconds * 1000, Timeout.Infinite);
        }

        private async void SendMessage()
        {
            if (!_webViewInitialized)
            {
                Debug.WriteLine("[WebChatDialog] Cannot send message, WebView not initialized");
                ShowTemporaryStatusMessage("WebView is still initializing. Please wait...", 3);
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
            _statusLabel.Text = "Waiting for response...";

            try
            {
                await GetAIResponseAndProcessToolCalls();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebChatDialog] Error getting response: {ex.Message}");
                AddSystemMessage($"Error: {ex.Message}");
                _statusLabel.Text = "Error occurred";
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
                    AddSystemMessage("Error: Failed to get response from AI provider.");
                    _statusLabel.Text = "Error: No response received";
                    return;
                }
                
                // Check for tool calls in the response
                if (response.ToolCalls != null && response.ToolCalls.Count > 0)
                {
                    foreach (var toolCall in response.ToolCalls)
                    {
                        Debug.WriteLine($"[WebChatDialog] Tool call detected: {toolCall.Name}");
                        
                        // Don't add the tool call message to chat history as regular text
                        // Instead, add a formatted tool call message
                        AddToolCallMessage(toolCall.Name, toolCall.Arguments);
                        
                        // Process the tool call
                        await ProcessToolCall(toolCall.Name, toolCall.Arguments, response.Provider, response.Model);
                    }
                }
                else
                {
                    Debug.WriteLine("[WebChatDialog] Regular response received, adding to chat");
                    // Add response to chat history as a regular message
                    AddAssistantMessage(response.Response);
                    
                    // Notify listeners
                    ResponseReceived?.Invoke(this, response);
                    
                    _statusLabel.Text = $"Response received ({response.InTokens} in, {response.OutTokens} out)";
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
        /// <param name="toolName">Name of the tool to execute</param>
        /// <param name="toolArgs">JSON string of tool arguments</param>
        /// <param name="provider">AI provider of the current dialog</param>
        /// <param name="model">AI model of the current dialog</param>
        private async Task ProcessToolCall(string toolName, string toolArgs, string provider, string model)
        {
            try
            {
                // Parse tool arguments
                JObject parameters = JObject.Parse(toolArgs);
                
                Debug.WriteLine($"[WebChatDialog] Processing tool call: {toolName}");
                _statusLabel.Text = $"Executing tool: {toolName}...";
                
                // Execute the tool
                var result = await AIToolManager.ExecuteTool(toolName, parameters, new JObject { ["provider"] = provider, ["model"] = model });
                
                // Add tool result to chat history
                string resultJson = JsonConvert.SerializeObject(result, Formatting.Indented);
                AddToolResultMessage(resultJson);
                
                // Add tool result to chat history for the AI to see
                _chatHistory.Add(new KeyValuePair<string, string>("tool_result", resultJson));
                
                // Get a new response from the AI with the tool result
                await GetAIResponseAndProcessToolCalls();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebChatDialog] Error processing tool call: {ex.Message}");
                AddSystemMessage($"Error executing tool '{toolName}': {ex.Message}");
            }
        }
        
        /// <summary>
        /// Adds a tool call message to the chat display
        /// </summary>
        /// <param name="toolName">Name of the tool being called</param>
        /// <param name="toolArgs">JSON string of tool arguments</param>
        private void AddToolCallMessage(string toolName, string toolArgs)
        {
            try
            {
                // Format arguments for display
                JObject parameters = JObject.Parse(toolArgs);
                string formattedArgs = JsonConvert.SerializeObject(parameters, Formatting.Indented);
                
                // Create a formatted message
                string message = $"🔧 **Tool Call**: `{toolName}`\n```json\n{formattedArgs}\n```";
                
                // Add as a system message
                AddSystemMessage(message);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebChatDialog] Error formatting tool call: {ex.Message}");
                AddSystemMessage($"Tool Call: {toolName} (Error formatting arguments: {ex.Message})");
            }
        }
        
        /// <summary>
        /// Adds a tool result message to the chat display
        /// </summary>
        /// <param name="resultJson">JSON result from the tool execution</param>
        private void AddToolResultMessage(string resultJson)
        {
            try
            {
                // Create a formatted message
                string message = $"⚙️ **Tool Result**:\n```json\n{resultJson}\n```";
                
                // Add as a system message
                AddSystemMessage(message);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebChatDialog] Error formatting tool result: {ex.Message}");
                AddSystemMessage($"Tool Result: (Error formatting result: {ex.Message})");
            }
        }

        /// <summary>
        /// Initializes a new conversation with a welcome message.
        /// </summary>
        private void InitializeNewConversation()
        {
            AddSystemMessage("I'm an AI assistant. How can I help you today?");
            _statusLabel.Text = "Ready";
        }
    }
}
