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

namespace SmartHopper.Core.Serialization.DataTypes.Serializers
{
    /// <summary>
    /// Serializer for System.Drawing.Color type.
    /// Format: "argb:a,r,g,b" (e.g., "argb:255,255,128,64").
    /// </summary>
    public class ColorSerializer : IDataTypeSerializer
    {
        /// <inheritdoc/>
        public string TypeName => "Color";

        /// <inheritdoc/>
        public Type TargetType => typeof(Color);

        /// <inheritdoc/>
        public string Serialize(object value)
        {
            if (value is Color color)
            {
                return $"argb:{color.A},{color.R},{color.G},{color.B}";
            }

            throw new ArgumentException($"Value must be of type Color, got {value?.GetType().Name ?? "null"}");
        }

        /// <inheritdoc/>
        public object Deserialize(string value)
        {
            if (!Validate(value))
            {
                throw new FormatException($"Invalid Color format: '{value}'. Expected format: 'argb:a,r,g,b' with values 0-255.");
            }

            // Remove prefix
            var valueWithoutPrefix = value.Substring(value.IndexOf(':') + 1);
            var parts = valueWithoutPrefix.Split(',');
            int a = int.Parse(parts[0], CultureInfo.InvariantCulture);
            int r = int.Parse(parts[1], CultureInfo.InvariantCulture);
            int g = int.Parse(parts[2], CultureInfo.InvariantCulture);
            int b = int.Parse(parts[3], CultureInfo.InvariantCulture);

            return Color.FromArgb(a, r, g, b);
        }

        /// <inheritdoc/>
        public bool Validate(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            // Check prefix
            if (!value.StartsWith("argb:"))
            {
                return false;
            }

            var valueWithoutPrefix = value.Substring(5); // "argb:".Length
            var parts = valueWithoutPrefix.Split(',');
            if (parts.Length != 4)
            {
                return false;
            }

            foreach (var part in parts)
            {
                if (!int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out int component))
                {
                    return false;
                }

                if (component < 0 || component > 255)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
