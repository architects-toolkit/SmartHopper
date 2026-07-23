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
using System.IO;
using System.Linq;
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
using SmartHopper.ProviderSdk.AICall.Core.Base;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AICall.Metrics;
using SmartHopper.ProviderSdk.AICall.Utilities;
using SmartHopper.ProviderSdk.Diagnostics;

namespace SmartHopper.Components.Input
{
    /// <summary>
    /// Reads a file using the file2md AI tool and wraps its Markdown content into an AIInputPayload.
    /// Supports PDF, DOCX, XLSX, PPTX, HTML, CSV, JSON, XML, TXT, EML, EPUB, RTF, and more.
    /// AI is used only for image description when Image Mode is set to embed, describe, or caption.
    /// </summary>
    public class File2AIComponent : AIStatefulAsyncComponentBase
    {
        /// <summary>
        /// Per-file context stored during DoWorkAsync so <see cref="OnBatchCompleted"/> can
        /// reconstruct the final Markdown once all batch image descriptions are available.
        /// </summary>
        private Dictionary<string, MarkdownImageBatchContext> _fileContexts;

        /// <summary>
        /// Flag to prevent clearing <see cref="_fileContexts"/> during polling re-runs.
        /// </summary>
        private bool _fileContextsInitialized;

        /// <summary>
        /// Set to true when the batch is active but <see cref="_fileContexts"/> could not be restored.
        /// </summary>
        private bool _batchContextLost;

        /// <summary>
        /// Local Markdown tree produced during DoWorkAsync; used to build the AIInputPayload output.
        /// </summary>
        private GH_Structure<GH_String> _localMarkdown;

        /// <summary>
        /// Local Format tree produced during DoWorkAsync; does not need AI so it is always available.
        /// </summary>
        private GH_Structure<GH_String> _localFormat;

        public override Guid ComponentGuid => new Guid("AC2DDCD0-B4E9-4B99-80DA-CA9F9BBCD4C9");

        protected override Bitmap Icon => Resources.toaifile;

        public override GH_Exposure Exposure => GH_Exposure.septenary;

        /// <inheritdoc/>
        protected override IReadOnlyList<string> UsingAiTools => new[] { "file2md", "img2text" };

        /// <inheritdoc/>
        protected override ProcessingOptions ComponentProcessingOptions => new ProcessingOptions
        {
            Topology = ProcessingTopology.BranchToBranch,
            OnlyMatchingPaths = false,
            GroupIdenticalBranches = true,
        };

        public File2AIComponent()
            : base(
                "File to AI",
                "File2AI",
                "Reads a file and converts it to Markdown using the file2md AI tool, wrapping the content into an AIInputPayload. AI is used only for image description when Image Mode is not 'skip'.",
                "SmartHopper",
                "B. Input")
        {
        }

        protected override void RegisterAdditionalInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("File Path", "F", "Path(s) to the file(s) to convert and wrap into an AIInputPayload.", GH_ParamAccess.tree);
            pManager.AddBooleanParameter("Remove Headers", "RH", "Attempt to remove headers and footers from PDF/DOCX. Default: true.", GH_ParamAccess.tree, true);
            pManager.AddTextParameter("Image Mode", "IM", "How extracted images appear in the Markdown. 'skip' (default): do not extract images. 'embed': embed as base64 data URI with a short AI caption. 'describe': replace with a long AI text description. 'caption': replace with a short AI-generated title. Requires an AI provider for embed/describe/caption.", GH_ParamAccess.tree, "skip");
        }

        protected override void RegisterAdditionalOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddParameter(new AIInputPayloadParameter(), "Input >", ">", "AIInputPayload(s) containing the file content as Markdown.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Markdown", "M", "Converted file content as Markdown.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Format", "F", "Detected source format of the file.", GH_ParamAccess.tree);
        }

        protected override void OnEnteringNeedsRun()
        {
            base.OnEnteringNeedsRun();
            this._fileContexts = null;
            this._fileContextsInitialized = false;
            this._batchContextLost = false;
            this._localMarkdown = null;
            this._localFormat = null;
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

            var slotMetricsByPath = new Dictionary<GH_Path, List<AIMetrics>>();

            var reconstructedMarkdown = this.ProcessBatchResults<GH_String>(
                "Markdown",
                sentinel,
                results,
                (representativeSentinelId, _) =>
                {
                    if (this._fileContexts == null ||
                        !this._fileContexts.TryGetValue(representativeSentinelId, out var fileCtx))
                    {
                        Debug.WriteLine($"[File2AI] OnBatchCompleted: File context missing for sentinel {representativeSentinelId}");
                        return new GH_String("[File context missing]");
                    }

                    if (!sentinelToPath.TryGetValue(representativeSentinelId, out var representativePath))
                    {
                        representativePath = new GH_Path(0);
                    }

                    var markdown = MarkdownImageBatchProcessor.Reconstruct(
                        fileCtx,
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
                        this.CombineIntoPersistedMetrics(m);
                    }
                }

                var totalSlotMetrics = slotMetricsByPath.Values.Sum(l => l.Count);
                Debug.WriteLine($"[File2AI] OnBatchCompleted: merged {totalSlotMetrics} slot metrics into {slotMetricsByPath.Count} branch path(s)");
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

            if (this._localFormat != null)
            {
                this.SetPersistentOutput("Format", this._localFormat, null);
            }
        }

        protected override AsyncWorkerBase CreateWorker(Action<string> progressReporter)
        {
            return new File2AIWorker(this, this.AddRuntimeMessage, this.ComponentProcessingOptions);
        }

        private sealed class File2AIWorker : AsyncWorkerBase
        {
            private readonly File2AIComponent parent;
            private readonly ProcessingOptions processingOptions;
            private Dictionary<string, GH_Structure<GH_String>> inputTrees;
            private Dictionary<string, GH_Structure<IGH_Goo>> result;

            public File2AIWorker(File2AIComponent parent, Action<GH_RuntimeMessageLevel, string> addRuntimeMessage, ProcessingOptions options)
                : base(parent, addRuntimeMessage)
            {
                this.parent = parent;
                this.processingOptions = options;
                this.result = new Dictionary<string, GH_Structure<IGH_Goo>>
                {
                    { "Input >", new GH_Structure<IGH_Goo>() },
                    { "Markdown", new GH_Structure<IGH_Goo>() },
                    { "Format", new GH_Structure<IGH_Goo>() },
                };
            }

            public override void GatherInput(IGH_DataAccess DA, out int dataCount)
            {
                if (!this.parent.HasActiveBatchSubmission || this.parent._batchContextLost)
                {
                    this.parent._fileContextsInitialized = false;
                }

                this.inputTrees = new Dictionary<string, GH_Structure<GH_String>>();

                var pathTree = new GH_Structure<GH_String>();
                DA.GetDataTree("File Path", out pathTree);

                var removeTree = new GH_Structure<GH_Boolean>();
                DA.GetDataTree("Remove Headers", out removeTree);

                GH_Structure<GH_String> imageModeTree;
                DA.GetDataTree("Image Mode", out imageModeTree);

                this.inputTrees["FilePath"] = pathTree;
                this.inputTrees["RemoveHeaders"] = File2MdToolResult.ConvertBoolTreeToString(removeTree, "true");
                this.inputTrees["ImageMode"] = imageModeTree ?? new GH_Structure<GH_String>();

                dataCount = 0;
            }

            public override async Task DoWorkAsync(CancellationToken token)
            {
                this.result = new Dictionary<string, GH_Structure<IGH_Goo>>
                {
                    { "Input >", new GH_Structure<IGH_Goo>() },
                    { "Markdown", new GH_Structure<IGH_Goo>() },
                    { "Format", new GH_Structure<IGH_Goo>() },
                };

                if (!this.parent._fileContextsInitialized)
                {
                    this.parent._fileContexts = new Dictionary<string, MarkdownImageBatchContext>();
                    this.parent._fileContextsInitialized = true;
                }

                this.parent._localMarkdown = null;
                this.parent._localFormat = null;

                try
                {
                    this.result = await this.parent.RunProcessingAsync(
                        this.inputTrees,
                        async branches => await this.ProcessBranches(branches, token).ConfigureAwait(false),
                        this.processingOptions,
                        token).ConfigureAwait(false);

                    var markdownTree = DataTreeProcessor.ExtractTypedTree<GH_String>(this.result, "Markdown");
                    var formatTree = DataTreeProcessor.ExtractTypedTree<GH_String>(this.result, "Format");

                    this.parent._localMarkdown = markdownTree;
                    this.parent._localFormat = formatTree;

                    var markdownDict = new Dictionary<string, GH_Structure<GH_String>>
                    {
                        { "Markdown", markdownTree },
                    };
                    var batchSubmitted = await this.parent.TrySubmitBatchAsync("Markdown", markdownDict, token).ConfigureAwait(false);
                    if (batchSubmitted)
                    {
                        Debug.WriteLine("[File2AI] Sentinel tree stored, batch submitted");
                    }
                    else
                    {
                        this.parent.FinishResults(
                            "Markdown",
                            markdownTree ?? new GH_Structure<GH_String>(),
                            ("Input >", (object)(this.result["Input >"] ?? new GH_Structure<IGH_Goo>())),
                            ("Format", (object)(formatTree ?? new GH_Structure<GH_String>())));
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[File2AIWorker] Error: {ex.Message}");
                    this.CollectMessage(SHRuntimeMessageSeverity.Error, $"Error processing files: {ex.Message}");
                }
            }

            private async Task<Dictionary<string, List<IGH_Goo>>> ProcessBranches(Dictionary<string, List<GH_String>> branches, CancellationToken token)
            {
                var outputs = new Dictionary<string, List<IGH_Goo>>
                {
                    { "Input >", new List<IGH_Goo>() },
                    { "Markdown", new List<IGH_Goo>() },
                    { "Format", new List<IGH_Goo>() },
                };

                var filePaths = branches["FilePath"];
                var removeList = branches["RemoveHeaders"];
                var imageModeList = branches["ImageMode"];

                var normalizedLists = DataTreeProcessor.NormalizeBranchLengths(new List<List<GH_String>> { filePaths, removeList, imageModeList });
                filePaths = normalizedLists[0];
                removeList = normalizedLists[1];
                imageModeList = normalizedLists[2];

                for (int i = 0; i < filePaths.Count; i++)
                {
                    string filePath = filePaths[i]?.Value;
                    bool removeHeaders = bool.TryParse(removeList[i]?.Value, out var rh) ? rh : true;
                    string imageMode = imageModeList?[i]?.Value?.ToLowerInvariant() ?? "skip";
                    if (imageMode != "skip" && imageMode != "embed" && imageMode != "describe" && imageMode != "caption")
                    {
                        imageMode = "skip";
                    }

                    if (string.IsNullOrWhiteSpace(filePath))
                    {
                        outputs["Input >"].Add(new GH_AIInputPayload(null));
                        outputs["Markdown"].Add(new GH_String(string.Empty));
                        outputs["Format"].Add(new GH_String("unknown"));
                        continue;
                    }

                    if (!File.Exists(filePath))
                    {
                        this.CollectMessage(SHRuntimeMessageSeverity.Error, $"File not found: {filePath}");
                        outputs["Input >"].Add(new GH_AIInputPayload(null));
                        outputs["Markdown"].Add(new GH_String(string.Empty));
                        outputs["Format"].Add(new GH_String("not_found"));
                        continue;
                    }

                    try
                    {
                        bool extractImages = imageMode != "skip";
                        var converted = await File2MdToolResult.CallAsync(
                            filePath,
                            removeHeaders,
                            extractImages,
                            preserveFormatting: true,
                            preserveComments: true,
                            preserveFootnotes: true,
                            preserveEndnotes: true,
                            describeImages: false,
                            imageMode: imageMode).ConfigureAwait(false);

                        if (converted == null)
                        {
                            this.CollectMessage(SHRuntimeMessageSeverity.Warning, $"Tool 'file2md' returned no result for: {filePath}", SHRuntimeMessageOrigin.Tool);
                            outputs["Input >"].Add(new GH_AIInputPayload(null));
                            outputs["Markdown"].Add(new GH_String(string.Empty));
                            outputs["Format"].Add(new GH_String("error"));
                            continue;
                        }

                        string markdown = converted.Markdown;
                        bool isBatch = this.parent.IsBatchRequest();

                        var imageSlots = new List<MarkdownImageSlot>();
                        if (extractImages)
                        {
                            int idx = 1;
                            foreach (var img in converted.Images)
                            {
                                imageSlots.Add(new MarkdownImageSlot
                                {
                                    Index = idx,
                                    ImageId = img.Id ?? "img",
                                    ImageMode = imageMode,
                                    ImageContext = img.Context ?? string.Empty,
                                    MimeType = img.MimeType ?? "image/png",
                                    Base64Data = img.RawValue ?? string.Empty,
                                    Placeholder = $"[image {idx}]",
                                });
                                idx++;
                            }
                        }

                        var processingResult = await MarkdownImageBatchProcessor.ProcessAsync(
                            markdown,
                            imageSlots,
                            imageMode,
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

                        var payload = string.IsNullOrWhiteSpace(processedMarkdown)
                            ? null
                            : AIInputPayload.FromText(processedMarkdown);

                        foreach (var w in converted.Warnings)
                        {
                            this.CollectMessage(SHRuntimeMessageSeverity.Warning, w, SHRuntimeMessageOrigin.Tool);
                        }

                        outputs["Input >"].Add(new GH_AIInputPayload(payload));
                        outputs["Markdown"].Add(new GH_String(processedMarkdown));
                        outputs["Format"].Add(new GH_String(string.IsNullOrEmpty(converted.Format) ? "unknown" : converted.Format));
                    }
                    catch (Exception ex)
                    {
                        this.CollectMessage(SHRuntimeMessageSeverity.Warning, $"Error converting {filePath}: {ex.Message}");
                        outputs["Input >"].Add(new GH_AIInputPayload(null));
                        outputs["Markdown"].Add(new GH_String(string.Empty));
                        outputs["Format"].Add(new GH_String("error"));
                    }
                }

                return outputs;
            }

            public override void SetOutput(IGH_DataAccess DA, out string message)
            {
                message = null;
            }
        }
    }
}
