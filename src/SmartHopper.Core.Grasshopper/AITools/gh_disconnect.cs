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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GhJSON.Grasshopper;
using Grasshopper;
using Grasshopper.Kernel;
using Newtonsoft.Json.Linq;
using SmartHopper.Core.Grasshopper.Utils.Canvas;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AICall.Core.Returns;
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.Infrastructure.AITools;

namespace SmartHopper.Core.Grasshopper.AITools
{
    /// <summary>
    /// Tool provider for disconnecting Grasshopper components.
    /// Removes wires between component parameters on the canvas.
    /// </summary>
    public class gh_disconnect : IAIToolProvider
    {
        /// <summary>
        /// Name of the AI tool provided by this class.
        /// </summary>
        private readonly string toolName = "gh_disconnect";

        /// <summary>
        /// Returns the GH disconnect tool.
        /// </summary>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: this.toolName,
                description: "Disconnect Grasshopper components by removing wires between outputs and inputs. Use this to break data flow between existing components on the canvas. Requires component GUIDs (use gh_get_selected or gh_get to find them first).",
                category: "Components",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""connections"": {
                            ""type"": ""array"",
                            ""description"": ""Array of disconnection specifications"",
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
                execute: this.GhDisconnectToolAsync,
                mutatesCanvas: true,
                enabled: true,
                tags: new[] { "canvas", "components", "mutating", "connections", "disconnect" },
                outputSchema: @"{ ""type"": ""object"", ""properties"": { ""successful"": { ""type"": ""array"", ""items"": { ""type"": ""object"" } }, ""failed"": { ""type"": ""array"", ""items"": { ""type"": ""object"" } }, ""successCount"": { ""type"": ""integer"" }, ""failCount"": { ""type"": ""integer"" } } }",
                annotations: new AIToolAnnotations(destructiveHint: true));
        }

        /// <summary>
        /// Executes the GH disconnect tool.
        /// </summary>
        private async Task<AIReturn> GhDisconnectToolAsync(AIToolCall toolCall)
        {
            var output = new AIReturn()
            {
                Request = toolCall,
            };

            try
            {
                AIInteractionToolCall toolInfo = toolCall.GetToolCall();
                var args = toolInfo.GetArgumentsOrEmpty();
                var connectionsArray = args["connections"] as JArray;

                if (connectionsArray == null || !connectionsArray.Any())
                {
                    output.CreateError("The 'connections' array is required and must contain at least one disconnection specification.");
                    return output;
                }

                var doc = GhJsonGrasshopper.GetActiveDocument();
                if (doc == null)
                {
                    output.CreateError("No active Grasshopper document found.");
                    return output;
                }

                var successfulDisconnections = new List<JObject>();
                var failedDisconnections = new List<JObject>();

                foreach (var connSpec in connectionsArray)
                {
                    var sourceGuidStr = connSpec["sourceGuid"]?.ToString();
                    var targetGuidStr = connSpec["targetGuid"]?.ToString();
                    var sourceParamName = connSpec["sourceParam"]?.ToString();
                    var targetParamName = connSpec["targetParam"]?.ToString();

                    if (string.IsNullOrEmpty(sourceGuidStr) || string.IsNullOrEmpty(targetGuidStr))
                    {
                        failedDisconnections.Add(new JObject
                        {
                            ["error"] = "Missing sourceGuid or targetGuid",
                            ["spec"] = connSpec
                        });
                        continue;
                    }

                    if (!Guid.TryParse(sourceGuidStr, out var sourceGuid) || !Guid.TryParse(targetGuidStr, out var targetGuid))
                    {
                        failedDisconnections.Add(new JObject
                        {
                            ["error"] = "Invalid GUID format",
                            ["sourceGuid"] = sourceGuidStr,
                            ["targetGuid"] = targetGuidStr
                        });
                        continue;
                    }

                    if (CanvasProtection.IsProtected(sourceGuid) || CanvasProtection.IsProtected(targetGuid))
                    {
                        failedDisconnections.Add(new JObject
                        {
                            ["sourceGuid"] = sourceGuidStr,
                            ["targetGuid"] = targetGuidStr,
                            ["error"] = "Disconnection rejected because it involves a protected component.",
                        });
                        continue;
                    }

                    // Use centralized GhJSON disconnector. The caller handles a single
                    // solution recompute and canvas redraw after all disconnections are applied.
                    bool success = GhJsonGrasshopper.Disconnect(sourceGuid, targetGuid, sourceParamName, targetParamName);

                    if (success)
                    {
                        successfulDisconnections.Add(new JObject
                        {
                            ["sourceGuid"] = sourceGuidStr,
                            ["targetGuid"] = targetGuidStr,
                            ["sourceParam"] = sourceParamName ?? "(first output)",
                            ["targetParam"] = targetParamName ?? "(first input)",
                            ["status"] = "disconnected"
                        });
                    }
                    else
                    {
                        failedDisconnections.Add(new JObject
                        {
                            ["error"] = "Disconnection failed - check component GUIDs and parameter names",
                            ["sourceGuid"] = sourceGuidStr,
                            ["targetGuid"] = targetGuidStr
                        });
                    }
                }

                // Recompute the solution on the UI thread after all disconnections.
                if (successfulDisconnections.Any())
                {
                    var solutionTcs = new TaskCompletionSource<bool>();
                    Rhino.RhinoApp.InvokeOnUiThread(() =>
                    {
                        try
                        {
                            doc.NewSolution(false);
                            Instances.RedrawCanvas();
                            solutionTcs.SetResult(true);
                        }
                        catch (Exception ex)
                        {
                            solutionTcs.SetException(ex);
                        }
                    });

                    await solutionTcs.Task.ConfigureAwait(false);
                }

                var toolResult = new JObject
                {
                    ["successful"] = JArray.FromObject(successfulDisconnections),
                    ["failed"] = JArray.FromObject(failedDisconnections),
                    ["successCount"] = successfulDisconnections.Count,
                    ["failCount"] = failedDisconnections.Count
                };

                var body = AIBodyBuilder.Create()
                    .AddToolResult(toolResult, id: toolInfo.Id, name: toolInfo.Name ?? this.toolName)
                    .Build();

                output.CreateSuccess(body, toolCall);
                return output;
            }
            catch (Exception ex)
            {
                output.CreateError($"Error disconnecting components: {ex.Message}");
                return output;
            }
        }

    }
}
