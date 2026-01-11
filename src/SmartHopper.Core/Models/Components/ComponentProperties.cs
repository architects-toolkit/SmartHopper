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

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using SmartHopper.Core.Models.Serialization;
using SmartHopper.Core.Serialization;

namespace SmartHopper.Core.Models.Components
{
    /// <summary>
    /// Represents the properties and metadata of a Grasshopper component.
    /// </summary>
    public class ComponentProperties
    {
        /// <summary>
        /// Gets or sets the name of the component.
        /// </summary>
        [JsonProperty("name")]
        [JsonRequired]
        public required string Name { get; set; }

        /// <summary>
        /// Gets or sets the component library/category.
        /// </summary>
        [JsonProperty("library", NullValueHandling = NullValueHandling.Ignore)]
        [JsonConverter(typeof(EmptyStringIgnoreConverter))]
        public string? Library { get; set; }

        /// <summary>
        /// Gets or sets the component type/subcategory.
        /// </summary>
        [JsonProperty("type", NullValueHandling = NullValueHandling.Ignore)]
        [JsonConverter(typeof(EmptyStringIgnoreConverter))]
        public string? Type { get; set; }

        /// <summary>
        /// Gets or sets the nickname of the component.
        /// </summary>
        [JsonProperty("nickName", NullValueHandling = NullValueHandling.Ignore)]
        [JsonConverter(typeof(EmptyStringIgnoreConverter))]
        public string? NickName { get; set; }

        /// <summary>
        /// Gets or sets the unique identifier for the component type.
        /// </summary>
        [JsonProperty("componentGuid")]
        public Guid ComponentGuid { get; set; }

        /// <summary>
        /// Gets or sets the unique identifier for this specific component instance.
        /// </summary>
        [JsonProperty("instanceGuid")]
        [JsonRequired]
        public Guid InstanceGuid { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether indicates whether the component is currently selected in the Grasshopper canvas.
        /// </summary>
        [JsonProperty("selected", NullValueHandling = NullValueHandling.Ignore)]
        public bool? Selected { get; set; }

        /// <summary>
        /// Gets or sets the pivot point of the component on the canvas.
        /// Uses compact string format "X,Y" instead of object format for optimization.
        /// </summary>
        [JsonProperty("pivot")]
        public CompactPosition Pivot { get; set; }

        /// <summary>
        /// Gets or sets the integer ID for the component (used for group references and connections).
        /// </summary>
        [JsonProperty("id", NullValueHandling = NullValueHandling.Ignore)]
        public int? Id { get; set; }

        /// <summary>
        /// Gets or sets simple key-value pairs for basic component properties.
        /// </summary>
        [JsonProperty("params", NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<string, object>? Params { get; set; }

        /// <summary>
        /// Gets or sets the input parameter settings array.
        /// </summary>
        [JsonProperty("inputSettings", NullValueHandling = NullValueHandling.Ignore)]
        public List<ParameterSettings>? InputSettings { get; set; }

        /// <summary>
        /// Gets or sets the output parameter settings array.
        /// </summary>
        [JsonProperty("outputSettings", NullValueHandling = NullValueHandling.Ignore)]
        public List<ParameterSettings>? OutputSettings { get; set; }

        /// <summary>
        /// Gets or sets the component-specific UI state.
        /// </summary>
        [JsonProperty("componentState", NullValueHandling = NullValueHandling.Ignore)]
        public ComponentState? ComponentState { get; set; }

        /// <summary>
        /// Gets or sets a list of warnings associated with the component.
        /// </summary>
        [JsonProperty("warnings", NullValueHandling = NullValueHandling.Ignore)]
        public List<string>? Warnings { get; set; }

        /// <summary>
        /// Gets or sets a list of errors associated with the component.
        /// </summary>
        [JsonProperty("errors", NullValueHandling = NullValueHandling.Ignore)]
        public List<string>? Errors { get; set; }

        /// <summary>
        /// Gets or sets schema-specific properties (legacy format).
        /// </summary>
        [JsonProperty("properties", NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<string, object>? SchemaProperties { get; set; }

        /// <summary>
        /// Gets a value indicating whether checks if the component has any validation errors or warnings.
        /// </summary>
        [JsonIgnore]
        public bool HasIssues => this.Warnings?.Any() == true || this.Errors?.Any() == true;

    }
}
