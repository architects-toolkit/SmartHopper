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
using System.Drawing;
using System.Linq;
using System.Threading;
using GhJSON.Core;
using GhJSON.Core.SchemaModels;
using GhJSON.Grasshopper;
using GhJSON.Grasshopper.Deserialization;
using Grasshopper.Kernel;
using SmartHopper.Core.Grasshopper.Utils.Canvas;

namespace SmartHopper.Components.Test.AiTools
{
    /// <summary>
    /// Exercises measured placement planning without adding objects or switching documents.
    /// Requires Grasshopper and therefore lives outside CI unit-test projects.
    /// </summary>
    public sealed class TestGhJsonLayoutComponent : GH_Component
    {
        /// <summary>Initializes the runtime placement regression component.</summary>
        public TestGhJsonLayoutComponent()
            : base("Test GhJSON Layout", "TEST-GH-LAYOUT", "Tests pivot preservation and compact auto-layout without changing the canvas.", "SmartHopper Tests", "Testing AiTools")
        {
        }

        /// <inheritdoc/>
        public override Guid ComponentGuid => new Guid("55047F7B-BCFB-4915-8275-96FCFFFB3CB4");

        /// <inheritdoc/>
        protected override Bitmap Icon => null;

        /// <inheritdoc/>
        public override GH_Exposure Exposure => GH_Exposure.hidden;

        /// <inheritdoc/>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddBooleanParameter("Run?", "R", "Run the placement regressions.", GH_ParamAccess.item, false);
        }

        /// <inheritdoc/>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddBooleanParameter("Passed", "P", "Whether all regressions passed.", GH_ParamAccess.item);
            pManager.AddTextParameter("Results", "R", "Per-scenario results.", GH_ParamAccess.list);
        }

        /// <inheritdoc/>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var run = false;
            if (!DA.GetData(0, ref run) || !run)
            {
                return;
            }

            var results = new List<string>();
            var passed = true;
            foreach (var scenario in new[]
                     {
                         "authored", "automatic", "mixed", "mixed-output", "mixed-multiple", "existing",
                         "aligned", "aligned-named", "aligned-fixed",
                     })
            {
                try
                {
                    VerifyScenario(scenario, this.InstanceGuid);
                    results.Add($"PASS: {scenario}");
                }
                catch (Exception ex)
                {
                    passed = false;
                    results.Add($"FAIL: {scenario}: {ex.Message}");
                }
            }

            DA.SetData(0, passed);
            DA.SetDataList(1, results);
        }

        /// <summary>Checks one placement contract against real off-canvas object bounds.</summary>
        private static void VerifyScenario(string scenario, Guid liveGuid)
        {
            var document = GhJson.FromJson(@"{
                ""schema"": ""1.0"",
                ""components"": [
                    { ""name"": ""Panel"", ""id"": 1 },
                    { ""name"": ""Addition"", ""id"": 2 },
                    { ""name"": ""Panel"", ""id"": 3 }
                ],
                ""connections"": [
                    { ""from"": { ""id"": 1, ""paramIndex"": 0 }, ""to"": { ""id"": 2, ""paramIndex"": 0 } },
                    { ""from"": { ""id"": 2, ""paramIndex"": 0 }, ""to"": { ""id"": 3, ""paramIndex"": 0 } }
                ]
            }");
            var inputIndex = scenario.StartsWith("aligned", StringComparison.Ordinal) ? 1 : 0;
            document.Connections[0].To.ParamIndex = inputIndex;
            if (scenario == "aligned-named")
            {
                var measured = GhJsonGrasshopper.Deserialize(document, new DeserializationOptions { RegenerateInstanceGuids = true });
                var operation = (IGH_Component)measured.IdToObjectMapping[2];
                document.Connections[0].To.ParamIndex = null;
                document.Connections[0].To.ParamName = operation.Params.Input[inputIndex].Name;
                document.Connections[1].From.ParamIndex = null;
                document.Connections[1].From.ParamName = operation.Params.Output[0].Name;
            }

            var preserved = new HashSet<Guid>();
            if (scenario == "authored")
            {
                foreach (var component in document.Components)
                {
                    component.Pivot = new GhJsonPivot((component.Id!.Value - 1) * 200, 0);
                }
            }
            else if (scenario == "mixed")
            {
                document.Components[0].Pivot = new GhJsonPivot(0, 0);
            }
            else if (scenario == "mixed-output")
            {
                document.Components[2].Pivot = new GhJsonPivot(800, 0);
            }
            else if (scenario == "mixed-multiple")
            {
                document.Components[0].Pivot = new GhJsonPivot(0, 0);
                document.Components[2].Pivot = new GhJsonPivot(800, 0);
            }
            else if (scenario == "existing")
            {
                document.Components[0].InstanceGuid = liveGuid;
                preserved.Add(liveGuid);
            }
            else if (scenario == "aligned-fixed")
            {
                document.Components[1].Pivot = new GhJsonPivot(400, 200);
            }

            var authored = document.Components.Where(c => c.Pivot != null).ToDictionary(c => c.Id!.Value, c => c.Pivot!);
            CanvasNewComponentLayout.ApplyAsync(document, preserved, offsetBelowExisting: scenario.StartsWith("mixed", StringComparison.Ordinal), CancellationToken.None).GetAwaiter().GetResult();
            foreach (var component in document.Components)
            {
                if (authored.TryGetValue(component.Id!.Value, out var original))
                {
                    Require(ReferenceEquals(original, component.Pivot), "An authored pivot was overwritten.");
                }
                else if (component.InstanceGuid.HasValue && preserved.Contains(component.InstanceGuid.Value))
                {
                    Require(component.Pivot == null, "An existing object's missing pivot must remain unspecified.");
                }
                else
                {
                    Require(component.Pivot != null, "A missing new-component pivot was not generated.");
                }
            }

            if (scenario == "automatic" || scenario.StartsWith("mixed", StringComparison.Ordinal) ||
                scenario.StartsWith("aligned", StringComparison.Ordinal))
            {
                var objects = GhJsonGrasshopper.Deserialize(document, new DeserializationOptions
                {
                    RegenerateInstanceGuids = true,
                    WireConnections = true,
                    PerformLayout = true,
                });
                Require(objects.IdToObjectMapping.Count == 3, "The fixture could not be measured.");
                var left = objects.IdToObjectMapping[1].Attributes.Bounds;
                var middle = objects.IdToObjectMapping[2].Attributes.Bounds;
                var right = objects.IdToObjectMapping[3].Attributes.Bounds;
                if (scenario != "mixed-multiple")
                {
                    Require(Math.Abs(middle.Left - left.Right - 28f) <= 2f, "The input wire gap is not compact.");
                    Require(Math.Abs(right.Left - middle.Right - 28f) <= 2f, "The output wire gap is not compact.");
                }
                else
                {
                    Require(middle.Left >= left.Right + 28f && middle.Right <= right.Left - 28f, "The missing component must remain between its fixed neighbors.");
                }

                if (scenario.StartsWith("mixed", StringComparison.Ordinal))
                {
                    Require(Math.Abs(middle.Top - left.Top) < 100f, "The connected component was placed below, not relative to its anchors.");
                }

                if (scenario.StartsWith("aligned", StringComparison.Ordinal))
                {
                    var inputObject = objects.IdToObjectMapping[1];
                    var operation = (IGH_Component)objects.IdToObjectMapping[2];
                    var outputObject = objects.IdToObjectMapping[3];
                    Require(Math.Abs(inputObject.Attributes.OutputGrip.Y -
                                     operation.Params.Input[inputIndex].Attributes.InputGrip.Y) <= 1f,
                        "The single input leaf is not aligned to the occupied input grip.");
                    Require(Math.Abs(operation.Params.Output[0].Attributes.OutputGrip.Y -
                                     outputObject.Attributes.InputGrip.Y) <= 1f,
                        "The single output leaf is not aligned to the occupied output grip.");
                }
            }

            var before = document.Components.Select(c => c.Pivot).ToList();
            CanvasNewComponentLayout.ApplyAsync(document, preserved, offsetBelowExisting: true, CancellationToken.None).GetAwaiter().GetResult();
            Require(before.SequenceEqual(document.Components.Select(c => c.Pivot)), "Repeating placement shifted supplied pivots.");
        }

        /// <summary>Fails a regression with a concise diagnostic.</summary>
        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
