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

namespace SmartHopper.Components.Knowledge
{
    /// <summary>
    /// Component that generates a concise AI summary of a McNeel Discourse forum topic by ID.
    /// Uses the mcneel_forum_topic_summarize tool, which in turn calls the configured AI provider/model.
    /// </summary>
    public class AIMcNeelForumTopicSummarizeComponent : AIStatefulAsyncComponentBase
    {
        public override Guid ComponentGuid => new Guid("B3C4D5E6-7890-4ABC-8123-4567DEF89012");

        protected override Bitmap Icon => Resources.mcneeltopicsummarize;

        public override GH_Exposure Exposure => GH_Exposure.primary;

        protected override AICapability RequiredCapability => AICapability.Text2Text;

        public AIMcNeelForumTopicSummarizeComponent()
            : base(
                  "AI McNeelForum Topic Summarize",
                  "AIMcNeelTopicSumm",
                  "Generate a concise summary of a McNeel Discourse forum topic by ID using the configured AI provider.",
                  "SmartHopper",
                  "Knowledge")
        {
            this.RunOnlyOnInputChanges = false;
        }

        protected override void RegisterAdditionalInputParams(GH_InputParamManager pManager)
        {
            pManager.AddIntegerParameter("Topic Id", "T", "REQUIRED ID or list of IDs of the forum topic(s) to summarize.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Instructions", "I", "Optional targeted summary instructions to focus on a specific question, target, or concern.", GH_ParamAccess.item, string.Empty);
        }

        protected override void RegisterAdditionalOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Summary", "S", "AI-generated summary of the forum topic.", GH_ParamAccess.tree);
        }

        protected override AsyncWorkerBase CreateWorker(Action<string> progressReporter)
        {
            return new AIMcNeelForumTopicSummarizeWorker(this, this.AddRuntimeMessage, ComponentProcessingOptions);
        }

        private sealed class AIMcNeelForumTopicSummarizeWorker : AsyncWorkerBase
        {
            private readonly AIMcNeelForumTopicSummarizeComponent parent;
            private readonly ProcessingOptions processingOptions;
            private GH_Structure<GH_Integer> idsTree;
            private string instructions;
            private bool hasWork;

            private GH_Structure<GH_String> resultSummaries;

            public AIMcNeelForumTopicSummarizeWorker(
                AIMcNeelForumTopicSummarizeComponent parent,
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

                                    var toolResult = await this.parent.CallAiToolAsync("mcneel_forum_topic_summarize", parameters).ConfigureAwait(false);

                                    if (toolResult == null)
                                    {
                                        this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Tool 'mcneel_forum_topic_summarize' returned no result.");
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
                    if (resultTrees.TryGetValue("Summary", out var summaryTree))
                    {
                        this.resultSummaries = summaryTree;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[AIMcNeelForumTopicSummarizeWorker] Error: {ex.Message}");
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
                }
            }

            public override void SetOutput(IGH_DataAccess DA, out string message)
            {
                this.parent.SetPersistentOutput("Summary", this.resultSummaries ?? new GH_Structure<GH_String>(), DA);

                var hasAnySummary = this.resultSummaries != null && this.resultSummaries.DataCount > 0;
                message = hasAnySummary ? "Topic(s) summarized" : "No summary available";
            }
        }
    }
}
