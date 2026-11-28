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

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SmartHopper.ProviderSdk.Diagnostics;
using SmartHopper.ProviderSdk.Hosting;
using SmartHopper.ProviderSdk.Settings;
namespace SmartHopper.ProviderSdk.AIProviders
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

        /// <summary>
        /// Gets the ordered settings surface: persisted settings bound to the store plus live
        /// chrome elements (status, actions, links) that never touch the store. The default
        /// wraps every non-hidden descriptor; override to compose chrome or live view-state.
        /// Implementations must construct elements only — delegates are evaluated by the host.
        /// </summary>
        /// <returns>The ordered surface elements.</returns>
        public virtual IEnumerable<ProviderSurfaceElement> GetSurface()
            => this.GetSettingDescriptors()
                .Where(descriptor => descriptor != null && !descriptor.IsHidden)
                .Select(descriptor => new BoundSettingElement(descriptor));

        public abstract bool ValidateSettings(Dictionary<string, object> settings);

        /// <inheritdoc/>
        public virtual bool EnableStreaming
        {
            get
            {
                try
                {
                    // Try persisted value first via the host-injected settings store
                    var store = ProviderSdkHost.SettingsStoreFactory(this.Provider.Name);
                    var value = store?.Get<object>("EnableStreaming");
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

        /// <summary>
        /// Reports a settings validation error through the host diagnostics.
        /// </summary>
        /// <param name="message">The validation message.</param>
        protected void ReportSettingsValidationError(string message)
        {
            ProviderSdkHost.Diagnostics.Report(this.GetType().Name, new SHRuntimeMessage(SHRuntimeMessageSeverity.Error, SHRuntimeMessageOrigin.Validation, SHMessageCode.InputInvalid, message));
        }

        /// <summary>
        /// Validates that the "MaxTokens" setting, if present, is a non-negative integer.
        /// Zero means unlimited (the provider omits the token cap). Unparseable or negative
        /// values are reported as errors. Providers whose API requires the cap (Anthropic)
        /// pass <paramref name="allowZero"/> as <see langword="false"/>.
        /// </summary>
        /// <param name="settings">The settings to validate.</param>
        /// <param name="reportError">If true, reports a validation error when invalid.</param>
        /// <param name="errorMessage">The error message to report. Defaults to a message matching <paramref name="allowZero"/>.</param>
        /// <param name="allowZero">If true, 0 is accepted as unlimited; if false, a positive value is required.</param>
        /// <returns>True if the setting is absent or valid; false if it is present and invalid.</returns>
        protected bool ValidateMaxTokens(Dictionary<string, object> settings, bool reportError, string errorMessage = null, bool allowZero = true)
        {
            if (settings == null)
            {
                return false;
            }

            if (settings.TryGetValue("MaxTokens", out var maxTokensObj) && maxTokensObj != null)
            {
                if (!int.TryParse(maxTokensObj.ToString(), out var maxTokens) || maxTokens < 0 || (maxTokens == 0 && !allowZero))
                {
                    if (reportError)
                    {
                        this.ReportSettingsValidationError(errorMessage ?? (allowZero ? "Max Tokens must be 0 or greater." : "Max Tokens must be greater than 0."));
                    }

                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Validates that the "Temperature" setting, if present, is within the specified range.
        /// Unparseable or out-of-range values are reported as errors.
        /// </summary>
        /// <param name="settings">The settings to validate.</param>
        /// <param name="reportError">If true, reports a validation error when invalid.</param>
        /// <param name="errorMessage">The error message to report.</param>
        /// <param name="min">The minimum allowed temperature.</param>
        /// <param name="max">The maximum allowed temperature.</param>
        /// <returns>True if the setting is absent or valid; false if it is present and invalid.</returns>
        protected bool ValidateTemperature(Dictionary<string, object> settings, bool reportError, string errorMessage = "Temperature must be between 0.0 and 2.0.", double min = 0.0, double max = 2.0)
        {
            if (settings == null)
            {
                return false;
            }

            if (settings.TryGetValue("Temperature", out var temperatureObj) && temperatureObj != null)
            {
                if (!double.TryParse(temperatureObj.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var temperature) || temperature < min || temperature > max)
                {
                    if (reportError)
                    {
                        this.ReportSettingsValidationError(errorMessage);
                    }

                    return false;
                }
            }

            return true;
        }
    }
}
