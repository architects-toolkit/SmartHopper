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
using SmartHopper.Core.Grasshopper.Types;
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AIModels;
using SmartHopper.Infrastructure.AIProviders;

namespace SmartHopper.Components.Knowledge
{
    /// <summary>
    /// Grasshopper component that converts local files to Markdown with optional AI-powered image description.
    /// Requires a vision-capable AI provider when image description is enabled.
    /// Use <c>File2MdComponent</c> for plain conversion without AI.
    /// </summary>
    public class AIFile2MdComponent : AIStatefulAsyncComponentBase
    {
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
        protected override IReadOnlyList<string> UsingAiTools => new[] { "file2md" };

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

            var reconstructed = this.ProcessBatchResults<GH_String>(
                "Markdown",
                sentinel,
                results,
                (customId, resultBody) =>
                {
                    var provider = ProviderManager.Instance.GetProvider(this.GetActualAIProviderName());
                    if (provider == null) return new GH_String(string.Empty);

                    var interactions = provider.Decode(resultBody);
                    var lastText = interactions
                        ?.OfType<AIInteractionText>()
                        .LastOrDefault(i => i.Agent == AIAgent.Assistant);

                    return new GH_String(lastText?.Content ?? string.Empty);
                });

            this.StoreReconstructedTree("Markdown", reconstructed);
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

                if (!this.hasWork)
                {
                    return;
                }

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

                                var parameters = new JObject
                                {
                                    ["filePath"] = filePath,
                                    ["preserveTableStructure"] = true,
                                    ["removeHeadersFooters"] = true,
                                    ["describeImages"] = true,
                                    ["imageMode"] = this.imageMode ?? "embed",
                                };

                                if (!string.IsNullOrWhiteSpace(this.imagePrompt))
                                {
                                    parameters["imageDescriptionPrompt"] = this.imagePrompt;
                                }

                                var toolResult = await this.parent.CallAiToolAsync("file2md", parameters).ConfigureAwait(false);

                                if (toolResult == null)
                                {
                                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Tool 'file2md' returned no result for '{filePath}'.");
                                    outputs["Markdown"].Add(new GH_String(string.Empty));
                                    outputs["Format"].Add(new GH_String(string.Empty));
                                    continue;
                                }

                                outputs["Markdown"].Add(new GH_String(toolResult["content"]?.ToString() ?? string.Empty));
                                outputs["Format"].Add(new GH_String(toolResult["originalFormat"]?.ToString() ?? string.Empty));

                                var imagesArray = toolResult["images"] as JArray;
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

                                var warningsArray = toolResult["warnings"] as JArray;
                                if (warningsArray != null)
                                {
                                    foreach (var w in warningsArray)
                                    {
                                        this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, w.ToString());
                                    }
                                }
                            }

                            return outputs;
                        },
                        this.processingOptions,
                        token).ConfigureAwait(false);

                    this.resultMarkdown = DataTreeProcessor.ExtractTypedTree<GH_String>(resultTrees, "Markdown");
                    this.resultFormat = DataTreeProcessor.ExtractTypedTree<GH_String>(resultTrees, "Format");
                    this.resultImages = DataTreeProcessor.ExtractTypedTree<GH_ExtractedImage>(resultTrees, "Images");

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
                    this.parent.SetPersistentOutput("Images", this.resultImages ?? new GH_Structure<GH_ExtractedImage>(), DA);
                    this.parent.SetPersistentOutput("Format", this.resultFormat ?? new GH_Structure<GH_String>(), DA);
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
