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
 * Uses HtmlAgilityPack for HTML parsing:
 * https://github.com/zzzprojects/html-agility-pack
 * MIT License
 */

using System;
using System.Collections.Generic;
using System.Globalization;
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
    /// Uses magic-html-inspired readability scoring to extract main content.
    /// </summary>
    public sealed class HtmlConverter : IFileConverter
    {
        private static readonly Regex HorizontalWhitespaceRegex = new Regex(@"[ \t]+", RegexOptions.Compiled);

        public IEnumerable<string> SupportedExtensions => new[] { ".html", ".htm" };

        public async Task<FileConversionResult> ConvertAsync(string filePath, FileConversionOptions options)
        {
            try
            {
                var html = await File.ReadAllTextAsync(filePath, Encoding.UTF8).ConfigureAwait(false);

                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                // Extract main content using readability scoring
                var mainContent = HtmlReadabilityHelper.ExtractMainContent(doc);
                if (mainContent == null)
                {
                    return FileConversionResult.Failure("html", "Failed to extract main content from HTML.");
                }

                // Convert the cleaned HTML to Markdown
                var markdown = ConvertNodeToMarkdown(mainContent);

                var result = FileConversionResult.Success(markdown, "html");

                // Try to extract title from <title> tag
                var titleNode = doc.DocumentNode.SelectSingleNode("//title");
                if (titleNode != null && !string.IsNullOrWhiteSpace(titleNode.InnerText))
                {
                    result.Metadata["title"] = titleNode.InnerText.Trim();
                }

                return result;
            }
            catch (Exception ex)
            {
                return FileConversionResult.Failure("html", $"Failed to convert HTML: {ex.Message}");
            }
        }

        /// <summary>
        /// Converts an HTML node to Markdown.
        /// </summary>
        private static string ConvertNodeToMarkdown(HtmlNode node)
        {
            if (node == null)
            {
                return string.Empty;
            }

            // Convert links to Markdown format
            var links = node.SelectNodes(".//a[@href]");
            if (links != null)
            {
                foreach (var link in links.ToList())
                {
                    var href = link.GetAttributeValue("href", string.Empty);
                    var linkText = link.InnerText.Trim();
                    if (string.IsNullOrWhiteSpace(linkText))
                    {
                        link.Remove();
                        continue;
                    }

                    var md = $"[{linkText}]({href})";
                    var replacement = HtmlNode.CreateNode(md);
                    link.ParentNode?.ReplaceChild(replacement, link);
                }
            }

            // Convert headings to Markdown format
            var headingNodes = node.SelectNodes(".//h1 | .//h2 | .//h3 | .//h4 | .//h5 | .//h6");
            if (headingNodes != null)
            {
                foreach (var heading in headingNodes.ToList())
                {
                    int level = int.Parse(heading.Name.Substring(1), NumberStyles.Integer, CultureInfo.InvariantCulture);
                    string headingText = heading.InnerText.Trim();
                    string mdHeading = new string('#', level) + " " + headingText + Environment.NewLine + Environment.NewLine;
                    var mdNode = HtmlNode.CreateNode(mdHeading);
                    heading.ParentNode?.ReplaceChild(mdNode, heading);
                }
            }

            // Convert paragraphs to Markdown format
            var paragraphNodes = node.SelectNodes(".//p");
            if (paragraphNodes != null)
            {
                foreach (var paragraph in paragraphNodes.ToList())
                {
                    var paragraphText = paragraph.InnerText.Trim();
                    if (string.IsNullOrWhiteSpace(paragraphText))
                    {
                        paragraph.Remove();
                        continue;
                    }

                    var mdParagraph = HtmlNode.CreateNode(paragraphText + Environment.NewLine + Environment.NewLine);
                    paragraph.ParentNode?.ReplaceChild(mdParagraph, paragraph);
                }
            }

            // Convert list items
            var listItems = node.SelectNodes(".//li");
            if (listItems != null)
            {
                foreach (var li in listItems.ToList())
                {
                    var liText = li.InnerText.Trim();
                    if (string.IsNullOrWhiteSpace(liText))
                    {
                        li.Remove();
                        continue;
                    }

                    // Check if parent is ol or ul
                    var parentList = li.ParentNode;
                    string prefix = "- ";
                    if (parentList != null && parentList.Name.Equals("ol", StringComparison.OrdinalIgnoreCase))
                    {
                        // For ordered lists, we'll use "1." for simplicity
                        prefix = "1. ";
                    }

                    var mdLi = HtmlNode.CreateNode(prefix + liText + Environment.NewLine);
                    li.ParentNode?.ReplaceChild(mdLi, li);
                }
            }

            // Convert blockquotes
            var blockquotes = node.SelectNodes(".//blockquote");
            if (blockquotes != null)
            {
                foreach (var blockquote in blockquotes.ToList())
                {
                    var quoteText = blockquote.InnerText.Trim();
                    if (string.IsNullOrWhiteSpace(quoteText))
                    {
                        blockquote.Remove();
                        continue;
                    }

                    var lines = quoteText.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                    var quotedLines = lines.Select(line => "> " + line.Trim());
                    var mdQuote = string.Join(Environment.NewLine, quotedLines) + Environment.NewLine + Environment.NewLine;
                    var mdNode = HtmlNode.CreateNode(mdQuote);
                    blockquote.ParentNode?.ReplaceChild(mdNode, blockquote);
                }
            }

            // Convert code blocks
            var codeBlocks = node.SelectNodes(".//pre | .//code");
            if (codeBlocks != null)
            {
                foreach (var code in codeBlocks.ToList())
                {
                    var codeText = code.InnerText.Trim();
                    if (string.IsNullOrWhiteSpace(codeText))
                    {
                        code.Remove();
                        continue;
                    }

                    string mdCode;
                    if (code.Name.Equals("pre", StringComparison.OrdinalIgnoreCase))
                    {
                        mdCode = "```" + Environment.NewLine + codeText + Environment.NewLine + "```" + Environment.NewLine;
                    }
                    else
                    {
                        mdCode = "`" + codeText + "`";
                    }

                    var mdNode = HtmlNode.CreateNode(mdCode);
                    code.ParentNode?.ReplaceChild(mdNode, code);
                }
            }

            // Get final text and normalize whitespace while preserving Markdown structure
            string text = node.InnerText;
            // Collapse horizontal whitespace (spaces/tabs) to single space
            text = HorizontalWhitespaceRegex.Replace(text, " ");
            // Normalize line breaks: remove trailing spaces before newlines, collapse multiple newlines to double newline
            text = text.Replace(" \r\n", "\r\n").Replace(" \n", "\n");
            text = Regex.Replace(text, @"\r?\n(\s*\r?\n)+", Environment.NewLine + Environment.NewLine);
            return text.Trim();
        }
    }
}
