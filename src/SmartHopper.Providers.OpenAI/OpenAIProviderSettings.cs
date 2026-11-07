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

namespace SmartHopper.Providers.OpenAI
{
    /// <summary>
    /// Settings implementation for the OpenAI provider.
    /// This class is responsible for creating the UI controls for configuring the provider
    /// and for managing the provider's settings.
    /// </summary>
    public class OpenAIProviderSettings : AIProviderSettings
    {
        private readonly OpenAIProvider provider;

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
        public override bool EnableStreaming => base.EnableStreaming || this.provider.IsChatGptConnected;

        /// <inheritdoc/>
        public override IEnumerable<SettingDescriptor> GetSettingDescriptors()
        {
            this.provider.NormalizeConnectionStatus();
            var connected = this.provider.IsChatGptConnected;
            var actionRunning = this.provider.IsChatGptActionRunning;
            var connectText = actionRunning
                ? "Cancel sign-in"
                : (connected ? "Disconnect" : "Continue with ChatGPT");
            var connectDescription = actionRunning
                ? "Sign-in in progress. Click to cancel it."
                : (connected
                    ? "Sign out of your ChatGPT account on this computer and remove the stored tokens."
                    : "Open your browser and sign in with your ChatGPT account. Eligible ChatGPT plans can be used for AI requests without an API key.");

            return new[]
            {
                new SettingDescriptor
                {
                    Name = "ApiKey",
                    Type = typeof(string),
                    DefaultValue = string.Empty,
                    IsSecret = true,
                    DisplayName = "API Key",
                    Description = "Your OpenAI API key. Get one at https://platform.openai.com/. When a ChatGPT sign-in is active it is used first and this key serves as fallback.",
                },
                new SettingDescriptor
                {
                    Name = "ChatGptConnect",
                    Type = typeof(bool),
                    DefaultValue = false,
                    DisplayName = "ChatGPT sign-in",
                    Description = connectDescription,
                    ControlParams = new ButtonSettingDescriptorControl { Text = connectText },
                },
                new SettingDescriptor
                {
                    Name = "ChatGptStatus",
                    Type = typeof(string),
                    DefaultValue = "Not connected",
                    DisplayName = "Status",
                    ControlParams = new StatusSettingDescriptorControl
                    {
                        LevelSettingName = "ChatGptStatusLevel",
                    },
                },
                new SettingDescriptor
                {
                    Name = "ChatGptStatusLevel",
                    Type = typeof(string),
                    DefaultValue = "none",
                    IsHidden = true,
                },
                new SettingDescriptor
                {
                    Name = "ChatGptAccessToken",
                    Type = typeof(string),
                    DefaultValue = string.Empty,
                    DisplayName = "ChatGPT access token",
                    Description = "Managed automatically by the ChatGPT sign-in.",
                    IsSecret = true,
                    IsHidden = true,
                },
                new SettingDescriptor
                {
                    Name = "ChatGptRefreshToken",
                    Type = typeof(string),
                    DefaultValue = string.Empty,
                    DisplayName = "ChatGPT refresh token",
                    Description = "Managed automatically by the ChatGPT sign-in.",
                    IsSecret = true,
                    IsHidden = true,
                },
                new SettingDescriptor
                {
                    Name = "ChatGptIdToken",
                    Type = typeof(string),
                    DefaultValue = string.Empty,
                    DisplayName = "ChatGPT ID token",
                    Description = "Managed automatically by the ChatGPT sign-in.",
                    IsSecret = true,
                    IsHidden = true,
                },
                new SettingDescriptor
                {
                    Name = "ChatGptClientId",
                    Type = typeof(string),
                    DefaultValue = string.Empty,
                    DisplayName = "ChatGPT client ID",
                    Description = "Managed automatically by the ChatGPT sign-in.",
                    IsHidden = true,
                },
                new SettingDescriptor
                {
                    Name = "ChatGptHostId",
                    Type = typeof(string),
                    DefaultValue = string.Empty,
                    DisplayName = "ChatGPT host ID",
                    Description = "Managed automatically by the ChatGPT sign-in.",
                    IsHidden = true,
                },
                new SettingDescriptor
                {
                    Name = "ChatGptEmail",
                    Type = typeof(string),
                    DefaultValue = string.Empty,
                    DisplayName = "ChatGPT account email",
                    Description = "Managed automatically by the ChatGPT sign-in.",
                    IsHidden = true,
                },
                new SettingDescriptor
                {
                    Name = "ChatGptAccessTokenExpiresAt",
                    Type = typeof(string),
                    DefaultValue = string.Empty,
                    DisplayName = "ChatGPT access token expiry",
                    Description = "Managed automatically by the ChatGPT sign-in.",
                    IsHidden = true,
                },
                new SettingDescriptor
                {
                    Name = "ChatGptScopes",
                    Type = typeof(string),
                    DefaultValue = string.Empty,
                    DisplayName = "ChatGPT granted scopes",
                    Description = "Managed automatically by the ChatGPT sign-in.",
                    IsHidden = true,
                },
                new SettingDescriptor
                {
                    Name = "ChatGptUsageLink",
                    Type = typeof(string),
                    DefaultValue = string.Empty,
                    DisplayName = "Plan usage",
                    Description = "Review how much of your ChatGPT plan this app uses and manage app access.",
                    ControlParams = new LinkSettingDescriptorControl
                    {
                        Url = "https://chatgpt.com/settings/usage",
                        Text = "ChatGPT settings → Usage",
                    },
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
                    Name = "Temperature",
                    Type = typeof(string),
                    DefaultValue = "0.5",
                    DisplayName = "Temperature",
                    Description = "Controls randomness (0.0–2.0). Higher values like 0.8 will make the output more random, while lower values like 0.2 will make it more focused and deterministic. Some models like o-series or gpt-5 do not support this parameter and it will be ignored.",
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
                    Name = "ForceChatCompletions",
                    Type = typeof(bool),
                    DefaultValue = false,
                    DisplayName = "Force Chat Completions",
                    Description = "Force the legacy /v1/chat/completions endpoint instead of the default v1/responses. Only use this if you specifically need Chat Completions behavior. The Responses API is a superset and recommended by OpenAI for all new projects.",
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

            // Managed values: the form must never overwrite what the sign-in stored.
            settings.Remove("ChatGptStatus");
            settings.Remove("ChatGptStatusLevel");
            settings.Remove("ChatGptAccessToken");
            settings.Remove("ChatGptRefreshToken");
            settings.Remove("ChatGptIdToken");
            settings.Remove("ChatGptClientId");
            settings.Remove("ChatGptHostId");
            settings.Remove("ChatGptEmail");
            settings.Remove("ChatGptAccessTokenExpiresAt");
            settings.Remove("ChatGptScopes");
            settings.Remove("ChatGptUsageLink");

            var connect = settings.TryGetValue("ChatGptConnect", out var connectObj)
                && bool.TryParse(connectObj?.ToString(), out var connectValue)
                && connectValue;
            if (connect)
            {
                // One-shot action: consumed here, never persisted.
                settings.Remove("ChatGptConnect");
                if (this.provider.IsChatGptActionRunning)
                {
                    // The button doubles as a cancel while work is in flight.
                    this.provider.CancelChatGptSignIn();
                    return true;
                }

                if (this.provider.IsChatGptConnected)
                {
                    // The button doubles as a disconnect once connected.
                    this.provider.DisconnectChatGpt();
                    return true;
                }

                this.provider.StartChatGptSignIn();
                return true;
            }

            // Set to false if you don't want to show error dialogs
            var showErrorDialogs = true;

            // Extract values from settings dictionary
            string? apiKey = null;
            string? model = null;
            string? reasoningEffort = null;

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
                        ProviderSdkHost.Diagnostics.Report(this.GetType().Name, new SHRuntimeMessage(SHRuntimeMessageSeverity.Error, SHRuntimeMessageOrigin.Validation, SHMessageCode.InputInvalid, "Reasoning effort must be low, medium, or high."));
                    }

                    return false;
                }
            }

            if (!this.ValidateMaxTokens(settings, showErrorDialogs))
            {
                return false;
            }

            if (!this.ValidateTemperature(settings, showErrorDialogs))
            {
                return false;
            }

            Debug.WriteLine($"Validating OpenAI settings: API Key: {(apiKey == null ? "<null>" : "<set>")}, Model: {model}, Reasoning Effort: {reasoningEffort}");

            return true;
        }
    }
}
