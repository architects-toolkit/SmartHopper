/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Newtonsoft.Json;

namespace SmartHopper.Core.Models.Components
{
    /// <summary>
    /// Represents the properties and metadata of a Grasshopper component.
    /// </summary>
    public class ComponentProperties
    {
        /// <summary>
        /// The name of the component.
        /// </summary>
        [JsonProperty("name")]
        [JsonRequired]
        public string Name { get; set; }

        /// <summary>
        /// The type of the component.
        /// </summary>
        [JsonProperty("type")]
        public string Type { get; set; }

        /// <summary>
        /// The object type of the component.
        /// </summary>
        [JsonProperty("objectType")]
        public string ObjectType { get; set; }

        /// <summary>
        /// The unique identifier for the component type.
        /// </summary>
        [JsonProperty("componentGuid")]
        public Guid ComponentGuid { get; set; }

        /// <summary>
        /// The unique identifier for this specific component instance.
        /// </summary>
        [JsonProperty("instanceGuid")]
        [JsonRequired]
        public Guid InstanceGuid { get; set; }

        /// <summary>
        /// A dictionary of component properties keyed by property name.
        /// </summary>
        [JsonProperty("properties")]
        public Dictionary<string, ComponentProperty> Properties { get; set; } = new Dictionary<string, ComponentProperty>();

        /// <summary>
        /// Indicates whether the component is currently selected in the Grasshopper canvas.
        /// </summary>
        [JsonProperty("selected")]
        public bool Selected { get; set; }

        /// <summary>
        /// The pivot point of the component on the canvas.
        /// </summary>
        [JsonProperty("pivot")]
        public PointF Pivot { get; set; }

        /// <summary>
        /// A list of warnings associated with the component.
        /// </summary>
        [JsonProperty("warnings")]
        public List<string> Warnings { get; set; } = new List<string>();

        /// <summary>
        /// A list of errors associated with the component.
        /// </summary>
        [JsonProperty("errors")]
        public List<string> Errors { get; set; } = new List<string>();

        /// <summary>
        /// Checks if the component has any validation errors or warnings.
        /// </summary>
        public bool HasIssues => Warnings.Any() || Errors.Any();

        /// <summary>
        /// Gets a property value by its key, with optional type conversion.
        /// </summary>
        /// <typeparam name="T">The type to convert the property value to</typeparam>
        /// <param name="key">The property key</param>
        /// <param name="defaultValue">Default value to return if property is not found or conversion fails</param>
        /// <returns>The property value converted to type T, or defaultValue if not found or conversion fails</returns>
        public T GetPropertyValue<T>(string key, T defaultValue = default)
        {
            if (Properties.TryGetValue(key, out var property) && property.Value != null)
            {
                try
                {
                    return (T)Convert.ChangeType(property.Value, typeof(T));
                }
                catch
                {
                    return defaultValue;
                }
            }
            return defaultValue;
        }

        /// <summary>
        /// Sets a property value with type information and optional human-readable format.
        /// </summary>
        /// <param name="key">The property key</param>
        /// <param name="value">The property value</param>
        /// <param name="humanReadable">Optional human-readable representation of the value</param>
        public void SetProperty(string key, object value, string humanReadable = null)
        {
            Properties[key] = new ComponentProperty
            {
                Value = value,
                Type = value?.GetType().Name ?? "null",
                HumanReadable = humanReadable ?? value?.ToString()
            };
        }
    }
}
