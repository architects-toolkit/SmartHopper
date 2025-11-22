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
    public class McNeelForumGetPostComponent : StatefulAsyncComponentBase
    {
        public override Guid ComponentGuid => new Guid("7C1B9A33-0177-4A60-9C08-9F8A1E4F2002");

        protected override Bitmap Icon => Resources.context;

        public override GH_Exposure Exposure => GH_Exposure.primary;

        public McNeelForumGetPostComponent()
            : base(
                  "McNeel Forum Get Post",
                  "McNeelGetPost",
                  "Retrieve a full McNeel Discourse forum post by its numeric ID.",
                  "SmartHopper",
                  "Knowladge")
        {
            this.RunOnlyOnInputChanges = false;
        }

        protected override void RegisterAdditionalInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddIntegerParameter("Id", "I", "REQUIRED ID of the forum post to fetch.", GH_ParamAccess.item);
        }

        protected override void RegisterAdditionalOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddIntegerParameter("Id", "I", "ID of the fetched forum post.", GH_ParamAccess.item);
            pManager.AddTextParameter("Post", "P", "JSON object representing the full forum post as returned by the tool.", GH_ParamAccess.item);
        }

        protected override AsyncWorkerBase CreateWorker(Action<string> progressReporter)
        {
            return new McNeelForumGetPostWorker(this, this.AddRuntimeMessage);
        }

        private sealed class McNeelForumGetPostWorker : AsyncWorkerBase
        {
            private readonly McNeelForumGetPostComponent parent;
            private int id;
            private bool hasWork;

            private int resultId;
            private string resultPostJson;

            public McNeelForumGetPostWorker(
                McNeelForumGetPostComponent parent,
                Action<GH_RuntimeMessageLevel, string> addRuntimeMessage)
                : base(parent, addRuntimeMessage)
            {
                this.parent = parent;
            }

            public override void GatherInput(IGH_DataAccess DA, out int dataCount)
            {
                int localId = 0;
                DA.GetData("Id", ref localId);

                this.id = localId;
                this.hasWork = this.id > 0;
                if (!this.hasWork)
                {
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Id must be a positive integer.");
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
                        ["id"] = this.id,
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

                    AIReturn aiResult = await toolCall.Exec().ConfigureAwait(false);
                    var toolResultInteraction = aiResult.Body?.GetLastInteraction(AIAgent.ToolResult) as AIInteractionToolResult;
                    var toolResult = toolResultInteraction?.Result;

                    if (toolResult == null)
                    {
                        this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Tool 'mcneel_forum_post_get' returned no result.");
                        return;
                    }

                    this.resultId = toolResult["id"]?.ToObject<int?>() ?? this.id;
                    this.resultPostJson = toolResult["post"]?.ToString() ?? string.Empty;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[AIMcNeelForumGetPostWorker] Error: {ex.Message}");
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
                }
            }

            public override void SetOutput(IGH_DataAccess DA, out string message)
            {
                DA.SetData("Id", this.resultId);
                DA.SetData("Post", this.resultPostJson);
                message = this.resultId > 0 ? "Post retrieved" : "No post retrieved";
            }
        }
    }
}
