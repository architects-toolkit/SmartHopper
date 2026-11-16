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
    /// Tool provider for surgically replacing items or whole branches inside the
    /// persistent data trees of panels and parameters, without rewriting the
    /// entire stored value.
    /// </summary>
    public class gh_edit_datatree : IAIToolProvider
    {
        /// <summary>
        /// Name of the AI tool provided by this class.
        /// </summary>
        private readonly string toolName = "gh_edit_datatree";

        /// <summary>
        /// Returns AI tools for editing stored data trees on canvas objects.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AIMutatingTool(
                name: this.toolName,
                description: "Replace individual items or whole branches inside the stored data of an existing canvas object — without rewriting the rest of the tree. Targets: panels (their lines) and parameters with persistent data, either standalone (e.g. a Number or Text param) or a component's input/output parameter selected via 'param' (name, nickname, or zero-based index; inputs are searched first). Addressing: '{a;b;c}' replaces a whole existing branch, '{a;b;c}(i)' replaces item i inside it, '(i)' is shorthand for '{0}(i)'. Indices are zero-based. Values use GhJSON prefixed encodings — e.g. 'text:DELTA', 'number:3.14', 'integer:7', 'boolean:true', 'pointXYZ:1,2,3', 'argb:255,0,0,0'. Item positions take one encoded string; branch positions take an array of encoded strings. Panels accept 'text:' values only; in list mode each value becomes/replaces one line, in single-item mode only '{0}(0)' or '{0}' with one value applies. Wired inputs and computed outputs cannot be edited — change the upstream source instead. All edits are validated before anything changes, then applied as one undoable step and the downstream solution is re-scheduled. Requires GUIDs from gh_get; use gh_set_value for single-value objects (toggles, sliders, value lists, whole-text scribbles).",
                category: "Canvas.Modifications",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""instanceGuid"": {
                            ""type"": ""string"",
                            ""description"": ""GUID of the target object (from gh_get): a panel, a standalone parameter, or a component whose parameter is selected via 'param'.""
                        },
                        ""param"": {
                            ""type"": ""string"",
                            ""description"": ""Optional. When the target is a component, the input or output parameter (name, nickname, or zero-based index; inputs are searched first) holding the persistent data to edit.""
                        },
                        ""edits"": {
                            ""type"": ""array"",
                            ""minItems"": 1,
                            ""items"": {
                                ""type"": ""object"",
                                ""properties"": {
                                    ""position"": {
                                        ""type"": ""string"",
                                        ""description"": ""Tree address: '{a;b;c}' targets a whole existing branch, '{a;b;c}(i)' targets item i inside it, '(i)' is shorthand for '{0}(i)'. Zero-based.""
                                    },
                                    ""value"": {
                                        ""type"": [""string"", ""array""],
                                        ""items"": { ""type"": ""string"" },
                                        ""description"": ""Replacement value(s) in GhJSON prefixed encoding (e.g. 'text:DELTA', 'pointXYZ:1,2,3'). A string for item positions, an array of strings for branch positions.""
                                    }
                                },
                                ""required"": [""position"", ""value""],
                                ""additionalProperties"": false
                            },
                            ""description"": ""Replacement edits applied in order. Overlapping addresses (a branch plus items inside it, or the same position twice) are rejected.""
                        }
                    },
                    ""required"": [ ""instanceGuid"", ""edits"" ]
                }",
                execute: this.GhEditDataTreeAsync,
                tags: new[] { "canvas", "components", "mutating", "data", "datatree", "values" },
                outputSchema: @"{ ""type"": ""object"", ""properties"": { ""success"": { ""type"": ""boolean"" }, ""affectedGuids"": { ""type"": ""array"" } } }",
                annotations: new AIToolAnnotations(destructiveHint: false),
                activityLabel: "editing data tree",
                resultLabel: "edited data tree");
        }

        private async Task<AIReturn> GhEditDataTreeAsync(AIToolCall toolCall)
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
                var paramName = args["param"]?.ToString();
                var edits = args["edits"] as JArray;

                if (!Guid.TryParse(instanceGuidArg, out var guid))
                {
                    output.CreateError($"Missing or invalid 'instanceGuid': '{instanceGuidArg}'");
                    return output;
                }

                if (edits == null || edits.Count == 0)
                {
                    output.CreateError("Missing required 'edits' array with at least one { position, value } entry.");
                    return output;
                }

                Debug.WriteLine($"[GhObjTools] GhEditDataTreeAsync: guid={guid}, param={paramName ?? "<null>"}, edits={edits.Count}");

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
                    "Edit data tree");
                var approved = await CanvasChangeReviewService.ReviewAsync(
                    review,
                    toolCall.InvocationContext,
                    toolCall.CancellationToken).ConfigureAwait(false);
                var accepted = approved
                    ? CanvasChangeReviewService.GetAcceptedComponentGuids(review)
                    : new HashSet<Guid>();

                var updated = new List<string>();
                var failed = new JObject();
                var shapes = new JObject();
                foreach (var acceptedGuid in allowedGuids.Where(accepted.Contains))
                {
                    var (result, detail, shape) = DataTreeManipulation.EditDataTree(acceptedGuid, paramName, edits);
                    if (result == ComponentManipulation.ObjectManipulationResult.Success)
                    {
                        updated.Add(acceptedGuid.ToString());
                        if (shape != null)
                        {
                            shapes[acceptedGuid.ToString()] = shape;
                        }
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
                if (shapes.Count > 0)
                {
                    toolResult["shape"] = shapes;
                }

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
