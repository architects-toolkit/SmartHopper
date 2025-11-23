/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using Newtonsoft.Json;

namespace SmartHopper.Core.Models.Components
{
    /// <summary>
    /// Represents the three code sections of a VB Script component.
    /// VB Script components have separate sections for imports, main code, and additional code.
    /// </summary>
    public class VBScriptCode
    {
        /// <summary>
        /// Gets or sets the imports section (Using statements).
        /// This section appears at the top of the script editor.
        /// Example: "Imports System\r\nImports Rhino"
        /// </summary>
        [JsonProperty("imports", NullValueHandling = NullValueHandling.Ignore)]
        public string? Imports { get; set; }

        /// <summary>
        /// Gets or sets the main RunScript code section.
        /// This is the primary user code that appears in the "Members" region.
        /// Contains the actual logic of the script.
        /// </summary>
        [JsonProperty("script", NullValueHandling = NullValueHandling.Ignore)]
        public string? Script { get; set; }

        /// <summary>
        /// Gets or sets the additional code section.
        /// This section appears after the RunScript method in the "Custom additional code" region.
        /// Used for helper methods, classes, or additional functionality.
        /// </summary>
        [JsonProperty("additional", NullValueHandling = NullValueHandling.Ignore)]
        public string? Additional { get; set; }
    }
}
