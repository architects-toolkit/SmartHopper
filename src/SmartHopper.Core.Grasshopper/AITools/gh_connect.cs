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
using GhJSON.Grasshopper;
using Grasshopper;
using Grasshopper.Kernel;
using Newtonsoft.Json.Linq;
using SmartHopper.Core.Grasshopper.Utils.Canvas;
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.Infrastructure.AITools;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AICall.Core.Returns;

namespace SmartHopper.Core.Grasshopper.AITools
{
    /// <summary>
    /// Tool provider for connecting Grasshopper components together.
    /// Creates wires between component parameters on the canvas.
    /// </summary>
    public class gh_connect : IAIToolProvider
    {
        /// <summary>
        /// Name of the AI tool provided by this class.
        /// </summary>
        private readonly string toolName = "gh_connect";

        /// <summary>
        /// Returns the GH connect tool.
        /// </summary>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AIMutatingTool(
                name: this.toolName,
                description: "Stage wires between Grasshopper outputs and inputs, show the user an in-canvas visual review, and create only accepted connections. Requires component GUIDs (use gh_get_selected or gh_get to find them first).",
                category: "Canvas.Modifications",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""connections"": {
                            ""type"": ""array"",
                            ""description"": ""Array of connection specifications"",
                            ""items"": {
                                ""type"": ""object"",
                                ""properties"": {
                                    ""sourceGuid"": {
                                        ""type"": ""string"",
                                        ""description"": ""GUID of the source component (output side)""
                                    },
                                    ""sourceParam"": {
                                        ""type"": ""string"",
                                        ""description"": ""Name or nickname of the output parameter. If not specified, uses the first output.""
                                    },
                                    ""targetGuid"": {
                                        ""type"": ""string"",
                                        ""description"": ""GUID of the target component (input side)""
                                    },
                                    ""targetParam"": {
                                        ""type"": ""string"",
                                        ""description"": ""Name or nickname of the input parameter. If not specified, uses the first input.""
                                    }
                                },
                                ""required"": [""sourceGuid"", ""targetGuid""]
                            }
                        }
                    },
                    ""required"": [""connections""]
                }",
                execute: this.GhConnectToolAsync,
                enabled: true,
                tags: new[] { "canvas", "components", "mutating", "connections" },
                outputSchema: @"{ ""type"": ""object"", ""properties"": { ""successful"": { ""type"": ""array"", ""items"": { ""type"": ""object"" } }, ""failed"": { ""type"": ""array"", ""items"": { ""type"": ""object"" } }, ""successCount"": { ""type"": ""integer"" }, ""failCount"": { ""type"": ""integer"" } } }",
                annotations: new AIToolAnnotations(destructiveHint: false),
                activityLabel: "wiring components together",
                resultLabel: "wired components together");
        }

        /// <summary>
        /// Executes the GH connect tool.
        /// </summary>
        private async Task<AIReturn> GhConnectToolAsync(AIToolCall toolCall)
        {
            var output = new AIReturn()
            {
                Request = toolCall,
            };

            try
            {
                AIInteractionToolCall toolInfo = toolCall.GetToolCall();
                var args = toolInfo.GetArgumentsOrEmpty();
                var connectionsArray = args["connections"] as JArray;

                if (connectionsArray == null || !connectionsArray.Any())
                {
                    output.CreateError("The 'connections' array is required and must contain at least one connection specification.");
                    return output;
                }

                var doc = GhJsonGrasshopper.GetActiveDocument();
                if (doc == null)
                {
                    output.CreateError("No active Grasshopper document found.");
                    return output;
                }

                var proposals = new List<CanvasConnectionReviewProposal>();
                var failedConnections = new List<JObject>();
                var protectedGuids = CanvasProtection.GetProtectedInstanceGuids();
                foreach (var connSpec in connectionsArray)
                {
                    var sourceGuidString = connSpec["sourceGuid"]?.ToString();
                    var targetGuidString = connSpec["targetGuid"]?.ToString();
                    if (!Guid.TryParse(sourceGuidString, out var sourceGuid) ||
                        !Guid.TryParse(targetGuidString, out var targetGuid))
                    {
                        failedConnections.Add(new JObject
                        {
                            ["error"] = "Missing or invalid sourceGuid or targetGuid",
                            ["spec"] = connSpec,
                        });
                        continue;
                    }

                    if (protectedGuids.Contains(sourceGuid) || protectedGuids.Contains(targetGuid))
                    {
                        failedConnections.Add(new JObject
                        {
                            ["error"] = "Connection rejected because it involves a protected component.",
                            ["sourceGuid"] = sourceGuidString,
                            ["targetGuid"] = targetGuidString,
                        });
                        continue;
                    }

                    proposals.Add(new CanvasConnectionReviewProposal
                    {
                        SourceGuid = sourceGuid,
                        TargetGuid = targetGuid,
                        SourceParameter = connSpec["sourceParam"]?.ToString(),
                        TargetParameter = connSpec["targetParam"]?.ToString(),
                    });
                }

                // A proposal whose wire already exists must not be staged: approving
                // it would re-prompt for a connection the user already allowed (for
                // example one just created by gh_put). Existing wires are reported
                // as no-op successes and never reach the review.
                var stagedProposals = new List<CanvasConnectionReviewProposal>();
                var noopConnections = new List<JObject>();
                var filterTcs = new TaskCompletionSource<bool>();
                Rhino.RhinoApp.InvokeOnUiThread(() =>
                {
                    try
                    {
                        foreach (var proposal in proposals)
                        {
                            if (IsAlreadyConnected(doc, proposal))
                            {
                                noopConnections.Add(new JObject
                                {
                                    ["sourceGuid"] = proposal.SourceGuid.ToString(),
                                    ["targetGuid"] = proposal.TargetGuid.ToString(),
                                    ["sourceParam"] = proposal.SourceParameter ?? "(first output)",
                                    ["targetParam"] = proposal.TargetParameter ?? "(first input)",
                                    ["status"] = "noop",
                                });
                            }
                            else
                            {
                                stagedProposals.Add(proposal);
                            }
                        }

                        filterTcs.SetResult(true);
                    }
                    catch (Exception ex)
                    {
                        filterTcs.SetException(ex);
                    }
                });
                await filterTcs.Task
                    .WaitAsync(toolCall.CancellationToken).ConfigureAwait(false);

                var reviewSession = CanvasChangeReviewService.CreateConnectionSession(
                    this.toolName,
                    stagedProposals,
                    CanvasChangeKind.ConnectionAdded);
                var applyReview = reviewSession.Items.Count > 0 &&
                    await CanvasChangeReviewService.ReviewAsync(reviewSession, toolCall.InvocationContext, toolCall.CancellationToken).ConfigureAwait(false);
                var acceptedIndexes = applyReview
                    ? CanvasChangeReviewService.GetAcceptedConnectionProposalIndexes(reviewSession)
                    : new HashSet<int>();
                var representedIndexes = reviewSession.Items
                    .Where(item => item.ComponentId.HasValue)
                    .Select(item => item.ComponentId!.Value)
                    .ToHashSet();
                var connectTcs = new TaskCompletionSource<List<JObject>>();
                Rhino.RhinoApp.InvokeOnUiThread(() =>
                {
                    try
                    {
                        toolCall.CancellationToken.ThrowIfCancellationRequested();
                        var successfulConnections = new List<JObject>();
                        for (var index = 0; index < stagedProposals.Count; index++)
                        {
                            var proposal = stagedProposals[index];
                            if (!acceptedIndexes.Contains(index))
                            {
                                failedConnections.Add(new JObject
                                {
                                    ["error"] = representedIndexes.Contains(index) ? "Connection rejected by the user." : "Source or target component was not found.",
                                    ["sourceGuid"] = proposal.SourceGuid.ToString(),
                                    ["targetGuid"] = proposal.TargetGuid.ToString(),
                                });
                                continue;
                            }

                            var success = GhJsonGrasshopper.Connect(
                                proposal.SourceGuid,
                                proposal.TargetGuid,
                                proposal.SourceParameter,
                                proposal.TargetParameter);
                            var result = new JObject
                            {
                                ["sourceGuid"] = proposal.SourceGuid.ToString(),
                                ["targetGuid"] = proposal.TargetGuid.ToString(),
                                ["sourceParam"] = proposal.SourceParameter ?? "(first output)",
                                ["targetParam"] = proposal.TargetParameter ?? "(first input)",
                            };
                            if (success)
                            {
                                result["status"] = "connected";
                                successfulConnections.Add(result);
                            }
                            else
                            {
                                result["error"] = "Connection failed - check component GUIDs and parameter names";
                                failedConnections.Add(result);
                            }
                        }

                        if (successfulConnections.Count > 0)
                        {
                            doc.NewSolution(false);
                            Instances.RedrawCanvas();
                        }

                        connectTcs.SetResult(successfulConnections);
                    }
                    catch (Exception ex)
                    {
                        connectTcs.SetException(ex);
                    }
                });

                var successfulConnections = await connectTcs.Task
                    .WaitAsync(toolCall.CancellationToken).ConfigureAwait(false);
                var allSuccessful = noopConnections.Concat(successfulConnections).ToList();

                var toolResult = new JObject
                {
                    ["successful"] = JArray.FromObject(allSuccessful),
                    ["failed"] = JArray.FromObject(failedConnections),
                    ["successCount"] = allSuccessful.Count,
                    ["failCount"] = failedConnections.Count
                };

                var body = AIBodyBuilder.Create()
                    .AddToolResult(toolResult, id: toolInfo.Id, name: toolInfo.Name ?? this.toolName)
                    .Build();

                output.CreateSuccess(body, toolCall);
                return output;
            }
            catch (Exception ex)
            {
                output.CreateError($"Error connecting components: {ex.Message}");
                return output;
            }
        }

        /// <summary>
        /// Checks whether the resolved wire already exists on the canvas.
        /// Mirrors the endpoint resolution GhJSON's <c>Connect</c> applies so the
        /// pre-check can never disagree with the apply step.
        /// </summary>
        private static bool IsAlreadyConnected(GH_Document doc, CanvasConnectionReviewProposal proposal)
        {
            var sourceParam = ResolveOutputParam(doc.FindObject(proposal.SourceGuid, true), proposal.SourceParameter);
            var targetParam = ResolveInputParam(doc.FindObject(proposal.TargetGuid, true), proposal.TargetParameter);
            return sourceParam != null && targetParam != null && targetParam.Sources.Contains(sourceParam);
        }

        private static IGH_Param? ResolveOutputParam(IGH_DocumentObject? obj, string? paramName)
        {
            if (obj is IGH_Param param)
            {
                return param;
            }

            return obj is IGH_Component component
                ? FindParam(component.Params.Output, paramName)
                : null;
        }

        private static IGH_Param? ResolveInputParam(IGH_DocumentObject? obj, string? paramName)
        {
            if (obj is IGH_Param param)
            {
                return param;
            }

            return obj is IGH_Component component
                ? FindParam(component.Params.Input, paramName)
                : null;
        }

        private static IGH_Param? FindParam(IList<IGH_Param> parameters, string? paramName)
        {
            if (string.IsNullOrWhiteSpace(paramName))
            {
                return parameters.FirstOrDefault();
            }

            var match = parameters.FirstOrDefault(p => string.Equals(p.NickName, paramName, StringComparison.OrdinalIgnoreCase))
                ?? parameters.FirstOrDefault(p => string.Equals(p.Name, paramName, StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                return match;
            }

            return int.TryParse(paramName, out var index) && index >= 0 && index < parameters.Count
                ? parameters[index]
                : null;
        }
    }
}
