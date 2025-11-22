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
using SmartHopper.Infrastructure.AIModels;

namespace SmartHopper.Components.Knowledge
{
    /// <summary>
    /// Component that generates a concise AI summary of a McNeel Discourse forum post by ID.
    /// Uses the mcneel_forum_post_summarize tool, which in turn calls the configured AI provider/model.
    /// </summary>
    public class AIMcNeelForumPostSummarizeComponent : AIStatefulAsyncComponentBase
    {
        public override Guid ComponentGuid => new Guid("A6B8D7E2-2345-4F8A-9C10-3D4E5F6A7004");

        // protected override Bitmap Icon => Resources.context;

        public override GH_Exposure Exposure => GH_Exposure.secondary;

        protected override AICapability RequiredCapability => AICapability.Text2Text;

        public AIMcNeelForumPostSummarizeComponent()
            : base(
                  "AI McNeel Forum Post Summarize",
                  "AIMcNeelPostSumm",
                  "Generate a concise summary of a McNeel Discourse forum post by ID using the configured AI provider.",
                  "SmartHopper",
                  "Knowledge")
        {
            this.RunOnlyOnInputChanges = false;
        }

        protected override void RegisterAdditionalInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddIntegerParameter("Post Id", "P", "REQUIRED ID or list of IDs of the forum post(s) to summarize.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Instructions", "I", "Optional targeted summary instructions to focus on a specific question, target, or concern.", GH_ParamAccess.item, string.Empty);
        }

        protected override void RegisterAdditionalOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddIntegerParameter("Post Id", "P", "ID of the summarized forum post.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Summary", "S", "AI-generated summary of the forum post.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Author", "A", "Username of the post author.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Date", "D", "Creation date of the post.", GH_ParamAccess.tree);
        }

        protected override AsyncWorkerBase CreateWorker(Action<string> progressReporter)
        {
            return new AIMcNeelForumPostSummarizeWorker(this, this.AddRuntimeMessage);
        }

        private sealed class AIMcNeelForumPostSummarizeWorker : AsyncWorkerBase
        {
            private readonly AIMcNeelForumPostSummarizeComponent parent;
            private GH_Structure<GH_Integer> idsTree;
            private string instructions;
            private bool hasWork;

            private GH_Structure<GH_Integer> resultIds;
            private GH_Structure<GH_String> resultSummaries;
            private GH_Structure<GH_String> resultAuthors;
            private GH_Structure<GH_String> resultDates;

            public AIMcNeelForumPostSummarizeWorker(
                AIMcNeelForumPostSummarizeComponent parent,
                Action<GH_RuntimeMessageLevel, string> addRuntimeMessage)
                : base(parent, addRuntimeMessage)
            {
                this.parent = parent;
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

                dataCount = this.hasWork ? this.idsTree.PathCount : 0;
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

                    var resultTrees = await this.parent.RunDataTreeFunctionAsync<GH_Integer, GH_String>(
                        trees,
                        async branchInputs =>
                        {
                            var outputs = new Dictionary<string, List<GH_String>>
                            {
                                { "Id", new List<GH_String>() },
                                { "Summary", new List<GH_String>() },
                                { "Author", new List<GH_String>() },
                                { "Date", new List<GH_String>() },
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

                                var toolResult = await this.parent.CallAiToolAsync("mcneel_forum_post_summarize", parameters).ConfigureAwait(false);

                                if (toolResult == null)
                                {
                                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Tool 'mcneel_forum_post_summarize' returned no result.");
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
                                    // Backward compatibility: single summary at root
                                    var singleId = toolResult["id"]?.ToObject<int?>();
                                    var singleSummary = toolResult["summary"]?.ToString() ?? string.Empty;
                                    var singleAuthor = toolResult["username"]?.ToString() ?? string.Empty;
                                    var singleDate = toolResult["date"]?.ToString() ?? string.Empty;

                                    if (singleId.HasValue)
                                    {
                                        outputs["Id"].Add(new GH_String(singleId.Value.ToString()));
                                        outputs["Summary"].Add(new GH_String(singleSummary));
                                        outputs["Author"].Add(new GH_String(singleAuthor));
                                        outputs["Date"].Add(new GH_String(singleDate));
                                    }

                                    continue;
                                }

                                foreach (var item in summariesArray.OfType<JObject>())
                                {
                                    var idValue = item["id"]?.ToObject<int?>();
                                    var summaryValue = item["summary"]?.ToString() ?? string.Empty;
                                    var authorValue = item["username"]?.ToString() ?? string.Empty;
                                    var dateValue = item["date"]?.ToString() ?? string.Empty;

                                    if (idValue.HasValue)
                                    {
                                        outputs["Id"].Add(new GH_String(idValue.Value.ToString()));
                                        outputs["Summary"].Add(new GH_String(summaryValue));
                                        outputs["Author"].Add(new GH_String(authorValue));
                                        outputs["Date"].Add(new GH_String(dateValue));
                                    }
                                }
                            }

                            return outputs;
                        },
                        onlyMatchingPaths: false,
                        groupIdenticalBranches: false,
                        token: token).ConfigureAwait(false);

                    // Map result trees back to strongly-typed structures
                    this.resultIds = new GH_Structure<GH_Integer>();
                    this.resultSummaries = new GH_Structure<GH_String>();
                    this.resultAuthors = new GH_Structure<GH_String>();
                    this.resultDates = new GH_Structure<GH_String>();

                    if (resultTrees.TryGetValue("Id", out var idTree))
                    {
                        foreach (var path in idTree.Paths)
                        {
                            var branch = idTree.get_Branch(path);
                            foreach (var item in branch)
                            {
                                if (item is GH_String ghString && int.TryParse(ghString.Value, out int parsedId))
                                {
                                    this.resultIds.Append(new GH_Integer(parsedId), path);
                                }
                            }
                        }
                    }

                    if (resultTrees.TryGetValue("Summary", out var summaryTree))
                    {
                        this.resultSummaries = summaryTree;
                    }

                    if (resultTrees.TryGetValue("Author", out var authorTree))
                    {
                        this.resultAuthors = authorTree;
                    }

                    if (resultTrees.TryGetValue("Date", out var dateTree))
                    {
                        this.resultDates = dateTree;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[AIMcNeelForumPostSummarizeWorker] Error: {ex.Message}");
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
                }
            }

            public override void SetOutput(IGH_DataAccess DA, out string message)
            {
                this.parent.SetPersistentOutput("Post Id", this.resultIds ?? new GH_Structure<GH_Integer>(), DA);
                this.parent.SetPersistentOutput("Summary", this.resultSummaries ?? new GH_Structure<GH_String>(), DA);
                this.parent.SetPersistentOutput("Author", this.resultAuthors ?? new GH_Structure<GH_String>(), DA);
                this.parent.SetPersistentOutput("Date", this.resultDates ?? new GH_Structure<GH_String>(), DA);

                var hasAnySummary = this.resultSummaries != null && this.resultSummaries.DataCount > 0;
                message = hasAnySummary ? "Post(s) summarized" : "No summary available";
            }
        }
    }
}
