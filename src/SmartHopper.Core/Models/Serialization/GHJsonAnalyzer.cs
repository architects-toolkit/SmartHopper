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
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SmartHopper.Core.Models.Serialization
{
    /// <summary>
    /// Utilities to validate Grasshopper JSON (GhJSON) format.
    /// </summary>
    public static class GHJsonAnalyzer
    {
        /// <summary>
        /// Validates that the given JSON string conforms to the expected Grasshopper document JSON format.
        /// </summary>
        /// <param name="json">The JSON string to validate.</param>
        /// <param name="errorMessage">Aggregated error, warning and info messages; null if no issues.</param>
        /// <returns>True if no errors; otherwise false.</returns>
        public static bool Analyze(string json, out string errorMessage)
        {
            var errors = new List<string>();
            var warnings = new List<string>();
            var infos = new List<string>();

            if (string.IsNullOrWhiteSpace(json))
            {
                errors.Add("JSON input is null or empty.");
            }

            JObject root = null;
            if (!errors.Any())
            {
                try
                {
                    root = JObject.Parse(json);
                }
                catch (JsonReaderException ex)
                {
                    errors.Add($"Invalid JSON: {ex.Message}");
                }
            }

            JArray components = null;
            JArray connections = null;
            if (root != null)
            {
                if (root["components"] is JArray comps)
                {
                    components = comps;
                }
                else
                {
                    errors.Add("'components' property is missing or not an array.");
                }

                if (root["connections"] is JArray conns)
                {
                    connections = conns;
                }
                else
                {
                    errors.Add("'connections' property is missing or not an array.");
                }

                if (components != null)
                {
                    for (int i = 0; i < components.Count; i++)
                    {
                        if (!(components[i] is JObject comp))
                        {
                            errors.Add($"components[{i}] is not a JSON object.");
                            continue;
                        }

                        if (comp["name"] == null || comp["name"].Type == JTokenType.Null)
                            errors.Add($"components[{i}].name is missing or null.");

                        if (comp["componentGuid"] == null || comp["componentGuid"].Type == JTokenType.Null)
                        {
                            errors.Add($"components[{i}].componentGuid is missing or null.");
                        }
                        else
                        {
                            var cg = comp["componentGuid"].ToString();
                            if (!Guid.TryParse(cg, out _))
                                warnings.Add($"components[{i}].componentGuid '{cg}' is not a valid GUID.");
                        }

                        if (comp["instanceGuid"] == null || comp["instanceGuid"].Type == JTokenType.Null)
                        {
                            errors.Add($"components[{i}].instanceGuid is missing or null.");
                        }
                        else
                        {
                            var ig = comp["instanceGuid"].ToString();
                            if (!Guid.TryParse(ig, out _))
                                warnings.Add($"components[{i}].instanceGuid '{ig}' is not a valid GUID.");
                        }

                        if (comp["type"] == null || comp["type"].Type == JTokenType.Null)
                            infos.Add($"components[{i}].type is missing or null.");

                        if (comp["objectType"] == null || comp["objectType"].Type == JTokenType.Null)
                            infos.Add($"components[{i}].objectType is missing or null.");

                        // Component existence validation
                        if (comp["componentGuid"] != null && comp["componentGuid"].Type != JTokenType.Null)
                        {
                            var componentGuid = comp["componentGuid"].ToString();
                            if (Guid.TryParse(componentGuid, out var guid))
                            {
                                if (!IsValidGrasshopperComponent(guid))
                                {
                                    var componentName = comp["name"]?.ToString() ?? "Unknown";
                                    errors.Add($"components[{i}] with name '{componentName}' and GUID '{componentGuid}' does not exist in the Grasshopper system.");
                                }
                            }
                        }
                    }
                }

                if (connections != null)
                {
                    // Verify connection 'componentId' references existing component instanceGuids
                    var definedIds = new HashSet<string>();
                    foreach (var token in components)
                    {
                        if (token is JObject compObj)
                        {
                            var inst = compObj["instanceGuid"]?.ToString();
                            if (!string.IsNullOrEmpty(inst))
                                definedIds.Add(inst);
                        }
                    }

                    for (int i = 0; i < connections.Count; i++)
                    {
                        if (!(connections[i] is JObject conn))
                        {
                            errors.Add($"connections[{i}] is not a JSON object.");
                            continue;
                        }

                        foreach (var endPoint in new[] { "from", "to" })
                        {
                            if (!(conn[endPoint] is JObject ep))
                            {
                                errors.Add($"connections[{i}].{endPoint} is missing or not an object.");
                                continue;
                            }

                            if (ep["componentId"] == null || ep["componentId"].Type == JTokenType.Null)
                            {
                                errors.Add($"connections[{i}].{endPoint}.componentId is missing or null.");
                            }
                            else
                            {
                                var cid = ep["componentId"].ToString();
                                if (!Guid.TryParse(cid, out _))
                                    warnings.Add($"connections[{i}].{endPoint}.componentId '{cid}' is not a valid GUID.");
                                if (!definedIds.Contains(cid))
                                    errors.Add($"connections[{i}].{endPoint}.componentId '{cid}' is not defined in components[].instanceGuid.");
                            }
                        }
                    }

                    // Connection data type compatibility validation
                    var connectionIssues = ValidateConnectionDataTypes(connections, components);
                    warnings.AddRange(connectionIssues); // Treating as warnings
                }
            }

            var sb = new StringBuilder();
            if (errors.Any())
            {
                sb.AppendLine("Errors:");
                foreach (var e in errors) sb.AppendLine($"- {e}");
            }
            if (warnings.Any())
            {
                sb.AppendLine("Warnings:");
                foreach (var w in warnings) sb.AppendLine($"- {w}");
            }
            if (infos.Any())
            {
                sb.AppendLine("Information:");
                foreach (var info in infos) sb.AppendLine($"- {info}");
            }

            var result = !errors.Any();
            errorMessage = sb.Length > 0 ? sb.ToString().TrimEnd() : null;
            return result;
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
                var objectProxy = Grasshopper.Instances.ComponentServer.FindObjectByGuid(componentGuid, false);
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

                if (string.IsNullOrEmpty(fromInstanceId) || string.IsNullOrEmpty(toInstanceId)) continue;
                if (string.IsNullOrEmpty(fromParameterName) || string.IsNullOrEmpty(toParameterName)) continue;

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
        private static string CheckDataTypeCompatibility(Guid fromComponentGuid, Guid toComponentGuid, string fromParameterName, string toParameterName)
        {
            try
            {
                // Get component proxies
                var fromProxy = Grasshopper.Instances.ComponentServer.FindObjectByGuid(fromComponentGuid, false);
                var toProxy = Grasshopper.Instances.ComponentServer.FindObjectByGuid(toComponentGuid, false);

                if (fromProxy == null || toProxy == null)
                    return null; // Can't validate if components don't exist

                // Create temporary instances to check parameter types
                var fromObj = fromProxy.CreateInstance();
                var toObj = toProxy.CreateInstance();

                if (fromObj == null || toObj == null)
                    return null;

                // Find parameters by name
                var outputParam = fromObj.Params.Output.FirstOrDefault(p => p.Name.Equals(fromParameterName, StringComparison.OrdinalIgnoreCase));
                var inputParam = toObj.Params.Input.FirstOrDefault(p => p.Name.Equals(toParameterName, StringComparison.OrdinalIgnoreCase));

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
