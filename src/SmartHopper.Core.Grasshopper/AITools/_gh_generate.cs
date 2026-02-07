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
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using GhJSON.Core;
using GhJSON.Core.SchemaModels;
using GhJSON.Core.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SmartHopper.Core.Grasshopper.Utils.Constants;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AICall.Core.Returns;
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.Infrastructure.AITools;

namespace SmartHopper.Core.Grasshopper.AITools
{
    /// <summary>
    /// Tool provider for generating GhJSON from component specifications.
    /// Creates components by name and parameters, returning GhJSON that can be passed to gh_put.
    /// </summary>
    public class gh_generate : IAIToolProvider
    {
        /// <summary>
        /// Name of the AI tool provided by this class.
        /// </summary>
        private readonly string toolName = "gh_generate";

        /// <summary>
        /// Returns the GH generate tool.
        /// </summary>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: this.toolName,
                description: "Generate GhJSON for creating a set of Grasshopper components by name and parameters. Returns a valid GhJSON structure that can be passed to gh_put to place components on canvas. Use this to create individual components or small networks when you know the exact component names. For complex networks, consider using the full gh_put workflow with AI-generated GhJSON.",

                // category: "Components",
                category: "NotTested",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""components"": {
                            ""type"": ""array"",
                            ""description"": ""Array of component specifications to generate"",
                            ""items"": {
                                ""type"": ""object"",
                                ""properties"": {
                                    ""name"": {
                                        ""type"": ""string"",
                                        ""description"": ""Component name or nickname (e.g., 'Number Slider', 'Circle', 'Addition')""
                                    },
                                    ""parameters"": {
                                        ""type"": ""object"",
                                        ""description"": ""Optional parameter values to set on the component. Keys are parameter names, values are the parameter values.""
                                    },
                                    ""position"": {
                                        ""type"": ""object"",
                                        ""description"": ""Optional position override {x, y}. If not specified, automatic layout will be used."",
                                        ""properties"": {
                                            ""x"": { ""type"": ""number"" },
                                            ""y"": { ""type"": ""number"" }
                                        }
                                    }
                                },
                                ""required"": [""name""]
                            }
                        }
                    },
                    ""required"": [""components""]
                }",
                execute: this.GhGenerateToolAsync);
        }

        /// <summary>
        /// Executes the GH generate tool.
        /// </summary>
        private Task<AIReturn> GhGenerateToolAsync(AIToolCall toolCall)
        {
            var output = new AIReturn()
            {
                Request = toolCall,
            };

            try
            {
                AIInteractionToolCall toolInfo = toolCall.GetToolCall();
                var args = toolInfo.Arguments ?? new JObject();
                var componentsArray = args["components"] as JArray;

                if (componentsArray == null || !componentsArray.Any())
                {
                    output.CreateError("The 'components' array is required and must contain at least one component specification.");
                    return Task.FromResult(output);
                }

                var ghComponents = new List<JObject>();
                var createdGuids = new List<string>();

                foreach (var compSpec in componentsArray)
                {
                    var name = compSpec["name"]?.ToString();
                    if (string.IsNullOrEmpty(name))
                    {
                        Debug.WriteLine("[gh_generate] Skipping component with no name");
                        continue;
                    }

                    // Parse position if specified
                    PointF? position = null;
                    var posToken = compSpec["position"];
                    if (posToken != null && posToken.Type == JTokenType.Object)
                    {
                        var x = posToken["x"]?.ToObject<float>() ?? 0f;
                        var y = posToken["y"]?.ToObject<float>() ?? 0f;
                        position = new PointF(x, y);
                    }

                    // Parse parameters if specified
                    Dictionary<string, object> parameters = null;
                    var paramsToken = compSpec["parameters"];
                    if (paramsToken != null && paramsToken.Type == JTokenType.Object)
                    {
                        parameters = new Dictionary<string, object>();
                        var paramsObj = paramsToken as JObject;
                        foreach (var kvp in paramsObj)
                        {
                            parameters[kvp.Key] = kvp.Value;
                        }
                    }

                    var instanceGuid = Guid.NewGuid();
                    createdGuids.Add(instanceGuid.ToString());

                    var component = new GhJsonComponent
                    {
                        Name = name,
                        InstanceGuid = instanceGuid,
                        Pivot = position.HasValue ? new GhJsonPivot(position.Value.X, position.Value.Y) : null,
                    };

                    // Store any ad-hoc parameters as extension data (best-effort).
                    if (parameters != null && parameters.Count > 0)
                    {
                        component.ComponentState = new GhJsonComponentState
                        {
                            Extensions = new Dictionary<string, object>
                            {
                                [GhJsonExtensionKeys.SmartHopperParameters] = parameters,
                            },
                        };
                    }

                    var compObj = JObject.FromObject(component);
                    ghComponents.Add(compObj);
                }

                if (!ghComponents.Any())
                {
                    output.CreateError("No valid components could be generated. Check component names.");
                    return Task.FromResult(output);
                }

                // Build GhJSON document
                var builder = GhJson.CreateDocumentBuilder();
                foreach (var compObj in ghComponents)
                {
                    try
                    {
                        var comp = compObj?.ToObject<GhJsonComponent>();
                        if (comp != null)
                        {
                            builder = builder.AddComponent(comp);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[gh_generate] Failed to deserialize component: {ex.Message}");
                    }
                }

                var doc = builder.Build();
                var ghJsonString = GhJson.ToJson(doc, new WriteOptions { Indented = true });

                var toolResult = new JObject
                {
                    ["ghjson"] = ghJsonString,
                    ["componentCount"] = ghComponents.Count,
                    ["componentGuids"] = JArray.FromObject(createdGuids),
                    ["message"] = $"Generated GhJSON for {ghComponents.Count} component(s). Pass this to gh_put to place on canvas."
                };

                var body = AIBodyBuilder.Create()
                    .AddToolResult(toolResult, id: toolInfo.Id, name: toolInfo.Name ?? this.toolName)
                    .Build();

                output.CreateSuccess(body, toolCall);
                return Task.FromResult(output);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[gh_generate] Error: {ex.Message}");
                output.CreateError($"Error generating components: {ex.Message}");
                return Task.FromResult(output);
            }
        }
    }
}
