/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

namespace SmartHopper.Menu.Dialogs.SettingsTabs.Models
{
    /// <summary>
    /// Model for general SmartHopper settings
    /// </summary>
    public class GeneralSettings
    {
        /// <summary>
        /// Gets or sets the default AI provider name
        /// </summary>
        public string DefaultAIProvider { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the debounce time in milliseconds for input stabilization
        /// </summary>
        public int DebounceTime { get; set; } = 1500;
    }
}
