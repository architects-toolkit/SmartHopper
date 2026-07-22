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
using System.Text;
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
using SmartHopper.Core.Types;
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AICall.Metrics;
using SmartHopper.Infrastructure.AICall.Utilities;
using SmartHopper.Infrastructure.AIProviders;
using SmartHopper.Infrastructure.Diagnostics;

namespace SmartHopper.Components.Knowledge
{
    /// <summary>
    /// Grasshopper component that converts local files to Markdown with optional AI-powered image description.
    /// File conversion and image extraction are performed locally (no AI). Only image description calls
    /// use AI via <c>img2text</c> and are batchable.
    /// Use <c>File2MdComponent</c> for plain conversion without AI.
    /// </summary>
    public class AIFile2MdComponent : AIStatefulAsyncComponentBase
    {
        /// <summary>
        /// Per-file context stored during DoWorkAsync so OnBatchCompleted can reconstruct
        /// the final markdown. Each entry maps the representative sentinel ID (first image of the file)
        /// to a <see cref="FileBatchContext"/> containing the base markdown and all image slots.
        /// </summary>
        private Dictionary<string, FileBatchContext> _fileContexts;

        /// <summary>
        /// Flag to prevent clearing _fileContexts during polling re-runs.
        /// Set to true when contexts are initialized for a fresh run.
        /// </summary>
        private bool _fileContextsInitialized;

        /// <summary>
        /// Set to true when the batch is active but <see cref="_fileContexts"/> could not be
        /// restored (e.g. after a crash without save). Forces <see cref="GatherInput"/> to allow
        /// a fresh context rebuild on the next poll cycle.
        /// </summary>
        private bool _batchContextLost;

        /// <summary>
        /// Local Format tree produced during DoWorkAsync; does not need AI so it is always available.
        /// </summary>
        private GH_Structure<GH_String> _localFormat;

        /// <summary>
        /// Local Images tree produced during DoWorkAsync; does not need AI so it is always available.
        /// </summary>
        private GH_Structure<GH_VersatileImage> _localImages;

        /// <summary>
        /// Holds everything needed to assemble the final markdown for one file once all
        /// batch image descriptions are available. Keyed by the representative sentinel ID
        /// (first image's sentinel) in <see cref="_fileContexts"/>.
        /// </summary>
        private sealed class FileBatchContext
        {
            /// <summary>Gets or sets the base markdown with <c>[image N]</c> placeholders.</summary>
            public string BaseMarkdown { get; set; }

            /// <summary>Gets or sets the ordered list of image slots for this file.</summary>
            public List<ImageSlot> Images { get; set; }
        }

        /// <summary>
        /// Holds the metadata for one image within a file, including the sentinel ID that
        /// maps to its AI-generated description in the batch results.
        /// </summary>
        private sealed class ImageSlot
        {
            /// <summary>Gets or sets the 1-based image index matching the <c>[image N]</c> placeholder.</summary>
            public int Index { get; set; }

            /// <summary>Gets or sets the batch sentinel ID for this image's img2text request.</summary>
            public string SentinelId { get; set; }

            /// <summary>Gets or sets the image identifier.</summary>
            public string ImageId { get; set; }

            /// <summary>Gets or sets the image mode ('embed', 'describe', 'caption').</summary>
            public string ImageMode { get; set; }

            /// <summary>Gets or sets the image context label.</summary>
            public string ImageContext { get; set; }

            /// <summary>Gets or sets the image MIME type (used in embed mode).</summary>
            public string MimeType { get; set; }

            /// <summary>Gets or sets the image base64 data (used in embed mode).</summary>
            public string Base64Data { get; set; }
        }

        /// <inheritdoc/>
        public override Guid ComponentGuid => new Guid("574FA3D1-3BA2-4B69-8D9B-5A208CD7FC7D");

        /// <inheritdoc/>
        protected override Bitmap Icon => Resources.fileai;

        /// <inheritdoc/>
        public override GH_Exposure Exposure => GH_Exposure.primary;

        /// <inheritdoc/>
        public override IEnumerable<string> Keywords => new[] {
            "AIFileToMd",
            "AIFile2Md",
            "file2md",
            "filetomd",
            "File to Markdown",
            "Convert File",
            "File Conversion",
            "Document to MD",
            "PDF to Markdown",
            "Word to Markdown",
            "PowerPoint to Markdown",
            "EPUB to Markdown",
            "TXT to Markdown",
            "Text to Markdown",
            "CSV to Markdown",
            "JSON to Markdown",
            "XML to Markdown",
            "HTML to Markdown",
            "Excel to Markdown",
            "XLSX to Markdown",
            "Email to Markdown",
            "EML to Markdown",
            "RTF to Markdown",
            "File Analysis",
            "Document AI",
            "File Reader",
            "Document Parser",
            "Extract Text from File",
            "File Text",
            "Markdown Generate",
            "AI Document",
        };

        /// <inheritdoc/>
        protected override IReadOnlyList<string> UsingAiTools => new[] { "file2md", "img2text" };

        /// <inheritdoc/>
        protected override ProcessingOptions ComponentProcessingOptions => new ProcessingOptions
        {
            Topology = ProcessingTopology.ItemGraft,
            OnlyMatchingPaths = false,
            GroupIdenticalBranches = false,
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="AIFile2MdComponent"/> class.
        /// </summary>
        public AIFile2MdComponent()
            : base(
                  "AI File To Markdown",
                  "AIFile2Md",
                  "Convert a local file (PDF, DOCX, XLSX, PPTX, HTML, CSV, JSON, XML, TXT, EML, EPUB, RTF, etc.) to Markdown with AI-powered image description. Image Mode: 'embed' — embed as base64 with short caption; 'describe' — long AI description; 'caption' — short AI title only.",
                  "SmartHopper",
                  "Knowledge")
        {
        }

        /// <inheritdoc/>
        protected override void RegisterAdditionalInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("File Path", "F", "Absolute path(s) to the file(s) to convert.", GH_ParamAccess.tree);
            pManager.AddBooleanParameter("Remove Headers", "RH", "Attempt to remove headers and footers from PDF/DOCX. Default: true.", GH_ParamAccess.tree, true);
            pManager.AddTextParameter("Image Mode", "IM", "How AI describes images in the output:\n'embed' (default) — embed image as base64 data URI with a short AI-generated caption as alt text.\n'describe' — replace image with a long, detailed AI text description.\n'caption' — replace image with a short AI-generated title/caption.", GH_ParamAccess.item, "embed");
            pManager[pManager.ParamCount - 1].Optional = true;
            pManager.AddTextParameter("Image Prompt", "IP", "Custom prompt for AI image description. Overrides the built-in prompt for the selected mode.", GH_ParamAccess.item);
            pManager[pManager.ParamCount - 1].Optional = true;
            pManager.AddTextParameter("HTML Readability", "R", "HTML main-content extraction strategy for HTML/EPUB/EML content: auto (default), smartreader, heuristic, or off.", GH_ParamAccess.item, "auto");
            pManager[pManager.ParamCount - 1].Optional = true;
        }

        /// <inheritdoc/>
        protected override void RegisterAdditionalOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Markdown", "Md", "Markdown content of the file with AI-generated image descriptions or captions embedded.", GH_ParamAccess.tree);
            pManager.AddGenericParameter("Images", "Img", "Raw images extracted from the document (PDF/DOCX/PPTX). Each item is a VersatileImage carrying base64 data, MIME type, and source context.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Format", "Fmt", "Detected original format (e.g., pdf, docx, html).", GH_ParamAccess.tree);
        }

        /// <inheritdoc/>
        public override bool Write(GH_IO.Serialization.GH_IWriter writer)
        {
            if (!base.Write(writer)) return false;

            try
            {
                if (this._fileContexts != null && this._fileContexts.Count > 0)
                {
                    writer.SetInt32("FileContextCount", this._fileContexts.Count);
                    int idx = 0;
                    foreach (var kvp in this._fileContexts)
                    {
                        writer.SetString($"FileContext_{idx}_Key", kvp.Key);
                        writer.SetString($"FileContext_{idx}_BaseMarkdown", kvp.Value.BaseMarkdown ?? string.Empty);
                        writer.SetInt32($"FileContext_{idx}_ImageCount", kvp.Value.Images?.Count ?? 0);

                        for (int i = 0; i < (kvp.Value.Images?.Count ?? 0); i++)
                        {
                            var img = kvp.Value.Images[i];
                            writer.SetInt32($"FileContext_{idx}_Img_{i}_Index", img.Index);
                            writer.SetString($"FileContext_{idx}_Img_{i}_SentinelId", img.SentinelId ?? string.Empty);
                            writer.SetString($"FileContext_{idx}_Img_{i}_ImageId", img.ImageId ?? string.Empty);
                            writer.SetString($"FileContext_{idx}_Img_{i}_ImageMode", img.ImageMode ?? string.Empty);
                            writer.SetString($"FileContext_{idx}_Img_{i}_ImageContext", img.ImageContext ?? string.Empty);
                            writer.SetString($"FileContext_{idx}_Img_{i}_MimeType", img.MimeType ?? string.Empty);
                            writer.SetString($"FileContext_{idx}_Img_{i}_Base64Data", img.Base64Data ?? string.Empty);
                        }

                        idx++;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AIFile2Md] Write file contexts error: {ex.Message}");
            }

            return true;
        }

        /// <inheritdoc/>
        public override bool Read(GH_IO.Serialization.GH_IReader reader)
        {
            if (!base.Read(reader)) return false;

            try
            {
                if (reader.ItemExists("FileContextCount"))
                {
                    int count = reader.GetInt32("FileContextCount");
                    this._fileContexts = new Dictionary<string, FileBatchContext>();

                    for (int idx = 0; idx < count; idx++)
                    {
                        string key = reader.GetString($"FileContext_{idx}_Key");
                        string baseMarkdown = reader.GetString($"FileContext_{idx}_BaseMarkdown");
                        int imageCount = reader.GetInt32($"FileContext_{idx}_ImageCount");

                        var images = new List<ImageSlot>(imageCount);
                        for (int i = 0; i < imageCount; i++)
                        {
                            images.Add(new ImageSlot
                            {
                                Index = reader.GetInt32($"FileContext_{idx}_Img_{i}_Index"),
                                SentinelId = reader.GetString($"FileContext_{idx}_Img_{i}_SentinelId"),
                                ImageId = reader.GetString($"FileContext_{idx}_Img_{i}_ImageId"),
                                ImageMode = reader.GetString($"FileContext_{idx}_Img_{i}_ImageMode"),
                                ImageContext = reader.GetString($"FileContext_{idx}_Img_{i}_ImageContext"),
                                MimeType = reader.GetString($"FileContext_{idx}_Img_{i}_MimeType"),
                                Base64Data = reader.GetString($"FileContext_{idx}_Img_{i}_Base64Data"),
                            });
                        }

                        this._fileContexts[key] = new FileBatchContext
                        {
                            BaseMarkdown = baseMarkdown,
                            Images = images,
                        };
                    }

                    this._fileContextsInitialized = true;
                    System.Diagnostics.Debug.WriteLine($"[AIFile2Md] Read: restored {count} file contexts");
                }
                else
                {
                    // No persisted contexts — if a batch is active this is a context-lost situation
                    this._batchContextLost = true;
                    System.Diagnostics.Debug.WriteLine("[AIFile2Md] Read: no file contexts found — batch context lost");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AIFile2Md] Read file contexts error: {ex.Message}");
                this._batchContextLost = true;
            }

            return true;
        }

        /// <inheritdoc/>
        protected override void OnBatchCompleted(IReadOnlyDictionary<string, JObject> results, IReadOnlyList<SHRuntimeMessage> messages = null)
        {
            var sentinel = this.GetSentinelTree("Markdown");
            if (results == null || sentinel == null) return;

            var provider = ProviderManager.Instance.GetProvider(this.GetActualAIProviderName());

            // Accumulate metrics from every per-slot decode (one AI call per image).
            // ProcessBatchResults only sees the representative sentinel per file, so per-slot
            // metrics (slots 2..N per file) must be collected manually and merged before FinishResults.
            var allSlotMetrics = new List<AIMetrics>();

            // ProcessBatchResults handles primary Markdown persistence and calls FinishResults.
            // Per-slot metrics are merged into the snapshot immediately after, before FinishResults
            // reads the snapshot to emit metrics — this works because FinishResults is called from
            // inside ProcessBatchResults after SetAIReturnSnapshot.
            this.ProcessBatchResults<GH_String>(
                "Markdown",
                sentinel,
                results,
                (representativeSentinelId, _) =>
                {
                    // Look up the file context stored during batch collection.
                    if (this._fileContexts == null ||
                        !this._fileContexts.TryGetValue(representativeSentinelId, out var fileCtx))
                    {
                        Debug.WriteLine($"[AIFile2Md] OnBatchCompleted: File context missing for sentinel {representativeSentinelId}");
                        return new GH_String("[File context missing]");
                    }

                    var sb = new StringBuilder(fileCtx.BaseMarkdown);

                    // Replace each [image N] placeholder with the AI-generated description.
                    foreach (var slot in fileCtx.Images)
                    {
                        string description = "[Image could not be described]";
                        if (provider != null && results.TryGetValue(slot.SentinelId, out var slotBody))
                        {
                            var interactions = provider.Decode(slotBody);
                            var assistantText = interactions?.OfType<AIInteractionText>()
                                .FirstOrDefault(i => i.Agent == AIAgent.Assistant);
                            if (!string.IsNullOrWhiteSpace(assistantText?.Content))
                            {
                                description = assistantText.Content;
                            }

                            // Collect metrics from non-representative slots for manual merging.
                            if (assistantText?.Metrics != null && slot.SentinelId != representativeSentinelId)
                            {
                                // Ensure slot metrics have provider/model set before combining
                                if (string.IsNullOrEmpty(assistantText.Metrics.Provider))
                                {
                                    assistantText.Metrics.Provider = this.GetActualAIProviderName();
                                }

                                if (string.IsNullOrEmpty(assistantText.Metrics.Model))
                                {
                                    assistantText.Metrics.Model = this.GetModel();
                                }

                                allSlotMetrics.Add(assistantText.Metrics);
                            }
                        }

                        string placeholder = $"[image {slot.Index}]";
                        string replacement = slot.ImageMode == "embed"
                            ? $"![{description}](data:{slot.MimeType};base64,{slot.Base64Data})"
                            : $"**[{slot.ImageId} — {slot.ImageContext}]**\n\n{description}";

                        sb.Replace(placeholder, replacement);
                    }

                    return new GH_String(sb.ToString());
                },
                messages);

            // Merge per-slot metrics (slots 2..N per file) into the single authoritative
            // _persistedMetrics set by ProcessBatchResults. CombineIntoPersistedMetrics
            // writes into the persistent field — not into the computed AIReturn.Metrics property.
            if (allSlotMetrics.Count > 0)
            {
                foreach (var m in allSlotMetrics)
                {
                    this.CombineIntoPersistedMetrics(m);
                }

                Debug.WriteLine($"[AIFile2Md] OnBatchCompleted: merged {allSlotMetrics.Count} slot metrics via CombineIntoPersistedMetrics");

                // Re-emit metrics now that per-slot tokens are included
                this.SetMetricsOutput(null);
            }

            // Persist Format and Images (computed locally, not via batch) — after FinishResults
            if (this._localFormat != null)
            {
                this.SetPersistentOutput("Format", this._localFormat, null);
            }

            if (this._localImages != null)
            {
                this.SetPersistentOutput("Images", this._localImages, null);
            }
        }

        /// <inheritdoc/>
        protected override void OnEnteringNeedsRun()
        {
            base.OnEnteringNeedsRun();
            this._fileContexts = null;
            this._fileContextsInitialized = false;
            this._batchContextLost = false;
            this._localFormat = null;
            this._localImages = null;
        }

        /// <inheritdoc/>
        protected override AsyncWorkerBase CreateWorker(Action<string> progressReporter)
        {
            return new AIFile2MdWorker(this, this.AddRuntimeMessage, this.ComponentProcessingOptions);
        }

        private sealed class AIFile2MdWorker : AsyncWorkerBase
        {
            private readonly AIFile2MdComponent parent;
            private readonly ProcessingOptions processingOptions;
            private GH_Structure<GH_String> filePathTree;
            private GH_Structure<GH_String> removeHeadersTree;
            private bool hasWork;

            private string imageMode;
            private string imagePrompt;
            private string htmlReadabilityMode;

            private GH_Structure<GH_String> resultMarkdown;
            private GH_Structure<GH_String> resultFormat;
            private GH_Structure<GH_VersatileImage> resultImages;

            private const string DefaultImageDescriptionPrompt =
                "Describe this image thoroughly for someone who cannot see it. Include: the main subject and overall scene, all visible objects and their spatial arrangement, any text, numbers, labels, charts, diagrams, or data visible in the image, colors and lighting when relevant, the apparent purpose or context of the image (e.g., photograph, technical diagram, screenshot, infographic), and any other details necessary to fully convey the image content. Be precise, complete, and well-structured.";

            private const string DefaultImageCaptionPrompt =
                "Write a concise, descriptive caption for this image in one sentence.";

            public AIFile2MdWorker(
                AIFile2MdComponent parent,
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
                // Clear file context flag for a genuinely fresh run only.
                // A poll cycle has an active _batchSubmission; a fresh run (even in batch mode)
                // does not. Resetting on _batchSubmission == null ensures old file contexts from
                // a previous batch run are discarded when the user re-runs the component.
                if (!this.parent.HasActiveBatchSubmission || this.parent._batchContextLost)
                {
                    this.parent._fileContextsInitialized = false;
                }

                this.filePathTree = new GH_Structure<GH_String>();
                DA.GetDataTree("File Path", out this.filePathTree);

                GH_Structure<GH_Boolean> removeTree;
                DA.GetDataTree("Remove Headers", out removeTree);

                this.removeHeadersTree = ConvertBoolTreeToString(removeTree, "true");

                var imageModeParam = new GH_String();
                DA.GetData("Image Mode", ref imageModeParam);
                this.imageMode = imageModeParam?.Value ?? "embed";

                var imagePromptParam = new GH_String();
                DA.GetData("Image Prompt", ref imagePromptParam);
                this.imagePrompt = imagePromptParam?.Value;

                var readabilityParam = new GH_String("auto");
                DA.GetData("HTML Readability", ref readabilityParam);
                this.htmlReadabilityMode = readabilityParam?.Value ?? "auto";

                this.hasWork = this.filePathTree != null && this.filePathTree.PathCount > 0 && this.filePathTree.DataCount > 0;
                dataCount = this.hasWork ? this.filePathTree.DataCount : 0;

                this.resultMarkdown = new GH_Structure<GH_String>();
                this.resultFormat = new GH_Structure<GH_String>();
                this.resultImages = new GH_Structure<GH_VersatileImage>();
            }

            /// <inheritdoc/>
            public override async Task DoWorkAsync(CancellationToken token)
            {
                this.resultMarkdown = new GH_Structure<GH_String>();
                this.resultFormat = new GH_Structure<GH_String>();
                this.resultImages = new GH_Structure<GH_VersatileImage>();

                // Reset per-run component-level context used by OnBatchCompleted
                // Only reset if not already initialized (prevents clearing during batch polling)
                if (!this.parent._fileContextsInitialized)
                {
                    this.parent._fileContexts = new Dictionary<string, FileBatchContext>();
                    this.parent._fileContextsInitialized = true;
                }

                this.parent._localFormat = null;
                this.parent._localImages = null;

                if (!this.hasWork)
                {
                    return;
                }

                string effectivePrompt = string.IsNullOrWhiteSpace(this.imagePrompt)
                    ? (this.imageMode == "describe" ? DefaultImageDescriptionPrompt : DefaultImageCaptionPrompt)
                    : this.imagePrompt;

                try
                {
                    var inputTrees = new Dictionary<string, GH_Structure<GH_String>>
                    {
                        { "File Path", this.filePathTree },
                        { "RemoveHeaders", this.removeHeadersTree },
                    };

                    var resultTrees = await this.parent.RunProcessingAsync<GH_String>(
                        inputTrees,
                        async branchInputs =>
                        {
                            var outputs = new Dictionary<string, List<IGH_Goo>>
                            {
                                { "Markdown", new List<IGH_Goo>() },
                                { "Format", new List<IGH_Goo>() },
                                { "Images", new List<IGH_Goo>() },
                            };

                            if (!branchInputs.TryGetValue("File Path", out var pathBranch) || pathBranch == null)
                            {
                                return outputs;
                            }

                            var removeBranch = branchInputs.TryGetValue("RemoveHeaders", out var rh) ? rh : new List<GH_String>();

                            var normalizedLists = DataTreeProcessor.NormalizeBranchLengths(new List<List<GH_String>> { pathBranch, removeBranch });
                            pathBranch = normalizedLists[0];
                            removeBranch = normalizedLists[1];

                            for (int i = 0; i < pathBranch.Count; i++)
                            {
                                token.ThrowIfCancellationRequested();

                                var ghPath = pathBranch[i];
                                bool removeHeaders = bool.TryParse(removeBranch[i]?.Value, out var rhValue) ? rhValue : true;

                                if (ghPath == null || string.IsNullOrWhiteSpace(ghPath.Value))
                                {
                                    outputs["Markdown"].Add(new GH_String(string.Empty));
                                    outputs["Format"].Add(new GH_String(string.Empty));
                                    continue;
                                }

                                string filePath = ghPath.Value;

                                // Step 1: call file2md locally — no AI, no image description
                                var localParams = new JObject
                                {
                                    ["filePath"] = filePath,
                                    ["preserveTableStructure"] = true,
                                    ["removeHeadersFooters"] = true,
                                    ["describeImages"] = false,
                                    ["extractImages"] = true,
                                };

                                if (!string.IsNullOrWhiteSpace(this.htmlReadabilityMode) &&
                                    !string.Equals(this.htmlReadabilityMode, "auto", StringComparison.OrdinalIgnoreCase))
                                {
                                    localParams["HTMLreadabilityMode"] = this.htmlReadabilityMode;
                                }

                                var localResult = await this.parent.CallAIToolAsync("file2md", localParams, token).ConfigureAwait(false);

                                if (localResult == null)
                                {
                                    this.CollectMessage(SHRuntimeMessageSeverity.Error, $"Tool 'file2md' returned no result for '{filePath}'.", SHRuntimeMessageOrigin.Tool);
                                    outputs["Markdown"].Add(new GH_String(string.Empty));
                                    outputs["Format"].Add(new GH_String(string.Empty));
                                    continue;
                                }

                                string baseMarkdown = localResult["content"]?.ToString() ?? string.Empty;
                                outputs["Format"].Add(new GH_String(localResult["originalFormat"]?.ToString() ?? string.Empty));

                                var imagesArray = localResult["images"] as JArray;

                                // Collect raw images for the Images output
                                if (imagesArray != null)
                                {
                                    int idx = 1;
                                    foreach (var imgToken in imagesArray)
                                    {
                                        var imgObj = imgToken as JObject;
                                        if (imgObj == null) continue;
                                        var imageSource = VersatileImage.FromExtractedDocument(
                                            base64Data: imgObj["base64Data"]?.ToString() ?? string.Empty,
                                            mimeType: imgObj["mimeType"]?.ToString() ?? "image/png",
                                            id: imgObj["id"]?.ToString() ?? "img",
                                            context: imgObj["context"]?.ToString() ?? string.Empty,
                                            pageOrSlide: imgObj["pageOrSlide"]?.Value<int>() ?? 0,
                                            sourceDocument: ghPath.Value);
                                        outputs["Images"].Add(new GH_VersatileImage(imageSource));
                                    }
                                }

                                var localMessages = RuntimeMessageUtility.ExtractMessages(localResult);
                                foreach (var m in localMessages)
                                {
                                    this.CollectMessage(m);
                                }

                                // Step 2: if no images, emit base markdown directly
                                if (imagesArray == null || imagesArray.Count == 0)
                                {
                                    outputs["Markdown"].Add(new GH_String(baseMarkdown));
                                    continue;
                                }

                                // Step 3: call img2text per image via CallAIToolAsync
                                // In batch mode these calls return sentinels; in normal mode they run async.
                                var fileSentinelIds = new List<string>();
                                var imgDescriptions = new List<string>();
                                bool isBatch = this.parent.IsBatchRequest();

                                foreach (var imgToken in imagesArray)
                                {
                                    var imgObj = imgToken as JObject;
                                    if (imgObj == null) continue;

                                    var imgParams = new JObject
                                    {
                                        ["imageBase64"] = imgObj["base64Data"]?.ToString(),
                                        ["mimeType"] = imgObj["mimeType"]?.ToString() ?? "image/png",
                                        ["prompt"] = effectivePrompt,
                                    };

                                    var imgResult = await this.parent.CallAIToolAsync("img2text", imgParams, token).ConfigureAwait(false);
                                    var resultStr = imgResult?["result"]?.ToString()
                                        ?? imgResult?["description"]?.ToString()
                                        ?? string.Empty;

                                    if (isBatch && BatchSentinel.TryExtract(resultStr, out var sentinelId))
                                    {
                                        // Sentinel placeholder: record context for OnBatchCompleted
                                        fileSentinelIds.Add(sentinelId);
                                    }
                                    else
                                    {
                                        imgDescriptions.Add(resultStr);
                                    }
                                }

                                if (isBatch && fileSentinelIds.Count > 0)
                                {
                                    // Build one FileBatchContext per file containing the base markdown
                                    // and an ordered list of ImageSlots (one per image).
                                    // Each ImageSlot knows its own sentinel ID so OnBatchCompleted
                                    // can fetch its AI description from the batch results directly.
                                    var fileCtx = new FileBatchContext
                                    {
                                        BaseMarkdown = baseMarkdown,
                                        Images = new List<ImageSlot>(),
                                    };

                                    int imgIndex = 0;
                                    foreach (var imgToken2 in imagesArray)
                                    {
                                        var imgObj2 = imgToken2 as JObject;
                                        if (imgObj2 == null) continue;
                                        if (imgIndex >= fileSentinelIds.Count) break;

                                        fileCtx.Images.Add(new ImageSlot
                                        {
                                            Index = imgIndex + 1, // 1-based, matches [image N] placeholder
                                            SentinelId = fileSentinelIds[imgIndex],
                                            ImageId = imgObj2["id"]?.ToString() ?? "img",
                                            ImageMode = this.imageMode,
                                            ImageContext = imgObj2["context"]?.ToString() ?? string.Empty,
                                            MimeType = imgObj2["mimeType"]?.ToString() ?? "image/png",
                                            Base64Data = imgObj2["base64Data"]?.ToString() ?? string.Empty,
                                        });
                                        imgIndex++;
                                    }

                                    // Store the file context under the representative sentinel (first image).
                                    this.parent._fileContexts[fileSentinelIds[0]] = fileCtx;

                                    // Only ONE sentinel enters the output tree — one per file.
                                    // OnBatchCompleted reads the FileBatchContext and assembles all images.
                                    outputs["Markdown"].Add(new GH_String(BatchSentinel.Wrap(fileSentinelIds[0])));
                                }
                                else
                                {
                                    // Normal (non-batch) mode: replace placeholders with descriptions inline
                                    var sb = new StringBuilder(baseMarkdown);

                                    int descIdx = 0;
                                    int imageIdx = 1;
                                    foreach (var imgToken2 in imagesArray)
                                    {
                                        var imgObj2 = imgToken2 as JObject;
                                        if (imgObj2 == null) continue;

                                        string aiText = descIdx < imgDescriptions.Count ? imgDescriptions[descIdx] : "[Image could not be described]";
                                        descIdx++;

                                        string placeholder = $"[image {imageIdx}]";
                                        string replacement;

                                        if (this.imageMode == "embed")
                                        {
                                            replacement = $"![{aiText}](data:{imgObj2["mimeType"]};base64,{imgObj2["base64Data"]})";
                                        }
                                        else
                                        {
                                            // describe or caption: text-only block
                                            replacement = $"**[{imgObj2["id"]} \u2014 {imgObj2["context"]}]**\n\n{aiText}";
                                        }

                                        sb.Replace(placeholder, replacement);
                                        imageIdx++;
                                    }

                                    outputs["Markdown"].Add(new GH_String(sb.ToString()));
                                }

                                outputs["Markdown"].Add(new GH_String(processedMarkdown));
                            }

                            return outputs;
                        },
                        this.processingOptions,
                        token).ConfigureAwait(false);

                    this.resultMarkdown = DataTreeProcessor.ExtractTypedTree<GH_String>(resultTrees, "Markdown");
                    this.resultFormat = DataTreeProcessor.ExtractTypedTree<GH_String>(resultTrees, "Format");
                    this.resultImages = DataTreeProcessor.ExtractTypedTree<GH_VersatileImage>(resultTrees, "Images");

                    // Store Format and Images on the component so OnBatchCompleted can persist them
                    this.parent._localFormat = this.resultFormat;
                    this.parent._localImages = this.resultImages;

                    var markdownDict = new Dictionary<string, GH_Structure<GH_String>>
                    {
                        { "Markdown", this.resultMarkdown },
                    };
                    var batchSubmitted = await this.parent.TrySubmitBatchAsync("Markdown", markdownDict, token).ConfigureAwait(false);
                    if (batchSubmitted)
                    {
                        this.resultMarkdown = null;
                        Debug.WriteLine("[AIFile2Md] Sentinel tree stored, batch submitted");
                    }
                    else
                    {
                        // Non-batch: persist all outputs and emit metrics via FinishResults
                        this.parent.FinishResults(
                            "Markdown",
                            this.resultMarkdown ?? new GH_Structure<GH_String>(),
                            ("Images", (object)(this.resultImages ?? new GH_Structure<GH_VersatileImage>())),
                            ("Format", (object)(this.resultFormat ?? new GH_Structure<GH_String>())));
                    }
                }
                catch (OperationCanceledException)
                {
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Operation was cancelled.");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[AIFile2Md] Error: {ex.Message}");
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
                }
            }

            /// <inheritdoc/>
            public override void SetOutput(IGH_DataAccess DA, out string errorMessage)
            {
                // Outputs and metrics are handled by FinishResults (non-batch) or
                // ProcessBatchResults → FinishResults (batch). RestorePersistentOutputs
                // replays them to the canvas on the next solve.
                errorMessage = null;
            }

            private static GH_Structure<GH_String> ConvertBoolTreeToString(GH_Structure<GH_Boolean> boolTree, string defaultValue)
            {
                var result = new GH_Structure<GH_String>();
                foreach (var path in boolTree.Paths)
                {
                    var branch = boolTree.get_Branch(path);
                    if (branch != null && branch.Count > 0)
                    {
                        var firstBool = branch[0] as GH_Boolean;
                        result.Append(new GH_String(firstBool?.Value.ToString().ToLowerInvariant() ?? defaultValue), path);
                    }
                    else
                    {
                        result.Append(new GH_String(defaultValue), path);
                    }
                }

                return result;
            }
        }
    }
}
