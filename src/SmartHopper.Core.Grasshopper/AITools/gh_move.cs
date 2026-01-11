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
using Newtonsoft.Json.Linq;
using SmartHopper.Core.Grasshopper.Utils.Canvas;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AICall.Core.Returns;
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.Infrastructure.AITools;

namespace SmartHopper.Core.Grasshopper.AITools
{
    /// <summary>
    /// Tool provider for toggling Grasshopper component preview by GUID.
    /// </summary>
    public class gh_move : IAIToolProvider
    {
        /// <summary>
        /// Name of the AI tool provided by this class.
        /// </summary>
        private readonly string toolName = "gh_move";
        /// <summary>
        /// Returns AI tools for component visibility control.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: this.toolName,
                description: "Reposition components on the canvas by specifying target coordinates. Use absolute coordinates (canvas position) or relative offsets (move by delta). Useful for organizing layouts or separating component groups. Requires component GUIDs from gh_get.",
                category: "Components",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""targets"": {
                            ""type"": ""object"",
                            ""patternProperties"": {
                                ""^[0-9a-fA-F]{8}-(?:[0-9a-fA-F]{4}-){3}[0-9a-fA-F]{12}$"": {
                                    ""type"": ""object"",
                                    ""properties"": {
                                        ""x"": { ""type"": ""number"" },
                                        ""y"": { ""type"": ""number"" }
                                    },
                                    ""required"": [ ""x"", ""y"" ]
                                }
                            },
                            ""additionalProperties"": false,
                            ""description"": ""Mapping of component GUID strings to target coordinates (keys must be GUIDs).""
                        },
                        ""relative"": {
                            ""type"": ""boolean"",
                            ""description"": ""True to treat targets as relative offsets.""
                        }
                    },
                    ""required"": [ ""targets"" ]
                }",
                execute: this.GhMoveObjAsync);
        }

        private async Task<AIReturn> GhMoveObjAsync(AIToolCall toolCall)
        {
            // Prepare the output
            var output = new AIReturn()
            {
                Request = toolCall,
            };

            try
            {
                // Extract parameters
                AIInteractionToolCall toolInfo = toolCall.GetToolCall();
                var args = toolInfo.Arguments ?? new JObject();
                var targetsObj = args["targets"] as JObject;
                if (targetsObj == null)
                {
                    output.CreateError("Missing or invalid 'targets' parameter.");
                    return output;
                }

                var relative = args["relative"]?.ToObject<bool>() ?? false;
                var dict = new Dictionary<Guid, PointF>();
                foreach (var prop in targetsObj.Properties())
                {
                    if (Guid.TryParse(prop.Name, out var g))
                    {
                        var xToken = prop.Value["x"];
                        var yToken = prop.Value["y"];
                        if (xToken == null || yToken == null)
                        {
                            Debug.WriteLine($"[GhObjTools] GhMoveObjAsync: Missing 'x' or 'y' for target {prop.Name}. Skipping.");
                            continue;
                        }

                        dict[g] = new PointF(
                            xToken.ToObject<float>(),
                            yToken.ToObject<float>());
                    }
                }

                var movedList = CanvasAccess.MoveInstance(dict, relative);

                var toolResult = new JObject
                {
                    ["updated"] = JArray.FromObject(movedList.Select(g => g.ToString())),
                };

                var builder = AIBodyBuilder.Create();
                builder.AddToolResult(toolResult, toolInfo.Id, toolInfo.Name);
                var immutable = builder.Build();
                output.CreateSuccess(immutable, toolCall);
                return output;
            }
            catch (Exception ex)
            {
                output.CreateError($"Error: {ex.Message}");
                return output;
            }
        }
    }
}
