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
using Grasshopper.Kernel.Types;
using Newtonsoft.Json.Linq;
using SmartHopper.Components.Properties;
using SmartHopper.Core.ComponentBase;
using SmartHopper.Infrastructure.AIModels;

namespace SmartHopper.Components.Knowledge
{
    public class AIMcNeelForumSearchComponent : AIStatefulAsyncComponentBase
    {
        public override Guid ComponentGuid => new Guid("5F8F0D47-29D6-44D8-A5B1-2E7C6A9B1001");

        protected override Bitmap Icon => Resources.context;

        public override GH_Exposure Exposure => GH_Exposure.primary;

        protected override AICapability RequiredCapability => AICapability.Text2Text;

        public AIMcNeelForumSearchComponent()
            : base(
                  "McNeel Forum Search",
                  "McNeelSearch",
                  "Search McNeel Discourse forum posts by query and optionally get AI summaries.",
                  "SmartHopper",
                  "Knowladge")
        {
            this.RunOnlyOnInputChanges = false;
        }

        protected override void RegisterAdditionalInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Query", "Q", "REQUIRED search query for the McNeel Discourse forum.", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Limit", "L", "Maximum number of posts to return (default: 10, max: 50).", GH_ParamAccess.item, 10);
            pManager.AddBooleanParameter("Summarize", "S", "Whether to generate AI summaries for the posts (applied to first 5 results).", GH_ParamAccess.item, false);
        }

        protected override void RegisterAdditionalOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Query", "Q", "Echo of the query that was executed.", GH_ParamAccess.item);
            pManager.AddTextParameter("Results", "R", "List of JSON objects, one per matching forum post.", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Count", "C", "Number of posts returned.", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Summarized", "Sm", "Number of posts for which summaries were generated.", GH_ParamAccess.item);
        }

        protected override AsyncWorkerBase CreateWorker(Action<string> progressReporter)
        {
            return new AIMcNeelForumSearchWorker(this, this.AddRuntimeMessage);
        }

        private sealed class AIMcNeelForumSearchWorker : AsyncWorkerBase
        {
            private readonly AIMcNeelForumSearchComponent parent;
            private string query;
            private int limit;
            private bool summarize;
            private bool hasWork;

            private string resultQuery;
            private readonly List<GH_String> resultPosts = new List<GH_String>();
            private int resultCount;
            private int resultSummarized;

            public AIMcNeelForumSearchWorker(
                AIMcNeelForumSearchComponent parent,
                Action<GH_RuntimeMessageLevel, string> addRuntimeMessage)
                : base(parent, addRuntimeMessage)
            {
                this.parent = parent;
            }

            public override void GatherInput(IGH_DataAccess DA, out int dataCount)
            {
                string localQuery = null;
                DA.GetData("Query", ref localQuery);
                int localLimit = 10;
                DA.GetData("Limit", ref localLimit);
                bool localSummarize = false;
                DA.GetData("Summarize", ref localSummarize);

                this.query = localQuery ?? string.Empty;
                this.limit = localLimit;
                this.summarize = localSummarize;

                this.hasWork = !string.IsNullOrWhiteSpace(this.query);
                if (!this.hasWork)
                {
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Query is required.");
                }

                dataCount = this.hasWork ? 1 : 0;
            }

            public override async Task DoWorkAsync(CancellationToken token)
            {
                if (!this.hasWork)
                {
                    return;
                }

                try
                {
                    var parameters = new JObject
                    {
                        ["query"] = this.query,
                        ["limit"] = this.limit,
                        ["summarize"] = this.summarize,
                    };

                    var toolResult = await this.parent.CallAiToolAsync("mcneel_forum_search", parameters).ConfigureAwait(false);

                    if (toolResult == null)
                    {
                        this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Tool 'mcneel_forum_search' returned no result.");
                        return;
                    }

                    this.resultQuery = toolResult["query"]?.ToString() ?? this.query;

                    var resultsArray = toolResult["results"] as JArray ?? new JArray();
                    this.resultPosts.Clear();
                    foreach (var post in resultsArray)
                    {
                        this.resultPosts.Add(new GH_String(post?.ToString() ?? string.Empty));
                    }

                    this.resultCount = toolResult["count"]?.ToObject<int?>() ?? resultsArray.Count;
                    this.resultSummarized = toolResult["summarized"]?.ToObject<int?>() ?? 0;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[AIMcNeelForumSearchWorker] Error: {ex.Message}");
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
                }
            }

            public override void SetOutput(IGH_DataAccess DA, out string message)
            {
                DA.SetData("Query", this.resultQuery);
                DA.SetDataList("Results", this.resultPosts);
                DA.SetData("Count", this.resultCount);
                DA.SetData("Summarized", this.resultSummarized);
                message = string.IsNullOrWhiteSpace(this.resultQuery) ? "No search executed" : "Search completed";
            }
        }
    }
}
