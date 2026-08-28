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
using SmartHopper.Infrastructure.AIProviders;
using SmartHopper.ProviderSdk.AICall.Core.Base;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AIModels;
using SmartHopper.ProviderSdk.AIProviders;
using SmartHopper.ProviderSdk.Diagnostics;

namespace SmartHopper.Components.Text
{
    public class AIText2TextComponent : AIStatefulAsyncComponentBase
    {
        public override Guid ComponentGuid => new ("EB073C7A-A500-4265-A45B-B1BFB38BA58E");

        protected override Bitmap Icon => Resources.textgenerate;

        public override GH_Exposure Exposure => GH_Exposure.primary;

        /// <inheritdoc/>
        public override IEnumerable<string> Keywords => new[] {
            "AI Text Generate",
            "AITextGenerate",
            "text2text",
            "Text Generate",
            "Text Generator",
            "Text Create",
            "Text Creator",
            "Text Modify",
            "Text Alter",
            "Text Edit",
            "AI Write",
            "AI Writer",
            "AI Compose",
            "Generate Text",
            "Write Text",
            "Create Text",
            "Modify Text",
            "Rephrase",
            "Paraphrase",
            "AI Response",
            "LLM Text",
        };

        /// <inheritdoc/>
        protected override IReadOnlyList<string> UsingAiTools => new[] { "text2text" };

        public AIText2TextComponent()
            : base(
                "AI Text To Text",
                "AIText2Text",
                "Generate text from natural language instructions. You can also use this component to modify or rephrase a text.\n\nIf a tree structure is provided, prompts and instructions will only match within the same branch paths.",
                "SmartHopper",
                "Text")
        {
        }

        /// <inheritdoc/>
        protected override void OnBatchCompleted(IReadOnlyDictionary<string, JObject> results, IReadOnlyList<SHRuntimeMessage> messages = null)
        {
            var sentinel = this.GetSentinelTree("Result");
            if (results == null || sentinel == null) return;

            // ProcessBatchResults automatically persists outputs and sets metrics
            this.ProcessBatchResults<GH_String>(
                "Result",
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
                },
                messages);
        }

        protected override void RegisterAdditionalInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Prompt", "P", "REQUIRED The user's prompt", GH_ParamAccess.tree);
            pManager.AddTextParameter("Instructions", "I", "Optionally, specify what the AI should do when receiving the prompt", GH_ParamAccess.tree, string.Empty);
        }

        protected override void RegisterAdditionalOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Result", "R", "The AI's response", GH_ParamAccess.tree);
        }

        protected override AsyncWorkerBase CreateWorker(Action<string> progressReporter)
        {
            return new AIText2TextWorker(this, this.AddRuntimeMessage, this.ComponentProcessingOptions);
        }

        private sealed class AIText2TextWorker : AsyncWorkerBase
        {
            private Dictionary<string, GH_Structure<GH_String>> inputTree;
            private Dictionary<string, GH_Structure<GH_String>> result;
            private readonly AIText2TextComponent parent;
            private readonly ProcessingOptions processingOptions;

            public AIText2TextWorker(
                AIText2TextComponent parent,
                Action<GH_RuntimeMessageLevel, string> addRuntimeMessage,
                ProcessingOptions processingOptions)
                : base(parent, addRuntimeMessage)
            {
                this.parent = parent;
                this.processingOptions = processingOptions;
                this.result = new Dictionary<string, GH_Structure<GH_String>>
                {
                    { "Result", new GH_Structure<GH_String>() },
                };
            }

            public override void GatherInput(IGH_DataAccess DA, out int dataCount)
            {
                this.inputTree = new Dictionary<string, GH_Structure<GH_String>>();

                // Get the input trees
                var promptTree = new GH_Structure<GH_String>();
                var instructionsTree = new GH_Structure<GH_String>();

                DA.GetDataTree("Prompt", out promptTree);
                DA.GetDataTree("Instructions", out instructionsTree);

                // The first defined tree is the one that overrides paths in case they don't match between trees
                this.inputTree["Prompt"] = promptTree;
                this.inputTree["Instructions"] = instructionsTree;

                dataCount = 0;
            }

            public override async Task DoWorkAsync(CancellationToken token)
            {
                try
                {
                    Debug.WriteLine($"[Worker] Starting DoWorkAsync");
                    Debug.WriteLine($"[Worker] Input tree keys: {string.Join(", ", this.inputTree.Keys)}");
                    Debug.WriteLine($"[Worker] Input tree data counts: {string.Join(", ", this.inputTree.Select(kvp => $"{kvp.Key}: {kvp.Value.DataCount}"))}");

                    this.result = await this.parent.RunProcessingAsync(
                        this.inputTree,
                        async (branches) =>
                        {
                            Debug.WriteLine($"[Worker] ProcessData called with {branches.Count} branches");
                            return await ProcessData(branches, this.parent, token).ConfigureAwait(false);
                        },
                        this.processingOptions,
                        token).ConfigureAwait(false);

                    // After all items processed: if batch mode, submit the collected queue
                    var batchSubmitted = await this.parent.TrySubmitBatchAsync("Result", this.result, token).ConfigureAwait(false);
                    if (batchSubmitted)
                    {
                        Debug.WriteLine($"[Worker] Sentinel tree stored, batch submitted");
                    }
                    else if (this.result.TryGetValue("Result", out var resultTree))
                    {
                        // Non-batch: persist output and emit metrics via FinishResults
                        this.parent.FinishResults("Result", resultTree);
                    }

                    Debug.WriteLine($"[Worker] Finished DoWorkAsync - Result keys: {string.Join(", ", this.result.Keys)}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Worker] Error: {ex.Message}");
                    this.CollectMessage(SHRuntimeMessageSeverity.Error, ex.Message);
                }
            }

            private static async Task<Dictionary<string, List<GH_String>>> ProcessData(Dictionary<string, List<GH_String>> branches, AIText2TextComponent parent, CancellationToken cancellationToken)
            {
                /*
                 * Inputs will be available as a dictionary
                 * of branches. No need to deal with paths.
                 *
                 * Outputs should be a dictionary where keys
                 * are each output parameter, and values are
                 * the output values.
                 */

                Debug.WriteLine($"[Worker] Processing {branches.Count} trees");
                Debug.WriteLine($"[Worker] Items per tree: {branches.Values.Max(branch => branch.Count)}");

                // Get the trees
                var promptTree = branches["Prompt"];
                var instructionsTree = branches["Instructions"];

                // Normalize tree lengths
                var normalizedLists = DataTreeProcessor.NormalizeBranchLengths(new List<List<GH_String>> { promptTree, instructionsTree });

                // Reassign normalized branches
                promptTree = normalizedLists[0];
                instructionsTree = normalizedLists[1];

                Debug.WriteLine($"[ProcessData] After normalization - Prompts count: {promptTree.Count}, Instructions count: {instructionsTree.Count}");

                // Initialize the output
                var outputs = new Dictionary<string, List<GH_String>>();
                outputs["Result"] = new List<GH_String>();

                // Iterate over the branches
                // For each item in the prompt tree, get the response from AI
                int i = 0;
                foreach (var prompt in promptTree)
                {
                    Debug.WriteLine($"[ProcessData] Processing prompt {i + 1}/{promptTree.Count}");

                    // Call the AI tool through the tool manager
                    var parameters = new JObject
                    {
                        ["prompt"] = promptTree[i]?.Value,
                        ["instructions"] = instructionsTree[i]?.Value,
                        ["contextFilter"] = "-*",
                    };

                    var toolResult = await parent.CallAIToolAsync(
                        "text2text", parameters, cancellationToken)
                        .ConfigureAwait(false);

                    string result = toolResult?["result"]?.ToString() ?? string.Empty;
                    outputs["Result"].Add(new GH_String(result));
                    i++;
                }

                return outputs;
            }

            public override void SetOutput(IGH_DataAccess DA, out string message)
            {
                // Outputs and metrics are handled by FinishResults (non-batch) or
                // ProcessBatchResults → FinishResults (batch). RestorePersistentOutputs
                // replays them to the canvas on the next solve.
                message = string.Empty;
            }
        }
    }
}
