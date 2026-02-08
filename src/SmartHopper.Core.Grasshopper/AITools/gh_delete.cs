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

/*
 * Portions of this code adapted from:
 * https://github.com/alfredatnycu/grasshopper-mcp
 * MIT License
 * Copyright (c) 2025 Alfred Chen
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using GhJSON.Grasshopper;
using Newtonsoft.Json.Linq;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AICall.Core.Returns;
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.Infrastructure.AITools;

namespace SmartHopper.Core.Grasshopper.AITools
{
    /// <summary>
    /// Tool provider for deleting Grasshopper components from the canvas.
    /// </summary>
    public class gh_delete : IAIToolProvider
    {
        /// <summary>
        /// Name of the AI tool for deleting specific components.
        /// </summary>
        private readonly string deleteToolName = "gh_delete";

        /// <summary>
        /// Name of the AI tool for clearing the canvas.
        /// </summary>
        private readonly string clearToolName = "gh_clear";

        /// <summary>
        /// Returns AI tools for component deletion operations.
        /// </summary>
        /// <returns>Enumerable of AITool instances for deletion operations.</returns>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: this.deleteToolName,
                description: "Delete specific components from the Grasshopper canvas by their GUIDs. Connected wires are automatically removed. Supports undo (Ctrl+Z). Use gh_get to retrieve component GUIDs before deletion.",
                category: "Components",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""guids"": {
                            ""type"": ""array"",
                            ""items"": {
                                ""type"": ""string"",
                                ""pattern"": ""^[0-9a-fA-F]{8}-(?:[0-9a-fA-F]{4}-){3}[0-9a-fA-F]{12}$""
                            },
                            ""description"": ""Array of component instance GUIDs to delete. Get these from gh_get."",
                            ""minItems"": 1
                        },
                        ""deleteConnectedWires"": {
                            ""type"": ""boolean"",
                            ""description"": ""Whether to delete connected wires. Default is true (Grasshopper handles this automatically)."",
                            ""default"": true
                        }
                    },
                    ""required"": [ ""guids"" ]
                }",
                execute: this.DeleteComponentsAsync);

            yield return new AITool(
                name: this.clearToolName,
                description: "Clear all components from the Grasshopper canvas. Optionally keep locked components. This is a destructive operation - use with caution. Supports undo (Ctrl+Z).",
                category: "Components",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""keepLocked"": {
                            ""type"": ""boolean"",
                            ""description"": ""If true, locked components will not be deleted. Default is false (delete everything)."",
                            ""default"": false
                        }
                    }
                }",
                execute: this.ClearCanvasAsync);
        }

        private async Task<AIReturn> DeleteComponentsAsync(AIToolCall toolCall)
        {
            var output = new AIReturn()
            {
                Request = toolCall,
            };

            try
            {
                AIInteractionToolCall toolInfo = toolCall.GetToolCall();
                var args = toolInfo.Arguments ?? new JObject();

                var guidsArray = args["guids"] as JArray;
                if (guidsArray == null || !guidsArray.Any())
                {
                    output.CreateError("Missing or empty 'guids' parameter. Provide an array of component GUIDs to delete.");
                    return output;
                }

                var deleteConnectedWires = args["deleteConnectedWires"]?.ToObject<bool>() ?? true;

                var guidList = new List<Guid>();
                var invalidGuids = new List<string>();

                foreach (var token in guidsArray)
                {
                    var guidStr = token.ToString();
                    if (Guid.TryParse(guidStr, out var guid))
                    {
                        guidList.Add(guid);
                    }
                    else
                    {
                        invalidGuids.Add(guidStr);
                        Debug.WriteLine($"[gh_delete] Invalid GUID format: {guidStr}");
                    }
                }

                if (!guidList.Any())
                {
                    output.CreateError($"No valid GUIDs provided. Invalid GUIDs: {string.Join(", ", invalidGuids)}");
                    return output;
                }

                var deleteOptions = new GhJSON.Grasshopper.DeleteOperations.DeleteOptions
                {
                    DeleteConnectedWires = deleteConnectedWires,
                    Redraw = true
                };

                var deleteResult = GhJsonGrasshopper.Delete(guidList, deleteOptions);

                var toolResult = new JObject
                {
                    ["deletedCount"] = deleteResult.DeletedCount,
                    ["deleted"] = JArray.FromObject(deleteResult.Deleted.Select(g => g.ToString())),
                };

                if (deleteResult.Failed.Any())
                {
                    toolResult["failedCount"] = deleteResult.FailedCount;
                    toolResult["failed"] = JArray.FromObject(deleteResult.Failed.Select(f => new JObject
                    {
                        ["guid"] = f.Guid.ToString(),
                        ["error"] = f.Error
                    }));
                }

                if (invalidGuids.Any())
                {
                    toolResult["invalidGuids"] = JArray.FromObject(invalidGuids);
                }

                var builder = AIBodyBuilder.Create();
                builder.AddToolResult(toolResult, toolInfo.Id, toolInfo.Name);
                var immutable = builder.Build();
                output.CreateSuccess(immutable, toolCall);
                return output;
            }
            catch (Exception ex)
            {
                output.CreateError($"Error deleting components: {ex.Message}");
                return output;
            }
        }

        private async Task<AIReturn> ClearCanvasAsync(AIToolCall toolCall)
        {
            var output = new AIReturn()
            {
                Request = toolCall,
            };

            try
            {
                AIInteractionToolCall toolInfo = toolCall.GetToolCall();
                var args = toolInfo.Arguments ?? new JObject();

                var keepLocked = args["keepLocked"]?.ToObject<bool>() ?? false;

                var clearOptions = new GhJSON.Grasshopper.DeleteOperations.DeleteOptions
                {
                    KeepLocked = keepLocked,
                    Redraw = true
                };

                var clearResult = GhJsonGrasshopper.Clear(clearOptions);

                var toolResult = new JObject
                {
                    ["deletedCount"] = clearResult.DeletedCount,
                    ["success"] = clearResult.Success
                };

                if (clearResult.Skipped.Any())
                {
                    toolResult["skippedCount"] = clearResult.SkippedCount;
                    toolResult["skipped"] = JArray.FromObject(clearResult.Skipped.Select(g => g.ToString()));
                    toolResult["message"] = $"Deleted {clearResult.DeletedCount} components. Kept {clearResult.SkippedCount} locked components.";
                }
                else if (clearResult.DeletedCount > 0)
                {
                    toolResult["message"] = $"Successfully cleared canvas. Deleted {clearResult.DeletedCount} components.";
                }
                else
                {
                    toolResult["message"] = "Canvas is already empty or all components are locked.";
                }

                var builder = AIBodyBuilder.Create();
                builder.AddToolResult(toolResult, toolInfo.Id, toolInfo.Name);
                var immutable = builder.Build();
                output.CreateSuccess(immutable, toolCall);
                return output;
            }
            catch (Exception ex)
            {
                output.CreateError($"Error clearing canvas: {ex.Message}");
                return output;
            }
        }
    }
}
