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
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SmartHopper.Infrastructure.AIProviders;
using SmartHopper.ProviderSdk.AICall.Core.Requests;
using SmartHopper.ProviderSdk.AICall.Core.Returns;
using SmartHopper.ProviderSdk.AIModels;
using SmartHopper.ProviderSdk.AIProviders;

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

            var models = new List<AIModelCapabilities>
            {
                // Released between April 2026 and July 2026

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openrouter/auto-beta",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.AudioInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.ImageOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    Verified = false,
                    Rank = 10000,
                    ContextLimit = 2000000,
                    Created = new DateTime(2026, 7, 17),
                    Pricing = new AIModelPricing
                    {
                        Prompt = -1m,
                        Completion = -1m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openrouter/fusion",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput,
                    Verified = false,
                    Rank = 9995,
                    ContextLimit = 1000000,
                    Created = new DateTime(2026, 6, 13),
                    Pricing = new AIModelPricing
                    {
                        Prompt = -1m,
                        Completion = -1m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openrouter/pareto-code",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 10000,
                    ContextLimit = 200000,
                    Created = new DateTime(2026, 4, 21),
                    Pricing = new AIModelPricing
                    {
                        Prompt = -1m,
                        Completion = -1m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "baidu/qianfan-ocr-fast:free",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9995,
                    ContextLimit = 65536,
                    Created = new DateTime(2026, 4, 20),
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "google/gemma-4-26b-a4b-it:free",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9990,
                    ContextLimit = 262144,
                    Created = new DateTime(2026, 4, 3),
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "poolside/laguna-xs-2.1:free",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    Verified = false,
                    Rank = 9985,
                    ContextLimit = 262144,
                    Created = new DateTime(2026, 4, 2),
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "cohere/north-mini-code:free",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    Verified = false,
                    Rank = 9980,
                    ContextLimit = 1048576,
                    Created = new DateTime(2026, 3, 30),
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "nvidia/nemotron-3.5-content-safety:free",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.Reasoning,
                    Verified = false,
                    Rank = 9975,
                    ContextLimit = 1048576,
                    Created = new DateTime(2026, 3, 30),
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "nvidia/nemotron-3-ultra-550b-a55b:free",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    Verified = false,
                    Rank = 9970,
                    ContextLimit = 196608,
                    Created = new DateTime(2026, 2, 12),
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "nvidia/nemotron-3-nano-omni-30b-a3b-reasoning:free",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.AudioInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9965,
                    ContextLimit = 256000,
                    Created = new DateTime(2026, 4, 28),
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "nvidia/nemotron-3-super-120b-a12b:free",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9960,
                    ContextLimit = 262144,
                    Created = new DateTime(2026, 3, 11),
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openrouter/owl-alpha",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9955,
                    ContextLimit = 1048756,
                    Created = new DateTime(2026, 4, 28),
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "poolside/laguna-m.1:free",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9950,
                    ContextLimit = 131072,
                    Created = new DateTime(2026, 4, 28),
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "poolside/laguna-xs.2:free",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9945,
                    ContextLimit = 131072,
                    Created = new DateTime(2026, 4, 28),
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "ibm-granite/granite-4.1-8b",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9940,
                    ContextLimit = 131072,
                    Created = new DateTime(2026, 4, 30),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000005m,
                        Completion = 0.0000001m,
                        InputCacheRead = 0.00000005m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "rekaai/reka-edge",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9935,
                    ContextLimit = 16384,
                    Created = new DateTime(2026, 3, 20),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000001m,
                        Completion = 0.0000001m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "liquid/lfm-2-24b-a2b",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9930,
                    ContextLimit = 32768,
                    Created = new DateTime(2026, 2, 25),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000003m,
                        Completion = 0.00000012m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen3.5-9b",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9925,
                    ContextLimit = 262144,
                    Created = new DateTime(2026, 3, 10),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000001m,
                        Completion = 0.00000015m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "inclusionai/ling-2.6-flash",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9960,
                    ContextLimit = 262144,
                    Created = new DateTime(2026, 4, 21),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000008m,
                        Completion = 0.00000024m,
                        InputCacheRead = 0.000000016m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "nex-agi/nex-n2-mini",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    Verified = false,
                    Rank = 9955,
                    ContextLimit = 262144,
                    Created = new DateTime(2026, 6, 24),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.000000025m,
                        Completion = 0.0000001m,
                        InputCacheRead = 0.0000000025m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "ibm-granite/granite-4.1-8b",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9915,
                    ContextLimit = 1000000,
                    Created = new DateTime(2026, 2, 25),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.000000065m,
                        Completion = 0.00000026m,
                        InputCacheWrite = 0.00000008125m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "deepseek/deepseek-v4-flash",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9940,
                    ContextLimit = 1048576,
                    Created = new DateTime(2026, 4, 24),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000014m,
                        Completion = 0.00000028m,
                        InputCacheRead = 0.0000000028m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "tencent/hy3-preview",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    Verified = false,
                    Rank = 9905,
                    ContextLimit = 262144,
                    Created = new DateTime(2026, 4, 3),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000006m,
                        Completion = 0.00000033m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "google/gemma-4-31b-it",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9900,
                    ContextLimit = 262144,
                    Created = new DateTime(2026, 4, 2),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000013m,
                        Completion = 0.00000038m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "bytedance-seed/seed-2.0-mini",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9895,
                    ContextLimit = 262144,
                    Created = new DateTime(2026, 2, 26),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000001m,
                        Completion = 0.0000004m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "nvidia/nemotron-3-super-120b-a12b",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9890,
                    ContextLimit = 262144,
                    Created = new DateTime(2026, 3, 11),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000009m,
                        Completion = 0.00000045m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "mistralai/mistral-small-2603",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9885,
                    ContextLimit = 262144,
                    Created = new DateTime(2026, 3, 16),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000015m,
                        Completion = 0.0000006m,
                        InputCacheRead = 0.000000015m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "inception/mercury-2",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9880,
                    ContextLimit = 128000,
                    Created = new DateTime(2026, 3, 4),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000025m,
                        Completion = 0.00000075m,
                        InputCacheRead = 0.000000025m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "arcee-ai/trinity-large-thinking",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9875,
                    ContextLimit = 262144,
                    Created = new DateTime(2026, 4, 1),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000022m,
                        Completion = 0.00000085m,
                        InputCacheRead = 0.00000006m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "deepseek/deepseek-v4-pro",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9870,
                    ContextLimit = 1048576,
                    Created = new DateTime(2026, 4, 24),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.000000435m,
                        Completion = 0.00000087m,
                        InputCacheRead = 0.000000003625m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen3.5-35b-a3b",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9865,
                    ContextLimit = 262144,
                    Created = new DateTime(2026, 2, 25),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000015m,
                        Completion = 0.000001m,
                        InputCacheRead = 0.00000005m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen3.6-35b-a3b",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9860,
                    ContextLimit = 262144,
                    Created = new DateTime(2026, 4, 27),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000015m,
                        Completion = 0.000001m,
                        InputCacheRead = 0.00000005m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "minimax/minimax-m2.5",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9855,
                    ContextLimit = 196608,
                    Created = new DateTime(2026, 2, 12),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000015m,
                        Completion = 0.00000115m,
                        InputCacheRead = 0.00000003m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "kwaipilot/kat-coder-pro-v2",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9850,
                    ContextLimit = 256000,
                    Created = new DateTime(2026, 3, 27),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000003m,
                        Completion = 0.0000012m,
                        InputCacheRead = 0.00000006m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "minimax/minimax-m2.7",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9845,
                    ContextLimit = 196608,
                    Created = new DateTime(2026, 3, 18),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000003m,
                        Completion = 0.0000012m,
                        InputCacheRead = 0.000000059m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-5.4-nano",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9840,
                    ContextLimit = 400000,
                    Created = new DateTime(2026, 3, 17),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000002m,
                        Completion = 0.00000125m,
                        InputCacheRead = 0.00000002m,
                        WebSearch = 0.01m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen3.6-flash",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9835,
                    ContextLimit = 1000000,
                    Created = new DateTime(2026, 4, 27),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000025m,
                        Completion = 0.0000015m,
                        InputCacheWrite = 0.0000003125m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen3.5-27b",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9830,
                    ContextLimit = 262144,
                    Created = new DateTime(2026, 2, 25),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.000000195m,
                        Completion = 0.00000156m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen3.5-plus-02-15",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9825,
                    ContextLimit = 1000000,
                    Created = new DateTime(2026, 2, 16),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000026m,
                        Completion = 0.00000156m,
                        InputCacheWrite = 0.000000325m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "aion-labs/aion-2.0",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9820,
                    ContextLimit = 131072,
                    Created = new DateTime(2026, 2, 23),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000008m,
                        Completion = 0.0000016m,
                        InputCacheRead = 0.0000002m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen3.6-plus",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9815,
                    ContextLimit = 1000000,
                    Created = new DateTime(2026, 4, 2),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.000000325m,
                        Completion = 0.00000195m,
                        InputCacheWrite = 0.00000040625m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "bytedance-seed/seed-2.0-lite",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9810,
                    ContextLimit = 262144,
                    Created = new DateTime(2026, 3, 10),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000025m,
                        Completion = 0.000002m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "xiaomi/mimo-v2-omni",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.AudioInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9805,
                    ContextLimit = 262144,
                    Created = new DateTime(2026, 3, 18),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000004m,
                        Completion = 0.000002m,
                        InputCacheRead = 0.00000008m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "xiaomi/mimo-v2.5",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.AudioInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9800,
                    ContextLimit = 1048576,
                    Created = new DateTime(2026, 4, 22),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000004m,
                        Completion = 0.000002m,
                        InputCacheRead = 0.00000008m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "kwaipilot/kat-coder-air-v2.5",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    Verified = false,
                    Rank = 9795,
                    ContextLimit = 262144,
                    Created = new DateTime(2026, 2, 25),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000026m,
                        Completion = 0.00000208m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "inclusionai/ring-2.6-1t",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    Verified = false,
                    Rank = 9920,
                    ContextLimit = 262144,
                    Created = new DateTime(2026, 5, 8),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.000000075m,
                        Completion = 0.000000625m,
                        InputCacheRead = 0.000000015m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "inclusionai/ling-2.6-1t",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    Verified = false,
                    Rank = 9915,
                    ContextLimit = 262144,
                    Created = new DateTime(2026, 4, 23),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.000000075m,
                        Completion = 0.000000625m,
                        InputCacheRead = 0.000000015m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "tencent/hy3",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    Verified = false,
                    Rank = 9910,
                    ContextLimit = 262144,
                    Created = new DateTime(2026, 7, 6),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000002m,
                        Completion = 0.0000008m,
                        InputCacheRead = 0.00000005m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "deepseek/deepseek-v4-pro",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9790,
                    ContextLimit = 202752,
                    Created = new DateTime(2026, 2, 11),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000006m,
                        Completion = 0.00000208m,
                        InputCacheRead = 0.00000012m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "google/gemini-3.1-flash-lite-preview",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.AudioInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9785,
                    ContextLimit = 1048576,
                    Created = new DateTime(2026, 3, 3),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000025m,
                        Completion = 0.0000015m,
                        Image = 0.00000025m,
                        Audio = 0.0000005m,
                        InputCacheRead = 0.000000025m,
                        InputCacheWrite = 0.00000008333333333333334m,
                        InternalReasoning = 0.0000015m,
                        WebSearch = 0.014m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen3.5-397b-a17b",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9780,
                    ContextLimit = 262144,
                    Created = new DateTime(2026, 2, 16),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000039m,
                        Completion = 0.00000234m,
                        InputCacheRead = 0.000000195m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen3.5-plus-20260420",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9775,
                    ContextLimit = 1000000,
                    Created = new DateTime(2026, 4, 27),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000004m,
                        Completion = 0.0000024m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "x-ai/grok-4.20",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9770,
                    ContextLimit = 2000000,
                    Created = new DateTime(2026, 3, 31),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000125m,
                        Completion = 0.0000025m,
                        InputCacheRead = 0.0000002m,
                        WebSearch = 0.005m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "x-ai/grok-4.3",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9765,
                    ContextLimit = 1000000,
                    Created = new DateTime(2026, 4, 30),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000125m,
                        Completion = 0.0000025m,
                        InputCacheRead = 0.0000002m,
                        WebSearch = 0.005m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "google/gemini-3.1-flash-image-preview",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.ImageOutput | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9760,
                    ContextLimit = 65536,
                    Created = new DateTime(2026, 2, 26),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000005m,
                        Completion = 0.000003m,
                        WebSearch = 0.014m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "xiaomi/mimo-v2-pro",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9755,
                    ContextLimit = 1048576,
                    Created = new DateTime(2026, 3, 18),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.000001m,
                        Completion = 0.000003m,
                        InputCacheRead = 0.0000002m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "xiaomi/mimo-v2.5-pro",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9750,
                    ContextLimit = 1048576,
                    Created = new DateTime(2026, 4, 22),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.000001m,
                        Completion = 0.000003m,
                        InputCacheRead = 0.0000002m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen3.6-27b",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9745,
                    ContextLimit = 262144,
                    Created = new DateTime(2026, 4, 27),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000032m,
                        Completion = 0.0000032m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "kwaipilot/kat-coder-pro-v2.5",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    Verified = false,
                    Rank = 9740,
                    ContextLimit = 262142,
                    Created = new DateTime(2026, 4, 27),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000074m,
                        Completion = 0.00000349m,
                        InputCacheRead = 0.00000014m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "moonshotai/kimi-k2.6",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9735,
                    ContextLimit = 262142,
                    Created = new DateTime(2026, 4, 20),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000074m,
                        Completion = 0.00000349m,
                        InputCacheRead = 0.00000014m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "z-ai/glm-5.1",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9730,
                    ContextLimit = 202752,
                    Created = new DateTime(2026, 4, 7),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000105m,
                        Completion = 0.0000035m,
                        InputCacheRead = 0.000000525m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen3-max-thinking",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9725,
                    ContextLimit = 262144,
                    Created = new DateTime(2026, 2, 9),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000078m,
                        Completion = 0.0000039m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "nvidia/nemotron-3-ultra-550b-a55b",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    Verified = false,
                    Rank = 9720,
                    ContextLimit = 202752,
                    Created = new DateTime(2026, 3, 15),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000012m,
                        Completion = 0.000004m,
                        InputCacheRead = 0.00000024m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "moonshotai/kimi-k2.7-code",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    Verified = false,
                    Rank = 9715,
                    ContextLimit = 202752,
                    Created = new DateTime(2026, 4, 1),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000012m,
                        Completion = 0.000004m,
                        InputCacheRead = 0.00000024m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "thinkingmachines/inkling",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.AudioInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    Verified = false,
                    Rank = 9710,
                    ContextLimit = 1048576,
                    Created = new DateTime(2026, 4, 27),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000005m,
                        Completion = 0.000003m,
                        Image = 0.0000005m,
                        Audio = 0.000001m,
                        InputCacheRead = 0.00000005m,
                        InputCacheWrite = 0.00000008333333333333334m,
                        InternalReasoning = 0.000003m,
                        WebSearch = 0.014m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "~openai/gpt-mini-latest",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9705,
                    ContextLimit = 400000,
                    Created = new DateTime(2026, 4, 27),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000075m,
                        Completion = 0.0000045m,
                        InputCacheRead = 0.000000075m,
                        WebSearch = 0.01m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-5.4-mini",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9700,
                    ContextLimit = 400000,
                    Created = new DateTime(2026, 3, 17),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000075m,
                        Completion = 0.0000045m,
                        InputCacheRead = 0.000000075m,
                        WebSearch = 0.01m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "~anthropic/claude-haiku-latest",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9695,
                    ContextLimit = 200000,
                    Created = new DateTime(2026, 4, 27),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.000001m,
                        Completion = 0.000005m,
                        InputCacheRead = 0.0000001m,
                        InputCacheWrite = 0.00000125m,
                        WebSearch = 0.01m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-5.6-luna-pro",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    Verified = false,
                    Rank = 9690,
                    ContextLimit = 2000000,
                    Created = new DateTime(2026, 3, 31),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.000002m,
                        Completion = 0.000006m,
                        InputCacheRead = 0.0000002m,
                        WebSearch = 0.005m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen3.6-max-preview",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9685,
                    ContextLimit = 262144,
                    Created = new DateTime(2026, 4, 27),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000104m,
                        Completion = 0.00000624m,
                        InputCacheWrite = 0.0000013m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-5.3-chat",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9680,
                    ContextLimit = 128000,
                    Created = new DateTime(2026, 3, 3),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000175m,
                        Completion = 0.000014m,
                        InputCacheRead = 0.000000175m,
                        WebSearch = 0.01m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-5.3-codex",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    Verified = false,
                    Rank = 9675,
                    ContextLimit = 400000,
                    Created = new DateTime(2026, 2, 24),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000175m,
                        Completion = 0.000014m,
                        InputCacheRead = 0.000000175m,
                        WebSearch = 0.01m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "~anthropic/claude-sonnet-latest",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9670,
                    ContextLimit = 1000000,
                    Created = new DateTime(2026, 4, 27),
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
                    Provider = provider,
                    Model = "google/gemini-3.5-flash",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.AudioInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    Verified = false,
                    Rank = 9665,
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
                    Provider = provider,
                    Model = "~google/gemini-flash-latest",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.AudioInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9660,
                    ContextLimit = 1050000,
                    Created = new DateTime(2026, 3, 5),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000025m,
                        Completion = 0.000015m,
                        InputCacheRead = 0.00000025m,
                        WebSearch = 0.01m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-5.6-terra",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    Verified = false,
                    Rank = 9715,
                    ContextLimit = 1050000,
                    Created = new DateTime(2026, 7, 9),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000025m,
                        Completion = 0.000015m,
                        InputCacheRead = 0.00000025m,
                        InputCacheWrite = 0.000003125m,
                        WebSearch = 0.01m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "~moonshotai/kimi-latest",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9655,
                    ContextLimit = 272000,
                    Created = new DateTime(2026, 4, 21),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.000008m,
                        Completion = 0.000015m,
                        InputCacheRead = 0.000002m,
                        WebSearch = 0.01m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "~google/gemini-pro-latest",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.AudioInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9650,
                    ContextLimit = 1048576,
                    Created = new DateTime(2026, 4, 27),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.000002m,
                        Completion = 0.000012m,
                        Image = 0.000002m,
                        Audio = 0.000002m,
                        InputCacheRead = 0.0000002m,
                        InputCacheWrite = 0.000000375m,
                        InternalReasoning = 0.000012m,
                        WebSearch = 0.014m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "anthropic/claude-opus-4.8",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    Verified = false,
                    Rank = 9645,
                    ContextLimit = 1048576,
                    Created = new DateTime(2026, 2, 19),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.000002m,
                        Completion = 0.000012m,
                        Image = 0.000002m,
                        Audio = 0.000002m,
                        InputCacheRead = 0.0000002m,
                        InputCacheWrite = 0.000000375m,
                        InternalReasoning = 0.000012m,
                        WebSearch = 0.014m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "google/gemini-3.1-pro-preview-customtools",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.AudioInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9640,
                    ContextLimit = 1048576,
                    Created = new DateTime(2026, 2, 25),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.000002m,
                        Completion = 0.000012m,
                        Image = 0.000002m,
                        Audio = 0.000002m,
                        InputCacheRead = 0.0000002m,
                        InputCacheWrite = 0.000000375m,
                        InternalReasoning = 0.000012m,
                        WebSearch = 0.014m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "~anthropic/claude-opus-latest",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9635,
                    ContextLimit = 1000000,
                    Created = new DateTime(2026, 4, 21),
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
                    Provider = provider,
                    Model = "openai/gpt-5.6-sol-pro",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    Verified = false,
                    Rank = 9630,
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
                    Provider = provider,
                    Model = "openai/gpt-5.6-sol",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    Verified = false,
                    Rank = 9625,
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

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "~openai/gpt-latest",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9620,
                    ContextLimit = 1050000,
                    Created = new DateTime(2026, 4, 27),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.000005m,
                        Completion = 0.00003m,
                        InputCacheRead = 0.0000005m,
                        WebSearch = 0.01m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-5.5",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9615,
                    ContextLimit = 1050000,
                    Created = new DateTime(2026, 4, 24),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.000005m,
                        Completion = 0.00003m,
                        InputCacheRead = 0.0000005m,
                        WebSearch = 0.01m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "google/gemini-3.1-flash-lite-image",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.ImageOutput | AICapability.JsonOutput | AICapability.Reasoning,
                    Default = AICapability.Text2Image | AICapability.Image2Image | AICapability.Image2Text,
                    Verified = false,
                    Rank = 9660,
                    ContextLimit = 65536,
                    Created = new DateTime(2026, 6, 30),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000025m,
                        Completion = 0.0000015m,
                        ImageOutput = 0.00003m,
                        WebSearch = 0.014m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-5.4-image-2",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.ImageOutput | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9610,
                    ContextLimit = 1000000,
                    Created = new DateTime(2026, 4, 7),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00003m,
                        Completion = 0.00015m,
                        InputCacheRead = 0.000003m,
                        InputCacheWrite = 0.0000375m,
                        WebSearch = 0.01m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "anthropic/claude-fable-5",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    Verified = false,
                    Rank = 9605,
                    ContextLimit = 1050000,
                    Created = new DateTime(2026, 3, 5),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00003m,
                        Completion = 0.00018m,
                        WebSearch = 0.01m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-5.5-pro",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9600,
                    ContextLimit = 1050000,
                    Created = new DateTime(2026, 4, 24),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00003m,
                        Completion = 0.00018m,
                        WebSearch = 0.01m,
                    },
                },



                // Released between January 2026 and April 2026

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "google/gemma-4-26b-a4b-it:free",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9595,
                    ContextLimit = 128000,
                    Created = new DateTime(2025, 12, 5),
                    Pricing = new AIModelPricing
                    {
                        Prompt = -1m,
                        Completion = -1m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "google/gemma-4-31b-it:free",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9590,
                    ContextLimit = 32768,
                    Created = new DateTime(2026, 1, 20),
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "google/lyria-3-pro-preview",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.AudioOutput | AICapability.JsonOutput,
                    Default = AICapability.Text2Speech,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9585,
                    ContextLimit = 32768,
                    Created = new DateTime(2026, 1, 20),
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "google/lyria-3-clip-preview",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.AudioOutput | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9580,
                    ContextLimit = 256000,
                    Created = new DateTime(2025, 12, 14),
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openrouter/free",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9575,
                    ContextLimit = 200000,
                    Created = new DateTime(2026, 2, 1),
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "rekaai/reka-edge",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9570,
                    ContextLimit = 131072,
                    Created = new DateTime(2025, 12, 2),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000001m,
                        Completion = 0.0000001m,
                        InputCacheRead = 0.00000001m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "arcee-ai/trinity-mini",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9565,
                    ContextLimit = 131072,
                    Created = new DateTime(2025, 12, 1),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.000000045m,
                        Completion = 0.00000015m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "essentialai/rnj-1-instruct",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9560,
                    ContextLimit = 32768,
                    Created = new DateTime(2025, 12, 7),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000015m,
                        Completion = 0.00000015m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "mistralai/ministral-8b-2512",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9555,
                    ContextLimit = 262144,
                    Created = new DateTime(2025, 12, 2),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000015m,
                        Completion = 0.00000015m,
                        InputCacheRead = 0.000000015m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "mistralai/ministral-14b-2512",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9550,
                    ContextLimit = 262144,
                    Created = new DateTime(2025, 12, 2),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000002m,
                        Completion = 0.0000002m,
                        InputCacheRead = 0.00000002m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "nvidia/nemotron-3-nano-30b-a3b",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9545,
                    ContextLimit = 262144,
                    Created = new DateTime(2025, 12, 14),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000005m,
                        Completion = 0.0000002m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "xiaomi/mimo-v2-flash",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9540,
                    ContextLimit = 262144,
                    Created = new DateTime(2025, 12, 14),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000009m,
                        Completion = 0.00000029m,
                        InputCacheRead = 0.000000045m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "bytedance-seed/seed-1.6-flash",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9535,
                    ContextLimit = 262144,
                    Created = new DateTime(2025, 12, 23),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.000000075m,
                        Completion = 0.0000003m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "stepfun/step-3.5-flash",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9530,
                    ContextLimit = 262144,
                    Created = new DateTime(2026, 1, 29),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000001m,
                        Completion = 0.0000003m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "google/gemma-4-26b-a4b-it",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9525,
                    ContextLimit = 131072,
                    Created = new DateTime(2025, 12, 1),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.000000252m,
                        Completion = 0.000000378m,
                        InputCacheRead = 0.0000000252m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "z-ai/glm-4.7-flash",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9520,
                    ContextLimit = 202752,
                    Created = new DateTime(2026, 1, 19),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000006m,
                        Completion = 0.0000004m,
                        InputCacheRead = 0.00000001m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "nvidia/nemotron-3-super-120b-a12b",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9515,
                    ContextLimit = 131000,
                    Created = new DateTime(2026, 1, 27),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000015m,
                        Completion = 0.00000045m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "google/gemma-4-31b-it",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9510,
                    ContextLimit = 65536,
                    Created = new DateTime(2025, 11, 21),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000015m,
                        Completion = 0.0000005m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "nex-agi/deepseek-v3.1-nex-n1",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9505,
                    ContextLimit = 131072,
                    Created = new DateTime(2025, 12, 8),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.000000135m,
                        Completion = 0.0000005m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "x-ai/grok-4.1-fast",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9500,
                    ContextLimit = 2000000,
                    Created = new DateTime(2025, 11, 19),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000002m,
                        Completion = 0.0000005m,
                        InputCacheRead = 0.00000005m,
                        WebSearch = 0.005m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "upstage/solar-pro-3",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9495,
                    ContextLimit = 128000,
                    Created = new DateTime(2026, 1, 27),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000015m,
                        Completion = 0.0000006m,
                        InputCacheRead = 0.000000015m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen3-coder-next",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9490,
                    ContextLimit = 262144,
                    Created = new DateTime(2026, 2, 4),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000012m,
                        Completion = 0.0000008m,
                        InputCacheRead = 0.00000007m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "z-ai/glm-4.6v",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9485,
                    ContextLimit = 131072,
                    Created = new DateTime(2025, 12, 8),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000003m,
                        Completion = 0.0000009m,
                        InputCacheRead = 0.00000005m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "minimax/minimax-m2.1",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9480,
                    ContextLimit = 196608,
                    Created = new DateTime(2025, 12, 23),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000029m,
                        Completion = 0.00000095m,
                        InputCacheRead = 0.00000003m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "arcee-ai/trinity-large-thinking",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9525,
                    ContextLimit = 262144,
                    Created = new DateTime(2026, 4, 1),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000025m,
                        Completion = 0.0000008m,
                        InputCacheRead = 0.00000006m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen3-coder-next",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9520,
                    ContextLimit = 262144,
                    Created = new DateTime(2026, 2, 4),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000011m,
                        Completion = 0.0000008m,
                        InputCacheRead = 0.00000007m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "minimax/minimax-m2.5",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9475,
                    ContextLimit = 131072,
                    Created = new DateTime(2025, 11, 27),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000002m,
                        Completion = 0.0000011m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "minimax/minimax-m2.7",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9470,
                    ContextLimit = 163840,
                    Created = new DateTime(2025, 12, 1),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000004m,
                        Completion = 0.0000012m,
                        InputCacheRead = 0.0000002m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "minimax/minimax-m2-her",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9495,
                    ContextLimit = 65536,
                    Created = new DateTime(2026, 1, 23),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000003m,
                        Completion = 0.0000012m,
                        InputCacheRead = 0.00000003m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-5.4-nano",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9490,
                    ContextLimit = 400000,
                    Created = new DateTime(2026, 3, 17),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000002m,
                        Completion = 0.00000125m,
                        InputCacheRead = 0.00000002m,
                        WebSearch = 0.01m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen3.5-plus-02-15",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9485,
                    ContextLimit = 1000000,
                    Created = new DateTime(2026, 2, 16),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000026m,
                        Completion = 0.00000156m,
                        InputCacheWrite = 0.000000325m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "aion-labs/aion-2.0",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9480,
                    ContextLimit = 131072,
                    Created = new DateTime(2026, 2, 23),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000008m,
                        Completion = 0.0000016m,
                        InputCacheRead = 0.0000002m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen3.6-plus",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9475,
                    ContextLimit = 1000000,
                    Created = new DateTime(2026, 4, 2),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.000000325m,
                        Completion = 0.00000195m,
                        InputCacheWrite = 0.00000040625m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "bytedance-seed/seed-2.0-lite",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9470,
                    ContextLimit = 262144,
                    Created = new DateTime(2026, 3, 10),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000025m,
                        Completion = 0.000002m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen3.5-122b-a10b",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9465,
                    ContextLimit = 262144,
                    Created = new DateTime(2026, 2, 25),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000026m,
                        Completion = 0.00000208m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "google/gemini-3.1-flash-lite-preview",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.AudioInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9460,
                    ContextLimit = 128000,
                    Created = new DateTime(2025, 11, 13),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000125m,
                        Completion = 0.00000125m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen3.5-397b-a17b",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9455,
                    ContextLimit = 262144,
                    Created = new DateTime(2025, 12, 1),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000005m,
                        Completion = 0.0000015m,
                        InputCacheRead = 0.00000005m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "x-ai/grok-4.20-multi-agent",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9450,
                    ContextLimit = 202752,
                    Created = new DateTime(2025, 12, 22),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000004m,
                        Completion = 0.00000175m,
                        InputCacheRead = 0.00000008m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "x-ai/grok-4.20",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9445,
                    ContextLimit = 262144,
                    Created = new DateTime(2025, 12, 23),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000025m,
                        Completion = 0.000002m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen3.5-27b",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9440,
                    ContextLimit = 262144,
                    Created = new DateTime(2025, 12, 9),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000004m,
                        Completion = 0.000002m,
                        InputCacheRead = 0.00000004m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "moonshotai/kimi-k2.5",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9435,
                    ContextLimit = 262144,
                    Created = new DateTime(2026, 1, 27),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000044m,
                        Completion = 0.000002m,
                        InputCacheRead = 0.00000022m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "z-ai/glm-5.1",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9430,
                    ContextLimit = 400000,
                    Created = new DateTime(2025, 11, 13),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000025m,
                        Completion = 0.000002m,
                        InputCacheRead = 0.00000003m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "z-ai/glm-5",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9425,
                    ContextLimit = 1000000,
                    Created = new DateTime(2025, 12, 2),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000003m,
                        Completion = 0.0000025m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen3-max-thinking",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9420,
                    ContextLimit = 262144,
                    Created = new DateTime(2025, 11, 6),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000006m,
                        Completion = 0.0000025m,
                        InputCacheRead = 0.00000015m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-audio-mini",
                    Capabilities = AICapability.TextInput | AICapability.AudioInput | AICapability.TextOutput | AICapability.AudioOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9415,
                    ContextLimit = 128000,
                    Created = new DateTime(2026, 1, 19),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000006m,
                        Completion = 0.0000024m,
                        Audio = 0.0000006m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "relace/relace-search",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9410,
                    ContextLimit = 256000,
                    Created = new DateTime(2025, 12, 8),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.000001m,
                        Completion = 0.000003m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "google/gemini-3-flash-preview",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.AudioInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9405,
                    ContextLimit = 1048576,
                    Created = new DateTime(2025, 12, 17),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000005m,
                        Completion = 0.000003m,
                        Image = 0.0000005m,
                        Audio = 0.000001m,
                        InputCacheRead = 0.00000005m,
                        InputCacheWrite = 0.00000008333333333333334m,
                        InternalReasoning = 0.000003m,
                        WebSearch = 0.014m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "writer/palmyra-x5",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9400,
                    ContextLimit = 1040000,
                    Created = new DateTime(2026, 1, 21),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000006m,
                        Completion = 0.000006m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-5.3-codex",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9395,
                    ContextLimit = 400000,
                    Created = new DateTime(2025, 11, 13),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000125m,
                        Completion = 0.00001m,
                        InputCacheRead = 0.00000013m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-5.4",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9390,
                    ContextLimit = 128000,
                    Created = new DateTime(2025, 11, 13),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000125m,
                        Completion = 0.00001m,
                        InputCacheRead = 0.000000125m,
                        WebSearch = 0.01m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "anthropic/claude-sonnet-4.6",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9385,
                    ContextLimit = 400000,
                    Created = new DateTime(2025, 11, 13),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000125m,
                        Completion = 0.00001m,
                        InputCacheRead = 0.000000125m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "google/gemini-3.1-pro-preview-customtools",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.AudioInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9380,
                    ContextLimit = 400000,
                    Created = new DateTime(2025, 12, 4),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000125m,
                        Completion = 0.00001m,
                        InputCacheRead = 0.000000125m,
                        WebSearch = 0.01m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "google/gemini-3.1-pro-preview",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.AudioInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9375,
                    ContextLimit = 400000,
                    Created = new DateTime(2025, 12, 10),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000175m,
                        Completion = 0.000014m,
                        InputCacheRead = 0.000000175m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "anthropic/claude-opus-4.7",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9370,
                    ContextLimit = 128000,
                    Created = new DateTime(2025, 12, 10),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000175m,
                        Completion = 0.000014m,
                        InputCacheRead = 0.000000175m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "anthropic/claude-opus-4.6",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9365,
                    ContextLimit = 400000,
                    Created = new DateTime(2026, 1, 14),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000175m,
                        Completion = 0.000014m,
                        InputCacheRead = 0.000000175m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "google/gemini-3.1-flash-image-preview",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.ImageOutput | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9360,
                    ContextLimit = 65536,
                    Created = new DateTime(2025, 11, 20),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.000002m,
                        Completion = 0.000012m,
                        Image = 0.000002m,
                        Audio = 0.000002m,
                        InputCacheRead = 0.0000002m,
                        InputCacheWrite = 0.000000375m,
                        InternalReasoning = 0.000012m,
                        WebSearch = 0.014m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "anthropic/claude-opus-4.5",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9355,
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
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-audio",
                    Capabilities = AICapability.TextInput | AICapability.AudioInput | AICapability.TextOutput | AICapability.AudioOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9355,
                    ContextLimit = 128000,
                    Created = new DateTime(2026, 1, 19),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000025m,
                        Completion = 0.00001m,
                        Audio = 0.000032m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-5.4-pro",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9345,
                    ContextLimit = 400000,
                    Created = new DateTime(2025, 12, 10),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.000021m,
                        Completion = 0.000168m,
                        WebSearch = 0.01m,
                    },
                },



                // Released between October 2025 and January 2026

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openrouter/bodybuilder",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9345,
                    ContextLimit = 128000,
                    Created = new DateTime(2025, 12, 5),
                    Pricing = new AIModelPricing
                    {
                        Prompt = -1m,
                        Completion = -1m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "nvidia/nemotron-3-nano-30b-a3b:free",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9340,
                    ContextLimit = 256000,
                    Created = new DateTime(2025, 12, 14),
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "nvidia/nemotron-nano-12b-v2-vl:free",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9340,
                    ContextLimit = 128000,
                    Created = new DateTime(2025, 10, 28),
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "nvidia/nemotron-nano-9b-v2:free",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9335,
                    ContextLimit = 128000,
                    Created = new DateTime(2025, 9, 5),
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "mistralai/ministral-3b-2512",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9330,
                    ContextLimit = 131072,
                    Created = new DateTime(2025, 8, 5),
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-oss-20b:free",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9325,
                    ContextLimit = 131072,
                    Created = new DateTime(2025, 8, 5),
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen3-next-80b-a3b-instruct:free",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9320,
                    ContextLimit = 262144,
                    Created = new DateTime(2025, 9, 11),
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "ibm-granite/granite-4.0-h-micro",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9325,
                    ContextLimit = 131000,
                    Created = new DateTime(2025, 10, 20),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.000000017m,
                        Completion = 0.000000112m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "mistralai/ministral-8b-2512",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9320,
                    ContextLimit = 262144,
                    Created = new DateTime(2025, 12, 2),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000015m,
                        Completion = 0.00000015m,
                        InputCacheRead = 0.000000015m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "nvidia/nemotron-3-nano-30b-a3b",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9315,
                    ContextLimit = 131000,
                    Created = new DateTime(2025, 10, 20),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.000000017m,
                        Completion = 0.00000011m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "mistralai/ministral-14b-2512",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9310,
                    ContextLimit = 131072,
                    Created = new DateTime(2025, 8, 5),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000003m,
                        Completion = 0.00000014m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "bytedance-seed/seed-1.6-flash",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9305,
                    ContextLimit = 131072,
                    Created = new DateTime(2025, 9, 5),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000004m,
                        Completion = 0.00000016m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-oss-120b",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9300,
                    ContextLimit = 131072,
                    Created = new DateTime(2025, 8, 5),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.000000039m,
                        Completion = 0.00000018m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "baidu/ernie-4.5-21b-a3b",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9295,
                    ContextLimit = 120000,
                    Created = new DateTime(2025, 8, 12),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000007m,
                        Completion = 0.00000028m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "baidu/ernie-4.5-21b-a3b-thinking",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9290,
                    ContextLimit = 131072,
                    Created = new DateTime(2025, 10, 9),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000007m,
                        Completion = 0.00000028m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-oss-safeguard-20b",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9300,
                    ContextLimit = 131072,
                    Created = new DateTime(2025, 10, 29),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.000000075m,
                        Completion = 0.0000003m,
                        InputCacheRead = 0.000000037m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "nousresearch/hermes-4-70b",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9280,
                    ContextLimit = 131072,
                    Created = new DateTime(2025, 8, 26),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000013m,
                        Completion = 0.0000004m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "nvidia/llama-3.3-nemotron-super-49b-v1.5",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9275,
                    ContextLimit = 131072,
                    Created = new DateTime(2025, 10, 10),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000001m,
                        Completion = 0.0000004m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-5-nano",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9270,
                    ContextLimit = 400000,
                    Created = new DateTime(2025, 8, 7),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000005m,
                        Completion = 0.0000004m,
                        InputCacheRead = 0.00000001m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen3-30b-a3b-thinking-2507",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9265,
                    ContextLimit = 131072,
                    Created = new DateTime(2025, 8, 28),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000008m,
                        Completion = 0.0000004m,
                        InputCacheRead = 0.00000008m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "deepseek/deepseek-v3.2-exp",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9260,
                    ContextLimit = 163840,
                    Created = new DateTime(2025, 9, 29),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000027m,
                        Completion = 0.00000041m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen3-vl-32b-instruct",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9255,
                    ContextLimit = 131072,
                    Created = new DateTime(2025, 10, 23),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.000000104m,
                        Completion = 0.000000416m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "alibaba/tongyi-deepresearch-30b-a3b",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9250,
                    ContextLimit = 131072,
                    Created = new DateTime(2025, 9, 18),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000009m,
                        Completion = 0.00000045m,
                        InputCacheRead = 0.00000009m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen3-vl-8b-instruct",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9245,
                    ContextLimit = 131072,
                    Created = new DateTime(2025, 10, 14),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000008m,
                        Completion = 0.0000005m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "thedrummer/cydonia-24b-v4.1",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9240,
                    ContextLimit = 131072,
                    Created = new DateTime(2025, 9, 27),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000003m,
                        Completion = 0.0000005m,
                        InputCacheRead = 0.00000015m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "x-ai/grok-4-fast",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9235,
                    ContextLimit = 2000000,
                    Created = new DateTime(2025, 9, 19),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000002m,
                        Completion = 0.0000005m,
                        InputCacheRead = 0.00000005m,
                        WebSearch = 0.005m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen3-vl-30b-a3b-instruct",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9230,
                    ContextLimit = 131072,
                    Created = new DateTime(2025, 10, 6),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000013m,
                        Completion = 0.00000052m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "baidu/ernie-4.5-vl-28b-a3b",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9225,
                    ContextLimit = 30000,
                    Created = new DateTime(2025, 8, 12),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000014m,
                        Completion = 0.00000056m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "deepseek/deepseek-chat-v3.1",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9220,
                    ContextLimit = 32768,
                    Created = new DateTime(2025, 8, 21),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000015m,
                        Completion = 0.00000075m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen-plus-2025-07-28",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9215,
                    ContextLimit = 1000000,
                    Created = new DateTime(2025, 9, 8),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000026m,
                        Completion = 0.00000078m,
                        InputCacheWrite = 0.000000325m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen-plus-2025-07-28:thinking",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9210,
                    ContextLimit = 1000000,
                    Created = new DateTime(2025, 9, 8),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000026m,
                        Completion = 0.00000078m,
                        InputCacheWrite = 0.000000325m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen3-next-80b-a3b-thinking",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9205,
                    ContextLimit = 131072,
                    Created = new DateTime(2025, 9, 11),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000000975m,
                        Completion = 0.00000078m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "google/gemini-2.5-flash-lite-preview-09-2025",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.AudioInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9200,
                    ContextLimit = 1048576,
                    Created = new DateTime(2025, 9, 25),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000001m,
                        Completion = 0.0000004m,
                        Image = 0.0000001m,
                        Audio = 0.0000003m,
                        InputCacheRead = 0.00000001m,
                        InputCacheWrite = 0.00000008333333333333334m,
                        InternalReasoning = 0.0000004m,
                        WebSearch = 0.014m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen3-vl-235b-a22b-instruct",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9195,
                    ContextLimit = 262144,
                    Created = new DateTime(2025, 9, 23),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000002m,
                        Completion = 0.00000088m,
                        InputCacheRead = 0.00000011m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "allenai/olmo-3-32b-think",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9285,
                    ContextLimit = 65536,
                    Created = new DateTime(2025, 11, 21),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000015m,
                        Completion = 0.0000005m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "z-ai/glm-4.6v",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9280,
                    ContextLimit = 131072,
                    Created = new DateTime(2025, 12, 8),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000003m,
                        Completion = 0.0000009m,
                        InputCacheRead = 0.000000055m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "minimax/minimax-m2.1",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9190,
                    ContextLimit = 163840,
                    Created = new DateTime(2025, 9, 22),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000027m,
                        Completion = 0.00000095m,
                        InputCacheRead = 0.00000013m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen3-coder-flash",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9185,
                    ContextLimit = 1000000,
                    Created = new DateTime(2025, 9, 17),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.000000195m,
                        Completion = 0.000000975m,
                        InputCacheRead = 0.000000039m,
                        InputCacheWrite = 0.00000024375m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "minimax/minimax-m2",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9270,
                    ContextLimit = 204800,
                    Created = new DateTime(2025, 10, 23),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000003m,
                        Completion = 0.0000012m,
                        InputCacheRead = 0.00000003m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "deepcogito/cogito-v2.1-671b",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9265,
                    ContextLimit = 128000,
                    Created = new DateTime(2025, 11, 13),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000125m,
                        Completion = 0.00000125m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "mistralai/mistral-large-2512",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9260,
                    ContextLimit = 262144,
                    Created = new DateTime(2025, 12, 1),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000005m,
                        Completion = 0.0000015m,
                        InputCacheRead = 0.00000005m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "z-ai/glm-4.7",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9255,
                    ContextLimit = 202752,
                    Created = new DateTime(2025, 12, 22),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000004m,
                        Completion = 0.00000175m,
                        InputCacheRead = 0.00000008m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "bytedance-seed/seed-1.6",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9250,
                    ContextLimit = 262144,
                    Created = new DateTime(2025, 12, 23),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000025m,
                        Completion = 0.000002m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "mistralai/devstral-2512",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9245,
                    ContextLimit = 262144,
                    Created = new DateTime(2025, 12, 9),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000004m,
                        Completion = 0.000002m,
                        InputCacheRead = 0.00000004m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-5.1-codex-mini",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9240,
                    ContextLimit = 400000,
                    Created = new DateTime(2025, 11, 13),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000025m,
                        Completion = 0.000002m,
                        InputCacheRead = 0.000000025m,
                        WebSearch = 0.01m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "amazon/nova-2-lite-v1",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9235,
                    ContextLimit = 1000000,
                    Created = new DateTime(2025, 12, 2),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000003m,
                        Completion = 0.0000025m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "moonshotai/kimi-k2-thinking",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9230,
                    ContextLimit = 262144,
                    Created = new DateTime(2025, 11, 6),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000006m,
                        Completion = 0.0000025m,
                        InputCacheRead = 0.00000015m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "relace/relace-search",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9225,
                    ContextLimit = 256000,
                    Created = new DateTime(2025, 12, 8),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.000001m,
                        Completion = 0.000003m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "google/gemini-3-flash-preview",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.AudioInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9220,
                    ContextLimit = 1048576,
                    Created = new DateTime(2025, 12, 17),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000005m,
                        Completion = 0.000003m,
                        Image = 0.0000005m,
                        Audio = 0.000001m,
                        InputAudioCache = 0.0000001m,
                        InputCacheRead = 0.00000005m,
                        InputCacheWrite = 0.00000008333333333333334m,
                        InternalReasoning = 0.000003m,
                        WebSearch = 0.014m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-5.1-codex-max",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9215,
                    ContextLimit = 400000,
                    Created = new DateTime(2025, 12, 4),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000125m,
                        Completion = 0.00001m,
                        InputCacheRead = 0.000000125m,
                        WebSearch = 0.01m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-5.1",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9210,
                    ContextLimit = 400000,
                    Created = new DateTime(2025, 11, 13),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000125m,
                        Completion = 0.00001m,
                        InputCacheRead = 0.000000125m,
                        WebSearch = 0.01m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-5.1-chat",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9205,
                    ContextLimit = 128000,
                    Created = new DateTime(2025, 11, 13),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000125m,
                        Completion = 0.00001m,
                        InputCacheRead = 0.000000125m,
                        WebSearch = 0.01m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-5.1-codex",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9200,
                    ContextLimit = 400000,
                    Created = new DateTime(2025, 11, 13),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000125m,
                        Completion = 0.00001m,
                        InputCacheRead = 0.000000125m,
                        WebSearch = 0.01m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "amazon/nova-premier-v1",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9195,
                    ContextLimit = 1000000,
                    Created = new DateTime(2025, 10, 31),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000025m,
                        Completion = 0.0000125m,
                        InputCacheRead = 0.000000625m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-5.2-codex",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9190,
                    ContextLimit = 400000,
                    Created = new DateTime(2026, 1, 14),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000175m,
                        Completion = 0.000014m,
                        InputCacheRead = 0.000000175m,
                        WebSearch = 0.01m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-5.2",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9185,
                    ContextLimit = 400000,
                    Created = new DateTime(2025, 12, 10),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000175m,
                        Completion = 0.000014m,
                        InputCacheRead = 0.000000175m,
                        WebSearch = 0.01m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "perplexity/sonar-pro-search",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9180,
                    ContextLimit = 196608,
                    Created = new DateTime(2025, 10, 23),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.000000255m,
                        Completion = 0.000001m,
                        InputCacheRead = 0.00000003m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen3-next-80b-a3b-instruct",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9035,
                    ContextLimit = 262144,
                    Created = new DateTime(2025, 9, 11),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000009m,
                        Completion = 0.0000011m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "relace/relace-apply-3",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9030,
                    ContextLimit = 256000,
                    Created = new DateTime(2025, 9, 26),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000085m,
                        Completion = 0.00000125m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen3-vl-8b-thinking",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9165,
                    ContextLimit = 131072,
                    Created = new DateTime(2025, 10, 14),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.000000117m,
                        Completion = 0.000001365m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen3-235b-a22b-thinking-2507",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9160,
                    ContextLimit = 256000,
                    Created = new DateTime(2025, 8, 26),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000002m,
                        Completion = 0.0000015m,
                        InputCacheRead = 0.00000002m,
                        WebSearch = 0.005m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen3-vl-30b-a3b-thinking",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9015,
                    ContextLimit = 131072,
                    Created = new DateTime(2025, 10, 6),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000013m,
                        Completion = 0.00000156m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "z-ai/glm-4.5v",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9150,
                    ContextLimit = 65536,
                    Created = new DateTime(2025, 8, 11),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000006m,
                        Completion = 0.0000018m,
                        InputCacheRead = 0.00000011m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "z-ai/glm-4.6",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9145,
                    ContextLimit = 204800,
                    Created = new DateTime(2025, 9, 30),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000039m,
                        Completion = 0.0000019m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "mistralai/mistral-medium-3.1",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9000,
                    ContextLimit = 131072,
                    Created = new DateTime(2025, 8, 13),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000004m,
                        Completion = 0.000002m,
                        InputCacheRead = 0.00000004m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "moonshotai/kimi-k2-0905",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9135,
                    ContextLimit = 262144,
                    Created = new DateTime(2025, 9, 4),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000004m,
                        Completion = 0.000002m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-5-image-mini",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.ImageOutput | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9130,
                    ContextLimit = 400000,
                    Created = new DateTime(2025, 10, 16),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000025m,
                        Completion = 0.000002m,
                        InputCacheRead = 0.00000025m,
                        WebSearch = 0.01m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-5-mini",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    Default = AICapability.Text2Text | AICapability.Text2Json,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8995,
                    ContextLimit = 400000,
                    Created = new DateTime(2025, 8, 7),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000025m,
                        Completion = 0.000002m,
                        InputCacheRead = 0.000000025m,
                        WebSearch = 0.01m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen3-vl-235b-a22b-thinking",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8990,
                    ContextLimit = 131072,
                    Created = new DateTime(2025, 9, 23),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000026m,
                        Completion = 0.0000026m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "nousresearch/hermes-4-405b",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8985,
                    ContextLimit = 131072,
                    Created = new DateTime(2025, 8, 26),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.000001m,
                        Completion = 0.000003m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen3-coder-plus",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8980,
                    ContextLimit = 1000000,
                    Created = new DateTime(2025, 9, 23),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000065m,
                        Completion = 0.00000325m,
                        InputCacheRead = 0.00000013m,
                        InputCacheWrite = 0.0000008125m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "google/gemini-2.5-flash-image",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.ImageOutput | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9105,
                    ContextLimit = 32768,
                    Created = new DateTime(2025, 10, 7),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000003m,
                        Completion = 0.0000025m,
                        Image = 0.0000003m,
                        Audio = 0.000001m,
                        InputCacheRead = 0.00000003m,
                        InputCacheWrite = 0.00000008333333333333334m,
                        InternalReasoning = 0.0000025m,
                        WebSearch = 0.014m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen3-max",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8975,
                    ContextLimit = 262144,
                    Created = new DateTime(2025, 9, 23),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000078m,
                        Completion = 0.0000039m,
                        InputCacheRead = 0.000000156m,
                        InputCacheWrite = 0.000000975m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "anthropic/claude-haiku-4.5",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8970,
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
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "ai21/jamba-large-1.7",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9090,
                    ContextLimit = 256000,
                    Created = new DateTime(2025, 8, 8),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.000002m,
                        Completion = 0.000008m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/o4-mini-deep-research",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8965,
                    ContextLimit = 200000,
                    Created = new DateTime(2025, 10, 10),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.000002m,
                        Completion = 0.000008m,
                        InputCacheRead = 0.0000005m,
                        WebSearch = 0.01m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "ai21/jamba-large-1.7",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8960,
                    ContextLimit = 256000,
                    Created = new DateTime(2025, 8, 8),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.000002m,
                        Completion = 0.000008m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-5-image-mini",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.ImageOutput | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8955,
                    ContextLimit = 400000,
                    Created = new DateTime(2025, 8, 7),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000125m,
                        Completion = 0.00001m,
                        InputCacheRead = 0.000000125m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-5-chat",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9075,
                    ContextLimit = 128000,
                    Created = new DateTime(2025, 8, 7),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000125m,
                        Completion = 0.00001m,
                        InputCacheRead = 0.000000125m,
                        WebSearch = 0.01m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-5-codex",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8950,
                    ContextLimit = 400000,
                    Created = new DateTime(2025, 9, 23),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000125m,
                        Completion = 0.00001m,
                        InputCacheRead = 0.000000125m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-5-chat",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9065,
                    ContextLimit = 400000,
                    Created = new DateTime(2025, 10, 14),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00001m,
                        Completion = 0.00001m,
                        InputCacheRead = 0.00000125m,
                        WebSearch = 0.01m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-5",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9060,
                    ContextLimit = 1000000,
                    Created = new DateTime(2025, 10, 31),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000025m,
                        Completion = 0.0000125m,
                        InputCacheRead = 0.000000625m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "anthropic/claude-sonnet-4.5",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8935,
                    ContextLimit = 1000000,
                    Created = new DateTime(2025, 9, 29),
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
                    Provider = provider,
                    Model = "google/gemini-2.5-flash-image",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.ImageOutput | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9050,
                    ContextLimit = 200000,
                    Created = new DateTime(2025, 10, 30),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.000003m,
                        Completion = 0.000015m,
                        WebSearch = 0.018m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/o3-deep-research",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8925,
                    ContextLimit = 200000,
                    Created = new DateTime(2025, 10, 10),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00001m,
                        Completion = 0.00004m,
                        InputCacheRead = 0.0000025m,
                        WebSearch = 0.01m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-5-image",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.ImageOutput | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9040,
                    ContextLimit = 128000,
                    Created = new DateTime(2025, 8, 15),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000025m,
                        Completion = 0.00001m,
                        Audio = 0.00004m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "anthropic/claude-opus-4.1",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8915,
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
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "mistralai/voxtral-small-24b-2507",
                    Capabilities = AICapability.TextInput | AICapability.AudioInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9030,
                    ContextLimit = 32000,
                    Created = new DateTime(2025, 10, 30),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000001m,
                        Completion = 0.0000003m,
                        Audio = 0.0001m,
                        InputCacheRead = 0.00000001m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-5-pro",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8910,
                    ContextLimit = 400000,
                    Created = new DateTime(2025, 10, 6),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.000015m,
                        Completion = 0.00012m,
                        WebSearch = 0.01m,
                    },
                },



                // Released between May 2025 and August 2025

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "cognitivecomputations/dolphin-mistral-24b-venice-edition:free",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9020,
                    ContextLimit = 32768,
                    Created = new DateTime(2025, 7, 9),
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "google/gemma-3n-e2b-it:free",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9015,
                    ContextLimit = 8192,
                    Created = new DateTime(2025, 7, 9),
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "google/gemma-3n-e4b-it:free",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9010,
                    ContextLimit = 8192,
                    Created = new DateTime(2025, 5, 20),
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen3-coder:free",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9005,
                    ContextLimit = 262000,
                    Created = new DateTime(2025, 7, 23),
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "z-ai/glm-4.5-air:free",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9000,
                    ContextLimit = 131072,
                    Created = new DateTime(2025, 7, 25),
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen3-235b-a22b-2507",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8995,
                    ContextLimit = 262144,
                    Created = new DateTime(2025, 7, 21),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.000000071m,
                        Completion = 0.0000001m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "z-ai/glm-4-32b",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8990,
                    ContextLimit = 128000,
                    Created = new DateTime(2025, 7, 24),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000001m,
                        Completion = 0.0000001m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "google/gemma-3n-e4b-it",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8905,
                    ContextLimit = 32768,
                    Created = new DateTime(2025, 5, 20),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000006m,
                        Completion = 0.00000012m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "meta-llama/llama-guard-4-12b",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8900,
                    ContextLimit = 163840,
                    Created = new DateTime(2025, 4, 30),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000018m,
                        Completion = 0.00000018m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen3-32b",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8895,
                    ContextLimit = 131072,
                    Created = new DateTime(2025, 5, 5),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000018m,
                        Completion = 0.00000018m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "bytedance/ui-tars-1.5-7b",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8975,
                    ContextLimit = 128000,
                    Created = new DateTime(2025, 7, 22),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000001m,
                        Completion = 0.0000002m,
                        InputCacheRead = 0.0000001m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "mistralai/mistral-small-3.2-24b-instruct",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8970,
                    ContextLimit = 128000,
                    Created = new DateTime(2025, 6, 20),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.000000075m,
                        Completion = 0.0000002m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen3-coder-30b-a3b-instruct",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8965,
                    ContextLimit = 160000,
                    Created = new DateTime(2025, 7, 31),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000007m,
                        Completion = 0.00000027m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "mistralai/devstral-small",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8960,
                    ContextLimit = 131072,
                    Created = new DateTime(2025, 7, 10),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000001m,
                        Completion = 0.0000003m,
                        InputCacheRead = 0.00000001m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen3-30b-a3b-instruct-2507",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8955,
                    ContextLimit = 262144,
                    Created = new DateTime(2025, 7, 29),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000009m,
                        Completion = 0.0000003m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "x-ai/grok-3-mini",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8885,
                    ContextLimit = 131072,
                    Created = new DateTime(2025, 6, 10),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000003m,
                        Completion = 0.0000005m,
                        InputCacheRead = 0.000000075m,
                        WebSearch = 0.005m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "tencent/hunyuan-a13b-instruct",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8875,
                    ContextLimit = 131072,
                    Created = new DateTime(2025, 7, 8),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000014m,
                        Completion = 0.00000057m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "cognitivecomputations/dolphin-mistral-24b-venice-edition",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput,
                    Verified = false,
                    Rank = 8940,
                    ContextLimit = 32768,
                    Created = new DateTime(2025, 5, 5),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000005m,
                        Completion = 0.0000008m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "google/gemini-2.5-flash-lite",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.AudioInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8935,
                    ContextLimit = 1048576,
                    Created = new DateTime(2025, 7, 22),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000001m,
                        Completion = 0.0000004m,
                        Image = 0.0000001m,
                        Audio = 0.0000003m,
                        InputCacheRead = 0.00000001m,
                        InputCacheWrite = 0.00000008333333333333334m,
                        InternalReasoning = 0.0000004m,
                        WebSearch = 0.014m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "z-ai/glm-4.5-air",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8930,
                    ContextLimit = 131072,
                    Created = new DateTime(2025, 7, 25),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000013m,
                        Completion = 0.00000085m,
                        InputCacheRead = 0.000000025m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "mistralai/codestral-2508",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8925,
                    ContextLimit = 256000,
                    Created = new DateTime(2025, 8, 1),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000003m,
                        Completion = 0.0000009m,
                        InputCacheRead = 0.00000003m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "baidu/ernie-4.5-300b-a47b",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8920,
                    ContextLimit = 123000,
                    Created = new DateTime(2025, 6, 30),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000028m,
                        Completion = 0.0000011m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "tngtech/deepseek-r1t2-chimera",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8915,
                    ContextLimit = 163840,
                    Created = new DateTime(2025, 7, 8),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000003m,
                        Completion = 0.0000011m,
                        InputCacheRead = 0.00000015m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "arcee-ai/virtuoso-large",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8910,
                    ContextLimit = 131072,
                    Created = new DateTime(2025, 5, 5),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000075m,
                        Completion = 0.0000012m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "morph/morph-v3-fast",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8860,
                    ContextLimit = 81920,
                    Created = new DateTime(2025, 7, 7),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000008m,
                        Completion = 0.0000012m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "baidu/ernie-4.5-vl-424b-a47b",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8900,
                    ContextLimit = 123000,
                    Created = new DateTime(2025, 6, 30),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000042m,
                        Completion = 0.00000125m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen3-235b-a22b",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8845,
                    ContextLimit = 131072,
                    Created = new DateTime(2025, 7, 25),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000001495m,
                        Completion = 0.000001495m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen3-coder",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8890,
                    ContextLimit = 262144,
                    Created = new DateTime(2025, 7, 23),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000022m,
                        Completion = 0.0000018m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "morph/morph-v3-large",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8840,
                    ContextLimit = 262144,
                    Created = new DateTime(2025, 7, 7),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000009m,
                        Completion = 0.0000019m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "mistralai/devstral-medium",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8880,
                    ContextLimit = 131072,
                    Created = new DateTime(2025, 7, 10),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000004m,
                        Completion = 0.000002m,
                        InputCacheRead = 0.00000004m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "mistralai/mistral-medium-3",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8835,
                    ContextLimit = 131072,
                    Created = new DateTime(2025, 5, 7),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000004m,
                        Completion = 0.000002m,
                        InputCacheRead = 0.00000004m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "deepseek/deepseek-r1-0528",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8830,
                    ContextLimit = 163840,
                    Created = new DateTime(2025, 5, 28),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000005m,
                        Completion = 0.00000215m,
                        InputCacheRead = 0.00000035m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "minimax/minimax-m1",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8825,
                    ContextLimit = 1000000,
                    Created = new DateTime(2025, 6, 17),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000004m,
                        Completion = 0.0000022m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "z-ai/glm-4.5",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8860,
                    ContextLimit = 131072,
                    Created = new DateTime(2025, 7, 25),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000006m,
                        Completion = 0.0000022m,
                        InputCacheRead = 0.00000011m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "moonshotai/kimi-k2",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8855,
                    ContextLimit = 131072,
                    Created = new DateTime(2025, 7, 11),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000057m,
                        Completion = 0.0000023m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "arcee-ai/maestro-reasoning",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8850,
                    ContextLimit = 131072,
                    Created = new DateTime(2025, 5, 5),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000009m,
                        Completion = 0.0000033m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "switchpoint/router",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8845,
                    ContextLimit = 131072,
                    Created = new DateTime(2025, 7, 11),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000085m,
                        Completion = 0.0000034m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "google/gemini-2.5-flash",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.AudioInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8815,
                    ContextLimit = 1048576,
                    Created = new DateTime(2025, 6, 17),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000003m,
                        Completion = 0.0000025m,
                        Image = 0.0000003m,
                        Audio = 0.000001m,
                        InputCacheRead = 0.00000003m,
                        InputCacheWrite = 0.00000008333333333333334m,
                        InternalReasoning = 0.0000025m,
                        WebSearch = 0.014m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "google/gemini-2.5-pro",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.AudioInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8810,
                    ContextLimit = 1048576,
                    Created = new DateTime(2025, 6, 17),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000125m,
                        Completion = 0.00001m,
                        Image = 0.00000125m,
                        Audio = 0.00000125m,
                        InputCacheRead = 0.000000125m,
                        InputCacheWrite = 0.000000375m,
                        InternalReasoning = 0.00001m,
                        WebSearch = 0.014m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "google/gemini-2.5-pro-preview",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.AudioInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8805,
                    ContextLimit = 1048576,
                    Created = new DateTime(2025, 6, 5),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000125m,
                        Completion = 0.00001m,
                        Image = 0.00000125m,
                        Audio = 0.00000125m,
                        InputCacheRead = 0.000000125m,
                        InputCacheWrite = 0.000000375m,
                        InternalReasoning = 0.00001m,
                        WebSearch = 0.014m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "google/gemini-2.5-pro-preview-05-06",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.AudioInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8800,
                    ContextLimit = 1048576,
                    Created = new DateTime(2025, 5, 7),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000125m,
                        Completion = 0.00001m,
                        Image = 0.00000125m,
                        Audio = 0.00000125m,
                        InputCacheRead = 0.000000125m,
                        InputCacheWrite = 0.000000375m,
                        InternalReasoning = 0.00001m,
                        WebSearch = 0.014m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "anthropic/claude-sonnet-4",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8795,
                    ContextLimit = 1000000,
                    Created = new DateTime(2025, 5, 22),
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
                    Provider = provider,
                    Model = "x-ai/grok-3",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8815,
                    ContextLimit = 131072,
                    Created = new DateTime(2025, 6, 10),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.000003m,
                        Completion = 0.000015m,
                        InputCacheRead = 0.00000075m,
                        WebSearch = 0.005m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "x-ai/grok-4",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8810,
                    ContextLimit = 256000,
                    Created = new DateTime(2025, 7, 9),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.000003m,
                        Completion = 0.000015m,
                        InputCacheRead = 0.00000075m,
                        WebSearch = 0.005m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "anthropic/claude-opus-4",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8790,
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
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/o3-pro",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8800,
                    ContextLimit = 200000,
                    Created = new DateTime(2025, 6, 10),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00002m,
                        Completion = 0.00008m,
                        WebSearch = 0.01m,
                    },
                },



                // Released between February 2025 and May 2025

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "google/gemma-3-12b-it:free",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8795,
                    ContextLimit = 32768,
                    Created = new DateTime(2025, 3, 13),
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "google/gemma-3-27b-it:free",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8790,
                    ContextLimit = 131072,
                    Created = new DateTime(2025, 3, 12),
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "google/gemma-3-4b-it:free",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8785,
                    ContextLimit = 32768,
                    Created = new DateTime(2025, 3, 13),
                },



                // Released between January 2025 and April 2025

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "mistralai/mistral-small-24b-instruct-2501",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8780,
                    ContextLimit = 131072,
                    Created = new DateTime(2025, 2, 12),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000048m,
                        Completion = 0.00000003m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "google/gemma-3-4b-it",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8775,
                    ContextLimit = 131072,
                    Created = new DateTime(2025, 3, 13),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000004m,
                        Completion = 0.00000008m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "google/gemma-3-12b-it",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8770,
                    ContextLimit = 131072,
                    Created = new DateTime(2025, 3, 13),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000004m,
                        Completion = 0.00000013m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "google/gemma-3-27b-it",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8765,
                    ContextLimit = 131072,
                    Created = new DateTime(2025, 3, 12),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000008m,
                        Completion = 0.00000016m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "meta-llama/llama-guard-4-12b",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8760,
                    ContextLimit = 163840,
                    Created = new DateTime(2025, 4, 30),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000018m,
                        Completion = 0.00000018m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "rekaai/reka-flash-3",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8765,
                    ContextLimit = 65536,
                    Created = new DateTime(2025, 3, 12),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000001m,
                        Completion = 0.0000002m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen3-14b",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8750,
                    ContextLimit = 40960,
                    Created = new DateTime(2025, 4, 28),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000006m,
                        Completion = 0.00000024m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen3-32b",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8745,
                    ContextLimit = 40960,
                    Created = new DateTime(2025, 4, 28),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000008m,
                        Completion = 0.00000024m,
                        InputCacheRead = 0.00000004m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "meta-llama/llama-4-scout",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8740,
                    ContextLimit = 327680,
                    Created = new DateTime(2025, 4, 5),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000008m,
                        Completion = 0.0000003m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-4.1-nano",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8750,
                    ContextLimit = 1047576,
                    Created = new DateTime(2025, 4, 14),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000001m,
                        Completion = 0.0000004m,
                        InputCacheRead = 0.000000025m,
                        WebSearch = 0.01m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen3-8b",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8730,
                    ContextLimit = 40960,
                    Created = new DateTime(2025, 4, 28),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000005m,
                        Completion = 0.0000004m,
                        InputCacheRead = 0.00000005m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen-vl-plus",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8725,
                    ContextLimit = 131072,
                    Created = new DateTime(2025, 2, 5),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000001365m,
                        Completion = 0.0000004095m,
                        InputCacheRead = 0.0000000273m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen3-30b-a3b",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8720,
                    ContextLimit = 40960,
                    Created = new DateTime(2025, 4, 28),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000009m,
                        Completion = 0.00000045m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "x-ai/grok-3-mini-beta",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8715,
                    ContextLimit = 131072,
                    Created = new DateTime(2025, 4, 9),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000003m,
                        Completion = 0.0000005m,
                        InputCacheRead = 0.000000075m,
                        WebSearch = 0.005m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "mistralai/mistral-small-3.1-24b-instruct",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8745,
                    ContextLimit = 128000,
                    Created = new DateTime(2025, 3, 17),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000035m,
                        Completion = 0.00000056m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "meta-llama/llama-4-maverick",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8705,
                    ContextLimit = 1048576,
                    Created = new DateTime(2025, 4, 5),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000015m,
                        Completion = 0.0000006m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "mistralai/mistral-saba",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8700,
                    ContextLimit = 32768,
                    Created = new DateTime(2025, 2, 17),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000002m,
                        Completion = 0.0000006m,
                        InputCacheRead = 0.00000002m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-4o-mini-search-preview",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8740,
                    ContextLimit = 128000,
                    Created = new DateTime(2025, 3, 12),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000015m,
                        Completion = 0.0000006m,
                        WebSearch = 0.0275m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "mistralai/mistral-saba",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8690,
                    ContextLimit = 163840,
                    Created = new DateTime(2025, 3, 24),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000002m,
                        Completion = 0.00000077m,
                        InputCacheRead = 0.000000135m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "thedrummer/skyfall-36b-v2",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8685,
                    ContextLimit = 32768,
                    Created = new DateTime(2025, 3, 10),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000055m,
                        Completion = 0.0000008m,
                        InputCacheRead = 0.00000025m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "alfredpros/codellama-7b-instruct-solidity",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8680,
                    ContextLimit = 4096,
                    Created = new DateTime(2025, 4, 14),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000008m,
                        Completion = 0.0000012m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "aion-labs/aion-1.0-mini",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8675,
                    ContextLimit = 131072,
                    Created = new DateTime(2025, 2, 4),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000007m,
                        Completion = 0.0000014m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "aion-labs/aion-rp-llama-3.1-8b",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8670,
                    ContextLimit = 32768,
                    Created = new DateTime(2025, 2, 4),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000008m,
                        Completion = 0.0000016m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-4.1-mini",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8665,
                    ContextLimit = 1047576,
                    Created = new DateTime(2025, 4, 14),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000004m,
                        Completion = 0.0000016m,
                        InputCacheRead = 0.0000001m,
                        WebSearch = 0.01m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen3-235b-a22b",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8660,
                    ContextLimit = 131072,
                    Created = new DateTime(2025, 4, 28),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.000000455m,
                        Completion = 0.00000182m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/o3-mini-high",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8655,
                    ContextLimit = 200000,
                    Created = new DateTime(2025, 2, 12),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000011m,
                        Completion = 0.0000044m,
                        InputCacheRead = 0.00000055m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/o4-mini",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8650,
                    ContextLimit = 200000,
                    Created = new DateTime(2025, 4, 16),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000011m,
                        Completion = 0.0000044m,
                        InputCacheRead = 0.000000275m,
                        WebSearch = 0.01m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/o4-mini-high",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8645,
                    ContextLimit = 200000,
                    Created = new DateTime(2025, 4, 16),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000011m,
                        Completion = 0.0000044m,
                        InputCacheRead = 0.000000275m,
                        WebSearch = 0.01m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "aion-labs/aion-1.0",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8640,
                    ContextLimit = 131072,
                    Created = new DateTime(2025, 2, 4),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.000004m,
                        Completion = 0.000008m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-4.1",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8635,
                    ContextLimit = 1047576,
                    Created = new DateTime(2025, 4, 14),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.000002m,
                        Completion = 0.000008m,
                        InputCacheRead = 0.0000005m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/o3",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8630,
                    ContextLimit = 200000,
                    Created = new DateTime(2025, 4, 16),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.000002m,
                        Completion = 0.000008m,
                        InputCacheRead = 0.0000005m,
                        WebSearch = 0.01m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "perplexity/sonar-deep-research",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8625,
                    ContextLimit = 128000,
                    Created = new DateTime(2025, 3, 7),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.000002m,
                        Completion = 0.000008m,
                        InternalReasoning = 0.000003m,
                        WebSearch = 0.005m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "perplexity/sonar-reasoning-pro",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8620,
                    ContextLimit = 128000,
                    Created = new DateTime(2025, 3, 7),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.000002m,
                        Completion = 0.000008m,
                        WebSearch = 0.005m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "cohere/command-a",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8615,
                    ContextLimit = 256000,
                    Created = new DateTime(2025, 3, 13),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000025m,
                        Completion = 0.00001m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-4o-search-preview",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8610,
                    ContextLimit = 128000,
                    Created = new DateTime(2025, 3, 12),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000025m,
                        Completion = 0.00001m,
                        WebSearch = 0.035m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "perplexity/sonar-pro",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8605,
                    ContextLimit = 200000,
                    Created = new DateTime(2025, 3, 7),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.000003m,
                        Completion = 0.000015m,
                        WebSearch = 0.005m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "x-ai/grok-3-beta",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8600,
                    ContextLimit = 131072,
                    Created = new DateTime(2025, 4, 9),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.000003m,
                        Completion = 0.000015m,
                        InputCacheRead = 0.00000075m,
                        WebSearch = 0.005m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/o1-pro",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8595,
                    ContextLimit = 200000,
                    Created = new DateTime(2025, 3, 19),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00015m,
                        Completion = 0.0006m,
                    },
                },



                // Released between November 2024 and February 2025

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "meta-llama/llama-3.3-70b-instruct:free",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8590,
                    ContextLimit = 65536,
                    Created = new DateTime(2024, 12, 6),
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "mistralai/mistral-small-24b-instruct-2501",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8585,
                    ContextLimit = 32768,
                    Created = new DateTime(2025, 1, 30),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000005m,
                        Completion = 0.00000008m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen-turbo",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8580,
                    ContextLimit = 131072,
                    Created = new DateTime(2025, 2, 1),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000000325m,
                        Completion = 0.00000013m,
                        InputCacheRead = 0.0000000065m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "amazon/nova-micro-v1",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8575,
                    ContextLimit = 128000,
                    Created = new DateTime(2024, 12, 5),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.000000035m,
                        Completion = 0.00000014m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "microsoft/phi-4",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8570,
                    ContextLimit = 16384,
                    Created = new DateTime(2025, 1, 10),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.000000065m,
                        Completion = 0.00000014m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "cohere/command-r7b-12-2024",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8565,
                    ContextLimit = 128000,
                    Created = new DateTime(2024, 12, 14),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000000375m,
                        Completion = 0.00000015m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "amazon/nova-lite-v1",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8560,
                    ContextLimit = 300000,
                    Created = new DateTime(2024, 12, 5),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000006m,
                        Completion = 0.00000024m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "deepseek/deepseek-r1-distill-qwen-32b",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8555,
                    ContextLimit = 32768,
                    Created = new DateTime(2025, 1, 29),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000029m,
                        Completion = 0.00000029m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "meta-llama/llama-3.3-70b-instruct",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8550,
                    ContextLimit = 131072,
                    Created = new DateTime(2024, 12, 6),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000001m,
                        Completion = 0.00000032m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "thedrummer/unslopnemo-12b",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8545,
                    ContextLimit = 32768,
                    Created = new DateTime(2024, 11, 8),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000004m,
                        Completion = 0.0000004m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen2.5-vl-72b-instruct",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8540,
                    ContextLimit = 32000,
                    Created = new DateTime(2025, 2, 1),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000025m,
                        Completion = 0.00000075m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "sao10k/l3.3-euryale-70b",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8535,
                    ContextLimit = 131072,
                    Created = new DateTime(2024, 12, 18),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000065m,
                        Completion = 0.00000075m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen-plus",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8730,
                    ContextLimit = 1000000,
                    Created = new DateTime(2025, 2, 1),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000026m,
                        Completion = 0.00000078m,
                        InputCacheRead = 0.000000052m,
                        InputCacheWrite = 0.000000325m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "deepseek/deepseek-r1-distill-llama-70b",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8525,
                    ContextLimit = 131072,
                    Created = new DateTime(2025, 1, 23),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000007m,
                        Completion = 0.0000008m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen2.5-vl-72b-instruct",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8520,
                    ContextLimit = 163840,
                    Created = new DateTime(2024, 12, 26),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000032m,
                        Completion = 0.00000089m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "perplexity/sonar",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8705,
                    ContextLimit = 127072,
                    Created = new DateTime(2025, 1, 27),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.000001m,
                        Completion = 0.000001m,
                        WebSearch = 0.005m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "deepseek/deepseek-chat-v3-0324",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8510,
                    ContextLimit = 32768,
                    Created = new DateTime(2024, 11, 11),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000066m,
                        Completion = 0.000001m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "minimax/minimax-01",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8505,
                    ContextLimit = 1000192,
                    Created = new DateTime(2025, 1, 15),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000002m,
                        Completion = 0.0000011m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen-vl-max",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8500,
                    ContextLimit = 131072,
                    Created = new DateTime(2025, 2, 1),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000052m,
                        Completion = 0.00000208m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "deepseek/deepseek-r1",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8495,
                    ContextLimit = 64000,
                    Created = new DateTime(2025, 1, 20),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000007m,
                        Completion = 0.0000025m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/o4-mini-high",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8490,
                    ContextLimit = 16000,
                    Created = new DateTime(2025, 1, 8),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.000003m,
                        Completion = 0.000003m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "amazon/nova-pro-v1",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8485,
                    ContextLimit = 300000,
                    Created = new DateTime(2024, 12, 5),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000008m,
                        Completion = 0.0000032m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/o4-mini",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8480,
                    ContextLimit = 32768,
                    Created = new DateTime(2025, 2, 1),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000104m,
                        Completion = 0.00000416m,
                        InputCacheRead = 0.000000208m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/o3-mini",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8475,
                    ContextLimit = 200000,
                    Created = new DateTime(2025, 1, 31),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000011m,
                        Completion = 0.0000044m,
                        InputCacheRead = 0.00000055m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "mistralai/mistral-large-2407",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8470,
                    ContextLimit = 131072,
                    Created = new DateTime(2024, 11, 19),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.000002m,
                        Completion = 0.000006m,
                        InputCacheRead = 0.0000002m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "mistralai/mistral-large-2411",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8465,
                    ContextLimit = 131072,
                    Created = new DateTime(2024, 11, 19),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.000002m,
                        Completion = 0.000006m,
                        InputCacheRead = 0.0000002m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "mistralai/pixtral-large-2411",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8460,
                    ContextLimit = 131072,
                    Created = new DateTime(2024, 11, 19),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.000002m,
                        Completion = 0.000006m,
                        InputCacheRead = 0.0000002m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-4o-2024-11-20",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8455,
                    ContextLimit = 128000,
                    Created = new DateTime(2024, 11, 20),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000025m,
                        Completion = 0.00001m,
                        InputCacheRead = 0.00000125m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/o1",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8450,
                    ContextLimit = 200000,
                    Created = new DateTime(2024, 12, 17),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.000015m,
                        Completion = 0.00006m,
                        InputCacheRead = 0.0000075m,
                    },
                },



                // Released between August 2024 and November 2024

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "meta-llama/llama-3.2-3b-instruct:free",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8445,
                    ContextLimit = 131072,
                    Created = new DateTime(2024, 9, 25),
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-4.1",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8440,
                    ContextLimit = 131072,
                    Created = new DateTime(2024, 8, 16),
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "perplexity/sonar-reasoning-pro",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8435,
                    ContextLimit = 8192,
                    Created = new DateTime(2024, 8, 13),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000004m,
                        Completion = 0.00000005m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen-2.5-7b-instruct",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8430,
                    ContextLimit = 32768,
                    Created = new DateTime(2024, 10, 16),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000004m,
                        Completion = 0.0000001m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "meta-llama/llama-3.2-1b-instruct",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8425,
                    ContextLimit = 60000,
                    Created = new DateTime(2024, 9, 25),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.000000027m,
                        Completion = 0.0000002m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "meta-llama/llama-3.2-11b-vision-instruct",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8420,
                    ContextLimit = 131072,
                    Created = new DateTime(2024, 9, 25),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.000000245m,
                        Completion = 0.000000245m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "nousresearch/hermes-3-llama-3.1-70b",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8415,
                    ContextLimit = 131072,
                    Created = new DateTime(2024, 8, 18),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000003m,
                        Completion = 0.0000003m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "meta-llama/llama-3.2-3b-instruct",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8410,
                    ContextLimit = 80000,
                    Created = new DateTime(2024, 9, 25),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.000000051m,
                        Completion = 0.00000034m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen-2.5-72b-instruct",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8405,
                    ContextLimit = 32768,
                    Created = new DateTime(2024, 9, 19),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000036m,
                        Completion = 0.0000004m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "thedrummer/rocinante-12b",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8400,
                    ContextLimit = 32768,
                    Created = new DateTime(2024, 9, 30),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000017m,
                        Completion = 0.00000043m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "cohere/command-r-08-2024",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8395,
                    ContextLimit = 128000,
                    Created = new DateTime(2024, 8, 30),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000015m,
                        Completion = 0.0000006m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "perplexity/sonar-deep-research",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8390,
                    ContextLimit = 131072,
                    Created = new DateTime(2024, 8, 28),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000085m,
                        Completion = 0.00000085m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "cohere/command-a",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8385,
                    ContextLimit = 131072,
                    Created = new DateTime(2024, 8, 16),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.000001m,
                        Completion = 0.000001m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "anthropic/claude-3.5-haiku",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8380,
                    ContextLimit = 200000,
                    Created = new DateTime(2024, 11, 4),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000008m,
                        Completion = 0.000004m,
                        InputCacheRead = 0.00000008m,
                        InputCacheWrite = 0.000001m,
                        WebSearch = 0.01m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "anthracite-org/magnum-v4-72b",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8375,
                    ContextLimit = 16384,
                    Created = new DateTime(2024, 10, 22),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.000003m,
                        Completion = 0.000005m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "mistralai/mistral-large-2407",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8370,
                    ContextLimit = 128000,
                    Created = new DateTime(2024, 8, 30),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000025m,
                        Completion = 0.00001m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "inflection/inflection-3-pi",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8365,
                    ContextLimit = 8000,
                    Created = new DateTime(2024, 10, 11),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000025m,
                        Completion = 0.00001m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "inflection/inflection-3-productivity",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8360,
                    ContextLimit = 8000,
                    Created = new DateTime(2024, 10, 11),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000025m,
                        Completion = 0.00001m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-4o-2024-08-06",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8555,
                    ContextLimit = 128000,
                    Created = new DateTime(2024, 8, 6),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000025m,
                        Completion = 0.00001m,
                        InputCacheRead = 0.00000125m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/o1",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8350,
                    ContextLimit = 131072,
                    Created = new DateTime(2024, 7, 19),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000002m,
                        Completion = 0.00000003m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "meta-llama/llama-3.1-8b-instruct",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8345,
                    ContextLimit = 16384,
                    Created = new DateTime(2024, 7, 23),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000002m,
                        Completion = 0.00000005m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen-2.5-7b-instruct",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8535,
                    ContextLimit = 131072,
                    Created = new DateTime(2024, 10, 16),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000004m,
                        Completion = 0.0000001m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "meta-llama/llama-3.2-1b-instruct",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8530,
                    ContextLimit = 131072,
                    Created = new DateTime(2024, 9, 25),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.000000027m,
                        Completion = 0.000000201m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "meta-llama/llama-3.2-3b-instruct",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8340,
                    ContextLimit = 8192,
                    Created = new DateTime(2024, 5, 27),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000014m,
                        Completion = 0.00000014m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "meta-llama/llama-3.1-70b-instruct",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8515,
                    ContextLimit = 131072,
                    Created = new DateTime(2024, 7, 23),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000004m,
                        Completion = 0.0000004m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-4o-mini",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8330,
                    ContextLimit = 128000,
                    Created = new DateTime(2024, 7, 18),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000015m,
                        Completion = 0.0000006m,
                        InputCacheRead = 0.000000075m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-4o-mini-2024-07-18",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8325,
                    ContextLimit = 128000,
                    Created = new DateTime(2024, 7, 18),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000015m,
                        Completion = 0.0000006m,
                        InputCacheRead = 0.000000075m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "google/gemma-2-27b-it",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8320,
                    ContextLimit = 8192,
                    Created = new DateTime(2024, 7, 13),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000065m,
                        Completion = 0.00000065m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "cohere/command-r-08-2024",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8315,
                    ContextLimit = 8192,
                    Created = new DateTime(2024, 6, 18),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000148m,
                        Completion = 0.00000148m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "nousresearch/hermes-3-llama-3.1-70b",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8500,
                    ContextLimit = 131072,
                    Created = new DateTime(2024, 8, 18),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000007m,
                        Completion = 0.0000007m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "sao10k/l3.1-euryale-70b",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8495,
                    ContextLimit = 131072,
                    Created = new DateTime(2024, 8, 28),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000085m,
                        Completion = 0.00000085m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "nousresearch/hermes-3-llama-3.1-405b",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8490,
                    ContextLimit = 131072,
                    Created = new DateTime(2024, 8, 16),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.000001m,
                        Completion = 0.000001m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "inflection/inflection-3-pi",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8485,
                    ContextLimit = 8000,
                    Created = new DateTime(2024, 10, 11),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000025m,
                        Completion = 0.00001m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "inflection/inflection-3-productivity",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8480,
                    ContextLimit = 8000,
                    Created = new DateTime(2024, 10, 11),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000025m,
                        Completion = 0.00001m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "cohere/command-r-plus-08-2024",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8475,
                    ContextLimit = 128000,
                    Created = new DateTime(2024, 8, 30),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000025m,
                        Completion = 0.00001m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-4o-2024-08-06",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8310,
                    ContextLimit = 128000,
                    Created = new DateTime(2024, 5, 13),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000025m,
                        Completion = 0.00001m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-4o-2024-05-13",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8305,
                    ContextLimit = 128000,
                    Created = new DateTime(2024, 5, 13),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.000005m,
                        Completion = 0.000015m,
                    },
                },



                // Released before July 2024 or unknown release date

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openrouter/auto",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.AudioInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.ImageOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8465,
                    ContextLimit = 2000000,
                    Created = new DateTime(2023, 11, 8),
                    Pricing = new AIModelPricing
                    {
                        Prompt = -1m,
                        Completion = -1m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gryphe/mythomax-l2-13b",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8455,
                    ContextLimit = 4096,
                    Created = new DateTime(2023, 7, 2),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000006m,
                        Completion = 0.00000006m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-4o-mini",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8290,
                    ContextLimit = 2824,
                    Created = new DateTime(2023, 9, 28),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000011m,
                        Completion = 0.00000019m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "microsoft/wizardlm-2-8x22b",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8285,
                    ContextLimit = 65535,
                    Created = new DateTime(2024, 4, 16),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000062m,
                        Completion = 0.00000062m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "undi95/remm-slerp-l2-13b",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8430,
                    ContextLimit = 6144,
                    Created = new DateTime(2023, 7, 22),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000045m,
                        Completion = 0.00000065m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "meta-llama/llama-3-70b-instruct",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8275,
                    ContextLimit = 8192,
                    Created = new DateTime(2024, 4, 18),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000051m,
                        Completion = 0.00000074m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "mancer/weaver",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8425,
                    ContextLimit = 8000,
                    Created = new DateTime(2023, 8, 2),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000075m,
                        Completion = 0.000001m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "anthropic/claude-3-haiku",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8420,
                    ContextLimit = 200000,
                    Created = new DateTime(2024, 3, 13),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000025m,
                        Completion = 0.00000125m,
                        InputCacheRead = 0.00000003m,
                        InputCacheWrite = 0.0000003m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-3.5-turbo",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8415,
                    ContextLimit = 16385,
                    Created = new DateTime(2023, 5, 28),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000005m,
                        Completion = 0.0000015m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-3.5-turbo-0613",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8410,
                    ContextLimit = 4095,
                    Created = new DateTime(2024, 1, 25),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.000001m,
                        Completion = 0.000002m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-3.5-turbo-instruct",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8405,
                    ContextLimit = 4095,
                    Created = new DateTime(2023, 9, 28),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000015m,
                        Completion = 0.000002m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-3.5-turbo-16k",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8400,
                    ContextLimit = 16385,
                    Created = new DateTime(2023, 8, 28),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.000003m,
                        Completion = 0.000004m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "mistralai/mistral-large",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8240,
                    ContextLimit = 128000,
                    Created = new DateTime(2024, 2, 26),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.000002m,
                        Completion = 0.000006m,
                        InputCacheRead = 0.0000002m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "mistralai/mixtral-8x22b-instruct",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8395,
                    ContextLimit = 65536,
                    Created = new DateTime(2024, 4, 17),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.000002m,
                        Completion = 0.000006m,
                        InputCacheRead = 0.0000002m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "alpindale/goliath-120b",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8230,
                    ContextLimit = 6144,
                    Created = new DateTime(2023, 11, 10),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000375m,
                        Completion = 0.0000075m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-4-1106-preview",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8390,
                    ContextLimit = 128000,
                    Created = new DateTime(2023, 11, 6),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00001m,
                        Completion = 0.00003m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-4-turbo",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8375,
                    ContextLimit = 128000,
                    Created = new DateTime(2024, 4, 9),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00001m,
                        Completion = 0.00003m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-4-turbo-preview",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8370,
                    ContextLimit = 128000,
                    Created = new DateTime(2024, 1, 25),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00001m,
                        Completion = 0.00003m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-4",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8210,
                    ContextLimit = 8191,
                    Created = new DateTime(2023, 5, 28),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00003m,
                        Completion = 0.00006m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-4-0314",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8205,
                    ContextLimit = 8191,
                    Created = new DateTime(2023, 5, 28),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00003m,
                        Completion = 0.00006m,
                    },
                },



                // Deprecated models

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "tencent/hy3:free",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    Verified = false,
                    Deprecated = true,
                    Rank = 0,
                    ContextLimit = 262144,
                    Created = new DateTime(2026, 4, 23),
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "poolside/laguna-m.1:free",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -5,
                    ContextLimit = 262144,
                    Created = new DateTime(2026, 4, 22),
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "allenai/olmo-3.1-32b-instruct",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -100,
                    ContextLimit = 65536,
                    Created = new DateTime(2026, 1, 6),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000002m,
                        Completion = 0.0000006m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "alpindale/goliath-120b",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -15,
                    ContextLimit = 131072,
                    Created = new DateTime(2025, 10, 28),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000002m,
                        Completion = 0.0000006m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "anthropic/claude-3.5-haiku",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -20,
                    ContextLimit = 1048576,
                    Created = new DateTime(2025, 2, 25),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.000000075m,
                        Completion = 0.0000003m,
                        Image = 0.000000075m,
                        Audio = 0.000000075m,
                        InternalReasoning = 0.0000003m,
                        WebSearch = 0.014m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "google/gemini-2.0-flash-001",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.AudioInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -25,
                    ContextLimit = 1000000,
                    Created = new DateTime(2025, 2, 5),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000001m,
                        Completion = 0.0000004m,
                        Image = 0.0000001m,
                        Audio = 0.0000007m,
                        InputCacheRead = 0.000000025m,
                        InputCacheWrite = 0.00000008333333333333334m,
                        InternalReasoning = 0.0000004m,
                        WebSearch = 0.014m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "anthropic/claude-3.7-sonnet",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -115,
                    ContextLimit = 200000,
                    Created = new DateTime(2025, 2, 24),
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
                    Provider = provider,
                    Model = "anthropic/claude-3.7-sonnet:thinking",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -120,
                    ContextLimit = 200000,
                    Created = new DateTime(2025, 2, 24),
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
                    Provider = provider,
                    Model = "anthropic/claude-opus-4.6-fast",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -125,
                    ContextLimit = 1000000,
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00003m,
                        Completion = 0.00015m,
                        InputCacheRead = 0.000003m,
                        InputCacheWrite = 0.0000375m,
                        WebSearch = 0.01m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "arcee-ai/coder-large",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -130,
                    ContextLimit = 32768,
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000005m,
                        Completion = 0.0000008m,
                        InputCacheRead = 0.00000025m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "arcee-ai/maestro-reasoning",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -135,
                    ContextLimit = 131072,
                    Created = new DateTime(2024, 10, 15),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000012m,
                        Completion = 0.0000012m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "arcee-ai/spotlight",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -140,
                    ContextLimit = 131072,
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000008m,
                        Completion = 0.00000016m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "arcee-ai/trinity-large-preview",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -145,
                    ContextLimit = 131000,
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000015m,
                        Completion = 0.00000045m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "arcee-ai/trinity-mini",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -150,
                    ContextLimit = 131072,
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.000000045m,
                        Completion = 0.00000015m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "baidu/ernie-4.5-21b-a3b",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -155,
                    ContextLimit = 120000,
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000007m,
                        Completion = 0.00000028m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "baidu/ernie-4.5-21b-a3b-thinking",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -160,
                    ContextLimit = 131072,
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000007m,
                        Completion = 0.00000028m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "baidu/ernie-4.5-300b-a47b",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -165,
                    ContextLimit = 123000,
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000028m,
                        Completion = 0.0000011m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "baidu/ernie-4.5-vl-28b-a3b",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -170,
                    ContextLimit = 30000,
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000014m,
                        Completion = 0.00000056m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "baidu/qianfan-ocr-fast:free",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -175,
                    ContextLimit = 65536,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "deepseek/deepseek-r1-distill-qwen-32b",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -180,
                    ContextLimit = 128000,
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000029m,
                        Completion = 0.00000029m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "deepseek/deepseek-v3.2-speciale",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -185,
                    ContextLimit = 163840,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "essentialai/rnj-1-instruct",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -190,
                    ContextLimit = 32768,
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000015m,
                        Completion = 0.00000015m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "google/gemini-2.0-flash-001",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.AudioInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -195,
                    ContextLimit = 1000000,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "google/gemini-2.0-flash-lite-001",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.AudioInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -200,
                    ContextLimit = 1048576,
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.000000075m,
                        Completion = 0.0000003m,
                        Image = 0.000000075m,
                        Audio = 0.000000075m,
                        InternalReasoning = 0.0000003m,
                        WebSearch = 0.014m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "google/gemini-2.5-flash-lite-preview-09-2025",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.AudioInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -205,
                    ContextLimit = 1048576,
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000001m,
                        Completion = 0.0000004m,
                        Image = 0.0000001m,
                        Audio = 0.0000003m,
                        InputCacheRead = 0.00000001m,
                        InputCacheWrite = 0.00000008333333333333334m,
                        InternalReasoning = 0.0000004m,
                        WebSearch = 0.014m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "google/gemma-3-12b-it:free",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -210,
                    ContextLimit = 32768,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "google/gemma-3-27b-it:free",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -215,
                    ContextLimit = 131072,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "google/gemma-3-4b-it:free",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -220,
                    ContextLimit = 32768,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "google/gemma-3n-e2b-it:free",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -225,
                    ContextLimit = 8192,
                    Created = new DateTime(2024, 4, 18),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000003m,
                        Completion = 0.00000004m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "mistralai/mixtral-8x7b-instruct",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -310,
                    ContextLimit = 32768,
                    Created = new DateTime(2023, 12, 10),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000054m,
                        Completion = 0.00000054m,
                    },
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

                var response = await this.openRouterProvider.Call(request).ConfigureAwait(false);

                Debug.WriteLine("[OpenRouterProviderModels] RetrieveApiModels response received: " + (response != null));

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
