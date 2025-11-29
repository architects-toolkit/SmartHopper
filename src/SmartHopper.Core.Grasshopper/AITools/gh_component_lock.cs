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
using SmartHopper.Core.Grasshopper.Utils.Canvas;
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
    public class gh_component_toggle_lock : IAIToolProvider
    {
        /// <summary>
        /// Name of the AI tool provided by this class.
        /// </summary>
        private readonly string toolName = "gh_component_toggle_lock";
        /// <summary>
        /// Returns AI tools for component visibility control.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: this.toolName,
                description: "Lock (disable) or unlock (enable) components. Locked components don't execute and show as grayed out. Use this to temporarily disable parts of a definition without deleting them. Requires component GUIDs from gh_get.",
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

            // Specialized wrapper: gh_lock_selected
            yield return new AITool(
                name: "gh_component_lock_selected",
                description: "Lock (disable) currently selected components. Quick way to disable selected items without needing to specify GUIDs manually. Locked components don't execute and show as grayed out.",
                category: "Components",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {}
                }",
                execute: (toolCall) => this.GhToggleLockSelectedAsync(toolCall, locked: true));

            // Specialized wrapper: gh_unlock_selected
            yield return new AITool(
                name: "gh_component_unlock_selected",
                description: "Unlock (enable) currently selected components. Quick way to enable selected items without needing to specify GUIDs manually. Unlocked components will execute normally.",
                category: "Components",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {}
                }",
                execute: (toolCall) => this.GhToggleLockSelectedAsync(toolCall, locked: false));
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
                // Local tool: do not enforce provider/model/finish_reason metrics
                toolCall.SkipMetricsValidation = true;

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
                        ComponentManipulation.SetComponentLock(guid, locked);
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

        private async Task<AIReturn> GhToggleLockSelectedAsync(AIToolCall toolCall, bool locked)
        {
            // Get selected component GUIDs
            var selectedGuids = CanvasAccess.GetCurrentObjects()
                .Where(o => o.Attributes.Selected)
                .Select(o => o.InstanceGuid.ToString())
                .ToList();

            if (!selectedGuids.Any())
            {
                var output = new AIReturn() { Request = toolCall };
                output.CreateError("No components are currently selected.");
                return output;
            }

            // Create a modified tool call with the selected GUIDs
            var toolInfo = toolCall.GetToolCall();
            var modifiedArgs = new JObject
            {
                ["guids"] = JArray.FromObject(selectedGuids),
                ["locked"] = locked
            };

            // Create a new tool call with modified arguments
            var modifiedToolCall = new AIToolCall
            {
                Provider = toolCall.Provider,
                Model = toolCall.Model,
                Body = AIBodyBuilder.Create()
                    .AddToolCall(
                        id: toolInfo.Id,
                        name: toolInfo.Name ?? (locked ? "gh_lock_selected" : "gh_unlock_selected"),
                        args: modifiedArgs)
                    .Build()
            };

            // Delegate to the general method
            return await this.GhToggleLockAsync(modifiedToolCall);
        }
    }
}
