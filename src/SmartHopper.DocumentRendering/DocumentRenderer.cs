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

namespace SmartHopper.DocumentRendering
{
    /// <summary>
    /// Stable reflection boundary for the host's isolated load context: format-keyed entry
    /// points that exchange only BCL values (format id, source bytes, page index, render
    /// parameters in; PNG bytes and page dimensions out). New renderers plug in by adding a
    /// <paramref name="formatId"/> case — the host contract never changes.
    /// </summary>
    public static class DocumentRenderer
    {
        /// <summary>
        /// Renders one document page (or a normalized region of it) to PNG bytes.
        /// </summary>
        /// <param name="formatId">Reader format id (currently only <c>"pdf"</c>).</param>
        /// <param name="source">Source document bytes.</param>
        /// <param name="zeroBasedPage">0-based page index.</param>
        /// <param name="dpi">Rasterization DPI.</param>
        /// <param name="maximumSide">Max pixels on the longest side.</param>
        /// <param name="normalizedRegion">Null/empty for a full page, else four normalized coordinates.</param>
        /// <returns>PNG bytes.</returns>
        public static byte[] Render(string formatId, byte[] source, int zeroBasedPage, int dpi, int maximumSide, double[] normalizedRegion)
        {
            return formatId switch
            {
                "pdf" => PdfRenderer.Render(source, zeroBasedPage, dpi, maximumSide, normalizedRegion),
                _ => throw new NotSupportedException($"No renderer is available for format '{formatId}'."),
            };
        }

        /// <summary>
        /// Returns flattened visible page dimensions in points [w0,h0,w1,h1,...].
        /// </summary>
        /// <param name="formatId">Reader format id (currently only <c>"pdf"</c>).</param>
        /// <param name="source">Source document bytes.</param>
        /// <returns>Flattened per-page width/height pairs.</returns>
        public static double[] GetPageSizes(string formatId, byte[] source)
        {
            return formatId switch
            {
                "pdf" => PdfRenderer.GetPageSizes(source),
                _ => throw new NotSupportedException($"No renderer is available for format '{formatId}'."),
            };
        }
    }
}
