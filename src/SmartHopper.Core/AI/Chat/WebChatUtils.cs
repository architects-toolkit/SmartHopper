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
 * Utility functions for the AI Web Chat component.
 * This class provides helper methods for managing web-based chat sessions.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Eto.Forms;
using SmartHopper.Config.Models;
using SmartHopper.Core.Utils;

namespace SmartHopper.Core.AI.Chat
{
    /// <summary>
    /// Utility functions for the AI Web Chat component.
    /// </summary>
    public static class WebChatUtils
    {
        /// <summary>
        /// Shows a web-based chat dialog for the specified AI provider and model.
        /// </summary>
        /// <param name="providerName">The name of the AI provider to use</param>
        /// <param name="modelName">The model to use for AI processing</param>
        /// <param name="endpoint">Optional custom endpoint for the AI provider</param>
        /// <returns>The last AI response received, or null if the dialog was closed without a response</returns>
        public static async Task<AIResponse> ShowWebChatDialog(string providerName, string modelName, string endpoint = null)
        {
            var tcs = new TaskCompletionSource<AIResponse>();
            AIResponse lastResponse = null;
            
            Debug.WriteLine("[WebChatUtils] Preparing to show web chat dialog");

            try
            {
                // Create a function to get responses from the AI provider
                Func<List<KeyValuePair<string, string>>, Task<AIResponse>> getResponse = 
                    messages => AIUtils.GetResponse(providerName, modelName, messages, endpoint: endpoint);

                // We need to use Rhino's UI thread to show the dialog
                Rhino.RhinoApp.InvokeOnUiThread((Action)(() =>
                {
                    try
                    {
                        // Initialize Eto.Forms application if needed
                        if (Application.Instance == null)
                        {
                            Debug.WriteLine("[WebChatUtils] Initializing Eto.Forms application");
                            var platform = Eto.Platform.Detect;
                            new Application(platform).Attach();
                        }

                        Debug.WriteLine("[WebChatUtils] Creating web chat dialog");
                        var dialog = new WebChatDialog(getResponse);
                        
                        // Handle dialog closing
                        dialog.Closed += (sender, e) => 
                        {
                            Debug.WriteLine("[WebChatUtils] Dialog closed");
                            // Complete the task with the last response
                            tcs.TrySetResult(lastResponse);
                        };
                        
                        // Handle responses
                        dialog.ResponseReceived += (sender, response) => 
                        {
                            Debug.WriteLine("[WebChatUtils] Response received");
                            lastResponse = response;
                        };
                        
                        // Configure the dialog window
                        dialog.Title = $"SmartHopper AI Web Chat - {modelName} ({providerName})";
                        
                        // Show the dialog
                        Debug.WriteLine("[WebChatUtils] Showing dialog");
                        dialog.Show();
                        
                        // Ensure the dialog is visible and active
                        dialog.BringToFront();
                        dialog.Focus();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[WebChatUtils] Error in UI thread: {ex.Message}");
                        tcs.TrySetException(ex);
                    }
                }));
                
                // Wait for the dialog to close
                return await tcs.Task;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebChatUtils] Error showing web chat dialog: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Worker class for processing web chat interactions asynchronously.
        /// </summary>
        public class WebChatWorker
        {
            private readonly Action<string> _progressReporter;
            private readonly string _providerName;
            private readonly string _modelName;
            private readonly string _endpoint;
            private AIResponse _lastResponse;

            /// <summary>
            /// Initializes a new instance of the WebChatWorker class.
            /// </summary>
            /// <param name="providerName">The name of the AI provider to use</param>
            /// <param name="modelName">The model to use for AI processing</param>
            /// <param name="endpoint">Optional custom endpoint for the AI provider</param>
            /// <param name="progressReporter">Action to report progress</param>
            public WebChatWorker(
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
            /// Shows the web chat dialog and processes the interaction.
            /// </summary>
            /// <param name="cancellationToken">Cancellation token to cancel the operation</param>
            /// <returns>The task representing the asynchronous operation</returns>
            public async Task ProcessChatAsync(CancellationToken cancellationToken)
            {
                _progressReporter?.Invoke("Opening web chat dialog...");
                
                try
                {
                    _lastResponse = await ShowWebChatDialog(_providerName, _modelName, _endpoint);
                    _progressReporter?.Invoke("Web chat dialog closed");
                }
                catch (Exception ex)
                {
                    _progressReporter?.Invoke($"Error: {ex.Message}");
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

        /// <summary>
        /// Creates a new web chat worker for processing chat interactions.
        /// </summary>
        /// <param name="providerName">The name of the AI provider to use</param>
        /// <param name="modelName">The model to use for AI processing</param>
        /// <param name="endpoint">Optional custom endpoint for the AI provider</param>
        /// <param name="progressReporter">Action to report progress</param>
        /// <returns>A new web chat worker</returns>
        public static WebChatWorker CreateWebChatWorker(
            string providerName, 
            string modelName, 
            string endpoint,
            Action<string> progressReporter)
        {
            return new WebChatWorker(providerName, modelName, endpoint, progressReporter);
        }
    }
}
