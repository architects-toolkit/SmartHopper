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
using SmartHopper.Infrastructure.AIProviders;
using SmartHopper.Infrastructure.Dialogs;
using SmartHopper.Infrastructure.Settings;

namespace SmartHopper.Providers.OpenAI
{
    /// <summary>
    /// Settings implementation for the OpenAI provider.
    /// This class is responsible for creating the UI controls for configuring the provider
    /// and for managing the provider's settings.
    /// </summary>
    public class OpenAIProviderSettings : AIProviderSettings
    {
        private new readonly OpenAIProvider provider;

        /// <summary>
        /// Initializes a new instance of the <see cref="OpenAIProviderSettings"/> class.
        /// </summary>
        /// <param name="provider">The provider associated with these settings.</param>
        public OpenAIProviderSettings(OpenAIProvider provider)
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
                    Description = "Your OpenAI API key. Get one at https://platform.openai.com/",
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
                        UseSlider = false, // keep the NumericStepper
                        Min = 1,
                        Max = 100000,
                        Step = 1,
                    },
                },
                new SettingDescriptor
                {
                    Name = "ReasoningEffort",
                    Type = typeof(string),
                    DefaultValue = "low",
                    IsSecret = false,
                    DisplayName = "Reasoning Effort",
                    Description = "Level of reasoning effort for reasoning models (low, medium, or high)",
                    AllowedValues = new[] { "low", "medium", "high" },
                },
                new SettingDescriptor
                {
                    Name = "Temperature",
                    Type = typeof(string),
                    DefaultValue = "0.5",
                    DisplayName = "Temperature",
                    Description = "Controls randomness (0.0–2.0). Higher values like 0.8 will make the output more random, while lower values like 0.2 will make it more focused and deterministic. Some models like o-series or gpt-5 do not support this parameter and it will be ignored.",
                },
            };
        }

        /// <inheritdoc/>
        public override bool ValidateSettings(Dictionary<string, object> settings)
        {
            Debug.WriteLine($"[OpenAI] ValidateSettings called. Settings null? {settings == null}");
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
            int? maxTokens = null;
            double? temperature = null;

            // Get API key if present
            if (settings.TryGetValue("ApiKey", out var apiKeyObj) && apiKeyObj != null)
            {
                apiKey = apiKeyObj.ToString();
                Debug.WriteLine($"[OpenAI] API key extracted (length: {apiKey.Length})");

                // Skip API key validation since any value is valid
            }

            // Get model if present
            if (settings.TryGetValue("Model", out var modelObj) && modelObj != null)
            {
                model = modelObj.ToString();
                Debug.WriteLine($"[OpenAI] Model extracted: {model}");

                // Skip model validation since any value is valid
            }

            // Get reasoning effort if present
            if (settings.TryGetValue("ReasoningEffort", out var reasoningEffortObj) && reasoningEffortObj != null)
            {
                reasoningEffort = reasoningEffortObj.ToString();
                Debug.WriteLine($"[OpenAI] ReasoningEffort extracted: {reasoningEffort}");

                // Check if reasoning effort is valid
                if (string.IsNullOrWhiteSpace(reasoningEffort) || !new[] { "low", "medium", "high" }.Contains(reasoningEffort))
                {
                    Debug.WriteLine($"[OpenAI] Invalid reasoning effort value: {reasoningEffort}");

                    if (showErrorDialogs)
                    {
                        StyledMessageDialog.ShowError("Reasoning effort must be low, medium, or high.", "Validation Error");
                    }

                    return false;
                }
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
                if (maxTokens <= 0)
                {
                    if (showErrorDialogs)
                    {
                        StyledMessageDialog.ShowError("Max Tokens must be greater than 0.", "Validation Error");
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

            Debug.WriteLine($"Validating OpenAI settings: API Key: {apiKey}, Model: {model}, Max Tokens: {maxTokens}, Reasoning Effort: {reasoningEffort}");

            return true;
        }
    }
}
