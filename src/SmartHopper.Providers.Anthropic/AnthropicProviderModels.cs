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
            var providerName = this.provider.Name.ToLowerInvariant();

            var models = new List<AIModelCapabilities>
            {
                // Released between February 2026 and May 2026

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "claude-sonnet-4-6",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 1000,
                    ContextLimit = 1000000,
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "claude-opus-4-6",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 995,
                    ContextLimit = 1000000,
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "claude-opus-4-7",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 990,
                    ContextLimit = 1000000,
                },



                // Released before May 2024 or unknown release date

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "claude-haiku-4-5-20251001",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.ImageInput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = true,
                    Rank = 985,
                    ContextLimit = 200000,
                    Aliases = new List<string> { "claude-haiku-4-5" },
                    DiscouragedForTools = new List<string> { "script_generate", "script_edit" },
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "claude-sonnet-4-5-20250929",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.ImageInput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = true,
                    Rank = 980,
                    ContextLimit = 200000,
                    Aliases = new List<string> { "claude-sonnet-4-5" },
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "claude-opus-4-5-20251101",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.ImageInput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 975,
                    ContextLimit = 200000,
                    Aliases = new List<string> { "claude-opus-4-5" },
                },



                // Deprecated models

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "claude-3-5-haiku-20241022",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.ImageInput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = 0,
                    ContextLimit = 200000,
                    DiscouragedForTools = new List<string> { "script_generate", "script_edit" },
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "claude-3-5-haiku-latest",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.ImageInput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -5,
                    ContextLimit = 200000,
                    DiscouragedForTools = new List<string> { "script_generate", "script_edit" },
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "claude-3-7-sonnet-20250219",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.ImageInput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -10,
                    ContextLimit = 200000,
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "claude-3-7-sonnet-latest",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.ImageInput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -15,
                    ContextLimit = 200000,
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "claude-3-haiku-20240307",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.ImageInput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -20,
                    ContextLimit = 200000,
                    DiscouragedForTools = new List<string> { "script_generate", "script_edit" },
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "claude-opus-4-0",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.ImageInput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -25,
                    ContextLimit = 200000,
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "claude-opus-4-1-20250805",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.ImageInput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -30,
                    ContextLimit = 200000,
                    Aliases = new List<string> { "claude-opus-4-1" },
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "claude-opus-4-20250514",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.ImageInput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -35,
                    ContextLimit = 200000,
                    Aliases = new List<string> { "claude-opus-4" },
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "claude-sonnet-4-0",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.ImageInput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -40,
                    ContextLimit = 200000,
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "claude-sonnet-4-20250514",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.ImageInput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -45,
                    ContextLimit = 200000,
                    Aliases = new List<string> { "claude-sonnet-4" },
                }
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
                if (response == null)
                {
                    return new List<string>();
                }

                var raw = (response as AIReturn)?.Raw;
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
