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

namespace SmartHopper.DocumentProcessing
{
    /// <summary>
    /// Options for <see cref="IDocumentReader.ReadAsync"/>: extraction flags, formatting,
    /// page selection, detail crop and renderer location/limits.
    /// </summary>
    public sealed record DocumentReadOptions
    {
        /// <summary>Extract embedded image assets.</summary>
        public bool ExtractEmbeddedImages { get; init; } = true;

        /// <summary>Render selected pages through the isolated renderer.</summary>
        public bool RenderPages { get; init; } = true;

        /// <summary>Preserve running headers/footers. Default false (removed) —
        /// matches the existing FileConversionOptions.RemoveHeadersFooters default.</summary>
        public bool PreserveHeaderFooter { get; init; } = false;

        /// <summary>Preserve inline formatting (bold/italic) in the Markdown projection.</summary>
        public bool PreserveFormatting { get; init; } = true;

        /// <summary>Detect and render tables in the Markdown projection.</summary>
        public bool PreserveTables { get; init; } = true;

        /// <summary>Preserve link targets in the Markdown projection.</summary>
        public bool PreserveLinks { get; init; } = true;

        /// <summary>Detect headings by font size in the Markdown projection.</summary>
        public bool PreserveHeadings { get; init; } = true;

        /// <summary>Optional selected 1-based page numbers. Null means the whole document;
        /// an EMPTY list is invalid (a selection that selects nothing). Validated:
        /// unique, in 1..totalPages.</summary>
        public IReadOnlyList<int> SelectedPages { get; init; }

        /// <summary>Optional normalized detail crop on a selected page.</summary>
        public DocumentRegionSelection Region { get; init; }

        /// <summary>Directory containing SmartHopper.DocumentRendering.dll + natives.
        /// Null uses the packaged install location.</summary>
        public string RendererDirectory { get; init; }

        /// <summary>Optional trust verifier for a caller-supplied
        /// <see cref="RendererDirectory"/> (hash/Authenticode check). Required whenever a
        /// non-packaged directory is supplied; the packaged default needs no verifier.</summary>
        public System.Func<string, bool> RendererTrustVerifier { get; init; }

        /// <summary>Rasterization DPI for full-page renders (default 144).</summary>
        public int RenderDpi { get; init; } = 144;

        /// <summary>Max pixels on the longest side of a full-page render (default 2048).</summary>
        public int MaxRenderSide { get; init; } = 2048;

        /// <summary>Max pixels on the longest side of a detail crop (default 4096).</summary>
        public int MaxCropSide { get; init; } = 4096;

        /// <summary>Maximum pages rendered in one call (default 32).</summary>
        public int MaxRenderPages { get; init; } = 32;

        /// <summary>Maximum total encoded PNG bytes across renders/crops (64 MiB).</summary>
        public long MaxEncodedBytes { get; init; } = 64L * 1024 * 1024;

        /// <summary>Maximum source file size (128 MiB).</summary>
        public long MaxFileBytes { get; init; } = 128L * 1024 * 1024;
    }
}
