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
using System.Diagnostics;
using Newtonsoft.Json.Linq;
using SmartHopper.Core.Models.Components;

namespace SmartHopper.Core.Grasshopper.Converters
{
    public static class NumberSliderUtils
    {
        public static (decimal minimum, decimal maximum, decimal value) ConvertFromJson(ComponentProperties properties)
        {
            // Extract values from the nested property structure and convert from JObject
            var currentValue = ((JObject)properties.Properties["CurrentValue"].Value)["value"].ToObject<decimal>();

            Debug.WriteLine($"Raw values from JSON - CurrentValue: {currentValue}");

            // Get instance description and parse it
            var instanceDescription = ((JObject)properties.Properties["InstanceDescription"].Value)["value"].ToString();
            var (_, lowerLimit, upperLimit) = ParseInstanceDescription(instanceDescription);

            Debug.WriteLine($"Parsed from description - Lower: {lowerLimit}, Upper: {upperLimit}");

            // When converting from JSON to GH, we use the current value directly
            return (lowerLimit, upperLimit, currentValue);
        }

        public static string FormatSliderValue(decimal minimum, decimal maximum, decimal value)
        {
            // When converting from GH to JSON, format as min<value<max
            Debug.WriteLine($"Formatting slider value - {minimum}<{value}<{maximum}");
            return $"{minimum}<{value}<{maximum}";
        }

        public static (int accuracy, decimal lowerLimit, decimal upperLimit) ParseInstanceDescription(string description)
        {
            try
            {
                // Default values in case parsing fails
                int accuracy = 0;
                decimal lowerLimit = 0;
                decimal upperLimit = 100;

                if (string.IsNullOrEmpty(description)) return (accuracy, lowerLimit, upperLimit);

                // Split into lines and process each line
                var lines = description.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    var trimmedLine = line.Trim();

                    // Parse accuracy from first line
                    if (trimmedLine.Contains("Integer"))
                    {
                        var parts = trimmedLine.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length > 1 && int.TryParse(parts[1], out int parsedAccuracy))
                        {
                            accuracy = parsedAccuracy;
                        }
                    }

                    // Parse lower limit
                    else if (trimmedLine.StartsWith("Lower limit:"))
                    {
                        var value = trimmedLine.Replace("Lower limit:", "").Trim();
                        if (decimal.TryParse(value, out decimal parsedLower))
                        {
                            lowerLimit = parsedLower;
                        }
                    }

                    // Parse upper limit
                    else if (trimmedLine.StartsWith("Upper limit:"))
                    {
                        var value = trimmedLine.Replace("Upper limit:", "").Trim();
                        if (decimal.TryParse(value, out decimal parsedUpper))
                        {
                            upperLimit = parsedUpper;
                        }
                    }
                }

                return (accuracy, lowerLimit, upperLimit);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error parsing instance description: {ex.Message}");
                return (0, 0, 100); // Return default values in case of error
            }
        }
    }
}
