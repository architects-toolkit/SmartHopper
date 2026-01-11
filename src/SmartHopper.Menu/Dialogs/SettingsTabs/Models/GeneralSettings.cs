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
