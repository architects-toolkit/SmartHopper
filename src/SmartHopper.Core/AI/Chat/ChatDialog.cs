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
 * Chat dialog interface using Eto.Forms.
 * This class provides a dialog-based chat interface for interacting with AI providers.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Eto.Forms;
using Eto.Drawing;
using SmartHopper.Config.Models;
using SmartHopper.Core.Utils;

namespace SmartHopper.Core.AI.Chat
{
    /// <summary>
    /// Dialog-based chat interface for interacting with AI providers.
    /// </summary>
    public class ChatDialog : Form
    {
        // UI Components
        private readonly TextArea _chatHistoryTextArea;
        private readonly TextBox _userInputTextBox;
        private readonly Button _sendButton;
        private readonly Button _clearButton;
        private readonly ProgressBar _progressBar;
        private readonly Label _statusLabel;

        // Chat state
        private readonly List<KeyValuePair<string, string>> _chatHistory;
        private bool _isProcessing;
        private readonly Func<List<KeyValuePair<string, string>>, Task<AIResponse>> _getResponse;

        /// <summary>
        /// Event raised when a new AI response is received.
        /// </summary>
        public event EventHandler<AIResponse> ResponseReceived;

        /// <summary>
        /// Creates a new chat dialog.
        /// </summary>
        /// <param name="getResponse">Function to get responses from the AI provider</param>
        public ChatDialog(Func<List<KeyValuePair<string, string>>, Task<AIResponse>> getResponse)
        {
            Title = "SmartHopper AI Chat";
            MinimumSize = new Size(500, 600);
            
            _getResponse = getResponse ?? throw new ArgumentNullException(nameof(getResponse));
            _chatHistory = new List<KeyValuePair<string, string>>();

            // Initialize UI components
            _chatHistoryTextArea = new TextArea
            {
                ReadOnly = true,
                Wrap = true,
                Height = 400
            };

            _userInputTextBox = new TextBox
            {
                PlaceholderText = "Type your message here...",
                Height = 60
            };
            _userInputTextBox.KeyDown += UserInputTextBox_KeyDown;

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
            Content = new TableLayout
            {
                Padding = new Padding(10),
                Spacing = new Size(5, 5),
                Rows =
                {
                    new TableRow
                    {
                        Cells = { new TableCell(_chatHistoryTextArea, true) }
                    },
                    new TableRow
                    {
                        Cells = { _userInputTextBox }
                    },
                    new TableRow
                    {
                        Cells =
                        {
                            new TableLayout
                            {
                                Spacing = new Size(5, 0),
                                Rows = { new TableRow { Cells = { _sendButton, _clearButton } } }
                            }
                        }
                    },
                    new TableRow
                    {
                        Cells = { _progressBar }
                    },
                    new TableRow
                    {
                        Cells = { _statusLabel }
                    }
                }
            };

            // Add system message to start the conversation
            AddSystemMessage("I'm an AI assistant. How can I help you today?");
        }

        private void UserInputTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Keys.Enter && e.Modifiers.HasFlag(Keys.Shift))
            {
                // Shift+Enter adds a new line
                _userInputTextBox.Text += Environment.NewLine;
                e.Handled = true;
            }
            else if (e.Key == Keys.Enter && !_isProcessing)
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
            _chatHistoryTextArea.Text = string.Empty;
            
            // Add system message to start the conversation
            AddSystemMessage("I'm an AI assistant. How can I help you today?");
        }

        private void AddSystemMessage(string message)
        {
            _chatHistory.Add(new KeyValuePair<string, string>("system", message));
            UpdateChatDisplay();
        }

        private void AddUserMessage(string message)
        {
            _chatHistory.Add(new KeyValuePair<string, string>("user", message));
            UpdateChatDisplay();
        }

        private void AddAssistantMessage(string message)
        {
            _chatHistory.Add(new KeyValuePair<string, string>("assistant", message));
            UpdateChatDisplay();
        }

        private void UpdateChatDisplay()
        {
            _chatHistoryTextArea.Text = string.Empty;
            
            foreach (var message in _chatHistory)
            {
                string role = message.Key;
                string content = message.Value;
                
                string displayRole;
                
                // Traditional switch statement instead of switch expression for C# 7.3 compatibility
                switch (role)
                {
                    case "user":
                        displayRole = "You";
                        break;
                    case "assistant":
                        displayRole = "AI";
                        break;
                    case "system":
                        displayRole = "System";
                        break;
                    default:
                        displayRole = role;
                        break;
                }
                
                _chatHistoryTextArea.Text += $"{displayRole}: {content}{Environment.NewLine}{Environment.NewLine}";
            }
            
            // Scroll to the bottom
            _chatHistoryTextArea.CaretIndex = _chatHistoryTextArea.Text.Length;
        }

        private async void SendMessage()
        {
            string userMessage = _userInputTextBox.Text.Trim();
            if (string.IsNullOrEmpty(userMessage))
            {
                return;
            }

            // Clear input and add message to history
            _userInputTextBox.Text = string.Empty;
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
