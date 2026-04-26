/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024-2026 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using Newtonsoft.Json.Linq;

namespace SmartHopper.Core.Grasshopper.Utils
{
    /// <summary>
    /// Convenience extensions for safely reading typed values from JSON tokens commonly
    /// returned by AI tools.
    /// </summary>
    public static class JTokenExtensions
    {
        /// <summary>
        /// Returns the boolean value of <paramref name="token"/>, or <paramref name="defaultValue"/>
        /// when the token is null, JSON-null, or cannot be cast to a boolean.
        /// </summary>
        /// <param name="token">The JSON token to read (may be <c>null</c>).</param>
        /// <param name="defaultValue">Value to return when the token is missing or unparseable.</param>
        public static bool GetBoolOrDefault(this JToken token, bool defaultValue)
        {
            if (token == null || token.Type == JTokenType.Null)
            {
                return defaultValue;
            }

            try
            {
                return token.ToObject<bool>();
            }
            catch
            {
                return defaultValue;
            }
        }

        /// <summary>
        /// Returns the integer value of <paramref name="token"/>, or <paramref name="defaultValue"/>
        /// when the token is null, JSON-null, or cannot be cast to an integer.
        /// </summary>
        public static int GetIntOrDefault(this JToken token, int defaultValue)
        {
            if (token == null || token.Type == JTokenType.Null)
            {
                return defaultValue;
            }

            try
            {
                return token.ToObject<int>();
            }
            catch
            {
                return defaultValue;
            }
        }

        /// <summary>
        /// Returns the string value of <paramref name="token"/>, or <paramref name="defaultValue"/>
        /// when the token is null or JSON-null.
        /// </summary>
        public static string GetStringOrDefault(this JToken token, string defaultValue)
        {
            if (token == null || token.Type == JTokenType.Null)
            {
                return defaultValue;
            }

            return token.ToString();
        }
    }
}
