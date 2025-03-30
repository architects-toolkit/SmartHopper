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
 * AI Web Chat Component for Grasshopper.
 * This component provides a WebView-based chat interface for interacting with AI providers.
 */

using System;
using System.Diagnostics;
using System.Threading;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using SmartHopper.Config.Models;
using SmartHopper.Core.AI.Chat;
using SmartHopper.Core.ComponentBase;

namespace SmartHopper.Components.AI
{
    /// <summary>
    /// Component that provides an interactive AI chat interface using WebView for HTML rendering.
    /// </summary>
    public class AIWebChatComponent : AIStatefulAsyncComponentBase
    {
        /// <summary>
        /// Initializes a new instance of the AIWebChatComponent class.
        /// </summary>
        public AIWebChatComponent()
            : base(
                "AI Web Chat",
                "AiWebChat",
                "Interactive AI-powered conversational interface with HTML rendering",
                "SmartHopper",
                "AI")
        {
            // Set RunOnlyOnInputChanges to false to ensure the component always runs when the Run parameter is true
            RunOnlyOnInputChanges = false;
        }

        /// <summary>
        /// Registers additional input parameters for the component.
        /// </summary>
        /// <param name="pManager">The parameter manager.</param>
        protected override void RegisterAdditionalInputParams(GH_InputParamManager pManager)
        {
            // No additional inputs needed - uses the base inputs (Model and Run)
        }

        /// <summary>
        /// Registers additional output parameters for the component.
        /// </summary>
        /// <param name="pManager">The parameter manager.</param>
        protected override void RegisterAdditionalOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Last Response", "R", "The last response from the AI assistant", GH_ParamAccess.item);
        }

        /// <summary>
        /// Creates a worker for the component.
        /// </summary>
        /// <param name="progressReporter">Action to report progress.</param>
        /// <returns>An asynchronous worker.</returns>
        protected override AsyncWorkerBase CreateWorker(Action<string> progressReporter)
        {
            return new AIWebChatWorker(this, progressReporter);
        }

        /// <summary>
        /// Gets the component's exposure level in the Grasshopper UI.
        /// </summary>
        public override GH_Exposure Exposure => GH_Exposure.secondary;

        /// <summary>
        /// Gets the component's icon.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Properties.Resources.aichat;

        /// <summary>
        /// Gets the unique ID for this component type.
        /// </summary>
        public override Guid ComponentGuid => new Guid("7D3F8B2A-E5C1-4F9D-B7A6-9C8D2E3F1A5B");

        /// <summary>
        /// Worker class for the AI Web Chat component.
        /// </summary>
        private class AIWebChatWorker : AsyncWorkerBase
        {
            private readonly AIWebChatComponent _component;
            private readonly Action<string> _progressReporter;
            private AIResponse _lastResponse;

            /// <summary>
            /// Initializes a new instance of the AIWebChatWorker class.
            /// </summary>
            /// <param name="component">The parent component.</param>
            /// <param name="progressReporter">Action to report progress.</param>
            public AIWebChatWorker(AIWebChatComponent component, Action<string> progressReporter)
                : base(component, (level, message) => component.AddRuntimeMessage(level, message))
            {
                _component = component;
                _progressReporter = progressReporter;
            }

            /// <summary>
            /// Gathers input data from the component.
            /// </summary>
            /// <param name="DA">The data access object.</param>
            public override void GatherInput(IGH_DataAccess DA)
            {
                // Base inputs (Model and Run) are handled by the base class
            }

            /// <summary>
            /// Performs the asynchronous work.
            /// </summary>
            /// <param name="token">Cancellation token.</param>
            public override async System.Threading.Tasks.Task DoWorkAsync(CancellationToken token)
            {
                try
                {
                    Debug.WriteLine("[AIWebChatWorker] Starting web chat worker");
                    _progressReporter?.Invoke("Starting web chat interface...");

                    // Get the actual provider name to use
                    string actualProvider = _component.GetActualProviderName();
                    Debug.WriteLine($"[AIWebChatWorker] Using Provider: {actualProvider} (Selected: {_component._aiProvider})");

                    // Create a web chat worker
                    var chatWorker = WebChatUtils.CreateWebChatWorker(
                        actualProvider,
                        _component.GetModel(),
                        _component.GetEndpoint(),
                        _progressReporter);

                    // Process the chat
                    await chatWorker.ProcessChatAsync(token);

                    // Get the last response
                    _lastResponse = chatWorker.GetLastResponse();

                    Debug.WriteLine("[AIWebChatWorker] Web chat worker completed");
                    _progressReporter?.Invoke("Web chat completed");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[AIWebChatWorker] Error: {ex.Message}");
                    _progressReporter?.Invoke($"Error: {ex.Message}");
                    throw;
                }
            }

            /// <summary>
            /// Sets the output data for the component.
            /// </summary>
            /// <param name="DA">The data access object.</param>
            /// <param name="message">Output message.</param>
            public override void SetOutput(IGH_DataAccess DA, out string message)
            {
                message = "Web chat completed";

                if (_lastResponse != null)
                {
                    // Set the last response output
                    var responseGoo = new GH_String(_lastResponse.Response);
                    _component.SetPersistentOutput("Last Response", responseGoo, DA);

                    // Store metrics for the base class to output
                    _component.StoreResponseMetrics(_lastResponse);

                    message = $"Web chat completed. Used {_lastResponse.InTokens} input tokens, {_lastResponse.OutTokens} output tokens.";
                }
                else
                {
                    // Set empty output if no response
                    _component.SetPersistentOutput("Last Response", new GH_String(""), DA);
                    message = "Web chat completed without a response.";
                }
            }
        }
    }
}
