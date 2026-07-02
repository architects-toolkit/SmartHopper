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

using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace SmartHopper.Core.Grasshopper.Converters
{
    /// <summary>
    /// Post-processing pass applied to Markdown produced by any <see cref="IFileConverter"/>.
    /// Converters (e.g. <see cref="Formats.PdfConverter"/>) normalize non-CommonMark ordered-list
    /// markers (letters, Roman numerals) to a plain "1." marker per item, since CommonMark has no
    /// native support for lettered/Roman ordered lists. Left as-is, this produces a visually
    /// incorrect list where every item reads "1.". This renumberer walks the final Markdown and
    /// rewrites consecutive ordered-list items (matched by leading indentation) to increasing
    /// integers, the same way a Markdown renderer would display them, so the raw text also reads
    /// correctly for humans and for downstream tools that don't render Markdown.
    /// </summary>
    public static class MarkdownListRenumberer
    {
        /// <summary>Matches an ordered-list item line: leading indentation, digits, '.', then a space.</summary>
        private static readonly Regex OrderedItemPattern = new Regex(@"^(?<indent>[ \t]*)(?<num>\d+)\.(?<sep>\s+)", RegexOptions.Compiled);

        /// <summary>
        /// Renumbers consecutive ordered-list items in the given Markdown so each list starts at
        /// its original first number and increments by one per item, regardless of what number the
        /// source converter assigned to each line. Lists are tracked per indentation level; a list
        /// at a given indentation ends when a non-blank line without a matching or deeper indented
        /// list item is encountered, or when a shallower-indented list item appears (which also
        /// resets any deeper nested list counters).
        /// </summary>
        /// <param name="markdown">The Markdown content to normalize.</param>
        /// <returns>Markdown with ordered-list numbering made consecutive.</returns>
        public static string Renumber(string markdown)
        {
            if (string.IsNullOrEmpty(markdown))
            {
                return markdown;
            }

            var lines = markdown.Replace("\r\n", "\n").Split('\n');
            var counters = new Dictionary<int, int>();
            var sb = new StringBuilder(markdown.Length);

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                var match = OrderedItemPattern.Match(line);

                if (match.Success)
                {
                    int indentLength = match.Groups["indent"].Length;

                    // A new item at this indentation ends any deeper nested lists.
                    ClearDeeperIndents(counters, indentLength);

                    int nextNumber = counters.TryGetValue(indentLength, out int current)
                        ? current + 1
                        : int.Parse(match.Groups["num"].Value);

                    counters[indentLength] = nextNumber;

                    sb.Append(match.Groups["indent"].Value)
                      .Append(nextNumber)
                      .Append('.')
                      .Append(match.Groups["sep"].Value)
                      .Append(line.Substring(match.Length));
                }
                else
                {
                    // Blank lines don't break a list (converters separate items with blank lines);
                    // any other content ends all open ordered lists.
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        counters.Clear();
                    }

                    sb.Append(line);
                }

                if (i < lines.Length - 1)
                {
                    sb.Append('\n');
                }
            }

            return sb.ToString();
        }

        /// <summary>Removes counters for indentation levels deeper than <paramref name="indentLength"/>.</summary>
        private static void ClearDeeperIndents(Dictionary<int, int> counters, int indentLength)
        {
            var deeperKeys = counters.Keys.Where(k => k > indentLength).ToList();
            foreach (var key in deeperKeys)
            {
                counters.Remove(key);
            }
        }
    }

    /// <summary>
    /// Final Markdown-hygiene pass applied to the output of any <see cref="IFileConverter"/>.
    /// Individual converters focus on faithfully extracting content; this class normalizes the
    /// resulting Markdown so it renders consistently and reads cleanly as raw text (e.g. when fed
    /// directly to an LLM without rendering).
    /// </summary>
    public static class MarkdownStyleCleanup
    {
        /// <summary>Matches a run of 3 or more consecutive newlines (2+ blank lines).</summary>
        private static readonly Regex ExcessBlankLinesPattern = new Regex(@"\n{3,}", RegexOptions.Compiled);

        /// <summary>Matches trailing whitespace at the end of a line.</summary>
        private static readonly Regex TrailingWhitespacePattern = new Regex(@"[ \t]+$", RegexOptions.Compiled | RegexOptions.Multiline);

        /// <summary>Matches an ATX heading line ("#" through "######").</summary>
        private static readonly Regex AtxHeadingPattern = new Regex(@"^(#{1,6})[ \t]+\S", RegexOptions.Compiled);

        /// <summary>
        /// Applies Markdown hygiene fixes: normalizes line endings, trims trailing whitespace
        /// (which would otherwise be interpreted as a CommonMark hard line break), ensures a blank
        /// line surrounds every ATX heading, collapses runs of 2+ blank lines into a single blank
        /// line, and trims leading/trailing blank lines from the document.
        /// </summary>
        /// <param name="markdown">The Markdown content to normalize.</param>
        /// <returns>Cleaned-up Markdown.</returns>
        public static string Cleanup(string markdown)
        {
            if (string.IsNullOrEmpty(markdown))
            {
                return markdown;
            }

            string normalized = markdown.Replace("\r\n", "\n").Replace("\r", "\n");
            normalized = TrailingWhitespacePattern.Replace(normalized, string.Empty);
            normalized = EnsureHeadingSpacing(normalized);
            normalized = ExcessBlankLinesPattern.Replace(normalized, "\n\n");

            return normalized.Trim('\n');
        }

        /// <summary>
        /// Inserts a blank line before and after every ATX heading line that doesn't already have
        /// one, so headings are recognized even by strict CommonMark parsers.
        /// </summary>
        private static string EnsureHeadingSpacing(string markdown)
        {
            var lines = markdown.Split('\n');
            var output = new List<string>(lines.Length + 8);

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                bool isHeading = AtxHeadingPattern.IsMatch(line);

                if (isHeading && output.Count > 0 && !string.IsNullOrWhiteSpace(output[output.Count - 1]))
                {
                    output.Add(string.Empty);
                }

                output.Add(line);

                bool hasNextLine = i < lines.Length - 1;
                bool nextIsBlank = hasNextLine && string.IsNullOrWhiteSpace(lines[i + 1]);

                if (isHeading && hasNextLine && !nextIsBlank)
                {
                    output.Add(string.Empty);
                }
            }

            return string.Join("\n", output);
        }
    }
}
