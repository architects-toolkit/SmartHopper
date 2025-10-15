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
    /// Serializer for Rhino.Geometry.BoundingBox type.
    /// Format: "box2p:x1,y1,z1;x2,y2,z2" (min and max corners).
    /// </summary>
    public class BoundingBoxSerializer : IDataTypeSerializer
    {
        /// <inheritdoc/>
        public string TypeName => "BoundingBox";

        /// <inheritdoc/>
        public Type TargetType => typeof(BoundingBox);

        /// <inheritdoc/>
        public string Serialize(object value)
        {
            if (value is BoundingBox bbox)
            {
                return $"box2p:{bbox.Min.X.ToString(CultureInfo.InvariantCulture)},{bbox.Min.Y.ToString(CultureInfo.InvariantCulture)},{bbox.Min.Z.ToString(CultureInfo.InvariantCulture)};" +
                       $"{bbox.Max.X.ToString(CultureInfo.InvariantCulture)},{bbox.Max.Y.ToString(CultureInfo.InvariantCulture)},{bbox.Max.Z.ToString(CultureInfo.InvariantCulture)}";
            }

            throw new ArgumentException($"Value must be of type BoundingBox, got {value?.GetType().Name ?? "null"}");
        }

        /// <inheritdoc/>
        public object Deserialize(string value)
        {
            if (!Validate(value))
            {
                throw new FormatException($"Invalid BoundingBox format: '{value}'. Expected format: 'box2p:x1,y1,z1;x2,y2,z2' with valid doubles.");
            }

            var valueWithoutPrefix = value.Substring(value.IndexOf(':') + 1);
            var points = valueWithoutPrefix.Split(';');
            var minParts = points[0].Split(',');
            var maxParts = points[1].Split(',');

            double x1 = double.Parse(minParts[0], CultureInfo.InvariantCulture);
            double y1 = double.Parse(minParts[1], CultureInfo.InvariantCulture);
            double z1 = double.Parse(minParts[2], CultureInfo.InvariantCulture);
            double x2 = double.Parse(maxParts[0], CultureInfo.InvariantCulture);
            double y2 = double.Parse(maxParts[1], CultureInfo.InvariantCulture);
            double z2 = double.Parse(maxParts[2], CultureInfo.InvariantCulture);

            return new BoundingBox(new Point3d(x1, y1, z1), new Point3d(x2, y2, z2));
        }

        /// <inheritdoc/>
        public bool Validate(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            if (!value.StartsWith("box2p:"))
            {
                return false;
            }

            var valueWithoutPrefix = value.Substring(6); // "box2p:".Length
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
