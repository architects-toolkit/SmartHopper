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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SmartHopper.Core.Grasshopper.Converters;
using SmartHopper.Core.Grasshopper.Converters.Formats;
using SmartHopper.Core.Grasshopper.Utils.Internal;
using SmartHopper.Core.Types;
using SmartHopper.DocumentProcessing;
using SmartHopper.Infrastructure.AICall.Fallback;
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.Infrastructure.AITools;
using SmartHopper.ProviderSdk.AICall.Core.Base;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AICall.Core.Returns;
using SmartHopper.ProviderSdk.Diagnostics;

using SmartHopper.ProviderSdk.Logging;
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
                description: "Read a local document as extracted Markdown and visual evidence. PDF documents include rendered pages with page references, preserving charts, diagrams, scans and layout. No AI call is made unless describeImages or consumerSupportsImages=false is explicitly requested. Use pages and region for bounded page selection and high-resolution details.",
                category: "Knowledge.Files",
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
                            ""description"": ""Preserve visual evidence. For PDF documents this includes rendered pages and extracted embedded assets. Default: true. Set false for local text-only extraction without rendering."",
                            ""default"": true
                        },
                        ""describeImages"": {
                            ""type"": ""boolean"",
                            ""description"": ""Explicitly use a configured vision helper to add contextual textual visual evidence while retaining source text and images. This may incur provider costs. Default: false."",
                            ""default"": false
                        },
                        ""consumerSupportsImages"": {
                            ""type"": [""boolean"", ""null""],
                            ""description"": ""Whether the external consuming model accepts image input. False explicitly requests vision-helper textual evidence; true preserves native images. If omitted, return original evidence and report that the client must check its model capabilities. This does not infer capabilities from MCP image transport support.""
                        },
                        ""pages"": {
                            ""type"": ""array"",
                            ""items"": { ""type"": ""integer"", ""minimum"": 1 },
                            ""minItems"": 1,
                            ""uniqueItems"": true,
                            ""description"": ""Optional unique 1-based PDF page numbers. Omit to select the whole document. Empty, duplicate, fractional and out-of-range values are invalid. Render at most 32 pages per call.""
                        },
                        ""region"": {
                            ""type"": ""object"",
                            ""properties"": {
                                ""page"": { ""type"": ""integer"", ""minimum"": 1 },
                                ""x"": { ""type"": ""number"" },
                                ""y"": { ""type"": ""number"" },
                                ""width"": { ""type"": ""number"", ""exclusiveMinimum"": 0 },
                                ""height"": { ""type"": ""number"", ""exclusiveMinimum"": 0 }
                            },
                            ""required"": [""page"", ""x"", ""y"", ""width"", ""height""],
                            ""description"": ""Optional high-resolution PDF detail region: page is a selected 1-based page number; x, y, width and height are normalized top-left coordinates within that visible page. The region must lie entirely inside the page.""
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
                outputSchema: @"{ ""type"": ""object"", ""properties"": { ""content"": { ""type"": ""string"" }, ""originalFormat"": { ""type"": ""string"" }, ""pages"": { ""type"": ""array"" }, ""images"": { ""type"": ""array"" }, ""coverage"": { ""type"": ""object"" }, ""warnings"": { ""type"": ""array"" } } }",
                annotations: new AIToolAnnotations(readOnlyHint: true, openWorldHint: true),
                activityLabel: "converting file to markdown",
                resultLabel: "converted file to markdown");
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
                bool? consumerSupportsImages = args.ContainsKey("consumerSupportsImages")
                    ? args["consumerSupportsImages"]?.Value<bool?>()
                    : null;
                string imageDescriptionPrompt = args["imageDescriptionPrompt"]?.ToString();
                var isPdf = string.Equals(Path.GetExtension(filePath), ".pdf", StringComparison.OrdinalIgnoreCase);

                // Structured page/region options — invalid on non-PDF sources.
                if (!DocumentSelectionJsonParser.TryParsePages(args["pages"], out var pages, out var pagesError))
                {
                    output.CreateError(pagesError);
                    return output;
                }

                if (!DocumentSelectionJsonParser.TryParseRegion(args["region"], out var region, out var regionError))
                {
                    output.CreateError(regionError);
                    return output;
                }

                if (!isPdf && (pages != null || region != null))
                {
                    output.CreateError("'pages' and 'region' are only valid for PDF sources.");
                    return output;
                }

                // Actionable contradiction, validated before any conversion work.
                var explicitAi = describeImages || consumerSupportsImages == false;
                if (explicitAi && args["extractImages"]?.Value<bool>() == false)
                {
                    output.CreateError("extractImages=false contradicts an explicit AI visual comprehension request (describeImages or consumerSupportsImages=false).");
                    return output;
                }

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
                    ExtractImages = args["extractImages"]?.Value<bool>() ?? true,
                    DetectHeadings = true,
                    MaxContentLength = 0,
                    HtmlReadabilityMode = ReadabilityModeExtensions.FromString(args["HTMLreadabilityMode"]?.ToString()),
                    IncludeLinks = args["includeLinks"]?.Value<bool>() ?? true,
                    IncludeImages = args["includeImages"]?.Value<bool>() ?? true,
                    Pages = pages,
                    Region = region,
                    CancellationToken = toolCall.CancellationToken,
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

                return await ConversionToolResultBuilder.BuildAsync(
                    result,
                    filePath,
                    toolCall,
                    toolInfo,
                    describeImages,
                    consumerSupportsImages,
                    imageDescriptionPrompt,
                    toolCall.CancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (UnauthorizedAccessException)
            {
                // Consent denials are a refusal, not a fetch failure — let them
                // propagate so the tool reports an explicit runtime error.
                throw;
            }
            catch (Exception ex)
            {
                Logger.For<file2md>().Exception(ex, $"[File2Md] Error in File2MdAsync: {ex.Message}");
                output.CreateError($"Error: {ex.Message}");
                return output;
            }
        }

    }
}
