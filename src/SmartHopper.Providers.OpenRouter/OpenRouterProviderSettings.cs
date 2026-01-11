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
using System.Diagnostics;
using System.Linq;
using SmartHopper.Infrastructure.AIProviders;
using SmartHopper.Infrastructure.Dialogs;
using SmartHopper.Infrastructure.Settings;

namespace SmartHopper.Providers.OpenRouter
{
    /// <summary>
    /// Settings implementation for the OpenRouter provider.
    /// </summary>
    public class OpenRouterProviderSettings : AIProviderSettings
    {
        private new readonly OpenRouterProvider provider;

        /// <summary>
        /// Initializes a new instance of the <see cref="OpenRouterProviderSettings"/> class.
        /// </summary>
        /// <param name="provider">The provider associated with these settings.</param>
        public OpenRouterProviderSettings(OpenRouterProvider provider)
            : base(provider)
        {
            this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        /// <inheritdoc/>
        public override IEnumerable<SettingDescriptor> GetSettingDescriptors()
        {
            return new[]
            {
                new SettingDescriptor
                {
                    Name = "ApiKey",
                    Type = typeof(string),
                    DefaultValue = string.Empty,
                    IsSecret = true,
                    DisplayName = "API Key",
                    Description = "Your OpenRouter API key. Get one at https://openrouter.ai/",
                },
                new SettingDescriptor
                {
                    Name = "Model",
                    Type = typeof(string),
                    DisplayName = "Model",
                    Description = "The model to use for completions",
                }.Apply(d => d.SetLazyDefault(() => this.provider.GetDefaultModel())),
                new SettingDescriptor
                {
                    Name = "EnableStreaming",
                    Type = typeof(bool),
                    DefaultValue = true,
                    DisplayName = "Enable Streaming",
                    Description = "Allow streaming responses for this provider. When enabled, you will receive the response as it is generated",
                },
                new SettingDescriptor
                {
                    Name = "MaxTokens",
                    Type = typeof(int),
                    DefaultValue = 2000,
                    DisplayName = "Max Tokens",
                    Description = "Maximum number of tokens to generate",
                    ControlParams = new NumericSettingDescriptorControl
                    {
                        UseSlider = false,
                        Min = 1,
                        Max = 100000,
                        Step = 1,
                    },
                },
                new SettingDescriptor
                {
                    Name = "Temperature",
                    Type = typeof(string),
                    DefaultValue = "0.5",
                    DisplayName = "Temperature",
                    Description = "Controls randomness (0.0–2.0). Some underlying models may ignore this parameter.",
                },

                // Provider selection controls
                new SettingDescriptor
                {
                    Name = "AllowFallbacks",
                    Type = typeof(bool),
                    DefaultValue = true,
                    DisplayName = "Allow Fallbacks",
                    Description = "Allow OpenRouter to fall back to compatible models if the preferred model is unavailable.",
                },
                new SettingDescriptor
                {
                    Name = "Sort",
                    Type = typeof(string),
                    DefaultValue = "price",
                    DisplayName = "Sort Strategy",
                    Description = "Provider selection sort: price, throughput, or latency.",
                    AllowedValues = new object[] { "price", "throughput", "latency" },
                },
                new SettingDescriptor
                {
                    Name = "DataCollection",
                    Type = typeof(string),
                    DefaultValue = "deny",
                    DisplayName = "Data Collection",
                    Description = "Allow or deny provider data collection (allow|deny). Defaults to deny.",
                    AllowedValues = new object[] { "deny", "allow" },
                },
            };
        }

        /// <inheritdoc/>
        public override bool ValidateSettings(Dictionary<string, object> settings)
        {
            Debug.WriteLine($"[OpenRouter] ValidateSettings called. Settings null? {settings == null}");
            if (settings == null)
            {
                return false;
            }

            var showErrorDialogs = true;

            string? apiKey = null;
            string? model = null;
            int? maxTokens = null;
            double? temperature = null;
            bool? allowFallbacks = null;
            string? sort = null;
            string? dataCollection = null;

            // API key (no strict validation here)
            if (settings.TryGetValue("ApiKey", out var apiKeyObj) && apiKeyObj != null)
            {
                apiKey = apiKeyObj.ToString();
                Debug.WriteLine($"[OpenRouter] API key extracted (length: {apiKey?.Length ?? 0})");
            }

            // Model (no strict validation here)
            if (settings.TryGetValue("Model", out var modelObj) && modelObj != null)
            {
                model = modelObj.ToString();
                Debug.WriteLine($"[OpenRouter] Model extracted: {model}");
            }

            // Max tokens must be > 0 if present
            if (settings.TryGetValue("MaxTokens", out var maxTokensObj) && maxTokensObj != null)
            {
                if (int.TryParse(maxTokensObj.ToString(), out int parsedMaxTokens))
                {
                    maxTokens = parsedMaxTokens;
                }

                if (maxTokens <= 0)
                {
                    if (showErrorDialogs)
                    {
                        StyledMessageDialog.ShowError("Max Tokens must be greater than 0.", "Validation Error");
                    }

                    return false;
                }
            }

            // Temperature should be parsable and within range if present
            if (settings.TryGetValue("Temperature", out var temperatureObj) && temperatureObj != null)
            {
                if (double.TryParse(temperatureObj.ToString(), out double parsedTemperature))
                {
                    temperature = parsedTemperature;
                }

                if (temperature < 0.0 || temperature > 2.0)
                {
                    if (showErrorDialogs)
                    {
                        StyledMessageDialog.ShowError("Temperature must be between 0.0 and 2.0.", "Validation Error");
                    }

                    return false;
                }
            }

            // AllowFallbacks (bool) optional
            if (settings.TryGetValue("AllowFallbacks", out var allowFallbacksObj) && allowFallbacksObj != null)
            {
                if (bool.TryParse(allowFallbacksObj.ToString(), out bool parsedAllowFallbacks))
                {
                    allowFallbacks = parsedAllowFallbacks;
                }
            }

            // Sort (string) must be one of: price, throughput, latency
            if (settings.TryGetValue("Sort", out var sortObj) && sortObj != null)
            {
                sort = sortObj.ToString()?.Trim().ToLowerInvariant();
                var allowedSort = new[] { "price", "throughput", "latency" };
                if (!allowedSort.Contains(sort))
                {
                    if (showErrorDialogs)
                    {
                        StyledMessageDialog.ShowError("Sort must be one of: price, throughput, latency.", "Validation Error");
                    }

                    return false;
                }
            }

            // DataCollection (string) must be one of: deny, allow
            if (settings.TryGetValue("DataCollection", out var dataCollectionObj) && dataCollectionObj != null)
            {
                dataCollection = dataCollectionObj.ToString()?.Trim().ToLowerInvariant();
                var allowedDc = new[] { "deny", "allow" };
                if (!allowedDc.Contains(dataCollection))
                {
                    if (showErrorDialogs)
                    {
                        StyledMessageDialog.ShowError("Data Collection must be either 'deny' or 'allow'.", "Validation Error");
                    }

                    return false;
                }
            }

            Debug.WriteLine($"Validating OpenRouter settings: API Key: {(string.IsNullOrEmpty(apiKey) ? "<empty>" : "<provided>")}, Model: {model}, Max Tokens: {maxTokens}");

            return true;
        }
    }
}
