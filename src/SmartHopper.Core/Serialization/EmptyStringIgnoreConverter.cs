/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using System;
using Newtonsoft.Json;

namespace SmartHopper.Core.Serialization
{
    /// <summary>
    /// JSON converter that treats empty strings as null for serialization.
    /// Use with NullValueHandling.Ignore to omit empty strings from JSON output.
    /// </summary>
    public class EmptyStringIgnoreConverter : JsonConverter<string>
    {
        /// <summary>
        /// Writes the JSON representation of the string value.
        /// Treats empty strings as null (to be omitted with NullValueHandling.Ignore).
        /// </summary>
        public override void WriteJson(JsonWriter writer, string value, JsonSerializer serializer)
        {
            if (string.IsNullOrEmpty(value))
            {
                // Write null for empty strings (will be omitted by NullValueHandling.Ignore)
                writer.WriteNull();
                return;
            }

            writer.WriteValue(value);
        }

        /// <summary>
        /// Reads the JSON representation of the string value.
        /// </summary>
        public override string ReadJson(JsonReader reader, Type objectType, string existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
            {
                return null;
            }

            if (reader.TokenType == JsonToken.String)
            {
                return (string)reader.Value;
            }

            return null;
        }
    }
}
