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
using SmartHopper.Core.ComponentBase.Batch;
using SmartHopper.Core.DataTree;
using SmartHopper.Core.Grasshopper.AITools;
using SmartHopper.Core.Grasshopper.Utils.Internal;
using SmartHopper.Core.Models;
using SmartHopper.Core.Types;
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AICall.Metrics;
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.Infrastructure.AICall.Utilities;
using SmartHopper.Infrastructure.Diagnostics;

namespace SmartHopper.Components.Input
{
    /// <summary>
    /// Fetches web content from a URL using the web2md AI tool and wraps it into an AIInputPayload.
    /// AI is used only for image description when Image Mode is set to embed, describe, or caption.
    /// Links and inline images are always kept in the raw Markdown output.
    /// </summary>
    public class Web2AIComponent : AIStatefulAsyncComponentBase
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
        /// Local Markdown tree produced during DoWorkAsync; used to build the AIInputPayload output.
        /// </summary>
        private GH_Structure<GH_String> _localMarkdown;

        public override Guid ComponentGuid => new Guid("A7294D39-8DCB-4178-A435-AD7D73BA5E14");

        protected override Bitmap Icon => Resources.toaiweb;

        public override GH_Exposure Exposure => GH_Exposure.septenary;

        /// <inheritdoc/>
        protected override IReadOnlyList<string> UsingAiTools => new[] { "web2md", "img2text" };

        /// <inheritdoc/>
        protected override ProcessingOptions ComponentProcessingOptions => new ProcessingOptions
        {
            Topology = ProcessingTopology.BranchToBranch,
            OnlyMatchingPaths = false,
            GroupIdenticalBranches = true,
        };

        public Web2AIComponent()
            : base(
                "Web to AI",
                "Web2AI",
                "Fetches web content from a URL using the web2md AI tool and wraps it into an AIInputPayload. Links and images are always kept in the raw Markdown. AI is used only for image description when Image Mode is not 'link'.",
                "SmartHopper",
                "B. Input")
        {
        }

        protected override void RegisterAdditionalInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("URL", "U", "Web URL(s) to fetch and convert to Markdown.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Image Mode", "IM", "How images appear in the Markdown. 'link' (default): keep remote image URLs as Markdown links. 'embed': download and embed as base64 data URIs with short AI captions. 'describe': replace with a long AI text description. 'caption': replace with a short AI-generated title. Requires an AI provider for embed/describe/caption.", GH_ParamAccess.tree, "link");
        }

        protected override void RegisterAdditionalOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddParameter(new AIInputPayloadParameter(), "Input >", ">", "AIInputPayload(s) containing the web content as Markdown.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Markdown", "M", "Extracted web content as Markdown.", GH_ParamAccess.tree);
        }

        protected override void OnEnteringNeedsRun()
        {
            base.OnEnteringNeedsRun();
            this._urlContexts = null;
            this._urlContextsInitialized = false;
            this._batchContextLost = false;
            this._localMarkdown = null;
        }

        /// <inheritdoc/>
        protected override void OnBatchCompleted(IReadOnlyDictionary<string, JObject> results, IReadOnlyList<SHRuntimeMessage> messages = null)
        {
            var sentinel = this.GetSentinelTree("Markdown");
            if (results == null || sentinel == null) return;

            // Map every sentinel custom ID in the Markdown tree to its output branch path.
            // All image slots for a single URL share the representative sentinel's path.
            var sentinelToPath = new Dictionary<string, GH_Path>();
            foreach (var path in sentinel.Paths)
            {
                var branch = sentinel.get_Branch(path);
                if (branch == null)
                {
                    continue;
                }

                foreach (GH_String item in branch)
                {
                    if (BatchSentinel.TryExtract(item?.Value ?? string.Empty, out var customId))
                    {
                        sentinelToPath[customId] = path;
                    }
                }
            }

            var slotMetricsByPath = new Dictionary<GH_Path, List<AIMetrics>>();

            var reconstructedMarkdown = this.ProcessBatchResults<GH_String>(
                "Markdown",
                sentinel,
                results,
                (representativeSentinelId, _) =>
                {
                    if (this._urlContexts == null ||
                        !this._urlContexts.TryGetValue(representativeSentinelId, out var urlCtx))
                    {
                        Debug.WriteLine($"[Web2AI] OnBatchCompleted: URL context missing for sentinel {representativeSentinelId}");
                        return new GH_String("[URL context missing]");
                    }

                    if (!sentinelToPath.TryGetValue(representativeSentinelId, out var representativePath))
                    {
                        representativePath = new GH_Path(0);
                    }

                    var markdown = MarkdownImageBatchProcessor.Reconstruct(
                        urlCtx,
                        results,
                        this.GetActualAIProviderName(),
                        metrics: null,
                        onImageResolved: (slotSentinelId, body, assistantText, description) =>
                        {
                            if (assistantText?.Metrics != null && slotSentinelId != representativeSentinelId)
                            {
                                if (string.IsNullOrEmpty(assistantText.Metrics.Provider))
                                {
                                    assistantText.Metrics.Provider = this.GetActualAIProviderName();
                                }

                                if (string.IsNullOrEmpty(assistantText.Metrics.Model))
                                {
                                    assistantText.Metrics.Model = this.GetModel();
                                }

                                if (!slotMetricsByPath.TryGetValue(representativePath, out var list))
                                {
                                    list = new List<AIMetrics>();
                                    slotMetricsByPath[representativePath] = list;
                                }

                                list.Add(assistantText.Metrics);
                            }
                        });

                    return new GH_String(markdown);
                },
                messages);

            if (slotMetricsByPath.Count > 0)
            {
                foreach (var kvp in slotMetricsByPath)
                {
                    var path = kvp.Key;
                    foreach (var m in kvp.Value)
                    {
                        this.CombineIntoPersistedMetricsAtPath(m, path, "tool:img2text");
                    }
                }

                var totalSlotMetrics = slotMetricsByPath.Values.Sum(l => l.Count);
                Debug.WriteLine($"[Web2AI] OnBatchCompleted: merged {totalSlotMetrics} slot metrics into {slotMetricsByPath.Count} branch path(s)");
                this.SetMetricsOutput(null);
            }

            // Build Input > tree from the reconstructed Markdown
            var inputPayloadTree = new GH_Structure<IGH_Goo>();
            foreach (var path in reconstructedMarkdown.Paths)
            {
                var branch = reconstructedMarkdown.get_Branch(path);
                if (branch == null) continue;
                foreach (var item in branch)
                {
                    if (item is GH_String gs)
                    {
                        var payload = string.IsNullOrWhiteSpace(gs.Value) ? null : AIInputPayload.FromText(gs.Value);
                        inputPayloadTree.Append(new GH_AIInputPayload(payload), path);
                    }
                }
            }

            this.SetPersistentOutput("Input >", inputPayloadTree, null);
        }

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
                if (!this.parent.HasActiveBatchSubmission || this.parent._batchContextLost)
                {
                    this.parent._urlContextsInitialized = false;
                }

                this.inputTrees = new Dictionary<string, GH_Structure<GH_String>>();

                var urlTree = new GH_Structure<GH_String>();
                DA.GetDataTree("URL", out urlTree);

                GH_Structure<GH_String> imageModeTree;
                DA.GetDataTree("Image Mode", out imageModeTree);

                this.inputTrees["URL"] = urlTree;
                this.inputTrees["ImageMode"] = imageModeTree ?? new GH_Structure<GH_String>();

                dataCount = 0;
            }

            public override async Task DoWorkAsync(CancellationToken token)
            {
                this.result = new Dictionary<string, GH_Structure<IGH_Goo>>
                {
                    { "Input >", new GH_Structure<IGH_Goo>() },
                    { "Markdown", new GH_Structure<IGH_Goo>() },
                };

                if (!this.parent._urlContextsInitialized)
                {
                    this.parent._urlContexts = new Dictionary<string, MarkdownImageBatchContext>();
                    this.parent._urlContextsInitialized = true;
                }

                this.parent._localMarkdown = null;

                try
                {
                    this.result = await this.parent.RunProcessingAsync(
                        this.inputTrees,
                        async branches => await this.ProcessBranches(branches, token).ConfigureAwait(false),
                        this.processingOptions,
                        token).ConfigureAwait(false);

                    var markdownTree = DataTreeProcessor.ExtractTypedTree<GH_String>(this.result, "Markdown");
                    this.parent._localMarkdown = markdownTree;

                    var markdownDict = new Dictionary<string, GH_Structure<GH_String>>
                    {
                        { "Markdown", markdownTree },
                    };
                    var batchSubmitted = await this.parent.TrySubmitBatchAsync("Markdown", markdownDict, token).ConfigureAwait(false);
                    if (batchSubmitted)
                    {
                        Debug.WriteLine("[Web2AI] Sentinel tree stored, batch submitted");
                    }
                    else
                    {
                        this.parent.FinishResults(
                            "Markdown",
                            markdownTree ?? new GH_Structure<GH_String>(),
                            ("Input >", (object)(this.result["Input >"] ?? new GH_Structure<IGH_Goo>())));
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Web2AIWorker] Error: {ex.Message}");
                    this.CollectMessage(SHRuntimeMessageSeverity.Error, $"Error processing web content: {ex.Message}");
                }
            }

            private async Task<Dictionary<string, List<IGH_Goo>>> ProcessBranches(Dictionary<string, List<GH_String>> branches, CancellationToken token)
            {
                var outputs = new Dictionary<string, List<IGH_Goo>>
                {
                    { "Input >", new List<IGH_Goo>() },
                    { "Markdown", new List<IGH_Goo>() },
                };

                var urlList = branches["URL"];
                var imageModeList = branches["ImageMode"];

                var normalizedLists = DataTreeProcessor.NormalizeBranchLengths(new List<List<GH_String>> { urlList, imageModeList });
                urlList = normalizedLists[0];
                imageModeList = normalizedLists[1];

                for (int i = 0; i < urlList.Count; i++)
                {
                    string url = urlList[i]?.Value;
                    string imageMode = imageModeList?[i]?.Value?.ToLowerInvariant() ?? "link";
                    if (imageMode != "link" && imageMode != "embed" && imageMode != "describe" && imageMode != "caption")
                    {
                        imageMode = "link";
                    }

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
                            outputs["Input >"].Add(new GH_AIInputPayload(null));
                            outputs["Markdown"].Add(new GH_String(string.Empty));
                            continue;
                        }

                        string markdown = toolResult.Result["content"]?.ToString() ?? string.Empty;

                        var messages = RuntimeMessageUtility.ExtractMessages(toolResult);
                        foreach (var m in messages) this.CollectMessage(m);

                        bool isBatch = this.parent.IsBatchRequest();

                        var imageSlots = ExtractImageSlots(markdown, imageMode);
                        var processingResult = await MarkdownImageBatchProcessor.ProcessAsync(
                            markdown,
                            imageSlots,
                            imageMode,
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

                        var payload = string.IsNullOrWhiteSpace(processedMarkdown)
                            ? null
                            : AIInputPayload.FromText(processedMarkdown);

                        outputs["Input >"].Add(new GH_AIInputPayload(payload));
                        outputs["Markdown"].Add(new GH_String(processedMarkdown));
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

            private static List<MarkdownImageSlot> ExtractImageSlots(string markdown, string imageMode)
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
                        ImageMode = imageMode,
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
                message = null;
            }
        }
    }
}
