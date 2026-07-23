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
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.ProviderSdk.AICall.Core.Base;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AICall.Core.Returns;
using SmartHopper.ProviderSdk.Diagnostics;

namespace SmartHopper.Components.Knowledge
{
    /// <summary>
    /// Searches any Discourse forum posts by query and returns raw JSON results.
    /// </summary>
    public class DiscourseSearchComponent : StatefulComponentBase
    {
        public override Guid ComponentGuid => new Guid("A0F1B2C3-D4E5-4F6A-9B0C-1D2E3F4A5B6C");

        protected override Bitmap Icon => Resources.discourseforumsearch;

        public override GH_Exposure Exposure => GH_Exposure.secondary;

        protected override ProcessingOptions ComponentProcessingOptions => new ProcessingOptions
        {
            Topology = ProcessingTopology.ItemGraft,
            OnlyMatchingPaths = false,
            GroupIdenticalBranches = true,
        };

        public DiscourseSearchComponent()
            : base(
                  "Discourse Search",
                  "DiscourseSearch",
                  "Search for Discourse forum posts by query string in any Discourse instance.",
                  "SmartHopper",
                  "Knowledge")
        {
            // Set RunOnlyOnInputChanges to false to ensure the component always runs when the Run parameter is true
            this.RunOnlyOnInputChanges = false;
        }

        protected override void RegisterAdditionalInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Base URL", "U", "REQUIRED Base URL of the Discourse forum (e.g., https://discourse.example.com).", GH_ParamAccess.item);
            pManager.AddTextParameter("Query", "Q", "REQUIRED search query or queries for the Discourse forum.", GH_ParamAccess.tree);
        }

        protected override void RegisterAdditionalOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Discourse Posts", "P", "Tree of JSON objects, one per matching forum post.", GH_ParamAccess.tree);
        }

        protected override AsyncWorkerBase CreateWorker(Action<string> progressReporter)
        {
            return new DiscourseSearchWorker(this, this.AddRuntimeMessage, this.ComponentProcessingOptions);
        }

        private sealed class DiscourseSearchWorker : AsyncWorkerBase
        {
            private readonly DiscourseSearchComponent parent;
            private readonly ProcessingOptions processingOptions;
            private string baseUrl;
            private Dictionary<string, GH_Structure<GH_String>> inputTrees;
            private bool hasWork;

            private GH_Structure<GH_String> resultPosts;

            public DiscourseSearchWorker(
                DiscourseSearchComponent parent,
                Action<GH_RuntimeMessageLevel, string> addRuntimeMessage,
                ProcessingOptions processingOptions)
                : base(parent, addRuntimeMessage)
            {
                this.parent = parent;
                this.processingOptions = processingOptions;
            }

            public override void GatherInput(IGH_DataAccess DA, out int dataCount)
            {
                string localBaseUrl = string.Empty;
                DA.GetData(0, ref localBaseUrl);

                var queryTree = new GH_Structure<GH_String>();
                DA.GetDataTree("Query", out queryTree);

                this.baseUrl = localBaseUrl ?? string.Empty;
                this.inputTrees = new Dictionary<string, GH_Structure<GH_String>>
                {
                    { "Query", queryTree ?? new GH_Structure<GH_String>() },
                };

                this.hasWork = !string.IsNullOrWhiteSpace(this.baseUrl) &&
                               queryTree != null &&
                               queryTree.DataCount > 0;
                if (!this.hasWork)
                {
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Base URL and Query are required.");
                }

                Debug.WriteLine($"[DiscourseSearchWorker] GatherInput - BaseUrl={this.baseUrl}, QueryTreeCount={queryTree?.DataCount ?? 0}, HasWork={this.hasWork}");

                dataCount = 0;
            }

            public override async Task DoWorkAsync(CancellationToken token)
            {
                if (!this.hasWork)
                {
                    Debug.WriteLine("[DiscourseSearchWorker] DoWorkAsync called with hasWork=false, exiting.");
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
                                { "DiscoursePosts", new List<GH_String>() },
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

                                Debug.WriteLine($"[DiscourseSearchWorker] DoWorkAsync starting. Query='{queryValue}'");
                                var parameters = new JObject
                                {
                                    ["base_url"] = this.baseUrl,
                                    ["query"] = queryValue,
                                };

                                var toolCallInteraction = new AIInteractionToolCall
                                {
                                    Name = "discourse_forum_search",
                                    Arguments = parameters,
                                    Agent = AIAgent.Assistant,
                                };

                                var toolCall = new AIToolCall
                                {
                                    Endpoint = "discourse_forum_search",
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
                                    Debug.WriteLine($"[DiscourseSearchWorker] Error executing tool: {ex}");
                                    this.CollectMessage(SHRuntimeMessageSeverity.Error, ex.Message);
                                    continue;
                                }

                                Debug.WriteLine($"[DiscourseSearchWorker] Tool call completed. Success={aiResult?.Success}, Status={aiResult?.Status}, HasBody={aiResult?.Body != null}");
                                if (aiResult?.Body != null)
                                {
                                    Debug.WriteLine($"[DiscourseSearchWorker] Body interactions count={aiResult.Body.Interactions?.Count ?? 0}");
                                }

                                var toolResult = ToolCallResult.FromAIReturn(aiResult);
                                if (toolResult.Result == null)
                                {
                                    this.CollectMessage(SHRuntimeMessageSeverity.Error, "Tool 'discourse_forum_search' returned no result.", SHRuntimeMessageOrigin.Tool);
                                    continue;
                                }

                                var resultsArray = toolResult["results"] as JArray ?? new JArray();
                                Debug.WriteLine($"[DiscourseSearchWorker] Parsed results array. Count={resultsArray.Count}");
                                foreach (var post in resultsArray)
                                {
                                    outputs["DiscoursePosts"].Add(new GH_String(post?.ToString() ?? string.Empty));
                                }
                            }

                            return outputs;
                        },
                        this.processingOptions,
                        token).ConfigureAwait(false);

                    this.resultPosts = new GH_Structure<GH_String>();
                    if (resultTrees.TryGetValue("DiscoursePosts", out var postsTree))
                    {
                        this.resultPosts = postsTree;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[DiscourseSearchWorker] Error: {ex}");
                    this.CollectMessage(SHRuntimeMessageSeverity.Error, ex.Message);
                }
            }

            public override void SetOutput(IGH_DataAccess DA, out string message)
            {
                var postsTree = this.resultPosts ?? new GH_Structure<GH_String>();
                Debug.WriteLine($"[DiscourseSearchWorker] SetOutput - resultPosts.DataCount={postsTree.DataCount}");
                this.parent.SetPersistentOutput("Discourse Posts", postsTree, DA);
                message = postsTree.DataCount == 0 ? "No search executed" : "Search completed";
            }
        }
    }
}