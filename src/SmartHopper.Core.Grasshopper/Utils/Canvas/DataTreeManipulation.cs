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
using GhJSON.Core.DataEditing;
using GhJSON.Grasshopper;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Special;
using Newtonsoft.Json.Linq;

namespace SmartHopper.Core.Grasshopper.Utils.Canvas
{
    /// <summary>
    /// Adapts AI-tool data-tree edits onto the shared GhJSON editing engine
    /// (<see cref="GhJsonGrasshopper.EditParamData"/> /
    /// <see cref="GhJsonGrasshopper.EditPanelData"/>). This class owns only the
    /// SmartHopper-specific concerns: translating the JSON tool arguments,
    /// resolving the canvas target, enforcing the wired-input policy, and
    /// wrapping the mutation in one undo record, expiry, and redraw. Position
    /// parsing, overlap checks, value decoding, and the staged atomic apply
    /// live in the GhJSON library.
    /// </summary>
    public static class DataTreeManipulation
    {
        /// <summary>
        /// Validates and applies a batch of replacement edits to a data-holding
        /// canvas object. Every edit must reference an existing address; the
        /// whole batch is validated and staged before anything mutates, so a
        /// failure leaves the target untouched. Executes on the Rhino UI thread.
        /// </summary>
        /// <param name="guid">GUID of the target object (panel, standalone param or component).</param>
        /// <param name="paramName">Optional input/output parameter selector when the target is a component.</param>
        /// <param name="edits">Edit objects with <c>position</c> and <c>value</c> fields.</param>
        /// <returns>Outcome, a human-readable detail message, and the resulting tree shape keyed by path.</returns>
        public static (ComponentManipulation.ObjectManipulationResult result, string detail, JObject? shape) EditDataTree(Guid guid, string? paramName, JArray edits)
        {
            var result = ComponentManipulation.ObjectManipulationResult.Failed;
            var detail = "Unknown failure";
            JObject? shape = null;

            // Translate the JSON tool arguments into library edit models before
            // touching the canvas — JSON shape validation needs no UI thread.
            var libraryEdits = new List<GhJsonDataEdit>(edits.Count);
            for (var i = 0; i < edits.Count; i++)
            {
                if (edits[i] is not JObject edit)
                {
                    return (result, $"Edit #{i} is not an object with 'position' and 'value'.", null);
                }

                var position = edit["position"]?.ToString();
                if (string.IsNullOrWhiteSpace(position))
                {
                    return (result, $"Edit #{i}: missing 'position'.", null);
                }

                var rawValue = edit["value"];
                List<string> values;
                if (rawValue is JArray array)
                {
                    values = new List<string>(array.Count);
                    foreach (var token in array)
                    {
                        if (token.Type != JTokenType.String)
                        {
                            return (result, $"Edit #{i}: branch values must be GhJSON-encoded strings.", null);
                        }

                        values.Add(token.ToString());
                    }
                }
                else if (rawValue != null && rawValue.Type == JTokenType.String)
                {
                    values = new List<string> { rawValue.ToString() };
                }
                else
                {
                    return (result, $"Edit #{i}: missing or invalid 'value'. Supply a GhJSON-encoded string (or an array for a branch).", null);
                }

                libraryEdits.Add(new GhJsonDataEdit { Position = position, Values = values });
            }

            try
            {
                UiThreadMarshal.RunAndWait(() =>
                {
                    var obj = CanvasAccess.FindInstance(guid);
                    if (obj == null)
                    {
                        result = ComponentManipulation.ObjectManipulationResult.NotFound;
                        detail = $"No canvas object matches {guid}";
                        return;
                    }

                    var doc = obj.OnPingDocument();
                    if (doc != null && doc.SolutionDepth > 0)
                    {
                        result = ComponentManipulation.ObjectManipulationResult.Busy;
                        detail = "A Grasshopper solution is in progress; the data tree was not edited.";
                        return;
                    }

                    // Resolve the data-holding target.
                    GH_Panel? panel = null;
                    IGH_Param? targetParam = null;
                    if (!string.IsNullOrWhiteSpace(paramName))
                    {
                        if (obj is not IGH_Component comp)
                        {
                            result = ComponentManipulation.ObjectManipulationResult.Unsupported;
                            detail = "The 'param' argument only applies to components.";
                            return;
                        }

                        targetParam = ResolveParam(comp, paramName);
                        if (targetParam == null)
                        {
                            result = ComponentManipulation.ObjectManipulationResult.NotFound;
                            detail = $"Component '{comp.NickName}' has no parameter matching '{paramName}'.";
                            return;
                        }
                    }
                    else if (obj is GH_Panel p)
                    {
                        panel = p;
                    }
                    else if (obj is IGH_Param prm)
                    {
                        targetParam = prm;
                    }
                    else
                    {
                        result = ComponentManipulation.ObjectManipulationResult.Unsupported;
                        detail = $"Object of type '{obj.GetType().Name}' does not hold an editable data tree.";
                        return;
                    }

                    // SmartHopper policy: persistent data on a wired input is
                    // shadowed by its sources, so an edit would be invisible —
                    // reject it with a hint instead of pretending it worked.
                    if (targetParam != null && targetParam.Kind == GH_ParamKind.input && targetParam.SourceCount > 0)
                    {
                        result = ComponentManipulation.ObjectManipulationResult.Unsupported;
                        detail = $"Input '{targetParam.NickName}' is wired; its persistent data is shadowed. Edit the upstream source or disconnect the wire first.";
                        return;
                    }

                    var targetName = panel?.NickName ?? targetParam!.NickName;

                    // Undo must capture the pre-change state, so record before the
                    // library applies the staged mutation (which runs inline now
                    // that we are on the UI thread).
                    obj.RecordUndoEvent("[SH] Edit Data Tree");

                    var editResult = panel != null
                        ? GhJsonGrasshopper.EditPanelData(panel, libraryEdits)
                        : GhJsonGrasshopper.EditParamData(targetParam!, libraryEdits);

                    if (!editResult.Success)
                    {
                        result = ComponentManipulation.ObjectManipulationResult.Failed;
                        detail = editResult.Error ?? "The data tree edit failed.";
                        return;
                    }

                    var expireTarget = (IGH_DocumentObject?)targetParam ?? obj;
                    if (expireTarget is IGH_ActiveObject active)
                    {
                        active.ExpireSolution(true);
                    }

                    // Defer the downstream solve to a scheduled solution instead of
                    // re-entering NewSolution inside a mutation turn (D12).
                    doc?.ScheduleSolution(5);
                    Instances.RedrawCanvas();

                    shape = editResult.Shape == null ? null : JObject.FromObject(editResult.Shape);
                    result = ComponentManipulation.ObjectManipulationResult.Success;
                    detail = $"Applied {editResult.AppliedCount} edit(s) to '{targetName}'";
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DataTreeManipulation] EditDataTree failed for {guid}: {ex.Message}");
                return (ComponentManipulation.ObjectManipulationResult.Failed, ex.Message, null);
            }

            return (result, detail, shape);
        }

        private static IGH_Param? ResolveParam(IGH_Component comp, string paramName)
        {
            if (int.TryParse(paramName, out var index))
            {
                if (index >= 0 && index < comp.Params.Input.Count)
                {
                    return comp.Params.Input[index];
                }
            }
            else
            {
                var match = comp.Params.Input.FirstOrDefault(p =>
                    string.Equals(p.NickName, paramName, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(p.Name, paramName, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                {
                    return match;
                }

                match = comp.Params.Output.FirstOrDefault(p =>
                    string.Equals(p.NickName, paramName, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(p.Name, paramName, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }
    }
}
