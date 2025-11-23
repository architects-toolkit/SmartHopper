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
using Rhino.Geometry;

namespace SmartHopper.Core.Serialization.DataTypes.Serializers
{
    /// <summary>
    /// Serializer for Rhino.Geometry.Line type.
    /// Format: "line2p:x1,y1,z1;x2,y2,z2" (e.g., "line2p:0,0,0;10,10,10").
    /// </summary>
    public class LineSerializer : IDataTypeSerializer
    {
        /// <inheritdoc/>
        public string TypeName => "Line";

        /// <inheritdoc/>
        public Type TargetType => typeof(Line);

        /// <inheritdoc/>
        public string Serialize(object value)
        {
            if (value is Line line)
            {
                return $"line2p:{line.From.X.ToString(CultureInfo.InvariantCulture)},{line.From.Y.ToString(CultureInfo.InvariantCulture)},{line.From.Z.ToString(CultureInfo.InvariantCulture)};" +
                       $"{line.To.X.ToString(CultureInfo.InvariantCulture)},{line.To.Y.ToString(CultureInfo.InvariantCulture)},{line.To.Z.ToString(CultureInfo.InvariantCulture)}";
            }

            throw new ArgumentException($"Value must be of type Line, got {value?.GetType().Name ?? "null"}");
        }

        /// <inheritdoc/>
        public object Deserialize(string value)
        {
            if (!Validate(value))
            {
                throw new FormatException($"Invalid Line format: '{value}'. Expected format: 'line2p:x1,y1,z1;x2,y2,z2' with valid doubles.");
            }

            var valueWithoutPrefix = value.Substring(value.IndexOf(':') + 1);
            var points = valueWithoutPrefix.Split(';');
            var fromParts = points[0].Split(',');
            var toParts = points[1].Split(',');

            double x1 = double.Parse(fromParts[0], CultureInfo.InvariantCulture);
            double y1 = double.Parse(fromParts[1], CultureInfo.InvariantCulture);
            double z1 = double.Parse(fromParts[2], CultureInfo.InvariantCulture);
            double x2 = double.Parse(toParts[0], CultureInfo.InvariantCulture);
            double y2 = double.Parse(toParts[1], CultureInfo.InvariantCulture);
            double z2 = double.Parse(toParts[2], CultureInfo.InvariantCulture);

            return new Line(new Point3d(x1, y1, z1), new Point3d(x2, y2, z2));
        }

        /// <inheritdoc/>
        public bool Validate(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            if (!value.StartsWith("line2p:"))
            {
                return false;
            }

            var valueWithoutPrefix = value.Substring(7); // "line2p:".Length
            var points = valueWithoutPrefix.Split(';');
            if (points.Length != 2)
            {
                return false;
            }

            foreach (var point in points)
            {
                var parts = point.Split(',');
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
