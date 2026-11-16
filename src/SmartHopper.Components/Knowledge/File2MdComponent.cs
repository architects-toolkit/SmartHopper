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
using SmartHopper.Core.Grasshopper.AITools;
using SmartHopper.Core.Models;
using SmartHopper.Core.Parameters;
using SmartHopper.Core.Types;
using SmartHopper.DocumentProcessing;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.Diagnostics;

using SmartHopper.ProviderSdk.Logging;
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
        protected override Bitmap Icon => Resources.File2Md;

        /// <inheritdoc/>
        public override GH_Exposure Exposure => GH_Exposure.primary;

        /// <summary>
        /// Initializes a new instance of the <see cref="File2MdComponent"/> class.
        /// </summary>
        public File2MdComponent()
            : base(
                  "File To Markdown",
                  "File2Md",
                  "Convert a local file (PDF, DOCX, XLSX, PPTX, HTML, CSV, JSON, XML, TXT, EML, EPUB, RTF, etc.) to Markdown, embedded image assets, and rendered page images locally — no AI provider required.\nUse AI File To Markdown when you want AI-generated image descriptions.",
                  "SmartHopper",
                  "F. Documents")
        {
        }

        /// <inheritdoc/>
        protected override void RegisterAdditionalInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("File Path", "F", "Local path(s) to the file(s) to convert.", GH_ParamAccess.tree);
            pManager.AddBooleanParameter("Remove Headers", "RH", "When true (default), removes repeating page headers and footers from PDF and DOCX files.", GH_ParamAccess.tree, true);
            pManager.AddTextParameter("Pages", "P", "Optional PDF page selection as a JSON array of unique 1-based page numbers (e.g. [1,3]). An empty string selects the whole document; an empty array is invalid.", GH_ParamAccess.tree, string.Empty);
            pManager.AddTextParameter("Region", "R", "Optional PDF detail region as a JSON object {page, x, y, width, height} with normalized top-left coordinates inside a selected page.", GH_ParamAccess.tree, string.Empty);
            pManager[pManager.ParamCount - 1].Optional = true;
            pManager[pManager.ParamCount - 2].Optional = true;
        }

        /// <inheritdoc/>
        protected override void RegisterAdditionalOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Markdown", "Md", "Converted file content in Markdown format.", GH_ParamAccess.tree);
            pManager.AddParameter(new VersatileImageParameter(), "Images", "Img", "Embedded image assets extracted from the document (connect to Image Viewer, Image to AI, or AI Image To Text).", GH_ParamAccess.tree);
            pManager.AddTextParameter("Format", "Fmt", "Detected source file format (e.g., pdf, docx, xlsx, html).", GH_ParamAccess.tree);
            pManager.AddParameter(new VersatileImageParameter(), "Rendered Pages", "Pgs", "Rendered page images of the document in document order, including any requested region crops (PDF sources).", GH_ParamAccess.tree);
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
            private GH_Structure<GH_String> pagesTree;
            private GH_Structure<GH_String> regionTree;
            private bool hasWork;

            private GH_Structure<GH_String> resultMarkdown;
            private GH_Structure<GH_String> resultFormat;
            private GH_Structure<GH_VersatileImage> resultImages;
            private GH_Structure<GH_VersatileImage> resultRenderedPages;

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

                GH_Structure<GH_Boolean> removeTree;
                DA.GetDataTree("Remove Headers", out removeTree);

                this.removeHeadersTree = File2MdToolResult.ConvertBoolTreeToString(removeTree, "true");

                DA.GetDataTree("Pages", out this.pagesTree);
                DA.GetDataTree("Region", out this.regionTree);

                this.hasWork = this.filePathTree != null && this.filePathTree.PathCount > 0 && this.filePathTree.DataCount > 0;
                dataCount = this.hasWork ? this.filePathTree.DataCount : 0;

                this.resultMarkdown = new GH_Structure<GH_String>();
                this.resultFormat = new GH_Structure<GH_String>();
                this.resultImages = new GH_Structure<GH_VersatileImage>();
                this.resultRenderedPages = new GH_Structure<GH_VersatileImage>();
            }

            /// <inheritdoc/>
            public override async Task DoWorkAsync(CancellationToken token)
            {
                this.resultMarkdown = new GH_Structure<GH_String>();
                this.resultFormat = new GH_Structure<GH_String>();
                this.resultImages = new GH_Structure<GH_VersatileImage>();
                this.resultRenderedPages = new GH_Structure<GH_VersatileImage>();

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
                        { "Pages", this.pagesTree ?? new GH_Structure<GH_String>() },
                        { "Region", this.regionTree ?? new GH_Structure<GH_String>() },
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
                                { "Rendered Pages", new List<IGH_Goo>() },
                            };

                            if (!branchInputs.TryGetValue("File Path", out var pathBranch) || pathBranch == null)
                            {
                                return outputs;
                            }

                            var removeBranch = branchInputs.TryGetValue("RemoveHeaders", out var rh) ? rh : new List<GH_String>();
                            var pagesBranch = branchInputs.TryGetValue("Pages", out var pg) ? pg : new List<GH_String>();
                            var regionBranch = branchInputs.TryGetValue("Region", out var rg) ? rg : new List<GH_String>();

                            var normalizedLists = DataTreeProcessor.NormalizeBranchLengths(new List<List<GH_String>> { pathBranch, removeBranch, pagesBranch, regionBranch });
                            pathBranch = normalizedLists[0];
                            removeBranch = normalizedLists[1];
                            pagesBranch = normalizedLists[2];
                            regionBranch = normalizedLists[3];

                            for (int i = 0; i < pathBranch.Count; i++)
                            {
                                token.ThrowIfCancellationRequested();

                                var ghPath = pathBranch[i];
                                bool removeHeaders = bool.TryParse(removeBranch[i]?.Value, out var rhValue) ? rhValue : true;
                                List<int> pages = null;
                                DocumentRegionSelection region = null;
                                var pagesRaw = pagesBranch[i]?.Value;
                                var regionRaw = regionBranch[i]?.Value;
                                var selectionFailed = false;
                                try
                                {
                                    selectionFailed = !string.IsNullOrWhiteSpace(pagesRaw)
                                        && !DocumentSelectionJsonParser.TryParsePages(Newtonsoft.Json.Linq.JToken.Parse(pagesRaw), out pages, out var _);
                                }
                                catch
                                {
                                    selectionFailed = true;
                                }

                                try
                                {
                                    selectionFailed |= !string.IsNullOrWhiteSpace(regionRaw)
                                        && !DocumentSelectionJsonParser.TryParseRegion(Newtonsoft.Json.Linq.JToken.Parse(regionRaw), out region, out var _);
                                }
                                catch
                                {
                                    selectionFailed = true;
                                }

                                if (selectionFailed)
                                {
                                    this.CollectMessage(SHRuntimeMessageSeverity.Error, $"Invalid 'Pages'/'Region' selection JSON for '{ghPath?.Value}'.", SHRuntimeMessageOrigin.Tool);
                                    outputs["Markdown"].Add(new GH_String(string.Empty));
                                    outputs["Format"].Add(new GH_String(string.Empty));
                                    continue;
                                }

                                if (ghPath == null || string.IsNullOrWhiteSpace(ghPath.Value))
                                {
                                    outputs["Markdown"].Add(new GH_String(string.Empty));
                                    outputs["Format"].Add(new GH_String(string.Empty));
                                    continue;
                                }

                                const bool PreserveFormatting = true;
                                var converted = await File2MdToolResult.CallAsync(
                                    ghPath.Value,
                                    removeHeaders,
                                    extractImages: true,
                                    preserveFormatting: PreserveFormatting,
                                    preserveComments: PreserveFormatting,
                                    preserveFootnotes: PreserveFormatting,
                                    preserveEndnotes: PreserveFormatting,
                                    pages: pages,
                                    region: region,
                                    cancellationToken: token).ConfigureAwait(false);

                                if (converted == null)
                                {
                                    this.CollectMessage(SHRuntimeMessageSeverity.Warning, $"Tool 'file2md' returned no result for '{ghPath.Value}'.", SHRuntimeMessageOrigin.Tool);
                                    outputs["Markdown"].Add(new GH_String(string.Empty));
                                    outputs["Format"].Add(new GH_String(string.Empty));
                                    continue;
                                }

                                outputs["Markdown"].Add(new GH_String(converted.Markdown));
                                outputs["Format"].Add(new GH_String(converted.Format));

                                foreach (var img in converted.Images)
                                {
                                    var kind = img.SourceReference?.Kind;
                                    if (kind == SourceContentKind.Page || kind == SourceContentKind.Crop)
                                    {
                                        outputs["Rendered Pages"].Add(new GH_VersatileImage(img));
                                    }
                                    else
                                    {
                                        outputs["Images"].Add(new GH_VersatileImage(img));
                                    }
                                }

                                foreach (var w in converted.Warnings)
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
                    this.resultRenderedPages = DataTreeProcessor.ExtractTypedTree<GH_VersatileImage>(resultTrees, "Rendered Pages");
                }
                catch (OperationCanceledException)
                {
                    this.CollectMessage(SHRuntimeMessageSeverity.Warning, "Operation was cancelled.");
                }
                catch (Exception ex)
                {
                    Logger.For<File2MdWorker>().Exception(ex, $"[File2Md] Error: {ex.Message}");
                    this.CollectMessage(SHRuntimeMessageSeverity.Error, ex.Message);
                }
            }

            /// <inheritdoc/>
            public override void SetOutput(IGH_DataAccess DA, out string errorMessage)
            {
                this.parent.SetPersistentOutput("Markdown", this.resultMarkdown ?? new GH_Structure<GH_String>(), DA);
                this.parent.SetPersistentOutput("Images", this.resultImages ?? new GH_Structure<GH_VersatileImage>(), DA);
                this.parent.SetPersistentOutput("Format", this.resultFormat ?? new GH_Structure<GH_String>(), DA);
                this.parent.SetPersistentOutput("Rendered Pages", this.resultRenderedPages ?? new GH_Structure<GH_VersatileImage>(), DA);
                errorMessage = null;
            }

        }
    }
}
