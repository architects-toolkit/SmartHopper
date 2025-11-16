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

        protected override Bitmap Icon => Resources.context;

        public override GH_Exposure Exposure => GH_Exposure.secondary;

        protected override AICapability RequiredCapability => AICapability.Text2Text;

        public AIMcNeelForumPostSummarizeComponent()
            : base(
                  "McNeel Forum Post Summarize",
                  "McNeelPostSumm",
                  "Generate a concise summary of a McNeel Discourse forum post by ID using the configured AI provider.",
                  "SmartHopper",
                  "Knowladge")
        {
            this.RunOnlyOnInputChanges = false;
        }

        protected override void RegisterAdditionalInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddIntegerParameter("Id", "I", "REQUIRED ID of the forum post to summarize.", GH_ParamAccess.item);
        }

        protected override void RegisterAdditionalOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddIntegerParameter("Id", "I", "ID of the summarized forum post.", GH_ParamAccess.item);
            pManager.AddTextParameter("Summary", "S", "AI-generated summary of the forum post.", GH_ParamAccess.item);
            pManager.AddTextParameter("Author", "A", "Username of the post author.", GH_ParamAccess.item);
            pManager.AddTextParameter("Date", "D", "Creation date of the post.", GH_ParamAccess.item);
        }

        protected override AsyncWorkerBase CreateWorker(Action<string> progressReporter)
        {
            return new AIMcNeelForumPostSummarizeWorker(this, this.AddRuntimeMessage);
        }

        private sealed class AIMcNeelForumPostSummarizeWorker : AsyncWorkerBase
        {
            private readonly AIMcNeelForumPostSummarizeComponent parent;
            private int id;
            private bool hasWork;

            private int resultId;
            private string resultSummary;
            private string resultAuthor;
            private string resultDate;

            public AIMcNeelForumPostSummarizeWorker(
                AIMcNeelForumPostSummarizeComponent parent,
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
                        ["contextFilter"] = "-*",
                    };

                    var toolResult = await this.parent.CallAiToolAsync("mcneel_forum_post_summarize", parameters).ConfigureAwait(false);

                    if (toolResult == null)
                    {
                        this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Tool 'mcneel_forum_post_summarize' returned no result.");
                        return;
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

                        return;
                    }

                    this.resultId = toolResult["id"]?.ToObject<int?>() ?? this.id;
                    this.resultSummary = toolResult["summary"]?.ToString() ?? string.Empty;
                    this.resultAuthor = toolResult["username"]?.ToString() ?? string.Empty;
                    this.resultDate = toolResult["date"]?.ToString() ?? string.Empty;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[AIMcNeelForumPostSummarizeWorker] Error: {ex.Message}");
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
                }
            }

            public override void SetOutput(IGH_DataAccess DA, out string message)
            {
                this.parent.SetPersistentOutput("Id", this.resultId, DA);
                this.parent.SetPersistentOutput("Summary", new GH_String(this.resultSummary ?? string.Empty), DA);
                this.parent.SetPersistentOutput("Author", new GH_String(this.resultAuthor ?? string.Empty), DA);
                this.parent.SetPersistentOutput("Date", new GH_String(this.resultDate ?? string.Empty), DA);

                message = this.resultId > 0 && !string.IsNullOrWhiteSpace(this.resultSummary)
                    ? "Post summarized"
                    : "No summary available";
            }
        }
    }
}
