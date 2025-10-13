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
using System.Linq;
using Grasshopper;
using Grasshopper.Kernel;

namespace SmartHopper.Core.Grasshopper.Utils
{
    /// <summary>
    /// Utilities for connecting Grasshopper components.
    /// </summary>
    public static class GHConnectionUtils
    {
        /// <summary>
        /// Connects two components by creating a wire between an output and input parameter.
        /// </summary>
        /// <param name="sourceGuid">GUID of the source component (output side).</param>
        /// <param name="targetGuid">GUID of the target component (input side).</param>
        /// <param name="sourceParamName">Name or nickname of the output parameter. If null, uses first output.</param>
        /// <param name="targetParamName">Name or nickname of the input parameter. If null, uses first input.</param>
        /// <param name="redraw">True to redraw canvas and trigger solution recalculation.</param>
        /// <returns>True if connection was successful, false otherwise.</returns>
        public static bool ConnectComponents(
            Guid sourceGuid,
            Guid targetGuid,
            string sourceParamName = null,
            string targetParamName = null,
            bool redraw = true)
        {
            var doc = GHCanvasUtils.GetCurrentCanvas();
            if (doc == null)
            {
                Debug.WriteLine("[GHConnectionUtils] No active Grasshopper document found.");
                return false;
            }

            // Find source and target objects
            var sourceObj = GHCanvasUtils.FindInstance(sourceGuid);
            var targetObj = GHCanvasUtils.FindInstance(targetGuid);

            if (sourceObj == null || targetObj == null)
            {
                Debug.WriteLine($"[GHConnectionUtils] Component not found. Source: {sourceObj != null}, Target: {targetObj != null}");
                return false;
            }

            // Get parameters
            var sourceParam = GetOutputParameter(sourceObj, sourceParamName);
            var targetParam = GetInputParameter(targetObj, targetParamName);

            if (sourceParam == null || targetParam == null)
            {
                Debug.WriteLine($"[GHConnectionUtils] Parameter not found. Source param: {sourceParam != null}, Target param: {targetParam != null}");
                return false;
            }

            // Check if connection already exists
            if (targetParam.Sources.Contains(sourceParam))
            {
                Debug.WriteLine($"[GHConnectionUtils] Connection already exists: {sourceParam.NickName} → {targetParam.NickName}");
                return true; // Already connected, consider it success
            }

            // Create the connection
            try
            {
                targetParam.AddSource(sourceParam);
                Debug.WriteLine($"[GHConnectionUtils] Connected {sourceParam.NickName} ({sourceGuid}) → {targetParam.NickName} ({targetGuid})");

                if (redraw)
                {
                    doc.NewSolution(false);
                    Instances.RedrawCanvas();
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GHConnectionUtils] Connection failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets an output parameter from a document object.
        /// </summary>
        private static IGH_Param GetOutputParameter(IGH_DocumentObject obj, string paramName)
        {
            if (obj is IGH_Component comp)
            {
                if (string.IsNullOrEmpty(paramName))
                {
                    return comp.Params.Output.FirstOrDefault();
                }

                return comp.Params.Output.FirstOrDefault(p =>
                    p.Name.Equals(paramName, StringComparison.OrdinalIgnoreCase) ||
                    p.NickName.Equals(paramName, StringComparison.OrdinalIgnoreCase));
            }
            else if (obj is IGH_Param param)
            {
                return param;
            }

            return null;
        }

        /// <summary>
        /// Gets an input parameter from a document object.
        /// </summary>
        private static IGH_Param GetInputParameter(IGH_DocumentObject obj, string paramName)
        {
            if (obj is IGH_Component comp)
            {
                if (string.IsNullOrEmpty(paramName))
                {
                    return comp.Params.Input.FirstOrDefault();
                }

                return comp.Params.Input.FirstOrDefault(p =>
                    p.Name.Equals(paramName, StringComparison.OrdinalIgnoreCase) ||
                    p.NickName.Equals(paramName, StringComparison.OrdinalIgnoreCase));
            }
            else if (obj is IGH_Param param)
            {
                return param;
            }

            return null;
        }
    }
}
