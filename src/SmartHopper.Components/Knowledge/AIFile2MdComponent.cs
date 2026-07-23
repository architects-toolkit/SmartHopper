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
using SmartHopper.Core.Grasshopper.AITools;
using SmartHopper.Core.Grasshopper.Utils.Internal;
using SmartHopper.Core.Types;
using SmartHopper.ProviderSdk.AICall.Core.Base;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AICall.Metrics;
using SmartHopper.ProviderSdk.AICall.Utilities;
using SmartHopper.ProviderSdk.AIProviders;
using SmartHopper.ProviderSdk.Diagnostics;

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
        /// to a <see cref="MarkdownImageBatchContext"/> containing the base markdown and all image slots.
        /// </summary>
        private Dictionary<string, MarkdownImageBatchContext> _fileContexts;

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
            // Set RunOnlyOnInputChanges to false to ensure the component always runs when the Run parameter is true
            this.RunOnlyOnInputChanges = false;
        }

        /// <inheritdoc/>
        protected override void RegisterAdditionalInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("File Path", "F", "Absolute path(s) to the file(s) to convert.", GH_ParamAccess.tree);
            pManager.AddBooleanParameter("Remove Headers", "RH", "Attempt to remove headers and footers from PDF/DOCX. Default: true.", GH_ParamAccess.tree, true);
            pManager.AddTextParameter("Image Mode", "IM", "How AI describes images in the output:\n'embed' (default) — embed image as base64 data URI with a short AI-generated caption as alt text.\n'describe' — replace image with a long, detailed AI text description.\n'caption' — replace image with a short AI-generated title/caption.", GH_ParamAccess.item, "embed");
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
                    this._fileContexts = new Dictionary<string, MarkdownImageBatchContext>();

                    for (int idx = 0; idx < count; idx++)
                    {
                        string key = reader.GetString($"FileContext_{idx}_Key");
                        string baseMarkdown = reader.GetString($"FileContext_{idx}_BaseMarkdown");
                        int imageCount = reader.GetInt32($"FileContext_{idx}_ImageCount");

                        var images = new List<MarkdownImageSlot>(imageCount);
                        for (int i = 0; i < imageCount; i++)
                        {
                            images.Add(new MarkdownImageSlot
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

                        this._fileContexts[key] = new MarkdownImageBatchContext
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

            // Map every sentinel custom ID in the Markdown tree to its output branch path.
            // All image slots for a single file share the representative sentinel's path.
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

            // Per-slot metrics grouped by the file's output branch path. ProcessBatchResults
            // only processes the representative sentinel per file, so the non-representative
            // image slots must be merged manually at the same grafted path.
            var slotMetricsByPath = new Dictionary<GH_Path, List<AIMetrics>>();

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

                    if (!sentinelToPath.TryGetValue(representativeSentinelId, out var representativePath))
                    {
                        representativePath = new GH_Path(0);
                    }

                    var finalMarkdown = MarkdownImageBatchProcessor.Reconstruct(
                        fileCtx,
                        results,
                        this.GetActualAIProviderName(),
                        metrics: null,
                        onImageResolved: (slotSentinelId, slotBody, assistantText, description) =>
                        {
                            // Collect metrics from non-representative slots and group them by file path.
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

            // Merge per-slot metrics at the same grafted path as their parent file.
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
                Debug.WriteLine($"[AIFile2Md] OnBatchCompleted: merged {totalSlotMetrics} slot metrics into {slotMetricsByPath.Count} branch path(s)");

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

            private GH_Structure<GH_String> resultMarkdown;
            private GH_Structure<GH_String> resultFormat;
            private GH_Structure<GH_VersatileImage> resultImages;

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
                    this.parent._fileContexts = new Dictionary<string, MarkdownImageBatchContext>();
                    this.parent._fileContextsInitialized = true;
                }

                this.parent._localFormat = null;
                this.parent._localImages = null;

                if (!this.hasWork)
                {
                    return;
                }

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
                                    ["removeHeadersFooters"] = removeHeaders,
                                    ["preserveFormatting"] = true,
                                    ["preserveComments"] = true,
                                    ["preserveFootnotes"] = true,
                                    ["preserveEndnotes"] = true,
                                    ["describeImages"] = false,
                                    ["extractImages"] = true,
                                };

                                var localResult = await this.parent.CallAIToolAsync("file2md", localParams, token).ConfigureAwait(false);

                                if (localResult?.Result == null)
                                {
                                    this.CollectMessage(SHRuntimeMessageSeverity.Error, $"Tool 'file2md' returned no result for '{filePath}'.", SHRuntimeMessageOrigin.Tool);
                                    outputs["Markdown"].Add(new GH_String(string.Empty));
                                    outputs["Format"].Add(new GH_String(string.Empty));
                                    continue;
                                }

                                var converted = File2MdToolResult.Parse(localResult.Result, filePath);
                                string baseMarkdown = converted.Markdown;
                                outputs["Format"].Add(new GH_String(converted.Format));

                                // Collect raw images for the Images output
                                foreach (var img in converted.Images)
                                {
                                    outputs["Images"].Add(new GH_VersatileImage(img));
                                }

                                foreach (var w in converted.Warnings)
                                {
                                    this.CollectMessage(SHRuntimeMessageSeverity.Warning, w, SHRuntimeMessageOrigin.Tool);
                                }

                                var imagesArray = localResult["images"] as JArray;

                                // Step 2: build image slots for the shared processor
                                var imageSlots = new List<MarkdownImageSlot>();
                                if (imagesArray != null)
                                {
                                    int idx = 1;
                                    foreach (var imgToken in imagesArray)
                                    {
                                        var imgObj = imgToken as JObject;
                                        if (imgObj == null) continue;

                                        imageSlots.Add(new MarkdownImageSlot
                                        {
                                            Index = idx,
                                            ImageId = imgObj["id"]?.ToString() ?? "img",
                                            ImageMode = this.imageMode,
                                            ImageContext = imgObj["context"]?.ToString() ?? string.Empty,
                                            MimeType = imgObj["mimeType"]?.ToString() ?? "image/png",
                                            Base64Data = imgObj["base64Data"]?.ToString() ?? string.Empty,
                                            Placeholder = $"[image {idx}]",
                                        });
                                        idx++;
                                    }
                                }

                                bool isBatch = this.parent.IsBatchRequest();

                                // Step 3: process images with the shared batch-aware helper
                                var processingResult = await MarkdownImageBatchProcessor.ProcessAsync(
                                    baseMarkdown,
                                    imageSlots,
                                    this.imageMode,
                                    async parameters =>
                                    {
                                        var result = await this.parent.CallAIToolAsync("img2text", parameters, token).ConfigureAwait(false);
                                        return result?["result"]?.ToString() ?? result?["description"]?.ToString() ?? string.Empty;
                                    },
                                    isBatch).ConfigureAwait(false);
                                string processedMarkdown = processingResult.Markdown;
                                MarkdownImageBatchContext batchContext = processingResult.BatchContext;

                                if (batchContext != null)
                                {
                                    this.parent._fileContexts[batchContext.Images[0].SentinelId] = batchContext;
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
