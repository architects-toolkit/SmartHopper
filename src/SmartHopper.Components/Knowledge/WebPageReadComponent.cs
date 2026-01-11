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
 * along with this library; if not, write to the Free Software Foundation,
 * Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301, USA.
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
    public class WebPageReadComponent : StatefulComponentBase
    {
        public override Guid ComponentGuid => new Guid("C2E6B13A-6245-4A4F-8C8F-3B7616D33003");

        protected override Bitmap Icon => Resources.websummarize;

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
            pManager.AddTextParameter("Url", "Url", "REQUIRED URL or URLs of the webpage(s) to fetch.", GH_ParamAccess.tree);
        }

        protected override void RegisterAdditionalOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Content", "C", "Plain text content of the webpage.", GH_ParamAccess.tree);
        }

        protected override AsyncWorkerBase CreateWorker(Action<string> progressReporter)
        {
            return new WebPageReadWorker(this, this.AddRuntimeMessage, ComponentProcessingOptions);
        }

        private sealed class WebPageReadWorker : AsyncWorkerBase
        {
            private readonly WebPageReadComponent parent;
            private readonly ProcessingOptions processingOptions;
            private Dictionary<string, GH_Structure<GH_String>> inputTrees;
            private bool hasWork;

            private GH_Structure<GH_String> resultContent;

            public WebPageReadWorker(
                WebPageReadComponent parent,
                Action<GH_RuntimeMessageLevel, string> addRuntimeMessage,
                ProcessingOptions processingOptions)
                : base(parent, addRuntimeMessage)
            {
                this.parent = parent;
                this.processingOptions = processingOptions;
            }

            public override void GatherInput(IGH_DataAccess DA, out int dataCount)
            {
                var urlTree = new GH_Structure<GH_String>();
                DA.GetDataTree("Url", out urlTree);

                this.inputTrees = new Dictionary<string, GH_Structure<GH_String>>
                {
                    { "Url", urlTree ?? new GH_Structure<GH_String>() },
                };

                this.hasWork = urlTree != null && urlTree.DataCount > 0;
                if (!this.hasWork)
                {
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Url is required.");
                }

                // Data count is computed centrally in RunProcessingAsync for item-based metrics.
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
                    var resultTrees = await this.parent.RunProcessingAsync<GH_String, GH_String>(
                        this.inputTrees,
                        async branchInputs =>
                        {
                            var outputs = new Dictionary<string, List<GH_String>>
                            {
                                { "Content", new List<GH_String>() },
                            };

                            if (!branchInputs.TryGetValue("Url", out var urls) || urls == null || urls.Count == 0)
                            {
                                return outputs;
                            }

                            foreach (var ghUrl in urls)
                            {
                                var urlValue = ghUrl?.Value ?? string.Empty;
                                if (string.IsNullOrWhiteSpace(urlValue))
                                {
                                    continue;
                                }

                                var parameters = new JObject
                                {
                                    ["url"] = urlValue,
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
                                toolCall.SkipMetricsValidation = true;

                                AIReturn aiResult;
                                try
                                {
                                    aiResult = await toolCall.Exec().ConfigureAwait(false);
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine($"[AIWebPageReadWorker] Error executing tool: {ex.Message}");
                                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
                                    continue;
                                }

                                var toolResultInteraction = aiResult.Body?.GetLastInteraction(AIAgent.ToolResult) as AIInteractionToolResult;
                                var toolResult = toolResultInteraction?.Result;

                                if (toolResult == null)
                                {
                                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Tool 'web_generic_page_read' returned no result.");
                                    continue;
                                }

                                string content = toolResult["content"]?.ToString() ?? string.Empty;

                                outputs["Content"].Add(new GH_String(content));
                            }

                            return outputs;
                        },
                        this.processingOptions,
                        token).ConfigureAwait(false);

                    this.resultContent = new GH_Structure<GH_String>();

                    if (resultTrees.TryGetValue("Content", out var contentTree))
                    {
                        this.resultContent = contentTree;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[AIWebPageReadWorker] Error: {ex.Message}");
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
                }
            }

            public override void SetOutput(IGH_DataAccess DA, out string message)
            {
                var contentTree = this.resultContent ?? new GH_Structure<GH_String>();
                this.parent.SetPersistentOutput("Content", contentTree, DA);

                var hasAnyContent = contentTree.DataCount > 0;
                message = hasAnyContent ? "Page content retrieved" : "No content retrieved";
            }
        }
    }
}
