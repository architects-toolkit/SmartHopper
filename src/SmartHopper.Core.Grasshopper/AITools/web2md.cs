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
using System.Security.Cryptography;
using System.Threading.Tasks;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Newtonsoft.Json.Linq;
using SmartHopper.Core.Grasshopper.Converters;
using SmartHopper.Core.Grasshopper.Converters.Formats;
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.Infrastructure.AITools;
using SmartHopper.Infrastructure.Networking;
using SmartHopper.ProviderSdk.AICall.Core.Base;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AICall.Core.Returns;
using SmartHopper.ProviderSdk.Documents;

using SmartHopper.ProviderSdk.Logging;
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
                category: "Knowledge.Web",
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
                        },
                        ""extractImages"": {
                            ""type"": ""boolean"",
                            ""description"": ""Preserve visual evidence. For web pages this downloads referenced images as typed visual evidence. Default: true. Set false for text-only conversion without downloads."",
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
                        ""imageDescriptionPrompt"": {
                            ""type"": ""string"",
                            ""description"": ""Custom prompt for AI image description. If omitted, a built-in detailed description prompt is used.""
                        }
                    },
                    ""required"": [""url""]
                }",
                execute: this.Web2MdAsync,
                mutatesCanvas: false,
                tags: new[] { "web", "knowledge", "text", "read-only", "external" },
                outputSchema: @"{ ""type"": ""object"", ""properties"": { ""content"": { ""type"": ""string"", ""description"": ""Markdown representation of the web page."" }, ""source"": { ""type"": ""string"", ""description"": ""The URL that was fetched."" }, ""retrievedAt"": { ""type"": ""string"", ""format"": ""date-time"", ""description"": ""UTC ISO 8601 timestamp when the page was retrieved."" }, ""metadata"": { ""type"": ""object"" }, ""warnings"": { ""type"": ""array"", ""items"": { ""type"": ""string"" } } } }",
                annotations: new AIToolAnnotations(readOnlyHint: true, openWorldHint: true),
                activityLabel: "fetching web page",
                resultLabel: "fetched web page");
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

                bool describeImages = args["describeImages"]?.Value<bool>() ?? false;
                bool? consumerSupportsImages = args.ContainsKey("consumerSupportsImages")
                    ? args["consumerSupportsImages"]?.Value<bool?>()
                    : null;
                string imageDescriptionPrompt = args["imageDescriptionPrompt"]?.ToString();
                bool extractImages = args["extractImages"]?.Value<bool>() ?? true;
                bool includeImages = args["includeImages"]?.Value<bool>() ?? true;

                // Actionable contradictions, validated before any conversion work.
                var explicitAi = describeImages || consumerSupportsImages == false;
                if (explicitAi && !extractImages)
                {
                    output.CreateError("extractImages=false contradicts an explicit AI visual comprehension request (describeImages or consumerSupportsImages=false).");
                    return output;
                }

                if (explicitAi && !includeImages)
                {
                    output.CreateError("includeImages=false contradicts an explicit AI visual comprehension request (describeImages or consumerSupportsImages=false).");
                    return output;
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
                    IncludeImages = includeImages,
                    ExtractImages = extractImages,
                    CancellationToken = toolCall.CancellationToken,
                };

                // Convert the web page — article Markdown is the canonical source text.
                var converter = GetUrlConverter();
                var result = await converter.ConvertAsync(url, options).ConfigureAwait(false);

                if (!result.IsSuccess)
                {
                    var errorMessage = result.Warnings.Count > 0
                        ? string.Join("; ", result.Warnings)
                        : "Conversion failed.";

                    output.CreateError($"[{result.FailureReason}] {errorMessage}");
                    return output;
                }

                // Page-less document identity is bound to article URL + source
                // Markdown BEFORE images materialize so every asset shares one
                // coherent DocumentId — never two differing identities.
                var documentId = "sha256-" + Convert.ToHexString(
                    SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(url + "\n" + (result.MarkdownContent ?? string.Empty))))
                    .ToLowerInvariant();

                // Materialize referenced article images through the authorized host
                // scope — never via a raw fetch and never as base64 in JSON/Markdown.
                var typedImages = new List<ToolResultImage>();
                var failed = 0;
                var encodedTotal = 0L;
                if (options.IncludeImages && extractImages && !string.IsNullOrEmpty(result.MarkdownContent))
                {
                    var pageUri = new Uri(url);
                    var ast = Markdig.Markdown.Parse(result.MarkdownContent);
                    var refs = ast.Descendants()
                        .OfType<LinkInline>()
                        .Where(l => l.IsImage && !string.IsNullOrEmpty(l.Url))
                        .Select(l =>
                        {
                            Uri.TryCreate(pageUri, l.Url, out var resolved);
                            var alt = string.Concat(l.Descendants().OfType<LiteralInline>().Select(t => t.Content.ToString()));
                            return (resolved, alt, span: l.UrlSpan);
                        })
                        .ToList();

                    // Unresolvable image references are explicit coverage gaps,
                    // never silent filters.
                    var invalid = refs.Count(r => r.resolved == null);
                    if (invalid > 0)
                    {
                        failed += invalid;
                        result.Warnings.Add($"{invalid} image reference(s) could not be resolved to a valid URI and were not materialized.");
                    }

                    var deduped = refs
                        .Where(r => r.resolved != null)
                        .GroupBy(r => r.resolved!.AbsoluteUri)
                        .Select(g => g.First())
                        .ToList();

                    // One stable smarthopper-asset:img-N identity per deduped source.
                    var assetIds = new Dictionary<string, string>(StringComparer.Ordinal);
                    for (var i = 0; i < deduped.Count; i++)
                    {
                        assetIds[deduped[i].resolved!.AbsoluteUri] = $"img-{i + 1}";
                    }

                    // Inline data: payloads must never remain in the projected
                    // Markdown or any JSON provenance — replace their URL spans with
                    // stable asset links (right-to-left keeps earlier spans valid).
                    var replacements = new List<(int Start, int Length, string Url)>();
                    foreach (var r in refs)
                    {
                        if (r.resolved == null || r.span.Start < 0 || r.span.End < r.span.Start)
                        {
                            continue;
                        }

                        if (r.resolved.Scheme.Equals("data", StringComparison.OrdinalIgnoreCase)
                            && assetIds.TryGetValue(r.resolved.AbsoluteUri, out var inlineId))
                        {
                            replacements.Add((r.span.Start, r.span.End - r.span.Start + 1, $"smarthopper-asset:{inlineId}"));
                        }
                    }

                    // Reference definitions carrying data: targets are sanitized too;
                    // unreferenced definitions still cannot leak payload bytes.
                    foreach (var def in ast.Descendants().OfType<LinkReferenceDefinition>())
                    {
                        if (string.IsNullOrEmpty(def.Url) || def.UrlSpan.Start < 0 || def.UrlSpan.End < def.UrlSpan.Start
                            || !Uri.TryCreate(def.Url, UriKind.Absolute, out var defUri)
                            || !defUri.Scheme.Equals("data", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        var defId = assetIds.TryGetValue(defUri.AbsoluteUri, out var existing)
                            ? existing
                            : $"unreferenced-{replacements.Count + 1}";
                        replacements.Add((def.UrlSpan.Start, def.UrlSpan.End - def.UrlSpan.Start + 1, $"smarthopper-asset:{defId}"));
                    }

                    if (replacements.Count > 0)
                    {
                        var builder = new System.Text.StringBuilder(result.MarkdownContent);
                        foreach (var rep in replacements.OrderByDescending(r => r.Start))
                        {
                            builder.Remove(rep.Start, rep.Length);
                            builder.Insert(rep.Start, rep.Url);
                        }

                        result.MarkdownContent = builder.ToString();
                    }

                    var iterated = 0;
                    var withinLimit = Math.Min(deduped.Count, 32);
                    foreach (var (resolved, alt, _) in deduped.Take(32))
                    {
                        iterated++;
                        var assetId = assetIds[resolved.AbsoluteUri];
                        var isData = resolved.Scheme.Equals("data", StringComparison.OrdinalIgnoreCase);
                        if (!isData && resolved.Scheme != Uri.UriSchemeHttp && resolved.Scheme != Uri.UriSchemeHttps)
                        {
                            failed++;
                            result.Warnings.Add($"Image source '{(resolved.OriginalString.Length > 80 ? resolved.OriginalString.Substring(0, 80) + "…" : resolved.OriginalString)}' uses an unsupported scheme and was not materialized.");
                            continue;
                        }

                        try
                        {
                            var image = await ExternalImageMaterializer.MaterializeAsync(
                                new AIInteractionImage { ImageUrl = resolved },
                                toolCall.CancellationToken).ConfigureAwait(false);

                            // Reject before storing when this asset would cross the
                            // 64 MiB aggregate encoded budget — and stop further
                            // downloads rather than crossing on a later asset.
                            var candidateLength = image.ImageData?.Length ?? 0;
                            if (encodedTotal + candidateLength > 64L * 1024 * 1024)
                            {
                                failed++;
                                result.Warnings.Add($"Image '{assetId}' would exceed the 64 MiB aggregate encoded budget; remaining image sources were not downloaded.");
                                break;
                            }

                            encodedTotal += candidateLength;
                            typedImages.Add(new ToolResultImage
                            {
                                ImageData = image.ImageData,
                                MimeType = image.MimeType,
                                SendToModel = true,
                                SourceReference = new SourceContentReference
                                {
                                    DocumentId = documentId,
                                    // data: payloads are never copied into provenance;
                                    // HTTP(S) sources keep their canonical image URL.
                                    SourceName = isData ? url : resolved.AbsoluteUri,
                                    AssetId = assetId,
                                    Caption = alt,
                                    ContextText = result.MarkdownContent,
                                    Kind = SourceContentKind.Image,
                                },
                            });
                        }
                        catch (Exception ex) when (ex is not OperationCanceledException and not UnauthorizedAccessException)
                        {
                            failed++;
                            result.Warnings.Add($"Image '{assetId}' could not be materialized: {ex.Message}");
                        }
                    }

                    // Downloads stopped at the encoded budget never silently resume.
                    var undownloaded = withinLimit - iterated;
                    if (undownloaded > 0)
                    {
                        failed += undownloaded;
                        result.Warnings.Add($"{undownloaded} referenced image source(s) were not downloaded because the 64 MiB aggregate encoded budget was reached.");
                    }

                    // Truncated references are explicit coverage gaps, not silent drops.
                    var omitted = deduped.Count - withinLimit;
                    if (omitted > 0)
                    {
                        failed += omitted;
                        result.Warnings.Add($"{omitted} referenced image source(s) exceeded the 32-source limit and were not materialized.");
                    }
                }

                // Extraction disabled while the article references images is an
                // explicit coverage gap — never a silent complete.
                var extractionSkipped = !extractImages && !string.IsNullOrEmpty(result.MarkdownContent)
                    && Markdig.Markdown.Parse(result.MarkdownContent).Descendants().OfType<LinkInline>().Any(l => l.IsImage);
                if (extractionSkipped)
                {
                    result.Warnings.Add("Article references images but extractImages=false; visual evidence is incomplete.");
                }

                // Page-less document evidence: web coverage is completeness of the
                // referenced image set, never invented page numbers.
                result.Document = new DocumentEvidence
                {
                    DocumentId = documentId,
                    SourceName = url,
                    Format = "web",
                    Metadata = new Dictionary<string, string>
                    {
                        ["url"] = url,
                        ["format"] = result.DetectedFormat ?? string.Empty,
                        ["retrievedAt"] = DateTime.UtcNow.ToString("O"),
                    },
                    Images = typedImages,
                    Warnings = result.Warnings.ToList(),
                    Coverage = new DocumentCoverage
                    {
                        IsComplete = failed == 0 && !extractionSkipped,
                    },
                };
                result.Metadata["retrievedAt"] = DateTime.UtcNow.ToString("O");

                return await ConversionToolResultBuilder.BuildAsync(
                    result,
                    url,
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
                Logger.For<web2md>().Exception(ex, $"[Web2Md] Error in Web2MdAsync: {ex.Message}");
                output.CreateError($"Error: {ex.Message}");
                return output;
            }
        }

    }
}
