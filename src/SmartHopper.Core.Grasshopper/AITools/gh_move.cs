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
using SmartHopper.Core.Grasshopper.Utils;
using SmartHopper.Infrastructure.Interfaces;
using SmartHopper.Infrastructure.Models;

namespace SmartHopper.Core.Grasshopper.AITools
{
    /// <summary>
    /// Tool provider for toggling Grasshopper component preview by GUID.
    /// </summary>
    public class gh_move : IAIToolProvider
    {
        /// <summary>
        /// Returns AI tools for component visibility control.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: "gh_move",
                description: "Move Grasshopper components by GUID with specific targets and optional relative offset.",
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
                execute: this.GhMoveObjAsync
            );
        }

        private async Task<object> GhMoveObjAsync(JObject parameters)
        {
            var targetsObj = parameters["targets"] as JObject;
            if (targetsObj == null)
                return new { success = false, error = "Missing or invalid 'targets' parameter." };
            var relative = parameters["relative"]?.ToObject<bool>() ?? false;
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
            var movedList = GHCanvasUtils.MoveInstance(dict, relative);
            return new { success = movedList.Any(), updated = movedList.Select(g => g.ToString()).ToList() };
        }
    }
}
