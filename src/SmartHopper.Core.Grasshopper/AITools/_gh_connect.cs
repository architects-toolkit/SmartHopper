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
using System.Linq;
using System.Threading.Tasks;
using Grasshopper;
using Grasshopper.Kernel;
using Newtonsoft.Json.Linq;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AICall.Core.Returns;
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.Infrastructure.AITools;

namespace SmartHopper.Core.Grasshopper.AITools
{
    /// <summary>
    /// Tool provider for connecting Grasshopper components together.
    /// Creates wires between component parameters on the canvas.
    /// </summary>
    public class gh_connect : IAIToolProvider
    {
        /// <summary>
        /// Name of the AI tool provided by this class.
        /// </summary>
        private readonly string toolName = "gh_connect";

        /// <summary>
        /// Returns the GH connect tool.
        /// </summary>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: this.toolName,
                description: "Connect Grasshopper components together by creating wires between outputs and inputs. Use this to establish data flow between existing components on the canvas. Requires component GUIDs (use gh_get_selected or gh_get to find them first).",

                // category: "Components",
                category: "NotTested",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""connections"": {
                            ""type"": ""array"",
                            ""description"": ""Array of connection specifications"",
                            ""items"": {
                                ""type"": ""object"",
                                ""properties"": {
                                    ""sourceGuid"": {
                                        ""type"": ""string"",
                                        ""description"": ""GUID of the source component (output side)""
                                    },
                                    ""sourceParam"": {
                                        ""type"": ""string"",
                                        ""description"": ""Name or nickname of the output parameter. If not specified, uses the first output.""
                                    },
                                    ""targetGuid"": {
                                        ""type"": ""string"",
                                        ""description"": ""GUID of the target component (input side)""
                                    },
                                    ""targetParam"": {
                                        ""type"": ""string"",
                                        ""description"": ""Name or nickname of the input parameter. If not specified, uses the first input.""
                                    }
                                },
                                ""required"": [""sourceGuid"", ""targetGuid""]
                            }
                        }
                    },
                    ""required"": [""connections""]
                }",
                execute: this.GhConnectToolAsync);
        }

        /// <summary>
        /// Executes the GH connect tool.
        /// </summary>
        private Task<AIReturn> GhConnectToolAsync(AIToolCall toolCall)
        {
            var output = new AIReturn()
            {
                Request = toolCall,
            };

            try
            {
                AIInteractionToolCall toolInfo = toolCall.GetToolCall();
                var args = toolInfo.Arguments ?? new JObject();
                var connectionsArray = args["connections"] as JArray;

                if (connectionsArray == null || !connectionsArray.Any())
                {
                    output.CreateError("The 'connections' array is required and must contain at least one connection specification.");
                    return Task.FromResult(output);
                }

                var doc = Instances.ActiveCanvas?.Document;
                if (doc == null)
                {
                    output.CreateError("No active Grasshopper document found.");
                    return Task.FromResult(output);
                }

                var successfulConnections = new List<JObject>();
                var failedConnections = new List<JObject>();

                foreach (var connSpec in connectionsArray)
                {
                    var sourceGuidStr = connSpec["sourceGuid"]?.ToString();
                    var targetGuidStr = connSpec["targetGuid"]?.ToString();
                    var sourceParamName = connSpec["sourceParam"]?.ToString();
                    var targetParamName = connSpec["targetParam"]?.ToString();

                    if (string.IsNullOrEmpty(sourceGuidStr) || string.IsNullOrEmpty(targetGuidStr))
                    {
                        failedConnections.Add(new JObject
                        {
                            ["error"] = "Missing sourceGuid or targetGuid",
                            ["spec"] = connSpec
                        });
                        continue;
                    }

                    if (!Guid.TryParse(sourceGuidStr, out var sourceGuid) || !Guid.TryParse(targetGuidStr, out var targetGuid))
                    {
                        failedConnections.Add(new JObject
                        {
                            ["error"] = "Invalid GUID format",
                            ["sourceGuid"] = sourceGuidStr,
                            ["targetGuid"] = targetGuidStr
                        });
                        continue;
                    }

                    // Use utility to connect components
                    bool success = TryConnect(sourceGuid, targetGuid, sourceParamName, targetParamName);

                    if (success)
                    {
                        successfulConnections.Add(new JObject
                        {
                            ["sourceGuid"] = sourceGuidStr,
                            ["targetGuid"] = targetGuidStr,
                            ["sourceParam"] = sourceParamName ?? "(first output)",
                            ["targetParam"] = targetParamName ?? "(first input)",
                            ["status"] = "connected"
                        });
                    }
                    else
                    {
                        failedConnections.Add(new JObject
                        {
                            ["error"] = "Connection failed - check component GUIDs and parameter names",
                            ["sourceGuid"] = sourceGuidStr,
                            ["targetGuid"] = targetGuidStr
                        });
                    }
                }

                // Redraw once after all connections
                if (successfulConnections.Any())
                {
                    doc.NewSolution(false);
                    Instances.RedrawCanvas();
                }

                var toolResult = new JObject
                {
                    ["successful"] = JArray.FromObject(successfulConnections),
                    ["failed"] = JArray.FromObject(failedConnections),
                    ["successCount"] = successfulConnections.Count,
                    ["failCount"] = failedConnections.Count
                };

                var body = AIBodyBuilder.Create()
                    .AddToolResult(toolResult, id: toolInfo.Id, name: toolInfo.Name ?? this.toolName)
                    .Build();

                output.CreateSuccess(body, toolCall);
                return Task.FromResult(output);
            }
            catch (Exception ex)
            {
                output.CreateError($"Error connecting components: {ex.Message}");
                return Task.FromResult(output);
            }
        }

        private static bool TryConnect(Guid sourceGuid, Guid targetGuid, string? sourceParamName, string? targetParamName)
        {
            var doc = Instances.ActiveCanvas?.Document;
            if (doc == null)
            {
                return false;
            }

            var sourceObj = doc.Objects.FirstOrDefault(o => o.InstanceGuid == sourceGuid);
            var targetObj = doc.Objects.FirstOrDefault(o => o.InstanceGuid == targetGuid);

            if (sourceObj == null || targetObj == null)
            {
                return false;
            }

            var sourceParams = GetOutputs(sourceObj);
            var targetParams = GetInputs(targetObj);

            if (sourceParams.Count == 0 || targetParams.Count == 0)
            {
                return false;
            }

            var src = FindParamByName(sourceParams, sourceParamName) ?? sourceParams[0];
            var dst = FindParamByName(targetParams, targetParamName) ?? targetParams[0];

            // Wire: add src as source for dst.
            dst.AddSource(src);
            return true;
        }

        private static List<IGH_Param> GetOutputs(IGH_DocumentObject obj)
        {
            if (obj is IGH_Component c)
            {
                return c.Params.Output.ToList();
            }

            if (obj is IGH_Param p)
            {
                return new List<IGH_Param> { p };
            }

            return new List<IGH_Param>();
        }

        private static List<IGH_Param> GetInputs(IGH_DocumentObject obj)
        {
            if (obj is IGH_Component c)
            {
                return c.Params.Input.ToList();
            }

            if (obj is IGH_Param p)
            {
                return new List<IGH_Param> { p };
            }

            return new List<IGH_Param>();
        }

        private static IGH_Param? FindParamByName(List<IGH_Param> list, string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            return list.FirstOrDefault(p =>
                string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(p.NickName, name, StringComparison.OrdinalIgnoreCase));
        }
    }
}
