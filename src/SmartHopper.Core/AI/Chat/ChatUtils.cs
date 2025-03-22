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

/*
 * Utility functions for the AI Chat component.
 * This class provides helper methods for managing chat sessions and formatting.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Eto.Forms;
using Grasshopper.Kernel.Types;
using Rhino;
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
        public static async Task<AIResponse> ShowChatDialog(string providerName, string modelName, string endpoint = null)
        {
            var tcs = new TaskCompletionSource<AIResponse>();
            AIResponse lastResponse = null;
            
            Debug.WriteLine("[ChatUtils] Preparing to show dialog");

            try
            {
                // Create a function to get responses from the AI provider
                Func<List<KeyValuePair<string, string>>, Task<AIResponse>> getResponse = 
                    messages => AIUtils.GetResponse(providerName, modelName, messages, endpoint: endpoint);

                // We need to use Rhino's UI thread to show the dialog
                // This is important because Eto.Forms requires UI operations on the UI thread
                Rhino.RhinoApp.InvokeOnUiThread((Action)(() =>
                {
                    try
                    {
                        // Initialize Eto.Forms application if needed
                        if (Application.Instance == null)
                        {
                            Debug.WriteLine("[ChatUtils] Initializing Eto.Forms application");
                            var platform = Eto.Platform.Detect;
                            new Application(platform).Attach();
                        }

                        Debug.WriteLine("[ChatUtils] Creating chat dialog");
                        var dialog = new ChatDialog(getResponse);
                        
                        // Handle dialog closing
                        dialog.Closed += (sender, e) => 
                        {
                            Debug.WriteLine("[ChatUtils] Dialog closed");
                            // Complete the task with the last response
                            tcs.TrySetResult(lastResponse);
                        };
                        
                        // Handle responses
                        dialog.ResponseReceived += (sender, response) => 
                        {
                            Debug.WriteLine("[ChatUtils] Response received");
                            lastResponse = response;
                        };
                        
                        // Configure the dialog window
                        dialog.Title = $"SmartHopper AI Chat - {modelName} ({providerName})";
                        
                        // Show the dialog
                        Debug.WriteLine("[ChatUtils] Showing dialog");
                        dialog.Show();
                        
                        // Ensure the dialog is visible and active
                        dialog.BringToFront();
                        dialog.Focus();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[ChatUtils] Error in UI thread: {ex.Message}");
                        tcs.TrySetException(ex);
                    }
                }));
                
                // Wait for the dialog to close
                return await tcs.Task;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ChatUtils] Error showing chat dialog: {ex.Message}");
                throw;
            }
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
        /// Converts a chat message to a Grasshopper string.
        /// </summary>
        /// <param name="message">The message to convert</param>
        /// <returns>A GH_String containing the message</returns>
        public static GH_String ToGrasshopperString(string message)
        {
            return new GH_String(message);
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
