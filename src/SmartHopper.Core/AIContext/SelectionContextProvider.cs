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
 * SelectionContextProvider
 * Purpose: Provides live-updating context about how many Grasshopper objects are currently selected.
 * This provider computes the selection count on demand (no event wiring), so values are always fresh
 * whenever AIContextManager.GetCurrentContext() is called.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Grasshopper;
using Grasshopper.Kernel;
using SmartHopper.Infrastructure.AIContext;

namespace SmartHopper.Core.AIContext
{
    /// <summary>
    /// Context provider that supplies the current number of selected Grasshopper objects.
    /// </summary>
    public class SelectionContextProvider : IAIContextProvider
    {
        /// <summary>
        /// Gets the provider identifier.
        /// </summary>
        public string ProviderId => "selection";

        /// <summary>
        /// Gets the current selection context for AI queries.
        /// Returns a single key "selected-count" with the number of selected objects.
        /// </summary>
        /// <returns>A dictionary containing the current selection count.</returns>
        public Dictionary<string, string> GetContext()
        {
            try
            {
                var canvas = Instances.ActiveCanvas;
                var doc = canvas?.Document;
                if (doc == null)
                {
                    return new Dictionary<string, string> { { "selected-count", "0" } };
                }

                // SelectedObjects() returns all selected IGH_DocumentObject on the active document
                int selectedCount = doc.SelectedObjects()?.OfType<IGH_DocumentObject>()?.Count() ?? 0;

                return new Dictionary<string, string>
                {
                    { "selected-count", selectedCount.ToString() },
                };
            }
            catch
            {
                // Be resilient: return zero if anything goes wrong
                return new Dictionary<string, string> { { "selected-count", "0" } };
            }
        }
    }
}
