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
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using GhJSON.Grasshopper;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Special;
using Rhino;

namespace SmartHopper.Core.Grasshopper.Utils.Canvas
{
    /// <summary>
    /// Utilities for manipulating Grasshopper component preview state.
    /// </summary>
    public static class ComponentManipulation
    {
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
