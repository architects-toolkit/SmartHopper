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
 * - Readability scoring for HTML content extraction
 * - Text density scoring for content identification
 * - Link density analysis for boilerplate detection
 * - Semantic tag prioritization (article, main, section)
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace SmartHopper.Core.Grasshopper.Converters.Formats
{
    /// <summary>
    /// Helper class for HTML readability extraction inspired by magic-html.
    /// Implements content scoring and boilerplate removal.
    /// </summary>
    public static class HtmlReadabilityHelper
    {
        private static readonly string[] BoilerplateClassPatterns = new[]
        {
            "nav", "header", "footer", "sidebar", "aside", "menu", "banner",
            "ad", "advertisement", "promo", "cookie", "consent", "social",
            "share", "comment-form", "subscribe", "newsletter", "related"
        };

        private static readonly string[] BoilerplateIdPatterns = new[]
        {
            "nav", "header", "footer", "sidebar", "menu", "banner",
            "ad", "advertisement", "cookie", "social", "comments-form"
        };

        private static readonly string[] BoilerplateTags = new[]
        {
            "nav", "header", "footer", "aside", "form"
        };

        /// <summary>
        /// Extracts the main content from HTML using readability scoring.
        /// </summary>
        public static HtmlNode? ExtractMainContent(HtmlDocument doc)
        {
            if (doc?.DocumentNode == null)
            {
                return null;
            }

            // Remove boilerplate nodes first
            RemoveBoilerplateNodes(doc);

            // Find candidate containers
            var candidates = FindContentCandidates(doc.DocumentNode);
            if (candidates.Count == 0)
            {
                return doc.DocumentNode;
            }

            // Score each candidate
            var scoredCandidates = candidates
                .Select(node => new { Node = node, Score = ScoreContentNode(node) })
                .OrderByDescending(x => x.Score)
                .ToList();

            // Return the highest-scoring candidate
            return scoredCandidates.FirstOrDefault()?.Node ?? doc.DocumentNode;
        }

        /// <summary>
        /// Removes boilerplate nodes (nav, header, footer, ads, etc.) from the document.
        /// </summary>
        private static void RemoveBoilerplateNodes(HtmlDocument doc)
        {
            // Remove by tag name
            foreach (var tagName in BoilerplateTags)
            {
                var nodes = doc.DocumentNode.SelectNodes($"//{tagName}");
                if (nodes != null)
                {
                    foreach (var node in nodes.ToList())
                    {
                        node.Remove();
                    }
                }
            }

            // Remove by class patterns
            var allNodes = doc.DocumentNode.Descendants().ToList();
            foreach (var node in allNodes)
            {
                var classAttr = node.GetAttributeValue("class", string.Empty).ToLowerInvariant();
                var idAttr = node.GetAttributeValue("id", string.Empty).ToLowerInvariant();

                bool shouldRemove = false;

                foreach (var pattern in BoilerplateClassPatterns)
                {
                    if (classAttr.Contains(pattern))
                    {
                        shouldRemove = true;
                        break;
                    }
                }

                if (!shouldRemove)
                {
                    foreach (var pattern in BoilerplateIdPatterns)
                    {
                        if (idAttr.Contains(pattern))
                        {
                            shouldRemove = true;
                            break;
                        }
                    }
                }

                if (shouldRemove)
                {
                    node.Remove();
                }
            }

            // Remove script, style, img, noscript, svg, canvas
            string[] removeTags = { "script", "style", "img", "noscript", "svg", "canvas" };
            foreach (var tagName in removeTags)
            {
                var nodes = doc.DocumentNode.SelectNodes($"//{tagName}");
                if (nodes != null)
                {
                    foreach (var node in nodes.ToList())
                    {
                        node.Remove();
                    }
                }
            }
        }

        /// <summary>
        /// Finds potential content container candidates (article, main, div, section).
        /// </summary>
        private static List<HtmlNode> FindContentCandidates(HtmlNode root)
        {
            var candidates = new List<HtmlNode>();

            // Prioritize semantic containers
            var semanticContainers = root.SelectNodes("//article | //main");
            if (semanticContainers != null)
            {
                candidates.AddRange(semanticContainers);
            }

            // Add div and section containers
            var divSectionContainers = root.SelectNodes("//div | //section");
            if (divSectionContainers != null)
            {
                candidates.AddRange(divSectionContainers);
            }

            // Filter out nested candidates (keep only top-level containers)
            var topLevelCandidates = new List<HtmlNode>();
            foreach (var candidate in candidates)
            {
                bool isNested = false;
                foreach (var other in candidates)
                {
                    if (other != candidate && IsDescendantOf(candidate, other))
                    {
                        isNested = true;
                        break;
                    }
                }

                if (!isNested)
                {
                    topLevelCandidates.Add(candidate);
                }
            }

            return topLevelCandidates;
        }

        /// <summary>
        /// Scores a content node based on text density and link density.
        /// Higher score = more likely to be main content.
        /// </summary>
        private static double ScoreContentNode(HtmlNode node)
        {
            if (node == null)
            {
                return 0;
            }

            // Get text length
            var textContent = node.InnerText ?? string.Empty;
            var textLength = textContent.Length;

            if (textLength == 0)
            {
                return 0;
            }

            // Count links
            var links = node.SelectNodes(".//a");
            var linkCount = links?.Count ?? 0;

            // Count paragraphs
            var paragraphs = node.SelectNodes(".//p");
            var paragraphCount = paragraphs?.Count ?? 0;

            // Calculate text density (text length / link count + 1)
            // Higher text density = more content, fewer links
            var textDensity = textLength / (double)(linkCount + 1);

            // Bonus for paragraphs
            var paragraphBonus = paragraphCount * 10;

            // Bonus for semantic tags
            var semanticBonus = 0;
            if (node.Name.Equals("article", StringComparison.OrdinalIgnoreCase))
            {
                semanticBonus = 100;
            }
            else if (node.Name.Equals("main", StringComparison.OrdinalIgnoreCase))
            {
                semanticBonus = 80;
            }

            // Check for content-indicating class/id patterns
            var classAttr = node.GetAttributeValue("class", string.Empty).ToLowerInvariant();
            var idAttr = node.GetAttributeValue("id", string.Empty).ToLowerInvariant();
            var contentBonus = 0;

            string[] contentPatterns = { "content", "main", "article", "post", "entry", "body" };
            foreach (var pattern in contentPatterns)
            {
                if (classAttr.Contains(pattern) || idAttr.Contains(pattern))
                {
                    contentBonus = 50;
                    break;
                }
            }

            return textDensity + paragraphBonus + semanticBonus + contentBonus;
        }

        /// <summary>
        /// Checks if a node is a descendant of another node.
        /// </summary>
        private static bool IsDescendantOf(HtmlNode node, HtmlNode potentialAncestor)
        {
            var current = node.ParentNode;
            while (current != null)
            {
                if (current == potentialAncestor)
                {
                    return true;
                }

                current = current.ParentNode;
            }

            return false;
        }
    }
}
