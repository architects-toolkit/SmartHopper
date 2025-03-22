/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 * 
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 * 
 * Portions of this code were inspired by:
 * https://github.com/agreentejada/winforms-chat
 * MIT License
 * Copyright (C) 2020 agreentejada 
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Eto.Forms;
using Eto.Drawing;
using SmartHopper.Config.Models;
using SmartHopper.Core.Controls;
using SmartHopper.Core.Converters;

namespace SmartHopper.Core.AI.Chat
{
    /// <summary>
    /// Dialog-based chat interface for interacting with AI providers.
    /// </summary>
    public class ChatDialog : Form
    {
        // UI Components
        private readonly StackLayout _chatHistoryPanel;
        private readonly Scrollable _chatScrollable;
        private readonly TextArea _userInputTextArea;
        private readonly Button _sendButton;
        private readonly Button _clearButton;
        private readonly ProgressBar _progressBar;
        private readonly Label _statusLabel;

        // Chat state
        private readonly List<KeyValuePair<string, string>> _chatHistory;
        private bool _isProcessing;
        private readonly Func<List<KeyValuePair<string, string>>, Task<AIResponse>> _getResponse;

        // Colors for the chat bubbles
        private readonly Color _userBubbleColor = Color.FromArgb(225, 255, 199); // Light green
        private readonly Color _botBubbleColor = Colors.White;  // White
        private readonly Color _systemBubbleColor = Color.FromArgb(240, 240, 240); // Light gray
        private readonly Color _chatBackgroundColor = Color.FromArgb(236, 229, 221); // Light beige

        // Message bubble sizing
        private const int MinMessageWidth = 350;
        private const double MaxMessageWidthPercentage = 0.8; // 80% of dialog width

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
            Size = new Size(600, 700);
                        
            _getResponse = getResponse ?? throw new ArgumentNullException(nameof(getResponse));
            _chatHistory = new List<KeyValuePair<string, string>>();

            // Initialize UI components
            _chatHistoryPanel = new StackLayout
            {
                Spacing = 10,
                Padding = new Padding(10),
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
            };

            _chatScrollable = new Scrollable
            {
                Content = _chatHistoryPanel,
                ExpandContentWidth = true,
                Height = 400 // Initial height
            };
            
            // Set the background color of the chat area
            _chatScrollable.BackgroundColor = _chatBackgroundColor;

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
            
            // Chat history area
            mainLayout.Add(_chatScrollable, yscale: true);
            
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
            
            // Handle window resize to update message bubble widths
            this.SizeChanged += ChatDialog_SizeChanged;
        }

        private void ChatDialog_SizeChanged(object sender, EventArgs e)
        {
            // Recalculate message widths when dialog size changes
            UpdateMessageBubbleWidths();
        }

        /// <summary>
        /// Updates the width of all message bubbles based on the current dialog width
        /// </summary>
        private void UpdateMessageBubbleWidths()
        {
            // Calculate new width based on dialog width
            int newWidth = Math.Max(MinMessageWidth, (int)(this.Width * MaxMessageWidthPercentage));
            
            // Update width of all message bubbles
            foreach (var item in _chatHistoryPanel.Items)
            {
                // In Eto.Forms, Items collection contains StackLayoutItem objects
                if (item is StackLayoutItem layoutItem && layoutItem.Control is StackLayout bubbleContainer)
                {
                    foreach (var childItem in bubbleContainer.Items)
                    {
                        if (childItem is StackLayoutItem childLayoutItem && 
                            childLayoutItem.Control is Panel messageBubble)
                        {
                            messageBubble.Width = newWidth;
                        }
                    }
                }
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
            _chatHistoryPanel.Items.Clear();
            
            // Add system message to start the conversation
            AddSystemMessage("I'm an AI assistant. How can I help you today?");
        }

        private void AddSystemMessage(string message)
        {
            _chatHistory.Add(new KeyValuePair<string, string>("system", message));
            AddMessageBubble("system", message);
        }

        private void AddUserMessage(string message)
        {
            _chatHistory.Add(new KeyValuePair<string, string>("user", message));
            AddMessageBubble("user", message);
        }

        private void AddAssistantMessage(string message)
        {
            _chatHistory.Add(new KeyValuePair<string, string>("assistant", message));
            AddMessageBubble("assistant", message);
        }

        private void AddMessageBubble(string role, string content)
        {
            Color bubbleColor;
            bool isUserMessage = false;
            
            switch (role)
            {
                case "user":
                    bubbleColor = Colors.LightGreen;
                    isUserMessage = true;
                    break;
                case "assistant":
                    bubbleColor = Colors.LightSkyBlue;
                    break;
                case "system":
                    bubbleColor = Colors.LightGray;
                    break;
                default:
                    bubbleColor = Colors.White;
                    break;
            }
            
            // Create a bubble container
            var bubble = new StackLayout
            {
                Orientation = Orientation.Horizontal,
                Spacing = 5,
                Padding = new Padding(5),
                HorizontalContentAlignment = isUserMessage ? HorizontalAlignment.Left : HorizontalAlignment.Right
            };
            
            // Use our custom FormattedMessageControl for all messages
            var formattedMessage = new FormattedMessageControl
            {
                Text = content,
                ControlFont = SystemFonts.Default(9),
                TextColor = Colors.Black,
                ControlBackgroundColor = bubbleColor,
                Padding = 10
            };
            
            // Add the message to the bubble
            if (isUserMessage)
            {
                bubble.Items.Add(new StackLayoutItem(new Label { Text = "You", TextColor = Colors.Gray, Font = SystemFonts.Bold(8) }));
                bubble.Items.Add(new StackLayoutItem(formattedMessage, true));
            }
            else
            {
                bubble.Items.Add(new StackLayoutItem(formattedMessage, true));
                bubble.Items.Add(new StackLayoutItem(new Label { Text = "AI", TextColor = Colors.Gray, Font = SystemFonts.Bold(8) }));
            }
            
            // Add the bubble to the chat container
            _chatHistoryPanel.Items.Add(bubble);
            
            // Scroll to the bottom to show the new message
            Application.Instance.AsyncInvoke(() =>
            {
                _chatScrollable.ScrollPosition = new Point(
                    _chatScrollable.ScrollPosition.X,
                    _chatScrollable.ScrollSize.Height);
            });
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
