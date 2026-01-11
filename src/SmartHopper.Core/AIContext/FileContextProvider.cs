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
 * along with this library; if not, write to the Free Software Foundation,
 * Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301, USA.
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
using System.Threading;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Special;
using Rhino;
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
                string fileName = "Untitled";
                int selectedCount = 0;
                int selectedComponentCount = 0;
                int selectedParamCount = 0;
                string selectedObjects = string.Empty;
                int componentCount = 0;
                int objectCount = 0;
                int paramCount = 0;
                int scribbleCount = 0;
                int groupCount = 0;

                // Use ManualResetEventSlim to ensure UI thread work completes before returning
                using (var uiThreadComplete = new ManualResetEventSlim(false))
                {
                    RhinoApp.InvokeOnUiThread(
                        (Action)(() =>
                        {
                            try
                            {
                                var canvas = Instances.ActiveCanvas;
                                var doc = canvas?.Document;
                                if (doc == null)
                                {
                                    return;
                                }

                                // Count total number of objects/components/params in the document
                                objectCount = doc.Objects?.Count ?? 0;
                                componentCount = doc.Objects?.OfType<IGH_DocumentObject>()?.OfType<IGH_Component>()?.Count() ?? 0;
                                paramCount = doc.Objects?.OfType<IGH_DocumentObject>()?.OfType<IGH_Param>()?.Count() ?? 0;
                                scribbleCount = doc.Objects?.OfType<IGH_DocumentObject>()?.OfType<GH_Scribble>()?.Count() ?? 0;
                                groupCount = doc.Objects?.OfType<IGH_DocumentObject>()?.OfType<GH_Group>()?.Count() ?? 0;

                                // File name (privacy friendly)
                                var path = doc.FilePath;
                                if (!string.IsNullOrWhiteSpace(path))
                                {
                                    fileName = Path.GetFileName(path);
                                }

                                // Selected objects.
                                // NOTE: When this context is queried from background worker threads, selection state can be stale.
                                // To keep it reliable, we always read it on the Rhino UI thread.
                                var selected = doc.SelectedObjects()?.OfType<IGH_DocumentObject>()?.ToList();
                                if (selected == null || selected.Count == 0)
                                {
                                    selected = doc.Objects
                                        ?.OfType<IGH_DocumentObject>()
                                        ?.Where(o => o?.Attributes?.Selected == true)
                                        ?.ToList();
                                }

                                selectedCount = selected?.Count ?? 0;
                                selectedComponentCount = selected?.OfType<IGH_Component>()?.Count() ?? 0;
                                selectedParamCount = selected?.OfType<IGH_Param>()?.Count() ?? 0;

                                if (selected != null && selected.Count > 0)
                                {
                                    selectedObjects = string.Join(
                                        "; ",
                                        selected
                                            .Take(10)
                                            .Select(o => $"{(string.IsNullOrWhiteSpace(o.NickName) ? o.Name : o.NickName)} ({o.GetType().Name})"));
                                }
                            }
                            finally
                            {
                                uiThreadComplete.Set();
                            }
                        }));

                    // Wait for UI thread to complete (timeout after 5 seconds to avoid deadlock)
                    uiThreadComplete.Wait(TimeSpan.FromSeconds(5));
                }

                return new Dictionary<string, string>
                {
                    { "file-name", fileName },
                    { "selected-count", selectedCount.ToString(CultureInfo.InvariantCulture) },
                    { "selected-component-count", selectedComponentCount.ToString(CultureInfo.InvariantCulture) },
                    { "selected-param-count", selectedParamCount.ToString(CultureInfo.InvariantCulture) },
                    { "selected-objects", selectedObjects },
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
                    { "selected-component-count", "0" },
                    { "selected-param-count", "0" },
                    { "selected-objects", string.Empty },
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
