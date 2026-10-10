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
    /// Tool provider for setting the value of value-bearing Grasshopper objects
    /// (panels, scribbles, boolean toggles, number sliders, value lists and
    /// persistent parameters, including component input parameters) without
    /// recreating them.
    /// </summary>
    public class gh_set_value : IAIToolProvider
    {
        /// <summary>
        /// Name of the AI tool provided by this class.
        /// </summary>
        private readonly string toolName = "gh_set_value";

        /// <summary>
        /// Returns AI tools for setting values on canvas objects.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AIMutatingTool(
                name: this.toolName,
                description: "Set the value of an existing canvas object in place: panel or scribble text, a boolean toggle, a number slider (clamped to its range), a value list selection (by index, name or item value), or a standalone persistent parameter (e.g. Number, Text, Integer, Boolean). When 'param' is supplied and the target is a component, the matching input parameter's persistent default is set instead (name, nickname, or zero-based index). The value is applied in place and the downstream solution is re-scheduled. Requires GUIDs from gh_get.",
                category: "Components",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""instanceGuid"": {
                            ""type"": ""string"",
                            ""description"": ""GUID of the target object (from gh_get).""
                        },
                        ""value"": {
                            ""type"": [""string"", ""number"", ""integer"", ""boolean""],
                            ""description"": ""Value to apply. Boolean for toggles, number for sliders and number params, text for panels/scribbles/text params, index or item name/expression for value lists.""
                        },
                        ""param"": {
                            ""type"": ""string"",
                            ""description"": ""Optional. When the target is a component, the input parameter (name, nickname, or zero-based index) whose persistent default should be set.""
                        }
                    },
                    ""required"": [ ""instanceGuid"", ""value"" ]
                }",
                execute: this.GhSetValueAsync,
                tags: new[] { "canvas", "components", "mutating", "values" },
                outputSchema: @"{ ""type"": ""object"", ""properties"": { ""success"": { ""type"": ""boolean"" }, ""affectedGuids"": { ""type"": ""array"" } } }",
                annotations: new AIToolAnnotations(destructiveHint: false));
        }

        private async Task<AIReturn> GhSetValueAsync(AIToolCall toolCall)
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
                var instanceGuidArg = args["instanceGuid"]?.ToString();
                var value = args["value"];
                var paramName = args["param"]?.ToString();

                if (!Guid.TryParse(instanceGuidArg, out var guid))
                {
                    output.CreateError($"Missing or invalid 'instanceGuid': '{instanceGuidArg}'");
                    return output;
                }

                if (value == null || value.Type == JTokenType.Null)
                {
                    output.CreateError("Missing required 'value' argument.");
                    return output;
                }

                Debug.WriteLine($"[GhObjTools] GhSetValueAsync: guid={guid}, param={paramName ?? "<null>"}, value={value}");

                var (allowedGuids, protectedGuids) = CanvasProtection.FilterProtectedGuids(new List<Guid> { guid });

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
                    "Set value");
                var approved = await CanvasChangeReviewService.ReviewAsync(
                    review,
                    toolCall.InvocationContext,
                    toolCall.CancellationToken).ConfigureAwait(false);
                var accepted = approved
                    ? CanvasChangeReviewService.GetAcceptedComponentGuids(review)
                    : new HashSet<Guid>();

                var updated = new List<string>();
                var failed = new JObject();
                foreach (var acceptedGuid in allowedGuids.Where(accepted.Contains))
                {
                    var (result, detail) = ComponentManipulation.SetObjectValue(acceptedGuid, value, paramName);
                    if (result == ComponentManipulation.ObjectManipulationResult.Success)
                    {
                        updated.Add(acceptedGuid.ToString());
                    }
                    else
                    {
                        failed[acceptedGuid.ToString()] = detail;
                    }
                }

                var toolResult = new JObject
                {
                    ["updated"] = JArray.FromObject(updated),
                    ["protectedGuids"] = JArray.FromObject(protectedGuids.Select(g => g.ToString())),
                };
                if (failed.Count > 0)
                {
                    toolResult["failed"] = failed;
                }

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
