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
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Grasshopper;
using Grasshopper.Kernel.Special;
using SmartHopper.Core.Grasshopper.Utils;
using SmartHopper.Core.Grasshopper.Converters;
using SmartHopper.Infrastructure.Interfaces;
using SmartHopper.Infrastructure.Models;

namespace SmartHopper.Core.Grasshopper.AITools
{
    /// <summary>
    /// Tool provider for grouping Grasshopper components by GUID.
    /// </summary>
    public class gh_group : IAIToolProvider
    {
        /// <summary>
        /// Returns the gh_group AI tool.
        /// </summary>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: "gh_group",
                description: "Group Grasshopper components by GUID into a single Grasshopper group.",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""guids"": {
                            ""type"": ""array"",
                            ""items"": { ""type"": ""string"" },
                            ""description"": ""List of component GUIDs to include in the group.""
                        },
                        ""groupName"": {
                            ""type"": ""string"",
                            ""description"": ""Optional name for the group.""
                        },
                        ""color"": {
                            ""type"": ""string"",
                            ""description"": ""Optional group color as ARGB 'A,R,G,B', RGB 'R,G,B', HTML hex '#RRGGBB', or known color name (e.g., 'Red'). Alpha defaults to 255 (100% opacity).""
                        }
                    },
                    ""required"": [""guids""]
                }",
                execute: this.GhGroupAsync
            );
        }

        private Task<object> GhGroupAsync(JObject parameters)
        {
            var guidStrings = parameters["guids"]?.ToObject<List<string>>() ?? new List<string>();
            var groupName = parameters["groupName"]?.ToString();
            var colorStr = parameters["color"]?.ToString();
            var validGuids = new List<Guid>();

            foreach (var s in guidStrings)
            {
                if (Guid.TryParse(s, out var g))
                    validGuids.Add(g);
            }

            if (!validGuids.Any())
                return Task.FromResult<object>(new { success = false, error = "No valid GUIDs provided for grouping." });

            var group = new GH_Group();

            Rhino.RhinoApp.InvokeOnUiThread(() =>
            {
                var doc = GHCanvasUtils.GetCurrentCanvas();

                if (!string.IsNullOrEmpty(groupName))
                {
                    group.NickName = groupName;
                    group.Colour = Color.FromArgb(255, 100, 100, 100);
                }
                if (!string.IsNullOrEmpty(colorStr))
                {
                    try
                    {
                        var argbColor = StringConverter.StringToColor(colorStr);
                        group.Colour = argbColor;
                    }
                    catch
                    {
                        // Invalid color string, ignoring
                    }
                }

                foreach (var guid in validGuids)
                {
                    var obj = GHCanvasUtils.FindInstance(guid);
                    if (obj != null)
                        group.AddObject(guid);
                }

                doc.AddObject(group, false);
                Instances.RedrawCanvas();
            });

            return Task.FromResult<object>(new
            {
                success = true,
                group = group.InstanceGuid.ToString(),
                grouped = validGuids.Select(g => g.ToString()).ToList()
            });
        }
    }
}
