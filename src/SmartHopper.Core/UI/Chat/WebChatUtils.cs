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
using SmartHopper.Infrastructure.AICall.Core.Requests;
using SmartHopper.Infrastructure.AICall.Core.Returns;
using SmartHopper.Infrastructure.AICall.Metrics;
using SmartHopper.Infrastructure.AIModels;

namespace SmartHopper.Core.UI.Chat
{
    /// <summary>
    /// Utility functions for the AI Chat component.
    /// </summary>
    public static class WebChatUtils
    {
        /// <summary>
        /// Dictionary to track open chat dialogs by component instance ID.
        /// </summary>
        private static readonly Dictionary<Guid, WebChatDialog> OpenDialogs = new ();

        /// <summary>
        /// Tracks ChatUpdated subscriptions to avoid duplicate handlers per component.
        /// </summary>
        private static readonly Dictionary<Guid, EventHandler<AIReturn>> UpdateHandlers = new ();

        /// <summary>
        /// Static constructor to set up application shutdown handling.
        /// </summary>
        static WebChatUtils()
        {
            // Subscribe to Rhino's closing event to clean up dialogs
            Rhino.RhinoApp.Closing += OnRhinoClosing;
        }

        /// <summary>
        /// Handles Rhino application closing by disposing all open chat dialogs.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        private static void OnRhinoClosing(object sender, EventArgs e)
        {
            Debug.WriteLine("[WebChatUtils] Rhino is closing, cleaning up open chat dialogs");

            try
            {
                // Create a copy of the keys to avoid collection modification during iteration
                var dialogIds = OpenDialogs.Keys.ToArray();

                foreach (var dialogId in dialogIds)
                {
                    if (OpenDialogs.TryGetValue(dialogId, out WebChatDialog dialog))
                    {
                        Debug.WriteLine($"[WebChatUtils] Closing dialog for component {dialogId}");

                        // Close the dialog on the UI thread to ensure proper cleanup
                        Rhino.RhinoApp.InvokeOnUiThread(() =>
                        {
                            try
                            {
                                // Detach update handler if any
                                if (UpdateHandlers.TryGetValue(dialogId, out var handler))
                                {
                                    try { dialog.ChatUpdated -= handler; } catch { /* ignore */ }
                                    UpdateHandlers.Remove(dialogId);
                                }
                                dialog?.Close();
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"[WebChatUtils] Error closing dialog {dialogId}: {ex.Message}");
                            }
                        });

                        // Remove from tracking dictionary
                        OpenDialogs.Remove(dialogId);
                    }
                }

                Debug.WriteLine($"[WebChatUtils] Cleanup complete. Closed {dialogIds.Length} dialogs");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebChatUtils] Error during dialog cleanup: {ex.Message}");
            }
        }

        /// <summary>
        /// Unsubscribes any ChatUpdated handler associated with the given component ID and
        /// removes the handler from internal tracking. Safe to call even if no dialog is open.
        /// </summary>
        /// <param name="componentId">The component instance ID.</param>
        public static void Unsubscribe(Guid componentId)
        {
            try
            {
                if (componentId == Guid.Empty)
                {
                    return;
                }

                if (OpenDialogs.TryGetValue(componentId, out var dialog))
                {
                    if (UpdateHandlers.TryGetValue(componentId, out var handler))
                    {
                        try { dialog.ChatUpdated -= handler; } catch { /* ignore */ }
                        UpdateHandlers.Remove(componentId);
                    }
                }
                else
                {
                    // No open dialog, just clear any stale handler reference
                    if (UpdateHandlers.ContainsKey(componentId))
                    {
                        UpdateHandlers.Remove(componentId);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebChatUtils] Unsubscribe error for {componentId}: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the current last return for an open dialog, if any; otherwise null.
        /// </summary>
        public static AIReturn? TryGetLastReturn(Guid componentId)
        {
            try
            {
                if (componentId != Guid.Empty && OpenDialogs.TryGetValue(componentId, out var dialog))
                {
                    return dialog.GetLastReturn();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebChatUtils] TryGetLastReturn error for {componentId}: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Shows a web-based chat dialog for the specified AI request.
        /// If a dialog is already open for the specified component, it will be focused instead of creating a new one.
        /// </summary>
        /// <param name="request">The AI request call containing provider, model, and configuration.</param>
        /// <param name="componentId">The unique ID of the component instance.</param>
        /// <param name="progressReporter">Optional action to report progress.</param>
        /// <param name="onUpdate">Optional callback invoked on incremental chat updates with an AIReturn snapshot.</param>
        /// <returns>The last AI return received, or null if the dialog was closed without a response.</returns>
        public static async Task<AIReturn> ShowWebChatDialog(AIRequestCall request, Guid componentId, Action<string>? progressReporter = null, Action<AIReturn>? onUpdate = null)
        {
            if (componentId == Guid.Empty)
            {
                componentId = Guid.NewGuid();
            }

            var tcs = new TaskCompletionSource<AIReturn>();
            AIReturn? lastReturn = null;

            Debug.WriteLine("[WebChatUtils] Preparing to show web chat dialog");

            try
            {
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

                            // Ensure update handler is wired at most once per component
                            if (onUpdate != null)
                            {
                                if (UpdateHandlers.TryGetValue(componentId, out var oldHandler))
                                {
                                    try { existingDialog.ChatUpdated -= oldHandler; } catch { /* ignore */ }
                                }

                                EventHandler<AIReturn> handler = (s, snapshot) =>
                                {
                                    try
                                    {
                                        progressReporter?.Invoke("Chatting...");
                                        onUpdate?.Invoke(snapshot);
                                        // Nudge GH to recompute to reflect incremental updates
                                        Rhino.RhinoApp.InvokeOnUiThread(() =>
                                        {
                                            try
                                            {
                                                var ghCanvas = Instances.ActiveCanvas;
                                                var ghDoc = ghCanvas?.Document;
                                                var comp = ghDoc?.Objects.OfType<GH_Component>().FirstOrDefault(c => c.InstanceGuid == componentId);
                                                comp?.ExpireSolution(true);
                                            }
                                            catch { /* ignore */ }
                                        });
                                    }
                                    catch (Exception updEx)
                                    {
                                        Debug.WriteLine($"[WebChatUtils] ChatUpdated handler error: {updEx.Message}");
                                    }
                                };
                                existingDialog.ChatUpdated += handler;
                                UpdateHandlers[componentId] = handler;
                            }

                            // Get the last return from the existing dialog
                            var existingReturn = existingDialog.GetLastReturn();
                            tcs.TrySetResult(existingReturn);
                            return;
                        }

                        Debug.WriteLine("[WebChatUtils] Creating web chat dialog");
                        var dialog = new WebChatDialog(request, progressReporter);

                        // If component ID is provided, store the dialog
                        if (componentId != default)
                        {
                            OpenDialogs[componentId] = dialog;
                        }

                        // Handle dialog closing
                        dialog.Closed += (sender, e) =>
                        {
                            Debug.WriteLine("[WebChatUtils] Dialog closed");

                            // Remove dialog from tracking
                            if (componentId != default)
                            {
                                OpenDialogs.Remove(componentId);
                                // Remove any update handler tracking
                                if (UpdateHandlers.ContainsKey(componentId))
                                {
                                    UpdateHandlers.Remove(componentId);
                                }
                            }

                            // Complete the task with the last return
                            lastReturn = dialog.GetLastReturn();
                            tcs.TrySetResult(lastReturn);
                        };

                        // Handle incremental chat updates to propagate to worker and GH canvas
                        if (onUpdate != null)
                        {
                            EventHandler<AIReturn> handler = (sender, snapshot) =>
                            {
                                try
                                {
                                    lastReturn = snapshot;
                                    // Nudge GH to recompute via the provided progress reporter
                                    progressReporter?.Invoke("Chatting...");
                                    // Propagate snapshot to caller
                                    onUpdate?.Invoke(snapshot);
                                }
                                catch (Exception updEx)
                                {
                                    Debug.WriteLine($"[WebChatUtils] ChatUpdated handler error: {updEx.Message}");
                                }
                            };
                            dialog.ChatUpdated += handler;
                            if (componentId != default)
                            {
                                UpdateHandlers[componentId] = handler;
                            }
                        }

                        // Handle final response received (kept for completeness)
                        dialog.ResponseReceived += (sender, result) =>
                        {
                            Debug.WriteLine("[WebChatUtils] Response received from dialog");
                            lastReturn = result;
                        };

                        // Configure the dialog window
                        dialog.Title = $"SmartHopper AI Chat - {request.Model} ({request.Provider})";

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
        /// Ensures a non-modal web chat dialog is open and focused for the given component. This method returns immediately
        /// and wires the ChatUpdated event (at most once per component) to provide incremental updates.
        /// </summary>
        /// <param name="providerName">AI provider name.</param>
        /// <param name="modelName">Model name.</param>
        /// <param name="endpoint">Custom endpoint identifier.</param>
        /// <param name="systemPrompt">Optional system prompt.</param>
        /// <param name="toolFilter">Tool filter for capabilities.</param>
        /// <param name="componentId">The unique component instance ID.</param>
        /// <param name="progressReporter">Progress reporter that will also be invoked on updates.</param>
        /// <param name="onUpdate">Callback for incremental updates.</param>
        public static void EnsureDialogOpen(
            string providerName,
            string modelName,
            string endpoint,
            string systemPrompt,
            string toolFilter,
            Guid componentId,
            Action<string> progressReporter = null!,
            Action<AIReturn>? onUpdate = null)
        {
            if (componentId == Guid.Empty)
            {
                componentId = Guid.NewGuid();
            }

            // Build request like WebChatWorker does
            var requiredCapabilites = AICapability.Text2Text;
            if (toolFilter != "-*")
            {
                requiredCapabilites = AICapability.ToolChat;
            }

            var request = new AIRequestCall();
            request.Initialize(
                provider: providerName,
                model: modelName,
                systemPrompt: systemPrompt,
                endpoint: endpoint,
                capability: requiredCapabilites,
                toolFilter: toolFilter);

            try
            {
                Rhino.RhinoApp.InvokeOnUiThread(() =>
                {
                    try
                    {
                        // Initialize Eto application if needed
                        if (Application.Instance == null)
                        {
                            var platform = Eto.Platform.Detect;
                            new Application(platform).Attach();
                        }

                        // If already open, just bring to front and ensure update subscription (one per component)
                        if (OpenDialogs.TryGetValue(componentId, out var existing))
                        {
                            existing.EnsureVisibility();

                            if (onUpdate != null)
                            {
                                if (UpdateHandlers.TryGetValue(componentId, out var oldHandler))
                                {
                                    try { existing.ChatUpdated -= oldHandler; } catch { /* ignore */ }
                                }

                                EventHandler<AIReturn> handler = (s, snapshot) =>
                                {
                                    try
                                    {
                                        progressReporter?.Invoke("Chatting...");
                                        onUpdate?.Invoke(snapshot);
                                    }
                                    catch (Exception updEx)
                                    {
                                        Debug.WriteLine($"[WebChatUtils] ChatUpdated handler error: {updEx.Message}");
                                    }
                                };
                                existing.ChatUpdated += handler;
                                UpdateHandlers[componentId] = handler;

                                // Immediately push current content if any
                                var current = existing.GetLastReturn();
                                if (current != null)
                                {
                                    try { onUpdate(current); } catch { /* ignore */ }
                                }
                            }

                            return;
                        }

                        // Create a new dialog
                        var dialog = new WebChatDialog(request, progressReporter);
                        OpenDialogs[componentId] = dialog;

                        // Closed cleanup
                        dialog.Closed += (s, e) =>
                        {
                            try
                            {
                                OpenDialogs.Remove(componentId);
                                if (UpdateHandlers.ContainsKey(componentId))
                                {
                                    UpdateHandlers.Remove(componentId);
                                }
                            }
                            catch { /* ignore */ }
                        };

                        // Updates
                        if (onUpdate != null)
                        {
                            EventHandler<AIReturn> handler = (s, snapshot) =>
                            {
                                try
                                {
                                    progressReporter?.Invoke("Chatting...");
                                    onUpdate?.Invoke(snapshot);
                                    // Nudge GH to recompute to reflect incremental updates
                                    Rhino.RhinoApp.InvokeOnUiThread(() =>
                                    {
                                        try
                                        {
                                            var ghCanvas = Instances.ActiveCanvas;
                                            var ghDoc = ghCanvas?.Document;
                                            var comp = ghDoc?.Objects.OfType<GH_Component>().FirstOrDefault(c => c.InstanceGuid == componentId);
                                            comp?.ExpireSolution(true);
                                        }
                                        catch { /* ignore */ }
                                    });
                                }
                                catch (Exception updEx)
                                {
                                    Debug.WriteLine($"[WebChatUtils] ChatUpdated handler error: {updEx.Message}");
                                }
                            };
                            dialog.ChatUpdated += handler;
                            UpdateHandlers[componentId] = handler;
                        }

                        dialog.Title = $"SmartHopper AI Chat - {request.Model} ({request.Provider})";
                        dialog.Show();
                        dialog.BringToFront();
                        dialog.Focus();
                        progressReporter?.Invoke("Ready");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[WebChatUtils] EnsureDialogOpen UI error: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebChatUtils] EnsureDialogOpen error: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Worker class for processing web chat interactions asynchronously.
        /// </summary>
        public class WebChatWorker
        {
            private AIRequestCall initialRequest = new AIRequestCall();
            private readonly Action<string> progressReporter;
            private readonly Guid componentId;
            private AIReturn? lastReturn;
            private readonly Action<AIReturn>? onUpdateCallback;

            /// <summary>
            /// Initializes a new instance of the <see cref="WebChatWorker"/> class.
            /// </summary>
            /// <param name="providerName">The name of the AI provider to use.</param>
            /// <param name="modelName">The model to use for AI processing.</param>
            /// <param name="systemPrompt">Optional system prompt to provide to the AI assistant.</param>
            /// <param name="endpoint">Optional custom endpoint for the AI provider.</param>
            /// <param name="toolFilter">Optional tool filter to provide to the AI assistant.</param>
            /// <param name="progressReporter">Action to report progress.</param>
            /// <param name="componentId">The unique ID of the component instance.</param>
            public WebChatWorker(
                string providerName,
                string modelName,
                string endpoint,
                string systemPrompt,
                string toolFilter,
                Action<string> progressReporter,
                Guid componentId = default,
                Action<AIReturn>? onUpdate = null)
            {
                var requiredCapabilites = AICapability.Text2Text;
                if (toolFilter != "-*")
                {
                    requiredCapabilites = AICapability.ToolChat;
                }
                
                this.initialRequest.Initialize(
                    provider: providerName,
                    model: modelName,
                    systemPrompt: systemPrompt,
                    endpoint: endpoint,
                    capability: requiredCapabilites,
                    toolFilter: toolFilter);
                this.progressReporter = progressReporter;
                this.componentId = componentId;
                this.onUpdateCallback = onUpdate;
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
                    this.lastReturn = await ShowWebChatDialog(
                        request: this.initialRequest,
                        componentId: this.componentId,
                        progressReporter: reporter,
                        onUpdate: snapshot =>
                        {
                            // Keep lastReturn updated incrementally and expire the component solution
                            this.lastReturn = snapshot;
                            reporter?.Invoke("Chatting...");
                            // Propagate snapshot to caller if provided
                            try { this.onUpdateCallback?.Invoke(snapshot); } catch (Exception cbEx) { Debug.WriteLine($"[WebChatWorker] onUpdate callback error: {cbEx.Message}"); }
                        }).ConfigureAwait(false);
                    reporter?.Invoke("Run me!");
                }
                catch (Exception ex)
                {
                    reporter?.Invoke($"Error: {ex.Message}");
                    throw;
                }
            }

            /// <summary>
            /// Gets the last AI return received from the chat dialog.
            /// </summary>
            /// <returns>The last AI return, or null if no return was received.</returns>
            public AIReturn GetLastReturn()
            {
                return this.lastReturn;
            }

            /// <summary>
            /// Gets the combined metrics from all interactions in the chat history.
            /// </summary>
            /// <returns>Combined AI metrics from all chat interactions.</returns>
            public AIMetrics GetCombinedMetrics()
            {
                if (this.lastReturn != null && this.lastReturn.Body?.Interactions?.Count > 0)
                {
                    var combinedMetrics = new AIMetrics();
                    foreach (var interaction in this.lastReturn.Body.Interactions)
                    {
                        if (interaction.Metrics != null)
                        {
                            combinedMetrics.Combine(interaction.Metrics);
                        }
                    }
                    return combinedMetrics;
                }
                return new AIMetrics();
            }
        }

        /// <summary>
        /// Creates a new web chat worker for processing chat interactions.
        /// </summary>
        /// <param name="providerName">The name of the AI provider to use.</param>
        /// <param name="modelName">The model to use for AI processing.</param>
        /// <param name="endpoint">Optional custom endpoint for the AI provider.</param>
        /// <param name="systemPrompt">Optional system prompt to provide to the AI assistant.</param>
        /// <param name="toolFilter">The tool filter to provide to the AI assistant.</param>
        /// <param name="componentId">The unique ID of the component instance.</param>
        /// <param name="progressReporter">Action to report progress.</param>
        /// <returns>A new web chat worker.</returns>
        public static WebChatWorker CreateWebChatWorker(
            string providerName,
            string modelName,
            string endpoint,
            string systemPrompt,
            string toolFilter,
            Guid componentId,
            Action<string> progressReporter = null!,
            Action<AIReturn>? onUpdate = null)
        {
            if (componentId == Guid.Empty)
            {
                componentId = Guid.NewGuid();
            }

            return new WebChatWorker(
                providerName,
                modelName,
                endpoint,
                systemPrompt,
                toolFilter,
                progressReporter,
                componentId,
                onUpdate);
        }
    }
}

