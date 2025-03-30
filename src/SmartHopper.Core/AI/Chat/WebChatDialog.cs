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
            Title = "SmartHopper AI Web Chat";
            MinimumSize = new Size(600, 700);
            Size = new Size(700, 800);
                        
            _getResponse = getResponse ?? throw new ArgumentNullException(nameof(getResponse));
            _chatHistory = new List<KeyValuePair<string, string>>();
            _htmlRenderer = new HtmlChatRenderer();

            // Initialize WebView
            _webView = new WebView
            {
                Height = 500
            };
            
            // Initialize the WebView with base HTML
            _webView.LoadHtml(_htmlRenderer.GetInitialHtml());

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
                Text = "Ready",
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

            // Add system message to start the conversation
            AddSystemMessage("I'm an AI assistant. How can I help you today?");
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
            _webView.LoadHtml(_htmlRenderer.GetInitialHtml());
            
            // Add system message to start the conversation
            AddSystemMessage("I'm an AI assistant. How can I help you today?");
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
            // Generate HTML for the message
            string messageHtml = _htmlRenderer.GenerateMessageHtml(role, content);
            
            // Execute JavaScript to add the message to the WebView
            _webView.ExecuteScript($"addMessage(`{messageHtml.Replace("`", "\\`")}`)");
            
            // Scroll to bottom
            _webView.ExecuteScript("scrollToBottom()");
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
            string userMessage = _userInputTextArea.Text.Trim();
            if (string.IsNullOrEmpty(userMessage))
            {
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
                
                // Get response from AI provider using the provided function
                var response = await _getResponse(messages);

                if (response != null)
                {
                    // Add response to chat history
                    AddAssistantMessage(response.Response);
                    
                    // Notify listeners
                    ResponseReceived?.Invoke(this, response);
                    
                    _statusLabel.Text = $"Response received ({response.InTokens} in, {response.OutTokens} out)";
                }
                else
                {
                    AddSystemMessage("Error: Failed to get response from AI provider.");
                    _statusLabel.Text = "Error: No response received";
                }
            }
            catch (Exception ex)
            {
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
