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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Grasshopper.Kernel;
using Newtonsoft.Json.Linq;
using SmartHopper.Core.Models.Serialization;

namespace SmartHopper.Core.Grasshopper.Utils
{
    /// <summary>
    /// Grasshopper-specific utilities for validating GhJSON format with Grasshopper component validation.
    /// </summary>
    public static class GHJsonLocal
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
            if (string.IsNullOrEmpty(validationMessage)) return;
            
            var lines = validationMessage.Split('\n');
            var currentSection = "";
            
            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                if (string.IsNullOrEmpty(trimmedLine)) continue;
                
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
                            errors.Add(trimmedLine.Substring(2));
                        break;
                    case "warnings":
                        if (trimmedLine.StartsWith("- "))
                            warnings.Add(trimmedLine.Substring(2));
                        break;
                    case "infos":
                        if (trimmedLine.StartsWith("- "))
                            infos.Add(trimmedLine.Substring(2));
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
            if (components == null) return issues;
            
            for (int i = 0; i < components.Count; i++)
            {
                if (!(components[i] is JObject comp)) continue;
                
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
                    result.AppendLine($"- {error}");
            }
            
            if (warnings.Any())
            {
                if (result.Length > 0) result.AppendLine();
                result.AppendLine("Warnings:");
                foreach (var warning in warnings)
                    result.AppendLine($"- {warning}");
            }
            
            if (infos.Any())
            {
                if (result.Length > 0) result.AppendLine();
                result.AppendLine("Information:");
                foreach (var info in infos)
                    result.AppendLine($"- {info}");
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
                var objectProxy = GHObjectFactory.FindProxy(componentGuid);
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
            if (connections == null || components == null) return issues;

            Debug.WriteLine($"[ValidateConnectionDataTypes] Validating {connections.Count} connections and {components.Count} components...");

            // Create a lookup for component information
            var componentLookup = new Dictionary<string, JObject>();
            foreach (var token in components)
            {
                if (token is JObject comp && comp["instanceGuid"]?.ToString() is string instanceGuid)
                {
                    componentLookup[instanceGuid] = comp;
                }
            }

            for (int i = 0; i < connections.Count; i++)
            {
                if (!(connections[i] is JObject conn)) continue;

                var fromEndpoint = conn["from"] as JObject;
                var toEndpoint = conn["to"] as JObject;

                if (fromEndpoint == null || toEndpoint == null) continue;

                var fromInstanceId = fromEndpoint["instanceId"]?.ToString();
                var toInstanceId = toEndpoint["instanceId"]?.ToString();
                var fromParameterName = fromEndpoint["name"]?.ToString();
                var toParameterName = toEndpoint["name"]?.ToString();

                Debug.WriteLine($"[ValidateConnectionDataTypes] Validating connection {i} from {fromInstanceId} to {toInstanceId}...");

                if (string.IsNullOrEmpty(fromInstanceId) || string.IsNullOrEmpty(toInstanceId))
                {
                    Debug.WriteLine($"[ValidateConnectionDataTypes] Skipping connection {i} due to missing instance IDs");
                    continue;
                }
                if (string.IsNullOrEmpty(fromParameterName) || string.IsNullOrEmpty(toParameterName))
                {
                    Debug.WriteLine($"[ValidateConnectionDataTypes] Skipping connection {i} due to missing parameter names");
                    continue;
                }

                if (componentLookup.TryGetValue(fromInstanceId, out var fromComponent) &&
                    componentLookup.TryGetValue(toInstanceId, out var toComponent))
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
                            Debug.WriteLine($"[ValidateConnectionDataTypes] Connection compatible :)");
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
        private static string CheckDataTypeCompatibility(Guid fromComponentGuid, Guid toComponentGuid, string fromParameterName, string toParameterName)
        {
            try
            {
                // Get component proxies
                var fromProxy = GHObjectFactory.FindProxy(fromComponentGuid);
                var toProxy = GHObjectFactory.FindProxy(toComponentGuid);

                if (fromProxy == null || toProxy == null)
                    return null; // Can't validate if components don't exist

                // Create temporary instances to check parameter types
                var fromObj = GHObjectFactory.CreateInstance(fromProxy);
                var toObj = GHObjectFactory.CreateInstance(toProxy);

                if (fromObj == null || toObj == null)
                    return null;

                // Find source parameter (following Put.cs pattern)
                IGH_Param outputParam = null;
                if (fromObj is IGH_Component fromComponent)
                    outputParam = GHParameterUtils.GetOutputByName(fromComponent, fromParameterName);
                else if (fromObj is IGH_Param fromParam)
                    outputParam = fromParam; // IGH_Param objects ARE the parameter

                // Find target parameter (following Put.cs pattern)
                IGH_Param inputParam = null;
                if (toObj is IGH_Component toComponent)
                    inputParam = GHParameterUtils.GetInputByName(toComponent, toParameterName);
                else if (toObj is IGH_Param toParam)
                    inputParam = toParam; // IGH_Param objects ARE the parameter

                if (outputParam == null)
                    return $"Output parameter '{fromParameterName}' not found";
                if (inputParam == null)
                    return $"Input parameter '{toParameterName}' not found";

                // Basic type compatibility check
                if (outputParam != null && inputParam != null)
                {
                    var outputType = outputParam.Type;
                    var inputType = inputParam.Type;

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
        /// Checks if two parameter types are compatible for connections.
        /// </summary>
        /// <param name="outputType">Type of the output parameter.</param>
        /// <param name="inputType">Type of the input parameter.</param>
        /// <returns>True if compatible; otherwise false.</returns>
        private static bool AreTypesCompatible(Type outputType, Type inputType)
        {
            if (outputType == null || inputType == null) return true; // Can't validate
            if (outputType == inputType) return true; // Same type
            if (inputType.IsAssignableFrom(outputType)) return true; // Output can be assigned to input

            // Check for common Grasshopper type conversions
            var outputName = outputType.Name.ToLower();
            var inputName = inputType.Name.ToLower();

            // Allow numeric conversions
            var numericTypes = new[] { "int", "double", "float", "decimal", "number" };
            if (numericTypes.Any(t => outputName.Contains(t)) && numericTypes.Any(t => inputName.Contains(t)))
                return true;

            // Allow geometry type conversions (common in Grasshopper)
            var geometryTypes = new[] { "point", "curve", "surface", "mesh", "geometry", "brep" };
            if (geometryTypes.Any(t => outputName.Contains(t)) && geometryTypes.Any(t => inputName.Contains(t)))
                return true;

            // Allow string conversions (most things can be converted to string)
            if (inputName.Contains("string") || inputName.Contains("text"))
                return true;

            return false; // Types seem incompatible
        }
    }
}
