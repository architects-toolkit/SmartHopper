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
using SmartHopper.Core.Grasshopper.Converters;
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
            "AI List Question",
            "Ask List",
            "List Analysis",
            "Evaluate List",
            "Check List",
            "List Verify",
            "List Boolean",
            "List Yes No",
            "List Query",
        };

        /// <inheritdoc/>
        protected virtual IReadOnlyList<string> UsingAiTools => Array.Empty<string>();

        // Cache to store (value, usedFallback) per customId during batch processing
        private Dictionary<string, (GH_Boolean value, bool usedFallback)> _batchParseCache;

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
            pManager.AddBooleanParameter("Fallback", "F", "OPTIONAL fallback value to use when AI response cannot be parsed as true/false.\nIf not provided, the output will be null for unparsable responses", GH_ParamAccess.item);
            pManager[2].Optional = true;
        }

        protected override void RegisterAdditionalOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddBooleanParameter("Result", "R", "Result of the evaluation (true/false or fallback value)", GH_ParamAccess.tree);
            pManager.AddBooleanParameter("Used Fallback", "UF", "True if fallback was used because the AI response could not be parsed as true/false", GH_ParamAccess.tree);
        }

        /// <inheritdoc/>
        protected override void OnBatchCompleted(IReadOnlyDictionary<string, JObject> results, IReadOnlyList<AIRuntimeMessage> messages = null)
        {
            var sentinel = this.GetSentinelTree("Result");
            if (results == null || sentinel == null) return;

            var provider = ProviderManager.Instance.GetProvider(this.GetActualAIProviderName());
            if (provider == null) return;

            // Initialize cache for this batch
            this._batchParseCache = new Dictionary<string, (GH_Boolean, bool)>();

            // Single ProcessBatchResults call - parse once, cache both values
            this.ProcessBatchResults<GH_Boolean>(
                "Result",
                sentinel,
                results,
                (customId, resultBody) =>
                {
                    // Decode and parse ONCE, cache both values
                    var (value, usedFallback) = ParseBooleanWithFallback(resultBody, provider.Decode);
                    this._batchParseCache[customId] = (value, usedFallback);
                    return value;
                },
                messages);

            // Process Used Fallback output using cached values (no re-parse!)
            // Use the same sentinel as Result since both trees have identical structure
            this.ProcessBatchResults<GH_Boolean>(
                "Used Fallback",
                sentinel,
                results,
                (customId, resultBody) =>
                {
                    // Retrieve from cache - no parsing needed
                    if (this._batchParseCache.TryGetValue(customId, out var cached))
                    {
                        return new GH_Boolean(cached.usedFallback);
                    }
                    return null;
                },
                messages);

            // Clear cache
            this._batchParseCache = null;
        }

        /// <summary>
        /// Helper to parse boolean from provider response with fallback detection.
        /// Returns both the parsed value (or null) and whether fallback was used.
        /// </summary>
        private static (GH_Boolean value, bool usedFallback) ParseBooleanWithFallback(
            JObject resultBody,
            System.Func<JObject, System.Collections.Generic.List<IAIInteraction>> decode)
        {
            if (resultBody == null)
            {
                return (null, true);
            }

            var interactions = decode(resultBody);
            var lastText = interactions
                ?.OfType<AIInteractionText>()
                .LastOrDefault(i => i.Agent == AIAgent.Assistant);

            if (lastText == null)
            {
                return (null, true);
            }

            if (bool.TryParse(lastText.Content?.Trim(), out bool value))
            {
                return (new GH_Boolean(value), false);
            }

            return (null, true);
        }

        protected override AsyncWorkerBase CreateWorker(Action<string> progressReporter)
        {
            return new AIList2BooleanWorker(this, this.AddRuntimeMessage, ComponentProcessingOptions);
        }

        private sealed class AIList2BooleanWorker : AsyncWorkerBase
        {
            private Dictionary<string, GH_Structure<IGH_Goo>> inputTree;
            private SmartHopper.Core.DataTree.DataTreeProcessor.ProcessingResult<IGH_Goo> result;
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
            }

            public override void GatherInput(IGH_DataAccess DA, out int dataCount)
            {
                this.inputTree = new Dictionary<string, GH_Structure<IGH_Goo>>();

                // Get the input trees
                var listTree = new GH_Structure<IGH_Goo>();
                var questionTree = new GH_Structure<GH_String>();

                DA.GetDataTree("List", out listTree);
                DA.GetDataTree("Question", out questionTree);

                // Get the fallback as a single item (not a tree)
                var fallbackItem = new GH_Boolean();
                bool hasFallback = DA.GetData("Fallback", ref fallbackItem);

                // Convert generic data to string structure
                var stringListTree = ConvertToGHString(listTree);

                // Store the converted trees using GHStructureConverter
                this.inputTree["List"] = GHStructureConverter.ConvertToGooTree(stringListTree);
                this.inputTree["Question"] = GHStructureConverter.ConvertToGooTree(questionTree);

                // Store fallback as GH_Boolean directly (no conversion needed)
                var fallbackStructure = new GH_Structure<IGH_Goo>();
                if (hasFallback && fallbackItem != null)
                {
                    fallbackStructure.Append(fallbackItem, new GH_Path(0));
                }
                this.inputTree["Fallback"] = fallbackStructure;

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
                            return await ProcessData(branches, this.parent).ConfigureAwait(false);
                        },
                        this.processingOptions,
                        token).ConfigureAwait(false);

                    // Extract typed tree from the heterogeneous result.Outputs dictionary
                    var resultTree = DataTreeProcessor.ExtractTypedTree<GH_String>(this.result.Outputs, "Result");

                    var batchSubmitted = await this.parent.TrySubmitBatchAsync("Result", resultTree, token).ConfigureAwait(false);
                    if (batchSubmitted)
                    {
                        Debug.WriteLine($"[Worker] Sentinel tree stored, batch submitted");
                    }
                    else
                    {
                        // Non-batch: convert strings to booleans and persist via FinishResults
                        var (boolTree, usedFallbackTree) = ConvertStringTreeToBoolean(resultTree);
                        this.parent.FinishResults("Result", boolTree);
                        this.parent.FinishResults("Used Fallback", usedFallbackTree);
                    }

                    Debug.WriteLine($"[Worker] Finished DoWorkAsync - Result keys: {string.Join(", ", this.result.Outputs.Keys)}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Worker] Error: {ex.Message}");
                }
            }

            /// <summary>
            /// Converts a string tree to boolean trees, parsing "true"/"false" strings.
            /// Returns both the result tree and the used fallback tree.
            /// </summary>
            private static (GH_Structure<GH_Boolean> result, GH_Structure<GH_Boolean> usedFallback) ConvertStringTreeToBoolean(GH_Structure<GH_String> stringTree)
            {
                var resultTree = new GH_Structure<GH_Boolean>();
                var usedFallbackTree = new GH_Structure<GH_Boolean>();
                if (stringTree == null) return (resultTree, usedFallbackTree);

                foreach (var path in stringTree.Paths)
                {
                    var branch = stringTree.get_Branch(path);
                    var resultBranch = new List<GH_Boolean>();
                    var usedFallbackBranch = new List<GH_Boolean>();
                    foreach (GH_String item in branch)
                    {
                        var str = item?.Value;
                        if (bool.TryParse(str, out bool val))
                        {
                            resultBranch.Add(new GH_Boolean(val));
                            usedFallbackBranch.Add(new GH_Boolean(false));
                        }
                        else if (!string.IsNullOrEmpty(str))
                        {
                            // Check if this is a fallback value (not empty but not parseable as bool)
                            // The result will be the parsed fallback or null if not parseable
                            bool? parsedFallback = null;
                            if (bool.TryParse(str, out bool fallbackVal))
                            {
                                parsedFallback = fallbackVal;
                            }
                            resultBranch.Add(parsedFallback.HasValue ? new GH_Boolean(parsedFallback.Value) : null);
                            usedFallbackBranch.Add(new GH_Boolean(!parsedFallback.HasValue));
                        }
                        else
                        {
                            // Empty string - null result, fallback was used
                            resultBranch.Add(null);
                            usedFallbackBranch.Add(new GH_Boolean(true));
                        }
                    }
                    resultTree.AppendRange(resultBranch, path);
                    usedFallbackTree.AppendRange(usedFallbackBranch, path);
                }
                return (resultTree, usedFallbackTree);
            }

            private static async Task<Dictionary<string, List<IGH_Goo>>> ProcessData(Dictionary<string, List<IGH_Goo>> branches, AIList2BooleanComponent parent)
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

                // Get the trees - cast from IGH_Goo to concrete types
                var listBranch = branches["List"].Cast<GH_String>().ToList();
                var questionBranch = branches["Question"].Cast<GH_String>().ToList();

                // Get the fallback value (single item, same for all) - read as GH_Boolean
                string fallbackValue = null;
                if (branches.TryGetValue("Fallback", out var fallbackBranch) && fallbackBranch.Count > 0)
                {
                    // Get the fallback value from GH_Boolean
                    if (fallbackBranch[0] is GH_Boolean ghBool)
                    {
                        fallbackValue = ghBool.Value.ToString();
                    }
                }

                // Normalize tree lengths
                var normalizedLists = DataTreeProcessor.NormalizeBranchLengths(
                    new List<List<GH_String>>
                    {
                        new (new GH_String[] { new (AIResponseParser.ConcatenateItemsToJson(listBranch, "array").ToString()) }),
                        questionBranch,
                    });

                // Reassign normalized branches
                var normalizedListTree = normalizedLists[0];
                questionBranch = normalizedLists[1];

                Debug.WriteLine($"[ProcessData] After normalization - Questions count: {questionBranch.Count}, List count: {normalizedListTree.Count}, Fallback: '{fallbackValue}'");

                // Initialize the output (as strings for batch support - sentinels are strings)
                var outputs = new Dictionary<string, List<IGH_Goo>>();
                outputs["Result"] = new List<IGH_Goo>();

                // Iterate over the branches
                // For each item in the prompt tree, get the response from AI
                int i = 0;
                foreach (var question in questionBranch)
                {
                    Debug.WriteLine($"[ProcessData] Processing prompt {i + 1}/{questionBranch.Count}");

                    // Call the AI tool through the tool manager
                    var parameters = new JObject
                    {
                        ["list"] = JArray.Parse(normalizedListTree[i].Value),
                        ["question"] = question.Value,
                        ["fallback"] = fallbackValue,
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

                    // Non-batch: get the result (could be boolean string or fallback value)
                    // The result from the tool is already processed
                    outputs["Result"].Add(new GH_String(resultValue ?? string.Empty));

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
