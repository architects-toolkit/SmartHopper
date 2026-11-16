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

using System.Globalization;

namespace SmartHopper.ProviderSdk.AICall.Core.Interactions
{
    /// <summary>
    /// Provider-agnostic parsing for the shared image-generation inputs (<c>size</c>,
    /// <c>aspect_ratio</c>) carried by request-mode <see cref="AIInteractionImage"/> interactions.
    /// Accepted forms: explicit dimensions (<c>1024x1024</c>), resolution tiers
    /// (<c>1K</c>/<c>2K</c>/<c>4K</c>), and <c>W:H</c> aspect ratios. Each provider owns the
    /// mapping from these concepts to its native size vocabulary.
    /// </summary>
    public static class ImageGenerationSpec
    {
        /// <summary>Parses a <c>WxH</c> size string into pixel dimensions.</summary>
        public static bool TryParseDimensions(string size, out int width, out int height)
        {
            width = 0;
            height = 0;
            if (string.IsNullOrWhiteSpace(size))
            {
                return false;
            }

            var parts = size.Trim().ToLowerInvariant().Split('x');
            return parts.Length == 2 &&
                   int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out width) &&
                   int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out height) &&
                   width > 0 && height > 0;
        }

        /// <summary>Parses a <c>W:H</c> ratio string into integer parts.</summary>
        public static bool TryParseRatio(string ratio, out int w, out int h)
        {
            w = 0;
            h = 0;
            if (string.IsNullOrWhiteSpace(ratio))
            {
                return false;
            }

            var parts = ratio.Trim().Split(':');
            return parts.Length == 2 &&
                   int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out w) &&
                   int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out h) &&
                   w > 0 && h > 0;
        }

        /// <summary>
        /// Resolves the effective <c>W:H</c> aspect ratio: the explicit input when it parses,
        /// otherwise the ratio implied by a <c>WxH</c> size. Returns null when neither yields one.
        /// </summary>
        public static string ResolveAspectRatio(string aspectRatio, string size)
        {
            if (TryParseRatio(aspectRatio, out var w, out var h))
            {
                return $"{w}:{h}";
            }

            if (TryParseDimensions(size, out var dw, out var dh))
            {
                var g = Gcd(dw, dh);
                return $"{dw / g}:{dh / g}";
            }

            return null;
        }

        /// <summary>
        /// Maps a resolution tier or <c>WxH</c> size to the pixel size of its longest side.
        /// Tiers: <c>1K</c> → 1024, <c>2K</c> → 2048, <c>4K</c> → 4096. Returns null when the
        /// input is neither a tier nor dimensions.
        /// </summary>
        public static int? ResolveMaxDimension(string size)
        {
            if (string.IsNullOrWhiteSpace(size))
            {
                return null;
            }

            return size.Trim().ToUpperInvariant() switch
            {
                "1K" => 1024,
                "2K" => 2048,
                "4K" => 4096,
                _ => TryParseDimensions(size, out var w, out var h) ? System.Math.Max(w, h) : (int?)null,
            };
        }

        /// <summary>
        /// Picks the closest candidate ratio (from a provider-specific allowed set) to the
        /// target <c>W:H</c> ratio by numeric distance. Returns null when no candidate parses.
        /// </summary>
        public static string SnapToClosestRatio(string ratio, params string[] candidates)
        {
            if (!TryParseRatio(ratio, out var tw, out var th))
            {
                return null;
            }

            var target = tw / (double)th;
            string best = null;
            var bestDist = double.MaxValue;
            foreach (var candidate in candidates)
            {
                if (!TryParseRatio(candidate, out var cw, out var ch))
                {
                    continue;
                }

                var dist = System.Math.Abs((cw / (double)ch) - target);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = candidate;
                }
            }

            return best;
        }

        private static int Gcd(int a, int b) => b == 0 ? a : Gcd(b, a % b);
    }
}
