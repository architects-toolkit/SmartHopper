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
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AICall.Core.Returns;
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.Infrastructure.AITools;
using SmartHopper.ProviderSdk.Diagnostics;

namespace SmartHopper.Core.Grasshopper.AITools
{
    /// <summary>
    /// Tool provider for simulating a momentary button press on Grasshopper button parameters.
    /// </summary>
    public class button_click : IAIToolProvider
    {
        /// <summary>
        /// Name of the AI tool provided by this class.
        /// </summary>
        private readonly string toolName = "button_click";

        /// <summary>
        /// Returns AI tools for clicking button parameters on the canvas.
        /// </summary>
        /// <returns>Collection of AI tools.</returns>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: this.toolName,
                description: "Simulate a momentary click on Grasshopper Buttons (not Boolean Toggles). The button is pressed for 100 ms, then released. Provide the instance GUIDs of the buttons.",
                category: "Components",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""instanceGuids"": {
                            ""type"": ""array"",
                            ""items"": { ""type"": ""string"", ""pattern"": ""^[0-9a-fA-F]{8}-(?:[0-9a-fA-F]{4}-){3}[0-9a-fA-F]{12}$"" },
                            ""description"": ""List of button instance GUIDs to click.""
                        }
                    },
                    ""required"": [""instanceGuids""]
                }",
                execute: this.ButtonClickToolAsync,
                mutatesCanvas: true,
                tags: new[] { "canvas", "components", "mutating", "button" },
                outputSchema: @"{ ""type"": ""object"", ""properties"": { ""clickedGuids"": { ""type"": ""array"", ""items"": { ""type"": ""string"" } }, ""notFoundGuids"": { ""type"": ""array"", ""items"": { ""type"": ""string"" } } } }",
                annotations: new AIToolAnnotations(destructiveHint: false));
        }

        private Task<AIReturn> ButtonClickToolAsync(AIToolCall toolCall)
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

                var requestedGuids = guidArray
                    .Select(token => Guid.TryParse(token.ToString(), out var g) && g != Guid.Empty ? (Guid?)g : null)
                    .OfType<Guid>()
                    .ToList();

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

                var clickedGuids = new List<Guid>();
                var notFoundGuids = new List<string>();

                foreach (var guid in allowedGuids)
                {
                    if (ComponentManipulation.ButtonClick(guid))
                    {
                        clickedGuids.Add(guid);
                    }
                    else
                    {
                        notFoundGuids.Add(guid.ToString());
                    }
                }

                if (clickedGuids.Count == 0 && protectedGuids.Count == 0)
                {
                    output.AddRuntimeMessage(
                        SHRuntimeMessageSeverity.Warning,
                        SHRuntimeMessageOrigin.Tool,
                        "None of the requested GUIDs correspond to a clickable button parameter on the canvas.");
                }

                var toolResult = new JObject
                {
                    ["clickedGuids"] = JArray.FromObject(clickedGuids.Select(g => g.ToString())),
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
