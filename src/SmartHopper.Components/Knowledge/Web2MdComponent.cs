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
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Newtonsoft.Json.Linq;
using SmartHopper.Components.Properties;
using SmartHopper.Core.ComponentBase;
using SmartHopper.Core.DataTree;
using SmartHopper.Core.Grasshopper.AITools;
using SmartHopper.Core.Grasshopper.Utils.Internal;
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
    /// The initial web fetch is performed by the local <c>web2md</c> tool. AI is used only for
    /// image description when Image Mode is set to embed, describe, or caption.
    /// </summary>
    public class Web2MdComponent : AIStatefulAsyncComponentBase
    {
        /// <summary>
        /// Per-URL context stored during DoWorkAsync so <see cref="OnBatchCompleted"/> can
        /// reconstruct the final Markdown once all batch image descriptions are available.
        /// </summary>
        private Dictionary<string, MarkdownImageBatchContext> _urlContexts;

        /// <summary>
        /// Flag to prevent clearing <see cref="_urlContexts"/> during polling re-runs.
        /// </summary>
        private bool _urlContextsInitialized;

        /// <summary>
        /// Set to true when the batch is active but <see cref="_urlContexts"/> could not be restored.
        /// </summary>
        private bool _batchContextLost;

        /// <summary>
        /// Local Format tree produced during DoWorkAsync; does not need AI so it is always available.
        /// </summary>
        private GH_Structure<GH_String> _localFormat;

        public override Guid ComponentGuid => new Guid("4053AD8D-10DF-47D3-AC0C-CA24E8BB638D");

        protected override Bitmap Icon => Resources.webtomd;

        public override GH_Exposure Exposure => GH_Exposure.primary;

        /// <inheritdoc/>
        protected override IReadOnlyList<string> UsingAiTools => new[] { "web2md", "img2text" };

        /// <inheritdoc/>
        protected override ProcessingOptions ComponentProcessingOptions => new ProcessingOptions
        {
            Topology = ProcessingTopology.BranchToBranch,
            OnlyMatchingPaths = false,
            GroupIdenticalBranches = true,
        };

        public Web2MdComponent()
            : base(
                "Web To Markdown",
                "Web2Md",
                "Convert a web page (URL) to Markdown text. Supports Wikipedia, GitHub, GitLab, Discourse, Stack Exchange, and generic HTML pages. Use Image Mode to describe or embed images via AI.",
                "SmartHopper",
                "Knowledge")
        {
            this.RunOnlyOnInputChanges = false;
        }

        protected override void RegisterAdditionalInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("URL", "U", "REQUIRED URL(s) of the webpage(s) to convert.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Image Mode", "IM", "How images appear in the Markdown. 'link' (default): keep remote image URLs as Markdown links. 'embed': download and embed as base64 data URIs with short AI captions. 'describe': replace with a long AI text description. 'caption': replace with a short AI-generated title. Requires an AI provider for embed/describe/caption.", GH_ParamAccess.item, "link");
        }

        protected override void RegisterAdditionalOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Markdown", "Md", "Markdown content of the webpage.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Format", "Fmt", "Detected content format (e.g., markdown, plain_text, url).", GH_ParamAccess.tree);
        }

        protected override void OnEnteringNeedsRun()
        {
            base.OnEnteringNeedsRun();
            this._urlContexts = null;
            this._urlContextsInitialized = false;
            this._batchContextLost = false;
            this._localFormat = null;
        }

        /// <inheritdoc/>
        protected override void OnBatchCompleted(IReadOnlyDictionary<string, JObject> results, IReadOnlyList<SHRuntimeMessage> messages = null)
        {
            var sentinel = this.GetSentinelTree("Markdown");
            if (results == null || sentinel == null) return;

            this.ProcessBatchResults<GH_String>(
                "Markdown",
                sentinel,
                results,
                (representativeSentinelId, _) =>
                {
                    if (this._urlContexts == null ||
                        !this._urlContexts.TryGetValue(representativeSentinelId, out var urlCtx))
                    {
                        Debug.WriteLine($"[Web2Md] OnBatchCompleted: URL context missing for sentinel {representativeSentinelId}");
                        return new GH_String("[URL context missing]");
                    }

                    var finalMarkdown = MarkdownImageBatchProcessor.Reconstruct(
                        urlCtx,
                        results,
                        this.GetActualAIProviderName());

                    return new GH_String(finalMarkdown);
                },
                messages);

            // Persist Format (computed locally, not via batch) — after FinishResults
            if (this._localFormat != null)
            {
                this.SetPersistentOutput("Format", this._localFormat, null);
            }
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
            private string imageMode;
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
                if (!this.parent.HasActiveBatchSubmission || this.parent._batchContextLost)
                {
                    this.parent._urlContextsInitialized = false;
                }

                var urlTree = new GH_Structure<GH_String>();
                DA.GetDataTree("URL", out urlTree);

                var imageModeParam = new GH_String();
                DA.GetData("Image Mode", ref imageModeParam);
                this.imageMode = imageModeParam?.Value?.ToLowerInvariant() ?? "link";
                if (this.imageMode != "link" && this.imageMode != "embed" && this.imageMode != "describe" && this.imageMode != "caption")
                {
                    this.imageMode = "link";
                }

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

                if (!this.parent._urlContextsInitialized)
                {
                    this.parent._urlContexts = new Dictionary<string, MarkdownImageBatchContext>();
                    this.parent._urlContextsInitialized = true;
                }

                this.parent._localFormat = null;

                if (!this.hasWork)
                {
                    return;
                }

                try
                {
                    var resultTrees = await this.parent.RunProcessingAsync(
                        this.inputTrees,
                        async branches => await this.ProcessBranches(branches, token).ConfigureAwait(false),
                        this.processingOptions,
                        token).ConfigureAwait(false);

                    this.resultMarkdown = DataTreeProcessor.ExtractTypedTree<GH_String>(resultTrees, "Markdown");
                    this.resultFormat = DataTreeProcessor.ExtractTypedTree<GH_String>(resultTrees, "Format");

                    this.parent._localFormat = this.resultFormat;

                    var markdownDict = new Dictionary<string, GH_Structure<GH_String>>
                    {
                        { "Markdown", this.resultMarkdown },
                    };
                    var batchSubmitted = await this.parent.TrySubmitBatchAsync("Markdown", markdownDict, token).ConfigureAwait(false);
                    if (batchSubmitted)
                    {
                        this.resultMarkdown = null;
                        Debug.WriteLine("[Web2Md] Sentinel tree stored, batch submitted");
                    }
                    else
                    {
                        this.parent.FinishResults(
                            "Markdown",
                            this.resultMarkdown ?? new GH_Structure<GH_String>(),
                            ("Format", (object)(this.resultFormat ?? new GH_Structure<GH_String>())));
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Web2Md] Error: {ex.Message}");
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
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
                        this.CollectMessage(SHRuntimeMessageSeverity.Warning, "Skipping empty or null URL.", SHRuntimeMessageOrigin.Worker);
                        outputs["Markdown"].Add(new GH_String(string.Empty));
                        outputs["Format"].Add(new GH_String(string.Empty));
                        continue;
                    }

                    string url = ghUrl.Value;

                    var parameters = new JObject
                    {
                        ["url"] = url,
                        ["includeLinks"] = true,
                        ["includeImages"] = true,
                        ["imageMode"] = "link",
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

                    var messages = RuntimeMessageUtility.ExtractMessages(toolResult);
                    foreach (var m in messages) this.CollectMessage(m);

                    bool isBatch = this.parent.IsBatchRequest();

                    var imageSlots = ExtractImageSlots(markdown);
                    foreach (var slot in imageSlots)
                    {
                        slot.ImageMode = this.imageMode;
                    }

                    var processingResult = await MarkdownImageBatchProcessor.ProcessAsync(
                        markdown,
                        imageSlots,
                        this.imageMode,
                        async imgParams =>
                        {
                            var result = await this.parent.CallAIToolAsync("img2text", imgParams, token).ConfigureAwait(false);
                            return result?["result"]?.ToString() ?? result?["description"]?.ToString() ?? string.Empty;
                        },
                        isBatch).ConfigureAwait(false);
                    string processedMarkdown = processingResult.Markdown;
                    MarkdownImageBatchContext batchContext = processingResult.BatchContext;

                    if (batchContext != null)
                    {
                        this.parent._urlContexts[batchContext.Images[0].SentinelId] = batchContext;
                    }

                    outputs["Markdown"].Add(new GH_String(processedMarkdown));
                    outputs["Format"].Add(new GH_String(format));
                }

                return outputs;
            }

            private static List<MarkdownImageSlot> ExtractImageSlots(string markdown)
            {
                var slots = new List<MarkdownImageSlot>();
                if (string.IsNullOrEmpty(markdown))
                {
                    return slots;
                }

                var matches = Regex.Matches(markdown, @"!\[([^]]*)\]\(([^)]+)\)");
                for (int i = 0; i < matches.Count; i++)
                {
                    var match = matches[i];
                    slots.Add(new MarkdownImageSlot
                    {
                        Index = i + 1,
                        ImageId = $"web-img-{i + 1}",
                        ImageContext = match.Groups[2].Value,
                        MimeType = "image/png",
                        Url = match.Groups[2].Value,
                        AltText = match.Groups[1].Value,
                        Placeholder = match.Value,
                    });
                }

                return slots;
            }

            public override void SetOutput(IGH_DataAccess DA, out string message)
            {
                // Outputs and metrics are handled by FinishResults (non-batch) or
                // ProcessBatchResults -> FinishResults (batch). RestorePersistentOutputs
                // replays them to the canvas on the next solve.
                message = null;
            }
        }
    }
}
