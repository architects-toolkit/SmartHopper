/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 * 
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

namespace SmartHopper.Core.Grasshopper.Tools
{
    /// <summary>
    /// Helper methods for parsing AI responses into specific data types
    /// </summary>
    public static class ParsingTools
    {
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
            if (cleanResponse == "TRUE")
                return true;
            if (cleanResponse == "FALSE")
                return false;

            return null;
        }
    }
}
