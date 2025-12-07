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
        public static bool Validate(string json, out string errorMessage)
        {
            var errors = new List<string>();
            var warnings = new List<string>();
            var infos = new List<string>();

            if (string.IsNullOrWhiteSpace(json))
            {
                errors.Add("JSON input is null or empty.");
            }

            JObject? root = null;
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

            JArray? components = null;
            JArray? connections = null;
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

                // Treat missing 'connections' as empty array (no connections is valid)
                // but error if it exists and is not an array
                if (root["connections"] == null || root["connections"].Type == JTokenType.Null)
                {
                    // Missing or null connections is valid - treat as empty array
                    connections = new JArray();
                }
                else if (root["connections"] is JArray conns)
                {
                    connections = conns;
                }
                else
                {
                    errors.Add("'connections' property is not an array.");
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
                        {
                            errors.Add($"components[{i}].name is missing or null.");
                        }

                        if (comp["componentGuid"] == null || comp["componentGuid"].Type == JTokenType.Null)
                        {
                            warnings.Add($"components[{i}].componentGuid is missing or null.");
                        }
                        else
                        {
                            var cg = comp["componentGuid"].ToString();
                            if (!Guid.TryParse(cg, out _))
                            {
                                warnings.Add($"components[{i}].componentGuid '{cg}' is not a valid GUID.");
                            }
                        }

                        if (comp["instanceGuid"] == null || comp["instanceGuid"].Type == JTokenType.Null)
                        {
                            errors.Add($"components[{i}].instanceGuid is missing or null.");
                        }
                        else
                        {
                            var ig = comp["instanceGuid"].ToString();
                            if (!Guid.TryParse(ig, out _))
                            {
                                warnings.Add($"components[{i}].instanceGuid '{ig}' is not a valid GUID.");
                            }
                        }
                    }
                }

                if (connections != null)
                {
                    // Build lookup for new integer id references
                    var definedIntIds = new HashSet<int>();
                    foreach (var token in components)
                    {
                        if (token is JObject compObj && compObj["id"]?.Type == JTokenType.Integer)
                        {
                            definedIntIds.Add(compObj["id"].Value<int>());
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

                            // New schema: require integer 'id' referencing components[].id
                            if (ep["id"] == null || ep["id"].Type != JTokenType.Integer)
                            {
                                errors.Add($"connections[{i}].{endPoint}.id is missing or not an integer.");
                                continue;
                            }

                            var intId = ep["id"].Value<int>();
                            if (!definedIntIds.Contains(intId))
                            {
                                errors.Add($"connections[{i}].{endPoint}.id '{intId}' is not defined in components[].id.");
                            }
                        }
                    }
                }
            }

            var sb = new StringBuilder();
            if (errors.Any())
            {
                sb.AppendLine("Errors:");
                foreach (var e in errors)
                {
                    sb.AppendLine($"- {e}");
                }
            }

            if (warnings.Any())
            {
                sb.AppendLine("Warnings:");
                foreach (var w in warnings)
                {
                    sb.AppendLine($"- {w}");
                }
            }

            if (infos.Any())
            {
                sb.AppendLine("Information:");
                foreach (var info in infos)
                {
                    sb.AppendLine($"- {info}");
                }
            }

            var result = !errors.Any();
            errorMessage = sb.Length > 0 ? sb.ToString().TrimEnd() : null;
            return result;
        }
    }
}
