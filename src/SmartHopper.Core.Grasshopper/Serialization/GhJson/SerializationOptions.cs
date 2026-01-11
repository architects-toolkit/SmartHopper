/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using SmartHopper.Core.Grasshopper.Utils.Serialization;
using SmartHopper.Core.Grasshopper.Utils.Serialization.PropertyFilters;

namespace SmartHopper.Core.Grasshopper.Serialization.GhJson
{
    /// <summary>
    /// Configuration options for GhJSON serialization (canvas to JSON).
    /// Controls what information is included in the serialized output.
    /// </summary>
    public class SerializationOptions
    {
        /// <summary>
        /// Serialization context that determines which properties to include.
        /// </summary>
        public SerializationContext Context { get; set; } = SerializationContext.Standard;

        /// <summary>
        /// Whether to include document metadata (creation date, Grasshopper version, etc.).
        /// </summary>
        public bool IncludeMetadata { get; set; } = false;

        /// <summary>
        /// Whether to include group information.
        /// </summary>
        public bool IncludeGroups { get; set; } = true;

        /// <summary>
        /// Whether to include connections between components.
        /// </summary>
        public bool IncludeConnections { get; set; } = true;

        /// <summary>
        /// Whether to use compact representation (integer IDs instead of GUIDs).
        /// </summary>
        public bool UseCompactIds { get; set; } = true;

        /// <summary>
        /// Custom property manager for advanced filtering.
        /// If null, a default manager will be created based on Context.
        /// </summary>
        public PropertyManagerV2 PropertyManager { get; set; } = null;

        /// <summary>
        /// Whether to extract type hints from script components.
        /// </summary>
        public bool ExtractScriptTypeHints { get; set; } = true;

        /// <summary>
        /// Whether to extract component state (enabled/locked/hidden).
        /// </summary>
        public bool ExtractComponentState { get; set; } = true;

        /// <summary>
        /// Whether to extract parameter expressions.
        /// </summary>
        public bool ExtractParameterExpressions { get; set; } = true;

        /// <summary>
        /// Creates default options for Standard format serialization.
        /// Balanced, clean structure for AI processing with all essential data.
        /// </summary>
        public static SerializationOptions Standard => new SerializationOptions
        {
            Context = SerializationContext.Standard,
            IncludeMetadata = true,
            IncludeGroups = true,
            IncludeConnections = true,
            UseCompactIds = true,
            ExtractScriptTypeHints = true,
            ExtractComponentState = true,
            ExtractParameterExpressions = true
        };

        /// <summary>
        /// Creates default options for Optimized format serialization.
        /// Same as Standard, but suppresses bulky data fields (PersistentData).
        /// Intended for AI context where values are not needed and token usage matters.
        /// </summary>
        public static SerializationOptions Optimized => new SerializationOptions
        {
            Context = SerializationContext.Optimized,
            IncludeMetadata = true,
            IncludeGroups = true,
            IncludeConnections = true,
            UseCompactIds = true,
            ExtractScriptTypeHints = true,
            ExtractComponentState = true,
            ExtractParameterExpressions = true
        };

        /// <summary>
        /// Creates options for Lite format serialization.
        /// Compressed variant optimized for minimal token usage.
        /// Excludes GUIDs, UI state, and component-specific properties.
        /// </summary>
        public static SerializationOptions Lite => new SerializationOptions
        {
            Context = SerializationContext.Lite,
            IncludeMetadata = false,
            IncludeGroups = false,
            IncludeConnections = true,
            UseCompactIds = true,
            ExtractScriptTypeHints = false,
            ExtractComponentState = false,
            ExtractParameterExpressions = false
        };
    }
}
