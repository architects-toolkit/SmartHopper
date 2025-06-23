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

namespace SmartHopper.Config.Models
{
    public class SettingDescriptor
    {
        /// <summary>
        /// The name of the setting (used as key in configuration)
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The data type of the setting
        /// </summary>
        public Type Type { get; set; } = typeof(string);

        /// <summary>
        /// The default value for the setting
        /// </summary>
        public object DefaultValue { get; set; }

        /// <summary>
        /// Whether the setting contains sensitive data that should be masked
        /// </summary>
        public bool IsSecret { get; set; }

        /// <summary>
        /// Display name shown in the UI
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// Description shown in the UI
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// For string settings, a list of allowed values that will be displayed as a dropdown
        /// </summary>
        public IEnumerable<object> AllowedValues { get; set; }

        /// <summary>
        /// Optional UI control parameters (e.g., slider vs stepper for numeric types)
        /// </summary>
        public SettingDescriptorControl ControlParams { get; set; }
    }
}
