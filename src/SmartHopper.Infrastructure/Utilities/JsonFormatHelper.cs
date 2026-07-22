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
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SmartHopper.Infrastructure.Utilities
{
    /// <summary>
    /// Centralized utility for consistent JSON formatting across all JSON components and tools.
    /// Ensures all JSON output is minified (no unnecessary whitespace) for consistency.
    /// Handles extraction of JSON from markdown code blocks before minification.
    /// </summary>
    public static partial class JsonFormatHelper
    {
        /// <summary>
        /// Regex pattern for extracting content from markdown code blocks (```json, ```txt, etc.).
        /// </summary>
        [GeneratedRegex(@"```(?:json|txt|text)?\s*\n?(.*?)\n?```", RegexOptions.Singleline)]
        private static partial Regex MarkdownCodeBlockRegex();

        /// <summary>
        /// Regex pattern for removing trailing commas before a closing object or array bracket.
        /// </summary>
        [GeneratedRegex(@",(\s*[\]}])")]
        private static partial Regex TrailingCommaRegex();

        /// <summary>
        /// Extracts JSON content from markdown code blocks if present.
        /// Supports ```json, ```txt, ```text, and bare ``` blocks.
        /// </summary>
        /// <param name="text">The text potentially containing markdown code blocks.</param>
        /// <returns>Extracted JSON content, or original text if no code block found.</returns>
        public static string ExtractFromMarkdownCodeBlock(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return text;
            }

            var match = MarkdownCodeBlockRegex().Match(text);
            if (match.Success)
            {
                var extracted = match.Groups[1].Value.Trim();
                return !string.IsNullOrWhiteSpace(extracted) ? extracted : text;
            }

            return text;
        }

        /// <summary>
        /// Sanitizes a JSON-like string produced by an AI, making a best-effort attempt to
        /// recover from common malformations that AIs tend to emit:
        /// <list type="bullet">
        ///   <item><description>Unescaped control characters (newline, carriage return, tab, form feed, backspace, other C0) inside string literals are escaped to their JSON form (<c>\n</c>, <c>\r</c>, <c>\t</c>, <c>\uXXXX</c>).</description></item>
        ///   <item><description>Smart / curly quotes (<c>“ ”</c>) and zero-width space (<c>\uFEFF</c>) are normalized.</description></item>
        ///   <item><description>Trailing commas before a closing <c>}</c> or <c>]</c> are removed.</description></item>
        ///   <item><description>Unterminated string literals are closed with a trailing <c>"</c>.</description></item>
        ///   <item><description>Unbalanced <c>{</c> and <c>[</c> are closed in the correct order with matching <c>}</c> / <c>]</c>, so the output is at minimum structurally parseable.</description></item>
        /// </list>
        /// The method is intentionally conservative: it walks the string with a tiny state machine
        /// tracking in-string / escape state and a stack of open containers. It does NOT attempt to
        /// fix missing quotes inside content, mismatched (wrong-type) closers, or truncated values
        /// such as <c>"key":</c> with no value — auto-closing those may produce syntactically valid
        /// but semantically nonsense JSON. Callers should still try parsing the original text first.
        /// </summary>
        /// <param name="json">The potentially malformed JSON string.</param>
        /// <returns>A sanitized JSON string. May still be invalid if malformations are beyond this method's scope.</returns>
        public static string SanitizeJsonString(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return json;
            }

            var sb = new StringBuilder(json.Length + 16);
            var containers = new Stack<char>(); // tracks open '{' and '[' outside strings
            bool inString = false;
            bool escape = false;

            for (int i = 0; i < json.Length; i++)
            {
                char c = json[i];

                // Normalize smart quotes and BOM/ZWSP outside of string literals.
                // Inside strings, curly quotes are left as-is (they are valid unicode content).
                if (!inString)
                {
                    switch (c)
                    {
                        case '\u201C': // “
                        case '\u201D': // ”
                            sb.Append('"');
                            inString = true;
                            continue;
                        case '\uFEFF': // BOM / zero-width no-break space
                        case '\u200B': // zero-width space
                            continue;
                    }
                }

                if (escape)
                {
                    sb.Append(c);
                    escape = false;
                    continue;
                }

                if (inString && c == '\\')
                {
                    sb.Append(c);
                    escape = true;
                    continue;
                }

                if (c == '"')
                {
                    sb.Append(c);
                    inString = !inString;
                    continue;
                }

                // Smart closing quote inside a string: also closes the string.
                if (inString && (c == '\u201C' || c == '\u201D'))
                {
                    sb.Append('"');
                    inString = false;
                    continue;
                }

                if (inString)
                {
                    switch (c)
                    {
                        case '\n': sb.Append("\\n"); break;
                        case '\r': sb.Append("\\r"); break;
                        case '\t': sb.Append("\\t"); break;
                        case '\b': sb.Append("\\b"); break;
                        case '\f': sb.Append("\\f"); break;
                        default:
                            if (c < 0x20)
                            {
                                sb.Append("\\u").Append(((int)c).ToString("X4"));
                            }
                            else
                            {
                                sb.Append(c);
                            }

                            break;
                    }
                }
                else
                {
                    // Track container nesting outside of string literals so we can auto-close
                    // any that are left open at the end.
                    switch (c)
                    {
                        case '{':
                        case '[':
                            containers.Push(c);
                            break;
                        case '}':
                            if (containers.Count > 0 && containers.Peek() == '{')
                            {
                                containers.Pop();
                            }

                            break;
                        case ']':
                            if (containers.Count > 0 && containers.Peek() == '[')
                            {
                                containers.Pop();
                            }

                            break;
                    }

                    sb.Append(c);
                }
            }

            // Close unterminated string literal.
            if (inString)
            {
                sb.Append('"');
            }

            // Close unbalanced containers in LIFO order so nesting stays correct.
            while (containers.Count > 0)
            {
                sb.Append(containers.Pop() == '{' ? '}' : ']');
            }

            var result = sb.ToString();

            // Remove trailing commas before closing } or ]. The regex is safe because by this
            // point string literals have had their control chars escaped, so commas inside
            // strings will not be followed by a raw closing bracket. Applied after auto-close
            // so dangling commas introduced by truncation (e.g. "a":1,]) are also handled.
            result = TrailingCommaRegex().Replace(result, "$1");

            return result;
        }

        /// <summary>
        /// Extracts the first complete JSON container from text by tracking brace/bracket depth.
        /// Correctly handles nested containers and string literals with escape sequences.
        /// The returned container is whichever opens first — a JSON object (<c>{...}</c>) or a
        /// JSON array (<c>[...]</c>). This is useful when the root of the payload may be either.
        /// </summary>
        /// <param name="text">The text to search for a JSON container.</param>
        /// <returns>The first complete JSON container string, or <c>null</c> if none found.</returns>
        public static string ExtractFirstJsonContainer(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }

            bool inString = false;
            bool escapeNext = false;
            int depth = 0;
            int startIndex = -1;
            char opener = '\0';
            char closer = '\0';

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];

                if (escapeNext)
                {
                    escapeNext = false;
                    continue;
                }

                if (c == '\\' && inString)
                {
                    escapeNext = true;
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

                // Wait until we see the first top-level opener to decide container type.
                if (depth == 0 && startIndex < 0)
                {
                    if (c == '{')
                    {
                        opener = '{';
                        closer = '}';
                    }
                    else if (c == '[')
                    {
                        opener = '[';
                        closer = ']';
                    }
                    else
                    {
                        continue;
                    }

                    startIndex = i;
                    depth = 1;
                    continue;
                }

                if (c == opener)
                {
                    depth++;
                }
                else if (c == closer && depth > 0)
                {
                    depth--;
                    if (depth == 0)
                    {
                        return text.Substring(startIndex, i - startIndex + 1);
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Extracts the first complete JSON object (<c>{...}</c>) from text by tracking brace depth.
        /// Convenience wrapper over <see cref="ExtractFirstJsonContainer(string)"/> that filters out
        /// array roots; callers expecting a <see cref="JObject"/> should use this, while callers that
        /// accept both objects and arrays should prefer <see cref="ExtractFirstJsonContainer(string)"/>.
        /// </summary>
        /// <param name="text">The text to search for a JSON object.</param>
        /// <returns>The first complete JSON object string, or <c>null</c> if none found.</returns>
        public static string ExtractFirstJsonObject(string text)
        {
            var container = ExtractFirstJsonContainer(text);
            return (container != null && container.Length > 0 && container[0] == '{') ? container : null;
        }

        /// <summary>
        /// Attempts to recover a valid <see cref="JToken"/> (object OR array root) from a possibly
        /// malformed input by running a pipeline of progressively more aggressive recovery
        /// strategies. Each strategy is tried in order and the first one to produce a parseable
        /// token wins.
        /// <list type="number">
        ///   <item><description>Direct <see cref="JToken.Parse(string)"/>.</description></item>
        ///   <item><description>Strip markdown code-block fences (<see cref="ExtractFromMarkdownCodeBlock(string)"/>) then parse.</description></item>
        ///   <item><description>Extract the first complete top-level JSON container (object OR array) by brace/bracket-depth tracking (<see cref="ExtractFirstJsonContainer(string)"/>) then parse.</description></item>
        ///   <item><description>Sanitize common AI malformations (<see cref="SanitizeJsonString(string)"/>) then parse. Tried against each previous candidate.</description></item>
        /// </list>
        /// The <paramref name="log"/> output records, in order, a human-readable description of
        /// every step performed and whether it succeeded.
        /// </summary>
        /// <param name="input">Raw input, potentially wrapped in markdown fences, containing prose, or malformed.</param>
        /// <param name="result">The recovered <see cref="JToken"/> on success (a <see cref="JObject"/> or a <see cref="JArray"/>), otherwise <c>null</c>.</param>
        /// <param name="log">Ordered list of steps performed (always populated, never <c>null</c>).</param>
        /// <returns><c>true</c> if a <see cref="JToken"/> was recovered; <c>false</c> otherwise.</returns>
        public static bool TryRecoverJsonToken(string input, out JToken result, out List<string> log)
        {
            log = new List<string>();
            result = null;

            if (string.IsNullOrWhiteSpace(input))
            {
                log.Add("Input is empty or whitespace.");
                return false;
            }

            // Strategy 1: direct parse
            if (TryParseToken(input, out result))
            {
                log.Add($"Parsed input directly as JSON {TokenKindLabel(result)}.");
                return true;
            }

            log.Add("Direct parse failed; trying recovery strategies.");

            // Strategy 2: strip markdown code-block fences
            string codeBlockContent = ExtractFromMarkdownCodeBlock(input);
            bool hadCodeBlock = !string.IsNullOrEmpty(codeBlockContent) && codeBlockContent != input;
            if (hadCodeBlock)
            {
                log.Add("Extracted content from markdown code block.");
                if (TryParseToken(codeBlockContent, out result))
                {
                    log.Add($"Parsed extracted code-block content as JSON {TokenKindLabel(result)}.");
                    return true;
                }

                log.Add("Code-block content is not directly parseable.");
            }
            else
            {
                log.Add("No markdown code block detected.");
            }

            // Strategy 3: container (object OR array) extraction via depth tracking
            string containerCandidate = ExtractFirstJsonContainer(input);
            if (!string.IsNullOrEmpty(containerCandidate) && containerCandidate != input)
            {
                log.Add("Extracted first JSON container (object or array) via depth tracking.");
                if (TryParseToken(containerCandidate, out result))
                {
                    log.Add($"Parsed depth-extracted candidate as JSON {TokenKindLabel(result)}.");
                    return true;
                }

                log.Add("Depth-extracted candidate is not directly parseable.");
            }

            // Strategy 4: sanitize each candidate (code block → container extract → raw input).
            foreach (var (candidate, label) in new[]
            {
                (codeBlockContent, "code-block content"),
                (containerCandidate, "depth-extracted candidate"),
                (input, "raw input"),
            })
            {
                if (string.IsNullOrWhiteSpace(candidate))
                {
                    continue;
                }

                var sanitized = SanitizeJsonString(candidate);
                if (TryParseToken(sanitized, out result))
                {
                    log.Add($"Parsed JSON {TokenKindLabel(result)} after sanitizing {label} (escaped control chars / closed unbalanced containers / stripped trailing commas).");
                    return true;
                }

                log.Add($"Sanitization of {label} did not yield parseable JSON.");
            }

            log.Add("All recovery strategies exhausted; input is not recoverable.");
            return false;
        }

        /// <summary>
        /// Object-only convenience wrapper over <see cref="TryRecoverJsonToken"/>. Returns
        /// <c>true</c> only if the recovered root is a <see cref="JObject"/>; array roots are
        /// rejected so callers that depend on object semantics (keys, properties) are safe.
        /// </summary>
        /// <param name="input">Raw input.</param>
        /// <param name="result">The recovered <see cref="JObject"/> on success, otherwise <c>null</c>.</param>
        /// <param name="log">Ordered list of steps performed.</param>
        /// <returns><c>true</c> if a <see cref="JObject"/> was recovered; <c>false</c> otherwise (including when a <see cref="JArray"/> was recovered).</returns>
        public static bool TryRecoverJsonObject(string input, out JObject result, out List<string> log)
        {
            if (TryRecoverJsonToken(input, out var token, out log) && token is JObject obj)
            {
                result = obj;
                return true;
            }

            if (token is JArray)
            {
                log.Add("Recovered root is a JSON array but an object was required; rejecting.");
            }

            result = null;
            return false;
        }

        private static bool TryParseToken(string text, out JToken token)
        {
            try
            {
                token = JToken.Parse(text);
                return token is JObject || token is JArray;
            }
            catch (JsonException)
            {
                token = null;
                return false;
            }
        }

        private static string TokenKindLabel(JToken token)
            => token is JArray ? "array" : "object";

        /// <summary>
        /// Converts a JSON string to minified format (JToken to string).
        /// Automatically extracts JSON from markdown code blocks if present.
        /// </summary>
        /// <param name="json">The JSON string to convert (may contain markdown code blocks).</param>
        /// <param name="error">Optional output parameter containing error message if conversion fails.</param>
        /// <returns>Minified JSON string, or empty string if input is null/whitespace or conversion fails.</returns>
        public static string JsonToString(string json, out string error)
        {
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(json))
            {
                error = "JSON string is empty";
                return string.Empty;
            }

            // Extract from markdown code blocks if present
            var cleanJson = ExtractFromMarkdownCodeBlock(json.Trim());

            try
            {
                var token = JToken.Parse(cleanJson);
                return token.ToString(Formatting.None);
            }
            catch (JsonException ex)
            {
                error = $"Invalid JSON format: {ex.Message}";
                return string.Empty;
            }
            catch (Exception ex)
            {
                error = $"Error converting JSON: {ex.Message}";
                return string.Empty;
            }
        }

        /// <summary>
        /// Converts a JSON string to minified format without error reporting.
        /// </summary>
        /// <param name="json">The JSON string to convert.</param>
        /// <returns>Minified JSON string, or empty string if conversion fails.</returns>
        public static string JsonToString(string json)
        {
            return JsonToString(json, out _);
        }

        /// <summary>
        /// Converts a JToken to minified JSON string.
        /// </summary>
        /// <param name="token">The JToken to convert.</param>
        /// <param name="error">Optional output parameter containing error message if conversion fails.</param>
        /// <returns>Minified JSON string, or empty string if token is null or conversion fails.</returns>
        public static string JsonToString(JToken token, out string error)
        {
            error = string.Empty;
            if (token == null)
            {
                error = "JToken is null";
                return string.Empty;
            }

            try
            {
                return token.ToString(Formatting.None);
            }
            catch (Exception ex)
            {
                error = $"Error converting JToken: {ex.Message}";
                return string.Empty;
            }
        }

        /// <summary>
        /// Converts a JToken to minified JSON string without error reporting.
        /// </summary>
        /// <param name="token">The JToken to convert.</param>
        /// <returns>Minified JSON string, or empty string if conversion fails.</returns>
        public static string JsonToString(JToken token)
        {
            return JsonToString(token, out _);
        }

        /// <summary>
        /// Converts a JSON string to a parsed JToken (string to JToken).
        /// Automatically extracts JSON from markdown code blocks if present.
        /// </summary>
        /// <param name="json">The JSON string to parse (may contain markdown code blocks).</param>
        /// <param name="error">Optional output parameter containing error message if parsing fails.</param>
        /// <returns>Parsed JToken, or null if parsing fails.</returns>
        public static JToken StringToJson(string json, out string error)
        {
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(json))
            {
                error = "JSON string is empty";
                return null;
            }

            // Extract from markdown code blocks if present
            var cleanJson = ExtractFromMarkdownCodeBlock(json.Trim());

            try
            {
                return JToken.Parse(cleanJson);
            }
            catch (JsonException ex)
            {
                error = $"Invalid JSON format: {ex.Message}";
                return null;
            }
            catch (Exception ex)
            {
                error = $"Error parsing JSON: {ex.Message}";
                return null;
            }
        }

        /// <summary>
        /// Converts a JSON string to a parsed JToken without error reporting.
        /// </summary>
        /// <param name="json">The JSON string to parse.</param>
        /// <returns>Parsed JToken, or null if parsing fails.</returns>
        public static JToken StringToJson(string json)
        {
            return StringToJson(json, out _);
        }

        /// <summary>
        /// Validates that a string is valid JSON.
        /// </summary>
        /// <param name="json">The JSON string to validate.</param>
        /// <param name="parsed">Output parameter containing the parsed JToken if valid.</param>
        /// <returns>True if valid JSON, false otherwise.</returns>
        public static bool IsValidJson(string json, out JToken parsed)
        {
            parsed = StringToJson(json, out _);
            return parsed != null;
        }

        /// <summary>
        /// Validates that a string is valid JSON without returning the parsed token.
        /// </summary>
        /// <param name="json">The JSON string to validate.</param>
        /// <returns>True if valid JSON, false otherwise.</returns>
        public static bool IsValidJson(string json)
        {
            return IsValidJson(json, out _);
        }

        /// <summary>
        /// Iterates each non-empty line of a JSONL payload, parsed as <see cref="JObject"/>.
        /// Silently skips blank and malformed lines so callers can focus on schema extraction.
        /// </summary>
        /// <param name="content">Raw JSONL content (one JSON object per line).</param>
        /// <returns>Lazy sequence of parsed <see cref="JObject"/> items.</returns>
        public static IEnumerable<JObject> ParseJsonLines(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                yield break;
            }

            foreach (var raw in content.Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0)
                {
                    continue;
                }

                JObject obj;
                try
                {
                    obj = JObject.Parse(line);
                }
                catch
                {
                    // Skip malformed lines
                    continue;
                }

                yield return obj;
            }
        }
    }
}
