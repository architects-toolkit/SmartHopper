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
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Threading;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using SmartHopper.Core.ComponentBase;
using SmartHopper.Core.UI.Chat;
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Returns;
using SmartHopper.Infrastructure.AIModels;

namespace SmartHopper.Components.AI
{
    /// <summary>
    /// Component that provides an interactive AI chat interface using WebView for HTML rendering.
    /// </summary>
    public class AIChatComponent : AIStatefulAsyncComponentBase
    {
        private string _systemPrompt;

        // Removed duplicated last-return storage; use base AIReturn snapshot instead

        protected override AICapability RequiredCapability => AICapability.ToolChat;

        private readonly string _defaultSystemPrompt = """
            Your function is not predefined. Follow user instructions. Be concise in your responses.
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
        }

        /// <summary>
        /// Called when the component is removed from the canvas.
        /// </summary>
        /// <param name="document">The Grasshopper document.</param>
        public override void RemovedFromDocument(GH_Document document)
        {
            // Ensure we detach ChatUpdated handlers for this component's dialog
            try { WebChatUtils.Unsubscribe(this.InstanceGuid); } catch { /* ignore */ }

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
                "Chat History",
                "H",
                "Full chat transcript with timestamps and aggregated metrics",
                GH_ParamAccess.list);
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

            // Run base state machine/async logic first
            base.SolveInstance(DA);

            // Always reflect the latest AIReturn snapshot in the Chat History output,
            // even when no workers are currently running (e.g., incremental UI updates).
            var historyItems = new List<GH_String>();
            try
            {
                var ret = this.GetSnapshot();
                var interactions = ret?.Body?.Interactions;

                if (interactions != null && interactions.Count > 0)
                {
                    foreach (var interaction in interactions)
                    {
                        if (interaction == null) continue;

                        Debug.WriteLine($"[AIChatComponent] Interaction from {interaction.Agent}: {interaction}");

                        var ts = interaction.Time.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture);
                        var role = interaction.Agent.ToDescription();
                        var content = interaction.ToString() ?? string.Empty;

                        // Inline, single-item per interaction
                        content = content.Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ").Trim();
                        var line = $"[{ts}] {role}: {content}".Trim();
                        historyItems.Add(new GH_String(line));
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AIChatComponent] SolveInstance history build error: {ex.Message}");
            }

            // Persistently set (or clear) the chat history so downstream updates occur
            this.SetPersistentOutput("Chat History", historyItems, DA);

            // Set metrics synchronously with chat history to ensure consistent downstream updates
            this.SetMetricsOutput(DA);
        }

        /// <summary>
        /// Sets the system prompt for the AI chat.
        /// </summary>
        /// <param name="systemPrompt">The system prompt to use.</param>
        private void SetSystemPrompt(string systemPrompt)
        {
            this._systemPrompt = string.IsNullOrWhiteSpace(systemPrompt) ? this._defaultSystemPrompt : systemPrompt;
            this.SystemPrompt = this._systemPrompt;
        }

        /// <summary>
        /// Gets the current system prompt.
        /// </summary>
        protected string SystemPrompt { get; private set; }

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
        /// Disable automatic restoration of persistent outputs from the base class.
        /// The AIChat component manages its outputs explicitly each solve using the
        /// latest AIReturn snapshot pushed by the WebChat dialog (partial updates only).
        /// </summary>
        protected override bool AutoRestorePersistentOutputs => false;

        // No local getters/setters for last return; rely on SetAIReturnSnapshot/GetAIReturnSnapshot from base

        /// <summary>
        /// Exposes the current AIReturn snapshot from the base class to internal helpers (e.g., nested worker).
        /// </summary>
        internal AIReturn GetSnapshot()
        {
            return this.CurrentAIReturnSnapshot;
        }

        /// <summary>
        /// Disable base post-solve metrics emission; metrics are emitted synchronously in SolveInstance.
        /// </summary>
        protected override bool ShouldEmitMetricsInPostSolve()
        {
            return false;
        }

        /// <summary>
        /// Worker class for the AI Chat component.
        /// </summary>
        private sealed class AIChatWorker : AsyncWorkerBase
        {
            private readonly AIChatComponent component;
            private readonly Action<string> progressReporter;

            // No local copy: rely solely on component's AIReturn snapshot

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
            public override void GatherInput(IGH_DataAccess DA, out int dataCount)
            {
                // Base inputs (Model and Run) are handled by the base class
                dataCount = 0;
            }

            /// <summary>
            /// Performs the asynchronous work.
            /// </summary>
            /// <param name="token">Cancellation token.</param>
            public override async System.Threading.Tasks.Task DoWorkAsync(CancellationToken token)
            {
                try
                {
                    Debug.WriteLine("[AIChatWorker] Ensuring web chat dialog is open");
                    this.progressReporter?.Invoke("Starting web chat interface...");

                    // Get the actual provider name to use
                    string actualProvider = this.component.GetActualAIProviderName();
                    Debug.WriteLine($"[AIChatWorker] Using Provider: {actualProvider} (Selected: {this.component.GetActualAIProviderName()})");

                    // Ensure dialog is open (non-blocking) and wire incremental updates
                    WebChatUtils.EnsureDialogOpen(
                        providerName: actualProvider,
                        modelName: this.component.GetModel(),
                        endpoint: "ai-chat",
                        systemPrompt: this.component.SystemPrompt,
                        toolFilter: "Components,ComponentsRetrieval,Knowledge,Scripting",
                        componentId: this.component.InstanceGuid,
                        progressReporter: this.progressReporter,
                        onUpdate: snapshot =>
                        {
                            try
                            {
                                // Store full AIReturn in the base snapshot so metrics and other
                                // downstream outputs can be derived from a single source of truth
                                this.component.SetAIReturnSnapshot(snapshot);

                                // Force downstream recompute so UI and dependents update promptly
                                Rhino.RhinoApp.InvokeOnUiThread(() =>
                                {
                                    this.component.ExpireSolution(true);
                                });

                                // Nudge GH to keep UI responsive
                                this.progressReporter?.Invoke("Chatting...");
                            }
                            catch (Exception cbEx)
                            {
                                Debug.WriteLine($"[AIChatWorker] onUpdate callback error: {cbEx.Message}");
                            }
                        });

                    // Immediately refresh local snapshot if dialog already had content
                    var current = WebChatUtils.TryGetLastReturn(this.component.InstanceGuid);
                    if (current != null)
                    {
                        // Keep base snapshot synchronized for metrics output
                        this.component.SetAIReturnSnapshot(current);
                    }

                    // This worker is intentionally short-lived; incremental updates will retrigger recomputes
                    await System.Threading.Tasks.Task.CompletedTask;
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
                // Output is set exclusively in AIChatComponent.SolveInstance to avoid duplication/nested branches.
                // Worker only reports status here.
                message = "Output data";
            }
        }
    }
}
