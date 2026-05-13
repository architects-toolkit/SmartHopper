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
using SmartHopper.Infrastructure.AIProviders;
using SmartHopper.Infrastructure.Dialogs;
using SmartHopper.Infrastructure.Settings;

namespace SmartHopper.Providers.Ollama
{
    /// <summary>
    /// Settings implementation for the Ollama provider.
    /// Exposes a configurable Base URL pointing at a local Ollama instance,
    /// plus an optional API key for installations placed behind an authenticating proxy.
    /// </summary>
    public class OllamaProviderSettings : AIProviderSettings
    {
        private new readonly IAIProvider provider;

        /// <summary>
        /// Initializes a new instance of the <see cref="OllamaProviderSettings"/> class.
        /// </summary>
        /// <param name="provider">The provider associated with these settings.</param>
        public OllamaProviderSettings(IAIProvider provider)
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
                    Name = "ServerUrl",
                    DisplayName = "Base URL",
                    Description = "Base URL of your Ollama server, including the OpenAI-compatible API prefix (e.g. http://localhost:11434/v1).",
                    Type = typeof(string),
                    DefaultValue = OllamaProvider.FallbackServerUrl.ToString(),
                },
                new SettingDescriptor
                {
                    Name = "ApiKey",
                    DisplayName = "API Key",
                    Description = "Optional API key. Ollama itself does not require authentication, but you can set one if your instance is behind a reverse proxy that enforces it.",
                    IsSecret = true,
                    Type = typeof(string),
                },
                new SettingDescriptor
                {
                    Name = "Model",
                    DisplayName = "Model",
                    Description = "Model tag as pulled with `ollama pull` (e.g. 'llama3.1', 'qwen2.5:14b', 'mistral:7b-instruct').",
                    Type = typeof(string),
                }.Apply(d => d.SetLazyDefault(() => this.provider.GetDefaultModel())),
                new SettingDescriptor
                {
                    Name = "EnableStreaming",
                    Type = typeof(bool),
                    DefaultValue = true,
                    DisplayName = "Enable Streaming",
                    Description = "Allow streaming responses for this provider. When enabled, you will receive the response as it is generated.",
                },
                new SettingDescriptor
                {
                    Name = "MaxTokens",
                    DisplayName = "Max Tokens",
                    Description = "Maximum number of tokens to generate.",
                    Type = typeof(int),
                    DefaultValue = 2000,
                    ControlParams = new NumericSettingDescriptorControl
                    {
                        UseSlider = false,
                        Min = 1,
                        Max = 32768,
                        Step = 1,
                    },
                },
                new SettingDescriptor
                {
                    Name = "Temperature",
                    Type = typeof(string),
                    DefaultValue = "0.5",
                    DisplayName = "Temperature",
                    Description = "Controls randomness (0.0–2.0). Higher values produce more diverse output; lower values are more focused and deterministic.",
                },
            };
        }

        /// <inheritdoc/>
        public override bool ValidateSettings(Dictionary<string, object> settings)
        {
            Debug.WriteLine($"[Ollama] ValidateSettings called. Settings null? {settings == null}");

            if (settings == null)
            {
                return false;
            }

            const bool showErrorDialogs = true;

            // Validate Base URL when supplied: must parse as an absolute HTTP(S) URI.
            if (settings.TryGetValue("ServerUrl", out var serverUrlObj) && serverUrlObj != null)
            {
                var serverUrl = serverUrlObj.ToString();
                if (!string.IsNullOrWhiteSpace(serverUrl))
                {
                    if (!Uri.TryCreate(serverUrl, UriKind.Absolute, out var parsed)
                        || (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps))
                    {
                        if (showErrorDialogs)
                        {
                            StyledMessageDialog.ShowError(
                                "Base URL must be an absolute HTTP or HTTPS URL (for example http://localhost:11434/v1).",
                                "Validation Error");
                        }

                        return false;
                    }
                }
            }

            // MaxTokens must be a positive integer
            if (settings.TryGetValue("MaxTokens", out var maxTokensObj) && maxTokensObj != null)
            {
                if (!int.TryParse(maxTokensObj.ToString(), out var parsedMaxTokens) || parsedMaxTokens <= 0)
                {
                    if (showErrorDialogs)
                    {
                        StyledMessageDialog.ShowError("Max tokens must be a positive number.", "Validation Error");
                    }

                    return false;
                }
            }

            // Temperature must be in [0.0, 2.0]
            if (settings.TryGetValue("Temperature", out var temperatureObj) && temperatureObj != null)
            {
                if (!double.TryParse(temperatureObj.ToString(), out var parsedTemperature)
                    || parsedTemperature < 0.0
                    || parsedTemperature > 2.0)
                {
                    if (showErrorDialogs)
                    {
                        StyledMessageDialog.ShowError("Temperature must be between 0.0 and 2.0.", "Validation Error");
                    }

                    return false;
                }
            }

            return true;
        }
    }
}
