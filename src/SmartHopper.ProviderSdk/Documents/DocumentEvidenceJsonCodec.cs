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
using System.Linq;
using Newtonsoft.Json.Linq;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;

namespace SmartHopper.ProviderSdk.Documents
{
    /// <summary>
    /// Shared compact document-evidence JSON projection. Encodes page metadata,
    /// coverage and warnings — never image bytes; the typed
    /// <see cref="ToolResultImage"/> list travels separately and is reattached on
    /// decode. Reused by tool adapters, <c>File2MdToolResult</c> and chat instead of
    /// three divergent DTOs.
    /// </summary>
    public static class DocumentEvidenceJsonCodec
    {
        /// <summary>Encodes a <see cref="DocumentEvidence"/> into a compact JObject
        /// (descriptor fields only — no image payloads).</summary>
        public static JObject Encode(DocumentEvidence document)
        {
            if (document == null)
            {
                return new JObject();
            }

            return new JObject
            {
                ["documentId"] = document.DocumentId,
                ["format"] = document.Format,
                ["sourceName"] = document.SourceName,
                ["pages"] = new JArray(document.Pages?.Select(p => new JObject
                {
                    ["pageNumber"] = p.PageNumber,
                    ["widthPoints"] = p.WidthPoints,
                    ["heightPoints"] = p.HeightPoints,
                    ["extractedText"] = p.ExtractedText,
                    ["markdown"] = p.Markdown,
                    ["renderImageId"] = p.RenderImageId,
                    ["assetIds"] = new JArray(p.AssetIds ?? Enumerable.Empty<string>()),
                    ["warnings"] = new JArray(p.Warnings ?? Enumerable.Empty<string>()),
                }) ?? Enumerable.Empty<JObject>()),
                ["coverage"] = document.Coverage == null ? new JObject() : new JObject
                {
                    ["totalPages"] = document.Coverage.TotalPages,
                    ["selectedPages"] = new JArray(document.Coverage.SelectedPages),
                    ["renderedPages"] = new JArray(document.Coverage.RenderedPages),
                    ["failedPages"] = new JArray(document.Coverage.FailedPages),
                    ["textOnlyPages"] = new JArray(document.Coverage.TextOnlyPages),
                    ["isComplete"] = document.Coverage.IsComplete,
                },
                ["warnings"] = new JArray(document.Warnings ?? Enumerable.Empty<string>()),
                ["metadata"] = document.Metadata == null ? new JObject() : new JObject(
                    document.Metadata.Select(kv => new JProperty(kv.Key, kv.Value))),
            };
        }

        /// <summary>Reconstructs a <see cref="DocumentEvidence"/> from the compact JObject
        /// produced by <see cref="Encode"/>, reattaching the typed images (whose bytes
        /// and provenance never round-tripped through JSON). Returns null when the
        /// object lacks document evidence.</summary>
        public static DocumentEvidence Decode(JObject raw, IReadOnlyList<ToolResultImage> images)
        {
            if (raw == null || raw["documentId"] == null || raw["pages"] is not JArray pagesJson)
            {
                return null;
            }

            var pages = new List<DocumentPageEvidence>();
            foreach (var p in pagesJson.OfType<JObject>())
            {
                pages.Add(new DocumentPageEvidence
                {
                    PageNumber = p.Value<int>("pageNumber"),
                    WidthPoints = p["widthPoints"]?.Value<double>() ?? 0,
                    HeightPoints = p["heightPoints"]?.Value<double>() ?? 0,
                    ExtractedText = p["extractedText"]?.ToString() ?? string.Empty,
                    Markdown = p["markdown"]?.ToString() ?? string.Empty,
                    RenderImageId = p["renderImageId"]?.ToString(),
                    AssetIds = (p["assetIds"] as JArray)?.Values<string>().ToList() ?? new List<string>(),
                    Warnings = (p["warnings"] as JArray)?.Values<string>().ToList() ?? new List<string>(),
                });
            }

            var cov = raw["coverage"] as JObject;
            var coverage = cov == null
                ? new DocumentCoverage()
                : new DocumentCoverage
                {
                    TotalPages = cov.Value<int>("totalPages"),
                    SelectedPages = (cov["selectedPages"] as JArray)?.Values<int>().ToList() ?? new List<int>(),
                    RenderedPages = (cov["renderedPages"] as JArray)?.Values<int>().ToList() ?? new List<int>(),
                    FailedPages = (cov["failedPages"] as JArray)?.Values<int>().ToList() ?? new List<int>(),
                    TextOnlyPages = (cov["textOnlyPages"] as JArray)?.Values<int>().ToList() ?? new List<int>(),
                    IsComplete = cov.Value<bool>("isComplete"),
                };

            return new DocumentEvidence
            {
                DocumentId = raw["documentId"]?.ToString(),
                SourceName = raw["sourceName"]?.ToString() ?? raw["source"]?.ToString(),
                Format = raw["format"]?.ToString() ?? raw["originalFormat"]?.ToString() ?? "pdf",
                Pages = pages,
                Images = (images ?? Array.Empty<ToolResultImage>()).Where(i => i != null).ToList(),
                Warnings = (raw["warnings"] as JArray)?.Values<string>().ToList() ?? new List<string>(),
                Coverage = coverage,
                Metadata = (raw["metadata"] as JObject)?.Properties()
                    .Where(p => p != null)
                    .ToDictionary(p => p.Name, p => p.Value?.ToString() ?? string.Empty)
                    ?? new Dictionary<string, string>(),
            };
        }
    }
}
