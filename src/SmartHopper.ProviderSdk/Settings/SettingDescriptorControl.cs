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
 * along with this library; if not, see <https://www.gnu.org/licenses/lgpl-3.0.html>.
 */

namespace SmartHopper.ProviderSdk.Settings
{
    /// <summary>
    /// Base class for UI control parameters for a setting descriptor.
    /// </summary>
    public abstract class SettingDescriptorControl
    {
        // Marker base class for per-type control parameters
    }

    /// <summary>
    /// Control parameters for int settings: choose between stepper or slider, with min, max, step.
    /// </summary>
    public class NumericSettingDescriptorControl : SettingDescriptorControl
    {
        public bool UseSlider { get; set; }

        public int Min { get; set; }

        public int Max { get; set; } = 100;

        public int Step { get; set; } = 1;
    }

    /// <summary>
    /// Control parameters that render a button instead of the default editor for a setting.
    /// When the user clicks the button, the provider settings are immediately validated
    /// with this setting's value set to true, allowing one-shot actions such as OAuth login.
    /// </summary>
    public class ButtonSettingDescriptorControl : SettingDescriptorControl
    {
        /// <summary>
        /// Optional text shown on the button. When empty, the descriptor display name is used.
        /// </summary>
        public string Text { get; set; } = string.Empty;
    }

    /// <summary>
    /// Control parameters that render the setting value as a read-only status label.
    /// The label colour is driven by a companion level setting named by
    /// <see cref="LevelSettingName"/>: "ok" (green), "busy" (orange), "error" (red)
    /// and any other or missing value (neutral grey).
    /// </summary>
    public class StatusSettingDescriptorControl : SettingDescriptorControl
    {
        /// <summary>
        /// Name of the sibling setting holding the semantic level
        /// ("ok", "busy", "error", "none") used to colour the label.
        /// </summary>
        public string LevelSettingName { get; set; } = string.Empty;
    }

    /// <summary>
    /// Control parameters that render a hyperlink which opens <see cref="Url"/> in the
    /// user's browser. Link clicks do not write settings and never trigger validation.
    /// </summary>
    public class LinkSettingDescriptorControl : SettingDescriptorControl
    {
        /// <summary>The absolute http(s) URL opened when the link is clicked.</summary>
        public string Url { get; set; } = string.Empty;

        /// <summary>Optional link text. When empty, <see cref="Url"/> is displayed.</summary>
        public string Text { get; set; } = string.Empty;
    }
}
