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
using SmartHopper.Core.Grasshopper.Utils.Internal;
using SmartHopper.Infrastructure.AICall.Utilities;
using SmartHopper.ProviderSdk.AICall.Utilities;
using SmartHopper.ProviderSdk.Diagnostics;

using SmartHopper.ProviderSdk.Logging;
namespace SmartHopper.Components.Knowledge
{
    /// <summary>
    /// Fetches a web page via the shared <c>web2md</c> tool and requests explicit AI
    /// visual evidence enrichment (<c>describeImages=true</c>). Enrichment runs
    /// synchronously through the component's selected provider/model; provider-batch
    /// submission is not supported for this component.
    /// </summary>
    public class AIWeb2MdComponent : AIStatefulAsyncComponentBase
    {
        /// <inheritdoc/>
        /// <summary>
        /// This component's work has side effects or consumes external state
        /// (canvas mutations, chat history, file/web fetches) so per-unit results
        /// are not reusable across runs.
        /// </summary>
        protected override bool SupportsResultReuse => false;

        public override Guid ComponentGuid => new Guid("2FADA1EA-E6E2-43AC-B411-228D999F92A8");

        /// <inheritdoc/>
        protected override Bitmap Icon => Resources.AIWeb2Md;

        /// <inheritdoc/>
        public override GH_Exposure Exposure => GH_Exposure.secondary;

        /// <inheritdoc/>
        public override IEnumerable<string> Keywords => new[] {
            "AIWebToMd",
            "AIWeb2Md",
            "web2md",
            "webtomd",
            "Web to Markdown",
            "URL to Markdown",
            "Website to Markdown",
            "Convert Web Page",
            "Web Page Conversion",
            "HTML to Markdown",
            "AI Web",
            "Web AI",
        };

        /// <inheritdoc/>
        protected override IReadOnlyList<string> UsingAiTools => new[] { "web2md" };

        /// <inheritdoc/>
        protected override ProcessingOptions ComponentProcessingOptions => new ProcessingOptions
        {
            Topology = ProcessingTopology.BranchToBranch,
            OnlyMatchingPaths = false,
            GroupIdenticalBranches = true,
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="AIWeb2MdComponent"/> class.
        /// </summary>
        public AIWeb2MdComponent()
            : base(
                "AI Web To Markdown",
                "AIWeb2Md",
                "Fetch a web page (URL) and convert it to Markdown with AI-generated image descriptions.\nUse Web To Markdown when no AI image description is needed.",
                "SmartHopper",
                "F. Documents")
        {
        }

        /// <inheritdoc/>
        protected override void RegisterAdditionalInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("URL", "U", "Web page URL(s) to fetch and convert.", GH_ParamAccess.tree);
        }

        /// <inheritdoc/>
        protected override void RegisterAdditionalOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Markdown", "Md", "Converted Markdown content of the web page with AI-generated visual evidence appended under labeled headings.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Format", "Fmt", "Detected content format (e.g., markdown, plain_text, url).", GH_ParamAccess.tree);
        }

        /// <inheritdoc/>
        protected override AsyncWorkerBase CreateWorker(Action<string> progressReporter)
        {
            return new AIWeb2MdWorker(this, this.AddRuntimeMessage, this.ComponentProcessingOptions);
        }

        private sealed class AIWeb2MdWorker : AsyncWorkerBase
        {
            private readonly AIWeb2MdComponent parent;
            private readonly ProcessingOptions processingOptions;
            private GH_Structure<GH_String> urlTree;
            private bool hasWork;

            private GH_Structure<GH_String> resultMarkdown;
            private GH_Structure<GH_String> resultFormat;

            public AIWeb2MdWorker(
                AIWeb2MdComponent parent,
                Action<GH_RuntimeMessageLevel, string> addRuntimeMessage,
                ProcessingOptions processingOptions)
                : base(parent, addRuntimeMessage)
            {
                this.parent = parent;
                this.processingOptions = processingOptions;
            }

            /// <inheritdoc/>
            public override void GatherInput(IGH_DataAccess DA, out int dataCount)
            {
                this.urlTree = new GH_Structure<GH_String>();
                DA.GetDataTree("URL", out this.urlTree);

                this.hasWork = this.urlTree != null && this.urlTree.PathCount > 0 && this.urlTree.DataCount > 0;
                dataCount = this.hasWork ? this.urlTree.DataCount : 0;

                this.resultMarkdown = new GH_Structure<GH_String>();
                this.resultFormat = new GH_Structure<GH_String>();
            }

            /// <inheritdoc/>
            public override async Task DoWorkAsync(CancellationToken token)
            {
                this.resultMarkdown = new GH_Structure<GH_String>();
                this.resultFormat = new GH_Structure<GH_String>();

                if (!this.hasWork)
                {
                    return;
                }

                // Explicit enrichment is synchronous — a provider batch submission would
                // silently skip the helper calls it exists for.
                if (this.parent.IsBatchRequest())
                {
                    this.CollectMessage(
                        SHRuntimeMessageSeverity.Error,
                        "AI Markdown enrichment currently runs synchronously and cannot be queued as a provider batch. Disable batch processing for this component.");
                    return;
                }

                try
                {
                    var inputTrees = new Dictionary<string, GH_Structure<GH_String>>
                    {
                        { "URL", this.urlTree },
                    };

                    var resultTrees = await this.parent.RunProcessingAsync<GH_String>(
                        inputTrees,
                        async branchInputs =>
                        {
                            var outputs = new Dictionary<string, List<IGH_Goo>>
                            {
                                { "Markdown", new List<IGH_Goo>() },
                                { "Format", new List<IGH_Goo>() },
                            };

                            if (!branchInputs.TryGetValue("URL", out var urlBranch) || urlBranch == null)
                            {
                                return outputs;
                            }

                            for (int i = 0; i < urlBranch.Count; i++)
                            {
                                token.ThrowIfCancellationRequested();

                                var ghUrl = urlBranch[i];
                                if (ghUrl == null || string.IsNullOrWhiteSpace(ghUrl.Value))
                                {
                                    outputs["Markdown"].Add(new GH_String(string.Empty));
                                    outputs["Format"].Add(new GH_String(string.Empty));
                                    continue;
                                }

                                var url = ghUrl.Value;
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
                                    // Explicit AI enrichment through the shared tool
                                    // contract; provider/model travel via the
                                    // component's request path.
                                    var parameters = new JObject
                                    {
                                        ["url"] = url,
                                        ["includeLinks"] = true,
                                        ["includeImages"] = true,
                                        ["extractImages"] = true,
                                        ["describeImages"] = true,
                                    };

                                    var toolResult = await this.parent.CallAIToolAsync("web2md", parameters, token).ConfigureAwait(false);

                                    if (toolResult.Result == null)
                                    {
                                        this.CollectMessage(SHRuntimeMessageSeverity.Warning, $"Tool 'web2md' returned no result for URL: {url}", SHRuntimeMessageOrigin.Tool);
                                        outputs["Markdown"].Add(new GH_String(string.Empty));
                                        outputs["Format"].Add(new GH_String(string.Empty));
                                        continue;
                                    }

                                    string markdown = toolResult.Result["content"]?.ToString() ?? string.Empty;
                                    string format = (toolResult.Result["metadata"] as JObject)?["format"]?.ToString() ?? "url";

                                    var messages = toolResult.ExtractMessages();
                                    foreach (var m in messages) this.CollectMessage(m);

                                    outputs["Markdown"].Add(new GH_String(markdown));
                                    outputs["Format"].Add(new GH_String(format));
                                }
                                catch (OperationCanceledException)
                                {
                                    throw;
                                }
                                catch (Exception ex)
                                {
                                    Logger.For<AIWeb2MdWorker>().Exception(ex, $"[AIWeb2Md] Error for URL {url}: {ex.Message}");
                                    this.CollectMessage(SHRuntimeMessageSeverity.Error, $"Error converting {url}: {ex.Message}");
                                    outputs["Markdown"].Add(new GH_String(string.Empty));
                                    outputs["Format"].Add(new GH_String("error"));
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
                    this.CollectMessage(SHRuntimeMessageSeverity.Warning, "Operation was cancelled.");
                }
                catch (Exception ex)
                {
                    Logger.For<AIWeb2MdWorker>().Exception(ex, $"[AIWeb2Md] Error: {ex.Message}");
                    this.CollectMessage(SHRuntimeMessageSeverity.Error, ex.Message);
                }
            }

            /// <inheritdoc/>
            public override void SetOutput(IGH_DataAccess DA, out string message)
            {
                this.parent.SetPersistentOutput("Markdown", this.resultMarkdown ?? new GH_Structure<GH_String>(), DA);
                this.parent.SetPersistentOutput("Format", this.resultFormat ?? new GH_Structure<GH_String>(), DA);
                message = null;
            }
        }
    }
}
