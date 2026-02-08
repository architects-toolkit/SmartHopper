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
using System.Globalization;
using Grasshopper.Kernel.Types;

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
