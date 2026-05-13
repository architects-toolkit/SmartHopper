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
using SmartHopper.Infrastructure.AICall.Core.Returns;
using SmartHopper.Infrastructure.AICall.Tools;

namespace SmartHopper.Components.Knowledge
{
    public class McNeelForumSearchComponent : StatefulComponentBase
    {
        public override Guid ComponentGuid => new Guid("5F8F0D47-29D6-44D8-A5B1-2E7C6A9B1001");

        protected override Bitmap Icon => Resources.mcneelforumsearch;

        public override GH_Exposure Exposure => GH_Exposure.primary;

        protected override ProcessingOptions ComponentProcessingOptions => new ProcessingOptions
        {
            Topology = ProcessingTopology.ItemGraft,
            OnlyMatchingPaths = false,
            GroupIdenticalBranches = true,
        };

        public McNeelForumSearchComponent()
            : base(
                  "McNeelForum Search",
                  "McNeelSearch",
                  "Search McNeel Discourse forum posts by query and return raw JSON results.",
                  "SmartHopper",
                  "Knowledge")
        {
            this.RunOnlyOnInputChanges = false;
        }

        protected override void RegisterAdditionalInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Query", "Q", "REQUIRED search query or queries for the McNeel Discourse forum.", GH_ParamAccess.tree);
            pManager.AddIntegerParameter("Limit", "L", "Maximum number of posts to return per query (default: 10, max: 50).", GH_ParamAccess.item, 10);
        }

        protected override void RegisterAdditionalOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("McNeelForum Posts", "McP", "Tree of JSON objects, one per matching forum post.", GH_ParamAccess.tree);
        }

        protected override AsyncWorkerBase CreateWorker(Action<string> progressReporter)
        {
            return new McNeelForumSearchWorker(this, this.AddRuntimeMessage, ComponentProcessingOptions);
        }

        private sealed class McNeelForumSearchWorker : AsyncWorkerBase
        {
            private readonly McNeelForumSearchComponent parent;
            private readonly ProcessingOptions processingOptions;
            private Dictionary<string, GH_Structure<GH_String>> inputTrees;
            private int limit;
            private bool hasWork;

            private GH_Structure<GH_String> resultPosts;

            public McNeelForumSearchWorker(
                McNeelForumSearchComponent parent,
                Action<GH_RuntimeMessageLevel, string> addRuntimeMessage,
                ProcessingOptions processingOptions)
                : base(parent, addRuntimeMessage)
            {
                this.parent = parent;
                this.processingOptions = processingOptions;
            }

            public override void GatherInput(IGH_DataAccess DA, out int dataCount)
            {
                var queryTree = new GH_Structure<GH_String>();
                DA.GetDataTree("Query", out queryTree);
                int localLimit = 10;
                DA.GetData(1, ref localLimit);

                this.inputTrees = new Dictionary<string, GH_Structure<GH_String>>
                {
                    { "Query", queryTree ?? new GH_Structure<GH_String>() },
                };

                this.limit = localLimit;

                this.hasWork = queryTree != null && queryTree.DataCount > 0;
                if (!this.hasWork)
                {
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Query is required.");
                }

                Debug.WriteLine($"[McNeelForumSearchWorker] GatherInput - QueryTreeCount={queryTree?.DataCount ?? 0}, Limit={this.limit}, HasWork={this.hasWork}");

                dataCount = 0;
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
                    var resultTrees = await this.parent.RunProcessingAsync<GH_String, GH_String>(
                        this.inputTrees,
                        async branchInputs =>
                        {
                            var outputs = new Dictionary<string, List<GH_String>>
                            {
                                { "McNeelForumPosts", new List<GH_String>() },
                            };

                            if (!branchInputs.TryGetValue("Query", out var queries) || queries == null || queries.Count == 0)
                            {
                                return outputs;
                            }

                            foreach (var ghQuery in queries)
                            {
                                var queryValue = ghQuery?.Value ?? string.Empty;
                                if (string.IsNullOrWhiteSpace(queryValue))
                                {
                                    continue;
                                }

                                Debug.WriteLine($"[McNeelForumSearchWorker] DoWorkAsync starting. Query='{queryValue}', Limit={this.limit}");
                                var parameters = new JObject
                                {
                                    ["query"] = queryValue,
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
                                toolCall.SkipMetricsValidation = true;

                                AIReturn aiResult;
                                try
                                {
                                    aiResult = await toolCall.Exec().ConfigureAwait(false);
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine($"[McNeelForumSearchWorker] Error executing tool: {ex}");
                                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
                                    continue;
                                }

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
                                    continue;
                                }

                                var resultsArray = toolResult["results"] as JArray ?? new JArray();
                                Debug.WriteLine($"[McNeelForumSearchWorker] Parsed results array. Count={resultsArray.Count}");
                                foreach (var post in resultsArray)
                                {
                                    outputs["McNeelForumPosts"].Add(new GH_String(post?.ToString() ?? string.Empty));
                                }
                            }

                            return outputs;
                        },
                        this.processingOptions,
                        token).ConfigureAwait(false);

                    this.resultPosts = new GH_Structure<GH_String>();
                    if (resultTrees.TryGetValue("McNeelForumPosts", out var postsTree))
                    {
                        this.resultPosts = postsTree;
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
                var postsTree = this.resultPosts ?? new GH_Structure<GH_String>();
                Debug.WriteLine($"[McNeelForumSearchWorker] SetOutput - resultPosts.DataCount={postsTree.DataCount}");
                this.parent.SetPersistentOutput("McNeelForum Posts", postsTree, DA);
                message = postsTree.DataCount == 0 ? "No search executed" : "Search completed";
            }
        }
    }
}
