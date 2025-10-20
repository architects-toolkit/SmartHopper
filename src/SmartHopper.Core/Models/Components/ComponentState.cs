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

namespace SmartHopper.Core.Models.Components
{
    /// <summary>
    /// Represents UI-specific state for components such as colors, multiline settings, and component-specific configurations.
    /// </summary>
    public class ComponentState
    {
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
        /// Gets or sets slider-specific configuration (for number sliders).
        /// </summary>
        [JsonProperty("slider", NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<string, object>? Slider { get; set; }

        /// <summary>
        /// Gets or sets script-specific configuration (for script components).
        /// </summary>
        [JsonProperty("script", NullValueHandling = NullValueHandling.Ignore)]
        public string? Script { get; set; }

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
        /// Gets or sets the current value for components with state (sliders, value lists, etc.).
        /// </summary>
        [JsonProperty("currentValue", NullValueHandling = NullValueHandling.Ignore)]
        public string? CurrentValue { get; set; }

        /// <summary>
        /// Gets or sets list items for value list components.
        /// </summary>
        [JsonProperty("listItems", NullValueHandling = NullValueHandling.Ignore)]
        public List<Dictionary<string, object>>? ListItems { get; set; }

        /// <summary>
        /// Gets or sets the list mode for value list components.
        /// </summary>
        [JsonProperty("listMode", NullValueHandling = NullValueHandling.Ignore)]
        public string? ListMode { get; set; }

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
        /// Gets or sets the universal value property for the component.
        /// </summary>
        [JsonProperty("value", NullValueHandling = NullValueHandling.Ignore)]
        public object? Value { get; set; }

        /// <summary>
        /// Gets or sets additional component-specific properties.
        /// </summary>
        [JsonProperty("additionalProperties", NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<string, object>? AdditionalProperties { get; set; }
    }
}
