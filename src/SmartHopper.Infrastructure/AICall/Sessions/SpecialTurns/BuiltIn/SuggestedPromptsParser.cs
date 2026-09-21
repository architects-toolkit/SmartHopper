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

namespace SmartHopper.Infrastructure.AICall.Sessions.SpecialTurns.BuiltIn
{
    using System;
    using System.Collections.Generic;
    using Newtonsoft.Json.Linq;
    /// <summary>
    /// Tolerant parser for the suggested-prompts special turn output. Accepts the schema
    /// shape (<c>{"suggestions": [...]}</c>) and, as a fallback, a bare JSON array — even when
    /// wrapped in code fences or surrounding prose. Any unparseable input yields an empty list.
    /// </summary>
    public static class SuggestedPromptsParser
    {
        private const int MaxSuggestionLength = 140;

        /// <summary>
        /// Parses raw model output into at most <see cref="SuggestedPromptsSpecialTurn.MaxSuggestions"/>
        /// non-empty, distinct suggestion strings.
        /// </summary>
        /// <param name="raw">The raw text returned by the special turn.</param>
        /// <returns>The parsed suggestions, or an empty list when nothing usable was produced.</returns>
        public static IReadOnlyList<string> Parse(string? raw)
        {
            var suggestions = new List<string>();
            if (string.IsNullOrWhiteSpace(raw))
            {
                return suggestions;
            }

            try
            {
                var text = StripCodeFence(raw.Trim());
                var token = TryParse(text)
                    ?? TryParse(ExtractJson(text, '{', '}'))
                    ?? TryParse(ExtractJson(text, '[', ']'));
                var items = token switch
                {
                    JObject obj => obj["suggestions"] as JArray,
                    JArray array => array,
                    _ => null,
                };

                if (items == null)
                {
                    return suggestions;
                }

                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var item in items)
                {
                    var suggestion = item?.Type == JTokenType.String ? item.ToString().Trim() : string.Empty;
                    if (string.IsNullOrWhiteSpace(suggestion) || !seen.Add(suggestion))
                    {
                        continue;
                    }

                    if (suggestion.Length > MaxSuggestionLength)
                    {
                        suggestion = suggestion.Substring(0, MaxSuggestionLength).TrimEnd();
                    }

                    suggestions.Add(suggestion);
                    if (suggestions.Count >= SuggestedPromptsSpecialTurn.MaxSuggestions)
                    {
                        break;
                    }
                }
            }
            catch (Exception)
            {
                // Defensive: malformed model output must never surface as a chat error.
                return new List<string>();
            }

            return suggestions;
        }

        private static JToken? TryParse(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            try
            {
                return JToken.Parse(text);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Extracts the outermost JSON substring delimited by the given open/close characters.
        /// </summary>
        private static string? ExtractJson(string text, char open, char close)
        {
            var start = text.IndexOf(open);
            var end = text.LastIndexOf(close);
            if (start < 0 || end <= start)
            {
                return null;
            }

            return text.Substring(start, end - start + 1);
        }

        private static string StripCodeFence(string text)
        {
            if (!text.StartsWith("```", StringComparison.Ordinal))
            {
                return text;
            }

            var firstLineEnd = text.IndexOf('\n');
            if (firstLineEnd < 0)
            {
                return text;
            }

            var body = text.Substring(firstLineEnd + 1);
            var fence = body.LastIndexOf("```", StringComparison.Ordinal);
            return fence >= 0 ? body.Substring(0, fence).Trim() : body.Trim();
        }
    }
}
