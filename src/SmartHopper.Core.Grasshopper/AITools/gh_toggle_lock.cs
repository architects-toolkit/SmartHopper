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
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AICall.Core.Requests;
using SmartHopper.Infrastructure.AICall.Core.Returns;
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.Infrastructure.AITools;

namespace SmartHopper.Core.Grasshopper.AITools
{
    /// <summary>
    /// Tool provider for toggling Grasshopper component preview by GUID.
    /// </summary>
    public class gh_toggle_lock : IAIToolProvider
    {
        /// <summary>
        /// Name of the AI tool provided by this class.
        /// </summary>
        private readonly string toolName = "gh_toggle_lock";
        /// <summary>
        /// Returns AI tools for component visibility control.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: this.toolName,
                description: "Toggle Grasshopper component locked state (enable/disable) by GUID.",
                category: "Components",
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
                execute: this.GhToggleLockAsync);
        }

        private async Task<AIReturn> GhToggleLockAsync(AIToolCall toolCall)
        {
            // Prepare the output
            var output = new AIReturn()
            {
                Request = toolCall,
            };

            try
            {
                // Extract parameters
                AIInteractionToolCall toolInfo = toolCall.GetToolCall();
                var args = toolInfo.Arguments ?? new JObject();
                var guids = args["guids"]?.ToObject<List<string>>() ?? new List<string>();
                var locked = args["locked"]?.ToObject<bool>() ?? false;
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

                var toolResult = new JObject
                {
                    ["updated"] = JArray.FromObject(updated),
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
    }
}
