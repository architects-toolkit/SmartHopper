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

using System.Collections.Generic;
using System.Linq;
using SmartHopper.ProviderSdk.Settings;
namespace SmartHopper.ProviderSdk.AIProviders
{
    /// <summary>
    /// Interface for provider settings UI and validation.
    /// </summary>
    public interface IAIProviderSettings
    {
        /// <summary>
        /// Gets the setting descriptors that will be used to create the Settings dialog.
        /// This is the persisted schema: implementations must only construct descriptors,
        /// never evaluate live provider state, because the settings store enumerates them
        /// while resolving secret flags inside <c>GetSetting</c>/<c>SetSetting</c> calls.
        /// </summary>
        /// <returns>The setting descriptors.</returns>
        IEnumerable<SettingDescriptor> GetSettingDescriptors();

        /// <summary>
        /// Gets the ordered settings surface the settings dialog renders: bound persisted
        /// settings (<see cref="BoundSettingElement"/>) interleaved with live chrome
        /// (<see cref="ChromeElement"/> rows such as status labels, action buttons and links)
        /// that is evaluated through delegates and never touches the settings store.
        /// The default wraps every non-hidden descriptor; override to add chrome or live
        /// view-state hooks. Like <see cref="GetSettingDescriptors"/>, implementations must
        /// construct elements without invoking their delegates.
        /// </summary>
        /// <returns>The ordered surface elements.</returns>
        IEnumerable<ProviderSurfaceElement> GetSurface()
            => this.GetSettingDescriptors()
                .Where(descriptor => descriptor != null && !descriptor.IsHidden)
                .Select(descriptor => new BoundSettingElement(descriptor));

        /// <summary>
        /// Validates the settings.
        /// </summary>
        /// <param name="settings">The settings to validate.</param>
        /// <returns>True if the settings are valid, false otherwise.</returns>
        bool ValidateSettings(Dictionary<string, object> settings);

        /// <summary>
        /// Gets a value indicating whether streaming is enabled for this provider.
        /// Implementations should source this from the persisted provider settings (EnableStreaming).
        /// </summary>
        bool EnableStreaming { get; }
    }
}
