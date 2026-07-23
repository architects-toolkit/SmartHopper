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
    /// Tool provider for removing Grasshopper components by GUID.
    /// </summary>
    public class gh_remove : IAIToolProvider
    {
        /// <summary>
        /// Name of the AI tool provided by this class.
        /// </summary>
        private readonly string toolName = "gh_remove";

        /// <summary>
        /// Returns AI tools for removing components from the canvas.
        /// </summary>
        /// <returns>Collection of AI tools.</returns>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: this.toolName,
                description: "Remove components from the Grasshopper canvas by their instance GUIDs. The operation records an undo event so the user can reverse it with Ctrl+Z. Use GUIDs from gh_get or similar tools.",
                category: "Components",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""instanceGuids"": {
                            ""type"": ""array"",
                            ""items"": { ""type"": ""string"", ""pattern"": ""^[0-9a-fA-F]{8}-(?:[0-9a-fA-F]{4}-){3}[0-9a-fA-F]{12}$"" },
                            ""description"": ""List of component instance GUIDs to remove.""
                        }
                    },
                    ""required"": [""instanceGuids""]
                }",
                execute: this.GhRemoveToolAsync,
                mutatesCanvas: true,
                tags: new[] { "canvas", "components", "mutating", "delete" },
                outputSchema: @"{ ""type"": ""object"", ""properties"": { ""removedGuids"": { ""type"": ""array"", ""items"": { ""type"": ""string"" } }, ""notFoundGuids"": { ""type"": ""array"", ""items"": { ""type"": ""string"" } } } }",
                annotations: new AIToolAnnotations(destructiveHint: true));
        }

        private Task<AIReturn> GhRemoveToolAsync(AIToolCall toolCall)
        {
            var output = new AIReturn { Request = toolCall };

            try
            {
                toolCall.SkipMetricsValidation = true;

                AIInteractionToolCall toolInfo = toolCall.GetToolCall();
                var args = toolInfo.GetArgumentsOrEmpty();
                var guidArray = args["instanceGuids"] as JArray;

                if (guidArray == null || guidArray.Count == 0)
                {
                    output.CreateError("Missing or empty 'instanceGuids' parameter.");
                    return Task.FromResult(output);
                }

                var requestedGuids = new List<Guid>();
                foreach (var token in guidArray)
                {
                    if (Guid.TryParse(token.ToString(), out var g) && g != Guid.Empty)
                    {
                        requestedGuids.Add(g);
                    }
                }

                if (requestedGuids.Count == 0)
                {
                    output.CreateError("No valid GUIDs provided in 'instanceGuids'.");
                    return Task.FromResult(output);
                }

                var (allowedGuids, protectedGuids) = CanvasProtection.FilterProtectedGuids(requestedGuids);

                if (protectedGuids.Count > 0)
                {
                    output.AddRuntimeMessage(
                        SHRuntimeMessageSeverity.Warning,
                        SHRuntimeMessageOrigin.Tool,
                        CanvasProtection.FormatProtectionMessage(protectedGuids));
                }

                var removedGuids = CanvasAccess.RemoveInstances(allowedGuids);
                var notFoundGuids = allowedGuids
                    .Except(removedGuids)
                    .Select(g => g.ToString())
                    .ToList();

                if (removedGuids.Count == 0 && protectedGuids.Count == 0)
                {
                    output.AddRuntimeMessage(
                        SHRuntimeMessageSeverity.Warning,
                        SHRuntimeMessageOrigin.Tool,
                        "None of the requested components were found on the canvas.");
                }

                var toolResult = new JObject
                {
                    ["removedGuids"] = JArray.FromObject(removedGuids.Select(g => g.ToString())),
                    ["notFoundGuids"] = JArray.FromObject(notFoundGuids),
                    ["protectedGuids"] = JArray.FromObject(protectedGuids.Select(g => g.ToString())),
                };

                var body = AIBodyBuilder.Create()
                    .AddToolResult(toolResult)
                    .Build();

                output.CreateSuccess(body, toolCall);
                return Task.FromResult(output);
            }
            catch (Exception ex)
            {
                output.CreateError($"Error: {ex.Message}");
                return Task.FromResult(output);
            }
        }
    }
}