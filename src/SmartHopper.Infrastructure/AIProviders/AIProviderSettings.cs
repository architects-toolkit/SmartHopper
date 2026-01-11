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
        private readonly IAIProvider _provider;

        /// <summary>
        /// Gets the provider instance these settings are associated with.
        /// </summary>
        protected IAIProvider Provider => this._provider;

        protected AIProviderSettings(IAIProvider provider)
        {
            this._provider = provider ?? throw new ArgumentNullException(nameof(provider));
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
                    var value = SmartHopperSettings.Instance.GetSetting(this.Provider.Name, "EnableStreaming");
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
