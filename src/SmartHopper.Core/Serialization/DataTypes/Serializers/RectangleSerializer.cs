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
    /// Serializer for Rhino.Geometry.Rectangle3d type.
    /// Format: "rectangleCXY:cx,cy,cz;xx,xy,xz;yx,yy,yz;w,h" (center + X-axis + Y-axis + dimensions).
    /// </summary>
    public class RectangleSerializer : IDataTypeSerializer
    {
        /// <inheritdoc/>
        public string TypeName => "Rectangle";

        /// <inheritdoc/>
        public Type TargetType => typeof(Rectangle3d);

        /// <inheritdoc/>
        public string Serialize(object value)
        {
            if (value is Rectangle3d rectangle)
            {
                var plane = rectangle.Plane;
                var center = rectangle.Center;
                var width = rectangle.Width;
                var height = rectangle.Height;

                return $"rectangleCXY:{center.X.ToString(CultureInfo.InvariantCulture)},{center.Y.ToString(CultureInfo.InvariantCulture)},{center.Z.ToString(CultureInfo.InvariantCulture)};" +
                       $"{plane.XAxis.X.ToString(CultureInfo.InvariantCulture)},{plane.XAxis.Y.ToString(CultureInfo.InvariantCulture)},{plane.XAxis.Z.ToString(CultureInfo.InvariantCulture)};" +
                       $"{plane.YAxis.X.ToString(CultureInfo.InvariantCulture)},{plane.YAxis.Y.ToString(CultureInfo.InvariantCulture)},{plane.YAxis.Z.ToString(CultureInfo.InvariantCulture)};" +
                       $"{width.ToString(CultureInfo.InvariantCulture)},{height.ToString(CultureInfo.InvariantCulture)}";
            }

            throw new ArgumentException($"Value must be of type Rectangle3d, got {value?.GetType().Name ?? "null"}");
        }

        /// <inheritdoc/>
        public object Deserialize(string value)
        {
            if (!Validate(value))
            {
                throw new FormatException($"Invalid Rectangle format: '{value}'. Expected format: 'rectangleCXY:cx,cy,cz;xx,xy,xz;yx,yy,yz;w,h' with valid doubles.");
            }

            var valueWithoutPrefix = value.Substring(value.IndexOf(':') + 1);
            var parts = valueWithoutPrefix.Split(';');

            // Parse center
            var centerParts = parts[0].Split(',');
            var center = new Point3d(
                double.Parse(centerParts[0], CultureInfo.InvariantCulture),
                double.Parse(centerParts[1], CultureInfo.InvariantCulture),
                double.Parse(centerParts[2], CultureInfo.InvariantCulture)
            );

            // Parse X axis
            var xAxisParts = parts[1].Split(',');
            var xAxis = new Vector3d(
                double.Parse(xAxisParts[0], CultureInfo.InvariantCulture),
                double.Parse(xAxisParts[1], CultureInfo.InvariantCulture),
                double.Parse(xAxisParts[2], CultureInfo.InvariantCulture)
            );

            // Parse Y axis
            var yAxisParts = parts[2].Split(',');
            var yAxis = new Vector3d(
                double.Parse(yAxisParts[0], CultureInfo.InvariantCulture),
                double.Parse(yAxisParts[1], CultureInfo.InvariantCulture),
                double.Parse(yAxisParts[2], CultureInfo.InvariantCulture)
            );

            // Parse dimensions
            var dimensionParts = parts[3].Split(',');
            var width = double.Parse(dimensionParts[0], CultureInfo.InvariantCulture);
            var height = double.Parse(dimensionParts[1], CultureInfo.InvariantCulture);

            // Normalize axes to ensure they are unit vectors
            xAxis.Unitize();
            yAxis.Unitize();

            // Ensure axes are orthogonal by recalculating Y-axis
            // Rectangle3d requires a valid orthonormal plane
            var normal = Vector3d.CrossProduct(xAxis, yAxis);
            normal.Unitize();
            yAxis = Vector3d.CrossProduct(normal, xAxis);
            yAxis.Unitize();

            // Create plane at center and rectangle using intervals
            var plane = new Plane(center, xAxis, yAxis);
            var xInterval = new Interval(-width / 2.0, width / 2.0);
            var yInterval = new Interval(-height / 2.0, height / 2.0);
            return new Rectangle3d(plane, xInterval, yInterval);
        }

        /// <inheritdoc/>
        public bool Validate(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            if (!value.StartsWith("rectangleCXY:"))
            {
                return false;
            }

            var valueWithoutPrefix = value.Substring(13); // "rectangleCXY:".Length
            var parts = valueWithoutPrefix.Split(';');
            if (parts.Length != 4)
            {
                return false;
            }

            // Validate center (3 doubles)
            var centerParts = parts[0].Split(',');
            if (centerParts.Length != 3)
                return false;
            foreach (var part in centerParts)
            {
                if (!double.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                    return false;
            }

            // Validate X axis (3 doubles)
            var xAxisParts = parts[1].Split(',');
            if (xAxisParts.Length != 3)
                return false;
            foreach (var part in xAxisParts)
            {
                if (!double.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                    return false;
            }

            // Validate Y axis (3 doubles)
            var yAxisParts = parts[2].Split(',');
            if (yAxisParts.Length != 3)
                return false;
            foreach (var part in yAxisParts)
            {
                if (!double.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                    return false;
            }

            // Validate dimensions (2 doubles)
            var dimensionParts = parts[3].Split(',');
            if (dimensionParts.Length != 2)
                return false;
            foreach (var part in dimensionParts)
            {
                if (!double.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                    return false;
            }

            return true;
        }
    }
}
