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
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AICall.Core.Returns;
using SmartHopper.Infrastructure.AICall.Tools;

namespace SmartHopper.Components.Knowledge
{
    public class FileToMdComponent : StatefulComponentBase
    {
        public override Guid ComponentGuid => new Guid("C0EF8C72-1233-4613-902C-2E07321BB2E3");

        protected override Bitmap Icon => Resources.filetomd;

        public override GH_Exposure Exposure => GH_Exposure.tertiary;

        public FileToMdComponent()
            : base(
                  "File To Markdown",
                  "FileToMd",
                  "Convert a local file (PDF, DOCX, XLSX, PPTX, HTML, CSV, JSON, XML, TXT, EML, EPUB, RTF, etc.) to Markdown text.",
                  "SmartHopper",
                  "Knowledge")
        {
            this.RunOnlyOnInputChanges = false;
        }

        protected override void RegisterAdditionalInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("File Path", "F", "REQUIRED absolute path(s) to the file(s) to convert.", GH_ParamAccess.tree);
        }

        protected override void RegisterAdditionalOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Markdown", "Md", "Markdown content of the file.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Format", "Fmt", "Detected original format (e.g., pdf, docx, html).", GH_ParamAccess.tree);
        }

        protected override AsyncWorkerBase CreateWorker(Action<string> progressReporter)
        {
            return new FileToMdWorker(this, this.AddRuntimeMessage, ComponentProcessingOptions);
        }

        private sealed class FileToMdWorker : AsyncWorkerBase
        {
            private readonly FileToMdComponent parent;
            private readonly ProcessingOptions processingOptions;
            private Dictionary<string, GH_Structure<GH_String>> inputTrees;
            private bool hasWork;

            private GH_Structure<GH_String> resultMarkdown;
            private GH_Structure<GH_String> resultFormat;

            public FileToMdWorker(
                FileToMdComponent parent,
                Action<GH_RuntimeMessageLevel, string> addRuntimeMessage,
                ProcessingOptions processingOptions)
                : base(parent, addRuntimeMessage)
            {
                this.parent = parent;
                this.processingOptions = processingOptions;
            }

            public override void GatherInput(IGH_DataAccess DA, out int dataCount)
            {
                var filePathTree = new GH_Structure<GH_String>();
                DA.GetDataTree("File Path", out filePathTree);

                this.inputTrees = new Dictionary<string, GH_Structure<GH_String>>
                {
                    { "File Path", filePathTree ?? new GH_Structure<GH_String>() },
                };

                this.hasWork = filePathTree != null && filePathTree.PathCount > 0 && filePathTree.DataCount > 0;
                dataCount = this.hasWork ? filePathTree.DataCount : 0;

                this.resultMarkdown = new GH_Structure<GH_String>();
                this.resultFormat = new GH_Structure<GH_String>();
            }

            public override async Task DoWorkAsync(CancellationToken token)
            {
                if (!this.hasWork)
                {
                    return;
                }

                try
                {
                    var resultTrees = await this.parent.RunProcessingAsync<GH_String, GH_String>(
                        this.inputTrees,
                        async branchInputs =>
                        {
                            var outputs = new Dictionary<string, List<GH_String>>
                            {
                                { "File Path", new List<GH_String>() },
                                { "Markdown", new List<GH_String>() },
                                { "Format", new List<GH_String>() },
                            };

                            foreach (var kvp in branchInputs)
                            {
                                var filePaths = kvp.Value;

                                foreach (var ghFilePath in filePaths)
                                {
                                    if (ghFilePath == null || string.IsNullOrWhiteSpace(ghFilePath.Value))
                                    {
                                        outputs["Markdown"].Add(new GH_String(string.Empty));
                                        outputs["Format"].Add(new GH_String(string.Empty));
                                        continue;
                                    }

                                    string filePath = ghFilePath.Value;

                                    var parameters = new JObject
                                    {
                                        ["filePath"] = filePath,
                                        ["preserveTableStructure"] = true,
                                        ["removeHeadersFooters"] = true
                                    };

                                    var toolCallInteraction = new AIInteractionToolCall
                                    {
                                        Name = "file_to_md",
                                        Arguments = parameters,
                                        Agent = AIAgent.Assistant,
                                    };

                                    var toolCall = new AIToolCall
                                    {
                                        Endpoint = "file_to_md",
                                    };

                                    toolCall.FromToolCallInteraction(toolCallInteraction);
                                    toolCall.SkipMetricsValidation = true;

                                    AIReturn aiResult = await toolCall.Exec().ConfigureAwait(false);
                                    var toolResultInteraction = aiResult.Body?.GetLastInteraction(AIAgent.ToolResult) as AIInteractionToolResult;
                                    var toolResult = toolResultInteraction?.Result;

                                    if (toolResult == null)
                                    {
                                        this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Tool 'file_to_md' returned no result for '{filePath}'.");
                                        outputs["Markdown"].Add(new GH_String(string.Empty));
                                        outputs["Format"].Add(new GH_String(string.Empty));
                                        continue;
                                    }

                                    string content = toolResult["content"]?.ToString() ?? string.Empty;
                                    string format = toolResult["originalFormat"]?.ToString() ?? string.Empty;

                                    outputs["Markdown"].Add(new GH_String(content));
                                    outputs["Format"].Add(new GH_String(format));

                                    // Add warnings if present
                                    var warnings = toolResult["warnings"] as JArray;
                                    if (warnings != null && warnings.Count > 0)
                                    {
                                        foreach (var warning in warnings)
                                        {
                                            this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, warning.ToString());
                                        }
                                    }
                                }
                            }

                            return outputs;
                        },
                        this.processingOptions,
                        token).ConfigureAwait(false);

                    // Only initialize result structures if they weren't already populated
                    this.resultMarkdown ??= new GH_Structure<GH_String>();
                    this.resultFormat ??= new GH_Structure<GH_String>();

                    if (resultTrees.TryGetValue("Markdown", out var markdownTree))
                    {
                        this.resultMarkdown = markdownTree;
                        Debug.WriteLine($"[FileToMd] Retrieved Markdown tree with {markdownTree.DataCount} items");
                    }
                    else
                    {
                        Debug.WriteLine("[FileToMd] WARNING: 'Markdown' not found in resultTrees. Keys: " + string.Join(", ", resultTrees.Keys));
                    }

                    if (resultTrees.TryGetValue("Format", out var formatTree))
                    {
                        this.resultFormat = formatTree;
                        Debug.WriteLine($"[FileToMd] Retrieved Format tree with {formatTree.DataCount} items");
                    }
                    else
                    {
                        Debug.WriteLine("[FileToMd] WARNING: 'Format' not found in resultTrees");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[FileToMd] Error: {ex.Message}");
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
                }
            }

            public override void SetOutput(IGH_DataAccess DA, out string errorMessage)
            {
                Debug.WriteLine($"[FileToMd] SetOutput called - Markdown has {this.resultMarkdown?.DataCount ?? 0} items, Format has {this.resultFormat?.DataCount ?? 0} items");
                this.parent.SetPersistentOutput("Markdown", this.resultMarkdown ?? new GH_Structure<GH_String>(), DA);
                this.parent.SetPersistentOutput("Format", this.resultFormat ?? new GH_Structure<GH_String>(), DA);
                errorMessage = null;
            }
        }
    }
}
