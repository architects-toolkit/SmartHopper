/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024 Marc Roca Musach
 * 
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

/*
 * Defines the contract for managing persistent component data.
 * This service handles saving and loading volatile data that needs
 * to persist between Grasshopper sessions.
 */

using GH_IO.Serialization;
using System;
using System.Collections.Generic;

namespace SmartHopper.Core.ComponentBase
{
    /// <summary>
    /// Interface for managing persistent component data. Implement this to provide
    /// data persistence capabilities between Grasshopper sessions.
    /// </summary>
    public interface IPersistentDataManager
    {
        /// <summary>
        /// Writes the component's volatile data to persistent storage.
        /// Called when the document is being saved.
        /// </summary>
        /// <param name="writer">The writer to serialize data with</param>
        /// <remarks>
        /// Implementation should:
        /// - Handle serialization of complex objects
        /// - Manage versioning of stored data
        /// - Handle missing or null data gracefully
        /// </remarks>
        void Write(GH_IWriter writer);

        /// <summary>
        /// Reads the component's persistent data from storage.
        /// Called when the document is being loaded.
        /// </summary>
        /// <param name="reader">The reader to deserialize data with</param>
        /// <remarks>
        /// Implementation should:
        /// - Handle deserialization errors
        /// - Support backward compatibility
        /// - Initialize default values if data is missing
        /// </remarks>
        void Read(GH_IReader reader);

        /// <summary>
        /// Gets whether there is persistent data available.
        /// </summary>
        bool HasPersistentData { get; }

        /// <summary>
        /// Clears all persistent data.
        /// </summary>
        void ClearPersistentData();

        /// <summary>
        /// Gets the types of data that can be persisted.
        /// </summary>
        IEnumerable<Type> PersistentDataTypes { get; }
    }
}
