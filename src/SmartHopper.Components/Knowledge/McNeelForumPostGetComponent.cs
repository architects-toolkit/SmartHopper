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
    public class McNeelForumPostGetComponent : StatefulComponentBase
    {
        public override Guid ComponentGuid => new Guid("7C1B9A33-0177-4A60-9C08-9F8A1E4F2002");

        protected override Bitmap Icon => Resources.mcneelpostget;

        public override GH_Exposure Exposure => GH_Exposure.secondary;

        public McNeelForumPostGetComponent()
            : base(
                  "McNeelForum Post Get",
                  "McNeelPostGet",
                  "Retrieve a full McNeel Discourse forum post by its numeric ID.",
                  "SmartHopper",
                  "Knowledge")
        {
            this.RunOnlyOnInputChanges = false;
        }

        protected override void RegisterAdditionalInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddIntegerParameter("Id", "I", "REQUIRED ID or list of IDs of the forum post(s) to fetch.", GH_ParamAccess.tree);
        }

        protected override void RegisterAdditionalOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("McNeelForum Post", "McP", "JSON object representing the full forum post as returned by the tool.", GH_ParamAccess.tree);
        }

        protected override AsyncWorkerBase CreateWorker(Action<string> progressReporter)
        {
            return new McNeelForumGetPostWorker(this, this.AddRuntimeMessage, ComponentProcessingOptions);
        }

        private sealed class McNeelForumGetPostWorker : AsyncWorkerBase
        {
            private readonly McNeelForumPostGetComponent parent;
            private readonly ProcessingOptions processingOptions;
            private GH_Structure<GH_Integer> idsTree;
            private bool hasWork;

            private GH_Structure<GH_String> resultPosts;

            public McNeelForumGetPostWorker(
                McNeelForumPostGetComponent parent,
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

                this.idsTree = localIdsTree ?? new GH_Structure<GH_Integer>();
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
                                        ["id"] = id,
                                    };

                                    var toolCallInteraction = new AIInteractionToolCall
                                    {
                                        Name = "mcneel_forum_post_get",
                                        Arguments = parameters,
                                        Agent = AIAgent.Assistant,
                                    };

                                    var toolCall = new AIToolCall
                                    {
                                        Endpoint = "mcneel_forum_post_get",
                                    };

                                    toolCall.FromToolCallInteraction(toolCallInteraction);
                                    toolCall.SkipMetricsValidation = true;

                                    AIReturn aiResult = await toolCall.Exec().ConfigureAwait(false);
                                    var toolResultInteraction = aiResult.Body?.GetLastInteraction(AIAgent.ToolResult) as AIInteractionToolResult;
                                    var toolResult = toolResultInteraction?.Result;

                                    if (toolResult == null)
                                    {
                                        this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Tool 'mcneel_forum_post_get' returned no result.");
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
                    Debug.WriteLine($"[AIMcNeelForumGetPostWorker] Error: {ex.Message}");
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
                }
            }

            public override void SetOutput(IGH_DataAccess DA, out string message)
            {
                this.parent.SetPersistentOutput("McNeelForum Post", this.resultPosts ?? new GH_Structure<GH_String>(), DA);

                var hasAnyPost = this.resultPosts != null && this.resultPosts.DataCount > 0;
                message = hasAnyPost ? "Post(s) retrieved" : "No post retrieved";
            }
        }
    }
}
