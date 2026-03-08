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
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using Grasshopper.Kernel.Types;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SmartHopper.Core.Grasshopper.Utils.Parsing
{
    /// <summary>
    /// Helper methods for parsing AI responses into specific data types.
    /// </summary>
    public static partial class AIResponseParser
    {
        #region Compiled Regex Patterns

        /// <summary>
        /// Regex pattern for extracting content from markdown code blocks.
        /// </summary>
        [GeneratedRegex(@"```(?:json|txt|text)?\s*\n?(.*?)\n?```", RegexOptions.Singleline)]
        private static partial Regex MarkdownCodeBlockRegex();

        /// <summary>
        /// Regex pattern for extracting the first bracketed array from text.
        /// </summary>
        [GeneratedRegex(@"\[([^\[\]]*)\]")]
        private static partial Regex FirstBracketedArrayRegex();

        /// <summary>
        /// Regex pattern for matching range notation (N-M or N..M).
        /// </summary>
        [GeneratedRegex(@"(\d+)(?:-|\.\.)(\d+)")]
        private static partial Regex RangeRegex();

        #endregion

        #region Response Parsing

        /// <summary>
        /// Parses a boolean value from the AI response.
        /// </summary>
        /// <param name="response">Raw response from the AI.</param>
        /// <returns>Parsed boolean value, or null if parsing fails.</returns>
        public static bool? ParseBooleanFromResponse(string response)
        {
            if (string.IsNullOrWhiteSpace(response))
            {
                return null;
            }

            var cleanResponse = response.Trim().ToUpper(System.Globalization.CultureInfo.CurrentCulture);
            if (cleanResponse.Contains("TRUE"))
            {
                return true;
            }

            if (cleanResponse.Contains("FALSE"))
            {
                return false;
            }

            return null;
        }

        /// <summary>
        /// Parses a list of indices from the AI response.
        /// Supports: JSON arrays (numbers/strings), comma/space/newline-separated numbers,
        /// markdown code blocks, text-wrapped arrays, JSON objects with known keys,
        /// dictionaries with index->bool, ranges (N-M, N..M), and "none"/empty indicators.
        /// </summary>
        /// <param name="response">Raw response from the AI.</param>
        /// <returns>List of parsed integer indices (order and duplicates preserved for expansion operations).</returns>
        public static List<int> ParseIndicesFromResponse(string response)
        {
            var indices = new List<int>();
            if (string.IsNullOrWhiteSpace(response))
            {
                return indices;
            }

            var trimmed = response.Trim();

            // Check for explicit "none" or empty indicators
            var lowerTrimmed = trimmed.ToLowerInvariant();
            if (lowerTrimmed == "[]" || lowerTrimmed == "none" || lowerTrimmed == "no matches" || lowerTrimmed == "empty")
            {
                return indices;
            }

            // 1. Extract from markdown code blocks
            var codeBlockContent = ExtractFromMarkdownCodeBlock(trimmed);
            if (!string.IsNullOrEmpty(codeBlockContent))
            {
                trimmed = codeBlockContent;
            }

            // 2. Try parsing as complete JSON array
            try
            {
                var jarray = JArray.Parse(trimmed);
                foreach (var token in jarray)
                {
                    if (int.TryParse(token.ToString(), out int index))
                    {
                        indices.Add(index);
                    }
                }

                return DeduplicatePreserveOrder(indices);
            }
            catch
            {
                // Not a valid JSON array, continue
            }

            // 3. Try parsing as JSON object with known keys
            try
            {
                var jobject = JObject.Parse(trimmed);

                // Check for common keys: indices, result, data
                if (TryExtractIndicesFromObject(jobject, out var objIndices))
                {
                    return DeduplicatePreserveOrder(objIndices);
                }

                // Check if it's a dictionary of index->bool/value
                if (TryExtractIndicesFromDictionary(jobject, out var dictIndices))
                {
                    return DeduplicatePreserveOrder(dictIndices);
                }
            }
            catch
            {
                // Not a valid JSON object, continue
            }

            // 4. Extract first bracketed array from text (text-wrapped arrays)
            var bracketedArray = ExtractFirstBracketedArray(trimmed);
            if (!string.IsNullOrEmpty(bracketedArray))
            {
                try
                {
                    var jarray = JArray.Parse(bracketedArray);
                    foreach (var token in jarray)
                    {
                        if (int.TryParse(token.ToString(), out int index))
                        {
                            indices.Add(index);
                        }
                    }

                    return DeduplicatePreserveOrder(indices);
                }
                catch
                {
                    // Parse as comma-separated from bracketed content
                    trimmed = bracketedArray.TrimStart('[').TrimEnd(']');
                }
            }

            // 5. Expand ranges (N-M or N..M)
            trimmed = ExpandRanges(trimmed);

            // 6. Extract all numbers from comma/space/newline-separated text
            var parts = trimmed.Split(new[] { ',', ' ', '\t', '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                var cleaned = part.Trim().TrimStart('[').TrimEnd(']').Trim('"', '\'');
                if (int.TryParse(cleaned, out int index))
                {
                    indices.Add(index);
                }
            }

            return DeduplicatePreserveOrder(indices);
        }

        /// <summary>
        /// Extracts content from markdown code blocks (```json, ```txt, etc.).
        /// </summary>
        private static string ExtractFromMarkdownCodeBlock(string text)
        {
            var match = MarkdownCodeBlockRegex().Match(text);
            return match.Success ? match.Groups[1].Value.Trim() : string.Empty;
        }

        /// <summary>
        /// Extracts the first bracketed array [...] from text.
        /// </summary>
        private static string ExtractFirstBracketedArray(string text)
        {
            var match = FirstBracketedArrayRegex().Match(text);
            return match.Success ? match.Value : string.Empty;
        }

        /// <summary>
        /// Tries to extract indices from a JSON object with known keys (indices, result, data).
        /// </summary>
        private static bool TryExtractIndicesFromObject(JObject obj, out List<int> indices)
        {
            indices = new List<int>();

            // Check common keys
            var keys = new[] { "indices", "result", "data" };
            foreach (var key in keys)
            {
                if (obj.TryGetValue(key, out var token))
                {
                    if (token is JArray array)
                    {
                        foreach (var item in array)
                        {
                            if (int.TryParse(item.ToString(), out int index))
                            {
                                indices.Add(index);
                            }
                        }

                        return indices.Count > 0;
                    }

                    // Handle nested objects like {"data": {"indices": [...]}}
                    if (token is JObject nestedObj && nestedObj.TryGetValue("indices", out var nestedToken) && nestedToken is JArray nestedArray)
                    {
                        foreach (var item in nestedArray)
                        {
                            if (int.TryParse(item.ToString(), out int index))
                            {
                                indices.Add(index);
                            }
                        }

                        return indices.Count > 0;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Tries to extract indices from a dictionary where keys are indices and values are bool/truthy.
        /// Example: {"2": true, "3": true} or {"2": "selected", "3": "selected"}
        /// </summary>
        private static bool TryExtractIndicesFromDictionary(JObject obj, out List<int> indices)
        {
            indices = new List<int>();

            foreach (var prop in obj.Properties())
            {
                if (int.TryParse(prop.Name, out int index))
                {
                    // Check if value is truthy (true, non-zero number, non-empty string)
                    var value = prop.Value;
                    bool isTruthy = false;

                    if (value.Type == JTokenType.Boolean && value.ToObject<bool>())
                    {
                        isTruthy = true;
                    }
                    else if (value.Type == JTokenType.Integer && value.ToObject<int>() != 0)
                    {
                        isTruthy = true;
                    }
                    else if (value.Type == JTokenType.String && !string.IsNullOrWhiteSpace(value.ToString()))
                    {
                        isTruthy = true;
                    }

                    if (isTruthy)
                    {
                        indices.Add(index);
                    }
                }
            }

            return indices.Count > 0;
        }

        /// <summary>
        /// Expands range notation (N-M or N..M) into comma-separated numbers.
        /// Example: "2-4, 7" becomes "2,3,4,7"
        /// </summary>
        private static string ExpandRanges(string text)
        {
            var result = RangeRegex().Replace(text, match =>
            {
                if (int.TryParse(match.Groups[1].Value, out int start) && int.TryParse(match.Groups[2].Value, out int end))
                {
                    var expanded = new List<string>();
                    for (int i = start; i <= end; i++)
                    {
                        expanded.Add(i.ToString());
                    }

                    return string.Join(",", expanded);
                }

                return match.Value;
            });

            return result;
        }

        /// <summary>
        /// Deduplicates indices while preserving the order they were provided in.
        /// </summary>
        private static List<int> DeduplicatePreserveOrder(IEnumerable<int> indices)
        {
            var seen = new HashSet<int>();
            var ordered = new List<int>();

            foreach (var index in indices)
            {
                if (seen.Add(index))
                {
                    ordered.Add(index);
                }
            }

            return ordered;
        }

        #endregion

        #region Data Formatting

        /// <summary>
        /// Concatenates a list of GH_String items into a JSON dictionary format.
        /// </summary>
        /// <param name="inputList">The list of GH_String items.</param>
        /// <param name="output">Dictionary or array format. Default is dictionary.</param>
        /// <returns>A JSON string representing the list as a dictionary or array.</returns>
        public static string ConcatenateItemsToJson(List<GH_String> inputList, string output = "dict")
        {
            var stringList = new List<string>();

            foreach (var item in inputList)
            {
                stringList.Add(item.ToString());
            }

            var result = "";
            if (output.ToLowerInvariant() == "array" || output.ToLowerInvariant() == "arr")
            {
                // Array format
                result = "[" + string.Join(",", stringList.Select((value, index) => $"\"{value}\"")) + "]";
            }
            else
            {
                // Dictionary format
                result = "{" + string.Join(",", stringList.Select((value, index) => $"\"{index}\":\"{value}\"")) + "}";
            }

            Debug.WriteLine($"[AIResponseParser] Concatenated JSON: {result}");

            return result;
        }

        #endregion

        #region List Parsing

        /// <summary>
        /// Parses a string (JSON array or comma-separated) into a list of string values.
        /// Handles missing quotes and formatting errors.
        /// </summary>
        /// <returns></returns>
        public static List<string> ParseStringArrayFromResponse(string response)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(response))
            {
                return result;
            }

            var trimmed = response.Trim();

            // Attempt JSON parsing
            try
            {
                var jarray = JArray.Parse(trimmed);
                foreach (var token in jarray)
                {
                    result.Add(token.ToString());
                }

                return result;
            }
            catch
            {
            }

            // Fallback to comma-separated parsing with delimiter-aware splitting
            var clean = trimmed.TrimStart('[').TrimEnd(']');
            var items = SplitRespectingDelimiters(clean);
            foreach (var item in items)
            {
                var val = item.Trim().Trim('"', '\'');
                if (!string.IsNullOrEmpty(val))
                {
                    result.Add(val);
                }
            }

            return result;
        }

        /// <summary>
        /// Splits a string on commas while respecting any type of delimiters to avoid splitting structured strings.
        /// Handles: {}, (), [], and any nested combinations.
        /// Properly handles escaped quotes within strings.
        /// </summary>
        private static List<string> SplitRespectingDelimiters(string input)
        {
            var result = new List<string>();
            var current = new System.Text.StringBuilder();
            int depth = 0;
            bool inQuotes = false;
            char quoteChar = '\0';

            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];

                // Handle quotes to avoid splitting quoted strings
                if ((c == '"' || c == '\'') && !inQuotes)
                {
                    // Check if this quote is escaped
                    if (!IsEscaped(input, i))
                    {
                        inQuotes = true;
                        quoteChar = c;
                    }

                    current.Append(c);
                }
                else if (c == quoteChar && inQuotes)
                {
                    // Check if this quote is escaped
                    if (!IsEscaped(input, i))
                    {
                        inQuotes = false;
                        quoteChar = '\0';
                    }

                    current.Append(c);
                }

                // Handle opening delimiters
                else if (!inQuotes && (c == '{' || c == '(' || c == '['))
                {
                    depth++;
                    current.Append(c);
                }

                // Handle closing delimiters
                else if (!inQuotes && (c == '}' || c == ')' || c == ']'))
                {
                    depth--;
                    current.Append(c);
                }

                // Handle comma separator only when not inside any delimiters or quotes
                else if (c == ',' && depth == 0 && !inQuotes)
                {
                    var item = current.ToString().Trim();
                    if (!string.IsNullOrEmpty(item))
                    {
                        result.Add(item);
                    }

                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }

            // Add the last item
            var lastItem = current.ToString().Trim();
            if (!string.IsNullOrEmpty(lastItem))
            {
                result.Add(lastItem);
            }

            return result;
        }

        /// <summary>
        /// Determines if a character at the given position is escaped by counting preceding backslashes.
        /// Handles multiple consecutive backslashes correctly (e.g., \\" where \\" represents \+ escaped quote).
        /// </summary>
        /// <param name="input">The input string.</param>
        /// <param name="position">The position of the character to check.</param>
        /// <returns>True if the character is escaped, false otherwise.</returns>
        private static bool IsEscaped(string input, int position)
        {
            if (position == 0) return false;

            int backslashCount = 0;
            for (int i = position - 1; i >= 0 && input[i] == '\\'; i--)
            {
                backslashCount++;
            }

            // If odd number of backslashes, the character is escaped
            return backslashCount % 2 == 1;
        }

        /// <summary>
        /// Converts a list of strings into a compact JSON array string.
        /// </summary>
        /// <returns></returns>
        public static string NormalizeJsonArrayString(List<string> values)
        {
            var jarray = new JArray();
            foreach (var val in values)
            {
                jarray.Add(val);
            }

            return jarray.ToString(Formatting.None);
        }

        #endregion

        #region JSON Object Parsing

        /// <summary>
        /// Attempts to extract and parse a JSON object from an AI response that may contain
        /// markdown formatting, HTML tags, or other non-JSON wrapping.
        /// Uses brace-depth tracking to correctly extract the first complete JSON object.
        /// </summary>
        /// <param name="response">Raw response from the AI.</param>
        /// <returns>Parsed <see cref="JObject"/>.</returns>
        /// <exception cref="JsonException">Thrown when the response cannot be parsed as JSON.</exception>
        public static JObject SanitizeAndParseJson(string response)
        {
            if (string.IsNullOrWhiteSpace(response))
            {
                throw new JsonException("AI response is empty.");
            }

            // Try direct parse first
            try
            {
                return JObject.Parse(response);
            }
            catch (JsonException)
            {
                // Continue with sanitization attempts
            }

            // Try extracting JSON from markdown code blocks (```json ... ``` or ``` ... ```)
            var trimmed = response.Trim();
            var match = MarkdownCodeBlockRegex().Match(trimmed);
            if (match.Success)
            {
                try
                {
                    return JObject.Parse(match.Groups[1].Value.Trim());
                }
                catch (JsonException)
                {
                    // Continue
                }
            }

            // Try extracting the first complete JSON object by tracking brace depth
            var jsonCandidate = ExtractFirstJsonObject(trimmed);
            if (!string.IsNullOrEmpty(jsonCandidate))
            {
                try
                {
                    return JObject.Parse(jsonCandidate);
                }
                catch (JsonException)
                {
                    // Continue
                }
            }

            // All attempts failed - provide a descriptive error
            var preview = response.Length > 200 ? response.Substring(0, 200) + "..." : response;
            if (trimmed.StartsWith("<", StringComparison.Ordinal))
            {
                throw new JsonException(
                    $"AI returned HTML/XML instead of JSON. This may indicate a provider error. Preview: {preview}");
            }

            throw new JsonException($"AI response is not valid JSON. Preview: {preview}");
        }

        /// <summary>
        /// Extracts the first complete JSON object from a string by tracking brace depth,
        /// correctly handling nested objects and string literals.
        /// </summary>
        /// <param name="text">The text to search for a JSON object.</param>
        /// <returns>The first complete JSON object string, or null if none found.</returns>
        private static string ExtractFirstJsonObject(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }

            bool inString = false;
            bool escape = false;
            int depth = 0;
            int startIndex = -1;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];

                if (escape)
                {
                    escape = false;
                    continue;
                }

                if (c == '\\')
                {
                    if (inString)
                    {
                        escape = true;
                    }

                    continue;
                }

                if (c == '"')
                {
                    inString = !inString;
                    continue;
                }

                if (inString)
                {
                    continue;
                }

                if (c == '{')
                {
                    if (depth == 0)
                    {
                        startIndex = i;
                    }

                    depth++;
                }
                else if (c == '}' && depth > 0)
                {
                    depth--;
                    if (depth == 0 && startIndex >= 0)
                    {
                        return text.Substring(startIndex, i - startIndex + 1);
                    }
                }
            }

            return null;
        }

        #endregion
    }
}
