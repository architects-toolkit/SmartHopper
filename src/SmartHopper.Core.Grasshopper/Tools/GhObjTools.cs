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
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SmartHopper.Config.Interfaces;
using SmartHopper.Config.Models;
using SmartHopper.Core.Grasshopper.Utils;
using SmartHopper.Core.Models.Document;
using System.Linq;
using SmartHopper.Core.Grasshopper.Graph;
using Grasshopper.Kernel;

namespace SmartHopper.Core.Grasshopper.Tools
{
    /// <summary>
    /// Tool provider for toggling Grasshopper component preview by GUID.
    /// </summary>
    public class GhObjTools : IAIToolProvider
    {
        #region ToolRegistration

        /// <summary>
        /// Returns AI tools for component visibility control.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: "gh_toggle_preview",
                description: "Toggle Grasshopper component preview on or off by GUID.",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""guids"": {
                            ""type"": ""array"",
                            ""items"": { ""type"": ""string"" },
                            ""description"": ""List of component GUIDs to toggle preview.""
                        },
                        ""previewOn"": {
                            ""type"": ""boolean"",
                            ""description"": ""True to enable preview, false to disable preview.""
                        }
                    },
                    ""required"": [ ""guids"", ""previewOn"" ]
                }",
                execute: this.GhTogglePreviewAsync
            );

            // New tool to toggle component locked state
            yield return new AITool(
                name: "gh_toggle_lock",
                description: "Toggle Grasshopper component locked state (enable/disable) by GUID.",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""guids"": {
                            ""type"": ""array"",
                            ""items"": { ""type"": ""string"" },
                            ""description"": ""List of component GUIDs to toggle lock state.""
                        },
                        ""locked"": {
                            ""type"": ""boolean"",
                            ""description"": ""True to lock (disable), false to unlock (enable) the component.""
                        }
                    },
                    ""required"": [ ""guids"", ""locked"" ]
                }",
                execute: this.GhToggleLockAsync
            );

            // New tool to move component pivot position
            yield return new AITool(
                name: "gh_move_obj",
                description: "Move Grasshopper components by GUID with specific targets and optional relative offset.",
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
                }" ,
                execute: this.GhMoveObjAsync
            );

            // New tool to tidy up selected components into a grid layout
            yield return new AITool(
                name: "gh_tidy_up",
                description: "Organize selected components into a tidy grid layout. Call `gh_get` first to get the list of GUIDs.",
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
        #endregion

        #region Preview
        private async Task<object> GhTogglePreviewAsync(JObject parameters)
        {
            var guids = parameters["guids"]?.ToObject<List<string>>() ?? new List<string>();
            var previewOn = parameters["previewOn"]?.ToObject<bool>() ?? false;
            Debug.WriteLine($"[GhObjTools] GhTogglePreviewAsync: previewOn={previewOn}, guids count={guids.Count}");
            var updated = new List<string>();

            foreach (var s in guids)
            {
                Debug.WriteLine($"[GhObjTools] Processing GUID string: {s}");
                if (Guid.TryParse(s, out var guid))
                {
                    Debug.WriteLine($"[GhObjTools] Parsed GUID: {guid}");
                    GHComponentUtils.SetComponentPreview(guid, previewOn);
                    Debug.WriteLine($"[GhObjTools] Set preview to {previewOn} for GUID: {guid}");
                    updated.Add(guid.ToString());
                }
                else
                {
                    Debug.WriteLine($"[GhObjTools] Invalid GUID: {s}");
                }
            }

            return new { success = true, updated };
        }
        #endregion

        #region Lock
        private async Task<object> GhToggleLockAsync(JObject parameters)
        {
            var guids = parameters["guids"]?.ToObject<List<string>>() ?? new List<string>();
            var locked = parameters["locked"]?.ToObject<bool>() ?? false;
            Debug.WriteLine($"[GhObjTools] GhToggleLockAsync: locked={locked}, guids count={guids.Count}");
            var updated = new List<string>();

            foreach (var s in guids)
            {
                Debug.WriteLine($"[GhObjTools] Processing GUID string: {s}");
                if (Guid.TryParse(s, out var guid))
                {
                    Debug.WriteLine($"[GhObjTools] Parsed GUID: {guid}");
                    GHComponentUtils.SetComponentLock(guid, locked);
                    Debug.WriteLine($"[GhObjTools] Set lock to {locked} for GUID: {guid}");
                    updated.Add(guid.ToString());
                }
                else
                {
                    Debug.WriteLine($"[GhObjTools] Invalid GUID: {s}");
                }
            }

            return new { success = true, updated };
        }
        #endregion

        #region MoveInstance
        private async Task<object> GhMoveObjAsync(JObject parameters)
        {
            var targetsObj = parameters["targets"] as JObject;
            if (targetsObj == null)
                return new { success = false, error = "Missing or invalid 'targets' parameter." };
            var relative = parameters["relative"]?.ToObject<bool>() ?? false;
            var dict = new Dictionary<Guid, PointF>();
            foreach (var prop in targetsObj.Properties())
                if (Guid.TryParse(prop.Name, out var g))
                    dict[g] = new PointF(
                        prop.Value["x"]?.ToObject<float>() ?? 0f,
                        prop.Value["y"]?.ToObject<float>() ?? 0f);
            var movedList = GHCanvasUtils.MoveInstance(dict, relative);
            return new { success = movedList.Any(), updated = movedList.Select(g => g.ToString()).ToList() };
        }
        #endregion

        #region TidyUp
        /// <summary>
        /// Reorganize selected components into a tidy grid layout by GUID list.
        /// </summary>
        private async Task<object> GhTidyUpAsync(JObject parameters)
        {
            var guids = parameters["guids"]?.ToObject<List<string>>() ?? new List<string>();
            var startToken = parameters["startPoint"];
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
                return new { success = false, error = "No matching components found for provided GUIDs." };
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
            return new { success = true, moved };
        }
        #endregion

    }
}
