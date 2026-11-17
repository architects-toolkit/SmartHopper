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
using SmartHopper.ProviderSdk.AICall.Core.Interactions;

namespace SmartHopper.ProviderSdk.Documents
{
    /// <summary>
    /// Neutral, immutable evidence for a converted document: extracted text, page renders,
    /// embedded assets, source associations, warnings and coverage. A document is evidence,
    /// not a flattened Markdown string — callers project Markdown when they need it.
    /// </summary>
    public sealed record DocumentEvidence
    {
        /// <summary>Stable document identity (content-derived when available).</summary>
        public string DocumentId { get; init; } = string.Empty;

        /// <summary>Display name of the source (file name, URL, etc.).</summary>
        public string SourceName { get; init; } = string.Empty;

        /// <summary>Source format identifier (for example, "pdf", "html", "docx").</summary>
        public string Format { get; init; } = string.Empty;

        /// <summary>Source-level metadata (title, author, dates, size).</summary>
        public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();

        /// <summary>Per-page evidence in page order.</summary>
        public IReadOnlyList<DocumentPageEvidence> Pages { get; init; } = new List<DocumentPageEvidence>();

        /// <summary>
        /// Ordered typed tool-result images (renders, crops, embedded assets). Identity is
        /// <see cref="SourceContentReference.AssetId"/>.
        /// </summary>
        public IReadOnlyList<ToolResultImage> Images { get; init; } = new List<ToolResultImage>();

        /// <summary>Document-level warnings (partial extraction, missing renderer, etc.).</summary>
        public IReadOnlyList<string> Warnings { get; init; } = new List<string>();

        /// <summary>Visual/textual coverage of the selected subset.</summary>
        public DocumentCoverage Coverage { get; init; } = new DocumentCoverage();
    }

    /// <summary>Evidence for one page of a document.</summary>
    public sealed record DocumentPageEvidence
    {
        /// <summary>1-based page number.</summary>
        public int PageNumber { get; init; }

        /// <summary>Visible (rotation/crop-adjusted) width in points.</summary>
        public double WidthPoints { get; init; }

        /// <summary>Visible (rotation/crop-adjusted) height in points.</summary>
        public double HeightPoints { get; init; }

        /// <summary>Raw extracted text for the page.</summary>
        public string ExtractedText { get; init; } = string.Empty;

        /// <summary>Markdown projection for the page (headers, tables, links).</summary>
        public string Markdown { get; init; } = string.Empty;

        /// <summary>AssetId of the full-page render in <see cref="DocumentEvidence.Images"/>, if any.</summary>
        public string RenderImageId { get; init; }

        /// <summary>AssetIds of embedded assets and detail crops on this page.</summary>
        public IReadOnlyList<string> AssetIds { get; init; } = new List<string>();

        /// <summary>Page-level warnings.</summary>
        public IReadOnlyList<string> Warnings { get; init; } = new List<string>();
    }

    /// <summary>
    /// Coverage of the selected subset: completeness means all SELECTED pages were covered,
    /// not all source pages — the selection itself is never obscured.
    /// </summary>
    public sealed record DocumentCoverage
    {
        /// <summary>Total pages in the source document.</summary>
        public int TotalPages { get; init; }

        /// <summary>The ACTUAL resolved selection of 1-based page numbers — always
        /// populated (the whole document when the caller selected all pages).</summary>
        public IReadOnlyList<int> SelectedPages { get; init; } = new List<int>();

        /// <summary>Pages successfully rendered.</summary>
        public IReadOnlyList<int> RenderedPages { get; init; } = new List<int>();

        /// <summary>Pages that failed to render.</summary>
        public IReadOnlyList<int> FailedPages { get; init; } = new List<int>();

        /// <summary>Pages covered by text extraction only (no render).</summary>
        public IReadOnlyList<int> TextOnlyPages { get; init; } = new List<int>();

        /// <summary>True when every selected page has full coverage.</summary>
        public bool IsComplete { get; init; }
    }
}
