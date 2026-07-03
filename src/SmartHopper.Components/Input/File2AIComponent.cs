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
using SmartHopper.Components.Properties;
using SmartHopper.Core.ComponentBase;
using SmartHopper.Core.DataTree;
using SmartHopper.Core.Grasshopper.AITools;
using SmartHopper.Core.Models;
using SmartHopper.Core.Types;
using SmartHopper.Infrastructure.AICall.Utilities;
using SmartHopper.Infrastructure.Diagnostics;

namespace SmartHopper.Components.Input
{
    /// <summary>
    /// Reads a file using the file2md AI tool and wraps its Markdown content into an AIInputPayload.
    /// Supports PDF, DOCX, XLSX, PPTX, HTML, CSV, JSON, XML, TXT, EML, EPUB, RTF, and more.
    /// </summary>
    public class File2AIComponent : StatefulComponentBase
    {
        public override Guid ComponentGuid => new Guid("AC2DDCD0-B4E9-4B99-80DA-CA9F9BBCD4C9");

        protected override Bitmap Icon => Resources.toaifile;

        public override GH_Exposure Exposure => GH_Exposure.septenary;

        public File2AIComponent()
            : base(
                "File to AI",
                "File2AI",
                "Reads a file and converts it to Markdown using file2md AI tool, wrapping the content into an AIInputPayload.",
                "SmartHopper",
                "B. Input")
        {
        }

        protected override void RegisterAdditionalInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("File Path", "F", "Path(s) to the file(s) to convert and wrap into an AIInputPayload.", GH_ParamAccess.tree);
            pManager.AddBooleanParameter("Remove Headers", "RH", "Attempt to remove headers and footers from PDF/DOCX. Default: true.", GH_ParamAccess.tree, true);
            pManager.AddBooleanParameter("Extract Images", "EI", "Extract embedded images as base64 data. Default: false.", GH_ParamAccess.tree, false);
        }

        protected override void RegisterAdditionalOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddParameter(new AIInputPayloadParameter(), "Input >", ">", "AIInputPayload(s) containing the file content as Markdown.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Markdown", "M", "Converted file content as Markdown.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Format", "F", "Detected source format of the file.", GH_ParamAccess.tree);
        }

        protected override ProcessingOptions ComponentProcessingOptions => new ProcessingOptions
        {
            Topology = ProcessingTopology.BranchToBranch,
            OnlyMatchingPaths = false,
            GroupIdenticalBranches = true,
        };

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

                var extractTree = new GH_Structure<GH_Boolean>();
                DA.GetDataTree("Extract Images", out extractTree);

                // Convert boolean trees to string trees for unified processing
                this.inputTrees["FilePath"] = pathTree;
                this.inputTrees["RemoveHeaders"] = File2MdToolResult.ConvertBoolTreeToString(removeTree, "true");
                this.inputTrees["ExtractImages"] = File2MdToolResult.ConvertBoolTreeToString(extractTree, "false");

                dataCount = 0;
            }

            public override async Task DoWorkAsync(CancellationToken token)
            {
                try
                {
                    this.result = await this.parent.RunProcessingAsync(
                        this.inputTrees,
                        async (branches) =>
                        {
                            return await this.ProcessBranches(branches).ConfigureAwait(false);
                        },
                        this.processingOptions,
                        token).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[File2AIWorker] Error: {ex.Message}");
                    this.CollectMessage(SHRuntimeMessageSeverity.Error, $"Error processing files: {ex.Message}");
                }
            }

            private async Task<Dictionary<string, List<IGH_Goo>>> ProcessBranches(Dictionary<string, List<GH_String>> branches)
            {
                var outputs = new Dictionary<string, List<IGH_Goo>>
                {
                    { "Input >", new List<IGH_Goo>() },
                    { "Markdown", new List<IGH_Goo>() },
                    { "Format", new List<IGH_Goo>() },
                };

                var filePaths = branches["FilePath"];
                var removeList = branches["RemoveHeaders"];
                var extractList = branches["ExtractImages"];

                // Normalize branch lengths to handle mismatched input trees
                var normalizedLists = DataTreeProcessor.NormalizeBranchLengths(new List<List<GH_String>> { filePaths, removeList, extractList });
                filePaths = normalizedLists[0];
                removeList = normalizedLists[1];
                extractList = normalizedLists[2];

                for (int i = 0; i < filePaths.Count; i++)
                {
                    string filePath = filePaths[i]?.Value;
                    bool removeHeaders = bool.TryParse(removeList[i]?.Value, out var rh) ? rh : true;
                    bool extractImages = bool.TryParse(extractList[i]?.Value, out var ei) ? ei : false;

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
                            extractImages,
                            preserveFormatting: true,
                            preserveComments: true,
                            preserveFootnotes: true,
                            preserveEndnotes: true).ConfigureAwait(false);

                        if (converted == null)
                        {
                            this.CollectMessage(SHRuntimeMessageSeverity.Warning, $"Tool 'file2md' returned no result for: {filePath}", SHRuntimeMessageOrigin.Tool);
                            outputs["Input >"].Add(new GH_AIInputPayload(null));
                            outputs["Markdown"].Add(new GH_String(string.Empty));
                            outputs["Format"].Add(new GH_String("error"));
                            continue;
                        }

                        AIInputPayload payload = string.IsNullOrWhiteSpace(converted.Markdown)
                            ? null
                            : AIInputPayload.FromText(converted.Markdown);

                        foreach (var w in converted.Warnings)
                        {
                            this.CollectMessage(SHRuntimeMessageSeverity.Warning, w, SHRuntimeMessageOrigin.Tool);
                        }

                        outputs["Input >"].Add(new GH_AIInputPayload(payload));
                        outputs["Markdown"].Add(new GH_String(converted.Markdown));
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
                if (this.result.TryGetValue("Input >", out var payloadTree) && payloadTree != null)
                {
                    this.parent.SetPersistentOutput("Input >", payloadTree, DA);
                }

                if (this.result.TryGetValue("Markdown", out var markdownTree) && markdownTree != null)
                {
                    this.parent.SetPersistentOutput("Markdown", markdownTree, DA);
                }

                if (this.result.TryGetValue("Format", out var formatTree) && formatTree != null)
                {
                    this.parent.SetPersistentOutput("Format", formatTree, DA);
                }

                int successCount = 0;
                if (this.result.TryGetValue("Markdown", out var tree) && tree != null)
                {
                    foreach (var path in tree.Paths)
                    {
                        var branch = tree.get_Branch(path);
                        if (branch != null)
                        {
                            foreach (var item in branch)
                            {
                                if (item is GH_String gs && !string.IsNullOrWhiteSpace(gs.Value))
                                {
                                    successCount++;
                                }
                            }
                        }
                    }
                }

                int totalCount = this.inputTrees?.Values.FirstOrDefault()?.DataCount ?? 0;
                message = $"Processed {successCount}/{totalCount} file(s)";
            }
        }
    }
}
