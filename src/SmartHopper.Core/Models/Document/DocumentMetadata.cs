/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using System.Collections.Generic;
using Newtonsoft.Json;
using SmartHopper.Core.Serialization;

namespace SmartHopper.Core.Models.Document
{
    /// <summary>
    /// Represents metadata for a Grasshopper document.
    /// </summary>
    public class DocumentMetadata
    {
        /// <summary>
        /// Gets or sets the description of the document.
        /// </summary>
        [JsonProperty("description", NullValueHandling = NullValueHandling.Ignore)]
        [JsonConverter(typeof(EmptyStringIgnoreConverter))]
        public string Description { get; set; }

        /// <summary>
        /// Gets or sets the document version.
        /// </summary>
        [JsonProperty("version", NullValueHandling = NullValueHandling.Ignore)]
        [JsonConverter(typeof(EmptyStringIgnoreConverter))]
        public string Version { get; set; }

        /// <summary>
        /// Gets or sets the creation timestamp in ISO 8601 format.
        /// </summary>
        [JsonProperty("created", NullValueHandling = NullValueHandling.Ignore)]
        [JsonConverter(typeof(EmptyStringIgnoreConverter))]
        public string Created { get; set; }

        /// <summary>
        /// Gets or sets the last modification timestamp in ISO 8601 format.
        /// </summary>
        [JsonProperty("modified", NullValueHandling = NullValueHandling.Ignore)]
        [JsonConverter(typeof(EmptyStringIgnoreConverter))]
        public string Modified { get; set; }

        /// <summary>
        /// Gets or sets the author of the document.
        /// </summary>
        [JsonProperty("author", NullValueHandling = NullValueHandling.Ignore)]
        [JsonConverter(typeof(EmptyStringIgnoreConverter))]
        public string Author { get; set; }

        /// <summary>
        /// Gets or sets the Rhino version.
        /// </summary>
        [JsonProperty("rhinoVersion", NullValueHandling = NullValueHandling.Ignore)]
        [JsonConverter(typeof(EmptyStringIgnoreConverter))]
        public string RhinoVersion { get; set; }

        /// <summary>
        /// Gets or sets the Grasshopper version.
        /// </summary>
        [JsonProperty("grasshopperVersion", NullValueHandling = NullValueHandling.Ignore)]
        [JsonConverter(typeof(EmptyStringIgnoreConverter))]
        public string GrasshopperVersion { get; set; }

        /// <summary>
        /// Gets or sets the list of required plugin dependencies.
        /// </summary>
        [JsonProperty("dependencies", NullValueHandling = NullValueHandling.Ignore)]
        public List<string> Dependencies { get; set; }

        /// <summary>
        /// Gets or sets the number of components in the document.
        /// </summary>
        [JsonProperty("componentCount", NullValueHandling = NullValueHandling.Ignore)]
        public int? ComponentCount { get; set; }

        /// <summary>
        /// Gets or sets the number of parameters in the document.
        /// </summary>
        [JsonProperty("parameterCount", NullValueHandling = NullValueHandling.Ignore)]
        public int? ParameterCount { get; set; }

        /// <summary>
        /// Gets or sets the SmartHopper plugin version.
        /// </summary>
        [JsonProperty("pluginVersion", NullValueHandling = NullValueHandling.Ignore)]
        [JsonConverter(typeof(EmptyStringIgnoreConverter))]
        public string PluginVersion { get; set; }
    }
}
