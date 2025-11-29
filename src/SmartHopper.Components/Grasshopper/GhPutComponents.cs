/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using Grasshopper.Kernel;
using Newtonsoft.Json.Linq;
using SmartHopper.Components.Properties;
using SmartHopper.Core.ComponentBase;
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AICall.Core.Returns;
using SmartHopper.Infrastructure.AICall.Tools;

namespace SmartHopper.Components.Grasshopper
{
    /// <summary>
    /// Grasshopper component for placing components from JSON data.
    /// Uses StatefulAsyncComponentBase to properly manage async execution, state, and prevent re-entrancy.
    /// </summary>
    public class GhPutComponents : StatefulAsyncComponentBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GhPutComponents"/> class.
        /// </summary>
        public GhPutComponents()
            : base("Place Components", "GhPut", "Convert GhJSON to a Grasshopper components in this file.\n\nNew components will be added at the bottom of the canvas.", "SmartHopper", "Grasshopper")
        {
            // Always run when Run is true, even if inputs haven't changed
            // This allows re-placing the same JSON multiple times if needed
            this.RunOnlyOnInputChanges = false;
        }

        /// <inheritdoc/>
        public override Guid ComponentGuid => new ("25E07FD9-382C-48C0-8A97-8BFFAEAD8592");

        /// <inheritdoc/>
        protected override Bitmap Icon => Resources.ghput;

        /// <inheritdoc/>
        protected override void RegisterAdditionalInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("JSON", "J", "GhJSON document to place on canvas", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Edit Mode", "E", "When true, existing components will be replaced instead of creating new ones. User will be prompted for confirmation.", GH_ParamAccess.item, false);
        }

        /// <inheritdoc/>
        protected override void RegisterAdditionalOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Components", "C", "List of placed component names", GH_ParamAccess.list);
        }

        /// <inheritdoc/>
        protected override AsyncWorkerBase CreateWorker(Action<string> progressReporter)
        {
            return new GhPutWorker(this, this.AddRuntimeMessage, progressReporter);
        }

        /// <summary>
        /// Worker that executes the gh_put tool asynchronously.
        /// </summary>
        private class GhPutWorker : AsyncWorkerBase
        {
            private readonly Action<string> progressReporter;

            // Input data
            private string json;
            private bool editMode;

            // Output data
            private List<string> componentNames;
            private string analysis;
            private string error;

            public GhPutWorker(
                GH_Component parent,
                Action<GH_RuntimeMessageLevel, string> addRuntimeMessage,
                Action<string> progressReporter)
                : base(parent, addRuntimeMessage)
            {
                this.progressReporter = progressReporter;
                this.componentNames = new List<string>();
            }

            /// <inheritdoc/>
            public override void GatherInput(IGH_DataAccess DA, out int dataCount)
            {
                dataCount = 1;

                // Read JSON (index 0, Run is added last by StatefulAsyncComponentBase)
                this.json = null;
                DA.GetData(0, ref this.json);

                // Read Edit Mode (index 1)
                this.editMode = false;
                DA.GetData(1, ref this.editMode);
            }

            /// <inheritdoc/>
            public override async Task DoWorkAsync(CancellationToken token)
            {
                if (string.IsNullOrEmpty(this.json))
                {
                    this.error = "No JSON provided";
                    return;
                }

                this.progressReporter?.Invoke("Placing...");

                try
                {
                    token.ThrowIfCancellationRequested();

                    var parameters = new JObject
                    {
                        ["ghjson"] = this.json,
                        ["editMode"] = this.editMode,
                    };

                    var toolCallInteraction = new AIInteractionToolCall
                    {
                        Name = "gh_put",
                        Arguments = parameters,
                        Agent = AIAgent.Assistant,
                    };

                    var toolCall = new AIToolCall
                    {
                        Endpoint = "gh_put",
                    };
                    toolCall.FromToolCallInteraction(toolCallInteraction);

                    var aiResult = await toolCall.Exec().ConfigureAwait(false);

                    token.ThrowIfCancellationRequested();

                    var toolResultInteraction = aiResult.Body?.GetLastInteraction() as AIInteractionToolResult;
                    var toolResult = toolResultInteraction?.Result;

                    var success = toolResult?["success"]?.ToObject<bool>() ?? false;
                    this.analysis = toolResult?["analysis"]?.ToString();

                    if (success)
                    {
                        this.componentNames = toolResult["components"]?.ToObject<List<string>>() ?? new List<string>();
                    }
                    else
                    {
                        this.error = !string.IsNullOrWhiteSpace(this.analysis) ? this.analysis : "gh_put execution failed";
                    }
                }
                catch (OperationCanceledException)
                {
                    this.error = "Operation cancelled";
                }
                catch (Exception ex)
                {
                    this.error = ex.Message;
                }
            }

            /// <inheritdoc/>
            public override void SetOutput(IGH_DataAccess DA, out string message)
            {
                message = null;

                // Surface analysis messages
                if (!string.IsNullOrEmpty(this.analysis))
                {
                    GH_RuntimeMessageLevel currentLevel = GH_RuntimeMessageLevel.Remark;
                    foreach (var line in this.analysis.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        var trimmed = line.Trim();
                        if (trimmed == "Errors:")
                        {
                            currentLevel = GH_RuntimeMessageLevel.Error;
                        }
                        else if (trimmed == "Warnings:")
                        {
                            currentLevel = GH_RuntimeMessageLevel.Warning;
                        }
                        else if (trimmed.StartsWith("Information:"))
                        {
                            currentLevel = GH_RuntimeMessageLevel.Remark;
                        }
                        else if (trimmed.StartsWith("- "))
                        {
                            this.AddRuntimeMessage(currentLevel, trimmed.Substring(2));
                        }
                    }
                }

                if (!string.IsNullOrEmpty(this.error))
                {
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, this.error);
                    return;
                }

                // Output component names
                if (this.componentNames.Count > 0)
                {
                    DA.SetDataList(0, this.componentNames);
                    message = $"Placed {this.componentNames.Count}";
                }
            }
        }
    }
}
