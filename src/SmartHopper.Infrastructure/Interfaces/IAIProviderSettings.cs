/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using System;
using System.Collections.Generic;
using SmartHopper.Infrastructure.Models;

namespace SmartHopper.Infrastructure.Interfaces
{
    /// <summary>
    /// Interface for provider settings UI and validation.
    /// </summary>
    public interface IAIProviderSettings
    {
        IEnumerable<SettingDescriptor> GetSettingDescriptors();

        bool ValidateSettings(Dictionary<string, object> settings);
    }
}
