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
 * This class provides helper methods for managing web-based chat sessions.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Eto.Forms;
using Grasshopper;
using Grasshopper.Kernel;
using SmartHopper.Config.Models;

namespace SmartHopper.Core.AI.Chat
{
    /// <summary>
    /// Utility functions for the AI Chat component.
    /// </summary>
    public static class WebChatUtils
    {
        /// <summary>
        /// Dictionary to track open chat dialogs by component instance ID.
        /// </summary>
        private static readonly Dictionary<Guid, WebChatDialog> OpenDialogs = new();

        /// <summary>
        /// Shows a web-based chat dialog for the specified AI provider and model.
        /// If a dialog is already open for the specified component, it will be focused instead of creating a new one.
        /// </summary>
        /// <param name="providerName">The name of the AI provider to use.</param>
        /// <param name="modelName">The model to use for AI processing.</param>
        /// <param name="endpoint">Optional custom endpoint for the AI provider.</param>
        /// <param name="componentId">The unique ID of the component instance.</param>
        /// <param name="progressReporter">Optional action to report progress.</param>
        /// <returns>The last AI response received, or null if the dialog was closed without a response.</returns>
        public static async Task<AIResponse> ShowWebChatDialog(string providerName, string modelName, string? endpoint = null, Guid componentId = default, Action<string>? progressReporter = null)
        {
            var tcs = new TaskCompletionSource<AIResponse>();
            AIResponse? lastResponse = null;

            Debug.WriteLine("[WebChatUtils] Preparing to show web chat dialog");

            try
            {
                // Create a function to get responses from the AI provider
                Func<List<ChatMessageModel>, Task<AIResponse>> getResponse =
                    messages => AIUtils.GetResponse(providerName, modelName, messages, endpoint: endpoint, includeToolDefinitions: true);

                // We need to use Rhino's UI thread to show the dialog
                Rhino.RhinoApp.InvokeOnUiThread(() =>
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

                        // Check if a dialog is already open for this component
                        if (componentId != default && OpenDialogs.TryGetValue(componentId, out WebChatDialog existingDialog))
                        {
                            Debug.WriteLine("[WebChatUtils] Reusing existing dialog for component");

                            // Use the cross-platform EnsureVisibility method to make the dialog visible
                            existingDialog.EnsureVisibility();

                            // Complete the task with null to indicate no new response
                            tcs.TrySetResult(null);
                            return;
                        }

                        Debug.WriteLine("[WebChatUtils] Creating web chat dialog");
                        var dialog = new WebChatDialog(getResponse, progressReporter);

                        // If component ID is provided, store the dialog
                        if (componentId != default)
                        {
                            OpenDialogs[componentId] = dialog;
                        }

                        // Handle dialog closing
                        dialog.Closed += (sender, e) =>
                        {
                            Debug.WriteLine("[WebChatUtils] Dialog closed");

                            // Remove from open dialogs dictionary
                            if (componentId != default)
                            {
                                OpenDialogs.Remove(componentId);
                            }

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
                        dialog.Title = $"SmartHopper AI Chat - {modelName} ({providerName})";

                        // Show the dialog as a non-modal window
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
                });

                // Wait for the dialog to close
                return await tcs.Task.ConfigureAwait(false);
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
            private readonly Action<string> progressReporter;
            private readonly string providerName;
            private readonly string modelName;
            private readonly string endpoint;
            private readonly Guid componentId;
            private AIResponse lastResponse;

            /// <summary>
            /// Initializes a new instance of the <see cref="WebChatWorker"/> class.
            /// </summary>
            /// <param name="providerName">The name of the AI provider to use.</param>
            /// <param name="modelName">The model to use for AI processing.</param>
            /// <param name="endpoint">Optional custom endpoint for the AI provider.</param>
            /// <param name="progressReporter">Action to report progress.</param>
            /// <param name="componentId">The unique ID of the component instance.</param>
            public WebChatWorker(
                string providerName,
                string modelName,
                string endpoint,
                Action<string> progressReporter,
                Guid componentId = default)
            {
                this.providerName = providerName;
                this.modelName = modelName;
                this.endpoint = endpoint;
                this.progressReporter = progressReporter;
                this.componentId = componentId;
            }

            /// <summary>
            /// Shows the web chat dialog and processes the interaction.
            /// </summary>
            /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
            /// <returns>The task representing the asynchronous operation.</returns>
            public async Task ProcessChatAsync(CancellationToken cancellationToken)
            {
                // Decorate reporter to also expire the GH component
                Action<string>? reporter = null;
                if (this.progressReporter != null)
                {
                    reporter = msg =>
                    {
                        this.progressReporter(msg);
                        Rhino.RhinoApp.InvokeOnUiThread(() =>
                        {
                            var ghCanvas = Instances.ActiveCanvas;
                            var ghDoc = ghCanvas?.Document;
                            var comp = ghDoc?.Objects.OfType<GH_Component>().FirstOrDefault(c => c.InstanceGuid == this.componentId);
                            comp?.ExpireSolution(true);
                        });
                    };
                }

                reporter?.Invoke("Opening...");

                try
                {
                    this.lastResponse = await ShowWebChatDialog(this.providerName, this.modelName, this.endpoint, this.componentId, reporter).ConfigureAwait(false);
                    reporter?.Invoke("Run me!");
                }
                catch (Exception ex)
                {
                    reporter?.Invoke($"Error: {ex.Message}");
                    throw;
                }
            }

            /// <summary>
            /// Gets the last AI response received from the chat dialog.
            /// </summary>
            /// <returns>The last AI response, or null if no response was received.</returns>
            public AIResponse GetLastResponse()
            {
                return this.lastResponse;
            }
        }

        /// <summary>
        /// Creates a new web chat worker for processing chat interactions.
        /// </summary>
        /// <param name="providerName">The name of the AI provider to use.</param>
        /// <param name="modelName">The model to use for AI processing.</param>
        /// <param name="endpoint">Optional custom endpoint for the AI provider.</param>
        /// <param name="progressReporter">Action to report progress.</param>
        /// <param name="componentId">The unique ID of the component instance.</param>
        /// <returns>A new web chat worker.</returns>
        public static WebChatWorker CreateWebChatWorker(
            string providerName,
            string modelName,
            string endpoint,
            Action<string> progressReporter,
            Guid componentId = default)
        {
            return new WebChatWorker(providerName, modelName, endpoint, progressReporter, componentId);
        }
    }
}
