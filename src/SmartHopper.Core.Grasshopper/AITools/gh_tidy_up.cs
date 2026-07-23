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
using GhJSON.Core.DependencyGraph;
using GhJSON.Grasshopper;
using GhJSON.Grasshopper.LayoutRefinements;
using GhJSON.Grasshopper.Serialization;
using Grasshopper;
using Newtonsoft.Json.Linq;
using SmartHopper.Core.Grasshopper.Utils.Canvas;
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.Infrastructure.AITools;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AICall.Core.Returns;

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
                        },
                        ""viewportOnly"": {
                            ""type"": ""boolean"",
                            ""default"": false,
                            ""description"": ""When true, only includes components currently visible in the canvas viewport. Ignores off-screen components even if their GUIDs are provided.""
                        }
                    },
                    ""required"": [ ""guids"" ]
                }",
                execute: this.GhTidyUpAsync,
                mutatesCanvas: true,
                tags: new[] { "canvas", "components", "mutating", "layout" },
                outputSchema: @"{ ""type"": ""object"", ""properties"": { ""success"": { ""type"": ""boolean"" }, ""moved"": { ""type"": ""array"", ""items"": { ""type"": ""string"" }, ""description"": ""Instance GUIDs of components that were moved."" }, ""affectedGuids"": { ""type"": ""array"", ""items"": { ""type"": ""string"" }, ""description"": ""Instance GUIDs of components that were moved (alias for moved)."" } } }",
                annotations: new AIToolAnnotations(destructiveHint: false));

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
                        },
                        ""viewportOnly"": {
                            ""type"": ""boolean"",
                            ""default"": false,
                            ""description"": ""When true, only includes selected components currently visible in the canvas viewport.""
                        }
                    }
                }",
                execute: this.GhTidyUpSelectedAsync,
                mutatesCanvas: true,
                tags: new[] { "canvas", "components", "mutating", "layout" },
                outputSchema: @"{ ""type"": ""object"", ""properties"": { ""success"": { ""type"": ""boolean"" }, ""moved"": { ""type"": ""array"", ""items"": { ""type"": ""string"" }, ""description"": ""Instance GUIDs of components that were moved."" }, ""affectedGuids"": { ""type"": ""array"", ""items"": { ""type"": ""string"" }, ""description"": ""Instance GUIDs of components that were moved (alias for moved)."" } } }",
                annotations: new AIToolAnnotations(destructiveHint: false));
        }

        /// <summary>
        /// Reorganize selected components into a tidy grid layout by GUID list.
        /// </summary>
        private Task<AIReturn> GhTidyUpAsync(AIToolCall toolCall)
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
                var args = toolInfo.GetArgumentsOrEmpty();
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
                var viewportOnly = args["viewportOnly"]?.ToObject<bool>() ?? false;

                if (viewportOnly)
                {
                    var canvas = Instances.ActiveCanvas;
                    if (canvas?.Viewport != null)
                    {
                        var visibleRegion = canvas.Viewport.VisibleRegion;
                        selected = selected.Where(o =>
                        {
                            var bounds = o.Attributes?.Bounds;
                            return bounds.HasValue && visibleRegion.IntersectsWith(bounds.Value);
                        }).ToList();
                    }
                }
                
                if (!selected.Any())
                {
                    Debug.WriteLine("[GhObjTools] GhTidyUpAsync: No matching components found after filtering.");
                    output.CreateError("No matching components found.");
                    return Task.FromResult(output);
                }

                var doc = GhJsonGrasshopper.Serialize(selected, SerializationOptions.Default);

                // Core dependency-graph layout (longest-path layering, dummy-chain routing,
                // crossing minimization, bounds-aware coordinates) followed by Grasshopper-aware
                // refinements that use real component bounds and input-port positions for
                // alignment and collision avoidance. This is the same pipeline used by gh_put,
                // so placement and tidy-up stay consistent.
                const float spacingX = 200f;
                const float spacingY = 100f;
                const float islandSpacingY = 150f;

                var layoutResult = GhJSON.Core.GhJson.CalculateLayout(doc, new LayoutOptions
                {
                    SpacingX = spacingX,
                    SpacingY = spacingY,
                    IslandSpacingY = islandSpacingY,
                });

                var positions = LayoutRefinementEngine.ApplyRefinements(
                    layoutResult,
                    doc,
                    new LayoutRefinementOptions
                    {
                        SpacingX = spacingX,
                        SpacingY = spacingY,
                        ApplyBoundsAwareSpacing = true,
                        AlignParamsToInputPorts = true,
                        AlignOneToOneConnections = true,
                        MinimizeConnectionLengths = true,
                        AvoidCollisions = true,
                    });

                if (positions.Count == 0)
                {
                    Debug.WriteLine("[GhObjTools] GhTidyUpAsync: Layout produced no positions.");
                    output.CreateError("Layout produced no positions for the selected components.");
                    return Task.FromResult(output);
                }

                if (!hasStart)
                {
                    // Anchor the laid-out cluster at the original pivot of the top-left component
                    // so the tidied result stays roughly where the user had it.
                    var firstKvp = positions.OrderBy(p => p.Value.X).ThenBy(p => p.Value.Y).First();
                    var origObj = selected.First(o => o.InstanceGuid == firstKvp.Key);
                    var origPivot = origObj.Attributes.Pivot;
                    origin = new PointF(origPivot.X - firstKvp.Value.X, origPivot.Y - firstKvp.Value.Y);
                }

                var moved = new List<string>();
                foreach (var kvp in positions)
                {
                    var guid = kvp.Key;
                    var target = new PointF(origin.X + kvp.Value.X, origin.Y + kvp.Value.Y);
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
                return Task.FromResult(output);
            }
            catch (Exception ex)
            {
                output.CreateError($"Error: {ex.Message}");
                return Task.FromResult(output);
            }
        }

        /// <summary>
        /// Reorganizes the currently selected components into a tidy grid layout.
        /// Collects selected objects first, applies optional viewport filtering, then delegates to GhTidyUpAsync.
        /// </summary>
        private async Task<AIReturn> GhTidyUpSelectedAsync(AIToolCall toolCall)
        {
            // Parse arguments first so viewportOnly is available before filtering
            var toolInfo = toolCall.GetToolCall();
            var args = toolInfo.GetArgumentsOrEmpty();
            var viewportOnly = args["viewportOnly"]?.ToObject<bool>() ?? false;

            // Collect selected document objects (not just GUIDs) so the viewport pivot check can be applied
            var selectedObjects = CanvasAccess.GetCurrentObjects()
                .Where(o => o.Attributes.Selected)
                .ToList();

            // Restrict to viewport-visible components if requested
            if (viewportOnly)
            {
                var canvas = Instances.ActiveCanvas;
                if (canvas?.Viewport != null)
                {
                    var visibleRegion = canvas.Viewport.VisibleRegion;
                    selectedObjects = selectedObjects
                        .Where(o =>
                        {
                            var bounds = o.Attributes?.Bounds;
                            return bounds.HasValue && visibleRegion.IntersectsWith(bounds.Value);
                        })
                        .ToList();
                }
            }

            if (!selectedObjects.Any())
            {
                Debug.WriteLine("[GhObjTools] GhTidyUpSelectedAsync: No selected components visible in viewport.");
                var earlyOutput = new AIReturn() { Request = toolCall };
                earlyOutput.CreateError("No selected components are visible in the current viewport.");
                return earlyOutput;
            }

            // Extract GUIDs after filtering
            var selectedGuids = selectedObjects.Select(o => o.InstanceGuid.ToString()).ToList();

            // Build forwarding args for the general method
            var modifiedArgs = new JObject
            {
                ["guids"] = JArray.FromObject(selectedGuids),
                ["viewportOnly"] = viewportOnly,
            };

            // Preserve optional startPoint parameter if provided
            if (args["startPoint"] != null)
            {
                modifiedArgs["startPoint"] = args["startPoint"];
            }

            var modifiedToolCall = new AIToolCall
            {
                Provider = toolCall.Provider,
                Model = toolCall.Model,
                Body = AIBodyBuilder.Create()
                    .AddToolCall(
                        id: toolInfo.Id,
                        name: toolInfo.Name ?? this.toolName,
                        args: modifiedArgs)
                    .Build(),
            };

            modifiedToolCall.SkipMetricsValidation = true;

            // Delegate to the general method
            return await this.GhTidyUpAsync(modifiedToolCall).ConfigureAwait(false);
        }
    }
}