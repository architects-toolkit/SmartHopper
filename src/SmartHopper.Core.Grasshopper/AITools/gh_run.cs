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
    /// Tool provider for re-running Grasshopper components through the normal
    /// lock transition (disable then re-enable), letting Grasshopper expire and
    /// re-schedule the solution itself.
    /// </summary>
    public class gh_run : IAIToolProvider
    {
        /// <summary>
        /// Name of the AI tool provided by this class.
        /// </summary>
        private readonly string toolName = "gh_run";

        /// <summary>
        /// Returns AI tools for re-running components.
        /// </summary>
        /// <returns>Collection of AI tools.</returns>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AIMutatingTool(
                name: this.toolName,
                description: "Re-run (recompute) components by pulsing their lock state: each target is disabled then re-enabled on the UI thread so Grasshopper schedules a normal solution. Safer than expiring components directly — the call is refused while a solution is in progress. Locked objects are skipped. Provide component GUIDs from gh_get.",
                category: "Components",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""instanceGuids"": {
                            ""type"": ""array"",
                            ""items"": { ""type"": ""string"", ""pattern"": ""^[0-9a-fA-F]{8}-(?:[0-9a-fA-F]{4}-){3}[0-9a-fA-F]{12}$"" },
                            ""description"": ""List of component or parameter instance GUIDs to re-run.""
                        }
                    },
                    ""required"": [""instanceGuids""]
                }",
                execute: this.GhRunAsync,
                tags: new[] { "canvas", "components", "mutating", "execute" },
                outputSchema: @"{ ""type"": ""object"", ""properties"": { ""runGuids"": { ""type"": ""array"", ""items"": { ""type"": ""string"" } }, ""skippedGuids"": { ""type"": ""object"" }, ""protectedGuids"": { ""type"": ""array"", ""items"": { ""type"": ""string"" } } } }",
                annotations: new AIToolAnnotations(destructiveHint: false));
        }

        private async Task<AIReturn> GhRunAsync(AIToolCall toolCall)
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
                    return output;
                }

                var requestedGuids = guidArray
                    .Select(token => Guid.TryParse(token.ToString(), out var g) && g != Guid.Empty ? (Guid?)g : null)
                    .OfType<Guid>()
                    .ToList();

                if (requestedGuids.Count == 0)
                {
                    output.CreateError("No valid GUIDs provided in 'instanceGuids'.");
                    return output;
                }

                var (allowedGuids, protectedGuids) = CanvasProtection.FilterProtectedGuids(requestedGuids);

                if (protectedGuids.Count > 0)
                {
                    output.AddRuntimeMessage(
                        SHRuntimeMessageSeverity.Warning,
                        SHRuntimeMessageOrigin.Tool,
                        CanvasProtection.FormatProtectionMessage(protectedGuids));
                }

                var review = CanvasChangeReviewService.CreateComponentStateSession(
                    toolInfo.Name ?? this.toolName,
                    allowedGuids,
                    "Re-run components");
                var approved = await CanvasChangeReviewService.ReviewAsync(
                    review,
                    toolCall.InvocationContext,
                    toolCall.CancellationToken).ConfigureAwait(false);
                var accepted = approved
                    ? CanvasChangeReviewService.GetAcceptedComponentGuids(review)
                    : new HashSet<Guid>();

                var runGuids = new List<Guid>();
                var skippedGuids = new JObject();

                foreach (var guid in allowedGuids.Where(accepted.Contains))
                {
                    var result = ComponentManipulation.PulseObject(guid);
                    if (result == ComponentManipulation.ObjectManipulationResult.Success)
                    {
                        runGuids.Add(guid);
                    }
                    else
                    {
                        skippedGuids[guid.ToString()] = result switch
                        {
                            ComponentManipulation.ObjectManipulationResult.NotFound => "No canvas object matches this GUID.",
                            ComponentManipulation.ObjectManipulationResult.Locked => "Object is locked; locked objects cannot run.",
                            ComponentManipulation.ObjectManipulationResult.Busy => "A Grasshopper solution is in progress.",
                            _ => "Object does not participate in solutions.",
                        };
                    }
                }

                if (runGuids.Count == 0 && protectedGuids.Count == 0)
                {
                    output.AddRuntimeMessage(
                        SHRuntimeMessageSeverity.Warning,
                        SHRuntimeMessageOrigin.Tool,
                        "None of the requested GUIDs could be re-run (see skippedGuids for reasons).");
                }

                var toolResult = new JObject
                {
                    ["runGuids"] = JArray.FromObject(runGuids.Select(g => g.ToString())),
                    ["skippedGuids"] = skippedGuids,
                    ["protectedGuids"] = JArray.FromObject(protectedGuids.Select(g => g.ToString())),
                };

                var body = AIBodyBuilder.Create()
                    .AddToolResult(toolResult, id: toolInfo.Id, name: toolInfo.Name ?? this.toolName)
                    .Build();

                output.CreateSuccess(body, toolCall);
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
