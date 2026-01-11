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
using System.Globalization;

namespace SmartHopper.Core.Serialization.DataTypes.Serializers
{
    /// <summary>
    /// Serializer for System.Double type (floating-point numbers).
    /// Format: "number:value" (double value using invariant culture).
    /// </summary>
    public class NumberSerializer : IDataTypeSerializer
    {
        /// <inheritdoc/>
        public string TypeName => "Number";

        /// <inheritdoc/>
        public Type TargetType => typeof(double);

        /// <inheritdoc/>
        public string Serialize(object value)
        {
            if (value is double number)
            {
                return $"number:{number.ToString(CultureInfo.InvariantCulture)}";
            }

            // Handle other numeric types
            if (value is float f)
            {
                return $"number:{f.ToString(CultureInfo.InvariantCulture)}";
            }

            if (value is decimal d)
            {
                return $"number:{d.ToString(CultureInfo.InvariantCulture)}";
            }

            throw new ArgumentException($"Value must be of type Double, Float, or Decimal, got {value?.GetType().Name ?? "null"}");
        }

        /// <inheritdoc/>
        public object Deserialize(string value)
        {
            if (!Validate(value))
            {
                throw new FormatException($"Invalid Number format: '{value}'. Expected format: 'number:value' with valid double.");
            }

            var numberStr = value.Substring(7); // "number:".Length
            return double.Parse(numberStr, CultureInfo.InvariantCulture);
        }

        /// <inheritdoc/>
        public bool Validate(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            if (!value.StartsWith("number:"))
            {
                return false;
            }

            var numberStr = value.Substring(7); // "number:".Length
            return double.TryParse(numberStr, NumberStyles.Float, CultureInfo.InvariantCulture, out _);
        }
    }
}
