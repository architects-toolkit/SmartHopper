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
using System.Drawing;
using System.Globalization;
using Newtonsoft.Json;

namespace SmartHopper.Core.Models.Serialization
{
    /// <summary>
    /// Compact position representation using string format "X,Y" instead of object format.
    /// Optimizes JSON size by ~70% compared to PointF object serialization.
    /// </summary>
    [JsonConverter(typeof(CompactPositionConverter))]
    public struct CompactPosition
    {
        /// <summary>
        /// Gets the X coordinate.
        /// </summary>
        public float X { get; }

        /// <summary>
        /// Gets the Y coordinate.
        /// </summary>
        public float Y { get; }

        /// <summary>
        /// Gets a value indicating whether this position represents an empty/unset position.
        /// </summary>
        public bool IsEmpty => Math.Abs(X) < float.Epsilon && Math.Abs(Y) < float.Epsilon;

        /// <summary>
        /// Represents an empty position (0,0).
        /// </summary>
        public static CompactPosition Empty => new CompactPosition(0, 0);

        /// <summary>
        /// Initializes a new instance of the <see cref="CompactPosition"/> struct.
        /// </summary>
        /// <param name="x">The X coordinate.</param>
        /// <param name="y">The Y coordinate.</param>
        public CompactPosition(float x, float y)
        {
            X = x;
            Y = y;
        }

        /// <summary>
        /// Implicit conversion from PointF to CompactPosition.
        /// </summary>
        /// <param name="point">The PointF to convert.</param>
        /// <returns>A new CompactPosition.</returns>
        public static implicit operator CompactPosition(PointF point)
        {
            return new CompactPosition(point.X, point.Y);
        }

        /// <summary>
        /// Implicit conversion from CompactPosition to PointF.
        /// </summary>
        /// <param name="position">The CompactPosition to convert.</param>
        /// <returns>A new PointF.</returns>
        public static implicit operator PointF(CompactPosition position)
        {
            return new PointF(position.X, position.Y);
        }

        /// <summary>
        /// Parses a compact position string in format "X,Y".
        /// </summary>
        /// <param name="value">The string to parse.</param>
        /// <returns>The parsed CompactPosition.</returns>
        public static CompactPosition Parse(string value)
        {
            if (string.IsNullOrEmpty(value))
                throw new ArgumentException("Position string cannot be null or empty", nameof(value));

            var parts = value.Split(',');
            if (parts.Length != 2)
                throw new FormatException($"Invalid position format: '{value}'. Expected format: 'X,Y'");

            if (!float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x))
                throw new FormatException($"Invalid X coordinate: '{parts[0]}'");

            if (!float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
                throw new FormatException($"Invalid Y coordinate: '{parts[1]}'");

            return new CompactPosition(x, y);
        }

        /// <summary>
        /// Converts the position to a compact string representation.
        /// </summary>
        /// <returns>String in format "X,Y".</returns>
        public override string ToString()
        {
            return $"{X.ToString(CultureInfo.InvariantCulture)},{Y.ToString(CultureInfo.InvariantCulture)}";
        }

        /// <summary>
        /// Determines whether two CompactPosition instances are equal.
        /// </summary>
        /// <param name="other">The other CompactPosition to compare.</param>
        /// <returns>True if equal, false otherwise.</returns>
        public bool Equals(CompactPosition other)
        {
            return Math.Abs(X - other.X) < float.Epsilon && Math.Abs(Y - other.Y) < float.Epsilon;
        }

        /// <summary>
        /// Determines whether this instance equals the specified object.
        /// </summary>
        /// <param name="obj">The object to compare.</param>
        /// <returns>True if equal, false otherwise.</returns>
        public override bool Equals(object obj)
        {
            return obj is CompactPosition other && Equals(other);
        }

        /// <summary>
        /// Gets the hash code for this instance.
        /// </summary>
        /// <returns>The hash code.</returns>
        public override int GetHashCode()
        {
            return HashCode.Combine(X, Y);
        }

        /// <summary>
        /// Equality operator.
        /// </summary>
        /// <param name="left">Left operand.</param>
        /// <param name="right">Right operand.</param>
        /// <returns>True if equal.</returns>
        public static bool operator ==(CompactPosition left, CompactPosition right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Inequality operator.
        /// </summary>
        /// <param name="left">Left operand.</param>
        /// <param name="right">Right operand.</param>
        /// <returns>True if not equal.</returns>
        public static bool operator !=(CompactPosition left, CompactPosition right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>
    /// JSON converter for CompactPosition that serializes as "X,Y" string.
    /// </summary>
    public class CompactPositionConverter : JsonConverter<CompactPosition>
    {
        /// <summary>
        /// Writes the JSON representation of the CompactPosition.
        /// </summary>
        /// <param name="writer">The JSON writer.</param>
        /// <param name="value">The CompactPosition value.</param>
        /// <param name="serializer">The JSON serializer.</param>
        public override void WriteJson(JsonWriter writer, CompactPosition value, JsonSerializer serializer)
        {
            writer.WriteValue(value.ToString());
        }

        /// <summary>
        /// Reads the JSON representation and converts it to CompactPosition.
        /// </summary>
        /// <param name="reader">The JSON reader.</param>
        /// <param name="objectType">The object type.</param>
        /// <param name="existingValue">The existing value.</param>
        /// <param name="hasExistingValue">Whether there's an existing value.</param>
        /// <param name="serializer">The JSON serializer.</param>
        /// <returns>The parsed CompactPosition.</returns>
        public override CompactPosition ReadJson(JsonReader reader, Type objectType, CompactPosition existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.String)
            {
                var value = reader.Value?.ToString();
                if (!string.IsNullOrEmpty(value))
                {
                    return CompactPosition.Parse(value);
                }
            }
            else if (reader.TokenType == JsonToken.StartObject)
            {
                // Handle legacy PointF format for backward compatibility
                var obj = serializer.Deserialize<PointF>(reader);
                return new CompactPosition(obj.X, obj.Y);
            }

            return new CompactPosition(0, 0);
        }
    }
}
