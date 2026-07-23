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
using SmartHopper.ProviderSdk.AIProviders;
using SmartHopper.ProviderSdk.AICall.Core.Requests;
using SmartHopper.ProviderSdk.AICall.Core.Returns;
using SmartHopper.ProviderSdk.AIModels;

namespace SmartHopper.Providers.OpenAI
{
    /// <summary>
    /// OpenAI provider-specific model management implementation.
    /// </summary>
    public class OpenAIProviderModels : AIProviderModels
    {
        private readonly OpenAIProvider openAIProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="OpenAIProviderModels"/> class.
        /// </summary>
        /// <param name="provider">The OpenAI provider instance.</param>
        public OpenAIProviderModels(OpenAIProvider provider)
            : base(provider)
        {
            this.openAIProvider = provider;
        }

        /// <inheritdoc/>
        public override Task<List<AIModelCapabilities>> RetrieveModels()
        {
            var provider = this.openAIProvider.Name.ToLowerInvariant();

            var models = new List<AIModelCapabilities>
            {
                // Released between April 2026 and July 2026

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-5.6-luna",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 10000,
                    ContextLimit = 1050000,
                    Created = new DateTime(2026, 7, 9),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.000001m,
                        Completion = 0.000006m,
                        InputCacheRead = 0.0000001m,
                        InputCacheWrite = 0.00000125m,
                        WebSearch = 0.01m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-5.6-terra",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9995,
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
                    Model = "gpt-5.6-sol",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9990,
                    ContextLimit = 1050000,
                    Created = new DateTime(2026, 7, 9),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.000005m,
                        Completion = 0.00003m,
                        InputCacheRead = 0.0000005m,
                        InputCacheWrite = 0.00000625m,
                        WebSearch = 0.01m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-5.5-2026-04-23",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9985,
                    ContextLimit = 1050000,
                    Created = new DateTime(2026, 4, 24),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.000005m,
                        Completion = 0.00003m,
                        InputCacheRead = 0.0000005m,
                        WebSearch = 0.01m,
                    },
                    Aliases = new List<string> { "gpt-5.5", "gpt-5.5-latest" },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-5.5-pro-2026-04-23",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9980,
                    ContextLimit = 1050000,
                    Created = new DateTime(2026, 4, 24),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00003m,
                        Completion = 0.00018m,
                        WebSearch = 0.01m,
                    },
                    Aliases = new List<string> { "gpt-5.5-pro", "gpt-5.5-pro-latest" },
                },



                // Released between January 2026 and April 2026

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-5.4-nano-2026-03-17",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9975,
                    ContextLimit = 400000,
                    Created = new DateTime(2026, 3, 17),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000002m,
                        Completion = 0.00000125m,
                        InputCacheRead = 0.00000002m,
                        WebSearch = 0.01m,
                    },
                    Aliases = new List<string> { "gpt-5.4-nano", "gpt-5.4-nano-latest" },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-5.4-mini-2026-03-17",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    Default = AICapability.Text2Text | AICapability.ToolChat | AICapability.ReasoningChat | AICapability.ToolReasoningChat | AICapability.Text2Json | AICapability.Image2Text,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9970,
                    ContextLimit = 400000,
                    Created = new DateTime(2026, 3, 17),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000075m,
                        Completion = 0.0000045m,
                        InputCacheRead = 0.000000075m,
                        WebSearch = 0.01m,
                    },
                    Aliases = new List<string> { "gpt-5.4-mini", "gpt-5.4-mini-latest" },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-audio-mini-2025-12-15",
                    Capabilities = AICapability.TextInput | AICapability.AudioInput | AICapability.TextOutput | AICapability.AudioOutput | AICapability.FunctionCalling,
                    Default = AICapability.Text2Speech | AICapability.Speech2Text,
                    SupportsStreaming = false,
                    Verified = false,
                    Rank = 9965,
                    ContextLimit = 128000,
                    Created = new DateTime(2026, 1, 19),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000006m,
                        Completion = 0.0000024m,
                        Audio = 0.0000006m,
                        AudioOutput = 0.0000024m,
                    },
                    Aliases = new List<string> { "gpt-audio-mini", "gpt-audio-mini-latest" },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-5.3-chat-latest",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9960,
                    ContextLimit = 400000,
                    Created = new DateTime(2026, 3, 3),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000175m,
                        Completion = 0.000014m,
                        InputCacheRead = 0.000000175m,
                        WebSearch = 0.01m,
                    },
                    Aliases = new List<string> { "gpt-5.3-chat" },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-5.3-codex",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9955,
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
                    Model = "gpt-5.4-2026-03-05",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9950,
                    ContextLimit = 1050000,
                    Created = new DateTime(2026, 3, 5),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000025m,
                        Completion = 0.000015m,
                        InputCacheRead = 0.00000025m,
                        WebSearch = 0.01m,
                    },
                    Aliases = new List<string> { "gpt-5.4", "gpt-5.4-latest" },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-audio-2025-08-28",
                    Capabilities = AICapability.TextInput | AICapability.AudioInput | AICapability.TextOutput | AICapability.AudioOutput | AICapability.FunctionCalling,
                    SupportsStreaming = false,
                    Verified = false,
                    Rank = 9975,
                    ContextLimit = 1050000,
                    Created = new DateTime(2026, 4, 24),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.000005m,
                        Completion = 0.00003m,
                        InputCacheRead = 0.0000005m,
                        WebSearch = 0.01m,
                    },
                    Aliases = new List<string> { "gpt-5.5", "gpt-5.5-latest" },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-5.4-pro-2026-03-05",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9940,
                    ContextLimit = 1050000,
                    Created = new DateTime(2026, 3, 5),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00003m,
                        Completion = 0.00018m,
                        WebSearch = 0.01m,
                    },
                    Aliases = new List<string> { "gpt-5.4-pro", "gpt-5.4-pro-latest" },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-5.5-pro-2026-04-23",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9965,
                    ContextLimit = 1050000,
                    Created = new DateTime(2026, 4, 24),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00003m,
                        Completion = 0.00018m,
                        WebSearch = 0.01m,
                    },
                    Aliases = new List<string> { "gpt-5.5-pro", "gpt-5.5-pro-latest" },
                },


                // Released between October 2025 and January 2026

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-5.1-codex-mini",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9935,
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
                    Model = "gpt-5.1-codex-max",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9955,
                    ContextLimit = 128000,
                    Created = new DateTime(2026, 1, 19),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000006m,
                        Completion = 0.0000024m,
                        Audio = 0.0000006m,
                    },
                    Aliases = new List<string> { "gpt-audio-mini", "gpt-audio-mini-latest" },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-5.1-2025-11-13",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9925,
                    ContextLimit = 400000,
                    Created = new DateTime(2025, 11, 13),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000125m,
                        Completion = 0.00001m,
                        InputCacheRead = 0.00000013m,
                    },
                    Aliases = new List<string> { "gpt-5.1", "gpt-5.1-latest" },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-5.1-chat-latest",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9920,
                    ContextLimit = 128000,
                    Created = new DateTime(2025, 11, 13),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000125m,
                        Completion = 0.00001m,
                        InputCacheRead = 0.000000125m,
                        WebSearch = 0.01m,
                    },
                    Aliases = new List<string> { "gpt-5.1-chat" },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-5.1-codex",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9915,
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
                    Model = "gpt-5.1-codex-max",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9935,
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
                    Model = "gpt-5.2-2025-12-11",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9930,
                    ContextLimit = 400000,
                    Created = new DateTime(2025, 12, 10),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000175m,
                        Completion = 0.000014m,
                        InputCacheRead = 0.000000175m,
                    },
                    Aliases = new List<string> { "gpt-5.2", "gpt-5.2-latest" },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-5.2-chat-latest",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9925,
                    ContextLimit = 128000,
                    Created = new DateTime(2025, 12, 10),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000175m,
                        Completion = 0.000014m,
                        InputCacheRead = 0.000000175m,
                    },
                    Aliases = new List<string> { "gpt-5.2-chat" },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-5.2-codex",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9910,
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
                    Model = "gpt-5.2-chat-latest",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9905,
                    ContextLimit = 128000,
                    Created = new DateTime(2026, 1, 19),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000025m,
                        Completion = 0.00001m,
                        Audio = 0.000032m,
                    },
                    Aliases = new List<string> { "gpt-audio", "gpt-audio-latest" },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-5.2-pro-2025-12-11",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9895,
                    ContextLimit = 400000,
                    Created = new DateTime(2025, 12, 10),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.000021m,
                        Completion = 0.000168m,
                        WebSearch = 0.01m,
                    },
                    Aliases = new List<string> { "gpt-5.2-pro", "gpt-5.2-pro-latest" },
                },



                // Released between July 2025 and October 2025

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-5-mini-2025-08-07",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    Default = AICapability.Text2Text | AICapability.ToolChat | AICapability.ReasoningChat | AICapability.ToolReasoningChat | AICapability.Text2Json | AICapability.Image2Text,
                    SupportsStreaming = true,
                    Verified = true,
                    Rank = 9890,
                    ContextLimit = 400000,
                    Created = new DateTime(2025, 8, 7),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000025m,
                        Completion = 0.000002m,
                        InputCacheRead = 0.000000025m,
                        WebSearch = 0.01m,
                    },
                    Aliases = new List<string> { "gpt-5-mini", "gpt-5-mini-latest" },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-5-nano-2025-08-07",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9885,
                    ContextLimit = 400000,
                    Created = new DateTime(2025, 8, 7),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000005m,
                        Completion = 0.0000004m,
                        InputCacheRead = 0.00000001m,
                    },
                    Aliases = new List<string> { "gpt-5-nano", "gpt-5-nano-latest" },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "o4-mini-deep-research-2025-06-26",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9880,
                    ContextLimit = 200000,
                    Created = new DateTime(2025, 10, 10),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.000002m,
                        Completion = 0.000008m,
                        InputCacheRead = 0.0000005m,
                        WebSearch = 0.01m,
                    },
                    Aliases = new List<string> { "o4-mini-deep-research", "o4-mini-deep-research-latest" },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-5-2025-08-07",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9890,
                    ContextLimit = 400000,
                    Created = new DateTime(2025, 8, 7),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000125m,
                        Completion = 0.00001m,
                        InputCacheRead = 0.000000125m,
                    },
                    Aliases = new List<string> { "gpt-5", "gpt-5-latest" },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-5-chat-latest",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.ImageInput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9885,
                    ContextLimit = 128000,
                    Created = new DateTime(2025, 8, 7),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000125m,
                        Completion = 0.00001m,
                        InputCacheRead = 0.000000125m,
                        WebSearch = 0.01m,
                    },
                    Aliases = new List<string> { "gpt-5-chat" },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-5-codex",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9875,
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
                    Model = "o3-deep-research-2025-06-26",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9860,
                    ContextLimit = 200000,
                    Created = new DateTime(2025, 10, 10),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00001m,
                        Completion = 0.00004m,
                        InputCacheRead = 0.0000025m,
                        WebSearch = 0.01m,
                    },
                    Aliases = new List<string> { "o3-deep-research", "o3-deep-research-latest" },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-4o-audio-preview-2025-06-03",
                    Capabilities = AICapability.TextInput | AICapability.AudioInput | AICapability.TextOutput | AICapability.AudioOutput | AICapability.FunctionCalling,
                    SupportsStreaming = false,
                    Verified = false,
                    Rank = 9870,
                    ContextLimit = 128000,
                    Created = new DateTime(2025, 8, 15),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000025m,
                        Completion = 0.00001m,
                        Audio = 0.00004m,
                    },
                    Aliases = new List<string> { "gpt-4o-audio-preview", "gpt-4o-audio-preview-latest" },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-5-pro-2025-10-06",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9855,
                    ContextLimit = 400000,
                    Created = new DateTime(2025, 10, 6),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.000015m,
                        Completion = 0.00012m,
                        WebSearch = 0.01m,
                    },
                    Aliases = new List<string> { "gpt-5-pro", "gpt-5-pro-latest" },
                },



                // Released between April 2025 and July 2025

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "o3-pro-2025-06-10",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9850,
                    ContextLimit = 200000,
                    Created = new DateTime(2025, 6, 10),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00002m,
                        Completion = 0.00008m,
                        WebSearch = 0.01m,
                    },
                    Aliases = new List<string> { "o3-pro", "o3-pro-latest" },
                },



                // Released between January 2025 and April 2025

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-4.1-nano-2025-04-14",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9845,
                    ContextLimit = 1047576,
                    Created = new DateTime(2025, 4, 14),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000001m,
                        Completion = 0.0000004m,
                        InputCacheRead = 0.000000025m,
                        WebSearch = 0.01m,
                    },
                    Aliases = new List<string> { "gpt-4.1-nano", "gpt-4.1-nano-latest" },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-4o-mini-search-preview-2025-03-11",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9840,
                    ContextLimit = 128000,
                    Created = new DateTime(2025, 3, 12),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000015m,
                        Completion = 0.0000006m,
                        WebSearch = 0.0275m,
                    },
                    Aliases = new List<string> { "gpt-4o-mini-search-preview", "gpt-4o-mini-search-preview-latest" },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-4.1-mini-2025-04-14",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9835,
                    ContextLimit = 1047576,
                    Created = new DateTime(2025, 4, 14),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000004m,
                        Completion = 0.0000016m,
                        InputCacheRead = 0.0000001m,
                        WebSearch = 0.01m,
                    },
                    Aliases = new List<string> { "gpt-4.1-mini", "gpt-4.1-mini-latest" },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "o4-mini-2025-04-16",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9830,
                    ContextLimit = 200000,
                    Created = new DateTime(2025, 4, 16),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000011m,
                        Completion = 0.0000044m,
                        InputCacheRead = 0.000000275m,
                        WebSearch = 0.01m,
                    },
                    Aliases = new List<string> { "o4-mini", "o4-mini-latest" },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "o3-mini-2025-01-31",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9835,
                    ContextLimit = 1047576,
                    Created = new DateTime(2025, 4, 14),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.000002m,
                        Completion = 0.000008m,
                        InputCacheRead = 0.0000005m,
                    },
                    Aliases = new List<string> { "gpt-4.1", "gpt-4.1-latest" },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "o3-2025-04-16",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9820,
                    ContextLimit = 200000,
                    Created = new DateTime(2025, 4, 16),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.000002m,
                        Completion = 0.000008m,
                        InputCacheRead = 0.0000005m,
                        WebSearch = 0.01m,
                    },
                    Aliases = new List<string> { "o3", "o3-latest" },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-4.1-2025-04-14",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9815,
                    ContextLimit = 1047576,
                    Created = new DateTime(2025, 4, 14),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.000002m,
                        Completion = 0.000008m,
                        InputCacheRead = 0.0000005m,
                        WebSearch = 0.01m,
                    },
                    Aliases = new List<string> { "gpt-4.1", "gpt-4.1-latest" },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-4o-search-preview-2025-03-11",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9810,
                    ContextLimit = 128000,
                    Created = new DateTime(2025, 3, 12),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000025m,
                        Completion = 0.00001m,
                        WebSearch = 0.035m,
                    },
                    Aliases = new List<string> { "gpt-4o-search-preview", "gpt-4o-search-preview-latest" },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "o1-pro-2025-03-19",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9805,
                    ContextLimit = 200000,
                    Created = new DateTime(2025, 3, 19),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00015m,
                        Completion = 0.0006m,
                    },
                    Aliases = new List<string> { "o1-pro", "o1-pro-latest" },
                },



                // Released between November 2024 and February 2025

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "o3-mini-2025-01-31",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9815,
                    ContextLimit = 200000,
                    Created = new DateTime(2025, 1, 31),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000011m,
                        Completion = 0.0000044m,
                        InputCacheRead = 0.00000055m,
                    },
                    Aliases = new List<string> { "o3-mini", "o3-mini-latest" },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "o1-2024-12-17",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9800,
                    ContextLimit = 200000,
                    Created = new DateTime(2024, 12, 17),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.000015m,
                        Completion = 0.00006m,
                        InputCacheRead = 0.0000075m,
                    },
                    Aliases = new List<string> { "o1", "o1-latest" },
                },



                // Released between July 2024 and October 2024

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-4o-2024-08-06",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9795,
                    ContextLimit = 128000,
                    Created = new DateTime(2024, 8, 6),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000025m,
                        Completion = 0.00001m,
                        InputCacheRead = 0.00000125m,
                    },
                    Aliases = new List<string> { "gpt-4o-latest", "gpt-4o" },
                },



                // Released before July 2024 or unknown release date

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-4o-mini-2024-07-18",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.ImageInput | AICapability.JsonOutput | AICapability.FunctionCalling,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9800,
                    ContextLimit = 128000,
                    Created = new DateTime(2024, 7, 18),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000015m,
                        Completion = 0.0000006m,
                        InputCacheRead = 0.000000075m,
                    },
                    Aliases = new List<string> { "gpt-4o-mini", "gpt-4o-mini-latest" },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-4o-2024-05-13",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9795,
                    ContextLimit = 128000,
                    Created = new DateTime(2024, 5, 13),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.000005m,
                        Completion = 0.000015m,
                    },
                },



                // Released before May 2024 or unknown release date

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "dall-e-3",
                    Capabilities = AICapability.TextInput | AICapability.ImageOutput,
                    Default = AICapability.Text2Image,
                    SupportsStreaming = false,
                    Verified = true,
                    Rank = 9790,
                    ContextLimit = 128000,
                    Created = new DateTime(2024, 7, 18),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000015m,
                        Completion = 0.0000006m,
                        InputCacheRead = 0.000000075m,
                    },
                    Aliases = new List<string> { "gpt-4o-mini", "gpt-4o-mini-latest" },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-3.5-turbo-instruct",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9785,
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
                    Model = "gpt-3.5-turbo-16k",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9780,
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
                    Model = "chatgpt-image-latest",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput,
                    Verified = false,
                    Rank = 9765,
                    ContextLimit = 128000,
                    Aliases = new List<string> { "chatgpt-image" },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-4o-mini-transcribe-2025-03-20",
                    Capabilities = AICapability.AudioInput | AICapability.TextOutput,
                    SupportsStreaming = false,
                    Verified = false,
                    Rank = 9760,
                    ContextLimit = 16000,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-4o-mini-transcribe-2025-12-15",
                    Capabilities = AICapability.AudioInput | AICapability.TextOutput,
                    SupportsStreaming = false,
                    Verified = false,
                    Rank = 9755,
                    ContextLimit = 16000,
                    Aliases = new List<string> { "gpt-4o-mini-transcribe", "gpt-4o-mini-transcribe-latest" },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-4o-mini-tts-2025-03-20",
                    Capabilities = AICapability.TextInput | AICapability.AudioOutput,
                    SupportsStreaming = false,
                    Verified = false,
                    Rank = 9750,
                    ContextLimit = 2000,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-4o-mini-tts-2025-12-15",
                    Capabilities = AICapability.TextInput | AICapability.AudioOutput,
                    SupportsStreaming = false,
                    Verified = false,
                    Rank = 9745,
                    ContextLimit = 2000,
                    Aliases = new List<string> { "gpt-4o-mini-tts", "gpt-4o-mini-tts-latest" },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-4o-transcribe",
                    Capabilities = AICapability.AudioInput | AICapability.TextOutput,
                    SupportsStreaming = false,
                    Verified = false,
                    Rank = 9740,
                    ContextLimit = 16000,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-4o-transcribe-diarize",
                    Capabilities = AICapability.AudioInput | AICapability.TextOutput,
                    SupportsStreaming = false,
                    Verified = false,
                    Rank = 9735,
                    ContextLimit = 16000,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-5-search-api-2025-10-14",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9730,
                    ContextLimit = 400000,
                    Aliases = new List<string> { "gpt-5-search-api", "gpt-5-search-api-latest" },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-audio-1.5",
                    Capabilities = AICapability.TextInput | AICapability.AudioInput | AICapability.TextOutput | AICapability.AudioOutput | AICapability.FunctionCalling,
                    SupportsStreaming = false,
                    Verified = false,
                    Rank = 9725,
                    ContextLimit = 128000,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-audio-mini-2025-10-06",
                    Capabilities = AICapability.TextInput | AICapability.AudioInput | AICapability.TextOutput | AICapability.AudioOutput | AICapability.FunctionCalling,
                    SupportsStreaming = false,
                    Verified = false,
                    Rank = 9720,
                    ContextLimit = 128000,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-image-1",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.ImageOutput,
                    SupportsStreaming = false,
                    Verified = false,
                    Rank = 9715,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-image-1-mini",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.ImageOutput,
                    SupportsStreaming = false,
                    Verified = false,
                    Rank = 9710,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-image-1.5",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.ImageOutput,
                    SupportsStreaming = false,
                    Verified = false,
                    Rank = 9705,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-image-2-2026-04-21",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.ImageOutput,
                    Default = AICapability.Text2Image | AICapability.Image2Image,
                    SupportsStreaming = false,
                    Verified = false,
                    Rank = 9700,
                    Aliases = new List<string> { "gpt-image-2", "gpt-image-2-latest" },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "omni-moderation-2024-09-26",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput,
                    SupportsStreaming = false,
                    Verified = false,
                    Rank = 9695,
                    Aliases = new List<string> { "omni-moderation-latest", "omni-moderation" },
                    DiscouragedForTools = new List<string> { "*" },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "sora-2",
                    Capabilities = AICapability.TextInput | AICapability.VideoOutput,
                    SupportsStreaming = false,
                    Verified = false,
                    Rank = 9690,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "sora-2-pro",
                    Capabilities = AICapability.TextInput | AICapability.VideoOutput,
                    SupportsStreaming = false,
                    Verified = false,
                    Rank = 9685,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "text-embedding-3-large",
                    Capabilities = AICapability.TextInput | AICapability.EmbedOutput,
                    SupportsStreaming = false,
                    Verified = false,
                    Rank = 9680,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "text-embedding-3-small",
                    Capabilities = AICapability.TextInput | AICapability.EmbedOutput,
                    SupportsStreaming = false,
                    Verified = false,
                    Rank = 9675,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "tts-1",
                    Capabilities = AICapability.TextInput | AICapability.AudioOutput,
                    SupportsStreaming = false,
                    Verified = false,
                    Rank = 9670,
                    ContextLimit = 2000,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "tts-1-1106",
                    Capabilities = AICapability.TextInput | AICapability.AudioOutput,
                    SupportsStreaming = false,
                    Verified = false,
                    Rank = 9665,
                    ContextLimit = 2000,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "tts-1-hd",
                    Capabilities = AICapability.TextInput | AICapability.AudioOutput,
                    SupportsStreaming = false,
                    Verified = false,
                    Rank = 9660,
                    ContextLimit = 2000,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "tts-1-hd-1106",
                    Capabilities = AICapability.TextInput | AICapability.AudioOutput,
                    SupportsStreaming = false,
                    Verified = false,
                    Rank = 9655,
                    ContextLimit = 2000,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "whisper-1",
                    Capabilities = AICapability.SpeechInput | AICapability.TextOutput,
                    Default = AICapability.Speech2Text,
                    SupportsStreaming = false,
                    Verified = false,
                    Rank = 9650,
                },



                // Deprecated models

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-4o-2024-11-20",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = 0,
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
                    Model = "gpt-3.5-turbo",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput,
                    SupportsStreaming = false,
                    Verified = false,
                    Deprecated = true,
                    Rank = -10,
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
                    Model = "gpt-4-turbo-2024-04-09",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -15,
                    ContextLimit = 128000,
                    Created = new DateTime(2024, 4, 9),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00001m,
                        Completion = 0.00003m,
                    },
                    Aliases = new List<string> { "gpt-4-turbo", "gpt-4-turbo-latest" },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-4",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -20,
                    ContextLimit = 8192,
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
                    Model = "babbage-002",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput,
                    SupportsStreaming = false,
                    Verified = false,
                    Deprecated = true,
                    Rank = -25,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "chatgpt-4o-latest",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.ImageInput,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -30,
                    ContextLimit = 128000,
                    Aliases = new List<string> { "chatgpt-4o" },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "codex-mini-latest",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -35,
                    ContextLimit = 200000,
                    Aliases = new List<string> { "codex-mini" },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "dall-e-2",
                    Capabilities = AICapability.TextInput | AICapability.ImageOutput,
                    SupportsStreaming = false,
                    Verified = false,
                    Deprecated = true,
                    Rank = -40,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "davinci-002",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput,
                    SupportsStreaming = false,
                    Verified = false,
                    Deprecated = true,
                    Rank = -45,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-3.5-turbo-0125",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -50,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-3.5-turbo-1106",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -55,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-3.5-turbo-instruct-0914",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -60,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-4-0613",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -65,
                    ContextLimit = 8192,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-4o-audio-preview-2024-12-17",
                    Capabilities = AICapability.TextInput | AICapability.AudioInput | AICapability.TextOutput | AICapability.AudioOutput | AICapability.FunctionCalling,
                    SupportsStreaming = false,
                    Verified = false,
                    Deprecated = true,
                    Rank = -70,
                    ContextLimit = 128000,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-4o-audio-preview-2025-06-03",
                    Capabilities = AICapability.TextInput | AICapability.AudioInput | AICapability.TextOutput | AICapability.AudioOutput | AICapability.FunctionCalling,
                    SupportsStreaming = false,
                    Verified = false,
                    Deprecated = true,
                    Rank = -75,
                    ContextLimit = 128000,
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000025m,
                        Completion = 0.00001m,
                        Audio = 0.00004m,
                    },
                    Aliases = new List<string> { "gpt-4o-audio-preview", "gpt-4o-audio-preview-latest" },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-4o-mini-audio-preview-2024-12-17",
                    Capabilities = AICapability.TextInput | AICapability.AudioInput | AICapability.TextOutput | AICapability.AudioOutput | AICapability.FunctionCalling,
                    SupportsStreaming = false,
                    Verified = false,
                    Deprecated = true,
                    Rank = -80,
                    ContextLimit = 128000,
                    Aliases = new List<string> { "gpt-4o-mini-audio-preview", "gpt-4o-mini-audio-preview-latest" },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "text-embedding-ada-002",
                    Capabilities = AICapability.TextInput | AICapability.EmbedOutput,
                    SupportsStreaming = false,
                    Verified = false,
                    Deprecated = true,
                    Rank = -85,
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

                var response = await this.openAIProvider.Call(request).ConfigureAwait(false);

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