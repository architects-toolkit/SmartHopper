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
        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: "ghtogglepreview",
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
                execute: this.ExecuteTogglePreviewAsync
            );

            // New tool to toggle component locked state
            yield return new AITool(
                name: "ghtogglelock",
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
                execute: this.ExecuteToggleLockAsync
            );

            // New tool to move component pivot position
            yield return new AITool(
                name: "ghmoveobj",
                description: "Move Grasshopper component pivot by GUID, with absolute or relative position.",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""guids"": {
                            ""type"": ""array"",
                            ""items"": { ""type"": ""string"" },
                            ""description"": ""List of component GUIDs to move.""
                        },
                        ""position"": {
                            ""type"": ""object"",
                            ""properties"": {
                                ""x"": { ""type"": ""number"", ""description"": ""X coordinate for pivot."" },
                                ""y"": { ""type"": ""number"", ""description"": ""Y coordinate for pivot."" }
                            },
                            ""required"": [ ""x"", ""y"" ],
                            ""description"": ""Pivot position.""
                        },
                        ""relative"": {
                            ""type"": ""boolean"",
                            ""description"": ""True for relative offset; false for absolute.""
                        },
                        ""live"": {
                            ""type"": ""boolean"",
                            ""description"": ""True to redraw canvas after moving.""
                        }
                    },
                    ""required"": [ ""guids"", ""position"" ]
                }",
                execute: this.ExecuteMoveObjAsync
            );
        }
        #endregion

        #region Preview
        private async Task<object> ExecuteTogglePreviewAsync(JObject parameters)
        {
            var guids = parameters["guids"]?.ToObject<List<string>>() ?? new List<string>();
            var previewOn = parameters["previewOn"]?.ToObject<bool>() ?? false;
            Debug.WriteLine($"[GhObjTools] ExecuteTogglePreviewAsync: previewOn={previewOn}, guids count={guids.Count}");
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
        private async Task<object> ExecuteToggleLockAsync(JObject parameters)
        {
            var guids = parameters["guids"]?.ToObject<List<string>>() ?? new List<string>();
            var locked = parameters["locked"]?.ToObject<bool>() ?? false;
            Debug.WriteLine($"[GhObjTools] ExecuteToggleLockAsync: locked={locked}, guids count={guids.Count}");
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
        private async Task<object> ExecuteMoveObjAsync(JObject parameters)
        {
            var guids = parameters["guids"]?.ToObject<List<string>>() ?? new List<string>();
            var posObj = parameters["position"];
            var x = posObj?["x"]?.ToObject<float>() ?? 0f;
            var y = posObj?["y"]?.ToObject<float>() ?? 0f;
            var relative = parameters["relative"]?.ToObject<bool>() ?? false;
            var live = parameters["live"]?.ToObject<bool>() ?? false;
            Debug.WriteLine($"[GhObjTools] ExecuteMoveObjAsync: relative={relative}, live={live}, count={guids.Count}, position=({x},{y})");
            var updated = new List<string>();
            foreach (var s in guids)
            {
                Debug.WriteLine($"[GhObjTools] Processing GUID string: {s}");
                if (Guid.TryParse(s, out var guid))
                {
                    var moved = GHCanvasUtils.MoveInstance(guid, new PointF(x, y), relative, live);
                    Debug.WriteLine(moved
                        ? $"[GhObjTools] Moved GUID: {guid} to ({x},{y}) relative={relative}"
                        : $"[GhObjTools] Instance not found for GUID: {guid}");
                    if (moved)
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

    }
}
