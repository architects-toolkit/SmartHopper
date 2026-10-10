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
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using GhJSON.Grasshopper;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Special;
using Grasshopper.Kernel.Types;
using Newtonsoft.Json.Linq;
using Rhino;

namespace SmartHopper.Core.Grasshopper.Utils.Canvas
{
    /// <summary>
    /// Utilities for manipulating Grasshopper component preview state.
    /// </summary>
    public static class ComponentManipulation
    {
        /// <summary>
        /// Outcome of a <see cref="SetObjectValue"/> attempt.
        /// </summary>
        public enum SetValueResult
        {
            /// <summary>The value was applied.</summary>
            Success,

            /// <summary>No document object exists for the supplied GUID.</summary>
            NotFound,

            /// <summary>The object (or the named input) cannot take a direct value.</summary>
            Unsupported,

            /// <summary>The mutation could not be applied safely.</summary>
            Failed,
        }

        /// <summary>
        /// Outcome of a <see cref="PulseObject"/> attempt.
        /// </summary>
        public enum PulseResult
        {
            /// <summary>The object was locked then re-enabled; a re-solve was scheduled.</summary>
            Pulsed,

            /// <summary>No document object exists for the supplied GUID.</summary>
            NotFound,

            /// <summary>The object is locked; locked objects cannot run.</summary>
            Locked,

            /// <summary>A solution is currently in progress.</summary>
            Busy,

            /// <summary>The object does not participate in solutions (not an active object).</summary>
            Unsupported,
        }

        /// <summary>
        /// Set preview state of a Grasshopper component by GUID.
        /// </summary>
        /// <param name="guid">GUID of the component.</param>
        /// <param name="previewOn">True to show preview, false to hide.</param>
        /// <param name="redraw">True to redraw canvas immediately.</param>
        public static void SetComponentPreview(Guid guid, bool previewOn, bool redraw = true)
        {
            Debug.WriteLine($"[ComponentManipulation] SetComponentPreview: guid={guid}, previewOn={previewOn}");
            var obj = CanvasAccess.FindInstance(guid);
            Debug.WriteLine(obj != null
                ? $"[ComponentManipulation] Found object of type {obj.GetType().Name}"
                : "[ComponentManipulation] Found null object");
            if (obj is GH_Component component)
            {
                Debug.WriteLine($"[ComponentManipulation] Component.IsPreviewCapable={component.IsPreviewCapable}, Hidden={component.Hidden}");
                if (component.IsPreviewCapable)
                {
                    obj.RecordUndoEvent("[SH] Set Component Preview");
                    component.Hidden = !previewOn;
                    Debug.WriteLine($"[ComponentManipulation] New Hidden={component.Hidden}");
                    if (redraw)
                    {
                        Instances.RedrawCanvas();
                        Debug.WriteLine("[ComponentManipulation] Canvas redrawn");
                    }
                }
                else
                {
                    Debug.WriteLine("[ComponentManipulation] Component is not preview capable");
                }
            }
            else if (obj is IGH_Param param)
            {
                Debug.WriteLine($"[ComponentManipulation] IGH_Param found: {param.GetType().Name}");

                // Check if the parameter implements IGH_PreviewObject for preview capabilities
                if (param is IGH_PreviewObject paramPreview)
                {
                    Debug.WriteLine($"[ComponentManipulation] IGH_Param.Hidden={paramPreview.Hidden}, IsPreviewCapable={paramPreview.IsPreviewCapable}");
                    if (paramPreview.IsPreviewCapable)
                    {
                        obj.RecordUndoEvent("[SH] Set Parameter Preview");
                        paramPreview.Hidden = !previewOn;
                        Debug.WriteLine($"[ComponentManipulation] New Hidden={paramPreview.Hidden}");
                        if (redraw)
                        {
                            Instances.RedrawCanvas();
                            Debug.WriteLine("[ComponentManipulation] Canvas redrawn");
                        }
                    }
                    else
                    {
                        Debug.WriteLine("[ComponentManipulation] IGH_Param is not preview capable");
                    }
                }
                else
                {
                    Debug.WriteLine("[ComponentManipulation] IGH_Param does not implement IGH_PreviewObject");
                }
            }
            else
            {
                Debug.WriteLine("[ComponentManipulation] Object is not previewable (not a GH_DocumentObject)");
            }
        }

        /// <summary>
        /// Set lock state of a Grasshopper component by GUID.
        /// </summary>
        /// <param name="guid">GUID of the component.</param>
        /// <param name="locked">True to lock (disable), false to unlock (enable).</param>
        /// <param name="redraw">True to redraw canvas immediately.</param>
        public static void SetComponentLock(Guid guid, bool locked, bool redraw = true)
        {
            Debug.WriteLine($"[ComponentManipulation] SetComponentLock: guid={guid}, locked={locked}");
            var obj = CanvasAccess.FindInstance(guid);
            Debug.WriteLine(obj != null
                ? $"[ComponentManipulation] Found object of type {obj.GetType().Name}"
                : "[ComponentManipulation] Found null object");
            if (obj is GH_Component component)
            {
                Debug.WriteLine($"[ComponentManipulation] Component.Locked={component.Locked}");
                obj.RecordUndoEvent("[SH] Set Component Lock");
                component.Locked = locked;
                Debug.WriteLine($"[ComponentManipulation] New Locked={component.Locked}");
                if (redraw)
                {
                    Instances.RedrawCanvas();
                    Debug.WriteLine("[ComponentManipulation] Canvas redrawn");
                }
            }
            else if (obj is IGH_Param param)
            {
                Debug.WriteLine($"[ComponentManipulation] IGH_Param.Locked={param.Locked}");
                obj.RecordUndoEvent("[SH] Set Component Lock");
                param.Locked = locked;
                Debug.WriteLine($"[ComponentManipulation] New Locked={param.Locked}");
                if (redraw)
                {
                    Instances.RedrawCanvas();
                    Debug.WriteLine("[ComponentManipulation] Canvas redrawn");
                }
            }
            else
            {
                Debug.WriteLine("[ComponentManipulation] Object is neither a GH_Component nor a GH_Param");
            }
        }

        /// <summary>
        /// Re-runs a canvas object through the normal lock transition: disables then
        /// re-enables it on the UI thread, letting Grasshopper expire and re-schedule
        /// the solution itself. No direct <c>ExpireSolution</c> call is made and the
        /// pulse is refused while a solution is in progress, avoiding
        /// "component expired while running" failures. Locked objects are skipped
        /// (locked components never compute). The pulse is a net-zero state change,
        /// so no undo record is produced.
        /// </summary>
        /// <param name="guid">GUID of the component or parameter to re-run.</param>
        /// <returns>The pulse outcome.</returns>
        public static PulseResult PulseObject(Guid guid)
        {
            var result = PulseResult.Unsupported;

            try
            {
                InvokeOnUiThreadAndWait(() =>
                {
                    var obj = CanvasAccess.FindInstance(guid);
                    if (obj == null)
                    {
                        result = PulseResult.NotFound;
                        return;
                    }

                    if (obj is not IGH_ActiveObject active)
                    {
                        result = PulseResult.Unsupported;
                        return;
                    }

                    var doc = obj.OnPingDocument();
                    if (doc != null && doc.SolutionDepth > 0)
                    {
                        result = PulseResult.Busy;
                        return;
                    }

                    if (active.Locked)
                    {
                        result = PulseResult.Locked;
                        return;
                    }

                    // Disable→enable transition. Re-enabling is the standard UI path
                    // that expires the object and schedules a new solution.
                    active.Locked = true;
                    active.Locked = false;

                    doc?.NewSolution(false);
                    result = PulseResult.Pulsed;
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ComponentManipulation] PulseObject failed for {guid}: {ex.Message}");
                return PulseResult.Unsupported;
            }

            return result;
        }

        /// <summary>
        /// Simulates a momentary button press on a Grasshopper Button component by setting its
        /// <c>ButtonDown</c> state to true, expiring the solution, waiting 100 ms, then setting it
        /// back to false. Records a single undo event for the operation.
        /// </summary>
        /// <param name="guid">GUID of the Button component to press.</param>
        /// <returns>True if the Button was found and pressed; otherwise false.</returns>
        public static bool ButtonClick(Guid guid)
        {
            bool found = false;
            bool clicked = false;

            InvokeOnUiThreadAndWait(() =>
            {
                var obj = CanvasAccess.FindInstance(guid);
                if (obj == null)
                {
                    Debug.WriteLine($"[ComponentManipulation] ButtonClick: object not found {guid}");
                    return;
                }

                found = true;

                // Grasshopper Button components expose a ButtonDown property.
                if (obj is GH_ButtonObject button)
                {
                    clicked = PressAndRelease(
                        guid,
                        button,
                        press: () => button.ButtonDown = true,
                        release: () => button.ButtonDown = false);
                }
                else
                {
                    Debug.WriteLine($"[ComponentManipulation] ButtonClick: object {guid} is not a Grasshopper Button");
                }
            });

            return found && clicked;
        }

        private static bool PressAndRelease(Guid guid, IGH_ActiveObject obj, Action press, Action release)
        {
            obj.RecordUndoEvent("[SH] Button Click");

            try
            {
                var doc = GhJsonGrasshopper.GetActiveDocument();

                press();
                obj.ExpireSolution(true);
                doc?.NewSolution(false);

                Task.Run(async () =>
                {
                    await Task.Delay(100).ConfigureAwait(false);

                    global::Rhino.RhinoApp.InvokeOnUiThread(() =>
                    {
                        release();
                        obj.ExpireSolution(true);
                        doc?.NewSolution(false);
                    });
                });

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ComponentManipulation] ButtonClick failed for {guid}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Sets the value of a value-bearing canvas object on the Grasshopper UI thread.
        /// Supports panels and scribbles (text), boolean toggles, number sliders
        /// (range-clamped), value lists (selection by index, name, or item value) and
        /// persistent parameters (persistent data replaced by a single item at {0}).
        /// When <paramref name="paramName"/> is supplied and the target is a component,
        /// the matching input parameter's persistent data is set instead.
        /// The document is never touched while a solution is in progress.
        /// </summary>
        /// <param name="guid">GUID of the target object.</param>
        /// <param name="value">The value to apply.</param>
        /// <param name="paramName">Optional input parameter name, nickname, or zero-based index.</param>
        /// <returns>Result code plus a human-readable detail message.</returns>
        public static (SetValueResult result, string detail) SetObjectValue(Guid guid, JToken value, string? paramName = null)
        {
            var result = SetValueResult.Failed;
            var detail = "Unknown failure";

            try
            {
                InvokeOnUiThreadAndWait(() =>
                {
                    var obj = CanvasAccess.FindInstance(guid);
                    if (obj == null)
                    {
                        result = SetValueResult.NotFound;
                        detail = $"No canvas object matches {guid}";
                        return;
                    }

                    var doc = obj.OnPingDocument();
                    if (doc != null && doc.SolutionDepth > 0)
                    {
                        result = SetValueResult.Failed;
                        detail = "A Grasshopper solution is in progress; the value was not applied.";
                        return;
                    }

                    // A named input parameter targets the component's persistent data.
                    if (!string.IsNullOrWhiteSpace(paramName))
                    {
                        if (obj is not IGH_Component comp)
                        {
                            result = SetValueResult.Unsupported;
                            detail = "The 'param' argument only applies to components.";
                            return;
                        }

                        var target = ResolveInputParam(comp, paramName);
                        if (target == null)
                        {
                            result = SetValueResult.NotFound;
                            detail = $"Component '{comp.NickName}' has no input parameter matching '{paramName}'.";
                            return;
                        }

                        result = ApplyValue(target, () => SetPersistentParamValue(target, value, out detail));
                    }
                    else if (!IsSupportedValueTarget(obj))
                    {
                        result = SetValueResult.Unsupported;
                        detail = $"Object of type '{obj.GetType().Name}' does not accept a direct value.";
                        return;
                    }
                    else
                    {
                        // Undo must capture the pre-change state, so record before applying.
                        obj.RecordUndoEvent("[SH] Set Value");
                        if (!TryApplyObjectValue(obj, value, out result, out detail))
                        {
                            return;
                        }
                    }

                    if (result == SetValueResult.Success)
                    {
                        // Schedule the downstream re-solve through the normal expiration
                        // path — always on the UI thread and only when no solve is running.
                        if (obj is IGH_ActiveObject active)
                        {
                            active.ExpireSolution(true);
                        }

                        doc?.NewSolution(false);
                        Instances.RedrawCanvas();
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ComponentManipulation] SetObjectValue failed for {guid}: {ex.Message}");
                return (SetValueResult.Failed, ex.Message);
            }

            return (result, detail);
        }

        /// <summary>
        /// Records the undo event then applies a mutation to the target object.
        /// </summary>
        private static SetValueResult ApplyValue(IGH_DocumentObject target, Func<bool> apply)
        {
            target.RecordUndoEvent("[SH] Set Value");
            return apply() ? SetValueResult.Success : SetValueResult.Failed;
        }

        /// <summary>
        /// Whether the object type can take a direct value assignment.
        /// </summary>
        private static bool IsSupportedValueTarget(IGH_DocumentObject obj)
        {
            return obj is GH_Panel or GH_Scribble or GH_BooleanToggle or GH_NumberSlider or GH_ValueList or IGH_Param;
        }

        /// <summary>
        /// Dispatches the value application to the concrete object type. Returns false
        /// when nothing was applied (result/detail are populated either way).
        /// </summary>
        private static bool TryApplyObjectValue(IGH_DocumentObject obj, JToken value, out SetValueResult result, out string detail)
        {
            switch (obj)
            {
                case GH_Panel panel:
                    panel.UserText = value?.ToString() ?? string.Empty;
                    result = SetValueResult.Success;
                    detail = $"Set panel text on '{panel.NickName}'";
                    return true;

                case GH_Scribble scribble:
                    scribble.Text = value?.ToString() ?? string.Empty;
                    result = SetValueResult.Success;
                    detail = $"Set scribble text on '{scribble.NickName}'";
                    return true;

                case GH_BooleanToggle toggle:
                    if (!TryGetBoolean(value, out var b))
                    {
                        result = SetValueResult.Failed;
                        detail = $"Value '{value}' is not a boolean.";
                        return false;
                    }

                    toggle.Value = b;
                    result = SetValueResult.Success;
                    detail = $"Set toggle '{toggle.NickName}' to {b}";
                    return true;

                case GH_NumberSlider slider:
                    if (!TryGetDecimal(value, out var d))
                    {
                        result = SetValueResult.Failed;
                        detail = $"Value '{value}' is not numeric.";
                        return false;
                    }

                    var min = slider.Slider.Minimum;
                    var max = slider.Slider.Maximum;
                    if (d < min)
                    {
                        d = min;
                    }

                    if (d > max)
                    {
                        d = max;
                    }

                    slider.SetSliderValue(d);
                    result = SetValueResult.Success;
                    detail = $"Set slider '{slider.NickName}' to {d} (clamped to [{min}, {max}])";
                    return true;

                case GH_ValueList valueList:
                    var index = FindValueListItem(valueList, value);
                    if (index < 0)
                    {
                        result = SetValueResult.NotFound;
                        detail = $"No value list item matches '{value}'.";
                        return false;
                    }

                    for (var i = 0; i < valueList.ListItems.Count; i++)
                    {
                        valueList.ListItems[i].Selected = i == index;
                    }

                    result = SetValueResult.Success;
                    detail = $"Selected item '{valueList.ListItems[index].Name}' in '{valueList.NickName}'";
                    return true;

                case IGH_Param param:
                    var applied = SetPersistentParamValue(param, value, out detail);
                    result = applied ? SetValueResult.Success : SetValueResult.Failed;
                    return applied;

                default:
                    result = SetValueResult.Unsupported;
                    detail = $"Object of type '{obj.GetType().Name}' does not accept a direct value.";
                    return false;
            }
        }

        /// <summary>
        /// Replaces a persistent parameter's data with a single item at path {0},
        /// converting the JSON value into the parameter's concrete goo type. Mirrors
        /// the reflection recipe used by the GhJSON deserializer (SetPersistentData
        /// lives on the generic <c>GH_PersistentParam&lt;T&gt;</c>, not on IGH_Param).
        /// </summary>
        private static bool SetPersistentParamValue(IGH_Param param, JToken value, out string detail)
        {
            var persistentBase = FindGenericBaseType(param.GetType(), typeof(GH_PersistentParam<>));
            if (persistentBase == null)
            {
                detail = $"Parameter '{param.NickName}' ({param.GetType().Name}) has no persistent data store.";
                return false;
            }

            var gooType = persistentBase.GetGenericArguments()[0];
            var goo = CreateGoo(gooType, value);
            if (goo == null)
            {
                detail = $"Cannot convert '{value}' to {gooType.Name}.";
                return false;
            }

            var structureType = typeof(GH_Structure<>).MakeGenericType(gooType);
            var structure = Activator.CreateInstance(structureType);
            if (structure == null)
            {
                detail = $"Cannot build data structure for {gooType.Name}.";
                return false;
            }

            structureType.GetMethod("Append", new[] { gooType, typeof(GH_Path) })
                ?.Invoke(structure, new object?[] { goo, new GH_Path(0) });

            var setMethod = param.GetType().GetMethod(
                "SetPersistentData",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[] { structureType },
                null);
            if (setMethod == null)
            {
                detail = $"Parameter '{param.NickName}' does not expose SetPersistentData.";
                return false;
            }

            setMethod.Invoke(param, new object?[] { structure });

            var wired = param.Kind == GH_ParamKind.input && param.SourceCount > 0
                ? " (note: the input is wired, so persistent data may be overridden)"
                : string.Empty;
            detail = $"Set '{param.NickName}' to '{value}' as {gooType.Name}{wired}";
            return true;
        }

        /// <summary>
        /// Converts a JSON scalar into the target goo type via its constructors.
        /// </summary>
        private static object? CreateGoo(Type gooType, JToken value)
        {
            var candidates = new List<object>();
            switch (value?.Type)
            {
                case JTokenType.Boolean:
                    candidates.Add(value.Value<bool>());
                    break;
                case JTokenType.Integer:
                    candidates.Add(value.Value<int>());
                    candidates.Add(value.Value<long>());
                    candidates.Add(value.Value<double>());
                    break;
                case JTokenType.Float:
                    candidates.Add(value.Value<double>());
                    candidates.Add(value.Value<decimal>());
                    break;
                case JTokenType.String:
                    candidates.Add(value.Value<string>()!);
                    break;
            }

            foreach (var candidate in candidates)
            {
                try
                {
                    var ctor = gooType.GetConstructor(new[] { candidate.GetType() });
                    if (ctor != null)
                    {
                        return ctor.Invoke(new[] { candidate });
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ComponentManipulation] CreateGoo ctor failed for {gooType.Name}: {ex.Message}");
                }
            }

            // String fallback: many goo types accept a text constructor.
            try
            {
                var stringCtor = gooType.GetConstructor(new[] { typeof(string) });
                if (stringCtor != null)
                {
                    return stringCtor.Invoke(new object?[] { value?.ToString() ?? string.Empty });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ComponentManipulation] CreateGoo string ctor failed for {gooType.Name}: {ex.Message}");
            }

            return null;
        }

        private static IGH_Param? ResolveInputParam(IGH_Component comp, string paramName)
        {
            if (int.TryParse(paramName, out var index)
                && index >= 0
                && index < comp.Params.Input.Count)
            {
                return comp.Params.Input[index];
            }

            return comp.Params.Input.FirstOrDefault(p =>
                string.Equals(p.NickName, paramName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(p.Name, paramName, StringComparison.OrdinalIgnoreCase));
        }

        private static int FindValueListItem(GH_ValueList valueList, JToken value)
        {
            if (value?.Type == JTokenType.Integer)
            {
                var index = value.Value<int>();
                return index >= 0 && index < valueList.ListItems.Count ? index : -1;
            }

            var text = value?.ToString();
            for (var i = 0; i < valueList.ListItems.Count; i++)
            {
                var item = valueList.ListItems[i];
                if (string.Equals(item.Name, text, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(item.Expression, text, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(item.Value?.ToString(), text, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool TryGetBoolean(JToken? value, out bool result)
        {
            switch (value?.Type)
            {
                case JTokenType.Boolean:
                    result = value.Value<bool>();
                    return true;
                case JTokenType.String:
                    return bool.TryParse(value.Value<string>(), out result);
                default:
                    result = false;
                    return false;
            }
        }

        private static bool TryGetDecimal(JToken? value, out decimal result)
        {
            try
            {
                switch (value?.Type)
                {
                    case JTokenType.Integer:
                        result = value.Value<decimal>();
                        return true;
                    case JTokenType.Float:
                        result = value.Value<decimal>();
                        return true;
                    case JTokenType.String:
                        return decimal.TryParse(value.Value<string>(), out result);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ComponentManipulation] Numeric conversion failed: {ex.Message}");
            }

            result = 0m;
            return false;
        }

        private static Type? FindGenericBaseType(Type type, Type openGenericBaseType)
        {
            var current = type;
            while (current != null)
            {
                if (current.IsGenericType && current.GetGenericTypeDefinition() == openGenericBaseType)
                {
                    return current;
                }

                current = current.BaseType;
            }

            return null;
        }

        /// <summary>
        /// Gets the bounding rectangle of a Grasshopper component or parameter on the canvas.
        /// </summary>
        /// <param name="guid">GUID of the component or parameter.</param>
        /// <returns>The bounding rectangle, or RectangleF.Empty if not found.</returns>
        public static RectangleF GetComponentBounds(Guid guid)
        {
            var obj = CanvasAccess.FindInstance(guid);
            if (obj != null)
            {
                return obj.Attributes.Bounds;
            }

            return RectangleF.Empty;
        }

        /// <summary>
        /// Queues <paramref name="action"/> on the Rhino UI thread and blocks until it
        /// returns (or 30 seconds elapse). If we are already on the UI thread the action runs inline.
        /// </summary>
        private static void InvokeOnUiThreadAndWait(Action action)
        {
            if (global::Rhino.RhinoApp.InvokeRequired == false)
            {
                action();
                return;
            }

            Exception? captured = null;
            using var done = new ManualResetEventSlim(false);

            global::Rhino.RhinoApp.InvokeOnUiThread(new Action(() =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    captured = ex;
                }
                finally
                {
                    done.Set();
                }
            }));

            if (!done.Wait(TimeSpan.FromSeconds(30)))
            {
                throw new TimeoutException(
                    "Grasshopper UI thread did not process the button click within 30 s.");
            }

            if (captured != null)
            {
                throw new InvalidOperationException(
                    "Button click on the Grasshopper UI thread failed.", captured);
            }
        }
    }
}
