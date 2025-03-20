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
 * Utility functions for the AI Chat component.
 * This class provides helper methods for managing chat sessions and formatting.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using Grasshopper.Kernel.Types;
using SmartHopper.Config.Models;
using SmartHopper.Core.Utils;

namespace SmartHopper.Core.AI.Chat
{
    /// <summary>
    /// Utility functions for the AI Chat component.
    /// </summary>
    public static class ChatUtils
    {
        /// <summary>
        /// Shows a chat dialog for the specified AI provider and model.
        /// </summary>
        /// <param name="providerName">The name of the AI provider to use</param>
        /// <param name="modelName">The model to use for AI processing</param>
        /// <param name="endpoint">Optional custom endpoint for the AI provider</param>
        /// <returns>The last AI response received, or null if the dialog was closed without a response</returns>
        public static async Task<AIResponse> ShowChatDialog(string providerName, string modelName, string endpoint = "")
        {
            // Create a TaskCompletionSource to get the result from the dialog
            var tcs = new TaskCompletionSource<AIResponse>();
            AIResponse lastResponse = null;

            // Ensure Avalonia is initialized before creating the dialog
            ChatDialog.EnsureAvaloniaInitialized();

            // Create and show the dialog on the UI thread
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                // Create a function to get responses from the AI provider
                Func<List<KeyValuePair<string, string>>, Task<AIResponse>> getResponse = 
                    messages => AIUtils.GetResponse(providerName, modelName, messages, endpoint: endpoint);

                var dialog = new ChatDialog(getResponse);
                
                // Handle dialog closing
                dialog.Closed += (sender, e) => 
                {
                    // Complete the task with the last response
                    tcs.TrySetResult(lastResponse);
                };
                
                // Handle responses
                dialog.ResponseReceived += (sender, response) => 
                {
                    lastResponse = response;
                };
                
                // Show the dialog non-modally to prevent freezing the canvas
                dialog.Show();
            });

            // Wait for the dialog to close and return the result
            return await tcs.Task;
        }

        /// <summary>
        /// Processes a chat message using the specified AI provider.
        /// </summary>
        /// <param name="messages">The chat messages</param>
        /// <param name="providerName">The name of the AI provider to use</param>
        /// <param name="modelName">The model to use for AI processing</param>
        /// <param name="endpoint">Optional custom endpoint for the AI provider</param>
        /// <returns>The AI response</returns>
        public static async Task<AIResponse> ProcessChatMessageAsync(
            List<KeyValuePair<string, string>> messages,
            string providerName,
            string modelName,
            string endpoint = "")
        {
            try
            {
                // Get response from AI provider
                return await AIUtils.GetResponse(providerName, modelName, messages, endpoint: endpoint);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ChatUtils] Error in ProcessChatMessageAsync: {ex.Message}");
                return new AIResponse
                {
                    Response = $"Error: {ex.Message}",
                    FinishReason = "error"
                };
            }
        }

        /// <summary>
        /// Formats a chat message for display.
        /// </summary>
        /// <param name="role">The role of the message sender (user, assistant, system)</param>
        /// <param name="content">The message content</param>
        /// <returns>A formatted message string</returns>
        public static string FormatChatMessage(string role, string content)
        {
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
            
            return $"{displayRole}: {content}";
        }

        /// <summary>
        /// Creates a new chat worker for processing chat interactions.
        /// </summary>
        /// <param name="providerName">The name of the AI provider to use</param>
        /// <param name="modelName">The model to use for AI processing</param>
        /// <param name="endpoint">Optional custom endpoint for the AI provider</param>
        /// <param name="progressReporter">Action to report progress</param>
        /// <returns>A new chat worker</returns>
        public static ChatWorker CreateChatWorker(
            string providerName, 
            string modelName, 
            string endpoint,
            Action<string> progressReporter)
        {
            return new ChatWorker(providerName, modelName, endpoint, progressReporter);
        }
    }

    /// <summary>
    /// Worker class for processing chat interactions asynchronously.
    /// </summary>
    public class ChatWorker
    {
        private readonly string _providerName;
        private readonly string _modelName;
        private readonly string _endpoint;
        private readonly Action<string> _progressReporter;
        private AIResponse _lastResponse;

        /// <summary>
        /// Creates a new chat worker.
        /// </summary>
        /// <param name="providerName">The name of the AI provider to use</param>
        /// <param name="modelName">The model to use for AI processing</param>
        /// <param name="endpoint">Optional custom endpoint for the AI provider</param>
        /// <param name="progressReporter">Action to report progress</param>
        public ChatWorker(
            string providerName, 
            string modelName, 
            string endpoint,
            Action<string> progressReporter)
        {
            _providerName = providerName;
            _modelName = modelName;
            _endpoint = endpoint;
            _progressReporter = progressReporter;
        }

        /// <summary>
        /// Shows the chat dialog and processes the interaction.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token to cancel the operation</param>
        /// <returns>The task representing the asynchronous operation</returns>
        public async Task ProcessChatAsync(CancellationToken cancellationToken)
        {
            _progressReporter?.Invoke("Opening chat dialog...");
            
            try
            {
                _lastResponse = await ChatUtils.ShowChatDialog(_providerName, _modelName, _endpoint);
                
                if (_lastResponse != null)
                {
                    _progressReporter?.Invoke($"Chat completed. Used {_lastResponse.InTokens} input tokens, {_lastResponse.OutTokens} output tokens.");
                }
                else
                {
                    _progressReporter?.Invoke("Chat dialog closed without a response.");
                }
            }
            catch (Exception ex)
            {
                _progressReporter?.Invoke($"Error in chat processing: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Gets the last AI response received from the chat dialog.
        /// </summary>
        /// <returns>The last AI response, or null if no response was received</returns>
        public AIResponse GetLastResponse()
        {
            return _lastResponse;
        }
    }
}
