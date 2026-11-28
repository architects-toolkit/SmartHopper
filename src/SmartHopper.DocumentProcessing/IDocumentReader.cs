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
using System.Threading;
using System.Threading.Tasks;
using SmartHopper.ProviderSdk.Documents;

namespace SmartHopper.DocumentProcessing
{
    /// <summary>
    /// A document reader that extracts a source file into neutral <see cref="DocumentEvidence"/>:
    /// per-page text/Markdown, rendered pages, embedded assets, provenance, warnings and coverage.
    /// Implementations are registered in <see cref="DocumentReaders"/>; new formats plug in without
    /// touching the consuming converters, tools or surfaces.
    /// </summary>
    public interface IDocumentReader
    {
        /// <summary>
        /// Canonical short format identifier (e.g. <c>"pdf"</c>). Feeds
        /// <see cref="DocumentEvidence.Format"/> and renderer dispatch inside the isolated
        /// rendering assembly.
        /// </summary>
        string FormatId { get; }

        /// <summary>File extensions this reader handles, with leading dot (e.g. <c>".pdf"</c>).</summary>
        IEnumerable<string> SupportedExtensions { get; }

        /// <summary>
        /// Reads <paramref name="sourcePath"/> into evidence. Implementations honour
        /// <paramref name="options"/> for embedded assets, page rendering, page/region
        /// selection and byte budgets; unsupported option features must degrade to explicit
        /// warnings, never silent drops.
        /// </summary>
        /// <param name="sourcePath">Local file path to read.</param>
        /// <param name="options">Extraction/render options and limits (never null).</param>
        /// <param name="cancellationToken">Cancellation token (cooperative around native work).</param>
        /// <returns>The document evidence.</returns>
        Task<DocumentEvidence> ReadAsync(string sourcePath, DocumentReadOptions options, CancellationToken cancellationToken);
    }
}
