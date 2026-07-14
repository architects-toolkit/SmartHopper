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

            var models = new List<AIModelCapabilities>
            {
                // Released between April 2026 and July 2026

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openrouter/fusion",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput,
                    Verified = false,
                    Rank = 10000,
                    ContextLimit = 1000000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openrouter/pareto-code",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9995,
                    ContextLimit = 2000000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "poolside/laguna-xs-2.1:free",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    Default = AICapability.ToolChat | AICapability.ReasoningChat | AICapability.ToolReasoningChat,
                    Verified = false,
                    Rank = 9990,
                    ContextLimit = 262144,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "cohere/north-mini-code:free",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    Verified = false,
                    Rank = 9985,
                    ContextLimit = 256000,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "nvidia/nemotron-3.5-content-safety:free",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.Reasoning,
                    Default = AICapability.Image2Text,
                    Verified = false,
                    Rank = 9980,
                    ContextLimit = 128000,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "nvidia/nemotron-3-ultra-550b-a55b:free",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    Verified = false,
                    Rank = 9975,
                    ContextLimit = 1000000,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "nvidia/nemotron-3-nano-omni-30b-a3b-reasoning:free",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.AudioInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    Default = AICapability.Speech2Text,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9970,
                    ContextLimit = 256000,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "poolside/laguna-m.1:free",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9965,
                    ContextLimit = 262144,
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
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "nex-agi/nex-n2-mini",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    Verified = false,
                    Rank = 9955,
                    ContextLimit = 262144,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "ibm-granite/granite-4.1-8b",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9950,
                    ContextLimit = 131072,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "poolside/laguna-xs-2.1",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    Verified = false,
                    Rank = 9945,
                    ContextLimit = 262144,
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
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "tencent/hy3-preview",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    Verified = false,
                    Rank = 9935,
                    ContextLimit = 262144,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "xiaomi/mimo-v2.5",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.AudioInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9930,
                    ContextLimit = 1048576,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "poolside/laguna-m.1",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    Verified = false,
                    Rank = 9925,
                    ContextLimit = 262144,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "tencent/hy3",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    Verified = false,
                    Rank = 9920,
                    ContextLimit = 262144,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "inclusionai/ring-2.6-1t",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    Verified = false,
                    Rank = 9915,
                    ContextLimit = 262144,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "inclusionai/ling-2.6-1t",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    Verified = false,
                    Rank = 9910,
                    ContextLimit = 262144,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "deepseek/deepseek-v4-pro",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9905,
                    ContextLimit = 1048576,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "xiaomi/mimo-v2.5-pro",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9900,
                    ContextLimit = 1048576,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "nex-agi/nex-n2-pro",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    Verified = false,
                    Rank = 9895,
                    ContextLimit = 262144,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen3.6-35b-a3b",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9890,
                    ContextLimit = 262144,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen3.6-flash",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9885,
                    ContextLimit = 1000000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "stepfun/step-3.7-flash",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    Verified = false,
                    Rank = 9880,
                    ContextLimit = 256000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "minimax/minimax-m3",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    Verified = false,
                    Rank = 9875,
                    ContextLimit = 1048576,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen3.7-plus",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    Verified = false,
                    Rank = 9870,
                    ContextLimit = 1000000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "z-ai/glm-5.2",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    Verified = false,
                    Rank = 9865,
                    ContextLimit = 1048576,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "aion-labs/aion-3.0-mini",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    Verified = false,
                    Rank = 9860,
                    ContextLimit = 131072,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "google/gemini-3.1-flash-lite-image",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.ImageOutput | AICapability.JsonOutput | AICapability.Reasoning,
                    Default = AICapability.Text2Image | AICapability.Image2Image,
                    Verified = false,
                    Rank = 9855,
                    ContextLimit = 65536,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "perceptron/perceptron-mk1",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.Reasoning,
                    Verified = false,
                    Rank = 9850,
                    ContextLimit = 32768,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen3.5-plus-20260420",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9845,
                    ContextLimit = 1000000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "x-ai/grok-build-0.1",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    Verified = false,
                    Rank = 9840,
                    ContextLimit = 256000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "nvidia/nemotron-3-ultra-550b-a55b",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    Verified = false,
                    Rank = 9835,
                    ContextLimit = 1000000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "google/gemini-3.1-flash-lite",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.AudioInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    Verified = false,
                    Rank = 9830,
                    ContextLimit = 1048576,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen3.6-27b",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9825,
                    ContextLimit = 262144,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "x-ai/grok-4.3",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9820,
                    ContextLimit = 1000000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "google/gemini-3.1-flash-image",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.ImageOutput | AICapability.JsonOutput | AICapability.Reasoning,
                    Verified = false,
                    Rank = 9815,
                    ContextLimit = 131072,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "~moonshotai/kimi-latest",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9810,
                    ContextLimit = 262144,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "moonshotai/kimi-k2.6",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9805,
                    ContextLimit = 262144,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "moonshotai/kimi-k2.7-code",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    Verified = false,
                    Rank = 9800,
                    ContextLimit = 262144,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen3.7-max",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    Verified = false,
                    Rank = 9795,
                    ContextLimit = 1000000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "~openai/gpt-mini-latest",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9790,
                    ContextLimit = 400000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "~anthropic/claude-haiku-latest",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9785,
                    ContextLimit = 200000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-5.6-luna-pro",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    Verified = false,
                    Rank = 9780,
                    ContextLimit = 1050000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-5.6-luna",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    Verified = false,
                    Rank = 9775,
                    ContextLimit = 1050000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "x-ai/grok-4.5",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    Verified = false,
                    Rank = 9770,
                    ContextLimit = 500000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "~x-ai/grok-latest",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    Verified = false,
                    Rank = 9765,
                    ContextLimit = 500000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "aion-labs/aion-3.0",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    Verified = false,
                    Rank = 9760,
                    ContextLimit = 131072,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen3.6-max-preview",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9755,
                    ContextLimit = 262144,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "mistralai/mistral-medium-3-5",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    Verified = false,
                    Rank = 9750,
                    ContextLimit = 262144,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "anthropic/claude-sonnet-5",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    Verified = false,
                    Rank = 9745,
                    ContextLimit = 1000000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "~anthropic/claude-sonnet-latest",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9740,
                    ContextLimit = 1000000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "google/gemini-3.5-flash",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.AudioInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    Verified = false,
                    Rank = 9735,
                    ContextLimit = 1048576,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "~google/gemini-flash-latest",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.AudioInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9730,
                    ContextLimit = 1048576,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-5.6-terra-pro",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    Verified = false,
                    Rank = 9725,
                    ContextLimit = 1050000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-5.6-terra",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    Verified = false,
                    Rank = 9720,
                    ContextLimit = 1050000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-5.4-image-2",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.ImageOutput | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9715,
                    ContextLimit = 272000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "google/gemini-3-pro-image",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.ImageOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    Verified = false,
                    Rank = 9710,
                    ContextLimit = 65536,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "~google/gemini-pro-latest",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.AudioInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9705,
                    ContextLimit = 1048576,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "anthropic/claude-opus-4.8",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    Verified = false,
                    Rank = 9700,
                    ContextLimit = 1000000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "~anthropic/claude-opus-latest",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9695,
                    ContextLimit = 1000000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "anthropic/claude-opus-4.7",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9690,
                    ContextLimit = 1000000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-5.6-sol-pro",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    Verified = false,
                    Rank = 9685,
                    ContextLimit = 1050000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-5.6-sol",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    Verified = false,
                    Rank = 9680,
                    ContextLimit = 1050000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "sakana/fugu-ultra",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    Verified = false,
                    Rank = 9675,
                    ContextLimit = 1000000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-chat-latest",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    Verified = false,
                    Rank = 9670,
                    ContextLimit = 400000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "~openai/gpt-latest",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9665,
                    ContextLimit = 1050000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-5.5",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9660,
                    ContextLimit = 1050000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "~anthropic/claude-fable-latest",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    Verified = false,
                    Rank = 9655,
                    ContextLimit = 1000000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "anthropic/claude-fable-5",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    Verified = false,
                    Rank = 9650,
                    ContextLimit = 1000000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "anthropic/claude-opus-4.8-fast",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    Verified = false,
                    Rank = 9645,
                    ContextLimit = 1000000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "anthropic/claude-opus-4.7-fast",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    Verified = false,
                    Rank = 9640,
                    ContextLimit = 1000000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-5.5-pro",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9635,
                    ContextLimit = 1050000,
                                    },



                // Released between January 2026 and April 2026

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "google/gemma-4-26b-a4b-it:free",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9630,
                    ContextLimit = 262144,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "google/gemma-4-31b-it:free",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9625,
                    ContextLimit = 262144,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "google/lyria-3-pro-preview",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.AudioOutput | AICapability.JsonOutput,
                    Default = AICapability.Text2Speech,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9620,
                    ContextLimit = 1048576,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "google/lyria-3-clip-preview",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.AudioOutput | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9615,
                    ContextLimit = 1048576,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "nvidia/nemotron-3-super-120b-a12b:free",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9610,
                    ContextLimit = 1000000,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openrouter/free",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9605,
                    ContextLimit = 200000,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "liquid/lfm-2.5-1.2b-thinking:free",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9600,
                    ContextLimit = 32768,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "liquid/lfm-2.5-1.2b-instruct:free",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9595,
                    ContextLimit = 32768,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "rekaai/reka-edge",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9590,
                    ContextLimit = 16384,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen3.5-9b",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9585,
                    ContextLimit = 262144,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen3.5-flash-02-23",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9580,
                    ContextLimit = 1000000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "stepfun/step-3.5-flash",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9575,
                    ContextLimit = 262144,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "google/gemma-4-26b-a4b-it",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9570,
                    ContextLimit = 262144,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "google/gemma-4-31b-it",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9565,
                    ContextLimit = 262144,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "bytedance-seed/seed-2.0-mini",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9560,
                    ContextLimit = 262144,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "z-ai/glm-4.7-flash",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9555,
                    ContextLimit = 202752,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "nvidia/nemotron-3-super-120b-a12b",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9550,
                    ContextLimit = 1000000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "mistralai/mistral-small-2603",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9545,
                    ContextLimit = 262144,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "upstage/solar-pro-3",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9540,
                    ContextLimit = 128000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "inception/mercury-2",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9535,
                    ContextLimit = 128000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "arcee-ai/trinity-large-thinking",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9530,
                    ContextLimit = 262144,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen3-coder-next",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9525,
                    ContextLimit = 262144,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "minimax/minimax-m2.5",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9520,
                    ContextLimit = 204800,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "minimax/minimax-m2.7",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9515,
                    ContextLimit = 204800,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen3.5-35b-a3b",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9510,
                    ContextLimit = 262144,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "kwaipilot/kat-coder-pro-v2",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9505,
                    ContextLimit = 256000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "minimax/minimax-m2-her",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9500,
                    ContextLimit = 65536,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-5.4-nano",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9495,
                    ContextLimit = 400000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen3.5-27b",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9490,
                    ContextLimit = 262144,
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
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "z-ai/glm-5",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9475,
                    ContextLimit = 202752,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen3.6-plus",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9470,
                    ContextLimit = 1000000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "bytedance-seed/seed-2.0-lite",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9465,
                    ContextLimit = 262144,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "moonshotai/kimi-k2.5",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9460,
                    ContextLimit = 262144,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen3.5-122b-a10b",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9455,
                    ContextLimit = 262144,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "google/gemini-3.1-flash-lite-preview",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.AudioInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9450,
                    ContextLimit = 1048576,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen3.5-397b-a17b",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9445,
                    ContextLimit = 256000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "x-ai/grok-4.20-multi-agent",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9440,
                    ContextLimit = 2000000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "x-ai/grok-4.20",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9435,
                    ContextLimit = 2000000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "google/gemini-3.1-flash-image-preview",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.ImageOutput | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9430,
                    ContextLimit = 131072,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-audio-mini",
                    Capabilities = AICapability.TextInput | AICapability.AudioInput | AICapability.TextOutput | AICapability.AudioOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9425,
                    ContextLimit = 128000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "z-ai/glm-5.1",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9420,
                    ContextLimit = 202752,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen3-max-thinking",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9415,
                    ContextLimit = 262144,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "z-ai/glm-5-turbo",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9410,
                    ContextLimit = 262144,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-5.4-mini",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9405,
                    ContextLimit = 400000,
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
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-5.3-chat",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9395,
                    ContextLimit = 128000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-5.3-codex",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9390,
                    ContextLimit = 400000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-5.2-codex",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9385,
                    ContextLimit = 400000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-5.4",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9380,
                    ContextLimit = 1050000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "anthropic/claude-sonnet-4.6",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9375,
                    ContextLimit = 1000000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "google/gemini-3.1-pro-preview-customtools",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.AudioInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9370,
                    ContextLimit = 1048756,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "google/gemini-3.1-pro-preview",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.AudioInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9365,
                    ContextLimit = 1048576,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "anthropic/claude-opus-4.6",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9360,
                    ContextLimit = 1000000,
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
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-5.4-pro",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9350,
                    ContextLimit = 1050000,
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
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "nvidia/nemotron-nano-12b-v2-vl:free",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9335,
                    ContextLimit = 128000,
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
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "nvidia/nemotron-3-nano-30b-a3b",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9315,
                    ContextLimit = 262144,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "mistralai/ministral-14b-2512",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9310,
                    ContextLimit = 262144,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "bytedance-seed/seed-1.6-flash",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9305,
                    ContextLimit = 262144,
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
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "deepseek/deepseek-v3.2",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9295,
                    ContextLimit = 131072,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen3-vl-32b-instruct",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9290,
                    ContextLimit = 262144,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen3-vl-8b-instruct",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9285,
                    ContextLimit = 256000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "allenai/olmo-3-32b-think",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9280,
                    ContextLimit = 65536,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "z-ai/glm-4.6v",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9275,
                    ContextLimit = 131072,
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
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "minimax/minimax-m2.1",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9265,
                    ContextLimit = 204800,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "deepcogito/cogito-v2.1-671b",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9260,
                    ContextLimit = 128000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen3-vl-8b-thinking",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9255,
                    ContextLimit = 256000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "mistralai/mistral-large-2512",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9250,
                    ContextLimit = 262144,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "z-ai/glm-4.7",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9245,
                    ContextLimit = 202752,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "bytedance-seed/seed-1.6",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9240,
                    ContextLimit = 262144,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "mistralai/devstral-2512",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9235,
                    ContextLimit = 262144,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-5.1-codex-mini",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9230,
                    ContextLimit = 400000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-5-image-mini",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.ImageOutput | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9225,
                    ContextLimit = 400000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "amazon/nova-2-lite-v1",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9220,
                    ContextLimit = 1000000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "moonshotai/kimi-k2-thinking",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9215,
                    ContextLimit = 262144,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "relace/relace-search",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9210,
                    ContextLimit = 256000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "google/gemini-3-flash-preview",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.AudioInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9205,
                    ContextLimit = 1048576,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "anthropic/claude-haiku-4.5",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9200,
                    ContextLimit = 200000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-5.1-codex-max",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9195,
                    ContextLimit = 400000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-5.1",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9190,
                    ContextLimit = 400000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-5.1-chat",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9185,
                    ContextLimit = 128000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-5.1-codex",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9180,
                    ContextLimit = 400000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-5-image",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.ImageOutput | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9175,
                    ContextLimit = 400000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "amazon/nova-premier-v1",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9170,
                    ContextLimit = 1000000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-5.2",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9165,
                    ContextLimit = 400000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "perplexity/sonar-pro-search",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9160,
                    ContextLimit = 200000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "google/gemini-3-pro-image-preview",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.ImageOutput | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9155,
                    ContextLimit = 65536,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "anthropic/claude-opus-4.5",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9150,
                    ContextLimit = 200000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "mistralai/voxtral-small-24b-2507",
                    Capabilities = AICapability.TextInput | AICapability.AudioInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9145,
                    ContextLimit = 32000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-5.2-pro",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9140,
                    ContextLimit = 400000,
                                    },



                // Released between July 2025 and October 2025

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "nvidia/nemotron-nano-9b-v2:free",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9135,
                    ContextLimit = 128000,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-oss-120b:free",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9130,
                    ContextLimit = 131072,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-oss-20b:free",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9125,
                    ContextLimit = 131072,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen3-235b-a22b-2507",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9120,
                    ContextLimit = 262144,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-oss-20b",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9115,
                    ContextLimit = 131072,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-oss-120b",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9110,
                    ContextLimit = 131072,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen3-30b-a3b-instruct-2507",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9105,
                    ContextLimit = 131072,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "bytedance/ui-tars-1.5-7b",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9100,
                    ContextLimit = 128000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen3-coder-30b-a3b-instruct",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9095,
                    ContextLimit = 160000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "nousresearch/hermes-4-70b",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9090,
                    ContextLimit = 131072,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-5-nano",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9085,
                    ContextLimit = 400000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "deepseek/deepseek-v3.2-exp",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9080,
                    ContextLimit = 163840,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "thedrummer/cydonia-24b-v4.1",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9075,
                    ContextLimit = 131072,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen3-vl-30b-a3b-instruct",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9070,
                    ContextLimit = 262144,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen3-next-80b-a3b-thinking",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9065,
                    ContextLimit = 262144,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen-plus-2025-07-28",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9060,
                    ContextLimit = 1000000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen-plus-2025-07-28:thinking",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9055,
                    ContextLimit = 1000000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "deepseek/deepseek-chat-v3.1",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9050,
                    ContextLimit = 163840,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "google/gemini-2.5-flash-lite",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.AudioInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9045,
                    ContextLimit = 1048576,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "z-ai/glm-4.5-air",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9040,
                    ContextLimit = 131072,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen3-vl-235b-a22b-instruct",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9035,
                    ContextLimit = 262144,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "mistralai/codestral-2508",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9030,
                    ContextLimit = 256000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "deepseek/deepseek-v3.1-terminus",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9025,
                    ContextLimit = 163840,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen3-coder-flash",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9020,
                    ContextLimit = 1000000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen3-next-80b-a3b-instruct",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9015,
                    ContextLimit = 262144,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "relace/relace-apply-3",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9010,
                    ContextLimit = 256000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen3-235b-a22b-thinking-2507",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9005,
                    ContextLimit = 262144,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen3-vl-30b-a3b-thinking",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9000,
                    ContextLimit = 131072,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen3-30b-a3b-thinking-2507",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8995,
                    ContextLimit = 131072,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "z-ai/glm-4.5v",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8990,
                    ContextLimit = 65536,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen3-coder",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8985,
                    ContextLimit = 1048576,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "mistralai/mistral-medium-3.1",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8980,
                    ContextLimit = 131072,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-5-mini",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    Default = AICapability.Text2Text | AICapability.Text2Json,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8975,
                    ContextLimit = 400000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen3-vl-235b-a22b-thinking",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8970,
                    ContextLimit = 131072,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "nousresearch/hermes-4-405b",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8965,
                    ContextLimit = 131072,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen3-coder-plus",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8960,
                    ContextLimit = 1000000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "google/gemini-2.5-flash-image",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.ImageOutput | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8955,
                    ContextLimit = 32768,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen3-max",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8950,
                    ContextLimit = 262144,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/o4-mini-deep-research",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8945,
                    ContextLimit = 200000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "ai21/jamba-large-1.7",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8940,
                    ContextLimit = 256000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-5-codex",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8935,
                    ContextLimit = 400000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-5-chat",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8930,
                    ContextLimit = 128000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-5",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8925,
                    ContextLimit = 400000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "anthropic/claude-sonnet-4.5",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8920,
                    ContextLimit = 1000000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/o3-deep-research",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8915,
                    ContextLimit = 200000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "anthropic/claude-opus-4.1",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8910,
                    ContextLimit = 200000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-5-pro",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8905,
                    ContextLimit = 400000,
                                    },



                // Released between April 2025 and July 2025

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "google/gemma-3n-e4b-it",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8900,
                    ContextLimit = 32768,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "meta-llama/llama-guard-4-12b",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8895,
                    ContextLimit = 163840,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "mistralai/mistral-small-3.2-24b-instruct",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8890,
                    ContextLimit = 128000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen3-14b",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8885,
                    ContextLimit = 131702,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen3-32b",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8880,
                    ContextLimit = 131072,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-4.1-nano",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8875,
                    ContextLimit = 1047576,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen3-8b",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8870,
                    ContextLimit = 131072,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen3-30b-a3b",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8865,
                    ContextLimit = 131072,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "tencent/hunyuan-a13b-instruct",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8860,
                    ContextLimit = 131072,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "arcee-ai/coder-large",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8855,
                    ContextLimit = 32768,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "cognitivecomputations/dolphin-mistral-24b-venice-edition",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput,
                    Verified = false,
                    Rank = 8850,
                    ContextLimit = 128000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "morph/morph-v3-fast",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8845,
                    ContextLimit = 81920,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "arcee-ai/virtuoso-large",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8840,
                    ContextLimit = 131072,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "baidu/ernie-4.5-vl-424b-a47b",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8835,
                    ContextLimit = 131072,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-4.1-mini",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8830,
                    ContextLimit = 1047576,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen3-235b-a22b",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8825,
                    ContextLimit = 131072,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "morph/morph-v3-large",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8820,
                    ContextLimit = 262144,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "mistralai/mistral-medium-3",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8815,
                    ContextLimit = 131072,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "deepseek/deepseek-r1-0528",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8810,
                    ContextLimit = 163840,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "minimax/minimax-m1",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8805,
                    ContextLimit = 1000000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "moonshotai/kimi-k2",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8800,
                    ContextLimit = 131072,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "google/gemini-2.5-flash",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.AudioInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8795,
                    ContextLimit = 1048576,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/o4-mini-high",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8790,
                    ContextLimit = 200000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/o4-mini",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8785,
                    ContextLimit = 200000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/o3",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8780,
                    ContextLimit = 200000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-4.1",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8775,
                    ContextLimit = 1047576,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "google/gemini-2.5-pro",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.AudioInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8770,
                    ContextLimit = 1048576,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "google/gemini-2.5-pro-preview",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.AudioInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8765,
                    ContextLimit = 1048576,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "google/gemini-2.5-pro-preview-05-06",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.AudioInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8760,
                    ContextLimit = 1048576,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "anthropic/claude-sonnet-4",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8755,
                    ContextLimit = 1000000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "anthropic/claude-opus-4",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8750,
                    ContextLimit = 200000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/o3-pro",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8745,
                    ContextLimit = 200000,
                                    },



                // Released between January 2025 and April 2025

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "mistralai/mistral-small-24b-instruct-2501",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8740,
                    ContextLimit = 32768,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "google/gemma-3-4b-it",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8735,
                    ContextLimit = 131072,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "google/gemma-3-12b-it",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8730,
                    ContextLimit = 131072,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "google/gemma-3-27b-it",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8725,
                    ContextLimit = 131072,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "rekaai/reka-flash-3",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8720,
                    ContextLimit = 65536,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "meta-llama/llama-4-scout",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8715,
                    ContextLimit = 10000000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "mistralai/mistral-small-3.1-24b-instruct",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8710,
                    ContextLimit = 128000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "meta-llama/llama-4-maverick",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8705,
                    ContextLimit = 1048576,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-4o-mini-search-preview",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8700,
                    ContextLimit = 128000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "mistralai/mistral-saba",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8695,
                    ContextLimit = 32768,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen2.5-vl-72b-instruct",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8690,
                    ContextLimit = 131072,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen-plus",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8685,
                    ContextLimit = 1000000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "thedrummer/skyfall-36b-v2",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8680,
                    ContextLimit = 32768,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "deepseek/deepseek-r1-distill-llama-70b",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8675,
                    ContextLimit = 128000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "deepseek/deepseek-chat-v3-0324",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8670,
                    ContextLimit = 163840,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "perplexity/sonar",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8665,
                    ContextLimit = 127072,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "minimax/minimax-01",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8660,
                    ContextLimit = 1000192,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "aion-labs/aion-rp-llama-3.1-8b",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8655,
                    ContextLimit = 32768,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "deepseek/deepseek-r1",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8650,
                    ContextLimit = 163840,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/o3-mini-high",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8645,
                    ContextLimit = 200000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/o3-mini",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8640,
                    ContextLimit = 200000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "perplexity/sonar-reasoning-pro",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8635,
                    ContextLimit = 128000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "perplexity/sonar-deep-research",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8630,
                    ContextLimit = 128000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "cohere/command-a",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8625,
                    ContextLimit = 256000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-4o-search-preview",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8620,
                    ContextLimit = 128000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "perplexity/sonar-pro",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8615,
                    ContextLimit = 200000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/o1-pro",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8610,
                    ContextLimit = 200000,
                                    },



                // Released between October 2024 and January 2025

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen-2.5-7b-instruct",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8605,
                    ContextLimit = 131072,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "microsoft/phi-4",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8600,
                    ContextLimit = 16384,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "amazon/nova-micro-v1",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8595,
                    ContextLimit = 128000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "cohere/command-r7b-12-2024",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8590,
                    ContextLimit = 128000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "amazon/nova-lite-v1",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8585,
                    ContextLimit = 300000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "meta-llama/llama-3.3-70b-instruct",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8580,
                    ContextLimit = 131072,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "thedrummer/unslopnemo-12b",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    Verified = false,
                    Rank = 8575,
                    ContextLimit = 32768,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "sao10k/l3.3-euryale-70b",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8570,
                    ContextLimit = 131072,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "deepseek/deepseek-chat",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8565,
                    ContextLimit = 131072,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen-2.5-coder-32b-instruct",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8560,
                    ContextLimit = 128000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "sao10k/l3.1-70b-hanami-x1",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8555,
                    ContextLimit = 16000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "amazon/nova-pro-v1",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8550,
                    ContextLimit = 300000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "anthracite-org/magnum-v4-72b",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8545,
                    ContextLimit = 32768,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "mistralai/mistral-large-2407",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8540,
                    ContextLimit = 131072,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-4o-2024-11-20",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8535,
                    ContextLimit = 128000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/o1",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8530,
                    ContextLimit = 200000,
                                    },



                // Released between July 2024 and October 2024

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "meta-llama/llama-3.1-8b-instruct",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8525,
                    ContextLimit = 131072,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "mistralai/mistral-nemo",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8520,
                    ContextLimit = 131072,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "sao10k/l3-lunaris-8b",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8515,
                    ContextLimit = 8192,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "meta-llama/llama-3.2-1b-instruct",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8510,
                    ContextLimit = 131072,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "meta-llama/llama-3.2-3b-instruct",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8505,
                    ContextLimit = 131072,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen-2.5-72b-instruct",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8500,
                    ContextLimit = 131072,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "meta-llama/llama-3.1-70b-instruct",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8495,
                    ContextLimit = 131072,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "thedrummer/rocinante-12b",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8490,
                    ContextLimit = 65536,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "cohere/command-r-08-2024",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8485,
                    ContextLimit = 128000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-4o-mini",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8480,
                    ContextLimit = 128000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-4o-mini-2024-07-18",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8475,
                    ContextLimit = 128000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "google/gemma-2-27b-it",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8470,
                    ContextLimit = 8192,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "nousresearch/hermes-3-llama-3.1-70b",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8465,
                    ContextLimit = 131072,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "sao10k/l3.1-euryale-70b",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8460,
                    ContextLimit = 131072,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "nousresearch/hermes-3-llama-3.1-405b",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8455,
                    ContextLimit = 131072,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "inflection/inflection-3-pi",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8450,
                    ContextLimit = 8000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "inflection/inflection-3-productivity",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8445,
                    ContextLimit = 8000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "cohere/command-r-plus-08-2024",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8440,
                    ContextLimit = 128000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-4o-2024-08-06",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8435,
                    ContextLimit = 128000,
                                    },



                // Released before July 2024 or unknown release date

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openrouter/auto",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.AudioInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.ImageOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8430,
                    ContextLimit = 2000000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gryphe/mythomax-l2-13b",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8425,
                    ContextLimit = 4096,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "microsoft/wizardlm-2-8x22b",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8420,
                    ContextLimit = 65536,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "undi95/remm-slerp-l2-13b",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8415,
                    ContextLimit = 6144,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "mancer/weaver",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8410,
                    ContextLimit = 8000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "anthropic/claude-3-haiku",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8405,
                    ContextLimit = 200000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-3.5-turbo",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8400,
                    ContextLimit = 16385,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-3.5-turbo-0613",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8395,
                    ContextLimit = 4095,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-3.5-turbo-instruct",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8390,
                    ContextLimit = 4095,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-3.5-turbo-16k",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8385,
                    ContextLimit = 16385,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "mistralai/mixtral-8x22b-instruct",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8380,
                    ContextLimit = 65536,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "mistralai/mistral-large",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8375,
                    ContextLimit = 128000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-4o",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8370,
                    ContextLimit = 128000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-4o-2024-05-13",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8365,
                    ContextLimit = 128000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-4-turbo",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8360,
                    ContextLimit = 128000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-4-turbo-preview",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8355,
                    ContextLimit = 128000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-4",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 8350,
                    ContextLimit = 8191,
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
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "z-ai/glm-5v-turbo",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -5,
                    ContextLimit = 202752,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-5.2-chat",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -10,
                    ContextLimit = 128000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen3-next-80b-a3b-instruct:free",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -15,
                    ContextLimit = 262144,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen3-coder:free",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -20,
                    ContextLimit = 1048576,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "nvidia/llama-3.3-nemotron-super-49b-v1.5",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -25,
                    ContextLimit = 131072,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "z-ai/glm-4.6",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -30,
                    ContextLimit = 202752,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "z-ai/glm-4.5",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -35,
                    ContextLimit = 131072,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "moonshotai/kimi-k2-0905",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -40,
                    ContextLimit = 262144,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "cognitivecomputations/dolphin-mistral-24b-venice-edition:free",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -45,
                    ContextLimit = 32768,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "meta-llama/llama-3.3-70b-instruct:free",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -50,
                    ContextLimit = 131072,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "meta-llama/llama-3.2-3b-instruct:free",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -55,
                    ContextLimit = 131072,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "nousresearch/hermes-3-llama-3.1-405b:free",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -60,
                    ContextLimit = 131072,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "meta-llama/llama-3.2-11b-vision-instruct",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -65,
                    ContextLimit = 131072,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "aion-labs/aion-1.0",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.Reasoning,
                    Verified = false,
                    Deprecated = true,
                    Rank = -70,
                    ContextLimit = 131072,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "aion-labs/aion-1.0-mini",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -75,
                    ContextLimit = 131072,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "alfredpros/codellama-7b-instruct-solidity",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -80,
                    ContextLimit = 131072,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "alibaba/tongyi-deepresearch-30b-a3b",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -85,
                    ContextLimit = 131072,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "allenai/olmo-3.1-32b-instruct",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -90,
                    ContextLimit = 65536,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "alpindale/goliath-120b",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -95,
                    ContextLimit = 6144,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "anthropic/claude-3.5-haiku",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -100,
                    ContextLimit = 200000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "anthropic/claude-3.7-sonnet",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -105,
                    ContextLimit = 200000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "anthropic/claude-3.7-sonnet:thinking",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -110,
                    ContextLimit = 200000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "anthropic/claude-opus-4.6-fast",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -115,
                    ContextLimit = 1000000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "arcee-ai/maestro-reasoning",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -120,
                    ContextLimit = 131072,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "arcee-ai/spotlight",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -125,
                    ContextLimit = 131072,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "arcee-ai/trinity-large-preview",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -130,
                    ContextLimit = 131000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "arcee-ai/trinity-mini",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -135,
                    ContextLimit = 131072,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "baidu/ernie-4.5-21b-a3b",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -140,
                    ContextLimit = 120000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "baidu/ernie-4.5-21b-a3b-thinking",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -145,
                    ContextLimit = 131072,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "baidu/ernie-4.5-300b-a47b",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -150,
                    ContextLimit = 123000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "baidu/ernie-4.5-vl-28b-a3b",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -155,
                    ContextLimit = 30000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "baidu/qianfan-ocr-fast:free",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -160,
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
                    Rank = -165,
                    ContextLimit = 128000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "deepseek/deepseek-v3.2-speciale",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -170,
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
                    Rank = -175,
                    ContextLimit = 32768,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "google/gemini-2.0-flash-001",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.AudioInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -180,
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
                    Rank = -185,
                    ContextLimit = 1048576,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "google/gemini-2.5-flash-lite-preview-09-2025",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.AudioInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -190,
                    ContextLimit = 1048576,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "google/gemma-3-12b-it:free",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -195,
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
                    Rank = -200,
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
                    Rank = -205,
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
                    Rank = -210,
                    ContextLimit = 8192,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "google/gemma-3n-e4b-it:free",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -215,
                    ContextLimit = 8192,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "inclusionai/ling-2.6-1t:free",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -220,
                    ContextLimit = 262144,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "inclusionai/ring-2.6-1t:free",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    Verified = false,
                    Deprecated = true,
                    Rank = -225,
                    ContextLimit = 262144,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "liquid/lfm-2-24b-a2b",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -230,
                    ContextLimit = 128000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "meta-llama/llama-3-70b-instruct",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -235,
                    ContextLimit = 8192,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "meta-llama/llama-3-8b-instruct",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -240,
                    ContextLimit = 8192,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "meta-llama/llama-guard-3-8b",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -245,
                    ContextLimit = 131072,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "microsoft/phi-4-mini-instruct",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput,
                    Verified = false,
                    Deprecated = true,
                    Rank = -250,
                    ContextLimit = 131072,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "minimax/minimax-m2.5:free",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -255,
                    ContextLimit = 196608,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "mistralai/devstral-medium",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -260,
                    ContextLimit = 131072,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "mistralai/devstral-small",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -265,
                    ContextLimit = 131072,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "mistralai/mistral-7b-instruct-v0.1",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -270,
                    ContextLimit = 2824,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "mistralai/mistral-large-2411",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -275,
                    ContextLimit = 131072,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "mistralai/mixtral-8x7b-instruct",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -280,
                    ContextLimit = 32768,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "mistralai/pixtral-large-2411",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -285,
                    ContextLimit = 131072,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "nex-agi/deepseek-v3.1-nex-n1",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -290,
                    ContextLimit = 131072,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "nex-agi/nex-n2-pro:free",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    Verified = false,
                    Deprecated = true,
                    Rank = -295,
                    ContextLimit = 262144,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "nousresearch/hermes-2-pro-llama-3-8b",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -300,
                    ContextLimit = 8192,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "nvidia/nemotron-nano-9b-v2",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -305,
                    ContextLimit = 131072,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-4-0314",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -310,
                    ContextLimit = 8191,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-4-1106-preview",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -315,
                    ContextLimit = 128000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openai/gpt-4o-audio-preview",
                    Capabilities = AICapability.TextInput | AICapability.AudioInput | AICapability.TextOutput | AICapability.AudioOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -320,
                    ContextLimit = 128000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "openrouter/owl-alpha",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -325,
                    ContextLimit = 1048756,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "poolside/laguna-xs.2",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    Verified = false,
                    Deprecated = true,
                    Rank = -330,
                    ContextLimit = 262144,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "poolside/laguna-xs.2:free",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -335,
                    ContextLimit = 262144,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "prime-intellect/intellect-3",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -340,
                    ContextLimit = 131072,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen-max",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -345,
                    ContextLimit = 32768,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen-turbo",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -350,
                    ContextLimit = 131072,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen-vl-max",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -355,
                    ContextLimit = 131072,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "qwen/qwen-vl-plus",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -360,
                    ContextLimit = 32768,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "sao10k/l3-euryale-70b",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -365,
                    ContextLimit = 8192,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "switchpoint/router",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -370,
                    ContextLimit = 131072,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "tngtech/deepseek-r1t2-chimera",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -375,
                    ContextLimit = 163840,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "x-ai/grok-3",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -380,
                    ContextLimit = 131072,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "x-ai/grok-3-beta",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -385,
                    ContextLimit = 131072,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "x-ai/grok-3-mini",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -390,
                    ContextLimit = 131072,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "x-ai/grok-3-mini-beta",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -395,
                    ContextLimit = 1000000,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "x-ai/grok-4",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -400,
                    ContextLimit = 256000,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "x-ai/grok-4-fast",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -405,
                    ContextLimit = 262144,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "x-ai/grok-4.1-fast",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -410,
                    ContextLimit = 262144,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "x-ai/grok-code-fast-1",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -415,
                    ContextLimit = 65536,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "xiaomi/mimo-v2-flash",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -420,
                    ContextLimit = 262144,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "xiaomi/mimo-v2-omni",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.AudioInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -425,
                    ContextLimit = 262144,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "xiaomi/mimo-v2-pro",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -430,
                    ContextLimit = 1048576,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "z-ai/glm-4-32b",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -435,
                    ContextLimit = 128000,
                                    },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "z-ai/glm-4.5-air:free",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -440,
                    ContextLimit = 131072,
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
