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
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AICall.Core.Returns;
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.Infrastructure.AITools;

namespace SmartHopper.Components.Knowledge
{
    public class McNeelForumSearchComponent : StatefulAsyncComponentBase
    {
        public override Guid ComponentGuid => new Guid("5F8F0D47-29D6-44D8-A5B1-2E7C6A9B1001");

        protected override Bitmap Icon => Resources.mcneelforumsearch;

        public override GH_Exposure Exposure => GH_Exposure.primary;

        public McNeelForumSearchComponent()
            : base(
                  "McNeel Forum Search",
                  "McNeelSearch",
                  "Search McNeel Discourse forum posts by query and return raw JSON results.",
                  "SmartHopper",
                  "Knowledge")
        {
            this.RunOnlyOnInputChanges = false;
        }

        protected override void RegisterAdditionalInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Query", "Q", "REQUIRED search query for the McNeel Discourse forum.", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Limit", "L", "Maximum number of posts to return (default: 10, max: 50).", GH_ParamAccess.item, 10);
        }

        protected override void RegisterAdditionalOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("McNeel Forum Posts", "McP", "List of JSON objects, one per matching forum post.", GH_ParamAccess.list);
        }

        protected override AsyncWorkerBase CreateWorker(Action<string> progressReporter)
        {
            return new McNeelForumSearchWorker(this, this.AddRuntimeMessage);
        }

        private sealed class McNeelForumSearchWorker : AsyncWorkerBase
        {
            private readonly McNeelForumSearchComponent parent;
            private string query;
            private int limit;
            private bool hasWork;

            private readonly List<GH_String> resultPosts = new List<GH_String>();

            public McNeelForumSearchWorker(
                McNeelForumSearchComponent parent,
                Action<GH_RuntimeMessageLevel, string> addRuntimeMessage)
                : base(parent, addRuntimeMessage)
            {
                this.parent = parent;
            }

            public override void GatherInput(IGH_DataAccess DA, out int dataCount)
            {
                string localQuery = null;
                DA.GetData(0, ref localQuery);
                int localLimit = 10;
                DA.GetData(1, ref localLimit);

                this.query = localQuery ?? string.Empty;
                this.limit = localLimit;

                this.hasWork = !string.IsNullOrWhiteSpace(this.query);
                if (!this.hasWork)
                {
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Query is required.");
                }

                Debug.WriteLine($"[McNeelForumSearchWorker] GatherInput - Query='{this.query}', Limit={this.limit}, HasWork={this.hasWork}");

                dataCount = this.hasWork ? 1 : 0;
            }

            public override async Task DoWorkAsync(CancellationToken token)
            {
                if (!this.hasWork)
                {
                    Debug.WriteLine("[McNeelForumSearchWorker] DoWorkAsync called with hasWork=false, exiting.");
                    return;
                }

                try
                {
                    Debug.WriteLine($"[McNeelForumSearchWorker] DoWorkAsync starting. Query='{this.query}', Limit={this.limit}");
                    var parameters = new JObject
                    {
                        ["query"] = this.query,
                        ["limit"] = this.limit,
                    };

                    var toolCallInteraction = new AIInteractionToolCall
                    {
                        Name = "mcneel_forum_search",
                        Arguments = parameters,
                        Agent = AIAgent.Assistant,
                    };

                    var toolCall = new AIToolCall
                    {
                        Endpoint = "mcneel_forum_search",
                    };

                    toolCall.FromToolCallInteraction(toolCallInteraction);

                    AIReturn aiResult = await toolCall.Exec().ConfigureAwait(false);
                    Debug.WriteLine($"[McNeelForumSearchWorker] Tool call completed. Success={aiResult?.Success}, Status={aiResult?.Status}, HasBody={aiResult?.Body != null}");
                    if (aiResult?.Body != null)
                    {
                        Debug.WriteLine($"[McNeelForumSearchWorker] Body interactions count={aiResult.Body.Interactions?.Count ?? 0}");
                    }

                    var toolResultInteraction = aiResult.Body?.GetLastInteraction(AIAgent.ToolResult) as AIInteractionToolResult;
                    if (toolResultInteraction == null)
                    {
                        Debug.WriteLine("[McNeelForumSearchWorker] toolResultInteraction is null.");
                    }
                    else
                    {
                        Debug.WriteLine($"[McNeelForumSearchWorker] toolResultInteraction.Result is {(toolResultInteraction.Result == null ? "null" : "non-null")}.");
                    }
                    var toolResult = toolResultInteraction?.Result;

                    if (toolResult == null)
                    {
                        this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Tool 'mcneel_forum_search' returned no result.");
                        return;
                    }

                    var resultsArray = toolResult["results"] as JArray ?? new JArray();
                    Debug.WriteLine($"[McNeelForumSearchWorker] Parsed results array. Count={resultsArray.Count}");
                    this.resultPosts.Clear();
                    foreach (var post in resultsArray)
                    {
                        this.resultPosts.Add(new GH_String(post?.ToString() ?? string.Empty));
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[McNeelForumSearchWorker] Error: {ex}");
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
                }
            }

            public override void SetOutput(IGH_DataAccess DA, out string message)
            {
                Debug.WriteLine($"[McNeelForumSearchWorker] SetOutput - resultPosts.Count={this.resultPosts.Count}");
                this.parent.SetPersistentOutput("McNeel Forum Posts", this.resultPosts, DA);
                message = this.resultPosts.Count == 0 ? "No search executed" : "Search completed";
            }
        }
    }
}
