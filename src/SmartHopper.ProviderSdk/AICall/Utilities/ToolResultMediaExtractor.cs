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
using Newtonsoft.Json.Linq;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;

namespace SmartHopper.ProviderSdk.AICall.Utilities
{
    /// <summary>
    /// Extracts image payloads from tool-result JSON into <see cref="ToolResultImage"/> objects.
    /// Convention: a tool result may carry "imageBase64" + "mimeType" ("image/*") + optional
    /// "imageAudience" ("model" | "display", default "display"). The extractor removes the
    /// base64 payload from the persisted JSON so it is never serialized to providers as text,
    /// while keeping "mimeType", "width", "height", "savedTo" and "imageAudience" as metadata
    /// (the audience tells the model whether the image was actually sent to it) plus an
    /// "imageAttached" marker.
    /// </summary>
    public static class ToolResultMediaExtractor
    {
        /// <summary>
        /// Splits a tool-result object into a compact JSON body (no base64 image data) and a
        /// list of extracted images. Returns false when the result contains no image payload;
        /// <paramref name="compact"/> then references the original object unmodified.
        /// </summary>
        /// <param name="result">The tool-result JSON as produced by the tool.</param>
        /// <param name="compact">The result without base64 image data (new object; original untouched).</param>
        /// <param name="images">Extracted images; empty when none.</param>
        /// <returns>True when an image payload was extracted.</returns>
        public static bool TrySplit(JObject result, out JObject compact, out List<ToolResultImage> images)
        {
            images = new List<ToolResultImage>();

            if (result == null)
            {
                compact = null;
                return false;
            }

            var imageData = result["imageBase64"]?.Value<string>();
            var mimeType = result["mimeType"]?.Value<string>();

            if (string.IsNullOrEmpty(imageData) ||
                string.IsNullOrEmpty(mimeType) ||
                !mimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                compact = result;
                return false;
            }

            var audience = result["imageAudience"]?.Value<string>();
            var sendToModel = string.Equals(audience, "model", StringComparison.OrdinalIgnoreCase);

            images.Add(new ToolResultImage
            {
                ImageData = imageData,
                MimeType = mimeType,
                SendToModel = sendToModel,
                Width = result["width"]?.Value<int>(),
                Height = result["height"]?.Value<int>(),
            });

            compact = (JObject)result.DeepClone();
            compact.Remove("imageBase64");
            compact["imageAttached"] = true;

            return true;
        }
    }
}
