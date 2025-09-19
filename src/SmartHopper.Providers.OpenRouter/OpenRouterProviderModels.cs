/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SmartHopper.Infrastructure.AICall.Core.Requests;
using SmartHopper.Infrastructure.AICall.Core.Returns;
using SmartHopper.Infrastructure.AIModels;
using SmartHopper.Infrastructure.AIProviders;

namespace SmartHopper.Providers.OpenRouter
{
    /// <summary>
    /// OpenRouter provider-specific model management.
    /// Uses provider/model naming (e.g., "openai/gpt-5-mini").
    /// </summary>
    public class OpenRouterProviderModels : AIProviderModels
    {
        private readonly OpenRouterProvider openRouterProvider;

        public OpenRouterProviderModels(OpenRouterProvider provider)
            : base(provider)
        {
            this.openRouterProvider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        /// <inheritdoc/>
        public override Task<List<AIModelCapabilities>> RetrieveModels()
        {
            var provider = this.openRouterProvider.Name.ToLowerInvariant();

            // Sample curated models exposed via OpenRouter
            var models = new List<AIModelCapabilities>
            {
                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-5-mini",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    Default = AICapability.Text2Text,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 90,
                },
                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "mistralai/mistral-small",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 80,
                },
                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "anthropic/claude-3.5-haiku",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 82,
                },
                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "deepseek/deepseek-chat-v3.1",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 78,
                },
                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "google/gemini-2.5-flash",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 85,
                },
                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "moonshotai/kimi-k2",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 70,
                },
            };

            return Task.FromResult(models);
        }

        /// <inheritdoc/>
        public override async Task<List<string>> RetrieveApiModels()
        {
            try
            {
                var request = new AIRequestCall
                {
                    Endpoint = "/models",
                };

                var response = await this.openRouterProvider.Call(request).ConfigureAwait(false);

                Debug.WriteLine("[OpenRouterProviderModels] RetrieveApiModels response: " + (response?.Success == true));

                if (response == null || !response.Success)
                {
                    return new List<string>();
                }

                var raw = (response as AIReturn)?.GetRaw();
                if (raw == null)
                {
                    return new List<string>();
                }

                var data = raw["data"] as JArray;
                if (data == null)
                {
                    return new List<string>();
                }

                var models = new List<string>();
                foreach (var item in data.OfType<JObject>())
                {
                    var id = item["id"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(id))
                    {
                        models.Add(id);
                    }
                }

                return models
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(m => m, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            catch
            {
                return new List<string>();
            }
        }
    }
}
