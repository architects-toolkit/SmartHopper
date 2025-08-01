/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using System;
using System.Diagnostics;
using System.Drawing;
using Grasshopper;
using Grasshopper.Kernel;

namespace SmartHopper.Core.Grasshopper.Utils
{
    /// <summary>
    /// Utilities for manipulating Grasshopper component preview state.
    /// </summary>
    public static class GHComponentUtils
    {
        /// <summary>
        /// Set preview state of a Grasshopper component by GUID.
        /// </summary>
        /// <param name="guid">GUID of the component.</param>
        /// <param name="previewOn">True to show preview, false to hide.</param>
        /// <param name="redraw">True to redraw canvas immediately.</param>
        public static void SetComponentPreview(Guid guid, bool previewOn, bool redraw = true)
        {
            Debug.WriteLine($"[GHComponentUtils] SetComponentPreview: guid={guid}, previewOn={previewOn}");
            var obj = GHCanvasUtils.FindInstance(guid);
            Debug.WriteLine(obj != null
                ? $"[GHComponentUtils] Found object of type {obj.GetType().Name}"
                : "[GHComponentUtils] Found null object");
            if (obj is GH_Component component)
            {
                Debug.WriteLine($"[GHComponentUtils] Component.IsPreviewCapable={component.IsPreviewCapable}, Hidden={component.Hidden}");
                if (component.IsPreviewCapable)
                {
                    obj.RecordUndoEvent("[SH] Set Component Preview");
                    component.Hidden = !previewOn;
                    Debug.WriteLine($"[GHComponentUtils] New Hidden={component.Hidden}");
                    if (redraw)
                    {
                        Instances.RedrawCanvas();
                        Debug.WriteLine("[GHComponentUtils] Canvas redrawn");
                    }
                }
                else
                {
                    Debug.WriteLine("[GHComponentUtils] Component is not preview capable");
                }
            }
            else if (obj is IGH_Param param)
            {
                Debug.WriteLine($"[GHComponentUtils] IGH_Param found: {param.GetType().Name}");
                // Check if the parameter implements IGH_PreviewObject for preview capabilities
                if (param is IGH_PreviewObject paramPreview)
                {
                    Debug.WriteLine($"[GHComponentUtils] IGH_Param.Hidden={paramPreview.Hidden}, IsPreviewCapable={paramPreview.IsPreviewCapable}");
                    if (paramPreview.IsPreviewCapable)
                    {
                        obj.RecordUndoEvent("[SH] Set Parameter Preview");
                        paramPreview.Hidden = !previewOn;
                        Debug.WriteLine($"[GHComponentUtils] New Hidden={paramPreview.Hidden}");
                        if (redraw)
                        {
                            Instances.RedrawCanvas();
                            Debug.WriteLine("[GHComponentUtils] Canvas redrawn");
                        }
                    }
                    else
                    {
                        Debug.WriteLine("[GHComponentUtils] IGH_Param is not preview capable");
                    }
                }
                else
                {
                    Debug.WriteLine("[GHComponentUtils] IGH_Param does not implement IGH_PreviewObject");
                }
            }
            else
            {
                Debug.WriteLine("[GHComponentUtils] Object is not previewable (not a GH_DocumentObject)");
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
            Debug.WriteLine($"[GHComponentUtils] SetComponentLock: guid={guid}, locked={locked}");
            var obj = GHCanvasUtils.FindInstance(guid);
            Debug.WriteLine(obj != null
                ? $"[GHComponentUtils] Found object of type {obj.GetType().Name}"
                : "[GHComponentUtils] Found null object");
            if (obj is GH_Component component)
            {
                Debug.WriteLine($"[GHComponentUtils] Component.Locked={component.Locked}");
                obj.RecordUndoEvent("[SH] Set Component Lock");
                component.Locked = locked;
                Debug.WriteLine($"[GHComponentUtils] New Locked={component.Locked}");
                if (redraw)
                {
                    Instances.RedrawCanvas();
                    Debug.WriteLine("[GHComponentUtils] Canvas redrawn");
                }
            }
            else if (obj is IGH_Param param)
            {
                Debug.WriteLine($"[GHComponentUtils] IGH_Param.Locked={param.Locked}");
                obj.RecordUndoEvent("[SH] Set Component Lock");
                param.Locked = locked;
                Debug.WriteLine($"[GHComponentUtils] New Locked={param.Locked}");
                if (redraw)
                {
                    Instances.RedrawCanvas();
                    Debug.WriteLine("[GHComponentUtils] Canvas redrawn");
                }
            }
            else
            {
                Debug.WriteLine("[GHComponentUtils] Object is neither a GH_Component nor a GH_Param");
            }
        }

        /// <summary>
        /// Gets the bounding rectangle of a Grasshopper component or parameter on the canvas.
        /// </summary>
        /// <param name="guid">GUID of the component or parameter.</param>
        /// <returns>The bounding rectangle, or RectangleF.Empty if not found.</returns>
        public static RectangleF GetComponentBounds(Guid guid)
        {
            var obj = GHCanvasUtils.FindInstance(guid);
            if (obj != null)
            {
                return obj.Attributes.Bounds;
            }
            return RectangleF.Empty;
        }
    }
}
