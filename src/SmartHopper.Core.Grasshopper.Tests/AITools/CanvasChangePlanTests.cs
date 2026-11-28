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
using System.Linq;
using Newtonsoft.Json.Linq;
using SmartHopper.Core.Grasshopper.AITools;
using Xunit;

namespace SmartHopper.Core.Grasshopper.Tests.AITools
{
    /// <summary>
    /// Pure <see cref="CanvasChangePlan.TryParse"/> tests: closed schema,
    /// per-kind required fields, target union, put positioning, duplicate
    /// detection and cross-operation conflicts — no canvas access involved.
    /// </summary>
    public sealed class CanvasChangePlanTests
    {
        private static readonly Guid G1 = Guid.NewGuid();
        private static readonly Guid G2 = Guid.NewGuid();

        private static JArray Ops(params JObject[] ops)
        {
            return new JArray(ops.Cast<object>().ToArray());
        }

        private static JObject Op(string opId, string kind, JObject? extra = null)
        {
            var op = new JObject { ["opId"] = opId, ["kind"] = kind };
            if (extra != null)
            {
                foreach (var property in extra.Properties())
                {
                    op[property.Name] = property.Value;
                }
            }

            return op;
        }

        private static JObject GuidTarget(Guid guid)
        {
            return new JObject { ["instanceGuid"] = guid.ToString() };
        }

        private static JObject IdTarget(int id)
        {
            return new JObject { ["id"] = id };
        }

        [Fact]
        public void TryParse_RejectsEmptyOperations()
        {
            Assert.False(CanvasChangePlan.TryParse(new JArray(), out _, out var error));
            Assert.Contains("non-empty", error);
        }

        [Fact]
        public void TryParse_RejectsNullOperations()
        {
            Assert.False(CanvasChangePlan.TryParse(null, out _, out var error));
            Assert.Contains("non-empty", error);
        }

        [Fact]
        public void TryParse_RejectsOverLimit()
        {
            var ops = new JArray(Enumerable.Range(0, CanvasChangePlan.MaxOperations + 1)
                .Select(i => Op($"op{i}", "disconnect", new JObject
                {
                    ["from"] = GuidTarget(G1),
                    ["to"] = GuidTarget(G2),
                }))
                .Cast<object>()
                .ToArray());
            Assert.False(CanvasChangePlan.TryParse(ops, out _, out var error));
            Assert.Contains("capped", error);
        }

        [Fact]
        public void TryParse_RejectsMissingOpId()
        {
            var ops = Ops(new JObject { ["kind"] = "disconnect", ["from"] = GuidTarget(G1), ["to"] = GuidTarget(G2) });
            Assert.False(CanvasChangePlan.TryParse(ops, out _, out var error));
            Assert.Contains("opId", error);
        }

        [Fact]
        public void TryParse_RejectsDuplicateOpIds()
        {
            var ops = Ops(
                Op("a", "disconnect", new JObject { ["from"] = GuidTarget(G1), ["to"] = GuidTarget(G2) }),
                Op("a", "disconnect", new JObject { ["from"] = GuidTarget(G2), ["to"] = GuidTarget(G1) }));
            Assert.False(CanvasChangePlan.TryParse(ops, out _, out var error));
            Assert.Contains("Duplicate", error);
        }

        [Fact]
        public void TryParse_RejectsUnknownKind()
        {
            var ops = Ops(Op("a", "tidy_up"));
            Assert.False(CanvasChangePlan.TryParse(ops, out _, out var error));
            Assert.Contains("unknown kind", error);
        }

        [Fact]
        public void TryParse_RejectsUnknownFields()
        {
            var ops = Ops(Op("a", "disconnect", new JObject
            {
                ["from"] = GuidTarget(G1),
                ["to"] = GuidTarget(G2),
                ["toolName"] = "gh_connect",
            }));
            Assert.False(CanvasChangePlan.TryParse(ops, out _, out var error));
            Assert.Contains("unsupported field", error);
        }

        [Fact]
        public void TryParse_RejectsPutNotFirst()
        {
            var ops = Ops(
                Op("w", "disconnect", new JObject { ["from"] = GuidTarget(G1), ["to"] = GuidTarget(G2) }),
                Op("p", "put", new JObject { ["ghjson"] = "{}" }));
            Assert.False(CanvasChangePlan.TryParse(ops, out _, out var error));
            Assert.Contains("first", error);
        }

        [Fact]
        public void TryParse_RejectsTwoPuts()
        {
            var ops = Ops(
                Op("p1", "put", new JObject { ["ghjson"] = "{}" }),
                Op("p2", "put", new JObject { ["ghjson"] = "{}" }));
            Assert.False(CanvasChangePlan.TryParse(ops, out _, out var error));
        }

        [Fact]
        public void TryParse_RejectsPutMissingGhJson()
        {
            var ops = Ops(Op("p", "put"));
            Assert.False(CanvasChangePlan.TryParse(ops, out _, out var error));
            Assert.Contains("ghjson", error);
        }

        [Fact]
        public void TryParse_RejectsNonBooleanEditMode()
        {
            var ops = Ops(Op("p", "put", new JObject { ["ghjson"] = "{}", ["editMode"] = "yes" }));
            Assert.False(CanvasChangePlan.TryParse(ops, out _, out var error));
            Assert.Contains("boolean", error);
        }

        [Fact]
        public void TryParse_AcceptsPutWithBooleans()
        {
            var ops = Ops(Op("p", "put", new JObject { ["ghjson"] = "{}", ["editMode"] = true, ["autoOffset"] = false }));
            Assert.True(CanvasChangePlan.TryParse(ops, out var plan, out var error));
            Assert.NotNull(plan!.PutOp);
            Assert.True(plan.PutOp!.EditMode);
            Assert.False(plan.PutOp.AutoOffset);
        }

        [Fact]
        public void TryParse_RejectsTargetWithBothIdentityKeys()
        {
            var both = new JObject { ["instanceGuid"] = G1.ToString(), ["id"] = 3 };
            var ops = Ops(Op("a", "set_provider", new JObject { ["target"] = both, ["provider"] = "x" }));
            Assert.False(CanvasChangePlan.TryParse(ops, out _, out var error));
            Assert.Contains("exactly one", error);
        }

        [Fact]
        public void TryParse_RejectsIdTargetWithoutPut()
        {
            var ops = Ops(Op("a", "set_value", new JObject { ["target"] = IdTarget(7), ["value"] = 5 }));
            Assert.False(CanvasChangePlan.TryParse(ops, out _, out var error));
            Assert.Contains("no 'put'", error);
        }

        [Fact]
        public void TryParse_AcceptsIdTargetWithPut()
        {
            var ops = Ops(
                Op("p", "put", new JObject { ["ghjson"] = "{}" }),
                Op("a", "set_value", new JObject { ["target"] = IdTarget(7), ["value"] = 5 }));
            Assert.True(CanvasChangePlan.TryParse(ops, out _, out var error));
        }

        [Fact]
        public void TryParse_RejectsSetValueWithNothingToApply()
        {
            var ops = Ops(Op("a", "set_value", new JObject { ["target"] = GuidTarget(G1) }));
            Assert.False(CanvasChangePlan.TryParse(ops, out _, out var error));
            Assert.Contains("nothing to apply", error);
        }

        [Fact]
        public void TryParse_RejectsSetValueParamWithoutValue()
        {
            var ops = Ops(Op("a", "set_value", new JObject { ["target"] = GuidTarget(G1), ["param"] = "Seed" }));
            Assert.False(CanvasChangePlan.TryParse(ops, out _, out var error));
            Assert.Contains("requires 'value'", error);
        }

        [Fact]
        public void TryParse_RejectsBadPanelMode()
        {
            var ops = Ops(Op("a", "set_value", new JObject { ["target"] = GuidTarget(G1), ["panelMode"] = "tree" }));
            Assert.False(CanvasChangePlan.TryParse(ops, out _, out var error));
            Assert.Contains("panelMode", error);
        }

        [Fact]
        public void TryParse_AcceptsPanelModeOnly()
        {
            var ops = Ops(Op("a", "set_value", new JObject { ["target"] = GuidTarget(G1), ["panelMode"] = "list" }));
            Assert.True(CanvasChangePlan.TryParse(ops, out var plan, out _));
            Assert.Equal("list", plan!.Ops[0].PanelMode);
        }

        [Fact]
        public void TryParse_RejectsSetProviderWithoutProviderOrModel()
        {
            var ops = Ops(Op("a", "set_provider", new JObject { ["target"] = GuidTarget(G1) }));
            Assert.False(CanvasChangePlan.TryParse(ops, out _, out var error));
            Assert.Contains("provider", error);
        }

        [Fact]
        public void TryParse_RejectsPortSelectorsOnValueTarget()
        {
            var withPort = new JObject { ["instanceGuid"] = G1.ToString(), ["paramName"] = "A" };
            var ops = Ops(Op("a", "set_value", new JObject { ["target"] = withPort, ["value"] = 1 }));
            Assert.False(CanvasChangePlan.TryParse(ops, out _, out var error));
            Assert.Contains("unsupported field", error);
        }

        [Fact]
        public void TryParse_AcceptsPortSelectorsOnEndpoints()
        {
            var ops = Ops(Op("a", "connect", new JObject
            {
                ["from"] = new JObject { ["instanceGuid"] = G1.ToString(), ["paramName"] = "Out" },
                ["to"] = new JObject { ["instanceGuid"] = G2.ToString(), ["paramIndex"] = 0 },
            }));
            Assert.True(CanvasChangePlan.TryParse(ops, out var plan, out _));
            Assert.Equal("Out", plan!.Ops[0].From!.ParamName);
            Assert.Equal(0, plan.Ops[0].To!.ParamIndex);
        }

        [Fact]
        public void TryParse_RejectsNegativeParamIndex()
        {
            var ops = Ops(Op("a", "connect", new JObject
            {
                ["from"] = GuidTarget(G1),
                ["to"] = new JObject { ["instanceGuid"] = G2.ToString(), ["paramIndex"] = -1 },
            }));
            Assert.False(CanvasChangePlan.TryParse(ops, out _, out var error));
            Assert.Contains("paramIndex", error);
        }

        [Fact]
        public void TryParse_RejectsContradictoryWirePair()
        {
            var pair = new JObject
            {
                ["from"] = GuidTarget(G1),
                ["to"] = GuidTarget(G2),
            };
            var ops = Ops(
                Op("a", "connect", pair),
                Op("b", "disconnect", pair));
            Assert.False(CanvasChangePlan.TryParse(ops, out _, out var error));
            Assert.Contains("contradictory", error);
        }

        [Fact]
        public void TryParse_MarksExactDuplicateWireAsNoop()
        {
            var pair = new JObject
            {
                ["from"] = GuidTarget(G1),
                ["to"] = GuidTarget(G2),
            };
            var ops = Ops(
                Op("a", "connect", pair),
                Op("b", "connect", pair));
            Assert.True(CanvasChangePlan.TryParse(ops, out var plan, out _));
            Assert.False(plan!.Ops[0].IsDuplicateNoop);
            Assert.True(plan.Ops[1].IsDuplicateNoop);
        }

        [Fact]
        public void TryParse_RejectsConflictingValueWrites()
        {
            var ops = Ops(
                Op("a", "set_value", new JObject { ["target"] = GuidTarget(G1), ["value"] = 1 }),
                Op("b", "set_value", new JObject { ["target"] = GuidTarget(G1), ["panelMode"] = "list" }));
            Assert.False(CanvasChangePlan.TryParse(ops, out _, out var error));
            Assert.Contains("conflicts", error);
        }

        [Fact]
        public void TryParse_RejectsDoubleProviderWrite()
        {
            var ops = Ops(
                Op("a", "set_provider", new JObject { ["target"] = GuidTarget(G1), ["provider"] = "x" }),
                Op("b", "set_provider", new JObject { ["target"] = GuidTarget(G1), ["provider"] = "y" }));
            Assert.False(CanvasChangePlan.TryParse(ops, out _, out var error));
        }

        [Fact]
        public void TryParse_RejectsConnectIntoReplacedSettingsInput()
        {
            var ops = Ops(
                Op("a", "set_provider", new JObject { ["target"] = GuidTarget(G1), ["model"] = "m" }),
                Op("b", "connect", new JObject { ["from"] = GuidTarget(G2), ["to"] = GuidTarget(G1) }));
            Assert.False(CanvasChangePlan.TryParse(ops, out _, out var error));
            Assert.Contains("Settings", error);
        }

        [Fact]
        public void TryParse_AcceptsModelPlusConnectToNamedOtherInput()
        {
            var ops = Ops(
                Op("a", "set_provider", new JObject { ["target"] = GuidTarget(G1), ["model"] = "m" }),
                Op("b", "connect", new JObject
                {
                    ["from"] = GuidTarget(G2),
                    ["to"] = new JObject { ["instanceGuid"] = G1.ToString(), ["paramName"] = "Extra" },
                }));
            Assert.True(CanvasChangePlan.TryParse(ops, out _, out var error));
        }
    }
}
