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
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Newtonsoft.Json.Linq;
using SmartHopper.Components.Properties;
using SmartHopper.Core.ComponentBase;
using SmartHopper.Infrastructure.AIModels;

namespace SmartHopper.Components.Script
{
    /// <summary>
    /// Grasshopper component that creates or edits script components using AI tools.
    /// Uses script_generate for new scripts and script_edit for existing ones.
    /// Outputs GhJSON that can be placed on canvas using GhPutComponents.
    /// </summary>
    public class AIScriptGeneratorComponent : AISelectingStatefulAsyncComponentBase
    {
        /// <inheritdoc/>
        public override Guid ComponentGuid => new Guid("B9E66C95-2A7D-46AB-9E37-7C28F8202F4A");

        /// <inheritdoc/>
        protected override Bitmap Icon => Resources.scriptgenerate;

        /// <inheritdoc/>
        public override GH_Exposure Exposure => GH_Exposure.primary;

        /// <inheritdoc/>
        protected override AICapability RequiredCapability => AICapability.Text2Json;

        /// <summary>
        /// Initializes a new instance of the <see cref="AIScriptGeneratorComponent"/> class.
        /// </summary>
        public AIScriptGeneratorComponent()
            : base(
                  "AI Script Generator",
                  "AIScriptGen",
                  "Create or edit Grasshopper script components from natural language instructions. Outputs GhJSON that can be placed on canvas using GH Put.",
                  "SmartHopper",
                  "Script")
        {
            this.RunOnlyOnInputChanges = false;
        }

        /// <inheritdoc/>
        protected override void RegisterAdditionalInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Prompt", "P", "REQUIRED instructions describing how to create or edit the script.", GH_ParamAccess.item);
        }

        /// <inheritdoc/>
        protected override void RegisterAdditionalOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("GhJSON", "J", "GhJSON representation of the script component. Use GH Put to place on canvas.", GH_ParamAccess.item);
            pManager.AddTextParameter("Summary", "Sm", "Brief summary of what changed (edit mode).", GH_ParamAccess.item);
            pManager.AddTextParameter("Information", "I", "Informational message from the operation.", GH_ParamAccess.item);
        }

        /// <inheritdoc/>
        protected override AsyncWorkerBase CreateWorker(Action<string> progressReporter)
        {
            return new AIScriptGeneratorWorker(this, this.AddRuntimeMessage);
        }

        /// <summary>
        /// Worker class that orchestrates script generation/editing using AI tools.
        /// </summary>
        private sealed class AIScriptGeneratorWorker : AsyncWorkerBase
        {
            private readonly AIScriptGeneratorComponent parent;
            private string guid;
            private string prompt;
            private bool hasWork;
            private bool isEditMode;
            private readonly Dictionary<string, GH_String> result = new Dictionary<string, GH_String>();

            public AIScriptGeneratorWorker(
                AIScriptGeneratorComponent parent,
                Action<GH_RuntimeMessageLevel, string> addRuntimeMessage)
                : base(parent, addRuntimeMessage)
            {
                this.parent = parent;
            }

            /// <inheritdoc/>
            public override void GatherInput(IGH_DataAccess DA, out int dataCount)
            {
                string localPrompt = null;
                DA.GetData("Prompt", ref localPrompt);

                // Get GUID from selection (via selecting button)
                var first = this.parent.SelectedObjects.FirstOrDefault();
                this.guid = first != null ? first.InstanceGuid.ToString() : string.Empty;

                this.prompt = localPrompt ?? string.Empty;
                this.isEditMode = !string.IsNullOrWhiteSpace(this.guid);

                this.hasWork = !string.IsNullOrWhiteSpace(this.prompt);
                if (!this.hasWork)
                {
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Prompt is required.");
                }

                dataCount = this.hasWork ? 1 : 0;
            }

            /// <inheritdoc/>
            public override async Task DoWorkAsync(CancellationToken token)
            {
                if (!this.hasWork)
                {
                    return;
                }

                try
                {
                    JObject scriptToolResult;

                    if (this.isEditMode)
                    {
                        // Edit mode: get existing component GhJSON, then edit it
                        scriptToolResult = await this.EditExistingScriptAsync(token).ConfigureAwait(false);
                    }
                    else
                    {
                        // Create mode: generate new script component
                        scriptToolResult = await this.GenerateNewScriptAsync(token).ConfigureAwait(false);
                    }

                    if (scriptToolResult == null)
                    {
                        return; // Error already set
                    }

                    // Set outputs from tool result
                    this.SetResultsFromToolOutput(scriptToolResult);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[AIScriptGeneratorWorker] Error: {ex.Message}");
                    this.result["Message"] = new GH_String($"Error: {ex.Message}");
                }
            }

            /// <summary>
            /// Generates a new script component using script_generate tool.
            /// </summary>
            private async Task<JObject> GenerateNewScriptAsync(CancellationToken token)
            {
                Debug.WriteLine("[AIScriptGeneratorWorker] Create mode: calling script_generate");

                var parameters = new JObject
                {
                    ["instructions"] = this.prompt,
                    ["contextFilter"] = "-*",
                };

                var toolResult = await this.parent.CallAiToolAsync("script_generate", parameters).ConfigureAwait(false);

                if (!this.ValidateToolResult(toolResult, "script_generate"))
                {
                    return null;
                }

                return toolResult;
            }

            /// <summary>
            /// Edits an existing script component using gh_get + script_edit tools.
            /// </summary>
            private async Task<JObject> EditExistingScriptAsync(CancellationToken token)
            {
                Debug.WriteLine($"[AIScriptGeneratorWorker] Edit mode: getting GhJSON for {this.guid}");

                // Step 1: Get existing component GhJSON using gh_get with guidFilter
                var getParams = new JObject
                {
                    ["guidFilter"] = new JArray(this.guid),
                    ["contextFilter"] = "-*",
                };

                var getResult = await this.parent.CallAiToolAsync("gh_get", getParams).ConfigureAwait(false);

                if (!this.ValidateToolResult(getResult, "gh_get"))
                {
                    return null;
                }

                // gh_get returns GhJSON in the "ghjson" field
                var existingGhJson = getResult["ghjson"]?.ToString();
                if (string.IsNullOrWhiteSpace(existingGhJson))
                {
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Could not retrieve GhJSON for component {this.guid}");
                    this.result["Message"] = new GH_String("Failed to retrieve existing component.");
                    return null;
                }

                Debug.WriteLine($"[AIScriptGeneratorWorker] Retrieved GhJSON, length: {existingGhJson.Length}");

                // Step 2: Edit the script using script_edit
                var editParams = new JObject
                {
                    ["ghjson"] = existingGhJson,
                    ["instructions"] = this.prompt,
                    ["contextFilter"] = "-*",
                };

                var editResult = await this.parent.CallAiToolAsync("script_edit", editParams).ConfigureAwait(false);

                if (!this.ValidateToolResult(editResult, "script_edit"))
                {
                    return null;
                }

                return editResult;
            }

            /// <summary>
            /// Validates a tool result and surfaces any errors.
            /// </summary>
            private bool ValidateToolResult(JObject toolResult, string toolName)
            {
                if (toolResult == null)
                {
                    this.result["Message"] = new GH_String($"Tool '{toolName}' returned no result.");
                    return false;
                }

                var hasErrors = toolResult["messages"] is JArray messages && messages.Any(m => m["severity"]?.ToString() == "Error");
                if (hasErrors)
                {
                    foreach (var msg in (JArray)toolResult["messages"])
                    {
                        if (msg["severity"]?.ToString() == "Error")
                        {
                            var text = msg["message"]?.ToString();
                            if (!string.IsNullOrWhiteSpace(text))
                            {
                                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, text);
                            }
                        }
                    }

                    this.result["Message"] = new GH_String($"Tool '{toolName}' failed. See runtime errors.");
                    return false;
                }

                return true;
            }

            /// <summary>
            /// Sets result dictionary from tool output.
            /// </summary>
            private void SetResultsFromToolOutput(JObject toolResult)
            {
                // GhJSON
                var ghJson = toolResult["ghjson"]?.ToString();
                if (!string.IsNullOrEmpty(ghJson))
                {
                    this.result["GhJSON"] = new GH_String(ghJson);
                }
                else
                {
                    this.result["Message"] = new GH_String("Script tool returned no GhJSON.");
                    return;
                }

                // Summary (edit mode)
                var changesSummary = toolResult["changesSummary"]?.ToString();
                if (!string.IsNullOrEmpty(changesSummary))
                {
                    this.result["Summary"] = new GH_String(changesSummary);
                }

                // Message
                var message = toolResult["message"]?.ToString() ?? (this.isEditMode ? "Script edited successfully." : "Script generated successfully.");
                this.result["Message"] = new GH_String(message);
            }

            /// <inheritdoc/>
            public override void SetOutput(IGH_DataAccess DA, out string message)
            {
                if (this.result.TryGetValue("GhJSON", out GH_String ghJsonValue))
                {
                    this.parent.SetPersistentOutput("GhJSON", ghJsonValue, DA);
                }

                if (this.result.TryGetValue("Summary", out GH_String summaryValue))
                {
                    this.parent.SetPersistentOutput("Summary", summaryValue, DA);
                }

                if (this.result.TryGetValue("Information", out GH_String msgValue))
                {
                    this.parent.SetPersistentOutput("Information", msgValue, DA);
                    message = msgValue.Value;
                }
                else
                {
                    message = "No script operation performed";
                }
            }
        }
    }
}
