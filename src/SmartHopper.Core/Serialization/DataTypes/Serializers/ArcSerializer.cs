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
    /// Serializer for Rhino.Geometry.Arc type.
    /// Format: "arcCNRAB:cx,cy,cz;nx,ny,nz;r;a1;a2" (circle + start/end angles).
    /// </summary>
    public class ArcSerializer : IDataTypeSerializer
    {
        /// <inheritdoc/>
        public string TypeName => "Arc";

        /// <inheritdoc/>
        public Type TargetType => typeof(Arc);

        /// <inheritdoc/>
        public string Serialize(object value)
        {
            if (value is Arc arc)
            {
                return $"arcCNRAB:{arc.Center.X.ToString(CultureInfo.InvariantCulture)},{arc.Center.Y.ToString(CultureInfo.InvariantCulture)},{arc.Center.Z.ToString(CultureInfo.InvariantCulture)};" +
                       $"{arc.Plane.Normal.X.ToString(CultureInfo.InvariantCulture)},{arc.Plane.Normal.Y.ToString(CultureInfo.InvariantCulture)},{arc.Plane.Normal.Z.ToString(CultureInfo.InvariantCulture)};" +
                       $"{arc.Radius.ToString(CultureInfo.InvariantCulture)};{arc.StartAngle.ToString(CultureInfo.InvariantCulture)};{arc.EndAngle.ToString(CultureInfo.InvariantCulture)}";
            }

            throw new ArgumentException($"Value must be of type Arc, got {value?.GetType().Name ?? "null"}");
        }

        /// <inheritdoc/>
        public object Deserialize(string value)
        {
            if (!Validate(value))
            {
                throw new FormatException($"Invalid Arc format: '{value}'. Expected format: 'arcCNRAB:cx,cy,cz;nx,ny,nz;r;a1;a2' with valid doubles and r > 0.");
            }

            var valueWithoutPrefix = value.Substring(value.IndexOf(':') + 1);
            var parts = valueWithoutPrefix.Split(';');
            var centerParts = parts[0].Split(',');
            var normalParts = parts[1].Split(',');

            double cx = double.Parse(centerParts[0], CultureInfo.InvariantCulture);
            double cy = double.Parse(centerParts[1], CultureInfo.InvariantCulture);
            double cz = double.Parse(centerParts[2], CultureInfo.InvariantCulture);
            double nx = double.Parse(normalParts[0], CultureInfo.InvariantCulture);
            double ny = double.Parse(normalParts[1], CultureInfo.InvariantCulture);
            double nz = double.Parse(normalParts[2], CultureInfo.InvariantCulture);
            double r = double.Parse(parts[2], CultureInfo.InvariantCulture);
            double a1 = double.Parse(parts[3], CultureInfo.InvariantCulture);
            double a2 = double.Parse(parts[4], CultureInfo.InvariantCulture);

            var center = new Point3d(cx, cy, cz);
            var normal = new Vector3d(nx, ny, nz);
            var plane = new Plane(center, normal);
            var circle = new Circle(plane, r);
            var angleInterval = new Interval(a1, a2);

            return new Arc(circle, angleInterval);
        }

        /// <inheritdoc/>
        public bool Validate(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            if (!value.StartsWith("arcCNRAB:"))
            {
                return false;
            }

            var valueWithoutPrefix = value.Substring(9); // "arcCNRAB:".Length
            var parts = valueWithoutPrefix.Split(';');
            if (parts.Length != 5)
            {
                return false;
            }

            // Validate center
            var centerParts = parts[0].Split(',');
            if (centerParts.Length != 3) return false;
            foreach (var part in centerParts)
            {
                if (!double.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out _)) return false;
            }

            // Validate normal
            var normalParts = parts[1].Split(',');
            if (normalParts.Length != 3) return false;
            foreach (var part in normalParts)
            {
                if (!double.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out _)) return false;
            }

            // Validate radius
            if (!double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double radius)) return false;
            if (radius <= 0) return false;

            // Validate angles
            if (!double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out _)) return false;
            if (!double.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out _)) return false;

            return true;
        }
    }
}
