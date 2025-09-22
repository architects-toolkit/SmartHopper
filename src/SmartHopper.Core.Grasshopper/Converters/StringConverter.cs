/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using System;
using System.Drawing;
using System.Globalization;
using Grasshopper.Kernel;

namespace SmartHopper.Core.Grasshopper.Converters
{
    public class StringConverter
    {
        public static Color StringToColor(string colorString)
        {
            // If the input is HTML hex or known color name (no commas), parse with full opacity
            if (string.IsNullOrWhiteSpace(colorString))
                throw new ArgumentException("Invalid color string format. Use 'R,G,B', 'A,R,G,B', '#RRGGBB', or known color name.");
            if (!colorString.Contains(","))
            {
                var c = ColorTranslator.FromHtml(colorString);
                return Color.FromArgb(255, c.R, c.G, c.B);
            }

            // Split the input string by commas
            string[] parts = colorString.Split(',');

            // Check if the input is in RGB or ARGB format
            if (parts.Length == 3)
            {
                // Parse RGB values
                int r = int.Parse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture);
                int g = int.Parse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture);
                int b = int.Parse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture);

                // Create and return the Color object (full opacity)
                return Color.FromArgb(r, g, b);
            }
            else if (parts.Length == 4)
            {
                // Parse ARGB values
                int a = int.Parse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture);
                int r = int.Parse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture);
                int g = int.Parse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture);
                int b = int.Parse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture);

                // Create and return the Color object
                return Color.FromArgb(a, r, g, b);
            }
            else
            {
                throw new ArgumentException("Invalid color string format. Use 'R,G,B' or 'A,R,G,B'.");
            }
        }

        public static Font StringToFont(string fontString)
        {
            // Split the input string by comma
            string[] parts = fontString.Split(',');

            if (parts.Length != 2)
            {
                throw new ArgumentException("Invalid font string format. Use 'FontFamilyName, FontSize'.");
            }

            // Extract the font family name and size
            string fontFamilyName = parts[0].Trim();
            string fontSizeString = parts[1].Trim();

            // Remove "pt" from the font size string if it exists and parse the size
            if (fontSizeString.EndsWith("pt", StringComparison.OrdinalIgnoreCase))
            {
                fontSizeString = fontSizeString.Substring(0, fontSizeString.Length - 2).Trim();
            }

            if (!float.TryParse(fontSizeString, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out float fontSize))
            {
                throw new ArgumentException("Invalid font size.");
            }

            // Create and return the Font object
            return new Font(fontFamilyName, fontSize);
        }

        public static Guid StringToGuid(string guidString)
        {
            return Guid.Parse(guidString);
        }

        public static GH_DataMapping StringToGHDataMapping(object value)
        {
            if (value is string s)
            {
                // Try parse enum by name
                if (Enum.TryParse<GH_DataMapping>(s, true, out var namedMapping))
                    return namedMapping;
                // Fallback to numeric string
                if (int.TryParse(s, out var intVal))
                    value = intVal;
                else
                    return GH_DataMapping.None;
            }
            else if (value is long || value is int)
            {
                // Convert numeric types to Int32
                value = Convert.ToInt32(value);
            }
            else
            {
                // Attempt to convert other types safely
                try { value = Convert.ToInt32(value); }
                catch { return GH_DataMapping.None; }
            }

            switch (value)
            {
                case 0:
                    return GH_DataMapping.None;
                case 1:
                    return GH_DataMapping.Flatten;
                case 2:
                    return GH_DataMapping.Graft;
                default:
                    System.Diagnostics.Debug.WriteLine($"Unknown GH_DataMapping value: {value}");
                    return GH_DataMapping.None;
            }
        }
    }
}
