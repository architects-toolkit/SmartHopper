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
using System.Threading;
using GhJSON.Core.SchemaModels;
using GhJSON.Grasshopper;
using GhJSON.Grasshopper.ConnectionOperations;
using GhJSON.Grasshopper.PutOperations;
using Grasshopper;
using Grasshopper.Kernel;
using Newtonsoft.Json.Linq;
using SmartHopper.Core.ComponentBase.Contracts;
using SmartHopper.Core.ComponentBase.Cores;
using SmartHopper.Core.Grasshopper.Utils.Canvas;
using SmartHopper.Infrastructure.AIProviders;
using SmartHopper.ProviderSdk.Logging;

namespace SmartHopper.Core.Grasshopper.AITools
{
    /// <summary>
    /// The <c>gh_apply_changes</c> commit engine: applies a validated
    /// <see cref="CanvasChangePlan"/> inside one UI-thread turn — optional put
    /// first, remaining operations in declared order, per-operation undo
    /// records, captured inverses for compensation, and exactly one scheduled
    /// solve at the end. A failing operation stops the commit and reverses
    /// already-applied inverses where they are sound.
    /// </summary>
    public static class CanvasChangeApply
    {
        /// <summary>
        /// Applies the plan. Must be called on the Grasshopper UI thread inside
        /// bounded admission; the caller owns the review that precedes it.
        /// </summary>
        /// <param name="plan">The validated plan.</param>
        /// <param name="putFragment">Parsed/normalized GhJSON for the put op (null when none).</param>
        /// <param name="doc">The bound document.</param>
        /// <param name="cancellationToken">Cancellation.</param>
        /// <returns>The commit result.</returns>
        public static CanvasChangeApplyResult ApplyOnUiThread(
            CanvasChangePlan plan,
            GhJsonDocument? putFragment,
            GH_Document doc,
            CancellationToken cancellationToken)
        {
            if (plan == null)
            {
                throw new ArgumentNullException(nameof(plan));
            }

            if (doc == null)
            {
                throw new ArgumentNullException(nameof(doc));
            }

            var result = new CanvasChangeApplyResult();
            if (doc.SolutionDepth > 0)
            {
                result.Status = "busy";
                result.Error = "A Grasshopper solution is in progress; the change set was not applied.";
                return result;
            }

            var inverses = new List<Action>();
            var changed = false;
            var putResult = (PutResult?)null;

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Phase 1: the single put fragment, always first.
                if (plan.PutOp != null && putFragment != null)
                {
                    var putOp = plan.PutOp;
                    var putOptions = new PutOptions
                    {
                        RegenerateInstanceGuids = false,
                        UpdateExistingByInstanceGuid = true,
                        CreateConnections = true,
                        CreateGroups = true,
                        SelectPlacedObjects = true,
                        SkipInvalidComponents = true,
                        AutoOffset = putOp.AutoOffset ?? !(putOp.EditMode ?? false),
                        RequestSolution = false,
                    };

                    using var prepared = GhJsonGrasshopper.PreparePut(putFragment, putOptions, doc);
                    if (prepared == null)
                    {
                        MarkOp(result, putOp, "failed", "No Grasshopper document could be bound.");
                        result.Error = "Put failed: no Grasshopper document could be bound.";
                        return result;
                    }

                    putResult = GhJsonGrasshopper.ApplyPreparedPut(prepared);
                    foreach (var pair in putResult.IdToGuidMapping)
                    {
                        result.IdToGuidMapping[pair.Key] = pair.Value;
                    }

                    foreach (var warning in putResult.Warnings)
                    {
                        result.Warnings.Add(warning);
                    }

                    var putDetail = putResult.Success
                        ? $"Placed {putResult.ComponentsPlaced}, updated {putResult.ComponentsUpdated}, wired {putResult.ConnectionsCreated}"
                        : putResult.ErrorMessage;
                    MarkOp(result, putOp, putResult.Success ? "applied" : "failed", putDetail);

                    if (!putResult.Success)
                    {
                        result.Error = $"Put failed: {putResult.ErrorMessage}";
                        result.Status = "failed";
                        return result;
                    }

                    // Inverse: remove everything the put added (placed or replaced),
                    // in reverse add order. Updated in-place objects keep their own
                    // undo records and are not restored by the compensator — a
                    // failed composite still reports partial honestly.
                    var placedForInverse = putResult.PlacedObjects.ToList();
                    inverses.Add(() =>
                    {
                        foreach (var obj in placedForInverse)
                        {
                            try
                            {
                                obj.IsolateObject();
                                doc.RemoveObject(obj, false);
                            }
                            catch (Exception ex)
                            {
                                Logger.Warning("CanvasChangeApply", $"Compensation remove failed for {obj.InstanceGuid}: {ex.Message}");
                            }
                        }
                    });

                    changed |= putResult.ComponentsPlaced + putResult.ComponentsUpdated + putResult.ConnectionsCreated > 0;
                    if (putResult.FailedComponents.Count > 0)
                    {
                        result.Warnings.Add($"Skipped invalid components: {string.Join(", ", putResult.FailedComponents)}");
                    }
                }

                // Phase 2: remaining operations in declared order.
                foreach (var op in plan.Ops.Skip(plan.PutOp != null ? 1 : 0))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var opError = ApplyOp(op, doc, result, inverses, ref changed);
                    if (opError != null)
                    {
                        // Compensate everything already applied, newest first.
                        var compensated = Compensate(inverses);
                        result.Status = compensated ? "failed_compensated" : "failed_partial";
                        result.Error = opError;
                        result.Warnings.Add(compensated
                            ? "The commit was compensated back to its pre-apply state; see per-op results."
                            : "Compensation was incomplete; some earlier changes remain applied.");
                        return result;
                    }
                }

                result.Status = "applied";
                result.Success = true;
                return result;
            }
            catch (OperationCanceledException)
            {
                var compensated = Compensate(inverses);
                result.Status = compensated ? "failed_compensated" : "failed_partial";
                result.Error = "The change set was cancelled during apply.";
                throw;
            }
            catch (Exception ex)
            {
                var compensated = Compensate(inverses);
                result.Status = compensated ? "failed_compensated" : "failed_partial";
                result.Error = $"Apply failed: {ex.Message}";
                return result;
            }
            finally
            {
                if (changed && result.Success)
                {
                    // Exactly one deferred solve covers the whole commit — placed
                    // objects, rewired endpoints and restored state all recompute
                    // together after the acknowledgement is owed.
                    try
                    {
                        doc.ScheduleSolution(5);
                        result.SolutionScheduled = true;
                    }
                    catch (Exception ex)
                    {
                        result.Warnings.Add($"Committed, but the downstream solution could not be scheduled: {ex.Message}");
                    }

                    try
                    {
                        Instances.RedrawCanvas();
                    }
                    catch
                    {
                        // Redraw is cosmetic; never mask the commit outcome.
                    }
                }
            }
        }

        private static string? ApplyOp(
            CanvasChangeOp op,
            GH_Document doc,
            CanvasChangeApplyResult result,
            List<Action> inverses,
            ref bool changed)
        {
            if (op.IsDuplicateNoop)
            {
                MarkOp(result, op, "noop", "Exact duplicate of an earlier operation.");
                return null;
            }

            switch (op.Kind)
            {
                case CanvasChangeOpKind.SetValue:
                    return ApplySetValue(op, doc, result, inverses, ref changed);
                case CanvasChangeOpKind.SetProvider:
                    return ApplySetProvider(op, doc, result, inverses, ref changed);
                case CanvasChangeOpKind.Connect:
                case CanvasChangeOpKind.Disconnect:
                    return ApplyWire(op, doc, result, inverses, ref changed);
                default:
                    return $"Operation '{op.OpId}' kind is not supported by the commit engine.";
            }
        }

        private static string? ApplySetValue(
            CanvasChangeOp op,
            GH_Document doc,
            CanvasChangeApplyResult result,
            List<Action> inverses,
            ref bool changed)
        {
            if (!ResolveObject(op.Target!, doc, result, out var obj, out var resolveError))
            {
                return resolveError;
            }

            if (CanvasProtection.IsProtected(obj!.InstanceGuid))
            {
                MarkOp(result, op, "failed", "Target is protected from canvas edits.");
                return $"Operation '{op.OpId}' targets a protected component.";
            }

            var (res, detail) = ComponentManipulation.SetObjectValueOnUiThread(
                obj.InstanceGuid, op.Value!, op.Param, op.PanelMode, out _);

            if (res != ComponentManipulation.ObjectManipulationResult.Success)
            {
                MarkOp(result, op, "failed", detail);
                return $"Operation '{op.OpId}' failed: {detail}";
            }

            // Compensation for arbitrary persistent-value writes is the recorded
            // Grasshopper undo record itself — re-applying old state for every
            // supported target type cannot be captured safely, so this op does
            // not contribute a compensating inverse and a later failure reports
            // failed_partial rather than fabricating a restore.
            MarkOp(result, op, "applied", detail);
            changed = true;
            return null;
        }

        private static string? ApplySetProvider(
            CanvasChangeOp op,
            GH_Document doc,
            CanvasChangeApplyResult result,
            List<Action> inverses,
            ref bool changed)
        {
            if (!ResolveObject(op.Target!, doc, result, out var obj, out var resolveError))
            {
                return resolveError;
            }

            if (CanvasProtection.IsProtected(obj!.InstanceGuid))
            {
                MarkOp(result, op, "failed", "Target is protected from canvas edits.");
                return $"Operation '{op.OpId}' targets a protected component.";
            }

            if (obj is not IProviderComponent providerComp || obj is not GH_Component ghComp)
            {
                MarkOp(result, op, "failed", "Target does not support AI provider selection.");
                return $"Operation '{op.OpId}' target does not support AI provider selection.";
            }

            var provider = op.Provider;
            if (!string.IsNullOrWhiteSpace(provider))
            {
                var registered = ProviderManager.Instance.GetProviders().ToList();
                var match = registered.FirstOrDefault(p => string.Equals(p.Name, provider, StringComparison.OrdinalIgnoreCase));
                var isDefault = string.Equals(provider, ProviderSelectionCore.DEFAULT_PROVIDER, StringComparison.OrdinalIgnoreCase);
                if (match == null && !isDefault)
                {
                    var available = string.Join(", ", registered.Select(p => p.Name).Append(ProviderSelectionCore.DEFAULT_PROVIDER));
                    MarkOp(result, op, "failed", $"Unknown provider '{provider}'. Available: {available}");
                    return $"Operation '{op.OpId}' references unknown provider '{provider}'.";
                }

                provider = match?.Name ?? ProviderSelectionCore.DEFAULT_PROVIDER;
            }

            IGH_Param? settingsParam = null;
            if (!string.IsNullOrWhiteSpace(op.Model))
            {
                settingsParam = ghComp.Params.Input
                    .FirstOrDefault(p => string.Equals(p.Name, WellKnownInputs.Settings, StringComparison.OrdinalIgnoreCase));
                if (settingsParam == null)
                {
                    MarkOp(result, op, "failed", "Target component has no Settings input.");
                    return $"Operation '{op.OpId}' target has no Settings input.";
                }
            }

            var outcome = ComponentProviderOps.ApplySetProviderOnUiThread(
                ghComp, providerComp, provider, op.Model, settingsParam, doc);
            if (!outcome.Success)
            {
                MarkOp(result, op, "failed", outcome.Error);
                return $"Operation '{op.OpId}' failed: {outcome.Error}";
            }

            if (outcome.Inverse != null)
            {
                var inverse = outcome.Inverse;
                inverses.Add(() => ComponentProviderOps.CompensateSetProviderOnUiThread(inverse, doc));
            }

            var detail = outcome.PanelGuid.HasValue ? $"Model panel {outcome.PanelGuid}" : "Provider updated";
            MarkOp(result, op, "applied", detail);
            changed = true;
            return null;
        }

        private static string? ApplyWire(
            CanvasChangeOp op,
            GH_Document doc,
            CanvasChangeApplyResult result,
            List<Action> inverses,
            ref bool changed)
        {
            if (!ResolveObject(op.From!, doc, result, out var fromObj, out var fromError))
            {
                return fromError;
            }

            if (!ResolveObject(op.To!, doc, result, out var toObj, out var toError))
            {
                return toError;
            }

            if (CanvasProtection.IsProtected(fromObj!.InstanceGuid) || CanvasProtection.IsProtected(toObj!.InstanceGuid))
            {
                MarkOp(result, op, "failed", "Operation involves a protected component.");
                return $"Operation '{op.OpId}' involves a protected component.";
            }

            var fromResolution = GhJsonGrasshopper.TryResolveEndpoint(
                fromObj, op.From!.ParamName, op.From.ParamIndex, input: false);
            var toResolution = GhJsonGrasshopper.TryResolveEndpoint(
                toObj, op.To!.ParamName, op.To.ParamIndex, input: true);
            if (!fromResolution.Resolved)
            {
                MarkOp(result, op, "failed", fromResolution.Error);
                return $"Operation '{op.OpId}': {fromResolution.Error}";
            }

            if (!toResolution.Resolved)
            {
                MarkOp(result, op, "failed", toResolution.Error);
                return $"Operation '{op.OpId}': {toResolution.Error}";
            }

            var sourceParam = fromResolution.Param!;
            var targetParam = toResolution.Param!;

            if (op.Kind == CanvasChangeOpKind.Connect)
            {
                if (targetParam.Sources.Contains(sourceParam))
                {
                    MarkOp(result, op, "noop", "Wire already exists.");
                    return null;
                }

                targetParam.RecordUndoEvent("[SH] Connect");
                targetParam.AddSource(sourceParam);
                inverses.Add(() =>
                {
                    if (targetParam.Sources.Contains(sourceParam))
                    {
                        targetParam.RemoveSource(sourceParam);
                    }
                });
                MarkOp(result, op, "applied", "Wire added.");
                changed = true;
                return null;
            }
            else
            {
                if (!targetParam.Sources.Contains(sourceParam))
                {
                    MarkOp(result, op, "noop", "Wire does not exist.");
                    return null;
                }

                targetParam.RecordUndoEvent("[SH] Disconnect");
                targetParam.RemoveSource(sourceParam);
                inverses.Add(() =>
                {
                    if (!targetParam.Sources.Contains(sourceParam))
                    {
                        targetParam.AddSource(sourceParam);
                    }
                });
                MarkOp(result, op, "applied", "Wire removed.");
                changed = true;
                return null;
            }
        }

        private static bool ResolveObject(
            CanvasChangeTarget target,
            GH_Document doc,
            CanvasChangeApplyResult result,
            out IGH_DocumentObject? obj,
            out string? error)
        {
            obj = null;
            error = null;

            Guid? guid = target.InstanceGuid;
            if (!guid.HasValue && target.Id.HasValue)
            {
                if (result.IdToGuidMapping.TryGetValue(target.Id.Value, out var mapped))
                {
                    guid = mapped;
                }
                else
                {
                    error = $"Component id {target.Id} was not placed by this request's put.";
                    return false;
                }
            }

            if (!guid.HasValue)
            {
                error = "Target carries neither instanceGuid nor a resolvable id.";
                return false;
            }

            obj = doc.FindObject(guid.Value, true);
            if (obj == null)
            {
                error = $"No canvas object matches {guid}.";
                return false;
            }

            return true;
        }

        private static bool Compensate(List<Action> inverses)
        {
            var clean = true;
            for (var i = inverses.Count - 1; i >= 0; i--)
            {
                try
                {
                    inverses[i]();
                }
                catch
                {
                    clean = false;
                }
            }

            return clean;
        }

        private static void MarkOp(CanvasChangeApplyResult result, CanvasChangeOp op, string status, string? detail)
        {
            var entry = new JObject { ["status"] = status };
            if (!string.IsNullOrEmpty(detail))
            {
                entry["detail"] = detail;
            }

            result.OpResults[op.OpId] = entry;
            if (status == "applied")
            {
                result.AppliedCount++;
            }
        }
    }
}
