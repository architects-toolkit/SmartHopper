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
using SmartHopper.Components.Properties;
using SmartHopper.Core.ComponentBase;
using SmartHopper.Core.DataTree;
using SmartHopper.Core.Models;
using SmartHopper.Core.Parameters;
using SmartHopper.Core.Types;
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AICall.Core.Returns;
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.Infrastructure.Diagnostics;

namespace SmartHopper.Components.Knowledge
{
    /// <summary>
    /// Grasshopper component that converts local files to Markdown and extracts embedded images.
    /// Non-AI: no provider or model required. Use <c>AIFile2MdComponent</c> for AI-powered image description.
    /// </summary>
    public class File2MdComponent : StatefulComponentBase
    {
        /// <inheritdoc/>
        public override Guid ComponentGuid => new Guid("C0EF8C72-1233-4613-902C-2E07321BB2E3");

        /// <inheritdoc/>
        protected override Bitmap Icon => Resources.filetomd;

        /// <inheritdoc/>
        public override GH_Exposure Exposure => GH_Exposure.primary;

        /// <summary>
        /// Initializes a new instance of the <see cref="File2MdComponent"/> class.
        /// </summary>
        public File2MdComponent()
            : base(
                  "File To Markdown",
                  "File2Md",
                  "Convert a local file (PDF, DOCX, XLSX, PPTX, HTML, CSV, JSON, XML, TXT, EML, EPUB, RTF, etc.) to Markdown text and extract embedded images. No AI required. Use AIFile2Md for AI-powered image description.",
                  "SmartHopper",
                  "Knowledge")
        {
            this.RunOnlyOnInputChanges = false;
        }

        /// <inheritdoc/>
        protected override void RegisterAdditionalInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("File Path", "F", "Absolute path(s) to the file(s) to convert.", GH_ParamAccess.tree);
            pManager.AddTextParameter("HTML Readability", "R", "HTML main-content extraction strategy for HTML/EPUB/EML content: auto (default), smartreader, heuristic, or off.", GH_ParamAccess.item, "auto");
            pManager[pManager.ParamCount - 1].Optional = true;
        }

        /// <inheritdoc/>
        protected override void RegisterAdditionalOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Markdown", "Md", "Markdown content of the file.", GH_ParamAccess.tree);
            pManager.AddParameter(new VersatileImageParameter(), "Images", "Img", "Images extracted from the document (PDF/DOCX/PPTX). Each item is a VersatileImage carrying base64 data, MIME type, and source context. Connect to Image Viewer or AIImg2Text.", GH_ParamAccess.tree);
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
            return new File2MdWorker(this, this.AddRuntimeMessage, this.ComponentProcessingOptions);
        }

        private sealed class File2MdWorker : AsyncWorkerBase
        {
            private readonly File2MdComponent parent;
            private readonly ProcessingOptions processingOptions;
            private GH_Structure<GH_String> filePathTree;
            private GH_Structure<GH_String> removeHeadersTree;
            private bool hasWork;
            private string htmlReadabilityMode;

            private GH_Structure<GH_String> resultMarkdown;
            private GH_Structure<GH_String> resultFormat;
            private GH_Structure<GH_VersatileImage> resultImages;

            public File2MdWorker(
                File2MdComponent parent,
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

                                var (markdown, format, images, warnings) = await ConvertFileAsync(ghPath.Value, this.htmlReadabilityMode).ConfigureAwait(false);

                                if (converted == null)
                                {
                                    outputs["Images"].Add(new GH_VersatileImage(img));
                                }

                                outputs["Markdown"].Add(new GH_String(converted.Markdown));
                                outputs["Format"].Add(new GH_String(converted.Format));

                                foreach (var img in converted.Images)
                                {
                                    this.CollectMessage(SHRuntimeMessageSeverity.Warning, w, SHRuntimeMessageOrigin.Tool);
                                }
                            }

                            return outputs;
                        },
                        this.processingOptions,
                        token).ConfigureAwait(false);

                    this.resultMarkdown = DataTreeProcessor.ExtractTypedTree<GH_String>(resultTrees, "Markdown");
                    this.resultFormat = DataTreeProcessor.ExtractTypedTree<GH_String>(resultTrees, "Format");
                    this.resultImages = DataTreeProcessor.ExtractTypedTree<GH_VersatileImage>(resultTrees, "Images");
                }
                catch (OperationCanceledException)
                {
                    this.CollectMessage(SHRuntimeMessageSeverity.Warning, "Operation was cancelled.");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[File2Md] Error: {ex.Message}");
                    this.CollectMessage(SHRuntimeMessageSeverity.Error, ex.Message);
                }
            }

            /// <inheritdoc/>
            public override void SetOutput(IGH_DataAccess DA, out string errorMessage)
            {
                this.parent.SetPersistentOutput("Markdown", this.resultMarkdown ?? new GH_Structure<GH_String>(), DA);
                this.parent.SetPersistentOutput("Images", this.resultImages ?? new GH_Structure<GH_VersatileImage>(), DA);
                this.parent.SetPersistentOutput("Format", this.resultFormat ?? new GH_Structure<GH_String>(), DA);
                errorMessage = null;
            }

            private static async Task<(string markdown, string format, List<VersatileImage> images, List<string> warnings)> ConvertFileAsync(string filePath, string htmlReadabilityMode)
            {
                var parameters = new JObject
                {
                    ["filePath"] = filePath,
                    ["preserveTableStructure"] = true,
                    ["removeHeadersFooters"] = true,
                    ["extractImages"] = true,
                };

                if (!string.IsNullOrWhiteSpace(htmlReadabilityMode) &&
                    !string.Equals(htmlReadabilityMode, "auto", StringComparison.OrdinalIgnoreCase))
                {
                    parameters["HTMLreadabilityMode"] = htmlReadabilityMode;
                }

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

                Debug.WriteLine($"[File2Md] Calling file2md tool for: {filePath}");
                AIReturn aiResult = await toolCall.Exec().ConfigureAwait(false);

                Debug.WriteLine($"[File2Md] AIReturn status: {aiResult?.Status}");
                Debug.WriteLine($"[File2Md] AIReturn has Body: {aiResult?.Body != null}");
                Debug.WriteLine($"[File2Md] AIReturn Metrics: {(aiResult?.Metrics != null ? $"InputTokens={aiResult.Metrics.InputTokens}, OutputTokens={aiResult.Metrics.OutputTokens}" : "NULL")}");

                if (aiResult?.Body != null)
                {
                    Debug.WriteLine($"[File2Md] Body interaction count: {aiResult.Body.Interactions.Count}");
                    foreach (var interaction in aiResult.Body.Interactions)
                    {
                        Debug.WriteLine($"[File2Md]   - Interaction: Agent={interaction.Agent}, Type={interaction.GetType().Name}");
                    }
                }

                var toolResult = ToolCallResult.FromAIReturn(aiResult);

                Debug.WriteLine($"[File2Md] ToolCallResult.Success={toolResult.Success}, Result is {(toolResult.Result == null ? "null" : "non-null")}");

                if (toolResult.Result == null)
                {
                    Debug.WriteLine($"[File2Md] ERROR: No tool result found. Checking for assistant text response...");
                    var assistantText = aiResult?.Body?.GetLastInteraction(AIAgent.Assistant) as AIInteractionText;
                    if (assistantText != null)
                    {
                        Debug.WriteLine($"[File2Md] Found assistant text instead: {assistantText.Content?.Substring(0, Math.Min(100, assistantText.Content?.Length ?? 0))}...");
                    }

                    return (string.Empty, string.Empty, new List<VersatileImage>(), new List<string> { $"Tool 'file2md' returned no result for '{filePath}'." });
                }

                string content = toolResult["content"]?.ToString() ?? string.Empty;
                string format = toolResult["originalFormat"]?.ToString() ?? string.Empty;

                Debug.WriteLine($"[File2Md] Extracted content length: {content.Length}, format: {format}");

                var images = new List<VersatileImage>();
                var imagesArray = toolResult["images"] as JArray;
                if (imagesArray != null)
                {
                    Debug.WriteLine($"[File2Md] Found {imagesArray.Count} images");
                    foreach (var imgToken in imagesArray)
                    {
                        var imgObj = imgToken as JObject;
                        if (imgObj == null) continue;
                        var imageSource = VersatileImage.FromExtractedDocument(
                            imgObj["base64Data"]?.ToString() ?? string.Empty,
                            imgObj["mimeType"]?.ToString() ?? "image/png",
                            imgObj["id"]?.ToString() ?? "img",
                            imgObj["context"]?.ToString() ?? string.Empty,
                            imgObj["pageOrSlide"]?.Value<int>() ?? 0,
                            filePath);
                        images.Add(imageSource);
                    }
                }

                var warnings = new List<string>();
                var warningsArray = toolResult["warnings"] as JArray;
                if (warningsArray != null)
                {
                    Debug.WriteLine($"[File2Md] Found {warningsArray.Count} warnings");
                    foreach (var w in warningsArray)
                    {
                        warnings.Add(w.ToString());
                    }
                }

                Debug.WriteLine($"[File2Md] ConvertFileAsync complete: content={content.Length} chars, {images.Count} images, {warnings.Count} warnings");
                return (content, format, images, warnings);
            }
        }
    }
}
