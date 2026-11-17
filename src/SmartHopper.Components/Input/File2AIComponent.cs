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
using System.Security.Cryptography;
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
using SmartHopper.Core.Models;
using SmartHopper.Core.Types;
using SmartHopper.DocumentProcessing;
using SmartHopper.ProviderSdk.AICall.Core.Base;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AICall.Metrics;
using SmartHopper.ProviderSdk.Diagnostics;
using SmartHopper.ProviderSdk.Documents;

using SmartHopper.ProviderSdk.Logging;
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
        /// Local Markdown tree produced during DoWorkAsync; used to build the AIInputPayload output.
        /// </summary>
        private GH_Structure<GH_String> _localMarkdown;

        /// <summary>
        /// Local Format tree produced during DoWorkAsync; does not need AI so it is always available.
        /// </summary>
        private GH_Structure<GH_String> _localFormat;

        /// <summary>
        /// This component's work has side effects or consumes external state
        /// (canvas mutations, chat history, file/web fetches) so per-unit results
        /// are not reusable across runs.
        /// </summary>
        protected override bool SupportsResultReuse => false;

        public override Guid ComponentGuid => new Guid("AC2DDCD0-B4E9-4B99-80DA-CA9F9BBCD4C9");

        protected override Bitmap Icon => Resources.File2AI;

        public override GH_Exposure Exposure => GH_Exposure.tertiary;

        /// <inheritdoc/>
        protected override IReadOnlyList<string> UsingAiTools => new[] { "file2md" };

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
                "Convert a local file (PDF, DOCX, XLSX, PPTX, HTML, CSV, JSON, XML, TXT, EML, EPUB, RTF, etc.) to Markdown and visual evidence packaged as one ordered multimodal payload ready to wire into any AI output component. PDF sources include rendered pages; no provider call is made while packaging.",
                "SmartHopper",
                "B. Input")
        {
        }

        protected override void RegisterAdditionalInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("File Path", "F", "Local path(s) to the file(s) to convert and send to the AI.", GH_ParamAccess.tree);
            pManager.AddBooleanParameter("Remove Headers", "RH", "When true (default), removes repeating page headers and footers from PDF and DOCX files.", GH_ParamAccess.tree, true);
            pManager.AddTextParameter("Pages", "P", "Optional PDF page selection as a JSON array of unique 1-based page numbers (e.g. [1,3]). An empty string selects the whole document; an empty array is invalid.", GH_ParamAccess.tree, string.Empty);
            pManager.AddTextParameter("Region", "R", "Optional PDF detail region as a JSON object {page, x, y, width, height} with normalized top-left coordinates inside a selected page.", GH_ParamAccess.tree, string.Empty);
            pManager[pManager.ParamCount - 1].Optional = true;
            pManager[pManager.ParamCount - 2].Optional = true;
        }

        protected override void RegisterAdditionalOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddParameter(new AIInputPayloadParameter(), "Input >", ">", "Packaged file content (Markdown) ready to wire into any AI output component's 'Input >' parameter.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Markdown", "M", "Converted file content in Markdown format.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Format", "F", "Detected source file format (e.g., pdf, docx, xlsx, html).", GH_ParamAccess.tree);
        }

        protected override void OnEnteringNeedsRun()
        {
            base.OnEnteringNeedsRun();
            this._localMarkdown = null;
            this._localFormat = null;
        }

        /// <inheritdoc/>

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
                this.inputTrees = new Dictionary<string, GH_Structure<GH_String>>();

                var pathTree = new GH_Structure<GH_String>();
                DA.GetDataTree("File Path", out pathTree);

                var removeTree = new GH_Structure<GH_Boolean>();
                DA.GetDataTree("Remove Headers", out removeTree);

                GH_Structure<GH_String> pagesTree;
                DA.GetDataTree("Pages", out pagesTree);

                GH_Structure<GH_String> regionTree;
                DA.GetDataTree("Region", out regionTree);

                this.inputTrees["FilePath"] = pathTree;
                this.inputTrees["RemoveHeaders"] = File2MdToolResult.ConvertBoolTreeToString(removeTree, "true");
                this.inputTrees["Pages"] = pagesTree ?? new GH_Structure<GH_String>();
                this.inputTrees["Region"] = regionTree ?? new GH_Structure<GH_String>();

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

                    this.parent.FinishResults(
                        "Markdown",
                        markdownTree ?? new GH_Structure<GH_String>(),
                        ("Input >", (object)(this.result["Input >"] ?? new GH_Structure<IGH_Goo>())),
                        ("Format", (object)(formatTree ?? new GH_Structure<GH_String>())));
                }
                catch (Exception ex)
                {
                    Logger.For<File2AIWorker>().Exception(ex, $"[File2AIWorker] Error: {ex.Message}");
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
                var pagesList = branches["Pages"];
                var regionList = branches["Region"];

                var normalizedLists = DataTreeProcessor.NormalizeBranchLengths(new List<List<GH_String>> { filePaths, removeList, pagesList, regionList });
                filePaths = normalizedLists[0];
                removeList = normalizedLists[1];
                pagesList = normalizedLists[2];
                regionList = normalizedLists[3];

                for (int i = 0; i < filePaths.Count; i++)
                {
                    string filePath = filePaths[i]?.Value;
                    bool removeHeaders = bool.TryParse(removeList[i]?.Value, out var rh) ? rh : true;

                    List<int> pages = null;
                    DocumentRegionSelection region = null;
                    var pagesJson = pagesList?[i]?.Value?.Trim();
                    var regionJson = regionList?[i]?.Value?.Trim();
                    if (!string.IsNullOrEmpty(pagesJson))
                    {
                        JToken pagesToken;
                        try
                        {
                            pagesToken = JToken.Parse(pagesJson);
                        }
                        catch (Exception ex)
                        {
                            this.CollectMessage(SHRuntimeMessageSeverity.Error, $"Invalid 'Pages' JSON for {filePath}: {ex.Message}");
                            outputs["Input >"].Add(new GH_AIInputPayload(null));
                            outputs["Markdown"].Add(new GH_String(string.Empty));
                            outputs["Format"].Add(new GH_String("error"));
                            continue;
                        }

                        if (!DocumentSelectionJsonParser.TryParsePages(pagesToken, out pages, out var pagesError))
                        {
                            this.CollectMessage(SHRuntimeMessageSeverity.Error, $"Invalid 'Pages' for {filePath}: {pagesError}");
                            outputs["Input >"].Add(new GH_AIInputPayload(null));
                            outputs["Markdown"].Add(new GH_String(string.Empty));
                            outputs["Format"].Add(new GH_String("error"));
                            continue;
                        }
                    }

                    if (!string.IsNullOrEmpty(regionJson))
                    {
                        JToken regionToken;
                        try
                        {
                            regionToken = JToken.Parse(regionJson);
                        }
                        catch (Exception ex)
                        {
                            this.CollectMessage(SHRuntimeMessageSeverity.Error, $"Invalid 'Region' JSON for {filePath}: {ex.Message}");
                            outputs["Input >"].Add(new GH_AIInputPayload(null));
                            outputs["Markdown"].Add(new GH_String(string.Empty));
                            outputs["Format"].Add(new GH_String("error"));
                            continue;
                        }

                        if (!DocumentSelectionJsonParser.TryParseRegion(regionToken, out region, out var regionError))
                        {
                            this.CollectMessage(SHRuntimeMessageSeverity.Error, $"Invalid 'Region' for {filePath}: {regionError}");
                            outputs["Input >"].Add(new GH_AIInputPayload(null));
                            outputs["Markdown"].Add(new GH_String(string.Empty));
                            outputs["Format"].Add(new GH_String("error"));
                            continue;
                        }
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
                        var converted = await File2MdToolResult.CallAsync(
                            filePath,
                            removeHeaders,
                            extractImages: true,
                            preserveFormatting: true,
                            preserveComments: true,
                            preserveFootnotes: true,
                            preserveEndnotes: true,
                            describeImages: false,
                            pages: pages,
                            region: region,
                            cancellationToken: token).ConfigureAwait(false);

                        if (converted == null)
                        {
                            this.CollectMessage(SHRuntimeMessageSeverity.Warning, $"Tool 'file2md' returned no result for: {filePath}", SHRuntimeMessageOrigin.Tool);
                            outputs["Input >"].Add(new GH_AIInputPayload(null));
                            outputs["Markdown"].Add(new GH_String(string.Empty));
                            outputs["Format"].Add(new GH_String("error"));
                            continue;
                        }

                        string markdown = converted.Markdown;

                        // ONE ordered multimodal source payload: document identity/coverage
                        // text first, then selected-page text + its model-visible page
                        // images/crops in page order. No provider call happens here.
                        var payload = BuildOrderedPayload(converted, filePath, markdown);

                        foreach (var w in converted.Warnings)
                        {
                            this.CollectMessage(SHRuntimeMessageSeverity.Warning, w, SHRuntimeMessageOrigin.Tool);
                        }

                        outputs["Input >"].Add(new GH_AIInputPayload(payload));
                        outputs["Markdown"].Add(new GH_String(markdown));
                        outputs["Format"].Add(new GH_String(string.IsNullOrEmpty(converted.Format) ? "unknown" : converted.Format));
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
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

            /// <summary>
            /// Builds the ONE ordered multimodal source payload via the shared
            /// <see cref="DocumentEvidenceInteractionBuilder"/>: identity/coverage header,
            /// then each selected page's text followed by its model-visible renders/crops
            /// in page order, all under one TurnId with source provenance. No provider
            /// call happens while packaging; scanned pages with empty text still produce
            /// image-bearing payloads.
            /// </summary>
            private static AIInputPayload BuildOrderedPayload(File2MdToolResult converted, string filePath, string markdown)
            {
                var document = converted.Document;
                if (document != null)
                {
                    var interactions = DocumentEvidenceInteractionBuilder.Build(document, markdown);
                    return interactions.Count == 0 ? null : AIInputPayload.FromInteractions(interactions);
                }

                // Non-document sources (no pages): wrap the result in a neutral
                // page-less DocumentEvidence so the shared builder emits the same
                // one-TurnId source/provenance contract as PDF content.
                var neutral = new DocumentEvidence
                {
                    DocumentId = "sha256-" + Convert.ToHexString(
                        SHA256.HashData(Encoding.UTF8.GetBytes(filePath + "\n" + (markdown ?? string.Empty))))
                        .ToLowerInvariant(),
                    SourceName = filePath,
                    Format = converted.Format,
                    Images = converted.TypedImages,
                    Coverage = new DocumentCoverage { IsComplete = true },
                };
                var built = DocumentEvidenceInteractionBuilder.Build(neutral, markdown);
                return built.Count == 0 ? null : AIInputPayload.FromInteractions(built);
            }
        }
    }
}
