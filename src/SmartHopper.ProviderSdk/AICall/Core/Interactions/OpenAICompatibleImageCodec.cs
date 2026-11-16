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
using SmartHopper.ProviderSdk.AICall.Core.Base;

namespace SmartHopper.ProviderSdk.AICall.Core.Interactions
{
    /// <summary>
    /// Shared helpers to encode <see cref="ToolResultImage"/> payloads into OpenAI-compatible
    /// chat message parts. Chat Completions-style APIs cannot attach images to
    /// <c>role:"tool"</c> messages, so images are emitted as a trailing <c>role:"user"</c>
    /// message containing <c>image_url</c> content parts with data URIs.
    /// </summary>
    public static class OpenAICompatibleImageCodec
    {
        /// <summary>
        /// Builds a single OpenAI-compatible <c>image_url</c> content part.
        /// </summary>
        /// <param name="image">The tool result image to encode.</param>
        /// <returns>A content part JObject with a data URI, or null when the image has no data.</returns>
        public static JObject ToImageUrlPart(ToolResultImage image)
        {
            if (image == null || string.IsNullOrEmpty(image.ImageData))
            {
                return null;
            }

            var mimeType = string.IsNullOrEmpty(image.MimeType) ? "image/png" : image.MimeType;
            return new JObject
            {
                ["type"] = "image_url",
                ["image_url"] = new JObject
                {
                    ["url"] = $"data:{mimeType};base64,{image.ImageData}",
                },
            };
        }

        /// <summary>
        /// Builds a <c>role:"user"</c> message containing one <c>image_url</c> part per image.
        /// </summary>
        /// <param name="images">Images to encode (typically <see cref="AIInteractionToolCallExtensions.GetModelImages"/> output).</param>
        /// <returns>A user message JObject, or null when no image has data.</returns>
        public static JObject ToUserImageMessage(IEnumerable<ToolResultImage> images)
        {
            if (images == null)
            {
                return null;
            }

            var parts = new JArray();
            foreach (var image in images)
            {
                var part = ToImageUrlPart(image);
                if (part != null)
                {
                    parts.Add(part);
                }
            }

            if (parts.Count == 0)
            {
                return null;
            }

            return new JObject
            {
                ["role"] = "user",
                ["content"] = parts,
            };
        }

        /// <summary>
        /// Extracts generated images from an OpenAI-compatible response <c>message</c> object.
        /// Image-capable chat models emit <c>message.images</c> as an array of
        /// <c>{"type":"image_url","image_url":{"url":"..."}}</c> entries where the URL is
        /// either a data URI (<c>data:&lt;mime&gt;;base64,&lt;data&gt;</c>) or a remote link.
        /// </summary>
        /// <param name="message">The response <c>message</c> JObject.</param>
        /// <returns>One <see cref="AIInteractionImage"/> per decodable entry; empty when absent.</returns>
        public static List<AIInteractionImage> ExtractImages(JObject message)
        {
            var results = new List<AIInteractionImage>();
            if (!(message?["images"] is JArray images))
            {
                return results;
            }

            foreach (var entry in images.OfType<JObject>())
            {
                var url = entry["image_url"]?["url"]?.ToString() ?? entry["url"]?.ToString();
                if (string.IsNullOrEmpty(url))
                {
                    continue;
                }

                if (TrySplitDataUri(url, out var mimeType, out var base64))
                {
                    results.Add(new AIInteractionImage { Agent = AIAgent.Assistant, ImageData = base64, MimeType = mimeType });
                }
                else if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
                {
                    results.Add(new AIInteractionImage { Agent = AIAgent.Assistant, ImageUrl = uri });
                }
            }

            return results;
        }

        /// <summary>
        /// Splits a <c>data:&lt;mime&gt;;base64,&lt;data&gt;</c> URI into its parts.
        /// </summary>
        private static bool TrySplitDataUri(string url, out string mimeType, out string base64)
        {
            mimeType = null;
            base64 = null;
            const string DataPrefix = "data:";
            const string Base64Marker = ";base64,";

            if (!url.StartsWith(DataPrefix, StringComparison.Ordinal))
            {
                return false;
            }

            var markerIndex = url.IndexOf(Base64Marker, StringComparison.Ordinal);
            if (markerIndex < 0)
            {
                return false;
            }

            mimeType = url.Substring(DataPrefix.Length, markerIndex - DataPrefix.Length);
            base64 = url.Substring(markerIndex + Base64Marker.Length);
            return base64.Length > 0;
        }
    }
}
