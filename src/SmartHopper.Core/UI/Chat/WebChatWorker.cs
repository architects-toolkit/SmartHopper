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
 * WebChatWorker: Processes web chat interactions asynchronously for WebChat dialogs.
 * Purpose: Provide a public, top-level worker class (moved from nested type to satisfy CA1034)
 *          that opens the WebChat dialog, relays progress, and exposes the last AIReturn.
 */

using System;
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
    /// Worker class for processing web chat interactions asynchronously.
    /// </summary>
    public class WebChatWorker
    {
        private AIRequestCall initialRequest = new AIRequestCall();
        private readonly Action<string> progressReporter;
        private readonly Guid componentId;
        private AIReturn? lastReturn;
        private readonly Action<AIReturn>? onUpdateCallback;
        private readonly bool generateGreeting;

        /// <summary>
        /// Gets the last AI return received from the chat dialog, or null if none.
        /// </summary>
        public AIReturn LastReturn => this.lastReturn;

        /// <summary>
        /// Initializes a new instance of the <see cref="WebChatWorker"/> class.
        /// </summary>
        /// <param name="providerName">The name of the AI provider to use.</param>
        /// <param name="modelName">The model to use for AI processing.</param>
        /// <param name="endpoint">Optional custom endpoint for the AI provider.</param>
        /// <param name="systemPrompt">Optional system prompt to provide to the AI assistant.</param>
        /// <param name="toolFilter">Optional tool filter to provide to the AI assistant.</param>
        /// <param name="progressReporter">Action to report progress.</param>
        /// <param name="componentId">The unique ID of the component instance.</param>
        /// <param name="onUpdate">Optional callback invoked on incremental chat updates.</param>
        /// <param name="generateGreeting">Whether to generate an assistant greeting when the dialog opens.</param>
        public WebChatWorker(
            string providerName,
            string modelName,
            string endpoint,
            string systemPrompt,
            string toolFilter,
            Action<string> progressReporter,
            Guid componentId = default,
            Action<AIReturn>? onUpdate = null,
            bool generateGreeting = false)
        {
            this.initialRequest = WebChatUtils.CreateWebChatRequest(
                providerName,
                modelName,
                endpoint,
                systemPrompt,
                toolFilter);
            this.progressReporter = progressReporter;
            this.componentId = componentId;
            this.onUpdateCallback = onUpdate;
            this.generateGreeting = generateGreeting;
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
                this.lastReturn = await WebChatUtils.ShowWebChatDialog(
                    request: this.initialRequest,
                    componentId: this.componentId,
                    progressReporter: reporter,
                    onUpdate: snapshot =>
                    {
                        // Keep lastReturn updated incrementally and expire the component solution
                        this.lastReturn = snapshot;
                        reporter?.Invoke("Chatting...");

                        // Propagate snapshot to caller if provided
                        try { this.onUpdateCallback?.Invoke(snapshot); } catch (Exception cbEx) { System.Diagnostics.Debug.WriteLine($"[WebChatWorker] onUpdate callback error: {cbEx.Message}"); }
                    },
                    generateGreeting: this.generateGreeting).ConfigureAwait(false);
                reporter?.Invoke("Run me!");
            }
            catch (Exception ex)
            {
                reporter?.Invoke($"Error: {ex.Message}");
                throw;
            }
        }
    }
}
