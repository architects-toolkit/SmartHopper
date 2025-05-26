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
    public class gh_tidy_up : IAIToolProvider
    {
        /// <summary>
        /// Returns AI tools for component visibility control.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<AITool> GetTools()
        {
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
    }
}
