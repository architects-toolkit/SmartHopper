/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 * 
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using Grasshopper.Kernel.Types;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace SmartHopper.Core.Grasshopper.Tools
{
    /// <summary>
    /// Helper methods for parsing AI responses into specific data types
    /// </summary>
    public static class ParsingTools
    {
        #region Response Parsing

        /// <summary>
        /// Parses a boolean value from the AI response
        /// </summary>
        /// <param name="response">Raw response from the AI</param>
        /// <returns>Parsed boolean value, or null if parsing fails</returns>
        public static bool? ParseBooleanFromResponse(string response)
        {
            if (string.IsNullOrWhiteSpace(response))
                return null;

            var cleanResponse = response.Trim().ToUpper();
            if (cleanResponse.Contains("TRUE"))
                return true;
            if (cleanResponse.Contains("FALSE"))
                return false;

            return null;
        }

        /// <summary>
        /// Parses a comma-separated list of indices from the AI response
        /// </summary>
        /// <param name="response">Raw response from the AI</param>
        /// <returns>List of parsed integer indices</returns>
        public static List<int> ParseIndicesFromResponse(string response)
        {
            var indices = new List<int>();
            if (string.IsNullOrWhiteSpace(response))
                return indices;

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
        /// Concatenates a list of GH_String items into a JSON dictionary format
        /// </summary>
        /// <param name="inputList">The list of GH_String items</param>
        /// <returns>A JSON string representing the list as a dictionary</returns>
        public static string ConcatenateItemsToJson(List<GH_String> inputList)
        {
            var stringList = new List<string>();

            foreach (var item in inputList)
            {
                stringList.Add(item.ToString());
            }

            return "{" + string.Join(",", stringList.Select((value, index) => $"\"{index}\":\"{value}\"")) + "}"; // Dictionary format
        }

        /// <summary>
        /// Concatenates a list of GH_String items into a JSON dictionary format and returns it as a GH_String list with a single item
        /// </summary>
        /// <param name="inputList">The list of GH_String items</param>
        /// <returns>A list containing a single GH_String with the JSON representation of the input list</returns>
        public static List<GH_String> ConcatenateItemsToJsonList(List<GH_String> inputList)
        {
            var result = new List<GH_String>();
            var jsonString = ConcatenateItemsToJson(inputList);
            result.Add(new GH_String(jsonString));
            return result;
        }

        #endregion

        #region List Parsing

        /// <summary>
        /// Parses a string (JSON array or comma-separated) into a list of string values.
        /// Handles missing quotes and formatting errors.
        /// </summary>
        public static List<string> ParseStringArrayFromResponse(string response)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(response))
                return result;

            var trimmed = response.Trim();
            // Attempt JSON parsing
            try
            {
                var jarray = JArray.Parse(trimmed);
                foreach (var token in jarray)
                    result.Add(token.ToString());
                return result;
            }
            catch { }

            // Fallback to comma-separated parsing
            var clean = trimmed.TrimStart('[').TrimEnd(']');
            var parts = clean.Split(',');
            foreach (var part in parts)
            {
                var val = part.Trim().Trim('"', '\'');
                if (!string.IsNullOrEmpty(val))
                    result.Add(val);
            }
            return result;
        }

        /// <summary>
        /// Converts a list of strings into a compact JSON array string.
        /// </summary>
        public static string NormalizeJsonArrayString(List<string> values)
        {
            var jarray = new JArray();
            foreach (var val in values)
                jarray.Add(val);
            return jarray.ToString(Formatting.None);
        }
        #endregion
    }
}
