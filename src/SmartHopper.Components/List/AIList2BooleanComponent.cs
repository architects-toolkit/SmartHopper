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
using SmartHopper.Core.Grasshopper.Utils.Parsing;
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AIModels;
using SmartHopper.Infrastructure.AIProviders;

namespace SmartHopper.Components.List
{
    public class AIList2BooleanComponent : AIStatefulAsyncComponentBase
    {
        public override Guid ComponentGuid => new("A8BAD48D-8723-42AD-B13C-A875F940B69C");

        protected override Bitmap Icon => Resources.listevaluate;

        public override GH_Exposure Exposure => GH_Exposure.primary;

        public override IEnumerable<string> Keywords => new[] {
            "AI List Evaluate",
            "AIListEvaluate",
            "textlist2boolean",
            "List Question",
            "List True/False",
        };

        /// <inheritdoc/>
        protected override IReadOnlyList<string> UsingAiTools => new[] { "textlist2boolean" };

        protected override ProcessingOptions ComponentProcessingOptions => new ProcessingOptions
        {
            Topology = ProcessingTopology.BranchToBranch,
            OnlyMatchingPaths = false,
            GroupIdenticalBranches = true,
        };

        public AIList2BooleanComponent()
            : base("AI List To Boolean", "AIList2Boolean",
                  "Use natural language to evaluate a list and output a TRUE/FALSE answer.\nThis components takes the list as a whole. This means that every question will return True or False for each provided list (not for each individual items).\nIf a tree structure is provided, questions and lists will only match within the same branch paths.",
                  "SmartHopper", "List")
        {
        }

        protected override void RegisterAdditionalInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("List", "L", " REQUIRED List of items to evaluate", GH_ParamAccess.tree);
            pManager.AddTextParameter("Question", "Q", "REQUIRED True or false question. The AI will answer it based on the input list.", GH_ParamAccess.tree);
        }

        protected override void RegisterAdditionalOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddBooleanParameter("Result", "R", "Result of the evaluation", GH_ParamAccess.tree);
        }

        /// <inheritdoc/>
        protected override void OnBatchCompleted(IReadOnlyDictionary<string, JObject> results, IReadOnlyList<AIRuntimeMessage> messages = null)
        {
            var sentinel = this.GetSentinelTree("Result");
            if (results == null || sentinel == null) return;

            // ProcessBatchResults automatically persists outputs and sets metrics
            this.ProcessBatchResults<GH_Boolean>(
                "Result",
                sentinel,
                results,
                (customId, resultBody) =>
                {
                    var provider = ProviderManager.Instance.GetProvider(this.GetActualAIProviderName());
                    if (provider == null) return null;

                    var interactions = provider.Decode(resultBody);
                    var lastText = interactions
                        ?.OfType<AIInteractionText>()
                        .LastOrDefault(i => i.Agent == AIAgent.Assistant);

                    if (lastText == null) return null;
                    if (bool.TryParse(lastText.Content?.Trim(), out bool value))
                        return new GH_Boolean(value);
                    return null;
                },
                messages);
        }

        protected override AsyncWorkerBase CreateWorker(Action<string> progressReporter)
        {
            return new AIList2BooleanWorker(this, this.AddRuntimeMessage, ComponentProcessingOptions);
        }

        private sealed class AIList2BooleanWorker : AsyncWorkerBase
        {
            private Dictionary<string, GH_Structure<GH_String>> inputTree;
            private Dictionary<string, GH_Structure<GH_String>> stringResult;
            private readonly AIList2BooleanComponent parent;
            private readonly ProcessingOptions processingOptions;

            public AIList2BooleanWorker(
            AIList2BooleanComponent parent,
            Action<GH_RuntimeMessageLevel, string> addRuntimeMessage,
            ProcessingOptions processingOptions)
            : base(parent, addRuntimeMessage)
            {
                this.parent = parent;
                this.processingOptions = processingOptions;
                this.stringResult = new Dictionary<string, GH_Structure<GH_String>>
                {
                    { "Result", new GH_Structure<GH_String>() },
                };
            }

            public override void GatherInput(IGH_DataAccess DA, out int dataCount)
            {
                this.inputTree = new Dictionary<string, GH_Structure<GH_String>>();

                // Get the input trees
                var listTree = new GH_Structure<IGH_Goo>();
                var questionTree = new GH_Structure<GH_String>();

                DA.GetDataTree("List", out listTree);
                DA.GetDataTree("Question", out questionTree);

                // Convert generic data to string structure
                var stringListTree = ConvertToGHString(listTree);

                // Store the converted trees
                this.inputTree["List"] = stringListTree;
                this.inputTree["Question"] = questionTree;

                dataCount = 0;
            }

            public override async Task DoWorkAsync(CancellationToken token)
            {
                try
                {
                    Debug.WriteLine($"[Worker] Starting DoWorkAsync");
                    Debug.WriteLine($"[Worker] Input tree keys: {string.Join(", ", this.inputTree.Keys)}");
                    Debug.WriteLine($"[Worker] Input tree data counts: {string.Join(", ", this.inputTree.Select(kvp => $"{kvp.Key}: {kvp.Value.DataCount}"))}");

                    this.stringResult = await this.parent.RunProcessingAsync(
                        this.inputTree,
                        async (branches) =>
                        {
                            Debug.WriteLine($"[Worker] ProcessData called with {branches.Count} branches");
                            return await ProcessData(branches, this.parent).ConfigureAwait(false);
                        },
                        this.processingOptions,
                        token).ConfigureAwait(false);

                    var batchSubmitted = await this.parent.TrySubmitBatchAsync("Result", this.stringResult, token).ConfigureAwait(false);
                    if (batchSubmitted)
                    {
                        Debug.WriteLine($"[Worker] Sentinel tree stored, batch submitted");
                    }
                    else if (this.stringResult.TryGetValue("Result", out var resultTree))
                    {
                        // Non-batch: convert strings to booleans and persist via FinishResults
                        var boolTree = ConvertStringTreeToBoolean(resultTree);
                        this.parent.FinishResults("Result", boolTree);
                    }

                    Debug.WriteLine($"[Worker] Finished DoWorkAsync - Result keys: {string.Join(", ", this.stringResult.Keys)}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Worker] Error: {ex.Message}");
                }
            }

            /// <summary>
            /// Converts a string tree to a boolean tree, parsing "true"/"false" strings.
            /// </summary>
            private static GH_Structure<GH_Boolean> ConvertStringTreeToBoolean(GH_Structure<GH_String> stringTree)
            {
                var boolTree = new GH_Structure<GH_Boolean>();
                if (stringTree == null) return boolTree;

                foreach (var path in stringTree.Paths)
                {
                    var branch = stringTree.get_Branch(path);
                    var boolBranch = new List<GH_Boolean>();
                    foreach (GH_String item in branch)
                    {
                        var str = item?.Value;
                        if (bool.TryParse(str, out bool val))
                            boolBranch.Add(new GH_Boolean(val));
                        else
                            boolBranch.Add(null);
                    }
                    boolTree.AppendRange(boolBranch, path);
                }
                return boolTree;
            }

            private static async Task<Dictionary<string, List<GH_String>>> ProcessData(Dictionary<string, List<GH_String>> branches, AIList2BooleanComponent parent)
            {
                /*
                 * Inputs will be available as a dictionary
                 * of branches. No need to deal with paths.
                 *
                 * Outputs should be a dictionary where keys
                 * are each output parameter, and values are
                 * the output values (as strings for batch support).
                 */

                Debug.WriteLine($"[Worker] Processing {branches.Count} trees");
                Debug.WriteLine($"[Worker] Items per tree: {branches.Values.Max(branch => branch.Count)}");

                // Get the trees
                var listAsJson = AIResponseParser.ConcatenateItemsToJson(branches["List"], "array");
                var questionTree = branches["Question"];

                // Normalize tree lengths
                var normalizedLists = DataTreeProcessor.NormalizeBranchLengths(
                    new List<List<GH_String>>
                    {
                        new (new GH_String[] { new (listAsJson.ToString()) }),
                        questionTree,
                    });

                // Reassign normalized branches
                var normalizedListTree = normalizedLists[0];
                questionTree = normalizedLists[1];

                Debug.WriteLine($"[ProcessData] After normalization - Questions count: {questionTree.Count}, List count: {normalizedListTree.Count}");

                // Initialize the output (as strings for batch support - sentinels are strings)
                var outputs = new Dictionary<string, List<GH_String>>();
                outputs["Result"] = new List<GH_String>();

                // Iterate over the branches
                // For each item in the prompt tree, get the response from AI
                int i = 0;
                foreach (var question in questionTree)
                {
                    Debug.WriteLine($"[ProcessData] Processing prompt {i + 1}/{questionTree.Count}");

                    // Call the AI tool through the tool manager
                    var parameters = new JObject
                    {
                        ["list"] = JArray.Parse(normalizedListTree[i].Value),
                        ["question"] = question.Value,
                        ["contextFilter"] = "-*",
                    };

                    var toolResult = await parent.CallAiToolAsync(
                        "textlist2boolean", parameters)
                        .ConfigureAwait(false);

                    if (toolResult == null)
                    {
                        outputs["Result"].Add(new GH_String(string.Empty));
                        i++;
                        continue;
                    }

                    // In batch mode, CallAiToolAsync returns a sentinel placeholder under "result".
                    // Forward it so ReconstructOutputTree can replace it after the batch completes.
                    var resultValue = toolResult["result"]?.ToString();
                    if (resultValue != null && resultValue.StartsWith("##SH_BATCH:", StringComparison.Ordinal))
                    {
                        outputs["Result"].Add(new GH_String(resultValue));
                        i++;
                        continue;
                    }

                    bool result = toolResult["result"]?.ToObject<bool>() ?? false;
                    outputs["Result"].Add(new GH_String(result.ToString()));

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
