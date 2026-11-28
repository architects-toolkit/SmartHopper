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
using SmartHopper.Infrastructure.AIProviders;
using SmartHopper.ProviderSdk.AICall.Core.Base;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AIProviders;
using SmartHopper.ProviderSdk.Diagnostics;

using SmartHopper.ProviderSdk.Logging;
namespace SmartHopper.Components.List
{
    public class AIList2BooleanComponent : AIStatefulAsyncComponentBase
    {
        public override Guid ComponentGuid => new ("A8BAD48D-8723-42AD-B13C-A875F940B69C");

        protected override Bitmap Icon => Resources.AIList2Boolean;

        public override GH_Exposure Exposure => GH_Exposure.secondary;

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
                "Evaluate a list of items as a whole against a True/False question using AI.\nReturns one boolean per list (branch), not one per item. When data trees are provided, lists and questions are matched by branch path.",
                "SmartHopper",
                "I. Text & Lists")
        {
        }

        protected override void RegisterAdditionalInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("List", "L", "List of items to evaluate together.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Question", "Q", "Yes/no or True/False question to answer about the list.", GH_ParamAccess.tree);
            pManager.AddBooleanParameter("Fallback", "F", "Optional fallback boolean returned when the AI response cannot be parsed as True or False (leave disconnected to output null on parse failure).", GH_ParamAccess.item);
            pManager[2].Optional = true;
        }

        protected override void RegisterAdditionalOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddBooleanParameter("Result", "R", "Boolean answer for each list (or the Fallback value when unparsable).", GH_ParamAccess.tree);
            pManager.AddBooleanParameter("Used Fallback", "UF", "True when the AI response could not be parsed as True/False and the fallback behavior was used.", GH_ParamAccess.tree);
        }

        /// <inheritdoc/>
        protected override void OnBatchCompleted(IReadOnlyDictionary<string, JObject>? results, IReadOnlyList<SHRuntimeMessage>? messages = null)
        {
            var sentinel = this.GetSentinelTree("Result");
            if (results == null || sentinel == null) return;

            // Decode with the provider that ran the batch — the live selection may
            // have changed while it was in flight (mirrors ProcessBatchResultsCore).
            var provider = ProviderManager.Instance.GetProvider(this.GetSubmittedProviderName());
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

            // Single finalization pass: "Used Fallback" is emitted as an extra
            // output by SentinelTransformOutputs during the Result pass above.
            // A second ProcessBatchResults call would re-decode every item,
            // duplicate the metrics tree, and overwrite persisted provenance.
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

        /// <inheritdoc/>
        /// <remarks>
        /// Persists the Fallback value submitted with the batch so an in-flight
        /// batch resumed after save/reopen resolves unparsable responses against
        /// the same fallback the request was built with.
        /// </remarks>
        protected override JObject CaptureBatchDecodeContext()
            => this._batchFallbackValue.HasValue
                ? new JObject { ["fallback"] = this._batchFallbackValue.Value }
                : null;

        /// <inheritdoc/>
        protected override void RestoreBatchDecodeContext(JObject context)
            => this._batchFallbackValue = context?["fallback"]?.ToObject<bool?>();

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
                    Logger.For<AIList2BooleanWorker>().Trace($"[Worker] Starting DoWorkAsync");
                    Logger.For<AIList2BooleanWorker>().Trace($"[Worker] Input tree keys: {string.Join(", ", this.inputTree.Keys)}");
                    Logger.For<AIList2BooleanWorker>().Trace($"[Worker] Input tree data counts: {string.Join(", ", this.inputTree.Select(kvp => $"{kvp.Key}: {kvp.Value.DataCount}"))}");

                    this.result = await this.parent.RunProcessingAsync(
                        this.inputTree,
                        async (branches) =>
                        {
                            Logger.For<AIList2BooleanWorker>().Trace($"[Worker] ProcessData called with {branches.Count} branches");
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
                        Logger.For<AIList2BooleanWorker>().Trace($"[Worker] Sentinel tree stored, batch submitted");
                    }
                    else
                    {
                        // Result and Used Fallback are outputs of the same branch
                        // operation — finalize them atomically so outputs and
                        // provenance commit together. Replayed fragments may hold
                        // typed booleans (batch-committed) or legacy strings;
                        // the converter handles both per item.
                        var (boolTree, usedFallbackTree) = ConvertToBooleanTrees(this.result.Outputs);
                        this.parent.FinishResults("Result", boolTree, ("Used Fallback", usedFallbackTree));
                    }

                    Logger.For<AIList2BooleanWorker>().Trace($"[Worker] Finished DoWorkAsync - Result keys: {string.Join(", ", this.result.Outputs.Keys)}");
                }
                catch (Exception ex)
                {
                    Logger.For<AIList2BooleanWorker>().Exception(ex, $"[Worker] Error: {ex.Message}");
                    this.CollectMessage(SHRuntimeMessageSeverity.Error, ex.Message);
                }
            }

            /// <summary>
            /// Converts the Result and "Used Fallback" channels emitted by
            /// <see cref="ProcessData"/> into typed boolean trees. Items may already
            /// be typed <see cref="GH_Boolean"/> (fresh non-batch results and
            /// fragments replayed from the result store) or <see cref="GH_String"/>
            /// (legacy committed fragments) — each item is converted independently
            /// so mixed-content channels emit correctly. The usedFallback flag is
            /// authoritative data from the fragment, never re-inferred from the
            /// result string.
            /// </summary>
            private static (GH_Structure<GH_Boolean> result, GH_Structure<GH_Boolean> usedFallback) ConvertToBooleanTrees(
                Dictionary<string, GH_Structure<IGH_Goo>> outputs)
            {
                var resultTree = new GH_Structure<GH_Boolean>();
                var usedFallbackTree = new GH_Structure<GH_Boolean>();
                if (outputs == null
                    || !outputs.TryGetValue("Result", out var resultSource)
                    || resultSource == null)
                {
                    return (resultTree, usedFallbackTree);
                }

                // "Used Fallback" is the canonical channel (matches the output
                // parameter name); "UsedFallback" only appears in fragments
                // committed before the channels were aligned with parameter names.
                outputs.TryGetValue("Used Fallback", out var fallbackSource);
                outputs.TryGetValue("UsedFallback", out var legacyFallbackSource);

                foreach (var path in resultSource.Paths)
                {
                    var resultBranchData = resultSource.get_Branch(path);
                    if (resultBranchData == null)
                    {
                        continue;
                    }

                    var resultBranch = new List<GH_Boolean>();
                    var usedFallbackBranch = new List<GH_Boolean>();

                    for (int i = 0; i < resultBranchData.Count; i++)
                    {
                        resultBranch.Add(ToBooleanGoo(resultBranchData[i] as IGH_Goo));
                        usedFallbackBranch.Add(ResolveUsedFallbackGoo(fallbackSource, legacyFallbackSource, path, i));
                    }

                    resultTree.AppendRange(resultBranch, path);
                    usedFallbackTree.AppendRange(usedFallbackBranch, path);
                }

                return (resultTree, usedFallbackTree);
            }

            /// <summary>
            /// Converts one fragment item to a <see cref="GH_Boolean"/>: typed
            /// booleans pass through, strings parse, everything else becomes null
            /// (unparseable tool results intentionally emit null, not fallback).
            /// </summary>
            private static GH_Boolean ToBooleanGoo(IGH_Goo item)
            {
                switch (item)
                {
                    case GH_Boolean boolean:
                        return new GH_Boolean(boolean.Value);
                    case GH_String text when bool.TryParse(text.Value, out bool parsed):
                        return new GH_Boolean(parsed);
                    default:
                        return null;
                }
            }

            /// <summary>
            /// Resolves the usedFallback flag for one output slot: canonical
            /// channel first, legacy channel next, defaulting to true when no
            /// parseable value exists (e.g. tool returned null or call failed).
            /// </summary>
            private static GH_Boolean ResolveUsedFallbackGoo(
                GH_Structure<IGH_Goo> fallbackSource,
                GH_Structure<IGH_Goo> legacyFallbackSource,
                GH_Path path,
                int index)
            {
                var item = GetGooItem(fallbackSource, path, index) ?? GetGooItem(legacyFallbackSource, path, index);
                switch (item)
                {
                    case GH_Boolean boolean:
                        return new GH_Boolean(boolean.Value);
                    case GH_String text when bool.TryParse(text.Value, out bool parsed):
                        return new GH_Boolean(parsed);
                    default:
                        return new GH_Boolean(true);
                }
            }

            private static IGH_Goo GetGooItem(GH_Structure<IGH_Goo> tree, GH_Path path, int index)
            {
                if (tree == null || !tree.PathExists(path))
                {
                    return null;
                }

                var branch = tree.get_Branch(path);
                return branch != null && index < branch.Count ? branch[index] as IGH_Goo : null;
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

                Logger.For<AIList2BooleanWorker>().Trace($"[Worker] Processing {branches.Count} trees");
                Logger.For<AIList2BooleanWorker>().Trace($"[Worker] Items per tree: {branches.Values.Max(branch => branch.Count)}");

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

                Logger.For<AIList2BooleanWorker>().Trace($"[ProcessData] After normalization - Questions count: {questionBranch.Count}, List count: {normalizedListTree.Count}, Fallback: '{fallbackValue}'");

                // Initialize the outputs. Channel names match the output parameter
                // names so stored fragments replay verbatim through BuildRunTrees
                // in every mode (sentinel placeholders are strings; decoded and
                // non-batch items are typed booleans — the canonical output type).
                var outputs = new Dictionary<string, List<IGH_Goo>>();
                outputs["Result"] = new List<IGH_Goo>();
                outputs["Used Fallback"] = new List<IGH_Goo>();

                // Iterate over the branches
                // For each item in the prompt tree, get the response from AI
                int i = 0;
                foreach (var question in questionBranch)
                {
                    Logger.For<AIList2BooleanWorker>().Trace($"[ProcessData] Processing prompt {i + 1}/{questionBranch.Count}");

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
                        outputs["Result"].Add(null);
                        outputs["Used Fallback"].Add(new GH_Boolean(true));
                        i++;
                        continue;
                    }

                    // In batch mode, CallAIToolAsync returns a sentinel placeholder under "result".
                    // Forward it so ReconstructOutputTree can replace it after the batch completes.
                    // "Used Fallback" is intentionally left empty here: storing a placeholder
                    // sentinel in a channel the decode pass does not resolve would leave the
                    // fragment flagged as unfinished work and dropped at commit. The decoded
                    // parse supplies the value post-batch via SentinelTransformOutputs.
                    var resultValue = toolResult["result"]?.ToString();
                    if (BatchSentinel.Is(resultValue))
                    {
                        outputs["Result"].Add(new GH_String(resultValue));
                        i++;
                        continue;
                    }

                    // Non-batch: get the result and the authoritative usedFallback flag from the tool.
                    // Typed items are stored so committed fragments replay in any mode.
                    var parseable = bool.TryParse(resultValue, out bool parsedValue);
                    outputs["Result"].Add(parseable ? new GH_Boolean(parsedValue) : null);

                    // The tool sets "usedFallback" explicitly (true when fallback was applied or
                    // when parsing failed without fallback). Default to true when missing.
                    var usedFallback = toolResult["usedFallback"].GetBoolOrDefault(true);
                    outputs["Used Fallback"].Add(new GH_Boolean(usedFallback));

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
