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
using Newtonsoft.Json.Linq;

namespace SmartHopper.ProviderSdk.AICall.Core.Interactions
{
    /// <summary>
    /// Single shared JSON codec for <see cref="SourceContentReference"/> full-provenance
    /// serialization — used by the payload codec, modality-fallback evidence entries and
    /// document DTOs so source identity is serialized identically everywhere.
    /// </summary>
    public static class SourceContentReferenceJsonCodec
    {
        /// <summary>Serializes the reference to a full-fidelity <see cref="JObject"/> (null when <paramref name="source"/> is null).</summary>
        public static JObject Encode(SourceContentReference source)
            => Encode(source, includeContextText: true);

        /// <summary>
        /// Serializes the reference. When <paramref name="includeContextText"/> is false the
        /// potentially large <c>contextText</c> payload is omitted — the compact projection
        /// used by JSON descriptors/evidence entries that would otherwise duplicate entire
        /// article text once per image.
        /// </summary>
        /// <param name="source">The reference to serialize.</param>
        /// <param name="includeContextText">Whether to carry <c>contextText</c>.</param>
        /// <returns>The encoded <see cref="JObject"/>, or null when <paramref name="source"/> is null.</returns>
        public static JObject Encode(SourceContentReference source, bool includeContextText)
        {
            if (source == null)
            {
                return null;
            }

            return new JObject
            {
                ["documentId"] = source.DocumentId,
                ["sourceName"] = source.SourceName,
                ["pageNumber"] = source.PageNumber,
                ["assetId"] = source.AssetId,
                ["figureLabel"] = source.FigureLabel,
                ["caption"] = source.Caption,
                ["contextText"] = includeContextText ? source.ContextText : null,
                ["kind"] = source.Kind.ToString().ToLowerInvariant(),
                ["region"] = source.Region == null
                    ? null
                    : new JObject
                    {
                        ["x"] = source.Region.X,
                        ["y"] = source.Region.Y,
                        ["width"] = source.Region.Width,
                        ["height"] = source.Region.Height,
                    },
            };
        }

        /// <summary>Deserializes a reference (null when <paramref name="json"/> is null).</summary>
        public static SourceContentReference Decode(JObject json)
        {
            if (json == null)
            {
                return null;
            }

            var regionJson = json["region"] as JObject;
            return new SourceContentReference
            {
                DocumentId = json.Value<string>("documentId"),
                SourceName = json.Value<string>("sourceName"),
                PageNumber = json.Value<int?>("pageNumber"),
                AssetId = json.Value<string>("assetId"),
                FigureLabel = json.Value<string>("figureLabel"),
                Caption = json.Value<string>("caption"),
                ContextText = json.Value<string>("contextText"),
                Kind = Enum.TryParse<SourceContentKind>(json.Value<string>("kind") ?? string.Empty, true, out var kind) ? kind : SourceContentKind.Unknown,
                Region = regionJson == null
                    ? null
                    : new SourceContentRegion
                    {
                        X = regionJson.Value<double>("x"),
                        Y = regionJson.Value<double>("y"),
                        Width = regionJson.Value<double>("width"),
                        Height = regionJson.Value<double>("height"),
                    },
            };
        }
    }
}
