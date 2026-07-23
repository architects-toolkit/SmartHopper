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
using SmartHopper.Infrastructure.AICall.Utilities;
using SmartHopper.ProviderSdk.AICall.Core.Base;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AICall.Metrics;
using SmartHopper.ProviderSdk.Diagnostics;

namespace SmartHopper.Components.Knowledge
{
    /// <summary>
    /// Grasshopper component that converts web pages (URLs) to Markdown with optional AI-powered image description.
    /// The initial web fetch is performed by the local <c>web2md</c> tool. AI is used only for image description
    /// when Image Mode is set to embed, describe, or caption.
    /// Use <c>Web2MdComponent</c> for plain conversion without AI.
    /// </summary>
    public class AIWeb2MdComponent : AIStatefulAsyncComponentBase
    {
        /// <summary>
        /// Per-URL context stored during DoWorkAsync so <see cref="OnBatchCompleted"/> can reconstruct
        /// the final Markdown once all batch image descriptions are available.
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

        /// <inheritdoc/>
        public override Guid ComponentGuid => new Guid("2FADA1EA-E6E2-43AC-B411-228D999F92A8");

        /// <inheritdoc/>
        protected override Bitmap Icon => Resources.webtomd;

        /// <inheritdoc/>
        public override GH_Exposure Exposure => GH_Exposure.primary;

        /// <inheritdoc/>
        public override IEnumerable<string> Keywords => new[]
        {
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
        protected override IReadOnlyList<string> UsingAiTools => new[] { "web2md", "img2text" };

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
                "Convert a web page (URL) to Markdown with optional AI-powered image description. Image Mode: 'link' (default) — keep remote image URLs as Markdown links; 'embed' — download and embed as base64 data URI with a short AI caption; 'describe' — long AI description; 'caption' — short AI-generated title.",
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
            pManager.AddTextParameter("Image Mode", "IM", "How images appear in the Markdown. 'link' (default): keep remote image URLs as Markdown links. 'embed': download and embed as base64 data URIs with short AI captions. 'describe': replace with a long AI text description. 'caption': replace with a short AI-generated title. Requires an AI provider for embed/describe/caption.", GH_ParamAccess.item, "link");
            pManager[pManager.ParamCount - 1].Optional = true;
        }

        /// <inheritdoc/>
        protected override void RegisterAdditionalOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Markdown", "Md", "Markdown content of the webpage with AI-generated image descriptions or captions embedded.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Format", "Fmt", "Detected content format (e.g., markdown, plain_text, url).", GH_ParamAccess.tree);
        }

        /// <inheritdoc/>
        public override bool Write(GH_IO.Serialization.GH_IWriter writer)
        {
            if (!base.Write(writer)) return false;

            try
            {
                if (this._urlContexts != null && this._urlContexts.Count > 0)
                {
                    writer.SetInt32("UrlContextCount", this._urlContexts.Count);
                    int idx = 0;
                    foreach (var kvp in this._urlContexts)
                    {
                        writer.SetString($"UrlContext_{idx}_Key", kvp.Key);
                        writer.SetString($"UrlContext_{idx}_BaseMarkdown", kvp.Value.BaseMarkdown ?? string.Empty);
                        writer.SetInt32($"UrlContext_{idx}_ImageCount", kvp.Value.Images?.Count ?? 0);

                        for (int i = 0; i < (kvp.Value.Images?.Count ?? 0); i++)
                        {
                            var img = kvp.Value.Images[i];
                            writer.SetInt32($"UrlContext_{idx}_Img_{i}_Index", img.Index);
                            writer.SetString($"UrlContext_{idx}_Img_{i}_SentinelId", img.SentinelId ?? string.Empty);
                            writer.SetString($"UrlContext_{idx}_Img_{i}_ImageId", img.ImageId ?? string.Empty);
                            writer.SetString($"UrlContext_{idx}_Img_{i}_ImageMode", img.ImageMode ?? string.Empty);
                            writer.SetString($"UrlContext_{idx}_Img_{i}_ImageContext", img.ImageContext ?? string.Empty);
                            writer.SetString($"UrlContext_{idx}_Img_{i}_MimeType", img.MimeType ?? string.Empty);
                            writer.SetString($"UrlContext_{idx}_Img_{i}_Base64Data", img.Base64Data ?? string.Empty);
                            writer.SetString($"UrlContext_{idx}_Img_{i}_Url", img.Url ?? string.Empty);
                            writer.SetString($"UrlContext_{idx}_Img_{i}_AltText", img.AltText ?? string.Empty);
                            writer.SetString($"UrlContext_{idx}_Img_{i}_Placeholder", img.Placeholder ?? string.Empty);
                        }

                        idx++;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AIWeb2Md] Write URL contexts error: {ex.Message}");
            }

            return true;
        }

        /// <inheritdoc/>
        public override bool Read(GH_IO.Serialization.GH_IReader reader)
        {
            if (!base.Read(reader)) return false;

            try
            {
                if (reader.ItemExists("UrlContextCount"))
                {
                    int count = reader.GetInt32("UrlContextCount");
                    this._urlContexts = new Dictionary<string, MarkdownImageBatchContext>();

                    for (int idx = 0; idx < count; idx++)
                    {
                        string key = reader.GetString($"UrlContext_{idx}_Key");
                        string baseMarkdown = reader.GetString($"UrlContext_{idx}_BaseMarkdown");
                        int imageCount = reader.GetInt32($"UrlContext_{idx}_ImageCount");

                        var images = new List<MarkdownImageSlot>(imageCount);
                        for (int i = 0; i < imageCount; i++)
                        {
                            images.Add(new MarkdownImageSlot
                            {
                                Index = reader.GetInt32($"UrlContext_{idx}_Img_{i}_Index"),
                                SentinelId = reader.GetString($"UrlContext_{idx}_Img_{i}_SentinelId"),
                                ImageId = reader.GetString($"UrlContext_{idx}_Img_{i}_ImageId"),
                                ImageMode = reader.GetString($"UrlContext_{idx}_Img_{i}_ImageMode"),
                                ImageContext = reader.GetString($"UrlContext_{idx}_Img_{i}_ImageContext"),
                                MimeType = reader.GetString($"UrlContext_{idx}_Img_{i}_MimeType"),
                                Base64Data = reader.GetString($"UrlContext_{idx}_Img_{i}_Base64Data"),
                                Url = reader.GetString($"UrlContext_{idx}_Img_{i}_Url"),
                                AltText = reader.GetString($"UrlContext_{idx}_Img_{i}_AltText"),
                                Placeholder = reader.GetString($"UrlContext_{idx}_Img_{i}_Placeholder"),
                            });
                        }

                        this._urlContexts[key] = new MarkdownImageBatchContext
                        {
                            BaseMarkdown = baseMarkdown,
                            Images = images,
                        };
                    }

                    this._urlContextsInitialized = true;
                    Debug.WriteLine($"[AIWeb2Md] Read: restored {count} URL contexts");
                }
                else
                {
                    this._batchContextLost = true;
                    Debug.WriteLine("[AIWeb2Md] Read: no URL contexts found — batch context lost");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AIWeb2Md] Read URL contexts error: {ex.Message}");
                this._batchContextLost = true;
            }

            return true;
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

            this.ProcessBatchResults<GH_String>(
                "Markdown",
                sentinel,
                results,
                (representativeSentinelId, _) =>
                {
                    if (this._urlContexts == null ||
                        !this._urlContexts.TryGetValue(representativeSentinelId, out var urlCtx))
                    {
                        Debug.WriteLine($"[AIWeb2Md] OnBatchCompleted: URL context missing for sentinel {representativeSentinelId}");
                        return new GH_String("[URL context missing]");
                    }

                    if (!sentinelToPath.TryGetValue(representativeSentinelId, out var representativePath))
                    {
                        representativePath = new GH_Path(0);
                    }

                    var finalMarkdown = MarkdownImageBatchProcessor.Reconstruct(
                        urlCtx,
                        results,
                        this.GetActualAIProviderName(),
                        metrics: null,
                        onImageResolved: (slotSentinelId, slotBody, assistantText, description) =>
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

                    return new GH_String(finalMarkdown);
                },
                messages);

            if (slotMetricsByPath.Count > 0)
            {
                foreach (var kvp in slotMetricsByPath)
                {
                    var path = kvp.Key;
                    foreach (var m in kvp.Value)
                    {
                        this.CombineIntoPersistedMetrics(m);
                    }
                }

                var totalSlotMetrics = slotMetricsByPath.Values.Sum(l => l.Count);
                Debug.WriteLine($"[AIWeb2Md] OnBatchCompleted: merged {totalSlotMetrics} slot metrics into {slotMetricsByPath.Count} branch path(s)");
                this.SetMetricsOutput(null);
            }

            if (this._localFormat != null)
            {
                this.SetPersistentOutput("Format", this._localFormat, null);
            }
        }

        /// <inheritdoc/>
        protected override void OnEnteringNeedsRun()
        {
            base.OnEnteringNeedsRun();
            this._urlContexts = null;
            this._urlContextsInitialized = false;
            this._batchContextLost = false;
            this._localFormat = null;
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
            private string imageMode;
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

            public override void GatherInput(IGH_DataAccess DA, out int dataCount)
            {
                if (!this.parent.HasActiveBatchSubmission || this.parent._batchContextLost)
                {
                    this.parent._urlContextsInitialized = false;
                }

                this.urlTree = new GH_Structure<GH_String>();
                DA.GetDataTree("URL", out this.urlTree);

                var imageModeParam = new GH_String();
                DA.GetData("Image Mode", ref imageModeParam);
                this.imageMode = imageModeParam?.Value?.ToLowerInvariant() ?? "link";
                if (this.imageMode != "link" && this.imageMode != "embed" && this.imageMode != "describe" && this.imageMode != "caption")
                {
                    this.imageMode = "link";
                }

                this.hasWork = this.urlTree != null && this.urlTree.PathCount > 0 && this.urlTree.DataCount > 0;
                dataCount = this.hasWork ? this.urlTree.DataCount : 0;

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
                    var inputTrees = new Dictionary<string, GH_Structure<GH_String>>
                    {
                        { "URL", this.urlTree },
                    };

                    var resultTrees = await this.parent.RunProcessingAsync<GH_String>(
                        inputTrees,
                        async branchInputs => await this.ProcessBranches(branchInputs, token).ConfigureAwait(false),
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
                        Debug.WriteLine("[AIWeb2Md] Sentinel tree stored, batch submitted");
                    }
                    else
                    {
                        this.parent.FinishResults(
                            "Markdown",
                            this.resultMarkdown ?? new GH_Structure<GH_String>(),
                            ("Format", (object)(this.resultFormat ?? new GH_Structure<GH_String>())));
                    }
                }
                catch (OperationCanceledException)
                {
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Operation was cancelled.");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[AIWeb2Md] Error: {ex.Message}");
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
                    catch (Exception ex)
                    {
                        this.CollectMessage(SHRuntimeMessageSeverity.Warning, $"Error fetching {url}: {ex.Message}");
                        outputs["Markdown"].Add(new GH_String(string.Empty));
                        outputs["Format"].Add(new GH_String(string.Empty));
                    }
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
                message = null;
            }
        }
    }
}