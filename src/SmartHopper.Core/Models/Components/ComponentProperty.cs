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
 * along with this library; if not, write to the Free Software Foundation,
 * Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301, USA.
 */

using System;
using Newtonsoft.Json;

namespace SmartHopper.Core.Models.Components
{
    /// <summary>
    /// Represents a property of a Grasshopper component.
    /// </summary>
    [JsonConverter(typeof(ComponentPropertyConverter))]
    public class ComponentProperty
    {
        /// <summary>
        /// Gets or sets the actual value of the property.
        /// </summary>
        [JsonProperty("value")]
        public object Value { get; set; }
    }

    /// <summary>
    /// Custom JSON converter that serializes simple types (bool, int, string) directly
    /// without the {"value": ...} wrapper, while keeping the wrapper for complex types.
    /// </summary>
    public class ComponentPropertyConverter : JsonConverter<ComponentProperty>
    {
        public override void WriteJson(JsonWriter writer, ComponentProperty value, JsonSerializer serializer)
        {
            if (value?.Value == null)
            {
                writer.WriteNull();
                return;
            }

            // For simple types (bool, int, string, double), write the value directly
            if (value.Value is bool ||
                value.Value is int ||
                value.Value is long ||
                value.Value is double ||
                value.Value is float ||
                value.Value is decimal ||
                value.Value is string)
            {
                serializer.Serialize(writer, value.Value);
            }
            else
            {
                // For complex types, keep the {"value": ...} wrapper
                writer.WriteStartObject();
                writer.WritePropertyName("value");
                serializer.Serialize(writer, value.Value);
                writer.WriteEndObject();
            }
        }

        public override ComponentProperty ReadJson(JsonReader reader, Type objectType, ComponentProperty existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
            {
                return null;
            }

            // If it's a simple value (not an object), read it directly
            if (reader.TokenType != JsonToken.StartObject)
            {
                var simpleValue = serializer.Deserialize(reader);
                return new ComponentProperty { Value = simpleValue };
            }

            // Otherwise, expect {"value": ...} structure
            var obj = serializer.Deserialize<Newtonsoft.Json.Linq.JObject>(reader);
            if (obj != null && obj.TryGetValue("value", out var valueToken))
            {
                return new ComponentProperty { Value = valueToken.ToObject<object>() };
            }

            return new ComponentProperty();
        }
    }
}
