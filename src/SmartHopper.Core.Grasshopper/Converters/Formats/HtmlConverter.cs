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

/*
 * Portions of this code inspired by:
 * https://github.com/opendatalab/magic-html
 * Apache License 2.0
 * Copyright (c) OpenDataLab
 *
 * Key concepts adapted:
 * - Content scoring by text density and link density
 * - Boilerplate removal via tag/class/ID pattern matching
 * - Readability scoring algorithm for main content extraction
 * - Semantic container prioritization (article, main tags)
 *
 * Uses HtmlAgilityPack (MIT) for HTML parsing:
 * https://github.com/zzzprojects/html-agility-pack
 */

/*
 * Portions of this code inspired by:
 * https://github.com/ariesdevil/markdown-clipper
 * Apache License 2.0
 * Copyright (c) ariesdevil
 *
 * Key concepts adapted:
 * - Readability (main-content extraction) + Turndown (HTML->Markdown rule engine)
 *   two-stage pipeline
 *
 * Uses ReverseMarkdown (Turndown-equivalent .NET rule engine):
 * https://github.com/mysticmind/reversemarkdown-net
 * MIT License
 *
 * Uses SmartReader (Mozilla Readability .NET port):
 * https://github.com/Strumenta/SmartReader
 * Apache License 2.0
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HtmlAgilityPack;

namespace SmartHopper.Core.Grasshopper.Converters.Formats
{
    /// <summary>
    /// Converter for HTML files (.html, .htm).
    /// Pipeline:
    /// 1. Main-content extraction via a pluggable <see cref="IReadabilityExtractor"/>
    ///    (SmartReader by default, heuristic fallback inspired by magic-html).
    /// 2. HTML -> Markdown via ReverseMarkdown (Turndown-equivalent rule engine).
    /// </summary>
    public sealed class HtmlConverter : IFileConverter
    {
        private static readonly Regex MultiBlankLineRegex = new Regex(@"(\r?\n){3,}", RegexOptions.Compiled);
        private static readonly Regex LangClassRegex = new Regex(@"(?:^|\s)(?:language|lang|highlight-source)-([A-Za-z0-9+#._-]+)", RegexOptions.Compiled);

        public IEnumerable<string> SupportedExtensions => new[] { ".html", ".htm" };

        public async Task<FileConversionResult> ConvertAsync(string filePath, FileConversionOptions options)
        {
            try
            {
                var html = await File.ReadAllTextAsync(filePath, Encoding.UTF8).ConfigureAwait(false);
                return await ConvertHtmlStringAsync(html, options).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                return FileConversionResult.Failure("html", $"Failed to convert HTML: {ex.Message}");
            }
        }

        /// <summary>
        /// Converts an HTML string directly to Markdown without file I/O.
        /// </summary>
        public Task<FileConversionResult> ConvertHtmlStringAsync(string html, FileConversionOptions options)
        {
            if (options == null)
            {
                options = new FileConversionOptions();
            }

            try
            {
                // Stage 1: Main-content extraction
                var extraction = ExtractMainContent(html, options);
                if (extraction == null || string.IsNullOrWhiteSpace(extraction.CleanHtml))
                {
                    return Task.FromResult(FileConversionResult.Failure("html", "Failed to extract main content from HTML."));
                }

                // Stage 2: HTML -> Markdown conversion
                var preparedHtml = PreProcessHtml(extraction.CleanHtml, options);
                var markdown = ConvertHtmlToMarkdown(preparedHtml, options);
                markdown = PostProcessMarkdown(markdown);

                var result = FileConversionResult.Success(markdown, "html");

                if (!string.IsNullOrWhiteSpace(extraction.Title))
                {
                    result.Metadata["title"] = extraction.Title!.Trim();
                }
                else
                {
                    // Fallback: raw <title>
                    var doc = new HtmlDocument();
                    doc.LoadHtml(html);
                    var titleNode = doc.DocumentNode.SelectSingleNode("//title");
                    if (titleNode != null && !string.IsNullOrWhiteSpace(titleNode.InnerText))
                    {
                        result.Metadata["title"] = titleNode.InnerText.Trim();
                    }
                }

                if (!string.IsNullOrWhiteSpace(extraction.Byline))
                {
                    result.Metadata["byline"] = extraction.Byline!.Trim();
                }

                if (!string.IsNullOrWhiteSpace(extraction.Excerpt))
                {
                    result.Metadata["excerpt"] = extraction.Excerpt!.Trim();
                }

                result.Metadata["readability_extractor"] = extraction.ExtractorName;

                return Task.FromResult(result);
            }
            catch (Exception ex)
            {
                return Task.FromResult(FileConversionResult.Failure("html", $"Failed to convert HTML: {ex.Message}"));
            }
        }

        /// <summary>
        /// Runs the configured readability pipeline:
        /// Auto: SmartReader first, heuristic if not confident.
        /// SmartReader / Heuristic: force that extractor only.
        /// Off: return the full body as-is.
        /// </summary>
        private static ReadabilityResult? ExtractMainContent(string html, FileConversionOptions options)
        {
            var baseUrl = options.BaseUrl;

            if (options.HtmlReadabilityMode == ReadabilityMode.Off)
            {
                var doc = new HtmlDocument();
                doc.LoadHtml(html);
                var body = doc.DocumentNode.SelectSingleNode("//body") ?? doc.DocumentNode;
                return new ReadabilityResult
                {
                    CleanHtml = body.InnerHtml ?? string.Empty,
                    ExtractorName = "off",
                    Confidence = 0,
                };
            }

            IReadabilityExtractor smart = new SmartReaderExtractor();
            IReadabilityExtractor heuristic = new HeuristicExtractor();

            switch (options.HtmlReadabilityMode)
            {
                case ReadabilityMode.SmartReader:
                    return smart.Extract(html, baseUrl) ?? heuristic.Extract(html, baseUrl);

                case ReadabilityMode.Heuristic:
                    return heuristic.Extract(html, baseUrl);

                case ReadabilityMode.Auto:
                default:
                    var smartResult = smart.Extract(html, baseUrl);
                    if (smartResult != null && smartResult.Confidence >= 0.2 && !string.IsNullOrWhiteSpace(smartResult.CleanHtml))
                    {
                        return smartResult;
                    }

                    return heuristic.Extract(html, baseUrl) ?? smartResult;
            }
        }

        /// <summary>
        /// Applies per-options pre-processing on the cleaned HTML:
        /// - Injects &lt;base&gt; so ReverseMarkdown's SmartHrefHandling can resolve relative URLs.
        /// - Drops images when <see cref="FileConversionOptions.IncludeImages"/> is false.
        /// - Drops links (keeping their text) when <see cref="FileConversionOptions.IncludeLinks"/> is false.
        /// - Propagates language class hints onto &lt;pre&gt; for fenced-code language detection.
        /// </summary>
        private static string PreProcessHtml(string html, FileConversionOptions options)
        {
            var doc = new HtmlDocument { OptionOutputOriginalCase = true };
            doc.LoadHtml(html);

            if (!string.IsNullOrWhiteSpace(options.BaseUrl) &&
                Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var baseUri))
            {
                ResolveRelativeUrls(doc, baseUri);
            }

            if (!options.IncludeImages)
            {
                var imgs = doc.DocumentNode.SelectNodes("//img");
                if (imgs != null)
                {
                    foreach (var img in imgs.ToList())
                    {
                        img.Remove();
                    }
                }
            }

            if (!options.IncludeLinks)
            {
                var anchors = doc.DocumentNode.SelectNodes("//a");
                if (anchors != null)
                {
                    foreach (var a in anchors.ToList())
                    {
                        var replacement = doc.CreateTextNode(a.InnerText ?? string.Empty);
                        a.ParentNode?.ReplaceChild(replacement, a);
                    }
                }
            }

            PropagateCodeLanguageClasses(doc);

            return doc.DocumentNode.OuterHtml;
        }

        private static void ResolveRelativeUrls(HtmlDocument doc, Uri baseUri)
        {
            foreach (var attrName in new[] { "href", "src" })
            {
                var nodes = doc.DocumentNode.SelectNodes($"//*[@{attrName}]");
                if (nodes == null)
                {
                    continue;
                }

                foreach (var node in nodes)
                {
                    var value = node.GetAttributeValue(attrName, string.Empty);
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        continue;
                    }

                    if (value.StartsWith("#", StringComparison.Ordinal) ||
                        value.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase) ||
                        value.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) ||
                        value.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (Uri.TryCreate(baseUri, value, out var absolute))
                    {
                        node.SetAttributeValue(attrName, absolute.ToString());
                    }
                }
            }
        }

        /// <summary>
        /// Promotes <c>language-*</c> / <c>lang-*</c> classes from <c>&lt;code&gt;</c>
        /// up to their parent <c>&lt;pre&gt;</c>, so ReverseMarkdown emits fenced code
        /// blocks with a language tag.
        /// </summary>
        private static void PropagateCodeLanguageClasses(HtmlDocument doc)
        {
            var pres = doc.DocumentNode.SelectNodes("//pre");
            if (pres == null)
            {
                return;
            }

            foreach (var pre in pres)
            {
                var preClass = pre.GetAttributeValue("class", string.Empty);
                if (ExtractLanguageFromClass(preClass) != null)
                {
                    continue;
                }

                var code = pre.SelectSingleNode(".//code");
                if (code == null)
                {
                    continue;
                }

                var codeClass = code.GetAttributeValue("class", string.Empty);
                var lang = ExtractLanguageFromClass(codeClass);
                if (lang != null)
                {
                    var merged = string.IsNullOrWhiteSpace(preClass)
                        ? "language-" + lang
                        : preClass + " language-" + lang;
                    pre.SetAttributeValue("class", merged);
                }
            }
        }

        private static string? ExtractLanguageFromClass(string classAttr)
        {
            if (string.IsNullOrWhiteSpace(classAttr))
            {
                return null;
            }

            var match = LangClassRegex.Match(classAttr);
            return match.Success ? match.Groups[1].Value.ToLowerInvariant() : null;
        }

        /// <summary>
        /// Delegates HTML -> Markdown to ReverseMarkdown with GitHub-flavored, table-aware settings.
        /// </summary>
        private static string ConvertHtmlToMarkdown(string html, FileConversionOptions options)
        {
            var config = new ReverseMarkdown.Config
            {
                GithubFlavored = true,
                RemoveComments = true,
                SmartHrefHandling = true,
                UnknownTags = ReverseMarkdown.Config.UnknownTagsOption.Bypass,
                TableWithoutHeaderRowHandling = ReverseMarkdown.Config.TableWithoutHeaderRowHandlingOption.EmptyRow,
            };

            var converter = new ReverseMarkdown.Converter(config);
            return converter.Convert(html) ?? string.Empty;
        }

        /// <summary>
        /// Cleans up Markdown produced by ReverseMarkdown: trims trailing whitespace and collapses
        /// 3+ consecutive newlines into exactly two.
        /// </summary>
        private static string PostProcessMarkdown(string markdown)
        {
            if (string.IsNullOrEmpty(markdown))
            {
                return string.Empty;
            }

            var normalized = markdown.Replace("\r\n", "\n");
            normalized = MultiBlankLineRegex.Replace(normalized, "\n\n");
            return normalized.Trim();
        }
    }
}
