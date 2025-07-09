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
    public class gh_toggle_preview : IAIToolProvider
    {
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
        }

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
    }
}
