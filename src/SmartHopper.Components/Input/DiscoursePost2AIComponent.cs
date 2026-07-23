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
using SmartHopper.Core.Models;
using SmartHopper.Core.Types;
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.ProviderSdk.AICall.Core.Base;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AICall.Core.Returns;
using SmartHopper.ProviderSdk.Diagnostics;

namespace SmartHopper.Components.Input
{
    /// <summary>
    /// Retrieves a Discourse forum post by ID and wraps it into an AIInputPayload for AI processing.
    /// Uses the discourse_forum_post_get AI tool to fetch post content.
    /// </summary>
    public class DiscoursePost2AIComponent : StatefulComponentBase
    {
        public override Guid ComponentGuid => new Guid("E03028FA-A7D4-41F0-A010-F6EEDE085594");

        public override GH_Exposure Exposure => GH_Exposure.septenary;

        protected override Bitmap Icon => Resources.toaidiscoursepost;

        public DiscoursePost2AIComponent()
            : base(
                  "Discourse Post to AI",
                  "DiscoursePost2AI",
                  "Retrieves a Discourse forum post by ID and wraps it into an AIInputPayload for AI processing.",
                  "SmartHopper",
                  "B. Input")
        {
        }

        protected override void RegisterAdditionalInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Base URL", "U", "Base URL(s) of the Discourse forum (e.g., https://discourse.example.com).", GH_ParamAccess.tree);
            pManager.AddIntegerParameter("Post ID", "P", "ID(s) of the forum post(s) to retrieve.", GH_ParamAccess.tree);
        }

        protected override void RegisterAdditionalOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddParameter(new AIInputPayloadParameter(), "Input >", ">", "AIInputPayload(s) containing the retrieved post content.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Post JSON", "J", "Retrieved post content as JSON.", GH_ParamAccess.tree);
        }

        protected override ProcessingOptions ComponentProcessingOptions => new ProcessingOptions
        {
            Topology = ProcessingTopology.BranchToBranch,
            OnlyMatchingPaths = false,
            GroupIdenticalBranches = true,
        };

        protected override AsyncWorkerBase CreateWorker(Action<string> progressReporter)
        {
            return new DiscoursePost2AIWorker(this, this.AddRuntimeMessage, this.ComponentProcessingOptions);
        }

        private sealed class DiscoursePost2AIWorker : AsyncWorkerBase
        {
            private readonly DiscoursePost2AIComponent parent;
            private readonly ProcessingOptions processingOptions;
            private Dictionary<string, GH_Structure<GH_String>> inputTrees;
            private Dictionary<string, GH_Structure<IGH_Goo>> result;

            public DiscoursePost2AIWorker(DiscoursePost2AIComponent parent, Action<GH_RuntimeMessageLevel, string> addRuntimeMessage, ProcessingOptions options)
                : base(parent, addRuntimeMessage)
            {
                this.parent = parent;
                this.processingOptions = options;
                this.result = new Dictionary<string, GH_Structure<IGH_Goo>>
                {
                    { "Input >", new GH_Structure<IGH_Goo>() },
                    { "Post JSON", new GH_Structure<IGH_Goo>() },
                };
            }

            public override void GatherInput(IGH_DataAccess DA, out int dataCount)
            {
                this.inputTrees = new Dictionary<string, GH_Structure<GH_String>>();

                var baseUrlTree = new GH_Structure<GH_String>();
                DA.GetDataTree("Base URL", out baseUrlTree);

                var postTree = new GH_Structure<GH_Integer>();
                DA.GetDataTree("Post ID", out postTree);

                this.inputTrees["BaseUrl"] = baseUrlTree;
                this.inputTrees["PostId"] = ConvertIntTreeToString(postTree, "0");

                dataCount = 0;
            }

            private static GH_Structure<GH_String> ConvertIntTreeToString(GH_Structure<GH_Integer> intTree, string defaultValue)
            {
                var result = new GH_Structure<GH_String>();
                foreach (var path in intTree.Paths)
                {
                    var branch = intTree.get_Branch(path);
                    if (branch != null && branch.Count > 0)
                    {
                        var firstInt = branch[0] as GH_Integer;
                        result.Append(new GH_String(firstInt?.Value.ToString() ?? defaultValue), path);
                    }
                    else
                    {
                        result.Append(new GH_String(defaultValue), path);
                    }
                }

                return result;
            }

            public override async Task DoWorkAsync(CancellationToken token)
            {
                try
                {
                    this.result = await this.parent.RunProcessingAsync(
                        this.inputTrees,
                        async (branches) =>
                        {
                            return await this.ProcessBranches(branches).ConfigureAwait(false);
                        },
                        this.processingOptions,
                        token).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[DiscoursePost2AIWorker] Error: {ex.Message}");
                    this.CollectMessage(SHRuntimeMessageSeverity.Error, $"Error retrieving posts: {ex.Message}");
                }
            }

            private async Task<Dictionary<string, List<IGH_Goo>>> ProcessBranches(Dictionary<string, List<GH_String>> branches)
            {
                var outputs = new Dictionary<string, List<IGH_Goo>>
                {
                    { "Input >", new List<IGH_Goo>() },
                    { "Post JSON", new List<IGH_Goo>() },
                };

                var baseUrls = branches["BaseUrl"];
                var postIds = branches["PostId"];

                // Normalize branch lengths to handle mismatched input trees
                var normalizedLists = DataTreeProcessor.NormalizeBranchLengths(new List<List<GH_String>> { baseUrls, postIds });
                baseUrls = normalizedLists[0];
                postIds = normalizedLists[1];

                for (int i = 0; i < postIds.Count; i++)
                {
                    string baseUrl = baseUrls[i]?.Value;
                    int postId = int.TryParse(postIds[i]?.Value, out var pid) ? pid : 0;

                    if (string.IsNullOrWhiteSpace(baseUrl))
                    {
                        this.CollectMessage(SHRuntimeMessageSeverity.Warning, "Base URL is required.");
                        outputs["Input >"].Add(new GH_AIInputPayload(null));
                        outputs["Post JSON"].Add(new GH_String(string.Empty));
                        continue;
                    }

                    if (postId <= 0)
                    {
                        this.CollectMessage(SHRuntimeMessageSeverity.Warning, $"Invalid Post ID: {postIds[i]?.Value}");
                        outputs["Input >"].Add(new GH_AIInputPayload(null));
                        outputs["Post JSON"].Add(new GH_String(string.Empty));
                        continue;
                    }

                    try
                    {
                        var parameters = new JObject
                        {
                            ["base_url"] = baseUrl,
                            ["id"] = postId,
                        };

                        var toolCallInteraction = new AIInteractionToolCall
                        {
                            Name = "discourse_forum_post_get",
                            Arguments = parameters,
                            Agent = AIAgent.Assistant,
                        };

                        var toolCall = new AIToolCall
                        {
                            Endpoint = "discourse_forum_post_get",
                        };

                        toolCall.FromToolCallInteraction(toolCallInteraction);
                        toolCall.SkipMetricsValidation = true;

                        AIReturn aiResult = await toolCall.Exec().ConfigureAwait(false);
                        var toolResult = ToolCallResult.FromAIReturn(aiResult);

                        if (toolResult.Result == null)
                        {
                            this.CollectMessage(SHRuntimeMessageSeverity.Warning, $"Tool returned no result for post ID: {postId}", SHRuntimeMessageOrigin.Tool);
                            outputs["Input >"].Add(new GH_AIInputPayload(null));
                            outputs["Post JSON"].Add(new GH_String(string.Empty));
                            continue;
                        }

                        string json = toolResult["post"]?.ToString() ?? string.Empty;
                        AIInputPayload payload = null;

                        if (!string.IsNullOrWhiteSpace(json))
                        {
                            payload = AIInputPayload.FromText(json);
                        }

                        outputs["Input >"].Add(new GH_AIInputPayload(payload));
                        outputs["Post JSON"].Add(new GH_String(json));
                    }
                    catch (Exception ex)
                    {
                        this.CollectMessage(SHRuntimeMessageSeverity.Warning, $"Error retrieving post {postId}: {ex.Message}");
                        outputs["Input >"].Add(new GH_AIInputPayload(null));
                        outputs["Post JSON"].Add(new GH_String(string.Empty));
                    }
                }

                return outputs;
            }

            public override void SetOutput(IGH_DataAccess DA, out string message)
            {
                if (this.result.TryGetValue("Input >", out var payloadTree) && payloadTree != null)
                {
                    this.parent.SetPersistentOutput("Input >", payloadTree, DA);
                }

                if (this.result.TryGetValue("Post JSON", out var jsonTree) && jsonTree != null)
                {
                    this.parent.SetPersistentOutput("Post JSON", jsonTree, DA);
                }

                int successCount = 0;
                if (this.result.TryGetValue("Post JSON", out var tree) && tree != null)
                {
                    foreach (var path in tree.Paths)
                    {
                        var branch = tree.get_Branch(path);
                        if (branch != null)
                        {
                            foreach (var item in branch)
                            {
                                if (item is GH_String gs && !string.IsNullOrWhiteSpace(gs.Value))
                                {
                                    successCount++;
                                }
                            }
                        }
                    }
                }

                int totalCount = this.inputTrees?.Values.FirstOrDefault()?.DataCount ?? 0;
                message = $"Retrieved {successCount}/{totalCount} post(s)";
            }
        }
    }
}