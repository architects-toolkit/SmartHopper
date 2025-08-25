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
using SmartHopper.Infrastructure.Settings;

namespace SmartHopper.Infrastructure.AIProviders
{
    /// <summary>
    /// Interface for provider settings UI and validation.
    /// </summary>
    public interface IAIProviderSettings
    {
        /// <summary>
        /// Gets the setting descriptors that will be used to create the Settings dialog.
        /// </summary>
        /// <returns>The setting descriptors.</returns>
        IEnumerable<SettingDescriptor> GetSettingDescriptors();

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
