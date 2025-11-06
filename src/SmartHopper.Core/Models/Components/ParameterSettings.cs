/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using Newtonsoft.Json;
using SmartHopper.Core.Serialization;

namespace SmartHopper.Core.Models.Components
{
    /// <summary>
    /// Represents settings for input or output parameters of a component.
    /// </summary>
    public class ParameterSettings
    {
        /// <summary>
        /// Gets or sets the name of the parameter.
        /// </summary>
        [JsonProperty("parameterName")]
        [JsonRequired]
        public required string ParameterName { get; set; }

        /// <summary>
        /// Gets or sets the nickname of the parameter.
        /// </summary>
        [JsonProperty("nickName", NullValueHandling = NullValueHandling.Ignore)]
        [JsonConverter(typeof(EmptyStringIgnoreConverter))]
        public string? NickName { get; set; }

        /// <summary>
        /// Gets or sets the description of the parameter.
        /// </summary>
        [JsonProperty("description", NullValueHandling = NullValueHandling.Ignore)]
        [JsonConverter(typeof(EmptyStringIgnoreConverter))]
        public string? Description { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this is the principal parameter.
        /// </summary>
        [JsonProperty("isPrincipal", NullValueHandling = NullValueHandling.Ignore)]
        public bool? IsPrincipal { get; set; }

        /// <summary>
        /// Gets or sets the data mapping mode for the parameter.
        /// </summary>
        [JsonProperty("dataMapping", NullValueHandling = NullValueHandling.Ignore)]
        [JsonConverter(typeof(EmptyStringIgnoreConverter))]
        public string? DataMapping { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the parameter is reparameterized.
        /// </summary>
        [JsonProperty("isReparameterized", NullValueHandling = NullValueHandling.Ignore)]
        public bool? IsReparameterized { get; set; }

        /// <summary>
        /// Gets or sets the parameter expression that transforms data.
        /// The presence of this property implies hasExpression=true, making that flag redundant.
        /// </summary>
        [JsonProperty("expression", NullValueHandling = NullValueHandling.Ignore)]
        [JsonConverter(typeof(EmptyStringIgnoreConverter))]
        public string? Expression { get; set; }

        /// <summary>
        /// Gets or sets the variable name for script parameters.
        /// </summary>
        [JsonProperty("variableName", NullValueHandling = NullValueHandling.Ignore)]
        [JsonConverter(typeof(EmptyStringIgnoreConverter))]
        public string? VariableName { get; set; }

        /// <summary>
        /// Gets or sets the access mode for the parameter (item, list, tree). Necessary for script components.
        /// </summary>
        [JsonProperty("access", NullValueHandling = NullValueHandling.Ignore)]
        [JsonConverter(typeof(EmptyStringIgnoreConverter))]
        public string? Access { get; set; }

        /// <summary>
        /// Gets or sets the type hint for script parameters (e.g., "int", "double", "DataTree", etc.).
        /// </summary>
        [JsonProperty("typeHint", NullValueHandling = NullValueHandling.Ignore)]
        [JsonConverter(typeof(EmptyStringIgnoreConverter))]
        public string? TypeHint { get; set; }

        /// <summary>
        /// Gets or sets additional parameter settings such as flags and modifiers.
        /// </summary>
        [JsonProperty("additionalSettings", NullValueHandling = NullValueHandling.Ignore)]
        public AdditionalParameterSettings? AdditionalSettings { get; set; }
    }
}
