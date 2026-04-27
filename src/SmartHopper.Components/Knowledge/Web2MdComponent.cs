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
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AICall.Core.Returns;
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.Infrastructure.AICall.Utilities;
using SmartHopper.Infrastructure.Diagnostics;

namespace SmartHopper.Components.Knowledge
{
    public class Web2MdComponent : StatefulComponentBase
    {
        public override Guid ComponentGuid => new Guid("4053AD8D-10DF-47D3-AC0C-CA24E8BB638D");

        protected override Bitmap Icon => Resources.webtomd;

        public override GH_Exposure Exposure => GH_Exposure.primary;

        public Web2MdComponent()
            : base(
                  "Web To Markdown",
                  "Web2Md",
                  "Convert a web page (URL) to Markdown text. Supports Wikipedia, GitHub, GitLab, Discourse, Stack Exchange, and generic HTML pages.",
                  "SmartHopper",
                  "Knowledge")
        {
            this.RunOnlyOnInputChanges = false;
        }

        protected override void RegisterAdditionalInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("URL", "U", "REQUIRED URL(s) of the webpage(s) to convert.", GH_ParamAccess.tree);
        }

        protected override void RegisterAdditionalOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Markdown", "Md", "Markdown content of the webpage.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Format", "Fmt", "Detected content format (e.g., markdown, plain_text, url).", GH_ParamAccess.tree);
        }

        protected override AsyncWorkerBase CreateWorker(Action<string> progressReporter)
        {
            return new Web2MdWorker(this, this.AddRuntimeMessage, this.ComponentProcessingOptions);
        }

        private sealed class Web2MdWorker : AsyncWorkerBase
        {
            private readonly Web2MdComponent parent;
            private readonly ProcessingOptions processingOptions;
            private Dictionary<string, GH_Structure<GH_String>> inputTrees;
            private bool hasWork;

            private GH_Structure<GH_String> resultMarkdown;
            private GH_Structure<GH_String> resultFormat;

            public Web2MdWorker(
                Web2MdComponent parent,
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
                DA.GetDataTree("URL", out urlTree);

                this.inputTrees = new Dictionary<string, GH_Structure<GH_String>>
                {
                    { "URL", urlTree ?? new GH_Structure<GH_String>() },
                };

                this.hasWork = urlTree != null && urlTree.PathCount > 0 && urlTree.DataCount > 0;
                dataCount = this.hasWork ? urlTree.DataCount : 0;

                this.resultMarkdown = new GH_Structure<GH_String>();
                this.resultFormat = new GH_Structure<GH_String>();
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
                                { "URL", new List<GH_String>() },
                                { "Markdown", new List<GH_String>() },
                                { "Format", new List<GH_String>() },
                            };

                            foreach (var kvp in branchInputs)
                            {
                                var urls = kvp.Value;

                                foreach (var ghUrl in urls)
                                {
                                    if (ghUrl == null || string.IsNullOrWhiteSpace(ghUrl.Value))
                                    {
                                        this.CollectMessage(SHRuntimeMessageSeverity.Warning, "Skipping empty or null URL.", SHRuntimeMessageOrigin.Worker);
                                        outputs["Markdown"].Add(new GH_String(string.Empty));
                                        outputs["Format"].Add(new GH_String(string.Empty));
                                        continue;
                                    }

                                    string url = ghUrl.Value;

                                    var parameters = new JObject
                                    {
                                        ["url"] = url,
                                    };

                                    var toolCallInteraction = new AIInteractionToolCall
                                    {
                                        Name = "web2md",
                                        Arguments = parameters,
                                        Agent = AIAgent.Assistant,
                                    };

                                    var toolCall = new AIToolCall
                                    {
                                        Endpoint = "web2md",
                                    };

                                    toolCall.FromToolCallInteraction(toolCallInteraction);
                                    toolCall.SkipMetricsValidation = true;

                                    AIReturn aiResult = await toolCall.Exec().ConfigureAwait(false);

                                    if (!aiResult.Success)
                                    {
                                        var errMsgs = aiResult.Messages.Where(m => m.Severity == SHRuntimeMessageSeverity.Error).Select(m => m.Message).ToList();
                                        var errStr = errMsgs.Count > 0 ? string.Join("; ", errMsgs) : "Unknown error";
                                        this.CollectMessage(SHRuntimeMessageSeverity.Error, $"Error fetching '{url}': {errStr}");
                                        outputs["Markdown"].Add(new GH_String(string.Empty));
                                        outputs["Format"].Add(new GH_String(string.Empty));
                                        continue;
                                    }

                                    var toolResult = ToolCallResult.FromAIReturn(aiResult);

                                    if (toolResult.Result == null)
                                    {
                                        this.CollectMessage(SHRuntimeMessageSeverity.Error, $"Tool 'web2md' returned no result for '{url}'.", SHRuntimeMessageOrigin.Tool);
                                        outputs["Markdown"].Add(new GH_String(string.Empty));
                                        outputs["Format"].Add(new GH_String(string.Empty));
                                        continue;
                                    }

                                    string content = toolResult["content"]?.ToString() ?? string.Empty;
                                    var metadata = toolResult["metadata"] as JObject;
                                    string format = metadata?["format"]?.ToString() ?? "url";

                                    outputs["Markdown"].Add(new GH_String(content));
                                    outputs["Format"].Add(new GH_String(format));

                                    // Extract and collect any messages from tool result
                                    var toolMessages = RuntimeMessageUtility.ExtractMessages(toolResult);
                                    foreach (var m in toolMessages) this.CollectMessage(m);
                                }
                            }

                            return outputs;
                        },
                        this.processingOptions,
                        token).ConfigureAwait(false);

                    this.resultMarkdown = new GH_Structure<GH_String>();
                    this.resultFormat = new GH_Structure<GH_String>();

                    if (resultTrees.TryGetValue("Markdown", out var markdownTree))
                    {
                        this.resultMarkdown = markdownTree;
                    }

                    if (resultTrees.TryGetValue("Format", out var formatTree))
                    {
                        this.resultFormat = formatTree;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Web2Md] Error: {ex.Message}");
                    this.CollectMessage(SHRuntimeMessageSeverity.Error, ex.Message);
                }
            }

            public override void SetOutput(IGH_DataAccess DA, out string errorMessage)
            {
                this.parent.SetPersistentOutput("Markdown", this.resultMarkdown ?? new GH_Structure<GH_String>(), DA);
                this.parent.SetPersistentOutput("Format", this.resultFormat ?? new GH_Structure<GH_String>(), DA);
                errorMessage = null;
            }
        }
    }
}
