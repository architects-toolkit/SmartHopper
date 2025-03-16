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
        private readonly Color _userBubbleColor = Color.Parse("#E1FFC7"); // Light green
        private readonly Color _botBubbleColor = Color.Parse("#FFFFFF");  // White
        private readonly Color _systemBubbleColor = Color.Parse("#F0F0F0"); // Light gray
        private readonly Color _chatBackgroundColor = Color.Parse("#ECE5DD"); // Light beige

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
            _chatHistoryPanel = new StackLayout
            {
                Spacing = 10,
                Padding = new Padding(10),
                HorizontalContentAlignment = HorizontalAlignment.Stretch
            };

            _chatScrollable = new Scrollable
            {
                Content = _chatHistoryPanel,
                ExpandContentWidth = true,
                Height = 400,
                BackgroundColor = _chatBackgroundColor
            };

            _userInputTextArea = new TextArea
            {
                Height = 60
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
            Content = new TableLayout
            {
                Padding = new Padding(10),
                Spacing = new Size(5, 5),
                Rows =
                {
                    new TableRow
                    {
                        Cells = { new TableCell(_chatScrollable, true) }
                    },
                    new TableRow
                    {
                        Cells = 
                        { 
                            new TableLayout
                            {
                                Spacing = new Size(5, 0),
                                Rows = 
                                { 
                                    new TableRow 
                                    { 
                                        Cells = 
                                        { 
                                            new TableCell(_userInputTextArea, true),
                                            _sendButton
                                        } 
                                    } 
                                }
                            }
                        }
                    },
                    new TableRow
                    {
                        Cells = { _clearButton }
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

        private void UserInputTextArea_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Keys.Enter && e.Modifiers.HasFlag(Keys.Shift))
            {
                // Shift+Enter adds a new line
                _userInputTextArea.Text += Environment.NewLine;
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
            string displayRole;

            // Set bubble color and alignment based on role
            switch (role)
            {
                case "user":
                    bubbleColor = _userBubbleColor;
                    isUserMessage = true;
                    displayRole = "You";
                    break;
                case "assistant":
                    bubbleColor = _botBubbleColor;
                    displayRole = "AI";
                    break;
                case "system":
                    bubbleColor = _systemBubbleColor;
                    displayRole = "System";
                    break;
                default:
                    bubbleColor = _botBubbleColor;
                    displayRole = role;
                    break;
            }

            // Create the message label with word wrapping
            var messageLabel = new Label
            {
                Text = content,
                Wrap = WrapMode.Word
            };

            // Create the message bubble
            var messageBubble = new Panel
            {
                BackgroundColor = bubbleColor,
                Padding = new Padding(10),
                Content = messageLabel
            };

            // Add rounded corners to the bubble
            messageBubble.Style = "border-radius: 10px;";
            
            // Create a container for the bubble with proper alignment
            var bubbleContainer = new StackLayout
            {
                Spacing = 5,
                HorizontalContentAlignment = isUserMessage ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                Items = 
                {
                    new StackLayoutItem
                    {
                        Control = messageBubble,
                        HorizontalAlignment = isUserMessage ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                        Expand = false
                    }
                }
            };

            // Add a small label for the role name
            if (role != "user" && role != "assistant")
            {
                bubbleContainer.Items.Insert(0, new Label
                {
                    Text = displayRole,
                    TextColor = Colors.Gray,
                    Font = new Font(SystemFont.Default, 8)
                });
            }

            // Add the bubble to the chat history
            _chatHistoryPanel.Items.Add(bubbleContainer);

            // Scroll to the bottom
            Application.Instance.AsyncInvoke(() =>
            {
                _chatScrollable.ScrollPosition = new Point(0, int.MaxValue);
            });
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
