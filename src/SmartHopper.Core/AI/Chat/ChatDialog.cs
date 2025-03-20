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
 * Chat dialog interface using Avalonia UI.
 * This class provides a dialog-based chat interface for interacting with AI providers.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Markdown.Avalonia;
using SmartHopper.Config.Models;
using SmartHopper.Core.Utils;

namespace SmartHopper.Core.AI.Chat
{
    /// <summary>
    /// Dialog-based chat interface for interacting with AI providers.
    /// </summary>
    public class ChatDialog : Window
    {
        // UI Components
        private readonly StackPanel _chatHistoryPanel;
        private readonly ScrollViewer _chatScrollable;
        private readonly TextBox _userInputTextArea;
        private readonly Button _sendButton;
        private readonly Button _clearButton;
        private readonly ProgressBar _progressBar;
        private readonly TextBlock _statusLabel;

        // Chat state
        private readonly List<KeyValuePair<string, string>> _chatHistory;
        private bool _isProcessing;
        private readonly Func<List<KeyValuePair<string, string>>, Task<AIResponse>> _getResponse;

        // Colors for the chat bubbles
        private readonly IBrush _userBubbleColor = new SolidColorBrush(Color.Parse("#E1FFC7")); // Light green
        private readonly IBrush _botBubbleColor = new SolidColorBrush(Color.Parse("#FFFFFF"));  // White
        private readonly IBrush _systemBubbleColor = new SolidColorBrush(Color.Parse("#F0F0F0")); // Light gray
        private readonly IBrush _chatBackgroundColor = new SolidColorBrush(Color.Parse("#ECE5DD")); // Light beige

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
            MinWidth = 500;
            MinHeight = 600;
                        
            _getResponse = getResponse ?? throw new ArgumentNullException(nameof(getResponse));
            _chatHistory = new List<KeyValuePair<string, string>>();

            // Initialize UI components
            _chatHistoryPanel = new StackPanel
            {
                Spacing = 10,
                Margin = new Thickness(10),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Background = _chatBackgroundColor
            };

            _chatScrollable = new ScrollViewer
            {
                Content = _chatHistoryPanel,
                HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                Height = 400 // Initial height
            };

            _userInputTextArea = new TextBox
            {
                Height = 60,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap
            };
            _userInputTextArea.KeyDown += UserInputTextArea_KeyDown;

            _sendButton = new Button
            {
                Content = "Send",
                IsEnabled = true
            };
            _sendButton.Click += SendButton_Click;

            _clearButton = new Button
            {
                Content = "Clear Chat",
                IsEnabled = true
            };
            _clearButton.Click += ClearButton_Click;

            _progressBar = new ProgressBar
            {
                IsIndeterminate = true,
                IsVisible = false
            };

            _statusLabel = new TextBlock
            {
                Text = "Ready",
                TextAlignment = TextAlignment.Center
            };

            // Layout
            var mainPanel = new Grid();
            mainPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Star });
            mainPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            mainPanel.Children.Add(_chatScrollable);
            Grid.SetRow(_chatScrollable, 0);

            var inputPanel = new Grid();
            inputPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
            inputPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            inputPanel.Children.Add(_userInputTextArea);
            inputPanel.Children.Add(_sendButton);
            Grid.SetColumn(_userInputTextArea, 0);
            Grid.SetColumn(_sendButton, 1);

            mainPanel.Children.Add(inputPanel);
            Grid.SetRow(inputPanel, 1);

            mainPanel.Children.Add(_clearButton);
            Grid.SetRow(_clearButton, 2);

            mainPanel.Children.Add(_progressBar);
            Grid.SetRow(_progressBar, 3);

            mainPanel.Children.Add(_statusLabel);
            Grid.SetRow(_statusLabel, 4);

            Content = mainPanel;
            Padding = new Thickness(10);

            // Add system message to start the conversation
            AddSystemMessage("I'm an AI assistant. How can I help you today?");

            // Handle window resize to update message bubble widths
            this.PropertyChanged += ChatDialog_PropertyChanged;
        }

        private void ChatDialog_PropertyChanged(object sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == BoundsProperty)
            {
                // Recalculate message widths when dialog size changes
                UpdateMessageBubbleWidths();
            }
        }

        /// <summary>
        /// Updates the width of all message bubbles based on the current dialog width
        /// </summary>
        private void UpdateMessageBubbleWidths()
        {
            // Calculate new width based on dialog width
            int newWidth = Math.Max(MinMessageWidth, (int)(this.Bounds.Width * MaxMessageWidthPercentage));
            
            // Update width of all message bubbles
            foreach (var item in _chatHistoryPanel.Children)
            {
                if (item is StackPanel bubbleContainer)
                {
                    foreach (var messageBubble in bubbleContainer.Children)
                    {
                        if (messageBubble is Border border)
                        {
                            border.Width = newWidth;
                        }
                    }
                }
            }
        }

        private void UserInputTextArea_KeyDown(object sender, Avalonia.Input.KeyEventArgs e)
        {
            if (e.Key == Avalonia.Input.Key.Enter && e.KeyModifiers.HasFlag(Avalonia.Input.KeyModifiers.Shift))
            {
                // Shift+Enter adds a new line
                _userInputTextArea.Text += Environment.NewLine;
                e.Handled = true;
            }
            else if (e.Key == Avalonia.Input.Key.Enter && !_isProcessing && !e.KeyModifiers.HasFlag(Avalonia.Input.KeyModifiers.Shift))
            {
                // Enter sends the message
                SendMessage();
                e.Handled = true;
            }
        }

        private void SendButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (!_isProcessing)
            {
                SendMessage();
            }
        }

        private void ClearButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            _chatHistory.Clear();
            _chatHistoryPanel.Children.Clear();
            
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
            IBrush bubbleColor;
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

            // Calculate message width based on dialog width
            int messageWidth = Math.Max(MinMessageWidth, (int)(this.Bounds.Width * MaxMessageWidthPercentage));

            // Create the message container with word wrapping
            Control messageContent;
            
            // Use Markdown.Avalonia for rendering all non-user messages
            // User messages are kept as plain text for simplicity
            if (role != "user")
            {
                // Use Markdown.Avalonia for rendering markdown
                var markdownScrollViewer = new MarkdownScrollViewer
                {
                    Markdown = content
                };
                
                messageContent = markdownScrollViewer;
            }
            else
            {
                // Use regular TextBlock for plain text
                messageContent = new TextBlock
                {
                    Text = content,
                    TextWrapping = TextWrapping.Wrap,
                    TextAlignment = isUserMessage ? TextAlignment.Right : TextAlignment.Left
                };
            }

            // Create the message bubble with width constraint to ensure wrapping
            var messageBubble = new Border
            {
                Background = bubbleColor,
                Padding = new Thickness(10),
                Child = messageContent,
                Width = messageWidth,
                CornerRadius = new CornerRadius(10)
            };
            
            // Add context menu for copy operations
            var contextMenu = new ContextMenu();
            var copyMenuItem = new MenuItem { Header = "Copy Message" };
            copyMenuItem.Click += (sender, e) => 
            {
                TopLevel.GetTopLevel(messageBubble)?.Clipboard?.SetTextAsync(content);
                
                // Show a temporary tooltip or status message
                ShowTemporaryStatusMessage("Message copied to clipboard");
            };
            
            contextMenu.Items.Add(copyMenuItem);
            messageBubble.ContextMenu = contextMenu;

            // Create a container for the bubble with proper alignment
            var bubbleContainer = new StackPanel
            {
                Spacing = 5,
                HorizontalAlignment = isUserMessage ? HorizontalAlignment.Right : HorizontalAlignment.Left
            };
            
            if (role != "user" && role != "assistant")
            {
                bubbleContainer.Children.Add(new TextBlock
                {
                    Text = displayRole,
                    Foreground = new SolidColorBrush(Colors.Gray),
                    FontSize = 8
                });
            }
            
            bubbleContainer.Children.Add(messageBubble);

            // Add the bubble to the chat history
            _chatHistoryPanel.Children.Add(bubbleContainer);

            // Scroll to the bottom
            Dispatcher.UIThread.Post(() =>
            {
                if (_chatScrollable.Extent.Height > _chatScrollable.Viewport.Height)
                {
                    _chatScrollable.Offset = new Vector(_chatScrollable.Offset.X, _chatScrollable.Extent.Height - _chatScrollable.Viewport.Height);
                }
            });
        }

        private void ShowTemporaryStatusMessage(string message, int seconds = 2)
        {
            _statusLabel.Text = message;
            
            // Reset status after specified seconds using a DispatcherTimer
            var statusResetTimer = new System.Threading.Timer(_ =>
            {
                Dispatcher.UIThread.Post(() =>
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
            _sendButton.IsEnabled = false;
            _progressBar.IsVisible = true;
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
                _sendButton.IsEnabled = true;
                _progressBar.IsVisible = false;
            }
        }
    }
}
