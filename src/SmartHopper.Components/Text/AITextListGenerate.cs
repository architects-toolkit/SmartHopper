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

namespace SmartHopper.Components.Text
{
    public class AITextListGenerate : AIStatefulAsyncComponentBase
    {
        public override Guid ComponentGuid => new Guid("D4723EA1-3BB9-4C9F-9AB2-EF1234567890");
        protected override Bitmap Icon => Resources.textlistgenerate;
        public override GH_Exposure Exposure => GH_Exposure.primary;

        protected override AICapability RequiredCapability => AICapability.Text2Json;

        public AITextListGenerate()
            : base(
                  "AI Text List Generate",
                  "AITextListGenerate",
                  "Generate a list of text items from a prompt and count using AI.\nIf a tree is provided, prompts and counts will match by branch path.",
                  "SmartHopper", "Text")
        {
        }

        protected override void RegisterAdditionalInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Prompt", "P", "REQUIRED The user's prompt", GH_ParamAccess.tree);
            pManager.AddTextParameter("Count", "C", "REQUIRED Number of items to generate", GH_ParamAccess.tree);
        }

        protected override void RegisterAdditionalOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Result", "R", "Generated list of text items", GH_ParamAccess.tree);
        }

        protected override AsyncWorkerBase CreateWorker(Action<string> progressReporter)
        {
            return new AITextListGenerateWorker(this, this.AddRuntimeMessage);
        }

        private sealed class AITextListGenerateWorker : AsyncWorkerBase
        {
            private Dictionary<string, GH_Structure<GH_String>> inputTree;
            private Dictionary<string, GH_Structure<GH_String>> result;
            private readonly AITextListGenerate parent;

            public AITextListGenerateWorker(
                AITextListGenerate parent,
                Action<GH_RuntimeMessageLevel, string> addRuntimeMessage)
                : base(parent, addRuntimeMessage)
            {
                this.parent = parent;
                this.result = new Dictionary<string, GH_Structure<GH_String>>
                {
                    { "Result", new GH_Structure<GH_String>() },
                };
            }

            public override void GatherInput(IGH_DataAccess DA, out int dataCount)
            {
                this.inputTree = new Dictionary<string, GH_Structure<GH_String>>();

                var promptTree = new GH_Structure<GH_String>();
                var countTree = new GH_Structure<GH_String>();

                DA.GetDataTree("Prompt", out promptTree);
                DA.GetDataTree("Count", out countTree);

                this.inputTree["Prompt"] = promptTree;
                this.inputTree["Count"] = countTree;

                var metrics = DataTreeProcessor.GetProcessingPathMetrics(this.inputTree);
                dataCount = metrics.dataCount;
            }

            public override async Task DoWorkAsync(CancellationToken token)
            {
                try
                {
                    Debug.WriteLine($"[AITextListGenerate] Starting DoWorkAsync");
                    this.result = await this.parent.RunDataTreeFunctionAsync(
                        this.inputTree,
                        async (branches) =>
                        {
                            Debug.WriteLine($"[AITextListGenerate] ProcessData called with {branches.Count} branches");
                            return await ProcessData(branches, this.parent).ConfigureAwait(false);
                        },
                        onlyMatchingPaths: false,
                        groupIdenticalBranches: true,
                        token).ConfigureAwait(false);
                    Debug.WriteLine($"[AITextListGenerate] Finished DoWorkAsync - Result keys: {string.Join(", ", this.result.Keys)}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[AITextListGenerate] Error: {ex.Message}");
                }
            }

            private static async Task<Dictionary<string, List<GH_String>>> ProcessData(
                Dictionary<string, List<GH_String>> branches,
                AITextListGenerate parent)
            {
                var promptList = branches["Prompt"];
                var countList = branches["Count"];

                var normalized = DataTreeProcessor.NormalizeBranchLengths(
                    new List<List<GH_String>> { promptList, countList });

                var normalizedPrompts = normalized[0];
                var normalizedCounts = normalized[1];

                var outputs = new Dictionary<string, List<GH_String>>
                {
                    { "Result", new List<GH_String>() },
                };

                for (int i = 0; i < normalizedPrompts.Count; i++)
                {
                    string prompt = normalizedPrompts[i]?.Value ?? string.Empty;
                    if (!int.TryParse(normalizedCounts[i]?.Value, out int count) || count <= 0)
                    {
                        Debug.WriteLine($"[AITextListGenerate] Invalid count at branch {i}: '{normalizedCounts[i]?.Value}'");
                        continue;
                    }

                    var parameters = new JObject
                    {
                        ["prompt"] = prompt,
                        ["count"] = count,
                        ["type"] = "text",
                        ["contextFilter"] = "-*",
                    };

                    var toolResult = await parent.CallAiToolAsync(
                        "list_generate", parameters).ConfigureAwait(false);

                    var items = toolResult?["list"]?.ToObject<List<string>>() ?? new List<string>();
                    foreach (var item in items)
                    {
                        outputs["Result"].Add(new GH_String(item));
                    }
                }

                return outputs;
            }

            public override void SetOutput(IGH_DataAccess DA, out string message)
            {
                if (!this.result.TryGetValue("Result", out GH_Structure<GH_String>? tree))
                {
                    message = "Error: No result available";
                    return;
                }

                this.parent.SetPersistentOutput("Result", tree, DA);
                message = "Done :)";
            }
        }
    }
}
