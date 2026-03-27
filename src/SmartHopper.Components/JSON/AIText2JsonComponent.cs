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
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AIModels;
using SmartHopper.Infrastructure.AIProviders;

namespace SmartHopper.Components.JSON
{
    /// <summary>
    /// Grasshopper component that generates structured JSON from a prompt using AI.
    /// </summary>
    public class AIText2JsonComponent : AIStatefulAsyncComponentBase
    {
        /// <inheritdoc/>
        public override Guid ComponentGuid => new Guid("D29CA935-C91C-48A4-976A-25CDFF8A4F87");

        /// <inheritdoc/>
        protected override Bitmap Icon => Resources.textgenerate;

        /// <inheritdoc/>
        public override GH_Exposure Exposure => GH_Exposure.primary;

        /// <inheritdoc/>
        public override IEnumerable<string> Keywords => new[] {
            "AIText2Json",
            "AI Text to JSON",
            "Text to JSON",
            "text2json",
            "JSON Generate",
            "JSON Creator",
        };

        /// <inheritdoc/>
        protected override IReadOnlyList<string> UsingAiTools => new[] { "text2json" };

        /// <inheritdoc/>
        protected override ProcessingOptions ComponentProcessingOptions => new ProcessingOptions
        {
            Topology = ProcessingTopology.ItemGraft,
            OnlyMatchingPaths = false,
            GroupIdenticalBranches = true,
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="AIText2JsonComponent"/> class.
        /// </summary>
        public AIText2JsonComponent()
            : base(
                  "AI Text To JSON",
                  "AIText2Json",
                  "Generate structured JSON from a prompt using AI, conforming to a provided JSON Schema.\nIf a tree structure is provided, prompts and schemas will only match within the same branch paths.",
                  "SmartHopper", "JSON")
        {
        }

        /// <inheritdoc/>
        protected override void RegisterAdditionalInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Prompt", "P", "REQUIRED The user's prompt describing the JSON data to generate", GH_ParamAccess.tree);
            pManager.AddTextParameter("Instructions", "I", "Optional custom system prompt override", GH_ParamAccess.tree, string.Empty);
            pManager.AddTextParameter("Schema", "S", "REQUIRED JSON Schema string the output must conform to. Use JsonSchemaComponent to build one.", GH_ParamAccess.tree);
        }

        /// <inheritdoc/>
        protected override void RegisterAdditionalOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("JSON", "J", "Generated JSON string conforming to the provided schema", GH_ParamAccess.tree);
        }

        /// <inheritdoc/>
        protected override void OnBatchCompleted(IReadOnlyDictionary<string, JObject> results)
        {
            var sentinel = this.GetSentinelTree("JSON");
            if (results == null || sentinel == null) return;

            var reconstructed = this.ProcessBatchResults<GH_String>(
                "JSON",
                sentinel,
                results,
                (customId, resultBody) =>
                {
                    var provider = ProviderManager.Instance.GetProvider(this.GetActualAIProviderName());
                    if (provider == null) return new GH_String(string.Empty);

                    var interactions = provider.Decode(resultBody);
                    var lastText = interactions
                        ?.OfType<AIInteractionText>()
                        .LastOrDefault(i => i.Agent == AIAgent.Assistant);

                    return new GH_String(lastText?.Content ?? string.Empty);
                });

            this.StoreReconstructedTree("JSON", reconstructed);
        }

        /// <inheritdoc/>
        protected override AsyncWorkerBase CreateWorker(Action<string> progressReporter)
        {
            return new AIText2JsonWorker(this, this.AddRuntimeMessage, ComponentProcessingOptions);
        }

        private sealed class AIText2JsonWorker : AsyncWorkerBase
        {
            private Dictionary<string, GH_Structure<GH_String>> inputTree;
            private Dictionary<string, GH_Structure<GH_String>> result;
            private readonly AIText2JsonComponent parent;
            private readonly ProcessingOptions processingOptions;

            public AIText2JsonWorker(
                AIText2JsonComponent parent,
                Action<GH_RuntimeMessageLevel, string> addRuntimeMessage,
                ProcessingOptions processingOptions)
                : base(parent, addRuntimeMessage)
            {
                this.parent = parent;
                this.processingOptions = processingOptions;
                this.result = new Dictionary<string, GH_Structure<GH_String>>
                {
                    { "JSON", new GH_Structure<GH_String>() },
                };
            }

            /// <inheritdoc/>
            public override void GatherInput(IGH_DataAccess DA, out int dataCount)
            {
                this.inputTree = new Dictionary<string, GH_Structure<GH_String>>();

                var promptTree = new GH_Structure<GH_String>();
                var instructionsTree = new GH_Structure<GH_String>();
                var schemaTree = new GH_Structure<GH_String>();

                DA.GetDataTree("Prompt", out promptTree);
                DA.GetDataTree("Instructions", out instructionsTree);
                DA.GetDataTree("Schema", out schemaTree);

                this.inputTree["Prompt"] = promptTree;
                this.inputTree["Instructions"] = instructionsTree;
                this.inputTree["Schema"] = schemaTree;

                dataCount = 0;
            }

            /// <inheritdoc/>
            public override async Task DoWorkAsync(CancellationToken token)
            {
                try
                {
                    Debug.WriteLine("[AIText2Json] Starting DoWorkAsync");

                    this.result = await this.parent.RunProcessingAsync(
                        this.inputTree,
                        async (branches) =>
                        {
                            Debug.WriteLine($"[AIText2Json] ProcessData called with {branches.Count} branches");
                            return await ProcessData(branches, this.parent).ConfigureAwait(false);
                        },
                        this.processingOptions,
                        token).ConfigureAwait(false);

                    // After all items processed: if batch mode, submit the collected queue
                    var batchSubmitted = await this.parent.TrySubmitBatchAsync("JSON", this.result, token).ConfigureAwait(false);
                    if (batchSubmitted)
                    {
                        Debug.WriteLine("[AIText2Json] Sentinel tree stored, batch submitted");
                    }

                    Debug.WriteLine($"[AIText2Json] Finished DoWorkAsync - Result keys: {string.Join(", ", this.result.Keys)}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[AIText2Json] Error: {ex.Message}");
                }
            }

            private static async Task<Dictionary<string, List<GH_String>>> ProcessData(
                Dictionary<string, List<GH_String>> branches,
                AIText2JsonComponent parent)
            {
                var promptList = branches["Prompt"];
                var instructionsList = branches["Instructions"];
                var schemaList = branches["Schema"];

                var normalized = DataTreeProcessor.NormalizeBranchLengths(
                    new List<List<GH_String>> { promptList, instructionsList, schemaList });

                var normalizedPrompts = normalized[0];
                var normalizedInstructions = normalized[1];
                var normalizedSchemas = normalized[2];

                var outputs = new Dictionary<string, List<GH_String>>
                {
                    { "JSON", new List<GH_String>() },
                };

                for (int i = 0; i < normalizedPrompts.Count; i++)
                {
                    string prompt = normalizedPrompts[i]?.Value ?? string.Empty;
                    string instructions = normalizedInstructions[i]?.Value ?? string.Empty;
                    string schema = normalizedSchemas[i]?.Value ?? string.Empty;

                    if (string.IsNullOrWhiteSpace(prompt))
                    {
                        Debug.WriteLine($"[AIText2Json] Skipping empty prompt at index {i}");
                        outputs["JSON"].Add(new GH_String(string.Empty));
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(schema))
                    {
                        Debug.WriteLine($"[AIText2Json] Skipping empty schema at index {i}");
                        outputs["JSON"].Add(new GH_String(string.Empty));
                        continue;
                    }

                    var parameters = new JObject
                    {
                        ["prompt"] = prompt,
                        ["jsonSchema"] = schema,
                        ["contextFilter"] = "-*",
                    };

                    if (!string.IsNullOrWhiteSpace(instructions))
                    {
                        parameters["instructions"] = instructions;
                    }

                    var toolResult = await parent.CallAiToolAsync(
                        "text2json", parameters).ConfigureAwait(false);

                    string json = toolResult?["json"]?.ToString() ?? string.Empty;
                    outputs["JSON"].Add(new GH_String(json));
                }

                return outputs;
            }

            /// <inheritdoc/>
            public override void SetOutput(IGH_DataAccess DA, out string message)
            {
                // If batch completed and tree was reconstructed, use that
                var reconstructed = this.parent.PopReconstructedTree<GH_String>("JSON");
                if (reconstructed != null)
                {
                    this.parent.SetPersistentOutput("JSON", reconstructed, DA);
                    message = string.Empty;
                    return;
                }

                if (!this.result.TryGetValue("JSON", out GH_Structure<GH_String>? tree))
                {
                    message = "Error: No result available";
                    return;
                }

                this.parent.SetPersistentOutput("JSON", tree, DA);
                message = string.Empty;
            }
        }
    }
}
