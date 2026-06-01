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

namespace SmartHopper.Components.Knowledge
{
    /// <summary>
    /// Component that generates a concise AI summary of a Ladybug Tools Discourse forum post by ID.
    /// </summary>
    public class AILadybugForumPostSummarizeComponent : AIStatefulAsyncComponentBase
    {
        public override Guid ComponentGuid => new Guid("6AA04A82-DD11-49B8-BF02-45CF5B9E98AE");

        protected override Bitmap Icon => Resources.ladybugpostsummarize;

        public override GH_Exposure Exposure => GH_Exposure.quarternary;

        /// <inheritdoc/>
        public override IEnumerable<string> Keywords => new[] {
            "AILadybugPostSumm",
            "ladybug_forum_post_summarize",
            "Ladybug Forum Post",
            "Ladybug Post Summary",
        };

        /// <inheritdoc/>
        protected override IReadOnlyList<string> UsingAiTools => new[] { "ladybug_forum_post_summarize" };

        public AILadybugForumPostSummarizeComponent()
            : base(
                  "AI LadybugForum Post Summarize",
                  "AILadybugPostSumm",
                  "Generate a concise summary of a Ladybug Tools Discourse forum post by ID using the configured AI provider.",
                  "SmartHopper",
                  "Knowledge")
        {
        }

        protected override void RegisterAdditionalInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddIntegerParameter("Post Id", "P", "REQUIRED ID or list of IDs of the forum post(s) to summarize.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Instructions", "I", "Optional targeted summary instructions to focus on a specific question, target, or concern.", GH_ParamAccess.item, string.Empty);
        }

        protected override void RegisterAdditionalOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Summary", "S", "AI-generated summary of the forum post.", GH_ParamAccess.tree);
            pManager.AddTextParameter("URL", "U", "URL of the post.", GH_ParamAccess.tree);
        }

        protected override AsyncWorkerBase CreateWorker(Action<string> progressReporter)
        {
            return new AILadybugForumPostSummarizeWorker(this, this.AddRuntimeMessage, this.ComponentProcessingOptions);
        }

        private sealed class AILadybugForumPostSummarizeWorker : AsyncWorkerBase
        {
            private readonly AILadybugForumPostSummarizeComponent parent;
            private readonly ProcessingOptions processingOptions;
            private GH_Structure<GH_Integer> idsTree;
            private string instructions;
            private bool hasWork;

            private GH_Structure<GH_String> resultSummaries;
            private GH_Structure<GH_String> resultUrls;

            public AILadybugForumPostSummarizeWorker(
                AILadybugForumPostSummarizeComponent parent,
                Action<GH_RuntimeMessageLevel, string> addRuntimeMessage,
                ProcessingOptions processingOptions)
                : base(parent, addRuntimeMessage)
            {
                this.parent = parent;
                this.processingOptions = processingOptions;
            }

            public override void GatherInput(IGH_DataAccess DA, out int dataCount)
            {
                var localIdsTree = new GH_Structure<GH_Integer>();
                DA.GetDataTree(0, out localIdsTree);

                string localInstructions = string.Empty;
                DA.GetData(1, ref localInstructions);

                this.idsTree = localIdsTree ?? new GH_Structure<GH_Integer>();
                this.instructions = localInstructions ?? string.Empty;

                this.hasWork = this.idsTree != null && this.idsTree.PathCount > 0 && this.idsTree.DataCount > 0;
                if (!this.hasWork)
                {
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "At least one valid Id is required.");
                }

                dataCount = 0;
            }

            public override async Task DoWorkAsync(CancellationToken token)
            {
                if (!this.hasWork)
                {
                    return;
                }

                try
                {
                    var trees = new Dictionary<string, GH_Structure<GH_Integer>>
                    {
                        { "Id", this.idsTree },
                    };

                    var resultTrees = await this.parent.RunProcessingAsync<GH_Integer, GH_String>(
                        trees,
                        async branchInputs =>
                        {
                            var outputs = new Dictionary<string, List<GH_String>>
                            {
                                { "Summary", new List<GH_String>() },
                                { "Url", new List<GH_String>() },
                            };

                            foreach (var kvp in branchInputs)
                            {
                                var ids = kvp.Value
                                    .Where(g => g != null && g.Value > 0)
                                    .Select(g => g.Value)
                                    .ToList();

                                if (ids.Count == 0)
                                {
                                    continue;
                                }

                                var parameters = new JObject
                                {
                                    ["ids"] = new JArray(ids),
                                };

                                if (!string.IsNullOrWhiteSpace(this.instructions))
                                {
                                    parameters["instructions"] = this.instructions;
                                }

                                var toolResult = await this.parent.CallAIToolAsync("ladybug_forum_post_summarize", parameters, token).ConfigureAwait(false);

                                if (toolResult == null)
                                {
                                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Tool 'ladybug_forum_post_summarize' returned no result.");
                                    continue;
                                }

                                var hasErrors = toolResult["messages"] is JArray messages && messages.Any(m => m["severity"]?.ToString() == "Error");
                                if (hasErrors)
                                {
                                    foreach (var msg in (JArray)toolResult["messages"])
                                    {
                                        if (msg["severity"]?.ToString() == "Error")
                                        {
                                            var text = msg["message"]?.ToString();
                                            if (!string.IsNullOrWhiteSpace(text))
                                            {
                                                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, text);
                                            }
                                        }
                                    }

                                    continue;
                                }

                                var summariesArray = toolResult["summaries"] as JArray;

                                if (summariesArray == null || summariesArray.Count == 0)
                                {
                                    var singleSummary = toolResult["summary"]?.ToString() ?? string.Empty;
                                    var singleUrl = toolResult["url"]?.ToString() ?? string.Empty;
                                    if (!string.IsNullOrWhiteSpace(singleSummary))
                                    {
                                        outputs["Summary"].Add(new GH_String(singleSummary));
                                        outputs["Url"].Add(new GH_String(singleUrl));
                                    }

                                    continue;
                                }

                                foreach (var item in summariesArray.OfType<JObject>())
                                {
                                    var summaryValue = item["summary"]?.ToString() ?? string.Empty;
                                    var urlValue = item["url"]?.ToString() ?? string.Empty;
                                    if (!string.IsNullOrWhiteSpace(summaryValue))
                                    {
                                        outputs["Summary"].Add(new GH_String(summaryValue));
                                        outputs["Url"].Add(new GH_String(urlValue));
                                    }
                                }
                            }

                            return outputs;
                        },
                        this.processingOptions,
                        token).ConfigureAwait(false);

                    this.resultSummaries = new GH_Structure<GH_String>();
                    this.resultUrls = new GH_Structure<GH_String>();

                    if (resultTrees.TryGetValue("Summary", out var summaryTree))
                    {
                        this.resultSummaries = summaryTree;
                    }

                    if (resultTrees.TryGetValue("Url", out var urlTree))
                    {
                        this.resultUrls = urlTree;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[AILadybugForumPostSummarizeWorker] Error: {ex.Message}");
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
                }
            }

            public override void SetOutput(IGH_DataAccess DA, out string message)
            {
                this.parent.SetPersistentOutput("Summary", this.resultSummaries ?? new GH_Structure<GH_String>(), DA);
                this.parent.SetPersistentOutput("URL", this.resultUrls ?? new GH_Structure<GH_String>(), DA);
                this.parent.SetMetricsOutput(DA);

                var hasAnySummary = this.resultSummaries != null && this.resultSummaries.DataCount > 0;
                message = hasAnySummary ? "Post(s) summarized" : "No summary available";
            }
        }
    }
}
