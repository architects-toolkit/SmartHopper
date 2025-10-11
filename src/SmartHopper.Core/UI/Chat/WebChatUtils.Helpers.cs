/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Eto.Forms;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AICall.Core.Requests;
using SmartHopper.Infrastructure.AICall.Core.Returns;
using SmartHopper.Infrastructure.AIModels;

namespace SmartHopper.Core.UI.Chat
{
    /// <summary>
    /// WebChatUtils (helpers). Partial class containing shared helpers and configuration.
    /// </summary>
    public static partial class WebChatUtils
    {
        private const string DefaultWebChatContextFilter = "time, environment, current-file";

        /// <summary>
        /// Ensures the Eto.Forms Application is initialized and attached.
        /// </summary>
        private static void EnsureEtoApplication()
        {
            if (Application.Instance == null)
            {
                Debug.WriteLine("[WebChatUtils] Initializing Eto.Forms application");
                var platform = Eto.Platform.Detect;
                new Application(platform).Attach();
            }
        }

        /// <summary>
        /// Brings a dialog to front, ensuring it is visible and focused.
        /// </summary>
        private static void BringToFrontAndFocus(WebChatDialog dialog)
        {
            dialog.EnsureVisibility();
            dialog.BringToFront();
            dialog.Focus();
        }

        /// <summary>
        /// Shared helper to open or reuse a WebChatDialog on the UI thread, attach handlers, and bring to front.
        /// Optionally wires closed cleanup to a TaskCompletionSource for ShowWebChatDialog flows.
        /// </summary>
        private static WebChatDialog OpenOrReuseDialogInternal(
            AIRequestCall request,
            Guid componentId,
            Action<string>? progressReporter,
            Action<AIReturn>? onUpdate,
            TaskCompletionSource<AIReturn>? completionTcs,
            bool pushCurrentImmediately,
            bool generateGreeting,
            out bool reused)
        {
            EnsureEtoApplication();

            if (componentId != default && OpenDialogs.TryGetValue(componentId, out WebChatDialog existingDialog))
            {
                Debug.WriteLine("[WebChatUtils] Reusing existing dialog for component");
                BringToFrontAndFocus(existingDialog);
                AttachOrReplaceUpdateHandler(componentId, existingDialog, progressReporter, onUpdate, pushCurrentImmediately: pushCurrentImmediately);

                // For ShowWebChatDialog flows, immediate completion mirrors previous behavior
                if (completionTcs != null)
                {
                    try { completionTcs.TrySetResult(existingDialog.GetLastReturn()); } catch { /* ignore */ }
                }

                reused = true;
                return existingDialog;
            }

            Debug.WriteLine("[WebChatUtils] Creating web chat dialog");
            var dialog = new WebChatDialog(request, progressReporter, generateGreeting: generateGreeting);
            if (componentId != default)
            {
                OpenDialogs[componentId] = dialog;
            }

            // Closed cleanup + optional result propagation
            WireClosedCleanup(componentId, dialog, completionTcs);

            // Incremental updates
            AttachOrReplaceUpdateHandler(componentId, dialog, progressReporter, onUpdate);

            dialog.Title = $"SmartHopper AI Chat - {request.Model} ({request.Provider})";
            dialog.Show();
            BringToFrontAndFocus(dialog);

            reused = false;
            return dialog;
        }

        /// <summary>
        /// Attaches (or replaces) a ChatUpdated handler for a component/dialog pair and tracks it.
        /// </summary>
        private static void AttachOrReplaceUpdateHandler(
            Guid componentId,
            WebChatDialog dialog,
            Action<string>? progressReporter,
            Action<AIReturn>? onUpdate,
            bool pushCurrentImmediately = false)
        {
            if (onUpdate == null)
            {
                return;
            }

            if (UpdateHandlers.TryGetValue(componentId, out var oldHandler))
            {
                try
                {
                    dialog.ChatUpdated -= oldHandler;
                }
                catch
                {
                    /* ignore */
                }
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

            dialog.ChatUpdated += handler;
            UpdateHandlers[componentId] = handler;

            if (pushCurrentImmediately)
            {
                try
                {
                    var current = dialog.GetLastReturn();
                    if (current != null)
                    {
                        onUpdate(current);
                    }
                }
                catch
                {
                    /* ignore */
                }
            }
        }

        /// <summary>
        /// Wires dialog closed cleanup and optionally completes a TaskCompletionSource with the last return.
        /// </summary>
        private static void WireClosedCleanup(Guid componentId, WebChatDialog dialog, TaskCompletionSource<AIReturn>? tcs = null)
        {
            dialog.Closed += (sender, e) =>
            {
                Debug.WriteLine("[WebChatUtils] Dialog closed");
                try
                {
                    if (componentId != default)
                    {
                        OpenDialogs.Remove(componentId);
                        if (UpdateHandlers.ContainsKey(componentId))
                        {
                            UpdateHandlers.Remove(componentId);
                        }
                    }

                    var last = dialog.GetLastReturn();
                    tcs?.TrySetResult(last);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[WebChatUtils] Closed cleanup error: {ex.Message}");
                }
            };
        }

        internal static AIRequestCall CreateWebChatRequest(
            string providerName,
            string modelName,
            string endpoint,
            string systemPrompt,
            string toolFilter)
        {
            var requiredCapabilities = ComputeRequiredCapabilities(toolFilter);

            var request = new AIRequestCall();
            request.Initialize(
                provider: providerName,
                model: modelName,
                systemPrompt: systemPrompt,
                endpoint: endpoint,
                capability: requiredCapabilities,
                toolFilter: toolFilter);

            request.Body = ApplyDefaultWebChatContext(request.Body);
            return request;
        }

        private static AIBody ApplyDefaultWebChatContext(AIBody body)
        {
            return AIBodyBuilder.FromImmutable(body)
                .WithContextFilter(DefaultWebChatContextFilter)
                .Build();
        }

        private static AICapability ComputeRequiredCapabilities(string toolFilter)
        {
            return toolFilter != "-*" ? AICapability.ToolChat : AICapability.Text2Text;
        }
    }
}
