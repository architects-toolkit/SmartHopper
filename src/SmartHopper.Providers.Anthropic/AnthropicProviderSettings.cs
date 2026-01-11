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
using SmartHopper.Infrastructure.AIProviders;
using SmartHopper.Infrastructure.Dialogs;
using SmartHopper.Infrastructure.Settings;

namespace SmartHopper.Providers.Anthropic
{
    /// <summary>
    /// Settings implementation for the Anthropic provider.
    /// </summary>
    public class AnthropicProviderSettings : AIProviderSettings
    {
        private new readonly AnthropicProvider provider;

        public AnthropicProviderSettings(AnthropicProvider provider)
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
                    Description = "Your Anthropic API key. Get one at https://console.anthropic.com/",
                },
                new SettingDescriptor
                {
                    Name = "Model",
                    Type = typeof(string),
                    DisplayName = "Model",
                    Description = "The model to use for messages",
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
                    Description = "Controls randomness (0.0–2.0). Higher values make the output more random; lower values make it more deterministic.",
                },
            };
        }

        /// <inheritdoc/>
        public override bool ValidateSettings(Dictionary<string, object> settings)
        {
            if (settings == null) return false;

            var showErrorDialogs = true;

            string apiKey = null;
            string model = null;
            int? maxTokens = null;
            double? temperature = null;
            string version = null;

            if (settings.TryGetValue("ApiKey", out var apiKeyObj) && apiKeyObj != null)
            {
                apiKey = apiKeyObj.ToString();
            }

            if (settings.TryGetValue("Model", out var modelObj) && modelObj != null)
            {
                model = modelObj.ToString();
            }

            if (settings.TryGetValue("MaxTokens", out var maxTokensObj) && maxTokensObj != null)
            {
                if (int.TryParse(maxTokensObj.ToString(), out int parsed))
                {
                    maxTokens = parsed;
                }

                if (maxTokens <= 0)
                {
                    if (showErrorDialogs) StyledMessageDialog.ShowError("Max Tokens must be greater than 0.", "Validation Error");
                    return false;
                }
            }

            if (settings.TryGetValue("Temperature", out var temperatureObj) && temperatureObj != null)
            {
                if (double.TryParse(temperatureObj.ToString(), out double parsed))
                {
                    temperature = parsed;
                }

                if (temperature < 0.0 || temperature > 2.0)
                {
                    if (showErrorDialogs) StyledMessageDialog.ShowError("Temperature must be between 0.0 and 2.0.", "Validation Error");
                    return false;
                }
            }

            Debug.WriteLine($"Validating Anthropic settings: API Key: {(apiKey == null ? "<null>" : "<set>")}, Model: {model}, Max Tokens: {maxTokens}");
            return true;
        }
    }
}
