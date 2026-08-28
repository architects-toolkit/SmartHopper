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
using System.Diagnostics;
using System.Linq;
using SmartHopper.ProviderSdk.AIProviders;
using SmartHopper.ProviderSdk.Diagnostics;
using SmartHopper.ProviderSdk.Hosting;
using SmartHopper.ProviderSdk.Settings;

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
                    Name = "ReasoningEffort",
                    Type = typeof(string),
                    DefaultValue = "high",
                    DisplayName = "Reasoning Effort",
                    Description = "Controls DeepSeek thinking mode and reasoning depth. Use 'none' to disable thinking, or 'high'/'max' to enable it. Only applies to deepseek-v4 models and deepseek-reasoner.",
                    AllowedValues = new[] { "none", "high", "max" },
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
                        Max = 400000,
                        Step = 1,
                    },
                },
                new SettingDescriptor
                {
                    Name = "Temperature",
                    Type = typeof(string),
                    DefaultValue = "0.5",
                    DisplayName = "Temperature",
                    Description = "Controls randomness (0.0–2.0). Higher values like 1.5 will make the output more random, while lower values like 0.2 will make it more focused and deterministic. Has no effect in thinking mode. Check https://api-docs.deepseek.com/quick_start/parameter_settings/ for more information.",
                },
                new SettingDescriptor
                {
                    Name = "TopP",
                    Type = typeof(string),
                    DefaultValue = "1",
                    DisplayName = "Top P",
                    Description = "Controls nucleus sampling (0.0–1.0). Only adjust this or Temperature, not both. Has no effect in thinking mode.",
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
            string? reasoningEffort = null;
            double? topP = null;

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

            if (!this.ValidateMaxTokens(settings, showErrorDialogs, "Max tokens must be a positive number."))
            {
                return false;
            }

            if (!this.ValidateTemperature(settings, showErrorDialogs))
            {
                return false;
            }

            if (settings.TryGetValue("TopP", out var topPObj) && topPObj != null)
            {
                // Try to parse as double
                if (double.TryParse(topPObj.ToString(), out double parsedTopP))
                {
                    topP = parsedTopP;
                }

                // Ensure top_p is between 0.0 and 1.0 (both included)
                if (topP < 0.0 || topP > 1.0)
                {
                    if (showErrorDialogs)
                    {
                        ProviderSdkHost.Diagnostics.Report(this.GetType().Name, new SHRuntimeMessage(SHRuntimeMessageSeverity.Error, SHRuntimeMessageOrigin.Validation, SHMessageCode.InputInvalid, "Top P must be between 0.0 and 1.0."));
                    }

                    return false;
                }
            }

            if (settings.TryGetValue("ReasoningEffort", out var reasoningEffortObj) && reasoningEffortObj != null)
            {
                reasoningEffort = reasoningEffortObj.ToString();

                // Ensure reasoning effort is one of the supported values
                if (string.IsNullOrWhiteSpace(reasoningEffort) || !new[] { "none", "high", "max" }.Contains(reasoningEffort, StringComparer.OrdinalIgnoreCase))
                {
                    if (showErrorDialogs)
                    {
                        ProviderSdkHost.Diagnostics.Report(this.GetType().Name, new SHRuntimeMessage(SHRuntimeMessageSeverity.Error, SHRuntimeMessageOrigin.Validation, SHMessageCode.InputInvalid, "Reasoning Effort must be 'none', 'high', or 'max'."));
                    }

                    return false;
                }
            }

            Debug.WriteLine($"Validating DeepSeek settings: API Key: {apiKey}, Model: {model}");

            return true;
        }
    }
}
