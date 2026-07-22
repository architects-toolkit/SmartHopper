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
    /// <summary>
    /// Grasshopper component that converts web pages (URLs) to Markdown.
    /// Performs a local web fetch via the <c>web2md</c> tool and keeps images as remote Markdown links.
    /// No AI provider or model is required. Use <c>AIWeb2MdComponent</c> for AI-powered image description.
    /// </summary>
    public class Web2MdComponent : StatefulComponentBase
    {
        /// <inheritdoc/>
        public override Guid ComponentGuid => new Guid("4053AD8D-10DF-47D3-AC0C-CA24E8BB638D");

        /// <inheritdoc/>
        protected override Bitmap Icon => Resources.webtomd;

        public override GH_Exposure Exposure => GH_Exposure.primary;

        /// <inheritdoc/>
        public override IEnumerable<string> Keywords => new[]
        {
            "WebToMd",
            "Web2Md",
            "web2md",
            "webtomd",
            "Web to Markdown",
            "URL to Markdown",
            "Website to Markdown",
            "Convert Web Page",
            "Web Page Conversion",
            "HTML to Markdown",
        };

        /// <inheritdoc/>
        protected override ProcessingOptions ComponentProcessingOptions => new ProcessingOptions
        {
            Topology = ProcessingTopology.BranchToBranch,
            OnlyMatchingPaths = false,
            GroupIdenticalBranches = true,
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="Web2MdComponent"/> class.
        /// </summary>
        public Web2MdComponent()
            : base(
                  "Web To Markdown",
                  "Web2Md",
                  "Convert a web page (URL) to Markdown text. Supports Wikipedia, GitHub, GitLab, Discourse, Stack Exchange, and generic HTML pages.",
                  "SmartHopper",
                  "Knowledge")
        {
            // Set RunOnlyOnInputChanges to false to ensure the component always runs when the Run parameter is true
            this.RunOnlyOnInputChanges = false;
        }

        /// <inheritdoc/>
        protected override void RegisterAdditionalInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("URL", "U", "REQUIRED URL(s) of the webpage(s) to convert.", GH_ParamAccess.tree);
            pManager.AddTextParameter("HTML Readability", "R", "HTML main-content extraction strategy: auto (default), smartreader, heuristic, or off.", GH_ParamAccess.item, "auto");
            pManager[pManager.ParamCount - 1].Optional = true;
        }

        /// <inheritdoc/>
        protected override void RegisterAdditionalOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Markdown", "Md", "Markdown content of the webpage.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Format", "Fmt", "Detected content format (e.g., markdown, plain_text, url).", GH_ParamAccess.tree);
        }

        /// <inheritdoc/>
        protected override AsyncWorkerBase CreateWorker(Action<string> progressReporter)
        {
            return new Web2MdWorker(this, this.AddRuntimeMessage, this.ComponentProcessingOptions);
        }

        private sealed class Web2MdWorker : AsyncWorkerBase
        {
            private readonly Web2MdComponent parent;
            private readonly ProcessingOptions processingOptions;
            private GH_Structure<GH_String> urlTree;
            private bool hasWork;
            private string htmlReadabilityMode;

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
                this.urlTree = new GH_Structure<GH_String>();
                DA.GetDataTree("URL", out this.urlTree);

                var readabilityParam = new GH_String("auto");
                DA.GetData("HTML Readability", ref readabilityParam);
                this.htmlReadabilityMode = readabilityParam?.Value ?? "auto";

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
                this.resultMarkdown = new GH_Structure<GH_String>();
                this.resultFormat = new GH_Structure<GH_String>();

                if (!this.hasWork)
                {
                    return;
                }

                try
                {
                    var inputTrees = new Dictionary<string, GH_Structure<GH_String>>
                    {
                        { "URL", this.urlTree },
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

                                    if (!string.IsNullOrWhiteSpace(this.htmlReadabilityMode) &&
                                        !string.Equals(this.htmlReadabilityMode, "auto", StringComparison.OrdinalIgnoreCase))
                                    {
                                        parameters["HTMLreadabilityMode"] = this.htmlReadabilityMode;
                                    }

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

                    this.resultMarkdown = DataTreeProcessor.ExtractTypedTree<GH_String>(resultTrees, "Markdown");
                    this.resultFormat = DataTreeProcessor.ExtractTypedTree<GH_String>(resultTrees, "Format");
                }
                catch (OperationCanceledException)
                {
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Operation was cancelled.");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Web2Md] Error: {ex.Message}");
                    this.CollectMessage(SHRuntimeMessageSeverity.Error, ex.Message);
                }
            }

            private async Task<Dictionary<string, List<IGH_Goo>>> ProcessBranches(Dictionary<string, List<GH_String>> branches, CancellationToken token)
            {
                var outputs = new Dictionary<string, List<IGH_Goo>>
                {
                    { "Markdown", new List<IGH_Goo>() },
                    { "Format", new List<IGH_Goo>() },
                };

                var urlList = branches["URL"];

                foreach (var ghUrl in urlList)
                {
                    if (ghUrl == null || string.IsNullOrWhiteSpace(ghUrl.Value))
                    {
                        outputs["Markdown"].Add(new GH_String(string.Empty));
                        outputs["Format"].Add(new GH_String(string.Empty));
                        continue;
                    }

                    string url = ghUrl.Value;

                    if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                        (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                    {
                        this.CollectMessage(SHRuntimeMessageSeverity.Error, $"Invalid HTTP(S) URL: {url}");
                        outputs["Markdown"].Add(new GH_String(string.Empty));
                        outputs["Format"].Add(new GH_String(string.Empty));
                        continue;
                    }

                    try
                    {
                        var parameters = new JObject
                        {
                            ["url"] = url,
                            ["includeLinks"] = true,
                            ["includeImages"] = true,
                            ["imageMode"] = "link",
                        };

                        var toolResult = await ExecuteWeb2MdToolAsync(parameters, token).ConfigureAwait(false);

                        if (toolResult.Result == null)
                        {
                            this.CollectMessage(SHRuntimeMessageSeverity.Warning, $"Tool 'web2md' returned no result for URL: {url}", SHRuntimeMessageOrigin.Tool);
                            outputs["Markdown"].Add(new GH_String(string.Empty));
                            outputs["Format"].Add(new GH_String(string.Empty));
                            continue;
                        }

                        string markdown = toolResult.Result["content"]?.ToString() ?? string.Empty;
                        string format = (toolResult.Result["metadata"] as JObject)?["format"]?.ToString() ?? "url";

                        var messages = RuntimeMessageUtility.ExtractMessages(toolResult);
                        foreach (var m in messages) this.CollectMessage(m);

                        outputs["Markdown"].Add(new GH_String(markdown));
                        outputs["Format"].Add(new GH_String(format));
                    }
                    catch (Exception ex)
                    {
                        this.CollectMessage(SHRuntimeMessageSeverity.Warning, $"Error fetching {url}: {ex.Message}");
                        outputs["Markdown"].Add(new GH_String(string.Empty));
                        outputs["Format"].Add(new GH_String(string.Empty));
                    }
                }

                return outputs;
            }

            private static async Task<ToolCallResult> ExecuteWeb2MdToolAsync(JObject parameters, CancellationToken token)
            {
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
                toolCall.CancellationToken = token;

                AIReturn aiResult = await toolCall.Exec(token).ConfigureAwait(false);

                var toolResultInteraction = aiResult.Body?.Interactions
                    .OfType<AIInteractionToolResult>()
                    .FirstOrDefault();

                if (toolResultInteraction?.Result != null)
                {
                    return new ToolCallResult(aiResult.Success, toolResultInteraction.Result, aiResult.Messages);
                }

                var fallbackPayload = new JObject
                {
                    ["success"] = aiResult.Success,
                    ["messages"] = JArray.FromObject(aiResult.Messages),
                };
                return new ToolCallResult(aiResult.Success, fallbackPayload, aiResult.Messages);
            }

            public override void SetOutput(IGH_DataAccess DA, out string message)
            {
                this.parent.SetPersistentOutput("Markdown", this.resultMarkdown ?? new GH_Structure<GH_String>(), DA);
                this.parent.SetPersistentOutput("Format", this.resultFormat ?? new GH_Structure<GH_String>(), DA);
                message = null;
            }
        }
    }
}
