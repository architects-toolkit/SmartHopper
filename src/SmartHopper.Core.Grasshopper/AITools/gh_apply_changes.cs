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
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using GhJSON.Core;
using GhJSON.Core.SchemaModels;
using GhJSON.Grasshopper;
using Newtonsoft.Json.Linq;
using SmartHopper.Core.Grasshopper.Utils.Canvas;
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.Infrastructure.AITools;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AICall.Core.Returns;
using SmartHopper.ProviderSdk.Diagnostics;
using SmartHopper.ProviderSdk.Logging;

namespace SmartHopper.Core.Grasshopper.AITools
{
    /// <summary>
    /// Tool provider for <c>gh_apply_changes</c>: a finite, closed change-set
    /// request that applies related canvas edits in one review, one undo group
    /// and one scheduled solve — at most one put fragment followed by
    /// set_value/set_provider/connect/disconnect operations in declared order.
    /// </summary>
    public class gh_apply_changes : IAIToolProvider
    {
        private readonly string toolName = "gh_apply_changes";

        /// <summary>
        /// Returns the AI tool for applying finite canvas change sets.
        /// </summary>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AIMutatingTool(
                name: this.toolName,
                description: "Apply a finite set of related canvas changes in one review, one undo step and one scheduled solve. 'operations' is a non-empty array (max 256): at most one 'put' (first; places a GhJSON fragment like gh_put) followed by 'set_value' (target + value/panelMode/param), 'set_provider' (target + provider/model), 'connect'/'disconnect' (from/to endpoints). A target is exactly one of {\"instanceGuid\":\"GUID\"} (live object) or {\"id\":N} (a component inside this request's put fragment). Endpoints add optional 'paramName'/'paramIndex'. Every operation needs a unique 'opId'. Rejecting any reviewed row rejects the whole set. Use gh_get to discover live instance GUIDs and gh_put for large placements.",
                category: "Canvas.Modifications",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""operations"": {
                            ""type"": ""array"",
                            ""minItems"": 1,
                            ""maxItems"": 256,
                            ""items"": {
                                ""type"": ""object"",
                                ""properties"": {
                                    ""opId"": { ""type"": ""string"", ""description"": ""Unique non-empty operation id, echoed in per-op results."" },
                                    ""kind"": { ""type"": ""string"", ""enum"": [""put"", ""set_value"", ""set_provider"", ""connect"", ""disconnect""] },
                                    ""ghjson"": { ""type"": ""string"", ""description"": ""put only: GhJSON fragment."" },
                                    ""editMode"": { ""type"": ""boolean"", ""description"": ""put only: when true the autoOffset default is off."" },
                                    ""autoOffset"": { ""type"": ""boolean"", ""description"": ""put only: explicit auto-offset override."" },
                                    ""target"": { ""type"": ""object"", ""description"": ""set_value/set_provider: exactly one of instanceGuid or id."" },
                                    ""value"": { ""description"": ""set_value: value to apply."" },
                                    ""param"": { ""type"": ""string"", ""description"": ""set_value: optional input-parameter selector."" },
                                    ""panelMode"": { ""type"": ""string"", ""enum"": [""item"", ""list""] },
                                    ""provider"": { ""type"": ""string"" },
                                    ""model"": { ""type"": ""string"" },
                                    ""from"": { ""type"": ""object"", ""description"": ""connect/disconnect source (output) endpoint."" },
                                    ""to"": { ""type"": ""object"", ""description"": ""connect/disconnect target (input) endpoint."" }
                                },
                                ""required"": [""opId"", ""kind""],
                                ""additionalProperties"": false
                            }
                        }
                    },
                    ""required"": [""operations""],
                    ""additionalProperties"": false
                }",
                execute: this.GhApplyChangesAsync,
                boundary: MutationBoundary.Composite);
        }

        /// <summary>Human-readable label for a target in the review list.</summary>
        private static string DescribeTarget(CanvasChangeTarget target)
        {
            if (target.InstanceGuid.HasValue)
            {
                var obj = CanvasAccess.FindInstance(target.InstanceGuid.Value);
                if (obj != null)
                {
                    return !string.IsNullOrWhiteSpace(obj.NickName) ? obj.NickName : obj.Name;
                }

                return $"Object {target.InstanceGuid}";
            }

            return $"New component id {target.Id}";
        }

        /// <summary>Compact detail string for a set_value operation in the review list.</summary>
        private static string DescribeSetValueDetail(CanvasChangeOp op)
        {
            var parts = new List<string>();
            if (op.Value != null && op.Value.Type != JTokenType.Null)
            {
                parts.Add("value");
            }

            if (!string.IsNullOrEmpty(op.PanelMode))
            {
                parts.Add($"panelMode={op.PanelMode}");
            }

            if (!string.IsNullOrEmpty(op.Param))
            {
                parts.Add($"param={op.Param}");
            }

            return parts.Count == 0 ? string.Empty : $" ({string.Join(", ", parts)})";
        }

        private async Task<AIReturn> GhApplyChangesAsync(AIToolCall toolCall)
        {
            var output = new AIReturn()
            {
                Request = toolCall,
            };

            try
            {
                toolCall.SkipMetricsValidation = true;

                var clock = Stopwatch.StartNew();
                var toolInfo = toolCall.GetToolCall();
                var args = toolInfo.GetArgumentsOrEmpty();

                if (!CanvasChangePlan.TryParse(args["operations"] as JArray, out var plan, out var parseError))
                {
                    output.CreateError($"Invalid change set: {parseError}");
                    return output;
                }

                var parseMs = clock.ElapsedMilliseconds;

                // Parse and normalize the put fragment exactly like gh_put.
                GhJsonDocument? putFragment = null;
                var existingDocument = new GhJsonDocument();
                var existingGuids = new HashSet<Guid>();
                if (plan!.PutOp != null)
                {
                    putFragment = GhJson.FromJson(plan.PutOp.GhJson!);
                    putFragment = GhJson.Fix(putFragment).Document;
                    string? analysisMsg = null;
                    if (putFragment == null ||
                        !GhJson.IsValid(GhJson.ToJson(putFragment), out analysisMsg))
                    {
                        output.CreateError($"Invalid GhJSON in put operation: {analysisMsg}");
                        return output;
                    }

                    if (putFragment.Components == null)
                    {
                        output.CreateError("The put operation's GhJSON document has no components.");
                        return output;
                    }

                    // Drop protected components from the fragment before review/apply.
                    var protectedGuids = CanvasProtection.GetProtectedInstanceGuids();
                    if (protectedGuids.Count > 0)
                    {
                        var filtered = putFragment.Components
                            .Where(c => !(c.InstanceGuid.HasValue && protectedGuids.Contains(c.InstanceGuid.Value)))
                            .ToList();
                        if (filtered.Count != putFragment.Components.Count)
                        {
                            var remainingIds = filtered.Where(c => c.Id.HasValue).Select(c => c.Id!.Value).ToHashSet();
                            putFragment = new GhJsonDocument(
                                putFragment.Schema,
                                putFragment.Metadata,
                                filtered,
                                putFragment.Connections?.Where(conn =>
                                    remainingIds.Contains(conn.From.Id) && remainingIds.Contains(conn.To.Id)).ToList(),
                                putFragment.Groups);
                        }
                    }

                    foreach (var component in putFragment.Components.Where(c =>
                                 c.InstanceGuid.HasValue && c.InstanceGuid!.Value != Guid.Empty))
                    {
                        if (CanvasAccess.FindInstance(component.InstanceGuid!.Value) != null)
                        {
                            existingGuids.Add(component.InstanceGuid!.Value);
                        }
                    }

                    if (existingGuids.Count > 0)
                    {
                        existingDocument = GhJsonGrasshopper.GetByGuids(existingGuids);
                    }

                    // Fill only missing new-component pivots before review, matching
                    // gh_put's layout contract.
                    putFragment = await CanvasNewComponentLayout.ApplyAsync(
                        putFragment,
                        existingGuids,
                        offsetBelowExisting: plan.PutOp.AutoOffset ?? !(plan.PutOp.EditMode ?? false),
                        toolCall.CancellationToken).ConfigureAwait(false);
                }

                var prepareMs = clock.ElapsedMilliseconds - parseMs;

                // One review covering the whole set; all-or-nothing acceptance.
                var session = this.BuildReviewSession(plan, putFragment, existingDocument);
                if (session.Items.Count > 0)
                {
                    var reviewed = await CanvasChangeReviewService.ReviewAsync(
                        session,
                        toolCall.InvocationContext,
                        toolCall.CancellationToken).ConfigureAwait(false);
                    var allAccepted = reviewed && session.Items.All(session.IsEffectivelyAccepted);
                    if (!allAccepted)
                    {
                        var rejected = reviewed
                            ? "One or more operations in the change set were deselected; gh_apply_changes applies all or nothing."
                            : "The user cancelled the staged canvas changes.";
                        var rejectedResult = new JObject
                        {
                            ["status"] = "rejected",
                            ["appliedCount"] = 0,
                            ["operations"] = new JObject(),
                        };
                        output.CreateSuccess(AIBodyBuilder.Create()
                            .AddToolResult(rejectedResult, toolInfo.Id, toolInfo.Name ?? this.toolName)
                            .Build(), toolCall);
                        return output;
                    }
                }

                var reviewMs = clock.ElapsedMilliseconds - parseMs - prepareMs;

                // Commit in one bounded UI turn.
                var applyResult = await UiThreadMarshal.RunOnUiThreadAsync(
                    () =>
                    {
                        toolCall.CancellationToken.ThrowIfCancellationRequested();
                        var doc = GhJsonGrasshopper.GetActiveDocument();
                        if (doc == null)
                        {
                            throw new InvalidOperationException("No active Grasshopper document.");
                        }

                        return CanvasChangeApply.ApplyOnUiThread(plan, putFragment, doc, toolCall.CancellationToken);
                    },
                    toolCall.CancellationToken).ConfigureAwait(false);

                var commitMs = clock.ElapsedMilliseconds - parseMs - prepareMs - reviewMs;
                Logger.Trace(
                    typeof(gh_apply_changes).FullName,
                    $"[gh_apply_changes] parse={parseMs}ms prepare={prepareMs}ms review={reviewMs}ms commit={commitMs}ms ops={plan!.Ops.Count} status={applyResult.Status} solve={applyResult.SolutionScheduled}");

                var toolResult = new JObject
                {
                    ["status"] = applyResult.Status,
                    ["success"] = applyResult.Success,
                    ["appliedCount"] = applyResult.AppliedCount,
                    ["solutionScheduled"] = applyResult.SolutionScheduled,
                    ["operations"] = applyResult.OpResults,
                };

                if (applyResult.IdToGuidMapping.Count > 0)
                {
                    toolResult["idToGuid"] = JObject.FromObject(
                        applyResult.IdToGuidMapping.ToDictionary(
                            p => p.Key.ToString(CultureInfo.InvariantCulture),
                            p => p.Value.ToString()));
                }

                if (applyResult.Warnings.Count > 0)
                {
                    toolResult["warnings"] = JArray.FromObject(applyResult.Warnings);
                }

                if (!string.IsNullOrEmpty(applyResult.Error))
                {
                    toolResult["error"] = applyResult.Error;
                }

                if (!applyResult.Success)
                {
                    output.CreateError(applyResult.Error ?? $"Change set ended with status '{applyResult.Status}'.");
                    return output;
                }

                output.CreateSuccess(AIBodyBuilder.Create()
                    .AddToolResult(toolResult, toolInfo.Id, toolInfo.Name ?? this.toolName)
                    .Build(), toolCall);
                return output;
            }
            catch (OperationCanceledException)
            {
                var cancelInfo = toolCall.GetToolCall();
                var cancelledResult = new JObject
                {
                    ["status"] = "cancelled",
                    ["appliedCount"] = 0,
                    ["operations"] = new JObject(),
                };
                output.CreateSuccess(AIBodyBuilder.Create()
                    .AddToolResult(cancelledResult, cancelInfo.Id, cancelInfo.Name ?? this.toolName)
                    .Build(), toolCall);
                return output;
            }
            catch (Exception ex)
            {
                output.CreateError($"Error: {ex.Message}");
                return output;
            }
        }

        /// <summary>
        /// Builds the all-or-nothing review session: put items reuse
        /// <see cref="GhPutChangePlan"/> (component adds/modifies, connections,
        /// groups), and each remaining operation contributes one item.
        /// </summary>
        private CanvasChangeReviewSession BuildReviewSession(
            CanvasChangePlan plan,
            GhJsonDocument? putFragment,
            GhJsonDocument existingDocument)
        {
            var items = new List<CanvasChangeReviewItem>();
            GhJsonDocument previewDocument = putFragment ?? new GhJsonDocument();

            if (putFragment != null)
            {
                var putPlan = GhPutChangePlan.Create(putFragment, existingDocument);
                items.AddRange(putPlan.Session.Items);
            }

            foreach (var op in plan.Ops.Skip(plan.PutOp != null ? 1 : 0))
            {
                switch (op.Kind)
                {
                    case CanvasChangeOpKind.SetValue:
                        items.Add(new CanvasChangeReviewItem(
                            $"op:{op.OpId}",
                            CanvasChangeKind.ComponentModified,
                            DescribeTarget(op.Target!),
                            $"Set value{DescribeSetValueDetail(op)}"));
                        break;
                    case CanvasChangeOpKind.SetProvider:
                        items.Add(new CanvasChangeReviewItem(
                            $"op:{op.OpId}",
                            CanvasChangeKind.ComponentModified,
                            DescribeTarget(op.Target!),
                            $"Set provider/model to {op.Provider ?? "unchanged"} / {op.Model ?? "unchanged"}"));
                        break;
                    case CanvasChangeOpKind.Connect:
                        items.Add(new CanvasChangeReviewItem(
                            $"op:{op.OpId}",
                            CanvasChangeKind.ConnectionAdded,
                            $"{DescribeTarget(op.From!)} → {DescribeTarget(op.To!)}",
                            "Add wire"));
                        break;
                    case CanvasChangeOpKind.Disconnect:
                        items.Add(new CanvasChangeReviewItem(
                            $"op:{op.OpId}",
                            CanvasChangeKind.ConnectionRemoved,
                            $"{DescribeTarget(op.From!)} → {DescribeTarget(op.To!)}",
                            "Remove wire"));
                        break;
                }
            }

            // Include live targets in the preview document so the overlay shows
            // the objects the non-put operations touch.
            var liveTargets = plan.Ops
                .SelectMany(op => new[] { op.Target, op.From, op.To })
                .Where(t => t?.InstanceGuid != null)
                .Select(t => t!.InstanceGuid!.Value)
                .ToHashSet();
            if (liveTargets.Count > 0)
            {
                var liveDoc = GhJsonGrasshopper.GetByGuids(liveTargets);
                if (liveDoc?.Components != null && liveDoc.Components.Count > 0)
                {
                    previewDocument = new GhJsonDocument(
                        previewDocument.Schema,
                        previewDocument.Metadata,
                        previewDocument.Components
                            .Concat(liveDoc.Components.Where(lc =>
                                !(lc.InstanceGuid.HasValue &&
                                  previewDocument.Components.Any(pc => pc.InstanceGuid == lc.InstanceGuid))))
                            .ToList(),
                        previewDocument.Connections,
                        previewDocument.Groups);
                }
            }

            return new CanvasChangeReviewSession(
                "Review AI change set",
                this.toolName,
                previewDocument,
                items);
        }
    }
}
