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

namespace SmartHopper.Menu.Dialogs.SettingsTabs.Models
{
    /// <summary>
    /// Model for trusted AI providers settings
    /// </summary>
    public class TrustedProvidersSettings : Dictionary<string, bool>
    {
        /// <summary>
        /// Initializes a new instance of the TrustedProvidersSettings class
        /// </summary>
        public TrustedProvidersSettings() : base()
        {
        }

        /// <summary>
        /// Initializes a new instance of the TrustedProvidersSettings class with initial values
        /// </summary>
        /// <param name="dictionary">Initial trusted providers dictionary</param>
        public TrustedProvidersSettings(IDictionary<string, bool> dictionary) : base(dictionary)
        {
        }
    }
}
