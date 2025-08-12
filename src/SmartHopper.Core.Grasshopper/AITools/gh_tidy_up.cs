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
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SmartHopper.Core.Grasshopper.Graph;
using SmartHopper.Core.Grasshopper.Utils;
using SmartHopper.Infrastructure.AICall;
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
                description: "Organize selected components into a tidy grid layout. Call `gh_get` first to get the list of GUIDs.",
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
                execute: this.GhTidyUpAsync
            );
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
                // Extract parameters
                AIInteractionToolCall toolInfo = toolCall.Body.PendingToolCallsList().First();
                var guids = toolInfo.Arguments["guids"]?.ToObject<List<string>>() ?? new List<string>();
                var startToken = toolInfo.Arguments["startPoint"];
                var hasStart = startToken != null;
                PointF origin = default;
                if (hasStart)
                {
                    var sx = startToken["x"]?.ToObject<float>() ?? 0f;
                    var sy = startToken["y"]?.ToObject<float>() ?? 0f;
                    origin = new PointF(sx, sy);
                }
                var currentObjs = GHCanvasUtils.GetCurrentObjects();
                var selected = currentObjs.Where(o => guids.Contains(o.InstanceGuid.ToString())).ToList();
                    if (!selected.Any())
                    {
                        Debug.WriteLine("[GhObjTools] GhTidyUpAsync: No matching GUIDs found.");
                    output.CreateError("No matching components found for provided GUIDs.");
                    return output;
                }
                var doc = GHDocumentUtils.GetObjectsDetails(selected);
                var layoutNodes = DependencyGraphUtils.CreateComponentGrid(doc, force: true);

                if (!hasStart)
                {
                    // Anchor grid at original pivot of top-left component
                    var firstNode = layoutNodes.OrderBy(n => n.Pivot.X).ThenBy(n => n.Pivot.Y).First();
                    var origObj = selected.First(o => o.InstanceGuid == firstNode.ComponentId);
                    var origPivot = origObj.Attributes.Pivot;
                    origin = new PointF(origPivot.X - firstNode.Pivot.X, origPivot.Y - firstNode.Pivot.Y);
                }
                var moved = new List<string>();
                foreach (var node in layoutNodes)
                {
                    var guid = node.ComponentId;
                    var rel = node.Pivot;
                    var target = new PointF(origin.X + rel.X, origin.Y + rel.Y);
                    var ok = GHCanvasUtils.MoveInstance(guid, target, relative: false);
                    Debug.WriteLine(ok
                        ? $"[GhObjTools] GhTidyUpAsync: Moved {guid} to ({target.X},{target.Y})"
                        : $"[GhObjTools] GhTidyUpAsync: Failed to move {guid}, not found");
                    if (ok) moved.Add(guid.ToString());
                }
                var toolResult = new JObject
                {
                    ["moved"] = JArray.FromObject(moved)
                };

                var toolBody = new AIBody();
                toolBody.AddInteractionToolResult(toolResult);

                output.CreateSuccess(toolBody);
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
