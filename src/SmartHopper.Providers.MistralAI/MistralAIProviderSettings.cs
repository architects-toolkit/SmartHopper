/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using SmartHopper.Config.Dialogs;
using SmartHopper.Config.Interfaces;
using SmartHopper.Config.Models;

namespace SmartHopper.Providers.MistralAI
{
    public class MistralAIProviderSettings : AIProviderSettings
    {
        private new readonly MistralAIProvider provider;

        public MistralAIProviderSettings(MistralAIProvider provider)
            : base(provider)
        {
            this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

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
                    Description = "Your MistralAI API key",
                },
                new SettingDescriptor
                {
                    Name = "Model",
                    Type = typeof(string),
                    DefaultValue = this.provider.DefaultModel,
                    IsSecret = false,
                    DisplayName = "Model",
                    Description = "The model to use for completions",
                },
                new SettingDescriptor
                {
                    Name = "MaxTokens",
                    Type = typeof(int),
                    DefaultValue = 500,
                    IsSecret = false,
                    DisplayName = "Max Tokens",
                    Description = "Maximum number of tokens to generate",
                    ControlParams = new NumericSettingDescriptorControl
                    {
                        UseSlider = false,
                        Min       = 1,
                        Max       = 100000,
                        Step      = 1,
                    }
                },
                new SettingDescriptor
                {
                    Name = "Temperature",
                    Type = typeof(string),
                    DefaultValue = "1",
                    IsSecret = false,
                    DisplayName = "Temperature",
                    Description = "Controls randomness (0.0–2.0). Higher values like 0.8 will make the output more random, while lower values like 0.2 will make it more focused and deterministic.",
                },
            };
        }

        public override bool ValidateSettings(Dictionary<string, object> settings)
        {
            Debug.WriteLine($"[MistralAI] ValidateSettings called. Settings null? {settings == null}");

            if (settings == null)
            {
                return false;
            }

            var showErrorDialogs = true; // Set to false if you don't want to show error dialogs

            // Extract values from settings dictionary
            string apiKey = null;
            string model = null;
            int? maxTokens = null;
            double? temperature = null;

            // Get API key if present
            if (settings.TryGetValue("ApiKey", out var apiKeyObj) && apiKeyObj != null)
            {
                apiKey = apiKeyObj.ToString();
                Debug.WriteLine($"[MistralAI] API key extracted (length: {apiKey.Length})");

                // Skip API key validation since any value is valid
            }

            // Get model if present
            if (settings.TryGetValue("Model", out var modelObj) && modelObj != null)
            {
                model = modelObj.ToString();
                Debug.WriteLine($"[MistralAI] Model extracted: {model}");

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
                if (temperature <= 0.0 || temperature >= 2.0)
                {
                    if (showErrorDialogs)
                    {
                        StyledMessageDialog.ShowError("Temperature must be between 0.0 and 2.0.", "Validation Error");
                    }
                    return false;
                }
            }

            Debug.WriteLine($"Validating MistralAI settings: API Key: {apiKey}, Model: {model}, Max Tokens: {maxTokens}");

            return true;
        }
    }
}
