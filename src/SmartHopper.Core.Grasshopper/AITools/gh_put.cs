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
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SmartHopper.Core.Grasshopper.Utils;
 using SmartHopper.Core.Models.Serialization;
using SmartHopper.Infrastructure.AITools;

namespace SmartHopper.Core.Grasshopper.AITools
{
    /// <summary>
    /// Tool provider for placing Grasshopper components from GhJSON format.
    /// </summary>
    public class gh_put : IAIToolProvider
    {
        /// <summary>
        /// Returns the GH put tool.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: "gh_put",
                description: "Place Grasshopper components on the canvas from GhJSON format",
                category: "Components",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""json"": { ""type"": ""string"", ""description"": ""GhJSON document string"" }
                    },
                    ""required"": [""json""]
                }",
                execute: this.GhPutToolAsync
            );
        }

        private async Task<AIToolCall> GhPutToolAsync(AIToolCall toolCall)
        {
            string analysisMsg = null;
            try
            {
                var json = toolCall.Arguments["json"]?.ToString() ?? string.Empty;

                GHJsonLocal.Validate(json, out analysisMsg);
                var document = GHJsonConverter.DeserializeFromJson(json, fixJson: true);

                if (document?.Components == null || !document.Components.Any())
                {
                    var msg = analysisMsg ?? "JSON must contain a non-empty components array";
                    toolCall.ErrorMessage = msg;
                    return toolCall;
                }

                // Placement & wiring using Put utils
                var placed = Put.PutObjectsOnCanvas(document);
                toolCall.Result = new { components = placed, analysis = analysisMsg };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PutTools] Error in GhPutToolAsync: {ex.Message}");
                var combined = string.IsNullOrEmpty(analysisMsg)
                    ? ex.Message
                    : analysisMsg + "\nException: " + ex.Message;
                toolCall.ErrorMessage = combined;
                return toolCall;
            }
        }
    }
}
