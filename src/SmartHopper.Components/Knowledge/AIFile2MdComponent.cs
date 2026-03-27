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
using SmartHopper.Core.DataTree;
using SmartHopper.Core.Grasshopper.Types;
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AIProviders;

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
        /// Per-sentinel context stored during DoWorkAsync so OnBatchCompleted can reconstruct
        /// the final markdown. Each entry maps an img2text sentinel customId to an <see cref="ImageSentinelContext"/>.
        /// </summary>
        private Dictionary<string, ImageSentinelContext> _sentinelContexts;

        /// <summary>
        /// Flag to prevent clearing _sentinelContexts during polling re-runs.
        /// Set to true when contexts are initialized for a fresh run.
        /// </summary>
        private bool _sentinelContextsInitialized;

        /// <summary>
        /// Local Format tree produced during DoWorkAsync; does not need AI so it is always available.
        /// </summary>
        private GH_Structure<GH_String> _localFormat;

        /// <summary>
        /// Local Images tree produced during DoWorkAsync; does not need AI so it is always available.
        /// </summary>
        private GH_Structure<GH_ExtractedImage> _localImages;

        /// <summary>
        /// Holds per-file assembly state so OnBatchCompleted can rebuild the markdown strings
        /// once all img2text batch results are available.
        /// </summary>
        private sealed class ImageSentinelContext
        {
            /// <summary>Gets or sets the base markdown content (with placeholders).</summary>
            public string BaseMarkdown { get; set; }

            /// <summary>Gets or sets the image mode ('embed', 'describe', 'caption').</summary>
            public string ImageMode { get; set; }

            /// <summary>Gets or sets the image context label (used in embed/describe output).</summary>
            public string ImageContext { get; set; }

            /// <summary>Gets or sets the image id (used in describe/caption output).</summary>
            public string ImageId { get; set; }

            /// <summary>Gets or sets the image MIME type (used in embed output).</summary>
            public string MimeType { get; set; }

            /// <summary>Gets or sets the image base64 data (used in embed output).</summary>
            public string Base64Data { get; set; }

            /// <summary>Gets or sets the ordered list of all sentinel ids for the same file, used to assemble the images section once all results are available.</summary>
            public List<string> FileSentinelIds { get; set; }

            /// <summary>Gets or sets the 1-based image index (for placeholder replacement).</summary>
            public int ImageIndex { get; set; }
        }

        /// <inheritdoc/>
        public override Guid ComponentGuid => new Guid("574FA3D1-3BA2-4B69-8D9B-5A208CD7FC7D");

        /// <inheritdoc/>
        protected override Bitmap Icon => Resources.filetomd;

        /// <inheritdoc/>
        public override GH_Exposure Exposure => GH_Exposure.tertiary;

        /// <inheritdoc/>
        public override IEnumerable<string> Keywords => new[] {
            "AIFileToMd",
            "AIFile2Md",
            "file2md",
            "filetomd",
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
            this.RunOnlyOnInputChanges = false;
        }

        /// <inheritdoc/>
        protected override void RegisterAdditionalInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("File Path", "F", "Absolute path(s) to the file(s) to convert.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Image Mode", "IM", "How AI describes images in the output:\n'embed' (default) — embed image as base64 data URI with a short AI-generated caption as alt text.\n'describe' — replace image with a long, detailed AI text description.\n'caption' — replace image with a short AI-generated title/caption.", GH_ParamAccess.item, "embed");
            pManager[pManager.ParamCount - 1].Optional = true;
            pManager.AddTextParameter("Image Prompt", "IP", "Custom prompt for AI image description. Overrides the built-in prompt for the selected mode.", GH_ParamAccess.item);
            pManager[pManager.ParamCount - 1].Optional = true;
        }

        /// <inheritdoc/>
        protected override void RegisterAdditionalOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Markdown", "Md", "Markdown content of the file with AI-generated image descriptions or captions embedded.", GH_ParamAccess.tree);
            pManager.AddGenericParameter("Images", "Img", "Raw images extracted from the document (PDF/DOCX/PPTX). Each item is a GH_ExtractedImage carrying base64 data, MIME type, and source context. Branched per input file.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Format", "Fmt", "Detected original format (e.g., pdf, docx, html).", GH_ParamAccess.tree);
        }

        /// <inheritdoc/>
        protected override void OnBatchCompleted(IReadOnlyDictionary<string, JObject> results)
        {
            var sentinel = this.GetSentinelTree("Markdown");
            if (results == null || sentinel == null) return;

            var contexts = this._sentinelContexts;

            var reconstructed = this.ProcessBatchResults<GH_String>(
                "Markdown",
                sentinel,
                results,
                (customId, resultBody) =>
                {
                    // Decode the img2text description from the batch result body
                    var provider = ProviderManager.Instance.GetProvider(this.GetActualAIProviderName());
                    string description = "[Image could not be described]";
                    if (provider != null)
                    {
                        var interactions = provider.Decode(resultBody);
                        var lastText = interactions
                            ?.OfType<AIInteractionText>()
                            .LastOrDefault(i => i.Agent == AIAgent.Assistant);
                        if (lastText != null)
                        {
                            description = lastText.Content ?? description;
                        }
                    }

                    // Rebuild the images section for this file using all sentinels in order
                    if (contexts == null || !contexts.TryGetValue(customId, out var ctx))
                    {
                        return new GH_String(description);
                    }

                    // Start with the base markdown containing placeholders
                    var sb = new StringBuilder(ctx.BaseMarkdown);

                    // Replace each placeholder with the corresponding image description
                    foreach (var siblingId in ctx.FileSentinelIds)
                    {
                        if (!contexts.TryGetValue(siblingId, out var sibCtx)) continue;

                        string sibDescription;
                        if (siblingId == customId)
                        {
                            sibDescription = description;
                        }
                        else if (results.TryGetValue(siblingId, out var sibBody) && provider != null)
                        {
                            var sibInteractions = provider.Decode(sibBody);
                            var sibText = sibInteractions
                                ?.OfType<AIInteractionText>()
                                .LastOrDefault(i => i.Agent == AIAgent.Assistant);
                            sibDescription = sibText?.Content ?? "[Image could not be described]";
                        }
                        else
                        {
                            sibDescription = "[Image could not be described]";
                        }

                        string placeholder = $"[image {sibCtx.ImageIndex}]";
                        string replacement;

                        if (sibCtx.ImageMode == "embed")
                        {
                            replacement = $"![{sibDescription}](data:{sibCtx.MimeType};base64,{sibCtx.Base64Data})";
                        }
                        else
                        {
                            // describe or caption: text-only block
                            replacement = $"**[{sibCtx.ImageId} \u2014 {sibCtx.ImageContext}]**\n\n{sibDescription}";
                        }

                        sb.Replace(placeholder, replacement);
                    }

                    // Only return the assembled markdown for the first sentinel of this file;
                    // return empty for the others so the tree structure is preserved correctly.
                    if (ctx.FileSentinelIds.Count > 0 && ctx.FileSentinelIds[0] == customId)
                    {
                        return new GH_String(sb.ToString());
                    }

                    return new GH_String(string.Empty);
                });

            this.StoreReconstructedTree("Markdown", reconstructed);

            // Also persist Format and Images (computed locally, not via batch)
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
        protected override AsyncWorkerBase CreateWorker(Action<string> progressReporter)
        {
            return new AIFile2MdWorker(this, this.AddRuntimeMessage, this.ComponentProcessingOptions);
        }

        private sealed class AIFile2MdWorker : AsyncWorkerBase
        {
            private readonly AIFile2MdComponent parent;
            private readonly ProcessingOptions processingOptions;
            private GH_Structure<GH_String> filePathTree;
            private bool hasWork;

            private string imageMode;
            private string imagePrompt;

            private GH_Structure<GH_String> resultMarkdown;
            private GH_Structure<GH_String> resultFormat;
            private GH_Structure<GH_ExtractedImage> resultImages;

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
                // Clear sentinel context flag for fresh run (resets on new input)
                this.parent._sentinelContextsInitialized = false;

                this.filePathTree = new GH_Structure<GH_String>();
                DA.GetDataTree("File Path", out this.filePathTree);

                var imageModeParam = new GH_String();
                DA.GetData("Image Mode", ref imageModeParam);
                this.imageMode = imageModeParam?.Value ?? "embed";

                var imagePromptParam = new GH_String();
                DA.GetData("Image Prompt", ref imagePromptParam);
                this.imagePrompt = imagePromptParam?.Value;

                this.hasWork = this.filePathTree != null && this.filePathTree.PathCount > 0 && this.filePathTree.DataCount > 0;
                dataCount = this.hasWork ? this.filePathTree.DataCount : 0;

                this.resultMarkdown = new GH_Structure<GH_String>();
                this.resultFormat = new GH_Structure<GH_String>();
                this.resultImages = new GH_Structure<GH_ExtractedImage>();
            }

            /// <inheritdoc/>
            public override async Task DoWorkAsync(CancellationToken token)
            {
                this.resultMarkdown = new GH_Structure<GH_String>();
                this.resultFormat = new GH_Structure<GH_String>();
                this.resultImages = new GH_Structure<GH_ExtractedImage>();

                // Reset per-run component-level context used by OnBatchCompleted
                // Only reset if not already initialized (prevents clearing during batch polling)
                if (!this.parent._sentinelContextsInitialized)
                {
                    this.parent._sentinelContexts = new Dictionary<string, ImageSentinelContext>();
                    this.parent._sentinelContextsInitialized = true;
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

                            foreach (var ghPath in pathBranch)
                            {
                                token.ThrowIfCancellationRequested();

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

                                var localResult = await this.parent.CallAiToolAsync("file2md", localParams).ConfigureAwait(false);

                                if (localResult == null)
                                {
                                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Tool 'file2md' returned no result for '{filePath}'.");
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
                                    foreach (var imgToken in imagesArray)
                                    {
                                        var imgObj = imgToken as JObject;
                                        if (imgObj == null) continue;
                                        var img = new SmartHopper.Core.Grasshopper.Converters.ExtractedImage(
                                            imgObj["id"]?.ToString() ?? "img",
                                            imgObj["base64Data"]?.ToString() ?? string.Empty,
                                            imgObj["mimeType"]?.ToString() ?? "image/png",
                                            imgObj["context"]?.ToString() ?? string.Empty,
                                            imgObj["pageOrSlide"]?.Value<int>() ?? 0);
                                        outputs["Images"].Add(new GH_ExtractedImage(img));
                                    }
                                }

                                var warningsArray = localResult["warnings"] as JArray;
                                if (warningsArray != null)
                                {
                                    foreach (var w in warningsArray)
                                    {
                                        this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, w.ToString());
                                    }
                                }

                                // Step 2: if no images, emit base markdown directly
                                if (imagesArray == null || imagesArray.Count == 0)
                                {
                                    outputs["Markdown"].Add(new GH_String(baseMarkdown));
                                    continue;
                                }

                                // Step 3: call img2text per image via CallAiToolAsync
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

                                    var imgResult = await this.parent.CallAiToolAsync("img2text", imgParams).ConfigureAwait(false);
                                    var resultStr = imgResult?["result"]?.ToString()
                                        ?? imgResult?["description"]?.ToString()
                                        ?? string.Empty;

                                    if (isBatch && resultStr.StartsWith("##SH_BATCH:", StringComparison.Ordinal))
                                    {
                                        // Sentinel placeholder: record context for OnBatchCompleted
                                        var sentinelId = resultStr.Substring("##SH_BATCH:".Length, resultStr.Length - "##SH_BATCH:".Length - "##".Length);
                                        fileSentinelIds.Add(sentinelId);
                                    }
                                    else
                                    {
                                        imgDescriptions.Add(resultStr);
                                    }
                                }

                                if (isBatch && fileSentinelIds.Count > 0)
                                {
                                    // Register context for each sentinel so OnBatchCompleted can reassemble markdown
                                    int imgIndex = 0;
                                    foreach (var imgToken2 in imagesArray)
                                    {
                                        var imgObj2 = imgToken2 as JObject;
                                        if (imgObj2 == null) continue;
                                        if (imgIndex >= fileSentinelIds.Count) break;

                                        var sentinelId = fileSentinelIds[imgIndex];
                                        this.parent._sentinelContexts[sentinelId] = new ImageSentinelContext
                                        {
                                            BaseMarkdown = baseMarkdown,
                                            ImageMode = this.imageMode,
                                            ImageContext = imgObj2["context"]?.ToString() ?? string.Empty,
                                            ImageId = imgObj2["id"]?.ToString() ?? "img",
                                            MimeType = imgObj2["mimeType"]?.ToString() ?? "image/png",
                                            Base64Data = imgObj2["base64Data"]?.ToString() ?? string.Empty,
                                            FileSentinelIds = fileSentinelIds,
                                            ImageIndex = imgIndex + 1, // 1-based index for placeholder replacement
                                        };
                                        imgIndex++;
                                    }

                                    // Only the first sentinel enters the output tree (one markdown per file).
                                    // OnBatchCompleted assembles the full markdown by reading all FileSentinelIds.
                                    outputs["Markdown"].Add(new GH_String($"##SH_BATCH:{fileSentinelIds[0]}##"));
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
                            }

                            return outputs;
                        },
                        this.processingOptions,
                        token).ConfigureAwait(false);

                    this.resultMarkdown = DataTreeProcessor.ExtractTypedTree<GH_String>(resultTrees, "Markdown");
                    this.resultFormat = DataTreeProcessor.ExtractTypedTree<GH_String>(resultTrees, "Format");
                    this.resultImages = DataTreeProcessor.ExtractTypedTree<GH_ExtractedImage>(resultTrees, "Images");

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
                var reconstructed = this.parent.PopReconstructedTree<GH_String>("Markdown");
                if (reconstructed != null)
                {
                    this.parent.SetPersistentOutput("Markdown", reconstructed, DA);
                    this.parent.SetPersistentOutput("Images", this.parent._localImages ?? new GH_Structure<GH_ExtractedImage>(), DA);
                    this.parent.SetPersistentOutput("Format", this.parent._localFormat ?? new GH_Structure<GH_String>(), DA);
                    errorMessage = null;
                    return;
                }

                this.parent.SetPersistentOutput("Markdown", this.resultMarkdown ?? new GH_Structure<GH_String>(), DA);
                this.parent.SetPersistentOutput("Images", this.resultImages ?? new GH_Structure<GH_ExtractedImage>(), DA);
                this.parent.SetPersistentOutput("Format", this.resultFormat ?? new GH_Structure<GH_String>(), DA);
                errorMessage = null;
            }
        }
    }
}
