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
 * along with this library; if not, see <https://www.gnu.org/licenses/lgpl-3.0.html>.
 */

namespace SmartHopper.Infrastructure.Utilities
{
    /// <summary>
    /// Helper class for JSON path operations and error message formatting.
    /// Provides centralized utilities for working with JSON paths in both Components and Infrastructure layers.
    /// </summary>
    public static class JsonPathHelper
    {
        /// <summary>
        /// Formats an error message with JSON path information.
        /// </summary>
        /// <param name="jsonPath">The JSON path where the error occurred (e.g., "results[29].Effect").</param>
        /// <param name="errorMessage">The error message details.</param>
        /// <returns>Formatted error message string.</returns>
        public static string FormatJsonPathError(string jsonPath, string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(jsonPath))
            {
                return $"Error: {errorMessage}";
            }

            return $"JSON Path: '{jsonPath}' | Error: {errorMessage}";
        }

        /// <summary>
        /// Formats a warning message with JSON path information.
        /// </summary>
        /// <param name="jsonPath">The JSON path where the warning occurred.</param>
        /// <param name="warningMessage">The warning message details.</param>
        /// <returns>Formatted warning message string.</returns>
        public static string FormatJsonPathWarning(string jsonPath, string warningMessage)
        {
            if (string.IsNullOrWhiteSpace(jsonPath))
            {
                return warningMessage;
            }

            return $"JSON Path: '{jsonPath}' | {warningMessage}";
        }

        /// <summary>
        /// Formats a validation error with JSON path information.
        /// </summary>
        /// <param name="jsonPath">The JSON path where the validation error occurred.</param>
        /// <param name="validationMessage">The validation error message.</param>
        /// <returns>Formatted validation error message string.</returns>
        public static string FormatJsonPathValidationError(string jsonPath, string validationMessage)
        {
            if (string.IsNullOrWhiteSpace(jsonPath))
            {
                return $"Validation Error: {validationMessage}";
            }

            return $"JSON Path: '{jsonPath}' | Validation Error: {validationMessage}";
        }

        /// <summary>
        /// Formats a field mapping error with the source path information.
        /// </summary>
        /// <param name="sourcePath">The source path being mapped (e.g., "Request.PropertyName").</param>
        /// <param name="targetKey">The target JSON key being mapped to.</param>
        /// <param name="errorMessage">The mapping error message.</param>
        /// <returns>Formatted mapping error message string.</returns>
        public static string FormatJsonPathMappingError(string sourcePath, string targetKey, string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                return $"Mapping to '{targetKey}': {errorMessage}";
            }

            return $"JSON Path: '{targetKey}' (from '{sourcePath}') | Error: {errorMessage}";
        }
    }
}
