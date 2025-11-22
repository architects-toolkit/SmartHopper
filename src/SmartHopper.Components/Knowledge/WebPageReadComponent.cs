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
    public class WebPageReadComponent : StatefulAsyncComponentBase
    {
        public override Guid ComponentGuid => new Guid("C2E6B13A-6245-4A4F-8C8F-3B7616D33003");

        // protected override Bitmap Icon => Resources.context;

        public override GH_Exposure Exposure => GH_Exposure.tertiary;

        public WebPageReadComponent()
            : base(
                  "Web Page Read",
                  "WebRead",
                  "Retrieve plain text content of a webpage at the given URL, excluding HTML, scripts, styles, and images.",
                  "SmartHopper",
                  "Knowledge")
        {
            this.RunOnlyOnInputChanges = false;
        }

        protected override void RegisterAdditionalInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Url", "U", "REQUIRED URL of the webpage to fetch.", GH_ParamAccess.item);
        }

        protected override void RegisterAdditionalOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Content", "C", "Plain text content of the webpage.", GH_ParamAccess.item);
            pManager.AddTextParameter("Url", "U", "Final URL that was fetched.", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Length", "L", "Length of the returned text content.", GH_ParamAccess.item);
        }

        protected override AsyncWorkerBase CreateWorker(Action<string> progressReporter)
        {
            return new WebPageReadWorker(this, this.AddRuntimeMessage);
        }

        private sealed class WebPageReadWorker : AsyncWorkerBase
        {
            private readonly WebPageReadComponent parent;
            private string url;
            private bool hasWork;

            private string resultContent;
            private string resultUrl;
            private int resultLength;

            public WebPageReadWorker(
                WebPageReadComponent parent,
                Action<GH_RuntimeMessageLevel, string> addRuntimeMessage)
                : base(parent, addRuntimeMessage)
            {
                this.parent = parent;
            }

            public override void GatherInput(IGH_DataAccess DA, out int dataCount)
            {
                string localUrl = null;
                DA.GetData(0, ref localUrl);

                this.url = localUrl ?? string.Empty;
                this.hasWork = !string.IsNullOrWhiteSpace(this.url);
                if (!this.hasWork)
                {
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Url is required.");
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
                        ["url"] = this.url,
                    };

                    var toolCallInteraction = new AIInteractionToolCall
                    {
                        Name = "web_generic_page_read",
                        Arguments = parameters,
                        Agent = AIAgent.Assistant,
                    };

                    var toolCall = new AIToolCall
                    {
                        Endpoint = "web_generic_page_read",
                    };

                    toolCall.FromToolCallInteraction(toolCallInteraction);

                    AIReturn aiResult = await toolCall.Exec().ConfigureAwait(false);
                    var toolResultInteraction = aiResult.Body?.GetLastInteraction(AIAgent.ToolResult) as AIInteractionToolResult;
                    var toolResult = toolResultInteraction?.Result;

                    if (toolResult == null)
                    {
                        this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Tool 'web_generic_page_read' returned no result.");
                        return;
                    }

                    this.resultContent = toolResult["content"]?.ToString() ?? string.Empty;
                    this.resultUrl = toolResult["url"]?.ToString() ?? this.url;
                    this.resultLength = toolResult["length"]?.ToObject<int?>() ?? this.resultContent.Length;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[AIWebPageReadWorker] Error: {ex.Message}");
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
                }
            }

            public override void SetOutput(IGH_DataAccess DA, out string message)
            {
                message = string.IsNullOrWhiteSpace(this.resultContent) ? "No content retrieved" : "Page content retrieved";
            }
        }
    }
}
