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
    /// Retrieves a full Discourse forum post by its numeric ID from any Discourse instance.
    /// </summary>
    public class DiscoursePostGetComponent : StatefulComponentBase
    {
        public override Guid ComponentGuid => new Guid("8D2C5A1E-3B4F-4C5D-8A9E-0F1A2B3C4D5E");

        protected override Bitmap Icon => Resources.discoursepostget;

        public override GH_Exposure Exposure => GH_Exposure.secondary;

        public DiscoursePostGetComponent()
            : base(
                  "Discourse Post Get",
                  "DiscoursePostGet",
                  "Retrieve a full Discourse forum post by its numeric ID from any Discourse instance.",
                  "SmartHopper",
                  "Knowledge")
        {
        }

        protected override void RegisterAdditionalInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Base URL", "U", "REQUIRED Base URL of the Discourse forum (e.g., https://discourse.example.com).", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Id", "I", "REQUIRED ID or list of IDs of the forum post(s) to fetch.", GH_ParamAccess.tree);
        }

        protected override void RegisterAdditionalOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Discourse Post", "P", "JSON object representing the full forum post as returned by the tool.", GH_ParamAccess.tree);
        }

        protected override AsyncWorkerBase CreateWorker(Action<string> progressReporter)
        {
            return new DiscoursePostGetWorker(this, this.AddRuntimeMessage, this.ComponentProcessingOptions);
        }

        private sealed class DiscoursePostGetWorker : AsyncWorkerBase
        {
            private readonly DiscoursePostGetComponent parent;
            private readonly ProcessingOptions processingOptions;
            private string baseUrl;
            private GH_Structure<GH_Integer> idsTree;
            private bool hasWork;

            private GH_Structure<GH_String> resultPosts;

            public DiscoursePostGetWorker(
                DiscoursePostGetComponent parent,
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

                var localIdsTree = new GH_Structure<GH_Integer>();
                DA.GetDataTree(1, out localIdsTree);

                this.baseUrl = localBaseUrl ?? string.Empty;
                this.idsTree = localIdsTree ?? new GH_Structure<GH_Integer>();
                this.hasWork = !string.IsNullOrWhiteSpace(this.baseUrl) &&
                               this.idsTree != null &&
                               this.idsTree.PathCount > 0 &&
                               this.idsTree.DataCount > 0;
                if (!this.hasWork)
                {
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Base URL and at least one valid Id are required.");
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
                                { "Id", new List<GH_String>() },
                                { "Post", new List<GH_String>() },
                            };

                            foreach (var kvp in branchInputs)
                            {
                                var ids = kvp.Value;

                                foreach (var ghId in ids)
                                {
                                    if (ghId == null || ghId.Value <= 0)
                                    {
                                        continue;
                                    }

                                    int id = ghId.Value;

                                    var parameters = new JObject
                                    {
                                        ["base_url"] = this.baseUrl,
                                        ["id"] = id,
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
                                        this.CollectMessage(SHRuntimeMessageSeverity.Error, "Tool 'discourse_forum_post_get' returned no result.", SHRuntimeMessageOrigin.Tool);
                                        continue;
                                    }

                                    string postJson = toolResult["post"]?.ToString() ?? string.Empty;

                                    outputs["Post"].Add(new GH_String(postJson));
                                }
                            }

                            return outputs;
                        },
                        this.processingOptions,
                        token).ConfigureAwait(false);

                    this.resultPosts = new GH_Structure<GH_String>();

                    if (resultTrees.TryGetValue("Post", out var postTree))
                    {
                        this.resultPosts = postTree;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[DiscoursePostGetWorker] Error: {ex.Message}");
                    this.CollectMessage(SHRuntimeMessageSeverity.Error, ex.Message);
                }
            }

            public override void SetOutput(IGH_DataAccess DA, out string message)
            {
                this.parent.SetPersistentOutput("Discourse Post", this.resultPosts ?? new GH_Structure<GH_String>(), DA);

                var hasAnyPost = this.resultPosts != null && this.resultPosts.DataCount > 0;
                message = hasAnyPost ? "Post(s) retrieved" : "No post retrieved";
            }
        }
    }
}
