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
using System.Drawing;
using System.IO;
using Grasshopper.Kernel.Types;
using Newtonsoft.Json.Linq;
using SmartHopper.Core.Types;

namespace SmartHopper.Core.IO.Codecs
{
    /// <summary>
    /// Codec for GH_VersatileImage goo values.
    /// </summary>
    internal sealed class VersatileImageCodec : IGooCodec
    {
        /// <inheritdoc/>
        public string TypeHint => "GH_VersatileImage";

        /// <inheritdoc/>
        public int Priority => 0;

        /// <inheritdoc/>
        public bool CanEncode(IGH_Goo goo) => goo is GH_VersatileImage img && img.Value != null;

        /// <inheritdoc/>
        public string Encode(IGH_Goo goo)
        {
            var img = ((GH_VersatileImage)goo).Value;
            var json = new JObject();
            json["k"] = img.Kind.ToString();
            json["v"] = img.RawValue;
            if (img.Kind == VersatileImageKind.Bitmap && img.Bitmap != null)
            {
                using (var ms = new MemoryStream())
                {
                    img.Bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                    json["b"] = Convert.ToBase64String(ms.ToArray());
                }
            }

            json["i"] = img.Id;
            json["c"] = img.Context;
            json["p"] = img.PageOrSlide;
            json["s"] = img.SourceDocument;
            json["m"] = img.MimeType ?? "image/png";
            return json.ToString(Newtonsoft.Json.Formatting.None);
        }

        /// <inheritdoc/>
        public bool TryDecode(string data, out IGH_Goo goo, out string warning)
        {
            warning = null;
            try
            {
                var json = JObject.Parse(data);
                var kind = (VersatileImageKind)Enum.Parse(typeof(VersatileImageKind), json.Value<string>("k"));
                Bitmap bitmap = null;
                if (kind == VersatileImageKind.Bitmap && json["b"] != null)
                {
                    var bytes = Convert.FromBase64String(json.Value<string>("b"));
                    using (var ms = new MemoryStream(bytes))
                    {
                        bitmap = new Bitmap(ms);
                    }
                }

                var img = VersatileImage.FromDeserialized(
                    kind,
                    json.Value<string>("v"),
                    bitmap,
                    json.Value<string>("i"),
                    json.Value<string>("c"),
                    json.Value<int>("p"),
                    json.Value<string>("s"),
                    json.Value<string>("m"));
                goo = new GH_VersatileImage(img);
                return true;
            }
            catch (Exception ex)
            {
                goo = new GH_String(data);
                warning = $"Failed to decode GH_VersatileImage; restored as GH_String. {ex.Message}";
                return true;
            }
        }
    }
}
