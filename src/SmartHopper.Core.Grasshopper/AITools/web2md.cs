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
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SmartHopper.Core.Grasshopper.Converters;
using SmartHopper.Core.Grasshopper.Converters.Formats;
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.Infrastructure.AITools;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AICall.Core.Returns;

namespace SmartHopper.Core.Grasshopper.AITools
{
    /// <summary>
    /// Provides AI tool for converting web pages (URLs) to Markdown.
    /// Supports Wikipedia, GitHub, GitLab, Discourse, Stack Exchange, and generic HTML pages.
    /// </summary>
    public sealed class web2md : IAIToolProvider
    {
        private readonly string toolName = "web2md";
        private static UrlConverter? urlConverter;

        /// <summary>
        /// Gets or creates the URL converter.
        /// </summary>
        private static UrlConverter GetUrlConverter()
        {
            if (urlConverter == null)
            {
                urlConverter = new UrlConverter();
            }

            return urlConverter;
        }

        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: this.toolName,
                description: "Convert a web page (URL) to Markdown text. Supports Wikipedia/Wikimedia, Discourse forums, GitHub/GitLab files, Stack Exchange questions, and generic webpages. Respects robots.txt. Use this when you know the URL and need to retrieve knowledge from the web or read the contents of a web page. Example: web2md({ url: 'https://en.wikipedia.org/wiki/Tensile_structure' }).",
                category: "Knowledge",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""url"": {
                            ""type"": ""string"",
                            ""format"": ""uri"",
                            ""description"": ""The URL of the webpage to convert to Markdown.""
                        },
                        ""HTMLreadabilityMode"": {
                            ""type"": ""string"",
                            ""enum"": [""auto"", ""smartreader"", ""heuristic"", ""off""],
                            ""description"": ""HTML main-content extraction strategy. 'auto' (default): SmartReader (Mozilla Readability port) with heuristic fallback. 'smartreader': force SmartReader. 'heuristic': force the built-in magic-html-inspired extractor. 'off': skip extraction and convert the full document body."",
                            ""default"": ""auto""
                        },
                        ""includeLinks"": {
                            ""type"": ""boolean"",
                            ""description"": ""Keep hyperlinks in the Markdown output. When false, link text is preserved but URLs are dropped. Default: true."",
                            ""default"": true
                        },
                        ""includeImages"": {
                            ""type"": ""boolean"",
                            ""description"": ""Keep inline image references in the Markdown output. Default: true."",
                            ""default"": true
                        }
                    },
                    ""required"": [""url""]
                }",
                execute: this.Web2MdAsync);
        }

        private async Task<AIReturn> Web2MdAsync(AIToolCall toolCall)
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

                string url = args["url"]?.ToString();
                if (string.IsNullOrWhiteSpace(url))
                {
                    output.CreateError($"[{FileConversionFailureReason.InvalidInput}] Missing 'url' parameter.");
                    return output;
                }

                string imageMode = args["imageMode"]?.ToString()?.ToLowerInvariant() ?? "link";
                if (imageMode != "link" && imageMode != "embed" && imageMode != "describe" && imageMode != "caption")
                {
                    imageMode = "link";
                }

                // Get conversion options
                var options = new FileConversionOptions
                {
                    PreserveTableStructure = true,
                    RemoveHeadersFooters = false, // Not applicable for web pages
                    DetectHeadings = true,
                    MaxContentLength = 0,
                    HtmlReadabilityMode = ReadabilityModeExtensions.FromString(args["HTMLreadabilityMode"]?.ToString()),
                    IncludeLinks = args["includeLinks"]?.Value<bool>() ?? true,
                    IncludeImages = args["includeImages"]?.Value<bool>() ?? true,
                };

                // Convert the URL
                var converter = GetUrlConverter();
                var result = await converter.ConvertAsync(url, options).ConfigureAwait(false);

                if (!result.IsSuccess)
                {
                    var errorMessage = result.Warnings.Count > 0
                        ? string.Join("; ", result.Warnings)
                        : "Conversion failed.";

                    // Prefix with the classified failure reason so callers/agents can distinguish
                    // failure shapes (invalid URL, login wall, bot challenge, oversized page, empty
                    // content, etc.) without having to parse free-text messages.
                    output.CreateError($"[{result.FailureReason}] {errorMessage}");
                    return output;
                }

                string markdownContent = result.MarkdownContent;

                // Process images according to the requested mode (link is the default / no-op).
                if (imageMode != "link" && options.IncludeImages)
                {
                    string providerName = toolCall.Provider?.ToString();
                    if (string.IsNullOrWhiteSpace(providerName))
                    {
                        result.Warnings.Add("Image processing skipped: no AI provider configured. Configure a provider or set imageMode='link'.");
                    }
                    else
                    {
                        markdownContent = await ProcessWebImagesAsync(markdownContent, imageMode, toolCall).ConfigureAwait(false);
                    }
                }

                // Build result
                var toolResult = new JObject
                {
                    ["content"] = markdownContent,
                    ["source"] = url,
                    ["retrievedAt"] = DateTime.UtcNow.ToString("O"),
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
                Debug.WriteLine($"[Web2Md] Error in Web2MdAsync: {ex.Message}");
                output.CreateError($"Error: {ex.Message}");
                return output;
            }
        }

    }
}
