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
using System.Threading.Tasks;
using Grasshopper;
using Newtonsoft.Json.Linq;
using SmartHopper.Core.Grasshopper.Utils;
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AICall.Core.Requests;
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
                category: "Components",
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

                if (GHCanvasUtils.GetCurrentCanvas() == null)
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
                    bool success = GHConnectionUtils.ConnectComponents(
                        sourceGuid,
                        targetGuid,
                        sourceParamName,
                        targetParamName,
                        redraw: false); // We'll redraw once at the end

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
                    var doc = GHCanvasUtils.GetCurrentCanvas();
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
    }
}
