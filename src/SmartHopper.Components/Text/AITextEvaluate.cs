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
using SmartHopper.Infrastructure.AIModels;
using CommonDrawing = System.Drawing;

namespace SmartHopper.Components.Text
{
    public class AITextEvaluate : AIStatefulAsyncComponentBase
    {
        public override Guid ComponentGuid => new("D3EB06A8-C219-46E3-854E-15EC798AD63A");

        protected override CommonDrawing::Bitmap Icon => Resources.textevaluate;

        public override GH_Exposure Exposure => GH_Exposure.primary;

        protected override AICapability RequiredCapability => AICapability.Text2Text;

        public AITextEvaluate()
            : base("AI Text Evaluate", "AITextEvaluate",
                  "Use natural language to ask a TRUE or FALSE question about a text.\nIf a tree structure is provided, questions and texts will only match within the same branch paths.",
                  "SmartHopper", "Text")
        {
        }

        protected override void RegisterAdditionalInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Text", "T", "REQUIRED text to evaluate", GH_ParamAccess.tree);
            pManager.AddTextParameter("Question", "Q", "REQUIRED true or false question.\nAI will answer this question based on the input text", GH_ParamAccess.tree);
        }

        protected override void RegisterAdditionalOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddBooleanParameter("Result", "R", "Result of the evaluation", GH_ParamAccess.tree);
        }

        protected override AsyncWorkerBase CreateWorker(Action<string> progressReporter)
        {
            return new AITextEvaluateWorker(this, this.AddRuntimeMessage, ComponentProcessingOptions);
        }

        private sealed class AITextEvaluateWorker : AsyncWorkerBase
        {
            private readonly AITextEvaluate parent;
            private readonly ProcessingOptions processingOptions;
            private Dictionary<string, GH_Structure<GH_String>> inputTree;
            private Dictionary<string, GH_Structure<GH_Boolean>> result;

            public AITextEvaluateWorker(
                AITextEvaluate parent,
                Action<GH_RuntimeMessageLevel, string> addRuntimeMessage,
                ProcessingOptions processingOptions)
                : base(parent, addRuntimeMessage)
            {
                this.parent = parent;
                this.processingOptions = processingOptions;
                this.result = new Dictionary<string, GH_Structure<GH_Boolean>>
                {
                    { "Result", new GH_Structure<GH_Boolean>() },
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

                // The first defined tree is the one that overrides paths in case they don't match between trees
                this.inputTree["Text"] = textTree;
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

                    this.result = await this.parent.RunProcessingAsync(
                        this.inputTree,
                        async (branches) =>
                        {
                            Debug.WriteLine($"[Worker] ProcessData called with {branches.Count} branches");
                            return await ProcessData(branches, this.parent).ConfigureAwait(false);
                        },
                        this.processingOptions,
                        token).ConfigureAwait(false);

                    Debug.WriteLine($"[Worker] Finished DoWorkAsync - Result keys: {string.Join(", ", this.result.Keys)}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Worker] Error: {ex.Message}");
                }
            }

            private static async Task<Dictionary<string, List<GH_Boolean>>> ProcessData(Dictionary<string, List<GH_String>> branches, AITextEvaluate parent)
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
                var textTree = branches["Text"];
                var questionTree = branches["Question"];

                // Normalize tree lengths
                var normalizedLists = DataTreeProcessor.NormalizeBranchLengths(new List<List<GH_String>> { textTree, questionTree });

                // Reassign normalized branches
                textTree = normalizedLists[0];
                questionTree = normalizedLists[1];

                Debug.WriteLine($"[ProcessData] After normalization - Text count: {textTree.Count}, Question count: {questionTree.Count}");

                // Initialize the output
                var outputs = new Dictionary<string, List<GH_Boolean>>();
                outputs["Result"] = new List<GH_Boolean>();

                // Iterate over the branches
                // For each item in the prompt tree, get the response from AI
                for (int i = 0; i < textTree.Count; i++)
                {
                    Debug.WriteLine($"[ProcessData] Processing text {i + 1}/{textTree.Count}");

                    string textValue = textTree[i]?.Value ?? string.Empty;
                    string questionValue = questionTree[i]?.Value ?? string.Empty;

                    Debug.WriteLine($"[ProcessData] Text: '{textValue}', Question: '{questionValue}'");

                    // Call the AI tool through the tool manager
                    var parameters = new JObject
                    {
                        ["text"] = textValue,
                        ["question"] = questionValue,
                        ["contextFilter"] = "-*",
                    };

                    var toolResult = await parent.CallAiToolAsync("text_evaluate", parameters)
                        .ConfigureAwait(false);

                    Debug.WriteLine($"[ProcessData] Tool result: {toolResult?.ToString() ?? "null"}");

                    var resultToken = toolResult? ["result"];
                    bool? parsedResult = ParseBooleanResult(resultToken);

                    if (!parsedResult.HasValue)
                    {
                        outputs["Result"].Add(null);
                        continue;
                    }

                    outputs["Result"].Add(new GH_Boolean(parsedResult.Value));
                }

                return outputs;
            }

            private static bool? ParseBooleanResult(JToken token)
            {
                if (token == null || token.Type == JTokenType.Null)
                {
                    return null;
                }

                if (token.Type == JTokenType.Boolean)
                {
                    return token.Value<bool>();
                }

                if (token.Type == JTokenType.String)
                {
                    if (bool.TryParse(token.Value<string>(), out bool result))
                    {
                        return result;
                    }
                }

                return null;
            }

            public override void SetOutput(IGH_DataAccess DA, out string message)
            {
                Debug.WriteLine($"[Worker] Setting output - Available keys: {string.Join(", ", this.result.Keys)}");

                if (!this.result.TryGetValue("Result", out GH_Structure<GH_Boolean>? value))
                {
                    Debug.WriteLine("[Worker] Warning: Result key not found in output dictionary");
                    message = "Error: No result available";
                    return;
                }

                this.parent.SetPersistentOutput("Result", value, DA);
                message = "Done :)";
            }
        }
    }
}
