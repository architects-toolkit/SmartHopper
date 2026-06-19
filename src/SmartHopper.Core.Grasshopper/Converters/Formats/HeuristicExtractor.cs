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
 * - Heuristic main-content extraction via text/link density scoring
 * - Boilerplate removal via tag/class/ID pattern matching
 *
 * Uses HtmlAgilityPack (MIT) for HTML parsing:
 * https://github.com/zzzprojects/html-agility-pack
 */

using System;
using System.Diagnostics;
using HtmlAgilityPack;

namespace SmartHopper.Core.Grasshopper.Converters.Formats
{
    /// <summary>
    /// <see cref="IReadabilityExtractor"/> implementation backed by
    /// <see cref="HtmlReadabilityHelper"/>'s magic-html-inspired heuristic scoring.
    /// Used as the fallback when <see cref="SmartReaderExtractor"/> is not confident
    /// or disabled.
    /// </summary>
    public sealed class HeuristicExtractor : IReadabilityExtractor
    {
        /// <inheritdoc />
        public string Name => "heuristic";

        /// <inheritdoc />
        public ReadabilityResult? Extract(string html, string? baseUrl = null)
        {
            if (string.IsNullOrWhiteSpace(html))
            {
                return null;
            }

            try
            {
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                var mainContent = HtmlReadabilityHelper.ExtractMainContent(doc);
                if (mainContent == null)
                {
                    return null;
                }

                string? title = null;
                var titleNode = doc.DocumentNode.SelectSingleNode("//title");
                if (titleNode != null && !string.IsNullOrWhiteSpace(titleNode.InnerText))
                {
                    title = titleNode.InnerText.Trim();
                }

                return new ReadabilityResult
                {
                    CleanHtml = mainContent.OuterHtml ?? string.Empty,
                    Title = title,
                    Confidence = 0.5, // heuristic: moderate baseline
                    ExtractorName = this.Name,
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[HeuristicExtractor] Extraction failed: {ex.Message}");
                return null;
            }
        }
    }
}
