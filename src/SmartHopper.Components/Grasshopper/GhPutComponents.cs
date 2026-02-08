/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024-2026 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 *
 * This library is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this library; if not, see <https://www.gnu.org/licenses/lgpl-3.0.html>.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Grasshopper.Kernel;
using Newtonsoft.Json.Linq;
using SmartHopper.Components.Properties;
using SmartHopper.Core.ComponentBase;
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AICall.Tools;

namespace SmartHopper.Components.Grasshopper
{
    /// <summary>
    /// Grasshopper component for placing components from JSON data.
    /// Uses StatefulComponentBase to properly manage async execution, state, and prevent re-entrancy.
    /// </summary>
    public class GhPutComponents : StatefulComponentBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GhPutComponents"/> class.
        /// </summary>
        public GhPutComponents()
            : base("Place GhJSON", "GhPut", "Convert GhJSON to a Grasshopper components on the canvas.\n\nNew components will be added at the bottom of the canvas.", "SmartHopper", "Grasshopper")
        {
            // Always run when Run is true, even if inputs haven't changed
            // This allows re-placing the same JSON multiple times if needed
            this.RunOnlyOnInputChanges = false;
        }

        /// <inheritdoc/>
        public override Guid ComponentGuid => new("25E07FD9-382C-48C0-8A97-8BFFAEAD8592");

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
                    toolCall.SkipMetricsValidation = true;

                    var aiResult = await toolCall.Exec().ConfigureAwait(false);

                    token.ThrowIfCancellationRequested();

                    // If the tool call itself failed, surface detailed error information
                    if (!aiResult.Success)
                    {
                        var parts = new List<string>();

                        if (aiResult.Messages != null)
                        {
                            parts.AddRange(
                                aiResult.Messages
                                    .Where(msg => !string.IsNullOrWhiteSpace(msg?.Message))
                                    .Select(msg => $"{msg.Severity}: {msg.Message}"));
                        }

                        var errorInteraction = aiResult.Body?.GetLastInteraction(AIAgent.Error) as AIInteractionError;
                        var errorPayload = errorInteraction?.Content;
                        if (!string.IsNullOrWhiteSpace(errorPayload))
                        {
                            parts.Add(errorPayload);
                        }

                        var combined = parts.Count > 0 ? string.Join(" \n", parts) : "gh_put execution failed";
                        Debug.WriteLine($"[GhPutComponents] gh_put Exec failed: {combined}");
                        this.error = combined;
                        return;
                    }

                    // Success path: read tool result payload from gh_put
                    var toolResultInteraction = aiResult.Body?.GetLastInteraction() as AIInteractionToolResult;
                    var toolResult = toolResultInteraction?.Result;

                    this.analysis = toolResult?["analysis"]?.ToString();
                    this.componentNames = toolResult?["components"]?.ToObject<List<string>>() ?? new List<string>();
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
                            var msgText = trimmed.Substring(2);
                            Debug.WriteLine($"[GhPutComponents] RuntimeMessage from analysis: Level={currentLevel}, Message={msgText}");
                            this.AddRuntimeMessage(currentLevel, msgText);
                        }
                    }
                }

                if (!string.IsNullOrEmpty(this.error))
                {
                    Debug.WriteLine($"[GhPutComponents] RuntimeMessage error: Level={GH_RuntimeMessageLevel.Error}, Message={this.error}");
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
