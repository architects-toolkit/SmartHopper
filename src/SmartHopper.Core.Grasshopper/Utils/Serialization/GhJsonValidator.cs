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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Newtonsoft.Json.Linq;
using SmartHopper.Core.Grasshopper.Utils.Canvas;
using SmartHopper.Core.Models.Serialization;

namespace SmartHopper.Core.Grasshopper.Utils.Serialization
{
    /// <summary>
    /// Grasshopper-specific utilities for validating GhJSON format with Grasshopper component validation.
    /// </summary>
    public static class GhJsonValidator
    {
        /// <summary>
        /// Validates that the given JSON string conforms to the expected Grasshopper document JSON format,
        /// including Grasshopper-specific component existence and data type compatibility checks.
        /// </summary>
        /// <param name="json">The JSON string to validate.</param>
        /// <param name="errorMessage">Aggregated error, warning and info messages; null if no issues.</param>
        /// <returns>True if no errors; otherwise false.</returns>
        public static bool Validate(string json, out string errorMessage)
        {
            // First run the general validation from Core
            bool coreValidationPassed = GHJsonAnalyzer.Validate(json, out var coreErrorMessage);

            var errors = new List<string>();
            var warnings = new List<string>();
            var infos = new List<string>();

            // Parse the core validation results
            if (!string.IsNullOrEmpty(coreErrorMessage))
            {
                ParseValidationMessage(coreErrorMessage, errors, warnings, infos);
            }

            // Only proceed with Grasshopper-specific validation if JSON is parseable
            if (coreValidationPassed || !errors.Any(e => e.Contains("Invalid JSON")))
            {
                try
                {
                    var root = JObject.Parse(json);
                    var components = root["components"] as JArray;
                    var connections = root["connections"] as JArray;

                    // Add Grasshopper-specific validations
                    if (components != null)
                    {
                        var componentIssues = ComponentExistenceValidation(components);
                        errors.AddRange(componentIssues); // Component existence issues are errors
                    }

                    if (components != null && connections != null)
                    {
                        var connectionIssues = ValidateConnectionDataTypes(connections, components);
                        warnings.AddRange(connectionIssues); // Data type compatibility issues are warnings
                    }
                }
                catch
                {
                    // If JSON parsing fails here, the core validation should have caught it
                    // so we don't need to add additional errors
                }
            }

            // Combine all validation results
            var result = BuildValidationResult(errors, warnings, infos, out errorMessage);
            return result;
        }

        /// <summary>
        /// Parses a validation message string into separate error, warning, and info lists.
        /// </summary>
        private static void ParseValidationMessage(string validationMessage, List<string> errors, List<string> warnings, List<string> infos)
        {
            if (string.IsNullOrEmpty(validationMessage))
            {
                return;
            }

            var lines = validationMessage.Split('\n');
            var currentSection = string.Empty;

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                if (string.IsNullOrEmpty(trimmedLine))
                {
                    continue;
                }

                if (trimmedLine.StartsWith("Errors:"))
                {
                    currentSection = "errors";
                    continue;
                }
                else if (trimmedLine.StartsWith("Warnings:"))
                {
                    currentSection = "warnings";
                    continue;
                }
                else if (trimmedLine.StartsWith("Information:"))
                {
                    currentSection = "infos";
                    continue;
                }

                // Add to appropriate list based on current section
                switch (currentSection)
                {
                    case "errors":
                        if (trimmedLine.StartsWith("- "))
                        {
                            errors.Add(trimmedLine.Substring(2));
                        }

                        break;
                    case "warnings":
                        if (trimmedLine.StartsWith("- "))
                        {
                            warnings.Add(trimmedLine.Substring(2));
                        }

                        break;
                    case "infos":
                        if (trimmedLine.StartsWith("- "))
                        {
                            infos.Add(trimmedLine.Substring(2));
                        }

                        break;
                }
            }
        }

        /// <summary>
        /// Validates component existence for Grasshopper components.
        /// </summary>
        /// <param name="components">The components array from GhJSON.</param>
        /// <returns>List of component existence issues.</returns>
        private static List<string> ComponentExistenceValidation(JArray components)
        {
            var issues = new List<string>();
            if (components == null)
            {
                return issues;
            }

            for (int i = 0; i < components.Count; i++)
            {
                if (!(components[i] is JObject comp))
                {
                    continue;
                }

                // Component existence validation
                if (comp["componentGuid"] != null && comp["componentGuid"].Type != JTokenType.Null)
                {
                    var componentGuid = comp["componentGuid"].ToString();
                    if (Guid.TryParse(componentGuid, out var guid))
                    {
                        if (!IsValidGrasshopperComponent(guid))
                        {
                            var componentName = comp["name"]?.ToString() ?? "Unknown";
                            issues.Add($"components[{i}] with name '{componentName}' and GUID '{componentGuid}' does not exist in the Grasshopper system.");
                        }
                    }
                }
            }

            return issues;
        }

        /// <summary>
        /// Builds the final validation result string from errors, warnings, and infos.
        /// </summary>
        private static bool BuildValidationResult(List<string> errors, List<string> warnings, List<string> infos, out string errorMessage)
        {
            var result = new StringBuilder();

            if (errors.Any())
            {
                result.AppendLine("Errors:");
                foreach (var error in errors)
                {
                    result.AppendLine($"- {error}");
                }
            }

            if (warnings.Any())
            {
                if (result.Length > 0)
                {
                    result.AppendLine();
                }

                result.AppendLine("Warnings:");
                foreach (var warning in warnings)
                {
                    result.AppendLine($"- {warning}");
                }
            }

            if (infos.Any())
            {
                if (result.Length > 0)
                {
                    result.AppendLine();
                }

                result.AppendLine("Information:");
                foreach (var info in infos)
                {
                    result.AppendLine($"- {info}");
                }
            }

            errorMessage = result.Length > 0 ? result.ToString().TrimEnd() : null;
            return !errors.Any(); // Return true if no errors
        }

        /// <summary>
        /// Validates if a component GUID exists in the Grasshopper system.
        /// </summary>
        /// <param name="componentGuid">The component GUID to validate.</param>
        /// <returns>True if the component exists; otherwise false.</returns>
        private static bool IsValidGrasshopperComponent(Guid componentGuid)
        {
            try
            {
                // Check if the component exists in Grasshopper's object library
                var objectProxy = ObjectFactory.FindProxy(componentGuid);
                return objectProxy != null;
            }
            catch
            {
                // If there's any error accessing Grasshopper's component server, assume invalid
                return false;
            }
        }

        /// <summary>
        /// Validates data type compatibility between connected components.
        /// </summary>
        /// <param name="connections">The connections array from GhJSON.</param>
        /// <param name="components">The components array from GhJSON.</param>
        /// <returns>List of connection data type compatibility issues.</returns>
        private static List<string> ValidateConnectionDataTypes(JArray connections, JArray components)
        {
            var issues = new List<string>();
            if (connections == null || components == null)
            {
                return issues;
            }

            Debug.WriteLine($"[ValidateConnectionDataTypes] Validating {connections.Count} connections and {components.Count} components...");

            // Create lookup for component information by integer ID
            var componentLookupById = new Dictionary<int, JObject>();
            foreach (var token in components)
            {
                if (token is JObject comp && comp["id"]?.Type == JTokenType.Integer)
                {
                    int id = comp["id"].Value<int>();
                    componentLookupById[id] = comp;
                }
            }

            for (int i = 0; i < connections.Count; i++)
            {
                if (!(connections[i] is JObject conn))
                {
                    continue;
                }

                var fromEndpoint = conn["from"] as JObject;
                var toEndpoint = conn["to"] as JObject;

                if (fromEndpoint == null || toEndpoint == null)
                {
                    continue;
                }

                // Support "paramName"
                var fromParameterName = fromEndpoint["paramName"]?.ToString();
                var toParameterName = toEndpoint["paramName"]?.ToString();

                if (string.IsNullOrEmpty(fromParameterName) || string.IsNullOrEmpty(toParameterName))
                {
                    Debug.WriteLine($"[ValidateConnectionDataTypes] Skipping connection {i} due to missing parameter names");
                    continue;
                }

                // Resolve components by integer ID
                JObject fromComponent = null;
                JObject toComponent = null;

                if (fromEndpoint["id"]?.Type == JTokenType.Integer)
                {
                    int fromId = fromEndpoint["id"].Value<int>();
                    componentLookupById.TryGetValue(fromId, out fromComponent);
                }

                if (toEndpoint["id"]?.Type == JTokenType.Integer)
                {
                    int toId = toEndpoint["id"].Value<int>();
                    componentLookupById.TryGetValue(toId, out toComponent);
                }

                Debug.WriteLine($"[ValidateConnectionDataTypes] Validating connection {i}: {fromParameterName} -> {toParameterName}");

                if (fromComponent != null && toComponent != null)
                {
                    var fromComponentGuid = fromComponent["componentGuid"]?.ToString();
                    var toComponentGuid = toComponent["componentGuid"]?.ToString();
                    var fromComponentName = fromComponent["name"]?.ToString() ?? "Unknown";
                    var toComponentName = toComponent["name"]?.ToString() ?? "Unknown";

                    // Check for basic data type compatibility issues
                    if (!string.IsNullOrEmpty(fromComponentGuid) && !string.IsNullOrEmpty(toComponentGuid))
                    {
                        if (Guid.TryParse(fromComponentGuid, out var fromGuid) &&
                            Guid.TryParse(toComponentGuid, out var toGuid))
                        {
                            var incompatibilityReason = CheckDataTypeCompatibility(fromGuid, toGuid, fromParameterName, toParameterName);
                            if (!string.IsNullOrEmpty(incompatibilityReason))
                            {
                                issues.Add($"connections[{i}]: Potential data type mismatch between '{fromComponentName}' output '{fromParameterName}' and '{toComponentName}' input '{toParameterName}'. {incompatibilityReason}");

                                Debug.WriteLine($"[ValidateConnectionDataTypes] Connection incompatible: {incompatibilityReason}");
                            }
                            else
                            {
                                Debug.WriteLine($"[ValidateConnectionDataTypes] Connection compatible :)");
                            }
                        }
                    }
                }
            }

            return issues;
        }

        /// <summary>
        /// Checks for potential data type compatibility issues between component outputs and inputs.
        /// </summary>
        /// <param name="fromComponentGuid">GUID of the source component.</param>
        /// <param name="toComponentGuid">GUID of the target component.</param>
        /// <param name="fromParameterName">Output parameter name of the source component.</param>
        /// <param name="toParameterName">Input parameter name of the target component.</param>
        /// <returns>Warning message if incompatible; null or empty if compatible.</returns>
        private static string? CheckDataTypeCompatibility(Guid fromComponentGuid, Guid toComponentGuid, string fromParameterName, string toParameterName)
        {
            try
            {
                // Get component proxies
                var fromProxy = ObjectFactory.FindProxy(fromComponentGuid);
                var toProxy = ObjectFactory.FindProxy(toComponentGuid);

                if (fromProxy == null || toProxy == null)
                {
                    return null; // Can't validate if components don't exist
                }

                // Create temporary instances to check parameter types
                var fromObj = ObjectFactory.CreateInstance(fromProxy);
                var toObj = ObjectFactory.CreateInstance(toProxy);

                if (fromObj == null || toObj == null)
                {
                    return null;
                }

                // Find source parameter (following GhJsonPlacer.cs pattern)
                IGH_Param? outputParam = null;
                if (fromObj is IGH_Component fromComponent)
                {
                    outputParam = ParameterAccess.GetOutputByName(fromComponent, fromParameterName);
                }
                else if (fromObj is IGH_Param fromParam)
                {
                    outputParam = fromParam; // IGH_Param objects ARE the parameter
                }

                // Find target parameter (following GhJsonPlacer.cs pattern)
                IGH_Param? inputParam = null;
                if (toObj is IGH_Component toComponent)
                {
                    inputParam = ParameterAccess.GetInputByName(toComponent, toParameterName);
                }
                else if (toObj is IGH_Param toParam)
                {
                    inputParam = toParam; // IGH_Param objects ARE the parameter
                }

                if (outputParam == null)
                {
                    return $"Output parameter '{fromParameterName}' not found";
                }

                if (inputParam == null)
                {
                    return $"Input parameter '{toParameterName}' not found";
                }

                // Basic type compatibility check
                if (outputParam != null && inputParam != null)
                {
                    var outputType = outputParam.Type;
                    var inputType = inputParam.Type;

                    Debug.WriteLine($"[CheckDataTypeCompatibility] Param types: {fromParameterName}({outputType.Name}) → {toParameterName}({inputType.Name})");
                    Debug.WriteLine($"[CheckDataTypeCompatibility] Full types: {outputType.FullName} → {inputType.FullName}");
                    Debug.WriteLine($"[CheckDataTypeCompatibility] Base types: output={outputType.BaseType?.Name}, input={inputType.BaseType?.Name}");

                    // If types are significantly different, warn
                    if (!AreTypesCompatible(outputType, inputType))
                    {
                        return $"Output type '{outputType.Name}' may not be compatible with input type '{inputType.Name}'";
                    }
                }

                return null; // No issues found
            }
            catch
            {
                // If any error occurs during validation, don't add warnings
                return null;
            }
        }

        /// <summary>
        /// Checks if two parameter types are compatible for connections using Grasshopper's reflection-based type system.
        /// </summary>
        /// <param name="outputType">Type of the output parameter.</param>
        /// <param name="inputType">Type of the input parameter.</param>
        /// <returns>True if compatible; otherwise false.</returns>
        private static bool AreTypesCompatible(Type outputType, Type inputType)
        {
            Debug.WriteLine($"[AreTypesCompatible] Checking: {outputType?.Name} → {inputType?.Name}");

            if (outputType == null || inputType == null)
            {
                Debug.WriteLine("[AreTypesCompatible] Null type(s), allowing connection");
                return true; // Can't validate
            }

            // Fast path: exact match or direct assignability
            if (outputType == inputType)
            {
                Debug.WriteLine("[AreTypesCompatible] Exact type match - COMPATIBLE");
                return true;
            }

            // Safe direction: output is more specific than input (e.g., GH_Point → IGH_GeometricGoo)
            if (inputType.IsAssignableFrom(outputType))
            {
                Debug.WriteLine($"[AreTypesCompatible] {outputType.Name} is assignable to {inputType.Name} (specific → general) - COMPATIBLE");
                return true;
            }

            // Unsafe direction: output is more general than input (e.g., IGH_GeometricGoo → GH_Point)
            // Check if input implements the output interface - this means output is a base type
            if (outputType.IsAssignableFrom(inputType))
            {
                Debug.WriteLine($"[AreTypesCompatible] {inputType.Name} implements {outputType.Name} (general → specific) - INCOMPATIBLE (unsafe)");
                return false; // Explicitly reject this unsafe direction
            }

            // Use Grasshopper's type conversion system via reflection
            try
            {
                // Try to create instances of IGH_Goo wrapper types
                var outputGoo = TryCreateGooInstance(outputType);
                var inputGoo = TryCreateGooInstance(inputType);

                Debug.WriteLine($"[AreTypesCompatible] Created instances: output={outputGoo?.GetType().Name ?? "null"}, input={inputGoo?.GetType().Name ?? "null"}");

                if (outputGoo != null && inputGoo != null)
                {
                    // Test if input can cast from output type using Grasshopper's CastFrom
                    bool castFromResult = inputGoo.CastFrom(outputGoo);
                    Debug.WriteLine($"[AreTypesCompatible] CastFrom test: {castFromResult}");

                    if (castFromResult)
                    {
                        Debug.WriteLine("[AreTypesCompatible] CastFrom succeeded - COMPATIBLE");
                        return true;
                    }

                    // Test QuickCast compatibility if both implement IGH_QuickCast
                    if (outputGoo is IGH_QuickCast outputQC && inputGoo is IGH_QuickCast inputQC)
                    {
                        Debug.WriteLine($"[AreTypesCompatible] QuickCast types: output={outputQC.QC_Type}, input={inputQC.QC_Type}");

                        // If they share the same QuickCast type, they're compatible
                        if (outputQC.QC_Type == inputQC.QC_Type)
                        {
                            Debug.WriteLine($"[AreTypesCompatible] Matching QuickCast type: {outputQC.QC_Type} - COMPATIBLE");
                            return true;
                        }
                    }
                    else
                    {
                        Debug.WriteLine($"[AreTypesCompatible] QuickCast not available: output={outputGoo is IGH_QuickCast}, input={inputGoo is IGH_QuickCast}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AreTypesCompatible] Reflection error: {ex.Message}");
            }

            Debug.WriteLine("[AreTypesCompatible] No compatibility found - INCOMPATIBLE");
            return false; // Types appear incompatible
        }

        /// <summary>
        /// Attempts to create an instance of an IGH_Goo type for type compatibility testing.
        /// </summary>
        /// <param name="gooType">The IGH_Goo type to instantiate.</param>
        /// <returns>An instance of the type, or null if creation fails.</returns>
        private static IGH_Goo? TryCreateGooInstance(Type gooType)
        {
            try
            {
                // Check if type implements IGH_Goo
                if (!typeof(IGH_Goo).IsAssignableFrom(gooType))
                {
                    return null;
                }

                // Try to create instance using parameterless constructor
                var constructor = gooType.GetConstructor(Type.EmptyTypes);
                if (constructor != null)
                {
                    return constructor.Invoke(null) as IGH_Goo;
                }

                // Some types might need Activator
                return Activator.CreateInstance(gooType) as IGH_Goo;
            }
            catch
            {
                return null;
            }
        }
    }
}
