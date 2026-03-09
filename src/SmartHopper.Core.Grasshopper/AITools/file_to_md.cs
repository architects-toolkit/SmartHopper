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
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SmartHopper.Core.Grasshopper.Converters;
using SmartHopper.Core.Grasshopper.Converters.Formats;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AICall.Core.Returns;
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.Infrastructure.AITools;

namespace SmartHopper.Core.Grasshopper.AITools
{
    /// <summary>
    /// Provides AI tool for converting local files to Markdown.
    /// Supports PDF, DOCX, XLSX, PPTX, HTML, CSV, JSON, XML, TXT, EML, EPUB, RTF, and more.
    /// </summary>
    public sealed class file_to_md : IAIToolProvider
    {
        private readonly string toolName = "file_to_md";
        private static FileConverterRegistry? registry;

        /// <summary>
        /// Gets or creates the converter registry with all built-in converters.
        /// </summary>
        private static FileConverterRegistry GetRegistry()
        {
            if (registry == null)
            {
                registry = new FileConverterRegistry();
                
                // Register all converters
                registry.RegisterAll(new IFileConverter[]
                {
                    new TxtConverter(),
                    new CsvConverter(),
                    new JsonConverter(),
                    new XmlConverter(),
                    new HtmlConverter(),
                    new PdfConverter(),
                    new DocxConverter(),
                    new XlsxConverter(),
                    new PptxConverter(),
                    new EmlConverter(),
                    new EpubConverter(),
                    new RtfConverter(),
                });
            }

            return registry;
        }

        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: this.toolName,
                description: "Convert a local file (PDF, DOCX, XLSX, PPTX, HTML, CSV, JSON, XML, TXT, EML, EPUB, RTF, etc.) to Markdown text. Use this when you need to read the contents of a file that the user has mentioned or referenced.",
                category: "Knowledge",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""filePath"": {
                            ""type"": ""string"",
                            ""description"": ""Absolute path to the file to convert.""
                        },
                        ""preserveTableStructure"": {
                            ""type"": ""boolean"",
                            ""description"": ""Whether to preserve table structure as Markdown tables. Default: true."",
                            ""default"": true
                        },
                        ""removeHeadersFooters"": {
                            ""type"": ""boolean"",
                            ""description"": ""Whether to attempt to remove headers and footers (PDF, DOCX). Default: true."",
                            ""default"": true
                        }
                    },
                    ""required"": [""filePath""]
                }",
                execute: this.FileToMdAsync);
        }

        private async Task<AIReturn> FileToMdAsync(AIToolCall toolCall)
        {
            var output = new AIReturn()
            {
                Request = toolCall,
            };

            try
            {
                // Local tool: skip metrics validation
                toolCall.SkipMetricsValidation = true;

                // Extract parameters
                AIInteractionToolCall toolInfo = toolCall.GetToolCall();
                var args = toolInfo.Arguments ?? new JObject();
                
                string filePath = args["filePath"]?.ToString();
                if (string.IsNullOrWhiteSpace(filePath))
                {
                    output.CreateError("Missing 'filePath' parameter.");
                    return output;
                }

                // Get conversion options
                var options = new FileConversionOptions
                {
                    PreserveTableStructure = args["preserveTableStructure"]?.Value<bool>() ?? true,
                    RemoveHeadersFooters = args["removeHeadersFooters"]?.Value<bool>() ?? true,
                    DetectHeadings = true,
                    MaxContentLength = 0
                };

                // Convert the file
                var converterRegistry = GetRegistry();
                var result = await converterRegistry.ConvertAsync(filePath, options).ConfigureAwait(false);

                if (!result.IsSuccess)
                {
                    var errorMessage = result.Warnings.Count > 0 
                        ? string.Join("; ", result.Warnings) 
                        : "Conversion failed.";
                    output.CreateError(errorMessage);
                    return output;
                }

                // Build result
                var toolResult = new JObject
                {
                    ["content"] = result.MarkdownContent,
                    ["originalFormat"] = result.DetectedFormat,
                    ["source"] = filePath,
                };

                // Add metadata if present
                if (result.Metadata.Count > 0)
                {
                    var metadata = new JObject();
                    foreach (var kvp in result.Metadata)
                    {
                        metadata[kvp.Key] = kvp.Value;
                    }
                    toolResult["metadata"] = metadata;
                }

                // Add warnings if present
                if (result.Warnings.Count > 0)
                {
                    var warnings = new JArray();
                    foreach (var warning in result.Warnings)
                    {
                        warnings.Add(warning);
                    }
                    toolResult["warnings"] = warnings;
                }

                var builder = AIBodyBuilder.Create();
                builder.AddToolResult(toolResult, toolInfo.Id, toolInfo.Name);
                var immutable = builder.Build();
                output.CreateSuccess(immutable, toolCall);
                return output;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FileToMd] Error in FileToMdAsync: {ex.Message}");
                output.CreateError($"Error: {ex.Message}");
                return output;
            }
        }
    }
}
