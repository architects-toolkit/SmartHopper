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
using SmartHopper.Core.AIContext;
using SmartHopper.Core.ComponentBase;
using SmartHopper.Core.UI.Chat;
using SmartHopper.Infrastructure.Managers.AIContext;
using SmartHopper.Infrastructure.Models;

namespace SmartHopper.Components.AI
{
    /// <summary>
    /// Component that provides an interactive AI chat interface using WebView for HTML rendering.
    /// </summary>
    public class AIChatComponent : AIStatefulAsyncComponentBase
    {
        private readonly TimeContextProvider timeProvider;
        private readonly EnvironmentContextProvider environmentProvider;
        private string _systemPrompt;

        private readonly string _defaultSystemPrompt = """
            You are a helpful AI assistant specialized in Grasshopper 3D and computational design. Follow these guidelines:

            - Be concise and technical in your responses
            - Explain complex concepts in simple terms
            - Avoid exposing Guids to the user
            - When providing code, include brief comments explaining key parts
            - If a question is unclear, ask for clarification
            - Admit when you don't know something rather than guessing
            - Respect the user's skill level and adjust explanations accordingly

            Focus on:
            1. Parametric design principles
            2. Algorithmic problem-solving
            3. Performance optimization
            4. Best practices in computational design

            Examples of tool calls:
            - gh_get: read the current canvas to know about the user's current structure of components
              - gh_get[attrFilters="selected"]: get only selected components
              - gh_get[attrFilters="selected +error"]: get only selected components with errors
              - gh_get[attrFilters="+error +warning"]: get all components with errors or warnings
              - gh_get[guidFilter="guid1"]: get all info about a specific component by its GUID
            - gh_list_components: list installed components to know about the user's available tools
            - gh_group: group components to highlight them to the user, or make notes about them
            - web_rhino_forum_search: look up Rhino forum discussions to try to find answers to the user's question
            - web_rhino_forum_read_post: read a specific post from the Rhino forum
            - generic_page_read: read a web page by providing the URL
            """;

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
        /// <inheritdoc/>
        protected override void RegisterAdditionalInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter(
                "Instructions",
                "I",
                "Optional initial instructions to specify the function and aim of the chat. By default, this is set to an assistant on Grasshopper.",
                GH_ParamAccess.item,
                this._defaultSystemPrompt);
        }

        /// <summary>
        /// Registers additional output parameters for the component.
        /// </summary>
        /// <param name="pManager">The parameter manager.</param>
        /// <inheritdoc/>
        protected override void RegisterAdditionalOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter(
                "Last Response",
                "R",
                "The last response from the AI assistant",
                GH_ParamAccess.item);
        }

        /// <summary>
        /// Gets the system prompt from the component.
        /// </summary>
        /// <inheritdoc/>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string systemPrompt = null;
            DA.GetData("Instructions", ref systemPrompt);
            this.SetSystemPrompt(systemPrompt);

            base.SolveInstance(DA);
        }

        /// <summary>
        /// Sets the system prompt for the AI chat.
        /// </summary>
        /// <param name="systemPrompt">The system prompt to use.</param>
        private void SetSystemPrompt(string systemPrompt)
        {
            this._systemPrompt = systemPrompt ?? throw new ArgumentNullException(nameof(systemPrompt));
        }

        /// <summary>
        /// Gets the current system prompt.
        /// </summary>
        /// <returns>The current system prompt.</returns>
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
                    Debug.WriteLine($"[AIChatWorker] Using Provider: {actualProvider} (Selected: {this.component.GetActualProviderName()})");

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
