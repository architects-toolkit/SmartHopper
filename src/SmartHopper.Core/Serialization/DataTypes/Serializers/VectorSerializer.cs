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
using Rhino.Geometry;

namespace SmartHopper.Core.Serialization.DataTypes.Serializers
{
    /// <summary>
    /// Serializer for Rhino.Geometry.Vector3d type.
    /// Format: "vectorXYZ:x,y,z" (e.g., "vectorXYZ:1.0,0.0,0.0").
    /// </summary>
    public class VectorSerializer : IDataTypeSerializer
    {
        /// <inheritdoc/>
        public string TypeName => "Vector";

        /// <inheritdoc/>
        public Type TargetType => typeof(Vector3d);

        /// <inheritdoc/>
        public string Serialize(object value)
        {
            if (value is Vector3d vector)
            {
                return $"vectorXYZ:{vector.X.ToString(CultureInfo.InvariantCulture)},{vector.Y.ToString(CultureInfo.InvariantCulture)},{vector.Z.ToString(CultureInfo.InvariantCulture)}";
            }

            throw new ArgumentException($"Value must be of type Vector3d, got {value?.GetType().Name ?? "null"}");
        }

        /// <inheritdoc/>
        public object Deserialize(string value)
        {
            if (!Validate(value))
            {
                throw new FormatException($"Invalid Vector format: '{value}'. Expected format: 'vectorXYZ:x,y,z' with valid doubles.");
            }

            var valueWithoutPrefix = value.Substring(value.IndexOf(':') + 1);
            var parts = valueWithoutPrefix.Split(',');
            double x = double.Parse(parts[0], CultureInfo.InvariantCulture);
            double y = double.Parse(parts[1], CultureInfo.InvariantCulture);
            double z = double.Parse(parts[2], CultureInfo.InvariantCulture);

            return new Vector3d(x, y, z);
        }

        /// <inheritdoc/>
        public bool Validate(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            if (!value.StartsWith("vectorXYZ:"))
            {
                return false;
            }

            var valueWithoutPrefix = value.Substring(10); // "vectorXYZ:".Length
            var parts = valueWithoutPrefix.Split(',');
            if (parts.Length != 3)
            {
                return false;
            }

            foreach (var part in parts)
            {
                if (!double.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
