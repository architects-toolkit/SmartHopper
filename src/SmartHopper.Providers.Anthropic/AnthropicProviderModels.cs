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
                // Released between April 2026 and July 2026

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "claude-sonnet-5",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 10000,
                    ContextLimit = 1000000,
                    Created = new DateTime(2026, 6, 30),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.000002m,
                        Completion = 0.00001m,
                        InputCacheRead = 0.0000002m,
                        InputCacheWrite = 0.0000025m,
                        WebSearch = 0.01m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "claude-opus-4-8",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9995,
                    ContextLimit = 1000000,
                    Created = new DateTime(2026, 5, 27),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.000005m,
                        Completion = 0.000025m,
                        InputCacheRead = 0.0000005m,
                        InputCacheWrite = 0.00000625m,
                        WebSearch = 0.01m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "claude-fable-5",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9990,
                    ContextLimit = 1000000,
                    Created = new DateTime(2026, 6, 9),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00001m,
                        Completion = 0.00005m,
                        InputCacheRead = 0.000001m,
                        InputCacheWrite = 0.0000125m,
                        WebSearch = 0.01m,
                    },
                },



                // Released between January 2026 and April 2026

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "claude-sonnet-4-6",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    Default = AICapability.Text2Json,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 10000,
                    ContextLimit = 1000000,
                    Created = new DateTime(2026, 2, 17),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.000003m,
                        Completion = 0.000015m,
                        InputCacheRead = 0.0000003m,
                        InputCacheWrite = 0.00000375m,
                        WebSearch = 0.01m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "claude-opus-4-6",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.ImageInput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9995,
                    ContextLimit = 1000000,
                    Created = new DateTime(2026, 2, 4),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.000005m,
                        Completion = 0.000025m,
                        InputCacheRead = 0.0000005m,
                        InputCacheWrite = 0.00000625m,
                        WebSearch = 0.01m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "claude-opus-4-7",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9980,
                    ContextLimit = 1000000,
                    Created = new DateTime(2026, 4, 16),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.000005m,
                        Completion = 0.000025m,
                        InputCacheRead = 0.0000005m,
                        InputCacheWrite = 0.00000625m,
                        WebSearch = 0.01m,
                    },
                },



                // Released between October 2025 and January 2026

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "claude-opus-4-5-20251101",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.ImageInput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9970,
                    ContextLimit = 200000,
                    Created = new DateTime(2025, 11, 24),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.000005m,
                        Completion = 0.000025m,
                        InputCacheRead = 0.0000005m,
                        InputCacheWrite = 0.00000625m,
                        WebSearch = 0.01m,
                    },
                    Aliases = new List<string> { "claude-opus-4-5", "claude-opus-4-5-latest" },
                },



                // Released between July 2025 and October 2025

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "claude-haiku-4-5-20251001",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    Default = AICapability.Text2Text | AICapability.ReasoningChat | AICapability.ToolReasoningChat | AICapability.ToolChat | AICapability.Image2Text,
                    SupportsStreaming = true,
                    Verified = true,
                    Rank = 9965,
                    ContextLimit = 200000,
                    Created = new DateTime(2025, 10, 15),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.000001m,
                        Completion = 0.000005m,
                        InputCacheRead = 0.0000001m,
                        InputCacheWrite = 0.00000125m,
                        WebSearch = 0.01m,
                    },
                    Aliases = new List<string> { "claude-haiku-4-5", "claude-haiku-4-5-latest" },
                    DiscouragedForTools = new List<string> { "script_generate", "script_edit" },
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "claude-sonnet-4-5-20250929",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.ImageInput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = true,
                    Rank = 9960,
                    ContextLimit = 200000,
                    Created = new DateTime(2025, 9, 29),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.000003m,
                        Completion = 0.000015m,
                        InputCacheRead = 0.0000003m,
                        InputCacheWrite = 0.00000375m,
                        WebSearch = 0.01m,
                    },
                    Aliases = new List<string> { "claude-sonnet-4-5", "claude-sonnet-4-5-latest" },
                },



                // Deprecated models

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "claude-opus-4-1-20250805",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.ImageInput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = 0,
                    ContextLimit = 200000,
                    Created = new DateTime(2025, 8, 5),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.000015m,
                        Completion = 0.000075m,
                        InputCacheRead = 0.0000015m,
                        InputCacheWrite = 0.00001875m,
                        WebSearch = 0.01m,
                    },
                    Aliases = new List<string> { "claude-opus-4-1", "claude-opus-4-1-latest" },
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "claude-sonnet-4-20250514",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.ImageInput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -5,
                    ContextLimit = 200000,
                    Created = new DateTime(2025, 5, 22),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.000003m,
                        Completion = 0.000015m,
                        InputCacheRead = 0.0000003m,
                        InputCacheWrite = 0.00000375m,
                        WebSearch = 0.01m,
                    },
                    Aliases = new List<string> { "claude-sonnet-4", "claude-sonnet-4-latest", "claude-sonnet-4-0", "claude-sonnet-4-0-latest" },
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "claude-opus-4-20250514",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.ImageInput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -10,
                    ContextLimit = 200000,
                    Created = new DateTime(2025, 5, 22),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.000015m,
                        Completion = 0.000075m,
                        InputCacheRead = 0.0000015m,
                        InputCacheWrite = 0.00001875m,
                        WebSearch = 0.01m,
                    },
                    Aliases = new List<string> { "claude-opus-4", "claude-opus-4-latest", "claude-opus-4-0", "claude-opus-4-0-latest" },
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "claude-3-haiku-20240307",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.ImageInput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -15,
                    ContextLimit = 200000,
                    Created = new DateTime(2025, 2, 24),
                    Aliases = new List<string> { "claude-3-7-sonnet", "claude-3-7-sonnet-latest" },
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "claude-3-5-haiku-20241022",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.ImageInput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -20,
                    ContextLimit = 200000,
                    Created = new DateTime(2024, 11, 4),
                    Aliases = new List<string> { "claude-3-5-haiku", "claude-3-5-haiku-latest" },
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
                    Rank = -25,
                    ContextLimit = 200000,
                    Created = new DateTime(2024, 3, 13),
                    Aliases = new List<string> { "claude-3-haiku", "claude-3-haiku-latest" },
                    DiscouragedForTools = new List<string> { "script_generate", "script_edit" },
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
                    if (!string.IsNullOrWhiteSpace(model))
                    {
                        models.Add(model);
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
