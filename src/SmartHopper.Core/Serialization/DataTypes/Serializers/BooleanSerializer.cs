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
    /// Serializer for System.Boolean type.
    /// Format: "boolean:true" or "boolean:false" (lowercase boolean values).
    /// </summary>
    public class BooleanSerializer : IDataTypeSerializer
    {
        /// <inheritdoc/>
        public string TypeName => "Boolean";

        /// <inheritdoc/>
        public Type TargetType => typeof(bool);

        /// <inheritdoc/>
        public string Serialize(object value)
        {
            if (value is bool boolean)
            {
                return $"boolean:{boolean.ToString().ToLowerInvariant()}";
            }

            throw new ArgumentException($"Value must be of type Boolean, got {value?.GetType().Name ?? "null"}");
        }

        /// <inheritdoc/>
        public object Deserialize(string value)
        {
            if (!Validate(value))
            {
                throw new FormatException($"Invalid Boolean format: '{value}'. Expected format: 'boolean:true' or 'boolean:false'.");
            }

            var booleanStr = value.Substring(8); // "boolean:".Length
            return bool.Parse(booleanStr);
        }

        /// <inheritdoc/>
        public bool Validate(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            if (!value.StartsWith("boolean:"))
            {
                return false;
            }

            var booleanStr = value.Substring(8); // "boolean:".Length
            return booleanStr == "true" || booleanStr == "false";
        }
    }
}
