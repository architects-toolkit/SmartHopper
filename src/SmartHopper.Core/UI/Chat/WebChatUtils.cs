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
using Grasshopper;
using Grasshopper.Kernel;
using SmartHopper.Infrastructure.AICall.Core.Requests;
using SmartHopper.Infrastructure.AICall.Core.Returns;

namespace SmartHopper.Core.UI.Chat
{
    /// <summary>
    /// Utility functions for the AI Chat component.
    /// </summary>
    public static partial class WebChatUtils
    {
        [Conditional("DEBUG")]
        private static void DebugLog(string message)
        {
            Debug.WriteLine(message);
        }

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
            DebugLog("[WebChatUtils] Rhino is closing, cleaning up open chat dialogs");

            try
            {
                // Create a copy of the keys to avoid collection modification during iteration
                var dialogIds = OpenDialogs.Keys.ToArray();

                foreach (var dialogId in dialogIds)
                {
                    if (OpenDialogs.TryGetValue(dialogId, out WebChatDialog dialog))
                    {
                        DebugLog($"[WebChatUtils] Closing dialog for component {dialogId}");

                        // Close the dialog on the UI thread to ensure proper cleanup
                        Rhino.RhinoApp.InvokeOnUiThread(() =>
                        {
                            try
                            {
                                // Detach update handler if any
                                if (UpdateHandlers.TryGetValue(dialogId, out var handler))
                                {
                                    try
                                    {
                                        dialog.ChatUpdated -= handler;
                                    }
                                    catch
                                    {
                                        /* ignore */
                                    }

                                    UpdateHandlers.Remove(dialogId);
                                }

                                dialog?.Close();
                            }
                            catch (Exception ex)
                            {
                                DebugLog($"[WebChatUtils] Error closing dialog {dialogId}: {ex.Message}");
                            }
                        });

                        // Remove from tracking dictionary
                        OpenDialogs.Remove(dialogId);
                    }
                }

                DebugLog($"[WebChatUtils] Cleanup complete. Closed {dialogIds.Length} dialogs");
            }
            catch (Exception ex)
            {
                DebugLog($"[WebChatUtils] Error during dialog cleanup: {ex.Message}");
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
                        try
                        {
                            dialog.ChatUpdated -= handler;
                        }
                        catch
                        {
                            /* ignore */
                        }

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
                DebugLog($"[WebChatUtils] Unsubscribe error for {componentId}: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the current last return for an open dialog, if any; otherwise null.
        /// </summary>
        /// <param name="componentId">The component instance ID whose dialog state to query.</param>
        /// <returns>The last AI return received, or null if the dialog was closed without a response.</returns>
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
                DebugLog($"[WebChatUtils] TryGetLastReturn error for {componentId}: {ex.Message}");
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
        /// <param name="generateGreeting">Whether to generate an initial assistant greeting on dialog open.</param>
        /// <returns>The last AI return received, or null if the dialog was closed without a response.</returns>
        public static async Task<AIReturn> ShowWebChatDialog(
            AIRequestCall request,
            Guid componentId,
            Action<string>? progressReporter = null,
            Action<AIReturn>? onUpdate = null,
            bool generateGreeting = false)
        {
            if (componentId == Guid.Empty)
            {
                componentId = Guid.NewGuid();
            }

            var tcs = new TaskCompletionSource<AIReturn>();
            AIReturn? lastReturn = null;
            DebugLog("[WebChatUtils] Preparing to show web chat dialog");

            try
            {
                // We need to use Rhino's UI thread to show the dialog
                Rhino.RhinoApp.InvokeOnUiThread(() =>
                {
                    try
                    {
                        bool reused;
                        var dialog = OpenOrReuseDialogInternal(
                            request,
                            componentId,
                            progressReporter,
                            onUpdate,
                            tcs,
                            pushCurrentImmediately: false,
                            generateGreeting: generateGreeting,
                            out reused);

                        // Mirror previous logging and keep a local lastReturn if needed by callers
                        dialog.ResponseReceived += (sender, result) =>
                        {
                            DebugLog("[WebChatUtils] Response received from dialog");
                            lastReturn = result;
                        };
                    }
                    catch (Exception ex)
                    {
                        DebugLog($"[WebChatUtils] Error in UI thread: {ex.Message}");
                        tcs.TrySetException(ex);
                    }
                });

                // Wait for the dialog to close
                return await tcs.Task.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                DebugLog($"[WebChatUtils] Error showing web chat dialog: {ex.Message}");
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
            var request = CreateWebChatRequest(providerName, modelName, endpoint, systemPrompt, toolFilter);

            try
            {
                Rhino.RhinoApp.InvokeOnUiThread(() =>
                {
                    try
                    {
                        bool reused;
                        _ = OpenOrReuseDialogInternal(
                            request,
                            componentId,
                            progressReporter,
                            onUpdate,
                            completionTcs: null,
                            pushCurrentImmediately: true,
                            generateGreeting: false,
                            out reused);

                        progressReporter?.Invoke("Ready");
                    }
                    catch (Exception ex)
                    {
                        DebugLog($"[WebChatUtils] EnsureDialogOpen UI error: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                DebugLog($"[WebChatUtils] EnsureDialogOpen error: {ex.Message}");
                throw;
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
        /// <param name="onUpdate">Optional callback invoked on incremental chat updates with an AIReturn snapshot.</param>
        /// <param name="generateGreeting">Whether to generate an assistant greeting when the dialog opens.</param>
        /// <returns>A new web chat worker.</returns>
        public static WebChatWorker CreateWebChatWorker(
            string providerName,
            string modelName,
            string endpoint,
            string systemPrompt,
            string toolFilter,
            Guid componentId,
            Action<string> progressReporter = null!,
            Action<AIReturn>? onUpdate = null,
            bool generateGreeting = false)
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
                onUpdate,
                generateGreeting);
        }
    }
}
