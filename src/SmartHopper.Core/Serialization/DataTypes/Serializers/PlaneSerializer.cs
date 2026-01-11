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
    /// Serializer for Rhino.Geometry.Plane type.
    /// Format: "planeOXY:ox,oy,oz;xx,xy,xz;yx,yy,yz" (origin + X/Y axes).
    /// </summary>
    public class PlaneSerializer : IDataTypeSerializer
    {
        /// <inheritdoc/>
        public string TypeName => "Plane";

        /// <inheritdoc/>
        public Type TargetType => typeof(Plane);

        /// <inheritdoc/>
        public string Serialize(object value)
        {
            if (value is Plane plane)
            {
                return $"planeOXY:{plane.Origin.X.ToString(CultureInfo.InvariantCulture)},{plane.Origin.Y.ToString(CultureInfo.InvariantCulture)},{plane.Origin.Z.ToString(CultureInfo.InvariantCulture)};" +
                       $"{plane.XAxis.X.ToString(CultureInfo.InvariantCulture)},{plane.XAxis.Y.ToString(CultureInfo.InvariantCulture)},{plane.XAxis.Z.ToString(CultureInfo.InvariantCulture)};" +
                       $"{plane.YAxis.X.ToString(CultureInfo.InvariantCulture)},{plane.YAxis.Y.ToString(CultureInfo.InvariantCulture)},{plane.YAxis.Z.ToString(CultureInfo.InvariantCulture)}";
            }

            throw new ArgumentException($"Value must be of type Plane, got {value?.GetType().Name ?? "null"}");
        }

        /// <inheritdoc/>
        public object Deserialize(string value)
        {
            if (!Validate(value))
            {
                throw new FormatException($"Invalid Plane format: '{value}'. Expected format: 'planeOXY:ox,oy,oz;xx,xy,xz;yx,yy,yz' with valid doubles.");
            }

            var valueWithoutPrefix = value.Substring(value.IndexOf(':') + 1);
            var vectors = valueWithoutPrefix.Split(';');
            var originParts = vectors[0].Split(',');
            var xAxisParts = vectors[1].Split(',');
            var yAxisParts = vectors[2].Split(',');

            double ox = double.Parse(originParts[0], CultureInfo.InvariantCulture);
            double oy = double.Parse(originParts[1], CultureInfo.InvariantCulture);
            double oz = double.Parse(originParts[2], CultureInfo.InvariantCulture);
            double xx = double.Parse(xAxisParts[0], CultureInfo.InvariantCulture);
            double xy = double.Parse(xAxisParts[1], CultureInfo.InvariantCulture);
            double xz = double.Parse(xAxisParts[2], CultureInfo.InvariantCulture);
            double yx = double.Parse(yAxisParts[0], CultureInfo.InvariantCulture);
            double yy = double.Parse(yAxisParts[1], CultureInfo.InvariantCulture);
            double yz = double.Parse(yAxisParts[2], CultureInfo.InvariantCulture);

            var origin = new Point3d(ox, oy, oz);
            var xAxis = new Vector3d(xx, xy, xz);
            var yAxis = new Vector3d(yx, yy, yz);

            return new Plane(origin, xAxis, yAxis);
        }

        /// <inheritdoc/>
        public bool Validate(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            if (!value.StartsWith("planeOXY:"))
            {
                return false;
            }

            var valueWithoutPrefix = value.Substring(9); // "planeOXY:".Length
            var vectors = valueWithoutPrefix.Split(';');
            if (vectors.Length != 3)
            {
                return false;
            }

            foreach (var vector in vectors)
            {
                var parts = vector.Split(',');
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
            }

            return true;
        }
    }
}
