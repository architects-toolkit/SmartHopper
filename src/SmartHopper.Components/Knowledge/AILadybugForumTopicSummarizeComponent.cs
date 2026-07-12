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
    /// Component that generates a concise AI summary of a Ladybug Tools Discourse forum topic by ID.
    /// </summary>
    public class AILadybugForumTopicSummarizeComponent : AIStatefulAsyncComponentBase
    {
        public override Guid ComponentGuid => new Guid("626E8B3E-6E36-45E7-89D3-58C149828C27");

        protected override Bitmap Icon => Resources.ladybugtopicsummarize;

        public override GH_Exposure Exposure => GH_Exposure.quarternary;

        /// <inheritdoc/>
        public override IEnumerable<string> Keywords => new[] {
            "AILadybugTopicSumm",
            "ladybug_forum_topic_summarize",
            "Ladybug Forum Topic",
            "Ladybug Summary",
            "Ladybug Tools Summary",
        };

        /// <inheritdoc/>
        protected override IReadOnlyList<string> UsingAiTools => new[] { "ladybug_forum_topic_summarize" };

        public AILadybugForumTopicSummarizeComponent()
            : base(
                  "AI LadybugForum Topic Summarize",
                  "AILadybugTopicSumm",
                  "Generate a concise summary of a Ladybug Tools Discourse forum topic (all posts) by topic ID using the configured AI provider.",
                  "SmartHopper",
                  "Knowledge")
        {
            // Set RunOnlyOnInputChanges to false to ensure the component always runs when the Run parameter is true
            this.RunOnlyOnInputChanges = false;
        }

        protected override void RegisterAdditionalInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddIntegerParameter("Topic Id", "T", "REQUIRED ID or list of IDs of the forum topic(s) to summarize.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Instructions", "I", "Optional targeted summary instructions to focus on a specific question, target, or concern.", GH_ParamAccess.item, string.Empty);
        }

        protected override void RegisterAdditionalOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Summary", "S", "AI-generated summary of the forum topic.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Title", "T", "Title of the forum topic.", GH_ParamAccess.tree);
            pManager.AddTextParameter("URL", "U", "URL of the forum topic.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Post Count", "P", "Number of posts included in the summary.", GH_ParamAccess.tree);
        }

        protected override AsyncWorkerBase CreateWorker(Action<string> progressReporter)
        {
            return new AILadybugForumTopicSummarizeWorker(this, this.AddRuntimeMessage, this.ComponentProcessingOptions);
        }

        private sealed class AILadybugForumTopicSummarizeWorker : AsyncWorkerBase
        {
            private readonly AILadybugForumTopicSummarizeComponent parent;
            private readonly ProcessingOptions processingOptions;
            private GH_Structure<GH_Integer> idsTree;
            private string instructions;
            private bool hasWork;

            private GH_Structure<GH_String> resultSummaries;
            private GH_Structure<GH_String> resultTitles;
            private GH_Structure<GH_String> resultUrls;
            private GH_Structure<GH_String> resultPostCounts;

            public AILadybugForumTopicSummarizeWorker(
                AILadybugForumTopicSummarizeComponent parent,
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
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "At least one valid Topic Id is required.");
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
                        { "TopicId", this.idsTree },
                    };

                    var resultTrees = await this.parent.RunProcessingAsync<GH_Integer, GH_String>(
                        trees,
                        async branchInputs =>
                        {
                            var outputs = new Dictionary<string, List<GH_String>>
                            {
                                { "TopicId", new List<GH_String>() },
                                { "Summary", new List<GH_String>() },
                                { "Title", new List<GH_String>() },
                                { "Url", new List<GH_String>() },
                                { "PostCount", new List<GH_String>() },
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

                                foreach (int id in ids)
                                {
                                    var parameters = new JObject
                                    {
                                        ["topic_id"] = id,
                                    };

                                    if (!string.IsNullOrWhiteSpace(this.instructions))
                                    {
                                        parameters["instructions"] = this.instructions;
                                    }

                                    var toolResult = await this.parent.CallAIToolAsync("ladybug_forum_topic_summarize", parameters, token).ConfigureAwait(false);

                                    if (toolResult == null)
                                    {
                                        this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Tool 'ladybug_forum_topic_summarize' returned no result.");
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

                                    var topicIdValue = toolResult["topic_id"]?.ToObject<int?>() ?? id;
                                    var summaryValue = toolResult["summary"]?.ToString() ?? string.Empty;
                                    var titleValue = toolResult["title"]?.ToString() ?? string.Empty;
                                    var urlValue = toolResult["url"]?.ToString() ?? string.Empty;
                                    var postCountValue = toolResult["post_count"]?.ToObject<int?>() ?? 0;

                                    outputs["TopicId"].Add(new GH_String(topicIdValue.ToString()));
                                    outputs["Summary"].Add(new GH_String(summaryValue));
                                    outputs["Title"].Add(new GH_String(titleValue));
                                    outputs["Url"].Add(new GH_String(urlValue));
                                    outputs["PostCount"].Add(new GH_String(postCountValue.ToString()));
                                }
                            }

                            return outputs;
                        },
                        this.processingOptions,
                        token).ConfigureAwait(false);

                    this.resultSummaries = new GH_Structure<GH_String>();
                    this.resultTitles = new GH_Structure<GH_String>();
                    this.resultUrls = new GH_Structure<GH_String>();
                    this.resultPostCounts = new GH_Structure<GH_String>();

                    if (resultTrees.TryGetValue("Summary", out var summaryTree))
                    {
                        this.resultSummaries = summaryTree;
                    }

                    if (resultTrees.TryGetValue("Title", out var titleTree))
                    {
                        this.resultTitles = titleTree;
                    }

                    if (resultTrees.TryGetValue("Url", out var urlTree))
                    {
                        this.resultUrls = urlTree;
                    }

                    if (resultTrees.TryGetValue("PostCount", out var postCountTree))
                    {
                        this.resultPostCounts = postCountTree;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[AILadybugForumTopicSummarizeWorker] Error: {ex.Message}");
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
                }
            }

            public override void SetOutput(IGH_DataAccess DA, out string message)
            {
                this.parent.SetPersistentOutput("Summary", this.resultSummaries ?? new GH_Structure<GH_String>(), DA);
                this.parent.SetPersistentOutput("Title", this.resultTitles ?? new GH_Structure<GH_String>(), DA);
                this.parent.SetPersistentOutput("URL", this.resultUrls ?? new GH_Structure<GH_String>(), DA);
                this.parent.SetPersistentOutput("Post Count", this.resultPostCounts ?? new GH_Structure<GH_String>(), DA);
                this.parent.SetMetricsOutput(DA);

                var hasAnySummary = this.resultSummaries != null && this.resultSummaries.DataCount > 0;
                message = hasAnySummary ? "Topic(s) summarized" : "No summary available";
            }
        }
    }
}
