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
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SmartHopper.Core.Grasshopper.Converters;
using SmartHopper.Core.Grasshopper.Converters.Formats;
using SmartHopper.Core.Grasshopper.Utils.Internal;
using SmartHopper.Core.Types;
using SmartHopper.Infrastructure.AICall.Core.Base;
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
    public sealed class file2md : IAIToolProvider
    {
        private readonly string toolName = "file2md";
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
                    new OpenDocumentConverter(),
                    new XlsxConverter(),
                    new XlsConverter(),
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
                description: "Convert a local file (PDF, DOCX, XLS, XLSX, PPTX, ODT, ODS, ODP, HTML, CSV, JSON, XML, TXT, EML, EPUB, RTF, etc.) to Markdown text. Use this when you need to read the contents of a file that the user has mentioned or referenced. Example: file2md({ filePath: 'C:/docs/spec.pdf' }).",
                category: "Knowledge",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""filePath"": {
                            ""type"": ""string"",
                            ""description"": ""Absolute path to the file to convert.""
                        },
                        ""removeHeadersFooters"": {
                            ""type"": ""boolean"",
                            ""description"": ""Whether to attempt to remove headers and footers (PDF, DOCX). Default: true."",
                            ""default"": true
                        },
                        ""preserveFormatting"": {
                            ""type"": ""boolean"",
                            ""description"": ""Whether to preserve inline text formatting. DOCX and ODF text documents preserve colors, highlights, bold, italic, underline, and strikethrough; XLSX, ODS, and PPTX preserve bold and italic. Default: true."",
                            ""default"": true
                        },
                        ""preserveComments"": {
                            ""type"": ""boolean"",
                            ""description"": ""Whether to preserve comments in DOCX files by appending them as blockquotes after the paragraph that contains them. Default: true."",
                            ""default"": true
                        },
                        ""preserveFootnotes"": {
                            ""type"": ""boolean"",
                            ""description"": ""Whether to preserve footnotes in DOCX files. Default: true."",
                            ""default"": true
                        },
                        ""preserveEndnotes"": {
                            ""type"": ""boolean"",
                            ""description"": ""Whether to preserve endnotes in DOCX files. Default: true."",
                            ""default"": true
                        },
                        ""extractImages"": {
                            ""type"": ""boolean"",
                            ""description"": ""Whether to extract embedded images from the document as base64 data. Applies to PDF, DOCX, and PPTX. Default: false."",
                            ""default"": false
                        },
                        ""describeImages"": {
                            ""type"": ""boolean"",
                            ""description"": ""When true, uses AI to describe each extracted image and embeds the results in the markdown output. Forces extractImages=true automatically. Requires an AI provider with vision capability. Default: false."",
                            ""default"": false
                        },
                        ""imageMode"": {
                            ""type"": ""string"",
                            ""description"": ""Controls how described images appear in the markdown. 'embed' (default): embed image as base64 data URI with short AI caption as alt text. 'describe': replace image with a long AI text description. 'caption': replace image with a short AI-generated title."",
                            ""default"": ""embed""
                        },
                        ""imageDescriptionPrompt"": {
                            ""type"": ""string"",
                            ""description"": ""Custom prompt for AI image description. If omitted, a built-in detailed description prompt is used.""
                        },
                        ""HTMLreadabilityMode"": {
                            ""type"": ""string"",
                            ""enum"": [""auto"", ""smartreader"", ""heuristic"", ""off""],
                            ""description"": ""HTML main-content extraction strategy (applies to .html/.htm and HTML parts of EPUB/EML). 'auto' (default): SmartReader with heuristic fallback. 'smartreader': force SmartReader. 'heuristic': force the magic-html-inspired extractor. 'off': skip extraction."",
                            ""default"": ""auto""
                        },
                        ""includeLinks"": {
                            ""type"": ""boolean"",
                            ""description"": ""Keep hyperlinks in the Markdown output for HTML sources. Default: true."",
                            ""default"": true
                        },
                        ""includeImages"": {
                            ""type"": ""boolean"",
                            ""description"": ""Keep inline image references in the Markdown output for HTML sources. Default: true."",
                            ""default"": true
                        }
                    },
                    ""required"": [""filePath""]
                }",
                execute: this.File2MdAsync,
                mutatesCanvas: false,
                tags: new[] { "file", "knowledge", "text", "read-only" },
                outputSchema: @"{ ""type"": ""object"", ""properties"": { ""text"": { ""type"": ""string"", ""description"": ""Markdown representation of the file contents."" }, ""fileType"": { ""type"": ""string"" }, ""images"": { ""type"": ""array"" } } }",
                annotations: new AIToolAnnotations(readOnlyHint: true));
        }

        private async Task<AIReturn> File2MdAsync(AIToolCall toolCall)
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
                var args = toolInfo.GetArgumentsOrEmpty();

                string filePath = args["filePath"]?.ToString();
                if (string.IsNullOrWhiteSpace(filePath))
                {
                    output.CreateError("Missing 'filePath' parameter.");
                    return output;
                }

                // Get conversion options
                bool describeImages = args["describeImages"]?.Value<bool>() ?? false;
                string imageMode = args["imageMode"]?.ToString() ?? "embed";
                string imageDescriptionPrompt = args["imageDescriptionPrompt"]?.ToString();

                var options = new FileConversionOptions
                {
                    PreserveTableStructure = true,
                    RemoveHeadersFooters = args["removeHeadersFooters"]?.Value<bool>() ?? true,
                    PreserveFormatting = args["preserveFormatting"]?.Value<bool>() ?? true,
                    PreserveComments = args["preserveComments"]?.Value<bool>() ?? true,
                    PreserveFootnotes = args["preserveFootnotes"]?.Value<bool>() ?? true,
                    PreserveEndnotes = args["preserveEndnotes"]?.Value<bool>() ?? true,
                    PreserveHyperlinks = true,
                    PreserveMath = true,
                    ExtractImages = (args["extractImages"]?.Value<bool>() ?? false) || describeImages,
                    DetectHeadings = true,
                    MaxContentLength = 0,
                    HtmlReadabilityMode = ReadabilityModeExtensions.FromString(args["HTMLreadabilityMode"]?.ToString()),
                    IncludeLinks = args["includeLinks"]?.Value<bool>() ?? true,
                    IncludeImages = args["includeImages"]?.Value<bool>() ?? true,
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

                // Always return raw images array when images were extracted
                if (result.Images.Count > 0)
                {
                    var imagesArray = new JArray();
                    foreach (var image in result.Images)
                    {
                        imagesArray.Add(new JObject
                        {
                            ["id"] = image.Id,
                            ["mimeType"] = image.MimeType,
                            ["context"] = image.Context,
                            ["pageOrSlide"] = image.PageOrSlide,
                            ["base64Data"] = image.RawValue,
                        });
                    }

                    toolResult["images"] = imagesArray;
                    toolResult["imageCount"] = result.Images.Count;

                    // Describe images via AI and replace inline [image N] placeholders
                    if (describeImages)
                    {
                        string providerName = toolCall.Provider?.ToString();
                        if (string.IsNullOrWhiteSpace(providerName))
                        {
                            result.Warnings.Add("Image description skipped: no AI provider configured. Configure a provider or set describeImages=false.");
                        }
                        else
                        {
                            string prompt = string.IsNullOrWhiteSpace(imageDescriptionPrompt)
                                ? ImageProcessingService.GetDefaultPrompt(imageMode)
                                : imageDescriptionPrompt;

                            var items = result.Images.Select((image, i) => new ImageProcessingItem
                            {
                                Id = image.Id,
                                Context = image.Context,
                                MimeType = image.MimeType,
                                Base64Data = image.RawValue,
                                AltText = image.Context,
                                Placeholder = $"[image {i + 1}]",
                            }).ToList();

                            result.MarkdownContent = await ImageProcessingService.ProcessMarkdownImagesAsync(
                                result.MarkdownContent,
                                items,
                                imageMode,
                                toolCall,
                                prompt: prompt).ConfigureAwait(false);
                            toolResult["content"] = result.MarkdownContent;
                        }
                    }
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
                Debug.WriteLine($"[File2Md] Error in File2MdAsync: {ex.Message}");
                output.CreateError($"Error: {ex.Message}");
                return output;
            }
        }

    }
}
