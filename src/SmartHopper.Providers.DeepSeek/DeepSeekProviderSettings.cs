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

namespace SmartHopper.Providers.DeepSeek
{
    /// <summary>
    /// Settings implementation for the DeepSeek provider.
    /// This class is responsible for creating the UI controls for configuring the provider
    /// and for managing the provider's settings.
    /// </summary>
    public class DeepSeekProviderSettings : AIProviderSettings
    {
        private new readonly IAIProvider provider;

        /// <summary>
        /// Initializes a new instance of the <see cref="DeepSeekProviderSettings"/> class.
        /// </summary>
        /// <param name="provider">The provider associated with these settings.</param>
        public DeepSeekProviderSettings(IAIProvider provider)
            : base(provider)
        {
            this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        /// <inheritdoc/>
        public override IEnumerable<SettingDescriptor> GetSettingDescriptors()
        {
            // Define the settings that your provider requires
            return new[]
            {
                new SettingDescriptor
                {
                    Name = "ApiKey",
                    DisplayName = "API Key",
                    Description = "Your API key for DeepSeek. Get one at https://platform.deepseek.com/",
                    IsSecret = true, // Set to true for sensitive data like API keys
                    Type = typeof(string),
                },
                new SettingDescriptor
                {
                    Name = "Model",
                    DisplayName = "Model",
                    Description = "The model to use for generating responses",
                    Type = typeof(string),
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
                    DisplayName = "Max Tokens",
                    Description = "Maximum number of tokens to generate",
                    Type = typeof(int),
                    DefaultValue = 2000,
                    ControlParams = new NumericSettingDescriptorControl
                    {
                        UseSlider = false, // keep the NumericStepper
                        Min = 1,
                        Max = 8192,
                        Step = 1,
                    },
                },
                new SettingDescriptor
                {
                    Name = "Temperature",
                    Type = typeof(string),
                    DefaultValue = "0.5",
                    DisplayName = "Temperature",
                    Description = "Controls randomness (0.0–2.0). Higher values like 1.5 will make the output more random, while lower values like 0.2 will make it more focused and deterministic. Check https://api-docs.deepseek.com/quick_start/parameter_settings/ for more information.",
                },
            };
        }

        /// <summary>
        /// Validates the provided settings.
        /// </summary>
        /// <param name="settings">The settings to validate.</param>
        /// <returns>True if the settings are valid, otherwise false.</returns>
        public override bool ValidateSettings(Dictionary<string, object> settings)
        {
            Debug.WriteLine($"[DeepSeek] ValidateSettings called. Settings null? {settings == null}");

            if (settings == null)
            {
                return false;
            }

            // Set to false if you don't want to show error dialogs
            var showErrorDialogs = true;

            // Extract values from settings dictionary
            string? apiKey = null;
            string? model = null;
            int? maxTokens = null;
            double? temperature = null;

            // Get API key if present
            if (settings.TryGetValue("ApiKey", out var apiKeyObj) && apiKeyObj != null)
            {
                apiKey = apiKeyObj.ToString();
                Debug.WriteLine($"[DeepSeek] API key extracted (length: {apiKey.Length})");

                // Skip API key validation since any value is valid
            }

            // Get model if present
            if (settings.TryGetValue("Model", out var modelObj) && modelObj != null)
            {
                model = modelObj.ToString();
                Debug.WriteLine($"[DeepSeek] Model extracted: {model}");

                // Skip model validation since any value is valid
            }

            // Check max tokens if present - must be a positive number
            if (settings.TryGetValue("MaxTokens", out var maxTokensObj) && maxTokensObj != null)
            {
                // Try to parse as integer
                if (int.TryParse(maxTokensObj.ToString(), out int parsedMaxTokens))
                {
                    maxTokens = parsedMaxTokens;
                }

                // Ensure max tokens is greater than 0
                if (maxTokens.HasValue && maxTokens.Value <= 0)
                {
                    if (showErrorDialogs)
                    {
                        StyledMessageDialog.ShowError("Max tokens must be a positive number.", "Validation Error");
                    }

                    return false;
                }
            }

            if (settings.TryGetValue("Temperature", out var temperatureObj) && temperatureObj != null)
            {
                // Try to parse as double
                if (double.TryParse(temperatureObj.ToString(), out double parsedTemperature))
                {
                    temperature = parsedTemperature;
                }

                // Ensure temperature is between 0.0 and 2.0 (both included)
                if (temperature < 0.0 || temperature > 2.0)
                {
                    if (showErrorDialogs)
                    {
                        StyledMessageDialog.ShowError("Temperature must be between 0.0 and 2.0.", "Validation Error");
                    }

                    return false;
                }
            }

            Debug.WriteLine($"Validating DeepSeek settings: API Key: {apiKey}, Model: {model}, Max Tokens: {maxTokens}");

            return true;
        }
    }
}
