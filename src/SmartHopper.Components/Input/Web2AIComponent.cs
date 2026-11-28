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
using SmartHopper.Core.Models;
using SmartHopper.Core.Types;
using SmartHopper.Infrastructure.AICall.Utilities;
using SmartHopper.ProviderSdk.AICall.Utilities;
using SmartHopper.ProviderSdk.Diagnostics;
using SmartHopper.ProviderSdk.Documents;

using SmartHopper.ProviderSdk.Logging;
namespace SmartHopper.Components.Input
{
    /// <summary>
    /// Fetches web content from a URL using the shared <c>web2md</c> tool and packages it
    /// into a single ordered multimodal <see cref="AIInputPayload"/> (article text plus
    /// model-visible images) — entirely local, with no provider call while packaging.
    /// </summary>
    public class Web2AIComponent : AIStatefulAsyncComponentBase
    {
        /// <inheritdoc/>
        /// <summary>
        /// This component's work has side effects or consumes external state
        /// (canvas mutations, chat history, file/web fetches) so per-unit results
        /// are not reusable across runs.
        /// </summary>
        protected override bool SupportsResultReuse => false;

        public override Guid ComponentGuid => new Guid("A7294D39-8DCB-4178-A435-AD7D73BA5E14");

        /// <inheritdoc/>
        protected override Bitmap Icon => Resources.Web2AI;

        /// <inheritdoc/>
        public override GH_Exposure Exposure => GH_Exposure.tertiary | GH_Exposure.obscure;

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
        /// Initializes a new instance of the <see cref="Web2AIComponent"/> class.
        /// </summary>
        public Web2AIComponent()
            : base(
                "Web to AI",
                "Web2AI",
                "Fetch a web page (URL), convert it to clean Markdown, and package it to wire into any AI output component. Conversion is local — no AI provider call is made while packaging.",
                "SmartHopper",
                "B. Input")
        {
        }

        /// <inheritdoc/>
        protected override void RegisterAdditionalInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("URL", "U", "Web page URL(s) to fetch and convert to Markdown.", GH_ParamAccess.tree);
        }

        /// <inheritdoc/>
        protected override void RegisterAdditionalOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddParameter(new AIInputPayloadParameter(), "Input >", ">", "Packaged web content (Markdown + materialized images) ready to wire into any AI output component's 'Input >' parameter.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Markdown", "M", "Extracted web page content in Markdown format.", GH_ParamAccess.tree);
        }

        /// <inheritdoc/>
        protected override AsyncWorkerBase CreateWorker(Action<string> progressReporter)
        {
            return new Web2AIWorker(this, this.AddRuntimeMessage, this.ComponentProcessingOptions);
        }

        private sealed class Web2AIWorker : AsyncWorkerBase
        {
            private readonly Web2AIComponent parent;
            private readonly ProcessingOptions processingOptions;
            private GH_Structure<GH_String> urlTree;
            private bool hasWork;

            private GH_Structure<GH_AIInputPayload> resultPayload;
            private GH_Structure<GH_String> resultMarkdown;

            public Web2AIWorker(
                Web2AIComponent parent,
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

                this.resultPayload = new GH_Structure<GH_AIInputPayload>();
                this.resultMarkdown = new GH_Structure<GH_String>();
            }

            /// <inheritdoc/>
            public override async Task DoWorkAsync(CancellationToken token)
            {
                this.resultPayload = new GH_Structure<GH_AIInputPayload>();
                this.resultMarkdown = new GH_Structure<GH_String>();

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

                    var resultTrees = await this.parent.RunProcessingAsync<GH_String>(
                        inputTrees,
                        async branchInputs =>
                        {
                            var outputs = new Dictionary<string, List<IGH_Goo>>
                            {
                                { "Input >", new List<IGH_Goo>() },
                                { "Markdown", new List<IGH_Goo>() },
                            };

                            if (!branchInputs.TryGetValue("URL", out var urlBranch) || urlBranch == null)
                            {
                                return outputs;
                            }

                            for (int i = 0; i < urlBranch.Count; i++)
                            {
                                token.ThrowIfCancellationRequested();

                                var url = urlBranch[i]?.Value;
                                if (string.IsNullOrWhiteSpace(url))
                                {
                                    outputs["Input >"].Add(new GH_AIInputPayload(null));
                                    outputs["Markdown"].Add(new GH_String(string.Empty));
                                    continue;
                                }

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
                                    // Local fetch + materialization only — describeImages
                                    // stays false so no provider helper is invoked here.
                                    var parameters = new JObject
                                    {
                                        ["url"] = url,
                                        ["includeLinks"] = true,
                                        ["includeImages"] = true,
                                        ["extractImages"] = true,
                                        ["describeImages"] = false,
                                    };

                                    var toolResult = await this.parent.CallAIToolAsync("web2md", parameters, token).ConfigureAwait(false);

                                    if (toolResult.Result == null)
                                    {
                                        this.CollectMessage(SHRuntimeMessageSeverity.Warning, $"Tool 'web2md' returned no result for URL: {url}", SHRuntimeMessageOrigin.Tool);
                                        outputs["Input >"].Add(new GH_AIInputPayload(null));
                                        outputs["Markdown"].Add(new GH_String(string.Empty));
                                        continue;
                                    }

                                    var messages = toolResult.ExtractMessages();
                                    foreach (var m in messages) this.CollectMessage(m);

                                    string markdown = toolResult.Result["content"]?.ToString() ?? string.Empty;

                                    // ONE ordered multimodal payload through the shared
                                    // codec + builder — no ad-hoc packaging.
                                    var document = DocumentEvidenceJsonCodec.Decode(toolResult.Result, toolResult.Images);
                                    AIInputPayload payload = null;
                                    if (document != null)
                                    {
                                        var interactions = DocumentEvidenceInteractionBuilder.Build(document, markdown);
                                        if (interactions.Count > 0)
                                        {
                                            payload = AIInputPayload.FromInteractions(interactions);
                                        }
                                    }

                                    if (payload == null && !string.IsNullOrWhiteSpace(markdown))
                                    {
                                        payload = AIInputPayload.FromText(markdown);
                                    }

                                    outputs["Input >"].Add(new GH_AIInputPayload(payload));
                                    outputs["Markdown"].Add(new GH_String(markdown));
                                }
                                catch (OperationCanceledException)
                                {
                                    throw;
                                }
                                catch (Exception ex)
                                {
                                    this.CollectMessage(SHRuntimeMessageSeverity.Warning, $"Error fetching {url}: {ex.Message}");
                                    outputs["Input >"].Add(new GH_AIInputPayload(null));
                                    outputs["Markdown"].Add(new GH_String(string.Empty));
                                }
                            }

                            return outputs;
                        },
                        this.processingOptions,
                        token).ConfigureAwait(false);

                    this.resultPayload = DataTreeProcessor.ExtractTypedTree<GH_AIInputPayload>(resultTrees, "Input >");
                    this.resultMarkdown = DataTreeProcessor.ExtractTypedTree<GH_String>(resultTrees, "Markdown");
                }
                catch (OperationCanceledException)
                {
                    this.CollectMessage(SHRuntimeMessageSeverity.Warning, "Operation was cancelled.");
                }
                catch (Exception ex)
                {
                    Logger.For<Web2AIWorker>().Exception(ex, $"[Web2AI] Error: {ex.Message}");
                    this.CollectMessage(SHRuntimeMessageSeverity.Error, ex.Message);
                }
            }

            /// <inheritdoc/>
            public override void SetOutput(IGH_DataAccess DA, out string message)
            {
                this.parent.SetPersistentOutput("Input >", this.resultPayload ?? new GH_Structure<GH_AIInputPayload>(), DA);
                this.parent.SetPersistentOutput("Markdown", this.resultMarkdown ?? new GH_Structure<GH_String>(), DA);
                message = null;
            }
        }
    }
}
