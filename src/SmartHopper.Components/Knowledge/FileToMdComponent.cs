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
using SmartHopper.Core.Grasshopper.Types;
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AICall.Core.Returns;
using SmartHopper.Infrastructure.AICall.Tools;

namespace SmartHopper.Components.Knowledge
{
    /// <summary>
    /// Grasshopper component that converts local files to Markdown and extracts embedded images.
    /// Non-AI: no provider or model required. Use <c>AIFile2MdComponent</c> for AI-powered image description.
    /// </summary>
    public class FileToMdComponent : StatefulComponentBase
    {
        /// <inheritdoc/>
        public override Guid ComponentGuid => new Guid("C0EF8C72-1233-4613-902C-2E07321BB2E3");

        /// <inheritdoc/>
        protected override Bitmap Icon => Resources.filetomd;

        /// <inheritdoc/>
        public override GH_Exposure Exposure => GH_Exposure.tertiary;

        /// <summary>
        /// Initializes a new instance of the <see cref="FileToMdComponent"/> class.
        /// </summary>
        public FileToMdComponent()
            : base(
                  "File To Markdown",
                  "FileToMd",
                  "Convert a local file (PDF, DOCX, XLSX, PPTX, HTML, CSV, JSON, XML, TXT, EML, EPUB, RTF, etc.) to Markdown text and extract embedded images. No AI required. Use AIFileToMd for AI-powered image description.",
                  "SmartHopper",
                  "Knowledge")
        {
            this.RunOnlyOnInputChanges = false;
        }

        /// <inheritdoc/>
        protected override void RegisterAdditionalInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("File Path", "F", "Absolute path(s) to the file(s) to convert.", GH_ParamAccess.tree);
        }

        /// <inheritdoc/>
        protected override void RegisterAdditionalOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Markdown", "Md", "Markdown content of the file.", GH_ParamAccess.tree);
            pManager.AddGenericParameter("Images", "Img", "Images extracted from the document (PDF/DOCX/PPTX). Each item is a GH_ExtractedImage carrying base64 data, MIME type, and source context. Branched per input file. Connect to Image Viewer or AIImg2Text.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Format", "Fmt", "Detected original format (e.g., pdf, docx, html).", GH_ParamAccess.tree);
        }

        /// <inheritdoc/>
        protected override ProcessingOptions ComponentProcessingOptions => new ProcessingOptions
        {
            Topology = ProcessingTopology.ItemGraft,
            OnlyMatchingPaths = false,
            GroupIdenticalBranches = false,
        };

        /// <inheritdoc/>
        protected override AsyncWorkerBase CreateWorker(Action<string> progressReporter)
        {
            return new FileToMdWorker(this, this.AddRuntimeMessage, this.ComponentProcessingOptions);
        }

        private sealed class FileToMdWorker : AsyncWorkerBase
        {
            private readonly FileToMdComponent parent;
            private readonly ProcessingOptions processingOptions;
            private GH_Structure<GH_String> filePathTree;
            private bool hasWork;

            private GH_Structure<GH_String> resultMarkdown;
            private GH_Structure<GH_String> resultFormat;
            private GH_Structure<GH_ExtractedImage> resultImages;

            public FileToMdWorker(
                FileToMdComponent parent,
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

                                var (markdown, format, images, warnings) = await ConvertFileAsync(ghPath.Value).ConfigureAwait(false);

                                outputs["Markdown"].Add(new GH_String(markdown));
                                outputs["Format"].Add(new GH_String(format));

                                foreach (var img in images)
                                {
                                    outputs["Images"].Add(new GH_ExtractedImage(img));
                                }

                                foreach (var w in warnings)
                                {
                                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, w);
                                }
                            }

                            return outputs;
                        },
                        this.processingOptions,
                        token).ConfigureAwait(false);

                    this.resultMarkdown = DataTreeProcessor.ExtractTypedTree<GH_String>(resultTrees, "Markdown");
                    this.resultFormat = DataTreeProcessor.ExtractTypedTree<GH_String>(resultTrees, "Format");
                    this.resultImages = DataTreeProcessor.ExtractTypedTree<GH_ExtractedImage>(resultTrees, "Images");
                }
                catch (OperationCanceledException)
                {
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Operation was cancelled.");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[FileToMd] Error: {ex.Message}");
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
                }
            }

            /// <inheritdoc/>
            public override void SetOutput(IGH_DataAccess DA, out string errorMessage)
            {
                this.parent.SetPersistentOutput("Markdown", this.resultMarkdown ?? new GH_Structure<GH_String>(), DA);
                this.parent.SetPersistentOutput("Images", this.resultImages ?? new GH_Structure<GH_ExtractedImage>(), DA);
                this.parent.SetPersistentOutput("Format", this.resultFormat ?? new GH_Structure<GH_String>(), DA);
                errorMessage = null;
            }

            private static async Task<(string markdown, string format, List<SmartHopper.Core.Grasshopper.Converters.ExtractedImage> images, List<string> warnings)> ConvertFileAsync(string filePath)
            {
                var parameters = new JObject
                {
                    ["filePath"] = filePath,
                    ["preserveTableStructure"] = true,
                    ["removeHeadersFooters"] = true,
                    ["extractImages"] = true,
                };

                var toolCallInteraction = new AIInteractionToolCall
                {
                    Name = "file2md",
                    Arguments = parameters,
                    Agent = AIAgent.Assistant,
                };

                var toolCall = new AIToolCall
                {
                    Endpoint = "file2md",
                };

                toolCall.FromToolCallInteraction(toolCallInteraction);
                toolCall.SkipMetricsValidation = true;

                AIReturn aiResult = await toolCall.Exec().ConfigureAwait(false);
                var toolResultInteraction = aiResult.Body?.GetLastInteraction(AIAgent.ToolResult) as AIInteractionToolResult;
                var toolResult = toolResultInteraction?.Result;

                if (toolResult == null)
                {
                    return (string.Empty, string.Empty, new List<SmartHopper.Core.Grasshopper.Converters.ExtractedImage>(), new List<string> { $"Tool 'file2md' returned no result for '{filePath}'." });
                }

                string content = toolResult["content"]?.ToString() ?? string.Empty;
                string format = toolResult["originalFormat"]?.ToString() ?? string.Empty;

                var images = new List<SmartHopper.Core.Grasshopper.Converters.ExtractedImage>();
                var imagesArray = toolResult["images"] as JArray;
                if (imagesArray != null)
                {
                    foreach (var imgToken in imagesArray)
                    {
                        var imgObj = imgToken as JObject;
                        if (imgObj == null) continue;
                        images.Add(new SmartHopper.Core.Grasshopper.Converters.ExtractedImage(
                            imgObj["id"]?.ToString() ?? "img",
                            imgObj["base64Data"]?.ToString() ?? string.Empty,
                            imgObj["mimeType"]?.ToString() ?? "image/png",
                            imgObj["context"]?.ToString() ?? string.Empty,
                            imgObj["pageOrSlide"]?.Value<int>() ?? 0));
                    }
                }

                var warnings = new List<string>();
                var warningsArray = toolResult["warnings"] as JArray;
                if (warningsArray != null)
                {
                    foreach (var w in warningsArray)
                    {
                        warnings.Add(w.ToString());
                    }
                }

                return (content, format, images, warnings);
            }
        }
    }
}
