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
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Newtonsoft.Json.Linq;
using SmartHopper.Components.Properties;
using SmartHopper.Core.ComponentBase;
using SmartHopper.Core.DataTree;

namespace SmartHopper.Components.Script
{
    /// <summary>
    /// Grasshopper component that creates or edits script components using AI tools.
    /// Uses script_generate for new scripts and script_edit for existing ones.
    /// Outputs GhJSON that can be placed on canvas using GhPutComponents.
    /// Supports multiple prompts (create mode) or multiple selected components (edit mode).
    /// In edit mode, prompts and components are matched first-first, second-second, etc.
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
        protected override IReadOnlyList<string> UsingAiTools => new[] { "script_generate", "script_edit", "gh_get" };

        /// <summary>
        /// Initializes a new instance of the <see cref="AIScriptGeneratorComponent"/> class.
        /// </summary>
        public AIScriptGeneratorComponent()
            : base(
                  "AI Script Generator",
                  "AIScriptGen",
                  "Create or edit Grasshopper script components from natural language instructions. Outputs GhJSON that can be placed on canvas using GH Put.\nIn create mode: processes each prompt as a separate branch.\nIn edit mode: matches prompts to selected components in order.",
                  "SmartHopper",
                  "Script")
        {
            this.RunOnlyOnInputChanges = false;
        }

        /// <inheritdoc/>
        protected override void RegisterAdditionalInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Prompt", "P", "REQUIRED instructions describing how to create or edit the script. In edit mode, prompts are matched to selected components in order.", GH_ParamAccess.list);
        }

        /// <inheritdoc/>
        protected override void RegisterAdditionalOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("GhJSON", "J", "GhJSON representation of the script component. Use GH Put to place on canvas. One branch per input.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Summary", "Sm", "Brief summary of the generated script and design decisions. One branch per input.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Information", "I", "Informational message from the operation. One branch per input.", GH_ParamAccess.tree);
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
            private List<GH_String> normalizedGuids;
            private List<GH_String> normalizedPrompts;
            private bool hasWork;
            private bool isEditMode;
            private int iterationCount;
            private readonly GH_Structure<GH_String> resultGhJson = new GH_Structure<GH_String>();
            private readonly GH_Structure<GH_String> resultSummary = new GH_Structure<GH_String>();
            private readonly GH_Structure<GH_String> resultInfo = new GH_Structure<GH_String>();

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
                var prompts = new List<string>();
                DA.GetDataList("Prompt", prompts);

                // Get GUIDs from selection (via selecting button)
                var guids = this.parent.SelectedObjects
                    .Select(obj => obj.InstanceGuid.ToString())
                    .ToList();

                this.isEditMode = guids.Count > 0;

                // Validate inputs
                prompts = prompts.Where(p => !string.IsNullOrWhiteSpace(p)).ToList();

                if (prompts.Count == 0)
                {
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "At least one prompt is required.");
                    this.hasWork = false;
                    dataCount = 0;
                    return;
                }

                // Convert to GH_String for normalization
                var promptsAsGhString = prompts.Select(p => new GH_String(p)).ToList();
                var guidsAsGhString = guids.Select(g => new GH_String(g)).ToList();

                if (this.isEditMode)
                {
                    // Edit mode: normalize lengths to match prompts to components
                    // Uses DataTreeProcessor.NormalizeBranchLengths for first-first, second-second matching
                    var normalized = DataTreeProcessor.NormalizeBranchLengths(
                        new List<List<GH_String>> { promptsAsGhString, guidsAsGhString });

                    this.normalizedPrompts = normalized[0];
                    this.normalizedGuids = normalized[1];
                    this.iterationCount = this.normalizedPrompts.Count;
                }
                else
                {
                    // Create mode: iterate over prompts only
                    this.normalizedPrompts = promptsAsGhString;
                    this.normalizedGuids = new List<GH_String>();
                    this.iterationCount = this.normalizedPrompts.Count;
                }

                this.hasWork = true;
                dataCount = this.iterationCount;

                // Initialize progress tracking
                this.parent.InitializeProgress(this.iterationCount);
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
                    for (int i = 0; i < this.iterationCount; i++)
                    {
                        if (token.IsCancellationRequested)
                        {
                            break;
                        }

                        // Update progress (1-based)
                        this.parent.UpdateProgress(i + 1);

                        var path = new GH_Path(i);
                        var prompt = this.normalizedPrompts[i]?.Value ?? string.Empty;

                        if (this.isEditMode)
                        {
                            var guid = this.normalizedGuids[i]?.Value ?? string.Empty;
                            Debug.WriteLine($"[AIScriptGeneratorWorker] Edit mode: processing {i + 1}/{this.iterationCount} (guid={guid})");

                            var result = await this.EditExistingScriptAsync(guid, prompt, token).ConfigureAwait(false);
                            this.StoreResult(path, result, isEdit: true);
                        }
                        else
                        {
                            Debug.WriteLine($"[AIScriptGeneratorWorker] Create mode: processing prompt {i + 1}/{this.iterationCount}");

                            var result = await this.GenerateNewScriptAsync(prompt, token).ConfigureAwait(false);
                            this.StoreResult(path, result, isEdit: false);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[AIScriptGeneratorWorker] Error: {ex.Message}");
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
                }
            }

            /// <summary>
            /// Stores the result from a tool call into the output trees.
            /// </summary>
            private void StoreResult(GH_Path path, JObject toolResult, bool isEdit)
            {
                if (toolResult == null)
                {
                    this.resultInfo.Append(new GH_String("Tool returned no result."), path);
                    return;
                }

                // Check for errors
                var hasErrors = toolResult["messages"] is JArray messages && messages.Any(m => m["severity"]?.ToString() == "Error");
                if (hasErrors)
                {
                    foreach (var text in ((JArray)toolResult["messages"])
                        .Where(msg => msg["severity"]?.ToString() == "Error")
                        .Select(msg => msg["message"]?.ToString())
                        .Where(text => !string.IsNullOrWhiteSpace(text)))
                    {
                        this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, text);
                    }

                    this.resultInfo.Append(new GH_String("Tool failed. See runtime errors."), path);
                    return;
                }

                // GhJSON
                var ghJson = toolResult["ghjson"]?.ToString();
                if (!string.IsNullOrEmpty(ghJson))
                {
                    this.resultGhJson.Append(new GH_String(ghJson), path);
                }
                else
                {
                    this.resultInfo.Append(new GH_String("Script tool returned no GhJSON."), path);
                    return;
                }

                // Summary
                var summary = toolResult["summary"]?.ToString() ?? toolResult["changesSummary"]?.ToString();
                if (!string.IsNullOrEmpty(summary))
                {
                    this.resultSummary.Append(new GH_String(summary), path);
                }

                // Message
                var message = toolResult["message"]?.ToString() ?? (isEdit ? "Script edited successfully." : "Script generated successfully.");
                this.resultInfo.Append(new GH_String(message), path);
            }

            /// <summary>
            /// Generates a new script component using script_generate tool.
            /// </summary>
            private async Task<JObject> GenerateNewScriptAsync(string prompt, CancellationToken token)
            {
                Debug.WriteLine("[AIScriptGeneratorWorker] Create mode: calling script_generate");

                var parameters = new JObject
                {
                    ["instructions"] = prompt,
                    ["contextFilter"] = "-*",
                };

                return await this.parent.CallAiToolAsync("script_generate", parameters).ConfigureAwait(false);
            }

            /// <summary>
            /// Edits an existing script component using gh_get + script_edit tools.
            /// </summary>
            private async Task<JObject> EditExistingScriptAsync(string guid, string prompt, CancellationToken token)
            {
                Debug.WriteLine($"[AIScriptGeneratorWorker] Edit mode: getting GhJSON for {guid}");

                // Step 1: Get existing component GhJSON using gh_get with guidFilter
                var getParams = new JObject
                {
                    ["guidFilter"] = new JArray(guid),
                    ["contextFilter"] = "-*",
                };

                var getResult = await this.parent.CallAiToolAsync("gh_get", getParams).ConfigureAwait(false);

                if (getResult == null)
                {
                    return new JObject
                    {
                        ["messages"] = new JArray(new JObject
                        {
                            ["severity"] = "Error",
                            ["message"] = $"gh_get returned no result for {guid}",
                        }),
                    };
                }

                // gh_get returns GhJSON in the "ghjson" field
                var existingGhJson = getResult["ghjson"]?.ToString();
                if (string.IsNullOrWhiteSpace(existingGhJson))
                {
                    return new JObject
                    {
                        ["messages"] = new JArray(new JObject
                        {
                            ["severity"] = "Error",
                            ["message"] = $"Could not retrieve GhJSON for component {guid}",
                        }),
                    };
                }

                Debug.WriteLine($"[AIScriptGeneratorWorker] Retrieved GhJSON, length: {existingGhJson.Length}");

                // Step 2: Edit the script using script_edit
                var editParams = new JObject
                {
                    ["ghjson"] = existingGhJson,
                    ["instructions"] = prompt,
                    ["contextFilter"] = "-*",
                };

                return await this.parent.CallAiToolAsync("script_edit", editParams).ConfigureAwait(false);
            }

            /// <inheritdoc/>
            public override void SetOutput(IGH_DataAccess DA, out string message)
            {
                this.parent.SetPersistentOutput("GhJSON", this.resultGhJson, DA);
                this.parent.SetPersistentOutput("Summary", this.resultSummary, DA);
                this.parent.SetPersistentOutput("Information", this.resultInfo, DA);

                message = this.isEditMode
                    ? $"Edited {this.iterationCount} script(s)"
                    : $"Generated {this.iterationCount} script(s)";
            }
        }
    }
}
