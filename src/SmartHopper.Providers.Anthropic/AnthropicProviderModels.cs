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

namespace SmartHopper.Providers.Anthropic
{
    /// <summary>
    /// Anthropic provider-specific model management implementation.
    /// </summary>
    public class AnthropicProviderModels : AIProviderModels
    {
        private readonly AnthropicProvider provider;

        public AnthropicProviderModels(AnthropicProvider provider)
            : base(provider)
        {
            this.provider = provider;
        }

        /// <inheritdoc/>
        public override Task<List<AIModelCapabilities>> RetrieveModels()
        {
            var providerName = this.provider.Name.ToLower();

            var models = new List<AIModelCapabilities>
            {
                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "claude-opus-4-1",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.ImageInput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 20,
                },
                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "claude-opus-4-0",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.ImageInput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 20,
                },
                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "claude-sonnet-4-0",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.ImageInput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 80,
                },
                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "claude-3-7-sonnet-latest",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.ImageInput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 90,
                },
                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "claude-3-5-haiku-latest",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.ImageInput,
                    Default = AICapability.Text2Text | AICapability.ToolChat | AICapability.Text2Json,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 95,
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

                var response = await this.provider.Call(request).ConfigureAwait(false);
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
                    var model = id;
                    if (!string.IsNullOrWhiteSpace(model)) models.Add(model);
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
