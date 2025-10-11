/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

/*
 * FileContextProvider
 * Purpose: Provides live-updating context about the current Grasshopper file.
 * Currently includes:
 *  - selected-count: number of selected Grasshopper objects
 *  - component-count: total number of components in the document
 * This provider computes values on demand (no event wiring), so values are always fresh
 * whenever AIContextManager.GetCurrentContext() is called.
 */

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Special;
using SmartHopper.Infrastructure.AIContext;

namespace SmartHopper.Core.AIContext
{
    /// <summary>
    /// Context provider that supplies the current number of selected Grasshopper objects.
    /// </summary>
    public class FileContextProvider : IAIContextProvider
    {
        /// <summary>
        /// Gets the provider identifier.
        /// </summary>
        public string ProviderId => "current-file";

        /// <summary>
        /// Gets the current file context for AI queries.
        /// Returns the following keys:
        ///  - "file-name": the current document file name or "Untitled".
        ///  - "selected-count": number of selected objects in the current document.
        ///  - "object-count": total number of document objects.
        ///  - "component-count": total number of components in the current document.
        ///  - "param-count": total number of parameters in the current document.
        ///  - "scribble-count": total number of scribbles/notes in the current document.
        ///  - "group-count": total number of groups in the current document.
        /// </summary>
        /// <returns>A dictionary containing the current file context values.</returns>
        public Dictionary<string, string> GetContext()
        {
            try
            {
                var canvas = Instances.ActiveCanvas;
                var doc = canvas?.Document;
                if (doc == null)
                {
                    return new Dictionary<string, string>
                    {
                        { "file-name", "Untitled" },
                        { "selected-count", "0" },
                        { "object-count", "0" },
                        { "component-count", "0" },
                        { "param-count", "0" },
                        { "scribble-count", "0" },
                        { "group-count", "0" },
                    };
                }

                // SelectedObjects() returns all selected IGH_DocumentObject on the active document
                int selectedCount = doc.SelectedObjects()?.OfType<IGH_DocumentObject>()?.Count() ?? 0;

                // Count total number of components (IGH_Component) in the document
                int componentCount = doc.Objects?.OfType<IGH_DocumentObject>()?.OfType<IGH_Component>()?.Count() ?? 0;

                // Total objects in the document
                int objectCount = doc.Objects?.Count ?? 0;

                // Total parameters in the document
                int paramCount = doc.Objects?.OfType<IGH_DocumentObject>()?.OfType<IGH_Param>()?.Count() ?? 0;

                // Total scribbles in the document
                int scribbleCount = doc.Objects?.OfType<IGH_DocumentObject>()?.OfType<GH_Scribble>()?.Count() ?? 0;

                // Total groups in the document
                int groupCount = doc.Objects?.OfType<IGH_DocumentObject>()?.OfType<GH_Group>()?.Count() ?? 0;

                // File name (privacy friendly)
                string fileName = "Untitled";
                var path = doc.FilePath;
                if (!string.IsNullOrWhiteSpace(path))
                {
                    fileName = Path.GetFileName(path);
                }

                return new Dictionary<string, string>
                {
                    { "file-name", fileName },
                    { "selected-count", selectedCount.ToString(CultureInfo.InvariantCulture) },
                    { "object-count", objectCount.ToString(CultureInfo.InvariantCulture) },
                    { "component-count", componentCount.ToString(CultureInfo.InvariantCulture) },
                    { "param-count", paramCount.ToString(CultureInfo.InvariantCulture) },
                    { "scribble-count", scribbleCount.ToString(CultureInfo.InvariantCulture) },
                    { "group-count", groupCount.ToString(CultureInfo.InvariantCulture) },
                };
            }
            catch
            {
                // Be resilient: return zeros if anything goes wrong
                return new Dictionary<string, string>
                {
                    { "file-name", "Untitled" },
                    { "selected-count", "0" },
                    { "object-count", "0" },
                    { "component-count", "0" },
                    { "param-count", "0" },
                    { "scribble-count", "0" },
                    { "group-count", "0" },
                };
            }
        }
    }
}
