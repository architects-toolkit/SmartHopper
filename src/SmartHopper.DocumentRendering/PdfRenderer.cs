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
using System.IO;
using PDFtoImage;
using SkiaSharp;

namespace SmartHopper.DocumentRendering
{
    /// <summary>
    /// Isolated PDF renderer boundary: BCL values only. PDF bytes and scalars in, PNG bytes
    /// and flattened scalars out — no SkiaSharp/PDFtoImage type crosses the load context.
    /// </summary>
    public static class PdfRenderer
    {
        /// <summary>
        /// Renders one page to PNG bytes.
        /// </summary>
        /// <param name="pdf">PDF document bytes.</param>
        /// <param name="zeroBasedPage">Zero-based page index.</param>
        /// <param name="dpi">Rasterization DPI.</param>
        /// <param name="maximumSide">Maximum pixel size of the longest output side (0 = unbounded).</param>
        /// <param name="normalizedRegion">Optional [x,y,width,height] in normalized top-left
        /// coordinates of the visible page; null or empty renders the full page.</param>
        /// <returns>PNG bytes, or null on invalid input.</returns>
        /// <summary>Absolute DPI ceiling for detail crops (bounds pathological regions).</summary>
        private const int MaxCropDpi = 1200;

        /// <summary>
        /// Renders one page to PNG bytes.
        /// </summary>
        /// <param name="pdf">PDF document bytes.</param>
        /// <param name="zeroBasedPage">Zero-based page index.</param>
        /// <param name="dpi">Rasterization DPI for full pages. For a detail region the
        /// effective DPI is raised so the region's longest side reaches
        /// <paramref name="maximumSide"/> (true high-density sampling, not a downscale).</param>
        /// <param name="maximumSide">Target pixel size of the longest output side
        /// (full pages are capped at it; crops are upsampled to it). Must be &gt; 0.</param>
        /// <param name="normalizedRegion">Optional [x,y,width,height] in normalized top-left
        /// coordinates of the visible page; null or empty renders the full page.</param>
        /// <returns>PNG bytes, or null on invalid input.</returns>
        public static byte[] Render(byte[] pdf, int zeroBasedPage, int dpi, int maximumSide, double[] normalizedRegion)
        {
            if (pdf == null || pdf.Length == 0 || zeroBasedPage < 0 || dpi <= 0 || maximumSide <= 0)
            {
                return null;
            }

            // Strict region validation: finite, normalized, inside the page.
            var hasRegion = normalizedRegion != null && normalizedRegion.Length >= 4;
            if (hasRegion)
            {
                var x = normalizedRegion[0];
                var y = normalizedRegion[1];
                var w = normalizedRegion[2];
                var h = normalizedRegion[3];
                if (!double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(w) || !double.IsFinite(h)
                    || x < 0 || y < 0 || w <= 0 || h <= 0
                    || (x + w) > 1.000001 || (y + h) > 1.000001)
                {
                    return null;
                }
            }

            var size = Conversion.GetPageSize(pdf, zeroBasedPage, null);
            if (size.Width <= 0 || size.Height <= 0)
            {
                return null;
            }

            // Bounds are expressed in PDF points (72 dpi units) relative to the page.
            System.Drawing.RectangleF? bounds = null;
            double regionWidthPts = size.Width;
            double regionHeightPts = size.Height;
            if (hasRegion)
            {
                var x = normalizedRegion[0];
                var y = normalizedRegion[1];
                var w = normalizedRegion[2];
                var h = normalizedRegion[3];
                bounds = new System.Drawing.RectangleF(
                    (float)(x * size.Width),
                    (float)(y * size.Height),
                    (float)(w * size.Width),
                    (float)(h * size.Height));
                regionWidthPts = w * size.Width;
                regionHeightPts = h * size.Height;
            }

            // Effective DPI: full pages rasterize at the requested DPI (downscaled to
            // maximumSide); detail crops raise DPI so the region's longest side reaches
            // maximumSide — real sampling density, not a resized thumbnail.
            var effectiveDpi = (float)dpi;
            if (hasRegion)
            {
                var longestPts = Math.Max(regionWidthPts, regionHeightPts);
                if (longestPts > 0)
                {
                    effectiveDpi = (float)Math.Min(MaxCropDpi, maximumSide * 72.0 / longestPts);
                }
            }

            var pxW = (float)regionWidthPts * effectiveDpi / 72f;
            var pxH = (float)regionHeightPts * effectiveDpi / 72f;
            if (!hasRegion)
            {
                var scale = maximumSide / Math.Max(pxW, pxH);
                if (scale < 1f)
                {
                    pxW *= scale;
                    pxH *= scale;
                }
            }
            else
            {
                pxW = Math.Min(pxW, maximumSide);
                pxH = Math.Min(pxH, maximumSide);
            }

            var options = new RenderOptions(
                Dpi: (int)Math.Round(effectiveDpi),
                Width: Math.Max(1, (int)Math.Round(pxW)),
                Height: Math.Max(1, (int)Math.Round(pxH)),
                WithAnnotations: true,
                WithFormFill: true,
                WithAspectRatio: true,
                Rotation: PdfRotation.Rotate0,
                AntiAliasing: PdfAntiAliasing.All,
                BackgroundColor: SKColors.White,
                Bounds: bounds,
                Grayscale: false,
                UseTiling: false,
                DpiRelativeToBounds: true);

            using var output = new MemoryStream();
            Conversion.SavePng(output, pdf, zeroBasedPage, null, options);
            return output.ToArray();
        }

        /// <summary>
        /// Returns flattened visible page dimensions in points: [w0, h0, w1, h1, ...].
        /// </summary>
        /// <param name="pdf">PDF document bytes.</param>
        /// <returns>Flattened width/height pairs, or null on invalid input.</returns>
        public static double[] GetPageSizes(byte[] pdf)
        {
            if (pdf == null || pdf.Length == 0)
            {
                return null;
            }

            var sizes = Conversion.GetPageSizes(pdf, null);
            var result = new double[sizes.Count * 2];
            for (var i = 0; i < sizes.Count; i++)
            {
                result[i * 2] = sizes[i].Width;
                result[(i * 2) + 1] = sizes[i].Height;
            }

            return result;
        }
    }
}
