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
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SmartHopper.Core.Grasshopper.Utils.Canvas;
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.Infrastructure.AITools;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AICall.Core.Returns;
using SmartHopper.ProviderSdk.Diagnostics;

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
                execute: this.GhToggleLockAsync,
                mutatesCanvas: true,
                tags: new[] { "canvas", "components", "mutating", "state" },
                outputSchema: @"{ ""type"": ""object"", ""properties"": { ""success"": { ""type"": ""boolean"" }, ""affectedGuids"": { ""type"": ""array"" } } }",
                annotations: new AIToolAnnotations(destructiveHint: false));

            // Specialized wrapper: gh_lock_selected
            yield return new AITool(
                name: "gh_component_lock_selected",
                description: "Lock (disable) currently selected components. Quick way to disable selected items without needing to specify GUIDs manually. Locked components don't execute and show as grayed out. IMPORTANT: This tool will not affect the enabled SmartHopper MCP Server component or any component directly wired to it.",
                category: "Components",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {}
                }",
                execute: (toolCall) => this.GhToggleLockSelectedAsync(toolCall, locked: true),
                mutatesCanvas: true,
                tags: new[] { "canvas", "components", "mutating", "state" },
                outputSchema: @"{ ""type"": ""object"", ""properties"": { ""success"": { ""type"": ""boolean"" }, ""affectedGuids"": { ""type"": ""array"" } } }",
                annotations: new AIToolAnnotations(destructiveHint: false));

            // Specialized wrapper: gh_unlock_selected
            yield return new AITool(
                name: "gh_component_unlock_selected",
                description: "Unlock (enable) currently selected components. Quick way to enable selected items without needing to specify GUIDs manually. Unlocked components will execute normally. IMPORTANT: This tool will not affect the enabled SmartHopper MCP Server component or any component directly wired to it.",
                category: "Components",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {}
                }",
                execute: (toolCall) => this.GhToggleLockSelectedAsync(toolCall, locked: false),
                mutatesCanvas: true,
                tags: new[] { "canvas", "components", "mutating", "state" },
                outputSchema: @"{ ""type"": ""object"", ""properties"": { ""success"": { ""type"": ""boolean"" }, ""affectedGuids"": { ""type"": ""array"" } } }",
                annotations: new AIToolAnnotations(destructiveHint: false));
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
                var args = toolInfo.GetArgumentsOrEmpty();
                var guids = args["guids"]?.ToObject<List<string>>() ?? new List<string>();
                var locked = args["locked"]?.ToObject<bool>() ?? false;
                Debug.WriteLine($"[GhObjTools] GhToggleLockAsync: locked={locked}, guids count={guids.Count}");

                var requestedGuids = new List<Guid>();
                foreach (var s in guids)
                {
                    Debug.WriteLine($"[GhObjTools] Processing GUID string: {s}");
                    if (Guid.TryParse(s, out var guid))
                    {
                        requestedGuids.Add(guid);
                    }
                    else
                    {
                        Debug.WriteLine($"[GhObjTools] Invalid GUID: {s}");
                    }
                }

                var (allowedGuids, protectedGuids) = CanvasProtection.FilterProtectedGuids(requestedGuids);

                if (protectedGuids.Count > 0)
                {
                    output.AddRuntimeMessage(
                        SHRuntimeMessageSeverity.Warning,
                        SHRuntimeMessageOrigin.Tool,
                        CanvasProtection.FormatProtectionMessage(protectedGuids));
                }

                var updated = new List<string>();
                foreach (var guid in allowedGuids)
                {
                    Debug.WriteLine($"[GhObjTools] Parsed GUID: {guid}");
                    ComponentManipulation.SetComponentLock(guid, locked);
                    Debug.WriteLine($"[GhObjTools] Set lock to {locked} for GUID: {guid}");
                    updated.Add(guid.ToString());
                }

                var toolResult = new JObject
                {
                    ["updated"] = JArray.FromObject(updated),
                    ["protectedGuids"] = JArray.FromObject(protectedGuids.Select(g => g.ToString())),
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
            return await this.GhToggleLockAsync(modifiedToolCall).ConfigureAwait(false);
        }
    }
}