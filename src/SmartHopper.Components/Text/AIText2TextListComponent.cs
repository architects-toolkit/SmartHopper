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
    public class AIText2TextListComponent : AIStatefulAsyncComponentBase
    {
        public override Guid ComponentGuid => new Guid("66AE34E5-E615-4631-9A1E-D87FB5B76321");

        protected override Bitmap Icon => Resources.textlistgenerate;

        public override GH_Exposure Exposure => GH_Exposure.primary;

        /// <inheritdoc/>
        public override IEnumerable<string> Keywords => new[] {
            "AI Text List Generate",
            "AITextListGenerate",
            "text2textlist",
            "Text List Generate",
            "Text List Generator",
            "Text List Create",
            "Text List Creator",
            "Generate List",
            "Create List",
            "List Items",
            "AI List",
            "List Generator",
            "List Creator",
            "AI Enumeration",
            "Enumerate",
            "List Production",
            "Generate Items",
            "Multiple Items",
        };

        /// <inheritdoc/>
        protected override IReadOnlyList<string> UsingAiTools => new[] { "text2textlist" };

        protected override ProcessingOptions ComponentProcessingOptions => new ProcessingOptions
        {
            Topology = ProcessingTopology.ItemGraft,
            OnlyMatchingPaths = false,
            GroupIdenticalBranches = true,
        };

        public AIText2TextListComponent()
            : base(
                "AI Text To Text List",
                "AIText2TextList",
                "Generate a list of text items from a prompt and count using AI.\nIf a tree is provided, prompts and counts will match by branch path.",
                "SmartHopper",
                "Text")
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

        protected override AsyncWorkerBase CreateWorker(Action<string> progressReporter)
        {
            return new AIText2TextListWorker(this, this.AddRuntimeMessage, this.ComponentProcessingOptions);
        }

        private sealed class AIText2TextListWorker : AsyncWorkerBase
        {
            private Dictionary<string, GH_Structure<GH_String>> inputTree;
            private Dictionary<string, GH_Structure<GH_String>> result;
            private readonly AIText2TextListComponent parent;
            private readonly ProcessingOptions processingOptions;

            public AIText2TextListWorker(
                AIText2TextListComponent parent,
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

                var promptTree = new GH_Structure<GH_String>();
                var countTree = new GH_Structure<GH_String>();

                DA.GetDataTree("Prompt", out promptTree);
                DA.GetDataTree("Count", out countTree);

                this.inputTree["Prompt"] = promptTree;
                this.inputTree["Count"] = countTree;

                dataCount = 0;
            }

            public override async Task DoWorkAsync(CancellationToken token)
            {
                try
                {
                    Debug.WriteLine($"[AIText2TextList] Starting DoWorkAsync");

                    this.result = await this.parent.RunProcessingAsync(
                        this.inputTree,
                        async (branches) => await ProcessData(branches, this.parent, token).ConfigureAwait(false),
                        this.processingOptions,
                        token).ConfigureAwait(false);

                    var batchSubmitted = await this.parent.TrySubmitBatchAsync("Result", this.result, token).ConfigureAwait(false);
                    if (batchSubmitted)
                    {
                        Debug.WriteLine($"[AIText2TextList] Sentinel tree stored, batch submitted");
                    }
                    else if (this.result.TryGetValue("Result", out var resultTree))
                    {
                        // Non-batch: persist output and emit metrics via FinishResults
                        this.parent.FinishResults("Result", resultTree);
                    }

                    Debug.WriteLine($"[AIText2TextList] Finished DoWorkAsync - Result keys: {string.Join(", ", this.result.Keys)}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[AIText2TextList] Error: {ex.Message}");
                    this.CollectMessage(SHRuntimeMessageSeverity.Error, ex.Message);
                }
            }

            private static async Task<Dictionary<string, List<GH_String>>> ProcessData(
                Dictionary<string, List<GH_String>> branches,
                AIText2TextListComponent parent,
                CancellationToken cancellationToken)
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
                        Debug.WriteLine($"[AIText2TextList] Invalid count at branch {i}: '{normalizedCounts[i]?.Value}'");
                        continue;
                    }

                    var parameters = new JObject
                    {
                        ["prompt"] = prompt,
                        ["count"] = count,
                        ["type"] = "text",
                        ["contextFilter"] = "-*",
                    };

                    var toolResult = await parent.CallAIToolAsync(
                        "text2textlist", parameters, cancellationToken).ConfigureAwait(false);

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
                // Outputs and metrics are handled by FinishResults (non-batch) or
                // ProcessBatchResults → FinishResults (batch). RestorePersistentOutputs
                // replays them to the canvas on the next solve.
                message = string.Empty;
            }
        }
    }
}
