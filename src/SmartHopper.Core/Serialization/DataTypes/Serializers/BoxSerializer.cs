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
    /// Serializer for Rhino.Geometry.Box type.
    /// Format: "boxOXY:ox,oy,oz;xx,xy,xz;yx,yy,yz;x0,x1;y0,y1;z0,z1" (origin + X-axis + Y-axis + intervals).
    /// </summary>
    public class BoxSerializer : IDataTypeSerializer
    {
        /// <inheritdoc/>
        public string TypeName => "Box";

        /// <inheritdoc/>
        public Type TargetType => typeof(Box);

        /// <inheritdoc/>
        public string Serialize(object value)
        {
            if (value is Box box)
            {
                var plane = box.Plane;
                var x = box.X;
                var y = box.Y;
                var z = box.Z;

                return $"boxOXY:{plane.Origin.X.ToString(CultureInfo.InvariantCulture)},{plane.Origin.Y.ToString(CultureInfo.InvariantCulture)},{plane.Origin.Z.ToString(CultureInfo.InvariantCulture)};" +
                       $"{plane.XAxis.X.ToString(CultureInfo.InvariantCulture)},{plane.XAxis.Y.ToString(CultureInfo.InvariantCulture)},{plane.XAxis.Z.ToString(CultureInfo.InvariantCulture)};" +
                       $"{plane.YAxis.X.ToString(CultureInfo.InvariantCulture)},{plane.YAxis.Y.ToString(CultureInfo.InvariantCulture)},{plane.YAxis.Z.ToString(CultureInfo.InvariantCulture)};" +
                       $"{x.T0.ToString(CultureInfo.InvariantCulture)},{x.T1.ToString(CultureInfo.InvariantCulture)};" +
                       $"{y.T0.ToString(CultureInfo.InvariantCulture)},{y.T1.ToString(CultureInfo.InvariantCulture)};" +
                       $"{z.T0.ToString(CultureInfo.InvariantCulture)},{z.T1.ToString(CultureInfo.InvariantCulture)}";
            }

            throw new ArgumentException($"Value must be of type Box, got {value?.GetType().Name ?? "null"}");
        }

        /// <inheritdoc/>
        public object Deserialize(string value)
        {
            if (!Validate(value))
            {
                throw new FormatException($"Invalid Box format: '{value}'. Expected format: 'boxOXY:ox,oy,oz;xx,xy,xz;yx,yy,yz;x0,x1;y0,y1;z0,z1' with valid doubles.");
            }

            var valueWithoutPrefix = value.Substring(value.IndexOf(':') + 1);
            var parts = valueWithoutPrefix.Split(';');

            // Parse origin
            var originParts = parts[0].Split(',');
            var origin = new Point3d(
                double.Parse(originParts[0], CultureInfo.InvariantCulture),
                double.Parse(originParts[1], CultureInfo.InvariantCulture),
                double.Parse(originParts[2], CultureInfo.InvariantCulture)
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

            // Parse intervals
            var xIntervalParts = parts[3].Split(',');
            var xInterval = new Interval(
                double.Parse(xIntervalParts[0], CultureInfo.InvariantCulture),
                double.Parse(xIntervalParts[1], CultureInfo.InvariantCulture)
            );

            var yIntervalParts = parts[4].Split(',');
            var yInterval = new Interval(
                double.Parse(yIntervalParts[0], CultureInfo.InvariantCulture),
                double.Parse(yIntervalParts[1], CultureInfo.InvariantCulture)
            );

            var zIntervalParts = parts[5].Split(',');
            var zInterval = new Interval(
                double.Parse(zIntervalParts[0], CultureInfo.InvariantCulture),
                double.Parse(zIntervalParts[1], CultureInfo.InvariantCulture)
            );

            // Normalize axes to ensure they are unit vectors
            xAxis.Unitize();
            yAxis.Unitize();

            // Create plane and box using the stored X and Y axes
            var plane = new Plane(origin, xAxis, yAxis);
            return new Box(plane, xInterval, yInterval, zInterval);
        }

        /// <inheritdoc/>
        public bool Validate(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            if (!value.StartsWith("boxOXY:"))
            {
                return false;
            }

            var valueWithoutPrefix = value.Substring(7); // "boxOXY:".Length
            var parts = valueWithoutPrefix.Split(';');
            if (parts.Length != 6)
            {
                return false;
            }

            // Validate origin (3 doubles)
            var originParts = parts[0].Split(',');
            if (originParts.Length != 3)
                return false;
            foreach (var part in originParts)
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

            // Validate intervals (2 doubles each)
            for (int i = 3; i <= 5; i++)
            {
                var intervalParts = parts[i].Split(',');
                if (intervalParts.Length != 2)
                    return false;
                foreach (var part in intervalParts)
                {
                    if (!double.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                        return false;
                }
            }

            return true;
        }
    }
}
