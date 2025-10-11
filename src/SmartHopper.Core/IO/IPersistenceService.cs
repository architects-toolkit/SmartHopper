/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

// Purpose: Define the contract for a safe, versioned persistence service for component outputs.
// Centralizes read/write logic to avoid GH internal type lookups on read and ensure forward compatibility.

using System;
using System.Collections.Generic;
using GH_IO.Serialization;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

namespace SmartHopper.Core.IO
{
    /// <summary>
    /// Defines a versioned persistence service for Grasshopper component outputs.
    /// </summary>
    public interface IPersistenceService
    {
        /// <summary>
        /// Writes outputs using the current safe persistence version (v2).
        /// Expects outputs keyed by output parameter GUID and structured as GH trees of IGH_Goo.
        /// </summary>
        /// <param name="writer">The Grasshopper writer.</param>
        /// <param name="component">The component owning the outputs.</param>
        /// <param name="outputsByGuid">Outputs keyed by parameter GUID.</param>
        /// <returns>True on success.</returns>
        bool WriteOutputsV2(GH_IWriter writer, IGH_Component component, IDictionary<Guid, GH_Structure<IGH_Goo>> outputsByGuid);

        /// <summary>
        /// Reads outputs using the current safe persistence version (v2). Never throws.
        /// Returns decoded trees keyed by output parameter GUID.
        /// </summary>
        /// <param name="reader">The Grasshopper reader.</param>
        /// <param name="component">The component to which outputs belong (used for logging/context).</param>
        /// <returns>Dictionary of decoded output trees keyed by parameter GUID.</returns>
        IDictionary<Guid, GH_Structure<IGH_Goo>> ReadOutputsV2(GH_IReader reader, IGH_Component component);
    }
}
