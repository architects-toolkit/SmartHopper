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
using System.Linq;
using SmartHopper.Infrastructure.Settings;

namespace SmartHopper.Infrastructure.AIProviders
{
    /// <summary>
    /// Base class for provider settings, encapsulating common UI building and persistence logic.
    /// </summary>
    public abstract class AIProviderSettings : IAIProviderSettings
    {
        protected readonly IAIProvider provider;

        protected AIProviderSettings(IAIProvider provider)
        {
            this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        public abstract IEnumerable<SettingDescriptor> GetSettingDescriptors();

        public abstract bool ValidateSettings(Dictionary<string, object> settings);

        /// <inheritdoc/>
        public virtual bool EnableStreaming
        {
            get
            {
                try
                {
                    // Try persisted value first
                    var value = SmartHopperSettings.Instance.GetSetting(this.provider.Name, "EnableStreaming");
                    if (value is bool b)
                    {
                        return b;
                    }
                    if (value != null && bool.TryParse(value.ToString(), out bool parsed))
                    {
                        return parsed;
                    }

                    // Fallback to descriptor default if available
                    var descriptor = this.GetSettingDescriptors()?.FirstOrDefault(d => d.Name == "EnableStreaming");
                    if (descriptor?.DefaultValue is bool defBool)
                    {
                        return defBool;
                    }
                }
                catch
                {
                    // Ignore and fall through to default
                }

                // Safe default: disabled
                return false;
            }
        }
    }
}
