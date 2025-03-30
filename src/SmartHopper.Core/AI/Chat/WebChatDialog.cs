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
using System.Text;
using Eto.Forms;
using Eto.Drawing;
using SmartHopper.Config.Models;

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
            
            Title = "SmartHopper AI Web Chat";
            MinimumSize = new Size(600, 700);
            Size = new Size(700, 800);
                        
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
            
            // Initialize WebView after the dialog is shown
            this.Shown += (sender, e) => 
            {
                Debug.WriteLine("[WebChatDialog] Dialog shown, initializing WebView");
                InitializeWebViewAsync();
            };
            
            // Add system message once WebView is initialized
            Task.Run(async () => 
            {
                try
                {
                    Debug.WriteLine("[WebChatDialog] Waiting for WebView initialization");
                    await _webViewInitializedTcs.Task;
                    Debug.WriteLine("[WebChatDialog] WebView initialization completed, adding system message");
                    
                    Application.Instance.AsyncInvoke(() => 
                    {
                        AddSystemMessage("I'm an AI assistant. How can I help you today?");
                        _statusLabel.Text = "Ready";
                    });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[WebChatDialog] Error waiting for WebView initialization: {ex.Message}");
                }
            });
        }
        
        /// <summary>
        /// Initializes the WebView control asynchronously.
        /// </summary>
        private async void InitializeWebViewAsync()
        {
            try
            {
                Debug.WriteLine("[WebChatDialog] Starting WebView initialization");
                
                // Get the HTML content first
                Debug.WriteLine("[WebChatDialog] Getting HTML from HtmlChatRenderer");
                string html = _htmlRenderer.GetInitialHtml();
                Debug.WriteLine($"[WebChatDialog] HTML length: {html?.Length ?? 0}");
                
                // For Windows, we need to ensure CoreWebView2 is initialized
                if (Eto.Platform.Detect.IsWpf)
                {
                    Debug.WriteLine("[WebChatDialog] Initializing WebView for WPF");
                    
                    // First load a simple HTML to initialize the WebView
                    string initHtml = "<html><body><h1>Initializing...</h1></body></html>";
                    _webView.LoadHtml(initHtml);
                    
                    // Wait for the WebView to load
                    var loadCompleteTcs = new TaskCompletionSource<bool>();
                    EventHandler<WebViewLoadedEventArgs> loadHandler = null;
                    
                    loadHandler = (sender, e) => 
                    {
                        Debug.WriteLine("[WebChatDialog] Initial HTML loaded");
                        _webView.DocumentLoaded -= loadHandler;
                        loadCompleteTcs.TrySetResult(true);
                    };
                    
                    _webView.DocumentLoaded += loadHandler;
                    
                    // Set a timeout for initialization
                    var timeoutTask = Task.Delay(5000);
                    var completedTask = await Task.WhenAny(loadCompleteTcs.Task, timeoutTask);
                    
                    if (completedTask == timeoutTask)
                    {
                        Debug.WriteLine("[WebChatDialog] WebView initialization timed out");
                    }
                    
                    // Small delay to ensure WebView is ready
                    await Task.Delay(500);
                }
                
                // Now load the actual chat HTML
                Debug.WriteLine("[WebChatDialog] Loading HTML into WebView");
                
                // Try a different approach for loading HTML
                if (Eto.Platform.Detect.IsWpf)
                {
                    // For WPF, use a data URI to load the HTML
                    Debug.WriteLine("[WebChatDialog] Using data URI approach for WPF");
                    string base64Html = Convert.ToBase64String(Encoding.UTF8.GetBytes(html));
                    string dataUri = $"data:text/html;base64,{base64Html}";
                    _webView.Url = new Uri(dataUri);
                }
                else
                {
                    // For other platforms, use LoadHtml
                    _webView.LoadHtml(html);
                }
                
                _webViewInitialized = true;
                _webViewInitializedTcs.TrySetResult(true);
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
                _statusLabel.Text = $"Error initializing WebView: {ex.Message}";
                _webViewInitializedTcs.TrySetException(ex);
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
                
                // Get the HTML content
                string html = _htmlRenderer.GetInitialHtml();
                
                // Use the same approach as in initialization
                if (Eto.Platform.Detect.IsWpf)
                {
                    // For WPF, use a data URI to load the HTML
                    string base64Html = Convert.ToBase64String(Encoding.UTF8.GetBytes(html));
                    string dataUri = $"data:text/html;base64,{base64Html}";
                    _webView.Url = new Uri(dataUri);
                }
                else
                {
                    // For other platforms, use LoadHtml
                    _webView.LoadHtml(html);
                }
                
                // Add system message to start the conversation
                AddSystemMessage("I'm an AI assistant. How can I help you today?");
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
                    .Replace("`", "\\`")
                    .Replace("$", "\\$");
                
                _webView.ExecuteScript($"addMessage(`{escapedHtml}`)");
                
                // Scroll to bottom
                _webView.ExecuteScript("scrollToBottom()");
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
                // Create a copy of the chat history for the API call
                var messages = _chatHistory.ToList();
                
                Debug.WriteLine("[WebChatDialog] Getting response from AI provider");
                // Get response from AI provider using the provided function
                var response = await _getResponse(messages);

                if (response != null)
                {
                    Debug.WriteLine("[WebChatDialog] Response received, adding to chat");
                    // Add response to chat history
                    AddAssistantMessage(response.Response);
                    
                    // Notify listeners
                    ResponseReceived?.Invoke(this, response);
                    
                    _statusLabel.Text = $"Response received ({response.InTokens} in, {response.OutTokens} out)";
                }
                else
                {
                    Debug.WriteLine("[WebChatDialog] No response received from AI provider");
                    AddSystemMessage("Error: Failed to get response from AI provider.");
                    _statusLabel.Text = "Error: No response received";
                }
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
    }
}
