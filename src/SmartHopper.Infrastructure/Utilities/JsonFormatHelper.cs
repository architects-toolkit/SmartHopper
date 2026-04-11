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
        /// Extracts JSON content from markdown code blocks if present.
        /// Supports ```json, ```txt, ```text, and bare ``` blocks.
        /// </summary>
        /// <param name="text">The text potentially containing markdown code blocks.</param>
        /// <returns>Extracted JSON content, or original text if no code block found.</returns>
        private static string ExtractFromMarkdownCodeBlock(string text)
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


    }
}
