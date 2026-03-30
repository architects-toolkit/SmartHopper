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
using CommonDrawing = System.Drawing;

namespace SmartHopper.Components.Text
{
    public class AIText2BooleanComponent : AIStatefulAsyncComponentBase
    {
        public override Guid ComponentGuid => new("D3EB06A8-C219-46E3-854E-15EC798AD63A");

        protected override CommonDrawing::Bitmap Icon => Resources.textevaluate;

        public override GH_Exposure Exposure => GH_Exposure.primary;

        /// <inheritdoc/>
        public override IEnumerable<string> Keywords => new[] {
            "AI Text Evaluate",
            "AITextEvaluate",
            "text2boolean",
            "Text Question",
            "Text True/False",
            "AI Question",
            "Ask AI",
            "AI Answer",
            "True False",
            "Yes No",
            "Boolean Question",
            "Evaluate Text",
            "Analyze Text",
            "Check Text",
            "Verify Text",
            "Text Analysis",
        };

        /// <inheritdoc/>
        protected override IReadOnlyList<string> UsingAiTools => new[] { "text2boolean" };

        // Cache to store (value, usedFallback) per customId during batch processing
        private Dictionary<string, (GH_Boolean value, bool usedFallback)> _batchParseCache;

        public AIText2BooleanComponent()
            : base("AI Text To Boolean", "AIText2Boolean",
                  "Use natural language to ask a TRUE or FALSE question about a text.\nIf a tree structure is provided, questions and texts will only match within the same branch paths.",
                  "SmartHopper", "Text")
        {
        }

        protected override void RegisterAdditionalInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Text", "T", "REQUIRED text to evaluate", GH_ParamAccess.tree);
            pManager.AddTextParameter("Question", "Q", "REQUIRED true or false question.\nAI will answer this question based on the input text", GH_ParamAccess.tree);
            pManager.AddTextParameter("Fallback", "F", "OPTIONAL fallback value to use when AI response cannot be parsed as true/false.\nIf not provided, the output will be null for unparsable responses", GH_ParamAccess.item);
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
            var sentinelUsedFallback = this.GetSentinelTree("Used Fallback");
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
            this.ProcessBatchResults<GH_Boolean>(
                "Used Fallback",
                sentinelUsedFallback,
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
            return new AIText2BooleanWorker(this, this.AddRuntimeMessage, ComponentProcessingOptions);
        }

        private sealed class AIText2BooleanWorker : AsyncWorkerBase
        {
            private readonly AIText2BooleanComponent parent;
            private readonly ProcessingOptions processingOptions;
            private Dictionary<string, GH_Structure<GH_String>> inputTree;
            private Dictionary<string, GH_Structure<GH_String>> stringResult;

            public AIText2BooleanWorker(
                AIText2BooleanComponent parent,
                Action<GH_RuntimeMessageLevel, string> addRuntimeMessage,
                ProcessingOptions processingOptions)
                : base(parent, addRuntimeMessage)
            {
                this.parent = parent;
                this.processingOptions = processingOptions;
                this.stringResult = new Dictionary<string, GH_Structure<GH_String>>
                {
                    { "Result", new GH_Structure<GH_String>() },
                    { "Used Fallback", new GH_Structure<GH_String>() },
                };
            }

            public override void GatherInput(IGH_DataAccess DA, out int dataCount)
            {
                this.inputTree = new Dictionary<string, GH_Structure<GH_String>>();

                // Get the input trees
                var textTree = new GH_Structure<GH_String>();
                var questionTree = new GH_Structure<GH_String>();

                DA.GetDataTree("Text", out textTree);
                DA.GetDataTree("Question", out questionTree);

                // Get the fallback as a single item (not a tree)
                var fallbackItem = new GH_String();
                DA.GetData("Fallback", ref fallbackItem);

                // The first defined tree is the one that overrides paths in case they don't match between trees
                this.inputTree["Text"] = textTree;
                this.inputTree["Question"] = questionTree;
                this.inputTree["Fallback"] = new GH_Structure<GH_String>(fallbackItem);

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
                        var (boolTree, usedFallbackTree) = ConvertStringTreeToBoolean(resultTree);
                        this.parent.FinishResults("Result", boolTree);
                        this.parent.FinishResults("Used Fallback", usedFallbackTree);
                    }

                    Debug.WriteLine($"[Worker] Finished DoWorkAsync - Result keys: {string.Join(", ", this.stringResult.Keys)}");
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
                            bool? parsedFallback = ParseBooleanResult(new Newtonsoft.Json.Linq.JValue(str));
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

            private static async Task<Dictionary<string, List<GH_String>>> ProcessData(Dictionary<string, List<GH_String>> branches, AIText2BooleanComponent parent)
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
                var textTree = branches["Text"];
                var questionTree = branches["Question"];

                // Get the fallback value (single item, same for all)
                string fallbackValue = null;
                if (branches.TryGetValue("Fallback", out var fallbackBranch) && fallbackBranch.Count > 0)
                {
                    fallbackValue = fallbackBranch[0]?.Value;
                }

                // Normalize tree lengths (only Text and Question)
                var normalizedLists = DataTreeProcessor.NormalizeBranchLengths(new List<List<GH_String>> { textTree, questionTree });

                // Reassign normalized branches
                textTree = normalizedLists[0];
                questionTree = normalizedLists[1];

                Debug.WriteLine($"[ProcessData] After normalization - Text count: {textTree.Count}, Question count: {questionTree.Count}, Fallback: '{fallbackValue}'");

                // Initialize the output (as strings for batch support - sentinels are strings)
                var outputs = new Dictionary<string, List<GH_String>>();
                outputs["Result"] = new List<GH_String>();

                // Iterate over the branches
                // For each item in the prompt tree, get the response from AI
                for (int i = 0; i < textTree.Count; i++)
                {
                    Debug.WriteLine($"[ProcessData] Processing text {i + 1}/{textTree.Count}");

                    string textValue = textTree[i]?.Value ?? string.Empty;
                    string questionValue = questionTree[i]?.Value ?? string.Empty;

                    Debug.WriteLine($"[ProcessData] Text: '{textValue}', Question: '{questionValue}', Fallback: '{fallbackValue}'");

                    // Call the AI tool through the tool manager
                    var parameters = new JObject
                    {
                        ["text"] = textValue,
                        ["question"] = questionValue,
                        ["fallback"] = fallbackValue,
                        ["contextFilter"] = "-*",
                    };

                    var toolResult = await parent.CallAiToolAsync("text2boolean", parameters)
                        .ConfigureAwait(false);

                    Debug.WriteLine($"[ProcessData] Tool result: {toolResult?.ToString() ?? "null"}");

                    if (toolResult == null)
                    {
                        outputs["Result"].Add(new GH_String(string.Empty));
                        continue;
                    }

                    // In batch mode, CallAiToolAsync returns a sentinel placeholder under "result".
                    // Forward it so ReconstructOutputTree can replace it after the batch completes.
                    var resultValue = toolResult["result"]?.ToString();
                    if (resultValue != null && resultValue.StartsWith("##SH_BATCH:", StringComparison.Ordinal))
                    {
                        outputs["Result"].Add(new GH_String(resultValue));
                        continue;
                    }

                    // Non-batch: get the result (could be boolean string or fallback value)
                    // The result from the tool is already processed
                    outputs["Result"].Add(new GH_String(resultValue ?? string.Empty));
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
