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
 * AI Chat Component for Grasshopper.
 * This component provides a WebView-based chat interface for interacting with AI providers.
 */

using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using SmartHopper.Config.Models;
using SmartHopper.Core.AI;
using SmartHopper.Core.AI.Chat;
using SmartHopper.Core.ComponentBase;

namespace SmartHopper.Components.AI
{
    /// <summary>
    /// Component that provides an interactive AI chat interface using WebView for HTML rendering.
    /// </summary>
    public class AIChatComponent : AIStatefulAsyncComponentBase
    {
        private TimeContextProvider timeProvider;
        private EnvironmentContextProvider environmentProvider;

        private string _systemPrompt;

        /// <summary>
        /// Initializes a new instance of the <see cref="AIChatComponent"/> class.
        /// </summary>
        public AIChatComponent()
            : base(
                "AI Chat",
                "AiChat",
                "Interactive AI-powered conversational interface.",
                "SmartHopper",
                "AI")
        {
            // Set RunOnlyOnInputChanges to false to ensure the component always runs when the Run parameter is true
            this.RunOnlyOnInputChanges = false;

            // Create and register time and environment context providers
            this.timeProvider = new TimeContextProvider();
            this.environmentProvider = new EnvironmentContextProvider();

            AIContextManager.RegisterProvider(this.timeProvider);
            AIContextManager.RegisterProvider(this.environmentProvider);
        }

        /// <summary>
        /// Called when the component is removed from the canvas.
        /// </summary>
        /// <param name="document">The Grasshopper document.</param>
        public override void RemovedFromDocument(GH_Document document)
        {
            // Unregister the context providers
            AIContextManager.UnregisterProvider(this.timeProvider);
            AIContextManager.UnregisterProvider(this.environmentProvider);

            base.RemovedFromDocument(document);
        }

        /// <summary>
        /// Registers additional input parameters for the component.
        /// </summary>
        /// <param name="pManager">The parameter manager.</param>
        protected override void RegisterAdditionalInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Instructions", "I", "Optional initial instructions to specify the function and aim of the chat. By default, this is set to an assistant on Grasshopper.", GH_ParamAccess.item, "You are an AI assistant on Grasshopper.");
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
        /// Gets the system prompt from the component.
        /// </summary>
        /// <returns>The system prompt.</returns>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string systemPrompt = null;
            DA.GetData("Instructions", ref systemPrompt);
            SetSystemPrompt(systemPrompt);

            base.SolveInstance(DA);
        }

        /// <summary>
        /// Sets the system prompt for the component.
        /// </summary>
        /// <param name="systemPrompt">The system prompt.</param>
        private void SetSystemPrompt(string systemPrompt)
        {
            this._systemPrompt = systemPrompt;
        }

        /// <summary>
        /// Gets the system prompt for the component.
        /// </summary>
        /// <returns>The system prompt.</returns>
        protected string GetSystemPrompt()
        {
            return this._systemPrompt;
        }

        /// <summary>
        /// Creates a worker for the component.
        /// </summary>
        /// <param name="progressReporter">Action to report progress.</param>
        /// <returns>An asynchronous worker.</returns>
        protected override AsyncWorkerBase CreateWorker(Action<string> progressReporter)
        {
            return new AIChatWorker(this, progressReporter);
        }

        /// <summary>
        /// Gets the component's exposure level in the Grasshopper UI.
        /// </summary>
        public override GH_Exposure Exposure => GH_Exposure.primary;

        /// <summary>
        /// Gets the component's icon.
        /// </summary>
        protected override Bitmap Icon => Properties.Resources.aichat;

        /// <summary>
        /// Gets the unique ID for this component type.
        /// </summary>
        public override Guid ComponentGuid => new("7D3F8B2A-E5C1-4F9D-B7A6-9C8D2E3F1A5B");

        /// <summary>
        /// Worker class for the AI Chat component.
        /// </summary>
        private class AIChatWorker : AsyncWorkerBase
        {
            private readonly AIChatComponent component;
            private readonly Action<string> progressReporter;
            private AIResponse lastResponse;

            /// <summary>
            /// Initializes a new instance of the <see cref="AIChatWorker"/> class.
            /// </summary>
            /// <param name="component">The parent component.</param>
            /// <param name="progressReporter">Action to report progress.</param>
            public AIChatWorker(AIChatComponent component, Action<string> progressReporter)
                : base(component, (level, message) => component.AddRuntimeMessage(level, message))
            {
                this.component = component;
                this.progressReporter = progressReporter;
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
                    Debug.WriteLine("[AIChatWorker] Starting web chat worker");
                    this.progressReporter?.Invoke("Starting web chat interface...");

                    // Get the actual provider name to use
                    string actualProvider = this.component.GetActualProviderName();
                    Debug.WriteLine($"[AIChatWorker] Using Provider: {actualProvider} (Selected: {this.component._aiProvider})");

                    // Create a web chat worker
                    var chatWorker = WebChatUtils.CreateWebChatWorker(
                        actualProvider,
                        this.component.GetModel(),
                        "chat",
                        this.component.GetSystemPrompt(),
                        this.progressReporter,
                        this.component.InstanceGuid); // Pass the component's instance GUID

                    // Process the chat
                    await chatWorker.ProcessChatAsync(token).ConfigureAwait(false);

                    // Get the last response
                    this.lastResponse = chatWorker.GetLastResponse();

                    Debug.WriteLine("[AIChatWorker] Web chat worker completed");
                    // this.progressReporter?.Invoke("Web chat completed");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[AIChatWorker] Error: {ex.Message}");
                    this.progressReporter?.Invoke($"Error: {ex.Message}");
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
                message = "Ready";

                if (this.lastResponse != null)
                {
                    // Set the last response output
                    var responseGoo = new GH_String(this.lastResponse.Response);
                    this.component.SetPersistentOutput("Last Response", responseGoo, DA);

                    // Store metrics for the base class to output
                    this.component.StoreResponseMetrics(this.lastResponse);

                     message = $"Ready";
                }
                else
                {
                    // Set empty output if no response
                    this.component.SetPersistentOutput("Last Response", new GH_String(string.Empty), DA);
                    message = "Ready";
                }
            }
        }
    }
}
