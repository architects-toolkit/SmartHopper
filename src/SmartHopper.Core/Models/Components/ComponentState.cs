/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using System.Collections.Generic;
using Newtonsoft.Json;
using SmartHopper.Core.Serialization;

namespace SmartHopper.Core.Models.Components
{
    /// <summary>
    /// Represents UI-specific state for components.
    /// The Value property stores the primary value for all component types.
    /// </summary>
    public class ComponentState
    {
        /// <summary>
        /// Gets or sets the universal value property for the component.
        /// This stores the primary value for all component types:
        /// - Number Slider: "5<2,10.000>" (value with range; highest decimal count determines precision)
        /// - Panel/Scribble: "Hello World" (plain text)
        /// - Value List: [{"Name":"A","Expression":"0"}] (array of items)
        /// - Script: "import math\nprint(x)" (script code)
        /// - Multidimensional Slider: "1.0,2.0,3.0" (coordinate values)
        /// </summary>
        [JsonProperty("value", NullValueHandling = NullValueHandling.Ignore)]
        public object? Value { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the component is locked.
        /// </summary>
        [JsonProperty("locked", NullValueHandling = NullValueHandling.Ignore)]
        public bool? Locked { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the component preview is hidden.
        /// </summary>
        [JsonProperty("hidden", NullValueHandling = NullValueHandling.Ignore)]
        public bool? Hidden { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the component is enabled (unlocked).
        /// </summary>
        [JsonProperty("enabled", NullValueHandling = NullValueHandling.Ignore)]
        public bool? Enabled { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether preview is enabled.
        /// </summary>
        [JsonProperty("preview", NullValueHandling = NullValueHandling.Ignore)]
        public bool? Preview { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether multiline mode is enabled (for panels, text components).
        /// </summary>
        [JsonProperty("multiline", NullValueHandling = NullValueHandling.Ignore)]
        public bool? Multiline { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether text wrapping is enabled.
        /// </summary>
        [JsonProperty("wrap", NullValueHandling = NullValueHandling.Ignore)]
        public bool? Wrap { get; set; }

        /// <summary>
        /// Gets or sets the component color as RGBA values.
        /// </summary>
        [JsonProperty("color", NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<string, int>? Color { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether inputs should be marshalled (for script components).
        /// </summary>
        [JsonProperty("marshInputs", NullValueHandling = NullValueHandling.Ignore)]
        public bool? MarshInputs { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether outputs should be marshalled (for script components).
        /// </summary>
        [JsonProperty("marshOutputs", NullValueHandling = NullValueHandling.Ignore)]
        public bool? MarshOutputs { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the standard output/error parameter ("out") should be shown (for script components).
        /// This corresponds to the contextual menu option "Standard Output/Error Parameter ("out")" in Grasshopper.
        /// </summary>
        [JsonProperty("showStandardOutput", NullValueHandling = NullValueHandling.Ignore)]
        public bool? ShowStandardOutput { get; set; }

        /// <summary>
        /// Gets or sets the list mode for value list components (e.g., "DropDown", "CheckList").
        /// </summary>
        [JsonProperty("listMode", NullValueHandling = NullValueHandling.Ignore)]
        [JsonConverter(typeof(EmptyStringIgnoreConverter))]
        public string? ListMode { get; set; }

        /// <summary>
        /// Gets or sets the indices of selected items in a value list.
        /// </summary>
        [JsonProperty("selectedIndices", NullValueHandling = NullValueHandling.Ignore)]
        public List<int>? SelectedIndices { get; set; }

        /// <summary>
        /// Gets or sets font configuration for text components.
        /// </summary>
        [JsonProperty("font", NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<string, object>? Font { get; set; }

        /// <summary>
        /// Gets or sets corner points for scribble components.
        /// </summary>
        [JsonProperty("corners", NullValueHandling = NullValueHandling.Ignore)]
        public List<Dictionary<string, float>>? Corners { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to draw indices (for panels).
        /// </summary>
        [JsonProperty("drawIndices", NullValueHandling = NullValueHandling.Ignore)]
        public bool? DrawIndices { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to draw paths (for panels).
        /// </summary>
        [JsonProperty("drawPaths", NullValueHandling = NullValueHandling.Ignore)]
        public bool? DrawPaths { get; set; }

        /// <summary>
        /// Gets or sets the alignment for panel text.
        /// </summary>
        [JsonProperty("alignment", NullValueHandling = NullValueHandling.Ignore)]
        public int? Alignment { get; set; }

        /// <summary>
        /// Gets or sets the bounds (size) for panels and other UI components.
        /// Format: {"width": 100, "height": 50}
        /// </summary>
        [JsonProperty("bounds", NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<string, float>? Bounds { get; set; }

        /// <summary>
        /// Gets or sets the rounding mode for number sliders.
        /// Values: "Round", "None", "Even", "Odd"
        /// </summary>
        [JsonProperty("rounding", NullValueHandling = NullValueHandling.Ignore)]
        [JsonConverter(typeof(EmptyStringIgnoreConverter))]
        public string? Rounding { get; set; }

        /// <summary>
        /// Gets or sets additional component-specific properties that don't fit into standard fields.
        /// </summary>
        [JsonProperty("additionalProperties", NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<string, object>? AdditionalProperties { get; set; }
    }
}
