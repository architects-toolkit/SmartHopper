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

namespace SmartHopper.Core.Grasshopper.Utils.Parsing
{
    /// <summary>
    /// Centralized resolver for AI text / JSON tokens → <c>List&lt;string&gt;</c>.
    /// Wraps the lenient <see cref="AIResponseParser.ParseStringArrayFromResponse"/> with
    /// JArray fast-paths and an optional fallback list for unparseable responses.
    /// </summary>
    public static class StringListResultResolver
    {
        /// <summary>
        /// Resolves a list of strings from raw AI response text.
        /// </summary>
        /// <param name="aiResponseText">The raw assistant response text.</param>
        /// <param name="fallback">Optional fallback list to return when parsing fails or yields an empty result.</param>
        /// <returns>A tuple with the resolved list (never null) and whether the fallback was applied.</returns>
        public static (List<string> Value, bool UsedFallback) Resolve(string aiResponseText, IReadOnlyList<string> fallback = null)
        {
            var parsed = AIResponseParser.ParseStringArrayFromResponse(aiResponseText);
            if (parsed != null && parsed.Count > 0)
            {
                return (parsed, false);
            }

            return (fallback?.ToList() ?? new List<string>(), fallback != null);
        }

        /// <summary>
        /// Resolves a list of strings from a JSON token. Optimized fast-path for <see cref="JArray"/>;
        /// otherwise falls back to lenient parsing of the token's string representation.
        /// </summary>
        /// <param name="token">JSON token (typically the value of a tool argument).</param>
        /// <param name="fallback">Optional fallback list when parsing fails.</param>
        public static (List<string> Value, bool UsedFallback) ResolveFromToken(JToken token, IReadOnlyList<string> fallback = null)
        {
            if (token is JArray array)
            {
                return (array.Select(t => t.ToString()).ToList(), false);
            }

            return Resolve(token?.ToString(), fallback);
        }

        /// <summary>
        /// Convenience overload that returns just the list (no usedFallback flag), defaulting to
        /// an empty list when the response is unparseable. Equivalent to the previous inline
        /// pattern <c>token is JArray a ? ... : AIResponseParser.ParseStringArrayFromResponse(...)</c>.
        /// </summary>
        public static List<string> ParseOrEmpty(JToken token)
        {
            return ResolveFromToken(token).Value;
        }
    }
}
