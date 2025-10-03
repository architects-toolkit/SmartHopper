/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Grasshopper.Kernel.Types;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SmartHopper.Core.Grasshopper.Utils
{
    /// <summary>
    /// Helper methods for parsing AI responses into specific data types.
    /// </summary>
    public static class ParsingTools
    {
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
        /// Parses a comma-separated list of indices from the AI response.
        /// </summary>
        /// <param name="response">Raw response from the AI.</param>
        /// <returns>List of parsed integer indices.</returns>
        public static List<int> ParseIndicesFromResponse(string response)
        {
            var indices = new List<int>();
            if (string.IsNullOrWhiteSpace(response))
            {
                return indices;
            }

            var parts = response.Split(',');
            foreach (var part in parts)
            {
                if (int.TryParse(part.Trim(), out int index))
                {
                    indices.Add(index);
                }
            }

            return indices;
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

            Debug.WriteLine($"[ParsingTools] Concatenated JSON: {result}");

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
    }
}
