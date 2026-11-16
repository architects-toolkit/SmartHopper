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

namespace SmartHopper.ProviderSdk.AICall.Core.Interactions
{
    /// <summary>
    /// Classifies what a sourced piece of content represents inside its parent document.
    /// </summary>
    public enum SourceContentKind
    {
        /// <summary>Kind not specified.</summary>
        Unknown = 0,

        /// <summary>A full document page (for example, a rendered PDF page).</summary>
        Page = 1,

        /// <summary>An embedded figure or named visual inside a page.</summary>
        Figure = 2,

        /// <summary>A standalone image asset.</summary>
        Image = 3,

        /// <summary>A normalized sub-region crop of a page or image.</summary>
        Crop = 4,
    }

    /// <summary>
    /// Normalized top-left origin rectangle inside a source page or image.
    /// Coordinates are expressed as fractions (0..1) of the containing content.
    /// </summary>
    public sealed record SourceContentRegion
    {
        /// <summary>Gets the normalized left offset (0..1).</summary>
        public double X { get; init; }

        /// <summary>Gets the normalized top offset (0..1).</summary>
        public double Y { get; init; }

        /// <summary>Gets the normalized width (0..1).</summary>
        public double Width { get; init; }

        /// <summary>Gets the normalized height (0..1).</summary>
        public double Height { get; init; }
    }

    /// <summary>
    /// Neutral provenance record associating an interaction or tool-result image with the
    /// document, page or asset it was extracted from. Carried end-to-end so visual evidence
    /// can be converted to text without losing page/figure attribution.
    /// </summary>
    public sealed record SourceContentReference
    {
        /// <summary>Gets the stable identifier of the containing document, when known.</summary>
        public string DocumentId { get; init; }

        /// <summary>Gets the human-readable source name (file name, URL, title).</summary>
        public string SourceName { get; init; }

        /// <summary>Gets the 1-based page number inside the document, when applicable.</summary>
        public int? PageNumber { get; init; }

        /// <summary>Gets the identifier of the embedded asset inside the document, when applicable.</summary>
        public string AssetId { get; init; }

        /// <summary>Gets the figure label as declared by the document (e.g. "Figure 3").</summary>
        public string FigureLabel { get; init; }

        /// <summary>Gets the caption text attached to this content, when present.</summary>
        public string Caption { get; init; }

        /// <summary>Gets the extracted text that surrounds this content in the source, when present.</summary>
        public string ContextText { get; init; }

        /// <summary>Gets what this content represents inside the document.</summary>
        public SourceContentKind Kind { get; init; } = SourceContentKind.Unknown;

        /// <summary>Gets the normalized region this content occupies inside its page, when it is a crop.</summary>
        public SourceContentRegion Region { get; init; }
    }
}
