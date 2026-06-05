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

// Purpose: Encode/decode IGH_Goo instances to a canonical string payload to allow safe, versioned persistence
// without invoking GH internal type cache on read. Supports common primitives and safely falls back to GH_String.
using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using Grasshopper.Kernel.Types;
using Newtonsoft.Json.Linq;
using SmartHopper.Core.Types;

namespace SmartHopper.Core.IO
{
    /// <summary>
    /// Provides safe encoding/decoding between IGH_Goo and string payloads.
    /// </summary>
    public static class SafeGooCodec
    {
        private const string Sep = "|"; // simple separator: typeHint|serialized

        /// <summary>
        /// Encodes a goo into a canonical payload string with a type hint prefix.
        /// </summary>
        public static string Encode(IGH_Goo goo)
        {
            if (goo == null)
                return "null";

            switch (goo)
            {
                case GH_String s:
                    return $"GH_String{Sep}{s.Value ?? string.Empty}";
                case GH_Number n:
                    return $"GH_Number{Sep}{n.Value.ToString(CultureInfo.InvariantCulture)}";
                case GH_Integer i:
                    return $"GH_Integer{Sep}{i.Value}";
                case GH_Boolean b:
                    return $"GH_Boolean{Sep}{(b.Value ? "1" : "0")}";
                case GH_VersatileImage imgGoo when imgGoo.Value != null:
                {
                    var img = imgGoo.Value;
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
                    return $"GH_VersatileImage{Sep}{json.ToString(Newtonsoft.Json.Formatting.None)}";
                }

                case GH_VersatileAudio audGoo when audGoo.Value != null:
                {
                    var aud = audGoo.Value;
                    var json = new JObject();
                    json["k"] = aud.Kind.ToString();
                    json["v"] = aud.RawValue;
                    json["i"] = aud.Id;
                    json["c"] = aud.Context;
                    json["p"] = aud.PageOrSlide;
                    json["s"] = aud.SourceDocument;
                    json["m"] = aud.MimeType ?? "audio/mpeg";
                    return $"GH_VersatileAudio{Sep}{json.ToString(Newtonsoft.Json.Formatting.None)}";
                }

                default:
                    // Best-effort: use the goo string representation
                    return $"GH_String{Sep}{goo.ToString() ?? string.Empty}";
            }
        }

        /// <summary>
        /// Attempts to decode a payload into an IGH_Goo. For unknown types returns a GH_String.
        /// Never throws.
        /// </summary>
        public static bool TryDecode(string payload, out IGH_Goo goo, out string warning)
        {
            goo = null;
            warning = null;

            if (string.IsNullOrEmpty(payload))
            {
                goo = new GH_String(string.Empty);
                return true;
            }

            var idx = payload.IndexOf(Sep, StringComparison.Ordinal);
            if (idx <= 0)
            {
                goo = new GH_String(payload);
                return true;
            }

            var typeHint = payload.Substring(0, idx);
            var data = payload.Substring(idx + Sep.Length);

            try
            {
                switch (typeHint)
                {
                    case "GH_String":
                        goo = new GH_String(data);

                        return true;

                    case "GH_Number":
                        if (double.TryParse(data, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
                        {
                            goo = new GH_Number(d);
                            return true;
                        }

                        goo = new GH_String(data);
                        warning = "Failed to parse GH_Number; restored as GH_String.";

                        return true;

                    case "GH_Integer":
                        if (int.TryParse(data, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i))
                        {
                            goo = new GH_Integer(i);
                            return true;
                        }

                        goo = new GH_String(data);
                        warning = "Failed to parse GH_Integer; restored as GH_String.";

                        return true;

                    case "GH_Boolean":
                        if (data == "1" || data.Equals("true", StringComparison.OrdinalIgnoreCase))
                        {
                            goo = new GH_Boolean(true);
                            return true;
                        }

                        if (data == "0" || data.Equals("false", StringComparison.OrdinalIgnoreCase))
                        {
                            goo = new GH_Boolean(false);
                            return true;
                        }

                        goo = new GH_String(data);
                        warning = "Failed to parse GH_Boolean; restored as GH_String.";

                        return true;

                    case "GH_VersatileImage":
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

                    case "GH_VersatileAudio":
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

                    default:
                        goo = new GH_String(data);
                        warning = $"Unknown type hint '{typeHint}'; restored as GH_String.";

                        return true;
                }
            }
            catch (Exception ex)
            {
                goo = new GH_String(data);
                warning = $"Exception decoding payload: {ex.Message}; restored as GH_String.";

                return true;
            }
        }
    }
}
