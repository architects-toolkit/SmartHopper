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
    /// Serializer for Rhino.Geometry.Interval type.
    /// Format: "domain:min<max" (e.g., "domain:0.0<10.0").
    /// </summary>
    public class DomainSerializer : IDataTypeSerializer
    {
        /// <inheritdoc/>
        public string TypeName => "Domain";

        /// <inheritdoc/>
        public Type TargetType => typeof(Interval);

        /// <inheritdoc/>
        public string Serialize(object value)
        {
            if (value is Interval interval)
            {
                return $"domain:{interval.Min.ToString(CultureInfo.InvariantCulture)}<{interval.Max.ToString(CultureInfo.InvariantCulture)}";
            }

            throw new ArgumentException($"Value must be of type Interval, got {value?.GetType().Name ?? "null"}");
        }

        /// <inheritdoc/>
        public object Deserialize(string value)
        {
            if (!Validate(value))
            {
                throw new FormatException($"Invalid Domain format: '{value}'. Expected format: 'domain:min<max' with valid doubles.");
            }

            var valueWithoutPrefix = value.Substring(value.IndexOf(':') + 1);
            var parts = valueWithoutPrefix.Split('<');
            double min = double.Parse(parts[0], CultureInfo.InvariantCulture);
            double max = double.Parse(parts[1], CultureInfo.InvariantCulture);

            return new Interval(min, max);
        }

        /// <inheritdoc/>
        public bool Validate(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            if (!value.StartsWith("domain:"))
            {
                return false;
            }

            var valueWithoutPrefix = value.Substring(7); // "domain:".Length
            var parts = valueWithoutPrefix.Split('<');
            if (parts.Length != 2)
            {
                return false;
            }

            if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double min))
            {
                return false;
            }

            if (!double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double max))
            {
                return false;
            }

            // Allow min == max for degenerate intervals
            return min <= max;
        }
    }
}
