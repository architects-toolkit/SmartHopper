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
 * https://github.com/ariesdevil/markdown-clipper
 * Apache License 2.0
 * Copyright (c) ariesdevil
 *
 * Uses SmartReader (Mozilla Readability .NET port):
 * https://github.com/Strumenta/SmartReader
 * Apache License 2.0
 *
 * Key concepts adapted:
 * - Readability-based article extraction (title, byline, excerpt, cleaned content)
 * - Confidence gating so callers can fall back to heuristics when extraction is weak
 */

using System;
using System.Diagnostics;

namespace SmartHopper.Core.Grasshopper.Converters.Formats
{
    /// <summary>
    /// <see cref="IReadabilityExtractor"/> implementation backed by SmartReader,
    /// a .NET port of Mozilla Readability.
    /// </summary>
    public sealed class SmartReaderExtractor : IReadabilityExtractor
    {
        /// <inheritdoc />
        public string Name => "smartreader";

        /// <inheritdoc />
        public ReadabilityResult? Extract(string html, string? baseUrl = null)
        {
            if (string.IsNullOrWhiteSpace(html))
            {
                return null;
            }

            try
            {
                var uri = !string.IsNullOrWhiteSpace(baseUrl) && Uri.TryCreate(baseUrl, UriKind.Absolute, out var parsed)
                    ? parsed
                    : new Uri("https://smarthopper.local/");

                var reader = new SmartReader.Reader(uri.ToString(), html);
                var article = reader.GetArticle();
                if (article == null || !article.IsReadable || string.IsNullOrWhiteSpace(article.Content))
                {
                    return null;
                }

                // SmartReader exposes a readable "score" via Length when IsReadable; use a
                // rough proxy: longer, dense articles are higher-confidence.
                double confidence = article.IsReadable ? Math.Min(1.0, article.Length / 1500.0) : 0.0;

                return new ReadabilityResult
                {
                    CleanHtml = article.Content,
                    Title = article.Title,
                    Byline = article.Byline,
                    Excerpt = article.Excerpt,
                    Confidence = confidence,
                    ExtractorName = this.Name,
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SmartReaderExtractor] Extraction failed: {ex.Message}");
                return null;
            }
        }
    }
}
