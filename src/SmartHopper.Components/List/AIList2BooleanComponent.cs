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
using SmartHopper.Core.ComponentBase.Batch;
using SmartHopper.Core.DataTree;
using SmartHopper.Core.Grasshopper.Converters;
using SmartHopper.Core.Grasshopper.Utils;
using SmartHopper.Core.Grasshopper.Utils.Parsing;
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AIProviders;
using SmartHopper.Infrastructure.Diagnostics;

namespace SmartHopper.Components.List
{
    public class AIList2BooleanComponent : AIStatefulAsyncComponentBase
    {
        public override Guid ComponentGuid => new ("A8BAD48D-8723-42AD-B13C-A875F940B69C");

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
        protected override IReadOnlyList<string> UsingAiTools => new[] { "textlist2boolean" };

        // Cache to store (value, usedFallback) per customId during batch processing
        private Dictionary<string, (GH_Boolean value, bool usedFallback)> _batchParseCache;

        // Fallback value captured at submission time. In batch mode the textlist2boolean
        // tool's execute body never runs, so OnBatchCompleted must apply the fallback
        // here using the value the user wired into the Fallback input.
        private bool? _batchFallbackValue;

        protected override ProcessingOptions ComponentProcessingOptions => new ProcessingOptions
        {
            Topology = ProcessingTopology.BranchToBranch,
            OnlyMatchingPaths = false,
            GroupIdenticalBranches = true,
        };

        public AIList2BooleanComponent()
            : base(
                "AI List To Boolean",
                "AIList2Boolean",
                "Use natural language to evaluate a list and output a TRUE/FALSE answer.\nThis components takes the list as a whole. This means that every question will return True or False for each provided list (not for each individual items).\nIf a tree structure is provided, questions and lists will only match within the same branch paths.",
                "SmartHopper",
                "List")
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
        protected override void OnBatchCompleted(IReadOnlyDictionary<string, JObject> results, IReadOnlyList<SHRuntimeMessage> messages = null)
        {
            var sentinel = this.GetSentinelTree("Result");
            if (results == null || sentinel == null) return;

            var provider = ProviderManager.Instance.GetProvider(this.GetActualAIProviderName());
            if (provider == null) return;

            // Initialize cache for this batch. Populated by the decode lambda below
            // and consumed by TransformOutputs to emit the parallel "Used Fallback" tree
            // in the same pass — avoiding a second ProcessBatchResults call (which would
            // re-iterate and re-Decode every item, and overwrite persisted metrics).
            this._batchParseCache = new Dictionary<string, (GH_Boolean, bool)>();

            var fallback = this._batchFallbackValue;
            this.ProcessBatchResults<GH_Boolean>(
                "Result",
                sentinel,
                results,
                (customId, resultBody) =>
                {
                    // Decode and parse ONCE via the centralized resolver, cache both
                    // values for SentinelTransformOutputs.
                    var (boolValue, usedFallback) = BooleanResultResolver.ResolveFromBody(resultBody, provider.Decode, fallback);
                    var value = boolValue.HasValue ? new GH_Boolean(boolValue.Value) : null;
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

        /// <inheritdoc/>
        /// <remarks>
        /// In batch mode, populates the parallel "Used Fallback" output from the cache
        /// built during the single <see cref="ProcessBatchResults{T}"/> pass. Outside of
        /// batch mode (or when the sentinel id is unknown) this is a no-op and the
        /// non-batch worker emits "Used Fallback" directly via <c>FinishResults</c>.
        /// </remarks>
        protected override Dictionary<string, IGH_Goo> SentinelTransformOutputs(
            Dictionary<string, IGH_Goo> decodedOutputs, ProcessingUnitContext context)
        {
            if (this._batchParseCache != null
                && !string.IsNullOrEmpty(context.SentinelId)
                && this._batchParseCache.TryGetValue(context.SentinelId, out var cached))
            {
                decodedOutputs["Used Fallback"] = new GH_Boolean(cached.usedFallback);
            }

            return decodedOutputs;
        }

        protected override AsyncWorkerBase CreateWorker(Action<string> progressReporter)
        {
            return new AIList2BooleanWorker(this, this.AddRuntimeMessage, this.ComponentProcessingOptions);
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

                // Capture for batch mode: OnBatchCompleted needs the fallback value to
                // apply when the AI response is unparseable. The batch path doesn't run
                // the tool's execute body where the non-batch fallback logic lives.
                this.parent._batchFallbackValue = (hasFallback && fallbackItem != null)
                    ? fallbackItem.Value
                    : (bool?)null;

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

                    // Extract typed tree from the heterogeneous result.Outputs dictionary
                    var resultTree = DataTreeProcessor.ExtractTypedTree<GH_String>(this.result.Outputs, "Result");

                    // Wrap in dictionary for batch submission
                    var resultDict = new Dictionary<string, GH_Structure<GH_String>> { { "Result", resultTree } };
                    var batchSubmitted = await this.parent.TrySubmitBatchAsync("Result", resultDict, token).ConfigureAwait(false);
                    if (batchSubmitted)
                    {
                        Debug.WriteLine($"[Worker] Sentinel tree stored, batch submitted");
                    }
                    else
                    {
                        // Non-batch: convert strings to booleans and persist via FinishResults.
                        // The usedFallback flag is taken directly from the tool output, not
                        // inferred from the result string (which would be wrong when the tool
                        // applied the fallback and produced a parseable boolean string).
                        var usedFallbackStringTree = DataTreeProcessor.ExtractTypedTree<GH_String>(this.result.Outputs, "UsedFallback");
                        var (boolTree, usedFallbackTree) = ConvertStringTreeToBoolean(resultTree, usedFallbackStringTree);
                        this.parent.FinishResults("Result", boolTree);
                        this.parent.FinishResults("Used Fallback", usedFallbackTree);
                    }

                    Debug.WriteLine($"[Worker] Finished DoWorkAsync - Result keys: {string.Join(", ", this.result.Outputs.Keys)}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Worker] Error: {ex.Message}");
                    this.CollectMessage(SHRuntimeMessageSeverity.Error, ex.Message);
                }
            }

            /// <summary>
            /// Converts the parallel Result and UsedFallback string trees emitted by
            /// <see cref="ProcessData"/> into typed boolean trees. The usedFallback flag is
            /// read directly from the tool output (no re-inference from the result string).
            /// </summary>
            private static (GH_Structure<GH_Boolean> result, GH_Structure<GH_Boolean> usedFallback) ConvertStringTreeToBoolean(
                GH_Structure<GH_String> resultStringTree,
                GH_Structure<GH_String> usedFallbackStringTree)
            {
                var resultTree = new GH_Structure<GH_Boolean>();
                var usedFallbackTree = new GH_Structure<GH_Boolean>();
                if (resultStringTree == null) return (resultTree, usedFallbackTree);

                foreach (var path in resultStringTree.Paths)
                {
                    var resultBranchData = resultStringTree.get_Branch(path);
                    var fallbackBranchData = usedFallbackStringTree?.PathExists(path) == true
                        ? usedFallbackStringTree.get_Branch(path)
                        : null;

                    var resultBranch = new List<GH_Boolean>();
                    var usedFallbackBranch = new List<GH_Boolean>();

                    for (int i = 0; i < resultBranchData.Count; i++)
                    {
                        var resultStr = (resultBranchData[i] as GH_String)?.Value;
                        var fallbackStr = (fallbackBranchData != null && i < fallbackBranchData.Count)
                            ? (fallbackBranchData[i] as GH_String)?.Value
                            : null;

                        // Parse the boolean result (null/empty/unparseable -> null).
                        // The usedFallback flag is taken from the tool output below,
                        // not inferred from the result string here.
                        if (bool.TryParse(resultStr, out bool val))
                        {
                            resultBranch.Add(new GH_Boolean(val));
                        }
                        else
                        {
                            resultBranch.Add(null);
                        }

                        // usedFallback flag comes directly from the tool result.
                        // Defaults to true when the flag is missing or unparseable
                        // (e.g. tool returned null or call failed).
                        var usedFallback = !bool.TryParse(fallbackStr, out bool uf) || uf;
                        usedFallbackBranch.Add(new GH_Boolean(usedFallback));
                    }

                    resultTree.AppendRange(resultBranch, path);
                    usedFallbackTree.AppendRange(usedFallbackBranch, path);
                }

                return (resultTree, usedFallbackTree);
            }

            private static async Task<Dictionary<string, List<IGH_Goo>>> ProcessData(Dictionary<string, List<IGH_Goo>> branches, AIList2BooleanComponent parent, CancellationToken cancellationToken)
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

                // Initialize the outputs (as strings for batch support - sentinels are strings).
                // Result and UsedFallback are kept as parallel lists with identical lengths.
                var outputs = new Dictionary<string, List<IGH_Goo>>();
                outputs["Result"] = new List<IGH_Goo>();
                outputs["UsedFallback"] = new List<IGH_Goo>();

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

                    var toolResult = await parent.CallAIToolAsync(
                        "textlist2boolean", parameters, cancellationToken)
                        .ConfigureAwait(false);

                    if (toolResult == null)
                    {
                        outputs["Result"].Add(new GH_String(string.Empty));
                        outputs["UsedFallback"].Add(new GH_String(bool.TrueString));
                        i++;
                        continue;
                    }

                    // In batch mode, CallAIToolAsync returns a sentinel placeholder under "result".
                    // Forward it so ReconstructOutputTree can replace it after the batch completes.
                    // The UsedFallback tree is unused in batch mode (OnBatchCompleted populates it
                    // from the cached parse), but we keep it aligned for non-batch consistency.
                    var resultValue = toolResult["result"]?.ToString();
                    if (BatchSentinel.Is(resultValue))
                    {
                        outputs["Result"].Add(new GH_String(resultValue));
                        outputs["UsedFallback"].Add(new GH_String(resultValue));
                        i++;
                        continue;
                    }

                    // Non-batch: get the result and the authoritative usedFallback flag from the tool.
                    outputs["Result"].Add(new GH_String(resultValue ?? string.Empty));

                    // The tool sets "usedFallback" explicitly (true when fallback was applied or
                    // when parsing failed without fallback). Default to true when missing.
                    var usedFallback = toolResult["usedFallback"].GetBoolOrDefault(true);
                    outputs["UsedFallback"].Add(new GH_String(usedFallback.ToString()));

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
