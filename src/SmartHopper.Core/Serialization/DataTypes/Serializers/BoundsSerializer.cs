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
    /// Serializer for simple bounds (width, height) pairs.
    /// Format: "bounds:W,H" (e.g., "bounds:120.5,80.25").
    /// </summary>
    public class BoundsSerializer : IDataTypeSerializer
    {
        /// <inheritdoc/>
        public string TypeName => "Bounds";

        /// <inheritdoc/>
        public Type TargetType => typeof((double width, double height));

        /// <inheritdoc/>
        public string Serialize(object value)
        {
            if (value is ValueTuple<double, double> tuple)
            {
                return $"bounds:{tuple.Item1.ToString(CultureInfo.InvariantCulture)},{tuple.Item2.ToString(CultureInfo.InvariantCulture)}";
            }

            throw new ArgumentException($"Value must be a (double width, double height) tuple, got {value?.GetType().Name ?? "null"}");
        }

        /// <inheritdoc/>
        public object Deserialize(string value)
        {
            if (!Validate(value))
            {
                throw new FormatException($"Invalid Bounds format: '{value}'. Expected format: 'bounds:width,height' with valid doubles.");
            }

            var valueWithoutPrefix = value.Substring(value.IndexOf(':') + 1);
            var parts = valueWithoutPrefix.Split(',');
            double width = double.Parse(parts[0], CultureInfo.InvariantCulture);
            double height = double.Parse(parts[1], CultureInfo.InvariantCulture);

            return (width, height);
        }

        /// <inheritdoc/>
        public bool Validate(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            if (!value.StartsWith("bounds:"))
            {
                return false;
            }

            var valueWithoutPrefix = value.Substring("bounds:".Length);
            var parts = valueWithoutPrefix.Split(',');
            if (parts.Length != 2)
            {
                return false;
            }

            return double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out _) &&
                   double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out _);
        }
    }
}
