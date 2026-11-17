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

using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace SmartHopper.DocumentProcessing
{
    /// <summary>
    /// Single strict parser for PDF page/crop selections used by the <c>file2md</c>
    /// tool schema and the optional component inputs. Rules: <c>pages</c> is a non-empty
    /// array of unique 1-based integers (omitted means the whole document, empty is
    /// invalid); <c>region</c> is an object with all of <c>page</c>/<c>x</c>/<c>y</c>/
    /// <c>width</c>/<c>height</c> present — page an integer ≥ 1, coordinates finite,
    /// width/height positive, the box fully inside the normalized page. Missing or
    /// malformed values are errors, never silently defaulted.
    /// </summary>
    public static class DocumentSelectionJsonParser
    {
        /// <summary>Parses the optional <c>pages</c> token into a sorted unique list.
        /// Null token means omitted; returns false with an error otherwise invalid.</summary>
        public static bool TryParsePages(JToken token, out List<int> pages, out string error)
        {
            pages = null;
            error = null;
            if (token == null || token.Type == JTokenType.Null)
            {
                return true;
            }

            if (token is not JArray array || array.Count == 0)
            {
                error = "'pages' must be a non-empty array of unique 1-based page numbers (omit for the whole document).";
                return false;
            }

            var list = new List<int>();
            foreach (var item in array)
            {
                if (item.Type != JTokenType.Integer || !TryInt32(item, out var page) || page < 1)
                {
                    error = "'pages' entries must be unique integers ≥ 1 (no fractional or non-positive values).";
                    return false;
                }

                list.Add(page);
            }

            if (list.Distinct().Count() != list.Count)
            {
                error = "'pages' must not contain duplicates.";
                return false;
            }

            pages = list.OrderBy(p => p).ToList();
            return true;
        }

        /// <summary>Parses the optional <c>region</c> token into a
        /// <see cref="DocumentRegionSelection"/>; every field must be present and valid.</summary>
        public static bool TryParseRegion(JToken token, out DocumentRegionSelection region, out string error)
        {
            region = null;
            error = null;
            if (token == null || token.Type == JTokenType.Null)
            {
                return true;
            }

            if (token is not JObject obj)
            {
                error = "'region' must be an object with 'page' and normalized 'x', 'y', 'width', 'height'.";
                return false;
            }

            if (obj["page"]?.Type != JTokenType.Integer || !TryInt32(obj["page"], out var page) || page < 1
                || !TryField(obj, "x", out var x) || !TryField(obj, "y", out var y)
                || !TryField(obj, "width", out var w) || !TryField(obj, "height", out var h)
                || w <= 0 || h <= 0 || x < 0 || y < 0 || x + w > 1 || y + h > 1)
            {
                error = "'region' must be an object with integer 'page' ≥ 1 and normalized finite 'x', 'y', 'width', 'height' (all present, positive, box inside the page).";
                return false;
            }

            region = new DocumentRegionSelection
            {
                Page = page,
                X = x,
                Y = y,
                Width = w,
                Height = h,
            };
            return true;
        }

        /// <summary>Strict int32 coercion: integral tokens that overflow int32 are a
        /// validation failure, never a thrown overflow exception.</summary>
        private static bool TryInt32(JToken token, out int value)
        {
            value = 0;
            try
            {
                var d = token.Value<double>();
                if (!double.IsFinite(d) || d < 1 || d > int.MaxValue || d != System.Math.Floor(d))
                {
                    return false;
                }

                value = (int)d;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryField(JObject obj, string name, out double value)
        {
            value = 0;
            var token = obj[name];
            if (token == null || (token.Type != JTokenType.Float && token.Type != JTokenType.Integer))
            {
                return false;
            }

            var d = token.Value<double>();
            if (!double.IsFinite(d))
            {
                return false;
            }

            value = d;
            return true;
        }
    }
}
