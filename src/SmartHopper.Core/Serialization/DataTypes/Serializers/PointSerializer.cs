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
    /// Serializer for Rhino.Geometry.Point3d type.
    /// Format: "pointXYZ:x,y,z" (e.g., "pointXYZ:10.5,20.0,30.5").
    /// </summary>
    public class PointSerializer : IDataTypeSerializer
    {
        /// <inheritdoc/>
        public string TypeName => "Point";

        /// <inheritdoc/>
        public Type TargetType => typeof(Point3d);

        /// <inheritdoc/>
        public string Serialize(object value)
        {
            if (value is Point3d point)
            {
                return $"pointXYZ:{point.X.ToString(CultureInfo.InvariantCulture)},{point.Y.ToString(CultureInfo.InvariantCulture)},{point.Z.ToString(CultureInfo.InvariantCulture)}";
            }

            throw new ArgumentException($"Value must be of type Point3d, got {value?.GetType().Name ?? "null"}");
        }

        /// <inheritdoc/>
        public object Deserialize(string value)
        {
            if (!Validate(value))
            {
                throw new FormatException($"Invalid Point format: '{value}'. Expected format: 'pointXYZ:x,y,z' with valid doubles.");
            }

            var valueWithoutPrefix = value.Substring(value.IndexOf(':') + 1);
            var parts = valueWithoutPrefix.Split(',');
            double x = double.Parse(parts[0], CultureInfo.InvariantCulture);
            double y = double.Parse(parts[1], CultureInfo.InvariantCulture);
            double z = double.Parse(parts[2], CultureInfo.InvariantCulture);

            return new Point3d(x, y, z);
        }

        /// <inheritdoc/>
        public bool Validate(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            if (!value.StartsWith("pointXYZ:"))
            {
                return false;
            }

            var valueWithoutPrefix = value.Substring(9); // "pointXYZ:".Length
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
