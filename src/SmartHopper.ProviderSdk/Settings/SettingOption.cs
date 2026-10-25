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
    /// Associates a setting value with the text shown for it in a selection control.
    /// </summary>
    public sealed class SettingOption
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SettingOption"/> class.
        /// </summary>
        /// <param name="value">The value persisted by the setting.</param>
        /// <param name="label">The value displayed to the user.</param>
        public SettingOption(object value, string label)
        {
            this.Value = value;
            this.Label = label;
        }

        /// <summary>
        /// Gets the value persisted by the setting.
        /// </summary>
        public object Value { get; }

        /// <summary>
        /// Gets the value displayed to the user.
        /// </summary>
        public string Label { get; }

        /// <inheritdoc/>
        public override string ToString() => this.Label;
    }
}
