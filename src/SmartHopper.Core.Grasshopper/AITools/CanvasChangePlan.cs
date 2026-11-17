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
using Newtonsoft.Json.Linq;

namespace SmartHopper.Core.Grasshopper.AITools
{
    /// <summary>
    /// A validated <c>gh_apply_changes</c> request. Parsing rejects unknown
    /// kinds/fields, malformed targets, duplicate opIds, conflicting writes and
    /// contradictory wire pairs before any canvas access happens.
    /// </summary>
    public sealed class CanvasChangePlan
    {
        /// <summary>Maximum number of operations accepted in one request.</summary>
        public const int MaxOperations = 256;

        private CanvasChangePlan(List<CanvasChangeOp> ops)
        {
            this.Ops = ops;
        }

        /// <summary>Gets the parsed operations in declared order.</summary>
        public IReadOnlyList<CanvasChangeOp> Ops { get; }

        /// <summary>Gets the single put operation, when present (always first).</summary>
        public CanvasChangeOp? PutOp => this.Ops.Count > 0 && this.Ops[0].Kind == CanvasChangeOpKind.Put ? this.Ops[0] : null;

        /// <summary>
        /// Parses and validates the operations array. A <c>null</c> return means
        /// the request is structurally invalid and <paramref name="error"/> names why.
        /// </summary>
        /// <param name="operations">The raw operations array.</param>
        /// <param name="plan">The validated plan.</param>
        /// <param name="error">The first rejection reason.</param>
        /// <returns>True when the plan is valid.</returns>
        public static bool TryParse(JArray? operations, out CanvasChangePlan? plan, out string? error)
        {
            plan = null;
            error = null;

            if (operations == null || operations.Count == 0)
            {
                error = "'operations' must be a non-empty array.";
                return false;
            }

            if (operations.Count > MaxOperations)
            {
                error = $"'operations' is capped at {MaxOperations} entries; got {operations.Count}.";
                return false;
            }

            var ops = new List<CanvasChangeOp>(operations.Count);
            var seenOpIds = new HashSet<string>(StringComparer.Ordinal);
            var putCount = 0;

            for (var i = 0; i < operations.Count; i++)
            {
                if (operations[i] is not JObject opObj)
                {
                    error = $"operations[{i}] must be an object.";
                    return false;
                }

                var opId = opObj["opId"]?.ToString();
                if (string.IsNullOrWhiteSpace(opId))
                {
                    error = $"operations[{i}] is missing a non-empty 'opId'.";
                    return false;
                }

                if (!seenOpIds.Add(opId))
                {
                    error = $"Duplicate 'opId' '{opId}' at operations[{i}].";
                    return false;
                }

                var kindText = opObj["kind"]?.ToString();
                if (!TryParseKind(kindText, out var kind))
                {
                    error = $"operations[{i}] ('{opId}') has unknown kind '{kindText}'. Expected put, set_value, set_provider, connect or disconnect.";
                    return false;
                }

                if (!ValidateFields(opObj, kind, opId, out error))
                {
                    return false;
                }

                CanvasChangeOp? op = null;
                switch (kind)
                {
                    case CanvasChangeOpKind.Put:
                        putCount++;
                        if (i != 0 || putCount > 1)
                        {
                            error = $"A 'put' operation must be the only put and first in the array ('{opId}').";
                            return false;
                        }

                        var ghjson = opObj["ghjson"]?.ToString();
                        if (string.IsNullOrWhiteSpace(ghjson))
                        {
                            error = $"operations[{i}] ('{opId}') is missing required 'ghjson'.";
                            return false;
                        }

                        var editModeToken = opObj["editMode"];
                        var autoOffsetToken = opObj["autoOffset"];
                        if ((editModeToken != null && editModeToken.Type != JTokenType.Boolean)
                            || (autoOffsetToken != null && autoOffsetToken.Type != JTokenType.Boolean))
                        {
                            error = $"operations[{i}] ('{opId}'): 'editMode' and 'autoOffset' must be booleans.";
                            return false;
                        }

                        op = new CanvasChangeOp
                        {
                            OpId = opId,
                            Kind = kind,
                            GhJson = ghjson,
                            EditMode = editModeToken?.ToObject<bool?>(),
                            AutoOffset = autoOffsetToken?.ToObject<bool?>(),
                        };
                        break;

                    case CanvasChangeOpKind.SetValue:
                        if (!CanvasChangeTarget.TryParse(opObj["target"], allowPortSelectors: false, out var setTarget, out error))
                        {
                            error = $"operations[{i}] ('{opId}') invalid target: {error}";
                            return false;
                        }

                        var value = opObj["value"];
                        var hasValue = value != null && value.Type != JTokenType.Null;
                        var param = opObj["param"]?.ToString();
                        var panelMode = opObj["panelMode"]?.ToString();
                        if (!string.IsNullOrEmpty(panelMode)
                            && !string.Equals(panelMode, "item", StringComparison.OrdinalIgnoreCase)
                            && !string.Equals(panelMode, "list", StringComparison.OrdinalIgnoreCase))
                        {
                            error = $"operations[{i}] ('{opId}') invalid 'panelMode': '{panelMode}'. Expected 'item' or 'list'.";
                            return false;
                        }

                        if (!string.IsNullOrWhiteSpace(param) && !hasValue)
                        {
                            error = $"operations[{i}] ('{opId}') requires 'value' when 'param' is supplied.";
                            return false;
                        }

                        if (string.IsNullOrWhiteSpace(param) && !hasValue && string.IsNullOrEmpty(panelMode))
                        {
                            error = $"operations[{i}] ('{opId}') has nothing to apply: supply 'value' and/or 'panelMode'.";
                            return false;
                        }

                        op = new CanvasChangeOp
                        {
                            OpId = opId,
                            Kind = kind,
                            Target = setTarget,
                            Value = value,
                            Param = param,
                            PanelMode = panelMode,
                        };
                        break;

                    case CanvasChangeOpKind.SetProvider:
                        if (!CanvasChangeTarget.TryParse(opObj["target"], allowPortSelectors: false, out var providerTarget, out error))
                        {
                            error = $"operations[{i}] ('{opId}') invalid target: {error}";
                            return false;
                        }

                        var provider = opObj["provider"]?.ToString()?.Trim();
                        var model = opObj["model"]?.ToString()?.Trim();
                        if (string.IsNullOrWhiteSpace(provider) && string.IsNullOrWhiteSpace(model))
                        {
                            error = $"operations[{i}] ('{opId}') requires at least one of 'provider' or 'model'.";
                            return false;
                        }

                        op = new CanvasChangeOp
                        {
                            OpId = opId,
                            Kind = kind,
                            Target = providerTarget,
                            Provider = provider,
                            Model = model,
                        };
                        break;

                    case CanvasChangeOpKind.Connect:
                    case CanvasChangeOpKind.Disconnect:
                        if (!CanvasChangeTarget.TryParse(opObj["from"], allowPortSelectors: true, out var from, out error))
                        {
                            error = $"operations[{i}] ('{opId}') invalid 'from' endpoint: {error}";
                            return false;
                        }

                        if (!CanvasChangeTarget.TryParse(opObj["to"], allowPortSelectors: true, out var to, out error))
                        {
                            error = $"operations[{i}] ('{opId}') invalid 'to' endpoint: {error}";
                            return false;
                        }

                        op = new CanvasChangeOp { OpId = opId, Kind = kind, From = from, To = to };
                        break;
                }

                ops.Add(op!);
            }

            if (!ValidateCrossOpRules(ops, out error))
            {
                return false;
            }

            plan = new CanvasChangePlan(ops);
            return true;
        }

        private static bool TryParseKind(string? kindText, out CanvasChangeOpKind kind)
        {
            kind = CanvasChangeOpKind.SetValue;
            switch (kindText)
            {
                case "put": kind = CanvasChangeOpKind.Put; return true;
                case "set_value": kind = CanvasChangeOpKind.SetValue; return true;
                case "set_provider": kind = CanvasChangeOpKind.SetProvider; return true;
                case "connect": kind = CanvasChangeOpKind.Connect; return true;
                case "disconnect": kind = CanvasChangeOpKind.Disconnect; return true;
                default: return false;
            }
        }

        private static bool ValidateFields(JObject opObj, CanvasChangeOpKind kind, string opId, out string? error)
        {
            var allowed = new HashSet<string>(StringComparer.Ordinal) { "opId", "kind" };
            switch (kind)
            {
                case CanvasChangeOpKind.Put:
                    allowed.UnionWith(new[] { "ghjson", "editMode", "autoOffset" });
                    break;
                case CanvasChangeOpKind.SetValue:
                    allowed.UnionWith(new[] { "target", "value", "param", "panelMode" });
                    break;
                case CanvasChangeOpKind.SetProvider:
                    allowed.UnionWith(new[] { "target", "provider", "model" });
                    break;
                case CanvasChangeOpKind.Connect:
                case CanvasChangeOpKind.Disconnect:
                    allowed.UnionWith(new[] { "from", "to" });
                    break;
            }

            var unknown = opObj.Properties().Select(p => p.Name).Where(name => !allowed.Contains(name)).ToList();
            if (unknown.Count > 0)
            {
                error = $"Operation '{opId}' contains unsupported field(s): {string.Join(", ", unknown)}.";
                return false;
            }

            error = null;
            return true;
        }

        /// <summary>
        /// Cross-operation rules: id targets require a put fragment, duplicate or
        /// contradictory wire ops conflict, and two writes to the same logical
        /// field of one target are ambiguous and rejected.
        /// </summary>
        private static bool ValidateCrossOpRules(List<CanvasChangeOp> ops, out string? error)
        {
            error = null;
            var hasPut = ops.Any(op => op.Kind == CanvasChangeOpKind.Put);
            var writes = new HashSet<string>(StringComparer.Ordinal);
            var wirePairs = new Dictionary<string, CanvasChangeOpKind>(StringComparer.Ordinal);
            var connectPairs = new HashSet<string>(StringComparer.Ordinal);
            var disconnectPairs = new HashSet<string>(StringComparer.Ordinal);

            foreach (var op in ops)
            {
                foreach (var target in EnumerateTargets(op))
                {
                    if (target.Id.HasValue && !hasPut)
                    {
                        error = $"Operation '{op.OpId}' targets component id {target.Id} but the request contains no 'put' fragment to resolve it.";
                        return false;
                    }
                }

                switch (op.Kind)
                {
                    case CanvasChangeOpKind.SetValue:
                        {
                            // Same target + same param selector is an ambiguous write,
                            // regardless of whether value or panelMode carries it.
                            var fieldKey = $"value:{op.Target!.IdentityKey()}:{op.Param ?? string.Empty}";
                            if (!writes.Add(fieldKey))
                            {
                                error = $"Operations write the same logical field twice: '{op.OpId}' conflicts on {op.Target!.IdentityKey()}.";
                                return false;
                            }

                            break;
                        }

                    case CanvasChangeOpKind.SetProvider:
                        {
                            if (op.Provider != null && !writes.Add($"provider:{op.Target!.IdentityKey()}"))
                            {
                                error = $"Operations set 'provider' twice on {op.Target!.IdentityKey()}: '{op.OpId}' conflicts.";
                                return false;
                            }

                            if (op.Model != null && !writes.Add($"model:{op.Target!.IdentityKey()}"))
                            {
                                error = $"Operations set 'model' twice on {op.Target!.IdentityKey()}: '{op.OpId}' conflicts.";
                                return false;
                            }

                            break;
                        }

                    case CanvasChangeOpKind.Connect:
                    case CanvasChangeOpKind.Disconnect:
                        {
                            var pair = $"wire:{op.From!.IdentityKey()}:{op.From.ParamName}:{op.From.ParamIndex}->{op.To!.IdentityKey()}:{op.To.ParamName}:{op.To.ParamIndex}";
                            if (wirePairs.TryGetValue(pair, out var previous) && previous != op.Kind)
                            {
                                error = $"Operations '{op.OpId}' and a previous op are a contradictory connect/disconnect pair on the same endpoints.";
                                return false;
                            }

                            wirePairs[pair] = op.Kind;
                            var pairSet = op.Kind == CanvasChangeOpKind.Connect ? connectPairs : disconnectPairs;
                            if (!pairSet.Add(pair))
                            {
                                // Exact duplicate wire: the first applies; this one is an
                                // explicit no-op rather than a last-writer-wins conflict.
                                op.IsDuplicateNoop = true;
                            }

                            break;
                        }
                }
            }

            // A set_provider(model=...) implicitly replaces the target's Settings
            // input sources with a new panel. An explicit op touching that input
            // in the same plan is ambiguous and must be rejected.
            var modelTargets = ops
                .Where(op => op.Kind == CanvasChangeOpKind.SetProvider && op.Model != null)
                .Select(op => op.Target!.IdentityKey())
                .ToHashSet(StringComparer.Ordinal);
            if (modelTargets.Count > 0)
            {
                foreach (var op in ops)
                {
                    if (op.Kind == CanvasChangeOpKind.Connect &&
                        op.To!.InstanceGuid.HasValue &&
                        modelTargets.Contains(op.To.IdentityKey()) &&
                        (string.IsNullOrEmpty(op.To.ParamName) ||
                         string.Equals(op.To.ParamName, "Settings", StringComparison.OrdinalIgnoreCase)))
                    {
                        error = $"Operation '{op.OpId}' wires into a Settings input that a same-plan 'set_provider(model)' already replaces; represent the panel/wires explicitly in 'put' instead.";
                        return false;
                    }
                }
            }

            return true;
        }

        private static IEnumerable<CanvasChangeTarget> EnumerateTargets(CanvasChangeOp op)
        {
            if (op.Target != null)
            {
                yield return op.Target;
            }

            if (op.From != null)
            {
                yield return op.From;
            }

            if (op.To != null)
            {
                yield return op.To;
            }
        }
    }
}
