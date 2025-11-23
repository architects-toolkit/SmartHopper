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

namespace SmartHopper.Core.Serialization.DataTypes.Serializers
{
    /// <summary>
    /// Serializer for System.String type.
    /// Format: "text:value" (simple string value).
    /// </summary>
    public class TextSerializer : IDataTypeSerializer
    {
        /// <inheritdoc/>
        public string TypeName => "Text";

        /// <inheritdoc/>
        public Type TargetType => typeof(string);

        /// <inheritdoc/>
        public string Serialize(object value)
        {
            if (value is string text)
            {
                return $"text:{text}";
            }

            throw new ArgumentException($"Value must be of type String, got {value?.GetType().Name ?? "null"}");
        }

        /// <inheritdoc/>
        public object Deserialize(string value)
        {
            if (!Validate(value))
            {
                throw new FormatException($"Invalid Text format: '{value}'. Expected format: 'text:value'.");
            }

            // Return everything after "text:"
            return value.Substring(5); // "text:".Length
        }

        /// <inheritdoc/>
        public bool Validate(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            return value.StartsWith("text:");
        }
    }
}
