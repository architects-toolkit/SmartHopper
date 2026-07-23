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
using SmartHopper.ProviderSdk.AICall.Utilities;
using SmartHopper.ProviderSdk.Diagnostics;

namespace SmartHopper.Components.Input
{
    /// <summary>
    /// Fetches web content from a URL using the web2md AI tool and wraps it into an AIInputPayload.
    /// Converts web pages to Markdown for AI processing.
    /// </summary>
    public class Web2AIComponent : StatefulComponentBase
    {
        public override Guid ComponentGuid => new Guid("A7294D39-8DCB-4178-A435-AD7D73BA5E14");

        protected override Bitmap Icon => Resources.webtomd;

        public override GH_Exposure Exposure => GH_Exposure.septenary;

        public Web2AIComponent()
            : base(
                "Web to AI",
                "Web2AI",
                "Fetches web content from a URL using web2md AI tool and wraps it into an AIInputPayload.",
                "SmartHopper",
                "Input")
        {
        }

        protected override void RegisterAdditionalInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("URL", "U", "Web URL(s) to fetch and convert to Markdown.", GH_ParamAccess.tree);
        }

        protected override void RegisterAdditionalOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddParameter(new AIInputPayloadParameter(), "Input >", ">", "AIInputPayload(s) containing the web content as Markdown.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Markdown", "M", "Extracted web content as Markdown.", GH_ParamAccess.tree);
        }

        protected override ProcessingOptions ComponentProcessingOptions => new ProcessingOptions
        {
            Topology = ProcessingTopology.BranchToBranch,
            OnlyMatchingPaths = false,
            GroupIdenticalBranches = true,
        };

        protected override AsyncWorkerBase CreateWorker(Action<string> progressReporter)
        {
            return new Web2AIWorker(this, this.AddRuntimeMessage, this.ComponentProcessingOptions);
        }

        private sealed class Web2AIWorker : AsyncWorkerBase
        {
            private readonly Web2AIComponent parent;
            private readonly ProcessingOptions processingOptions;
            private Dictionary<string, GH_Structure<GH_String>> inputTrees;
            private Dictionary<string, GH_Structure<IGH_Goo>> result;

            public Web2AIWorker(Web2AIComponent parent, Action<GH_RuntimeMessageLevel, string> addRuntimeMessage, ProcessingOptions options)
                : base(parent, addRuntimeMessage)
            {
                this.parent = parent;
                this.processingOptions = options;
                this.result = new Dictionary<string, GH_Structure<IGH_Goo>>
                {
                    { "Input >", new GH_Structure<IGH_Goo>() },
                    { "Markdown", new GH_Structure<IGH_Goo>() },
                };
            }

            public override void GatherInput(IGH_DataAccess DA, out int dataCount)
            {
                this.inputTrees = new Dictionary<string, GH_Structure<GH_String>>();

                var urlTree = new GH_Structure<GH_String>();
                DA.GetDataTree("URL", out urlTree);

                this.inputTrees["URL"] = urlTree;

                // Data count will be calculated by RunProcessingAsync
                dataCount = 0;
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
                    Debug.WriteLine($"[Web2AIWorker] Error: {ex.Message}");
                    this.CollectMessage(SHRuntimeMessageSeverity.Error, $"Error processing web content: {ex.Message}");
                }
            }

            private async Task<Dictionary<string, List<IGH_Goo>>> ProcessBranches(Dictionary<string, List<GH_String>> branches)
            {
                var outputs = new Dictionary<string, List<IGH_Goo>>
                {
                    { "Input >", new List<IGH_Goo>() },
                    { "Markdown", new List<IGH_Goo>() },
                };

                var urlList = branches["URL"];

                foreach (var urlItem in urlList)
                {
                    string url = urlItem?.Value;

                    if (string.IsNullOrWhiteSpace(url))
                    {
                        outputs["Input >"].Add(new GH_AIInputPayload(null));
                        outputs["Markdown"].Add(new GH_String(string.Empty));
                        continue;
                    }

                    // Validate URL format
                    if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                        (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                    {
                        this.CollectMessage(SHRuntimeMessageSeverity.Error, $"Invalid HTTP(S) URL: {url}");
                        outputs["Input >"].Add(new GH_AIInputPayload(null));
                        outputs["Markdown"].Add(new GH_String(string.Empty));
                        continue;
                    }

                    try
                    {
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
                        var toolResult = ToolCallResult.FromAIReturn(aiResult);

                        if (toolResult.Result == null)
                        {
                            this.CollectMessage(SHRuntimeMessageSeverity.Warning, $"Tool 'web2md' returned no result for URL: {url}", SHRuntimeMessageOrigin.Tool);
                            outputs["Input >"].Add(new GH_AIInputPayload(null));
                            outputs["Markdown"].Add(new GH_String(string.Empty));
                            continue;
                        }

                        string markdown = toolResult["content"]?.ToString() ?? string.Empty;
                        AIInputPayload payload = null;

                        if (!string.IsNullOrWhiteSpace(markdown))
                        {
                            payload = AIInputPayload.FromText(markdown);
                        }

                        // Extract and collect any messages from tool result
                        var toolMessages = ToolCallResultRuntimeMessageExtensions.ExtractMessages(toolResult);
                        foreach (var m in toolMessages) this.CollectMessage(m);

                        outputs["Input >"].Add(new GH_AIInputPayload(payload));
                        outputs["Markdown"].Add(new GH_String(markdown));
                    }
                    catch (Exception ex)
                    {
                        this.CollectMessage(SHRuntimeMessageSeverity.Warning, $"Error fetching {url}: {ex.Message}");
                        outputs["Input >"].Add(new GH_AIInputPayload(null));
                        outputs["Markdown"].Add(new GH_String(string.Empty));
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

                if (this.result.TryGetValue("Markdown", out var markdownTree) && markdownTree != null)
                {
                    this.parent.SetPersistentOutput("Markdown", markdownTree, DA);
                }

                int successCount = 0;
                if (this.result.TryGetValue("Markdown", out var tree) && tree != null)
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

                int totalCount = this.inputTrees?.Values.Sum(t => t.DataCount) ?? 0;
                message = $"Processed {successCount}/{totalCount} URL(s)";
            }
        }
    }
}