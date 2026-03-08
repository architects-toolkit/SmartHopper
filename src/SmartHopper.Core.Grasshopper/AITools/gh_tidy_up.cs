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
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using GhJSON.Core;
using GhJSON.Core.DependencyGraph;
using GhJSON.Grasshopper;
using GhJSON.Grasshopper.LayoutRefinements;
using GhJSON.Grasshopper.Serialization;
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
    public class gh_tidy_up : IAIToolProvider
    {
        /// <summary>
        /// Name of the AI tool provided by this class.
        /// </summary>
        private readonly string toolName = "gh_tidy_up";
        /// <summary>
        /// Returns AI tools for component visibility control.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: this.toolName,
                description: "Automatically arrange components into a clean grid layout respecting data flow direction. Organizes components left-to-right based on their connections. Use this to clean up messy definitions. Requires component GUIDs from gh_get.",
                category: "Components",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""guids"": {
                            ""type"": ""array"",
                            ""items"": { ""type"": ""string"" },
                            ""description"": ""List of component GUIDs to include in the tidy-up.""
                        },
                        ""startPoint"": {
                            ""type"": ""object"",
                            ""properties"": {
                                ""x"": { ""type"": ""number"" },
                                ""y"": { ""type"": ""number"" }
                            },
                            ""description"": ""Optional absolute start point for the top-left of the grid. Overrides selection's top-left if provided.""
                        }
                    },
                    ""required"": [ ""guids"" ]
                }",
                execute: this.GhTidyUpAsync);

            // Specialized wrapper: gh_tidy_up_selected
            yield return new AITool(
                name: "gh_tidy_up_selected",
                description: "Organize currently selected components into a tidy grid layout. Quick way to clean up selected items without needing to specify GUIDs manually. Arranges components left-to-right based on connections.",
                category: "Components",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""startPoint"": {
                            ""type"": ""object"",
                            ""properties"": {
                                ""x"": { ""type"": ""number"" },
                                ""y"": { ""type"": ""number"" }
                            },
                            ""description"": ""Optional absolute start point for the top-left of the grid. Overrides selection's top-left if provided.""
                        }
                    }
                }",
                execute: this.GhTidyUpSelectedAsync);
        }

        /// <summary>
        /// Reorganize selected components into a tidy grid layout by GUID list.
        /// </summary>
        private async Task<AIReturn> GhTidyUpAsync(AIToolCall toolCall)
        {
            // Prepare the output
            var output = new AIReturn()
            {
                Request = toolCall,
            };

            try
            {
                // Local tool: skip metrics validation (provider/model/finish_reason not required)
                toolCall.SkipMetricsValidation = true;

                // Extract parameters
                AIInteractionToolCall toolInfo = toolCall.GetToolCall();
                var args = toolInfo.Arguments ?? new JObject();
                var guids = args["guids"]?.ToObject<List<string>>() ?? new List<string>();
                var startToken = args["startPoint"];
                var hasStart = startToken != null;
                PointF origin = default;
                if (hasStart)
                {
                    var sx = startToken["x"]?.ToObject<float>() ?? 0f;
                    var sy = startToken["y"]?.ToObject<float>() ?? 0f;
                    origin = new PointF(sx, sy);
                }

                var currentObjs = CanvasAccess.GetCurrentObjects();
                var selected = currentObjs.Where(o => guids.Contains(o.InstanceGuid.ToString())).ToList();
                if (!selected.Any())
                {
                    Debug.WriteLine("[GhObjTools] GhTidyUpAsync: No matching GUIDs found.");
                    output.CreateError("No matching components found for provided GUIDs.");
                    return output;
                }

                var doc = GhJsonGrasshopper.Serialize(selected, SerializationOptions.Default);

                // Calculate base layout using GhJSON
                var layoutOptions = new LayoutOptions
                {
                    SpacingX = 50f,
                    SpacingY = 80f,
                    IslandSpacingY = 80f
                };
                var layoutResult = GhJson.CalculateLayout(doc, layoutOptions);

                // Apply Grasshopper-aware refinements
                var refinementOptions = new LayoutRefinementOptions
                {
                    SpacingX = 50f,
                    SpacingY = 80f,
                    ApplyBoundsAwareSpacing = true,
                    AlignParamsToInputPorts = true,
                    AlignOneToOneConnections = true,
                    MinimizeConnectionLengths = true,
                    AvoidCollisions = true
                };
                var refinedPositions = LayoutRefinementEngine.ApplyRefinements(
                    layoutResult,
                    doc,
                    refinementOptions);

                if (!hasStart)
                {
                    // Anchor grid at original pivot of top-left component
                    var firstPos = refinedPositions.OrderBy(kvp => kvp.Value.X).ThenBy(kvp => kvp.Value.Y).First();
                    var origObj = selected.First(o => o.InstanceGuid == firstPos.Key);
                    var origPivot = origObj.Attributes.Pivot;
                    origin = new PointF(origPivot.X - firstPos.Value.X, origPivot.Y - firstPos.Value.Y);
                }

                var moved = new List<string>();
                foreach (var kvp in refinedPositions)
                {
                    var guid = kvp.Key;
                    var rel = kvp.Value;
                    var target = new PointF(origin.X + rel.X, origin.Y + rel.Y);
                    var ok = CanvasAccess.MoveInstance(guid, target, relative: false);
                    Debug.WriteLine(ok
                        ? $"[GhObjTools] GhTidyUpAsync: Moved {guid} to ({target.X},{target.Y})"
                        : $"[GhObjTools] GhTidyUpAsync: Failed to move {guid}, not found");
                    if (ok) moved.Add(guid.ToString());
                }

                var toolResult = new JObject
                {
                    ["moved"] = JArray.FromObject(moved),
                };
                var immutableBody = AIBodyBuilder.Create()
                    .AddToolResult(toolResult, id: toolInfo.Id, name: toolInfo.Name ?? this.toolName)
                    .Build();

                output.CreateSuccess(immutableBody, toolCall);
                return output;
            }
            catch (Exception ex)
            {
                output.CreateError($"Error: {ex.Message}");
                return output;
            }
        }

        private async Task<AIReturn> GhTidyUpSelectedAsync(AIToolCall toolCall)
        {
            // Get selected component GUIDs
            var selectedGuids = CanvasAccess.GetCurrentObjects()
                .Where(o => o.Attributes.Selected)
                .Select(o => o.InstanceGuid.ToString())
                .ToList();

            if (!selectedGuids.Any())
            {
                Debug.WriteLine("[GhObjTools] GhTidyUpSelectedAsync: No components selected.");
                var output = new AIReturn() { Request = toolCall };
                output.CreateError("No components are currently selected.");
                return output;
            }

            // Create a modified tool call with the selected GUIDs
            var toolInfo = toolCall.GetToolCall();
            var args = toolInfo.Arguments ?? new JObject();
            var modifiedArgs = new JObject
            {
                ["guids"] = JArray.FromObject(selectedGuids)
            };

            // Preserve optional startPoint parameter if provided
            if (args["startPoint"] != null)
                modifiedArgs["startPoint"] = args["startPoint"];

            // Create a new tool call with modified arguments
            var modifiedToolCall = new AIToolCall
            {
                Provider = toolCall.Provider,
                Model = toolCall.Model,
                Body = AIBodyBuilder.Create()
                    .AddToolCall(
                        id: toolInfo.Id,
                        name: toolInfo.Name ?? this.toolName,
                        args: modifiedArgs)
                    .Build()
            };

            modifiedToolCall.SkipMetricsValidation = true;

            // Delegate to the general method
            return await this.GhTidyUpAsync(modifiedToolCall);
        }
    }
}
