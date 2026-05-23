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

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SmartHopper.Core.Grasshopper.Utils.Parsing
{
    /// <summary>
    /// Centralized resolver for AI text → <see cref="JToken"/> (JObject or JArray) using the
    /// robust <see cref="AIResponseParser.ParseJsonFromResponse"/> (handles markdown
    /// code-block wrapping, prefatory text, trailing garbage, etc.).
    /// </summary>
    /// <remarks>
    /// Designed for tools like <c>text2json</c> that consume free-form AI responses and need a
    /// validated JToken. Schema-aware tools (e.g. <c>script_generate</c>, <c>script_edit</c>)
    /// have richer correction loops and intentionally do not delegate to this resolver.
    /// </remarks>
    public static class JsonResultResolver
    {
        /// <summary>
        /// Outcome of a JSON parse attempt.
        /// </summary>
        public readonly struct ParseOutcome
        {
            /// <summary>The parsed token, or <c>null</c> on failure (and no fallback supplied).</summary>
            public JToken Value { get; }

            /// <summary>True when parsing failed and the fallback (if any) was used.</summary>
            public bool UsedFallback { get; }

            /// <summary>True when parsing succeeded.</summary>
            public bool Success { get; }

            /// <summary>Parser error message, when <see cref="Success"/> is false.</summary>
            public string Error { get; }

            internal ParseOutcome(JToken value, bool usedFallback, bool success, string error)
            {
                this.Value = value;
                this.UsedFallback = usedFallback;
                this.Success = success;
                this.Error = error;
            }
        }

        /// <summary>
        /// Robustly parses a JSON token from raw AI response text.
        /// </summary>
        /// <param name="aiResponseText">The raw assistant response text.</param>
        /// <param name="fallback">Optional fallback token to return on parse failure.</param>
        public static ParseOutcome Resolve(string aiResponseText, JToken fallback = null)
        {
            try
            {
                var parsed = AIResponseParser.ParseJsonFromResponse(aiResponseText);
                return new ParseOutcome(parsed, false, true, null);
            }
            catch (JsonException ex)
            {
                return new ParseOutcome(fallback, fallback != null, false, ex.Message);
            }
        }

        /// <summary>
        /// Builds a <c>{ "json", "usedFallback" }</c> tool result JObject with the parsed JSON
        /// serialized as a compact string under <c>json</c>.
        /// </summary>
        /// <param name="aiResponseText">The raw assistant response text.</param>
        /// <param name="fallback">Optional fallback token to use on parse failure.</param>
        /// <param name="error">Out parameter populated with the parse error on failure (or <c>null</c>).</param>
        /// <returns>The tool result JObject, or <c>null</c> if parsing failed and no fallback was supplied.</returns>
        public static JObject BuildToolResult(string aiResponseText, JToken fallback, out string error)
        {
            var outcome = Resolve(aiResponseText, fallback);
            error = outcome.Error;
            if (outcome.Value == null)
            {
                return null;
            }

            return new JObject
            {
                ["json"] = outcome.Value.ToString(Formatting.None),
                ["usedFallback"] = outcome.UsedFallback,
            };
        }
    }
}
