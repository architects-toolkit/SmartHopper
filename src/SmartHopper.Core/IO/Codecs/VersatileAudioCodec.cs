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
using Grasshopper.Kernel.Types;
using Newtonsoft.Json.Linq;
using SmartHopper.Core.Types;

namespace SmartHopper.Core.IO.Codecs
{
    /// <summary>
    /// Codec for GH_VersatileAudio goo values.
    /// </summary>
    internal sealed class VersatileAudioCodec : IGooCodec
    {
        /// <inheritdoc/>
        public string TypeHint => "GH_VersatileAudio";

        /// <inheritdoc/>
        public int Priority => 0;

        /// <inheritdoc/>
        public bool CanEncode(IGH_Goo goo) => goo is GH_VersatileAudio aud && aud.Value != null;

        /// <inheritdoc/>
        public string Encode(IGH_Goo goo)
        {
            var aud = ((GH_VersatileAudio)goo).Value;
            var json = new JObject();
            json["k"] = aud.Kind.ToString();
            json["v"] = aud.RawValue;
            json["i"] = aud.Id;
            json["c"] = aud.Context;
            json["p"] = aud.PageOrSlide;
            json["s"] = aud.SourceDocument;
            json["m"] = aud.MimeType ?? "audio/mpeg";
            return json.ToString(Newtonsoft.Json.Formatting.None);
        }

        /// <inheritdoc/>
        public bool TryDecode(string data, out IGH_Goo goo, out string warning)
        {
            warning = null;
            try
            {
                var json = JObject.Parse(data);
                var kind = (VersatileAudioKind)Enum.Parse(typeof(VersatileAudioKind), json.Value<string>("k"));
                var aud = VersatileAudio.FromDeserialized(
                    kind,
                    json.Value<string>("v"),
                    json.Value<string>("m"),
                    json.Value<string>("i"),
                    json.Value<string>("c"),
                    json.Value<int>("p"),
                    json.Value<string>("s"));
                goo = new GH_VersatileAudio(aud);
                return true;
            }
            catch (Exception ex)
            {
                goo = new GH_String(data);
                warning = $"Failed to decode GH_VersatileAudio; restored as GH_String. {ex.Message}";
                return true;
            }
        }
    }
}
