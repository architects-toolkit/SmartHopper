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
using System.Globalization;

namespace SmartHopper.Core.Serialization.DataTypes.Serializers
{
    /// <summary>
    /// Serializer for System.Int32 type (integer numbers).
    /// Format: "integer:value" (int32 value).
    /// </summary>
    public class IntegerSerializer : IDataTypeSerializer
    {
        /// <inheritdoc/>
        public string TypeName => "Integer";

        /// <inheritdoc/>
        public Type TargetType => typeof(int);

        /// <inheritdoc/>
        public string Serialize(object value)
        {
            if (value is int integer)
            {
                return $"integer:{integer.ToString(CultureInfo.InvariantCulture)}";
            }

            // Handle other integer types
            if (value is long l)
            {
                return $"integer:{l.ToString(CultureInfo.InvariantCulture)}";
            }

            if (value is short s)
            {
                return $"integer:{s.ToString(CultureInfo.InvariantCulture)}";
            }

            if (value is byte b)
            {
                return $"integer:{b.ToString(CultureInfo.InvariantCulture)}";
            }

            throw new ArgumentException($"Value must be of type Int32, Int64, Int16, or Byte, got {value?.GetType().Name ?? "null"}");
        }

        /// <inheritdoc/>
        public object Deserialize(string value)
        {
            if (!Validate(value))
            {
                throw new FormatException($"Invalid Integer format: '{value}'. Expected format: 'integer:value' with valid int32.");
            }

            var integerStr = value.Substring(8); // "integer:".Length
            return int.Parse(integerStr, CultureInfo.InvariantCulture);
        }

        /// <inheritdoc/>
        public bool Validate(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            if (!value.StartsWith("integer:"))
            {
                return false;
            }

            var integerStr = value.Substring(8); // "integer:".Length
            return int.TryParse(integerStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
        }
    }
}
