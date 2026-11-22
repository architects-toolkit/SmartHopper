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
using SmartHopper.ProviderSdk.AICall.Core.Base;
using SmartHopper.ProviderSdk.AICall.Core.Requests;
using SmartHopper.ProviderSdk.AICall.Core.Returns;
using SmartHopper.ProviderSdk.AIModels;
using SmartHopper.ProviderSdk.AIProviders;

namespace SmartHopper.Providers.Gemini
{
    /// <summary>
    /// Google provider-specific model management implementation.
    /// </summary>
    public class GeminiProviderModels : AIProviderModels
    {
        private readonly GeminiProvider provider;

        public GeminiProviderModels(GeminiProvider provider)
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
                // Released between August 2026 and November 2026

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemini-3.8-flash",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.AudioInput | AICapability.SpeechInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    Default = AICapability.Text2Text | AICapability.Text2Json | AICapability.ReasoningChat | AICapability.ToolReasoningChat,
                    SupportsBatch = true,
                    Verified = false,
                    Rank = 10000,
                    ContextLimit = 1048576,
                    Created = new DateTime(2026, 9, 2),
                    Pricing = new AIModelPricing
                    {
                        Entries =
                        {
                            new AIPriceEntry("text_input", AIPriceUnit.Token, 0.00000075m),
                            new AIPriceEntry("text_output", AIPriceUnit.Token, 0.00000375m),
                            new AIPriceEntry("image_input", AIPriceUnit.Token, 0.00000075m),
                            new AIPriceEntry("audio_input", AIPriceUnit.Token, 0.00000075m),
                            new AIPriceEntry("audio_cache_read", AIPriceUnit.Token, 0.000000075m),
                            new AIPriceEntry("cache_read", AIPriceUnit.Token, 0.000000075m),
                            new AIPriceEntry("cache_write", AIPriceUnit.Token, 0.0000000416666666666667m),
                            new AIPriceEntry("internal_reasoning", AIPriceUnit.Token, 0.00000375m),
                            new AIPriceEntry("tool:web_search", AIPriceUnit.Invocation, 0.014m),
                            new AIPriceEntry("image_input", AIPriceUnit.Item, 0.00000075m),
                        },
                    },
                    BatchPricing = new AIModelPricing
                    {
                        Entries =
                        {
                            new AIPriceEntry("text_input", AIPriceUnit.Token, 0.000000375m),
                            new AIPriceEntry("text_output", AIPriceUnit.Token, 0.000001875m),
                            new AIPriceEntry("image_input", AIPriceUnit.Item, 0.000000375m),
                            new AIPriceEntry("audio_input", AIPriceUnit.Token, 0.000000375m),
                            new AIPriceEntry("audio_cache_read", AIPriceUnit.Token, 0.0000000375m),
                            new AIPriceEntry("cache_read", AIPriceUnit.Token, 0.0000000375m),
                            new AIPriceEntry("cache_write", AIPriceUnit.Token, 0.0000000416666666666667m),
                            new AIPriceEntry("internal_reasoning", AIPriceUnit.Token, 0.000001875m),
                            new AIPriceEntry("tool:web_search", AIPriceUnit.Invocation, 0.014m),
                        },
                    },
                    Aliases = new List<string> { "gemini-3.8-flash:batch", "gemini-3-8-flash" },
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemini-omni-1.2-flash",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.VideoOutput | AICapability.Reasoning,
                    Verified = false,
                    Rank = 9995,
                    ContextLimit = 131072,
                    Created = new DateTime(2026, 11, 5),
                    Pricing = new AIModelPricing
                    {
                        Entries =
                        {
                            new AIPriceEntry("text_input", AIPriceUnit.Token, 0.0000015m),
                            new AIPriceEntry("text_output", AIPriceUnit.Token, 0.000009m),
                            new AIPriceEntry("tool:web_search", AIPriceUnit.Invocation, 0.014m),
                        },
                    },
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemini-omni-1.1-flash",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.VideoOutput | AICapability.Reasoning,
                    Verified = false,
                    Rank = 9990,
                    ContextLimit = 131072,
                    Created = new DateTime(2026, 11, 4),
                    Pricing = new AIModelPricing
                    {
                        Entries =
                        {
                            new AIPriceEntry("text_input", AIPriceUnit.Token, 0.0000015m),
                            new AIPriceEntry("text_output", AIPriceUnit.Token, 0.000009m),
                            new AIPriceEntry("tool:web_search", AIPriceUnit.Invocation, 0.014m),
                        },
                    },
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemini-4-hydrogen",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.AudioInput | AICapability.SpeechInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsBatch = true,
                    Verified = false,
                    Rank = 9985,
                    ContextLimit = 1048576,
                    Created = new DateTime(2026, 11, 12),
                    Pricing = new AIModelPricing
                    {
                        Entries =
                        {
                            new AIPriceEntry("text_input", AIPriceUnit.Token, 0.0000015m),
                            new AIPriceEntry("text_output", AIPriceUnit.Token, 0.0000075m),
                            new AIPriceEntry("image_input", AIPriceUnit.Item, 0.0000015m),
                            new AIPriceEntry("audio_input", AIPriceUnit.Token, 0.0000015m),
                            new AIPriceEntry("audio_cache_read", AIPriceUnit.Token, 0.000000075m),
                            new AIPriceEntry("cache_read", AIPriceUnit.Token, 0.000000075m),
                            new AIPriceEntry("cache_write", AIPriceUnit.Token, 0.0000000833333333333333m),
                            new AIPriceEntry("internal_reasoning", AIPriceUnit.Token, 0.0000075m),
                            new AIPriceEntry("tool:web_search", AIPriceUnit.Invocation, 0.014m),
                        },
                    },
                    BatchPricing = new AIModelPricing
                    {
                        Entries =
                        {
                            new AIPriceEntry("text_input", AIPriceUnit.Token, 0.00000075m),
                            new AIPriceEntry("text_output", AIPriceUnit.Token, 0.00000375m),
                            new AIPriceEntry("image_input", AIPriceUnit.Item, 0.00000075m),
                            new AIPriceEntry("audio_input", AIPriceUnit.Token, 0.00000075m),
                            new AIPriceEntry("audio_cache_read", AIPriceUnit.Token, 0.0000000375m),
                            new AIPriceEntry("cache_read", AIPriceUnit.Token, 0.0000000375m),
                            new AIPriceEntry("cache_write", AIPriceUnit.Token, 0.0000000833333333333333m),
                            new AIPriceEntry("internal_reasoning", AIPriceUnit.Token, 0.00000375m),
                            new AIPriceEntry("tool:web_search", AIPriceUnit.Invocation, 0.014m),
                        },
                    },
                    Aliases = new List<string> { "gemini-4-hydrogen:batch" },
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemini-4-carbon",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.AudioInput | AICapability.SpeechInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsBatch = true,
                    Verified = false,
                    Rank = 9980,
                    ContextLimit = 1048576,
                    Created = new DateTime(2026, 11, 10),
                    Pricing = new AIModelPricing
                    {
                        Entries =
                        {
                            new AIPriceEntry("text_input", AIPriceUnit.Token, 0.000002m),
                            new AIPriceEntry("text_output", AIPriceUnit.Token, 0.00001m),
                            new AIPriceEntry("image_input", AIPriceUnit.Item, 0.000002m),
                            new AIPriceEntry("audio_input", AIPriceUnit.Token, 0.000002m),
                            new AIPriceEntry("audio_cache_read", AIPriceUnit.Token, 0.0000001m),
                            new AIPriceEntry("cache_read", AIPriceUnit.Token, 0.0000001m),
                            new AIPriceEntry("cache_write", AIPriceUnit.Token, 0.0000001875m),
                            new AIPriceEntry("internal_reasoning", AIPriceUnit.Token, 0.00001m),
                            new AIPriceEntry("tool:web_search", AIPriceUnit.Invocation, 0.014m),
                        },
                    },
                    BatchPricing = new AIModelPricing
                    {
                        Entries =
                        {
                            new AIPriceEntry("text_input", AIPriceUnit.Token, 0.000001m),
                            new AIPriceEntry("text_output", AIPriceUnit.Token, 0.000005m),
                            new AIPriceEntry("image_input", AIPriceUnit.Item, 0.000001m),
                            new AIPriceEntry("audio_input", AIPriceUnit.Token, 0.000001m),
                            new AIPriceEntry("audio_cache_read", AIPriceUnit.Token, 0.00000005m),
                            new AIPriceEntry("cache_read", AIPriceUnit.Token, 0.00000005m),
                            new AIPriceEntry("cache_write", AIPriceUnit.Token, 0.0000001875m),
                            new AIPriceEntry("internal_reasoning", AIPriceUnit.Token, 0.000005m),
                            new AIPriceEntry("tool:web_search", AIPriceUnit.Invocation, 0.014m),
                        },
                    },
                    Aliases = new List<string> { "gemini-4-carbon:batch" },
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemini-4-argon",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.AudioInput | AICapability.SpeechInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsBatch = true,
                    Verified = false,
                    Rank = 9975,
                    ContextLimit = 1048576,
                    Created = new DateTime(2026, 10, 13),
                    Pricing = new AIModelPricing
                    {
                        Entries =
                        {
                            new AIPriceEntry("text_input", AIPriceUnit.Token, 0.000002m),
                            new AIPriceEntry("text_output", AIPriceUnit.Token, 0.00001m),
                            new AIPriceEntry("image_input", AIPriceUnit.Item, 0.000002m),
                            new AIPriceEntry("audio_input", AIPriceUnit.Token, 0.000002m),
                            new AIPriceEntry("audio_cache_read", AIPriceUnit.Token, 0.0000001m),
                            new AIPriceEntry("cache_read", AIPriceUnit.Token, 0.0000001m),
                            new AIPriceEntry("cache_write", AIPriceUnit.Token, 0.0000001875m),
                            new AIPriceEntry("internal_reasoning", AIPriceUnit.Token, 0.00001m),
                            new AIPriceEntry("tool:web_search", AIPriceUnit.Invocation, 0.014m),
                        },
                    },
                    BatchPricing = new AIModelPricing
                    {
                        Entries =
                        {
                            new AIPriceEntry("text_input", AIPriceUnit.Token, 0.000001m),
                            new AIPriceEntry("text_output", AIPriceUnit.Token, 0.000005m),
                            new AIPriceEntry("image_input", AIPriceUnit.Item, 0.000001m),
                            new AIPriceEntry("audio_input", AIPriceUnit.Token, 0.000001m),
                            new AIPriceEntry("audio_cache_read", AIPriceUnit.Token, 0.00000005m),
                            new AIPriceEntry("cache_read", AIPriceUnit.Token, 0.00000005m),
                            new AIPriceEntry("cache_write", AIPriceUnit.Token, 0.0000001875m),
                            new AIPriceEntry("internal_reasoning", AIPriceUnit.Token, 0.000005m),
                            new AIPriceEntry("tool:web_search", AIPriceUnit.Invocation, 0.014m),
                        },
                    },
                    Aliases = new List<string> { "gemini-4-argon:batch" },
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemini-4-neon",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.AudioInput | AICapability.SpeechInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsBatch = true,
                    Verified = false,
                    Rank = 9970,
                    ContextLimit = 1048576,
                    Created = new DateTime(2026, 11, 18),
                    Pricing = new AIModelPricing
                    {
                        Entries =
                        {
                            new AIPriceEntry("text_input", AIPriceUnit.Token, 0.000003m),
                            new AIPriceEntry("text_output", AIPriceUnit.Token, 0.000015m),
                            new AIPriceEntry("image_input", AIPriceUnit.Item, 0.000003m),
                            new AIPriceEntry("audio_input", AIPriceUnit.Token, 0.000003m),
                            new AIPriceEntry("audio_cache_read", AIPriceUnit.Token, 0.00000015m),
                            new AIPriceEntry("cache_read", AIPriceUnit.Token, 0.00000015m),
                            new AIPriceEntry("cache_write", AIPriceUnit.Token, 0.0000000833333333333333m),
                            new AIPriceEntry("internal_reasoning", AIPriceUnit.Token, 0.000015m),
                            new AIPriceEntry("tool:web_search", AIPriceUnit.Invocation, 0.014m),
                        },
                    },
                    BatchPricing = new AIModelPricing
                    {
                        Entries =
                        {
                            new AIPriceEntry("text_input", AIPriceUnit.Token, 0.0000015m),
                            new AIPriceEntry("text_output", AIPriceUnit.Token, 0.0000075m),
                            new AIPriceEntry("image_input", AIPriceUnit.Item, 0.0000015m),
                            new AIPriceEntry("audio_input", AIPriceUnit.Token, 0.0000015m),
                            new AIPriceEntry("audio_cache_read", AIPriceUnit.Token, 0.000000075m),
                            new AIPriceEntry("cache_read", AIPriceUnit.Token, 0.000000075m),
                            new AIPriceEntry("cache_write", AIPriceUnit.Token, 0.0000000833333333333333m),
                            new AIPriceEntry("internal_reasoning", AIPriceUnit.Token, 0.0000075m),
                            new AIPriceEntry("tool:web_search", AIPriceUnit.Invocation, 0.014m),
                        },
                    },
                    Aliases = new List<string> { "gemini-4-neon:batch" },
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemini-nano-banana-2.1",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.ImageOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    Verified = false,
                    Rank = 9965,
                    ContextLimit = 65536,
                    Created = new DateTime(2026, 10, 6),
                    Pricing = new AIModelPricing
                    {
                        Entries =
                        {
                            new AIPriceEntry("text_input", AIPriceUnit.Token, 0.0000015m),
                            new AIPriceEntry("text_output", AIPriceUnit.Token, 0.0000075m),
                            new AIPriceEntry("image_output", AIPriceUnit.Token, 0.00003m),
                            new AIPriceEntry("tool:web_search", AIPriceUnit.Invocation, 0.014m),
                        },
                    },
                },



                // Released between May 2026 and August 2026

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemini-3.5-flash-lite",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.AudioInput | AICapability.SpeechInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsBatch = true,
                    Verified = false,
                    Rank = 9960,
                    ContextLimit = 1048576,
                    Created = new DateTime(2026, 7, 21),
                    Pricing = new AIModelPricing
                    {
                        Entries =
                        {
                            new AIPriceEntry("text_input", AIPriceUnit.Token, 0.0000003m),
                            new AIPriceEntry("text_output", AIPriceUnit.Token, 0.0000025m),
                            new AIPriceEntry("image_input", AIPriceUnit.Token, 0.0000003m),
                            new AIPriceEntry("audio_input", AIPriceUnit.Token, 0.0000003m),
                            new AIPriceEntry("audio_cache_read", AIPriceUnit.Token, 0.00000003m),
                            new AIPriceEntry("cache_read", AIPriceUnit.Token, 0.00000003m),
                            new AIPriceEntry("cache_write", AIPriceUnit.Token, 0.0000000833333333333333m),
                            new AIPriceEntry("internal_reasoning", AIPriceUnit.Token, 0.0000025m),
                            new AIPriceEntry("tool:web_search", AIPriceUnit.Invocation, 0.014m),
                            new AIPriceEntry("image_input", AIPriceUnit.Item, 0.0000003m),
                        },
                    },
                    BatchPricing = new AIModelPricing
                    {
                        Entries =
                        {
                            new AIPriceEntry("text_input", AIPriceUnit.Token, 0.00000015m),
                            new AIPriceEntry("text_output", AIPriceUnit.Token, 0.00000125m),
                            new AIPriceEntry("image_input", AIPriceUnit.Item, 0.00000015m),
                            new AIPriceEntry("audio_input", AIPriceUnit.Token, 0.00000015m),
                            new AIPriceEntry("audio_cache_read", AIPriceUnit.Token, 0.000000015m),
                            new AIPriceEntry("cache_read", AIPriceUnit.Token, 0.000000015m),
                            new AIPriceEntry("internal_reasoning", AIPriceUnit.Token, 0.00000125m),
                            new AIPriceEntry("tool:web_search", AIPriceUnit.Invocation, 0.014m),
                        },
                    },
                    Aliases = new List<string> { "gemini-3.5-flash-lite:batch", "gemini-3-5-flash-lite" },
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemini-3.7-flash",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.AudioInput | AICapability.SpeechInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsBatch = true,
                    Verified = false,
                    Rank = 9955,
                    ContextLimit = 1048576,
                    Created = new DateTime(2026, 8, 13),
                    Pricing = new AIModelPricing
                    {
                        Entries =
                        {
                            new AIPriceEntry("text_input", AIPriceUnit.Token, 0.00000075m),
                            new AIPriceEntry("text_output", AIPriceUnit.Token, 0.00000375m),
                            new AIPriceEntry("image_input", AIPriceUnit.Token, 0.00000075m),
                            new AIPriceEntry("audio_input", AIPriceUnit.Token, 0.00000075m),
                            new AIPriceEntry("audio_cache_read", AIPriceUnit.Token, 0.000000075m),
                            new AIPriceEntry("cache_read", AIPriceUnit.Token, 0.000000075m),
                            new AIPriceEntry("cache_write", AIPriceUnit.Token, 0.0000000416666666666667m),
                            new AIPriceEntry("internal_reasoning", AIPriceUnit.Token, 0.00000375m),
                            new AIPriceEntry("tool:web_search", AIPriceUnit.Invocation, 0.014m),
                            new AIPriceEntry("image_input", AIPriceUnit.Item, 0.00000075m),
                        },
                    },
                    BatchPricing = new AIModelPricing
                    {
                        Entries =
                        {
                            new AIPriceEntry("text_input", AIPriceUnit.Token, 0.000000375m),
                            new AIPriceEntry("text_output", AIPriceUnit.Token, 0.000001875m),
                            new AIPriceEntry("image_input", AIPriceUnit.Item, 0.000000375m),
                            new AIPriceEntry("audio_input", AIPriceUnit.Token, 0.000000375m),
                            new AIPriceEntry("audio_cache_read", AIPriceUnit.Token, 0.0000000375m),
                            new AIPriceEntry("cache_read", AIPriceUnit.Token, 0.0000000375m),
                            new AIPriceEntry("cache_write", AIPriceUnit.Token, 0.0000000416666666666667m),
                            new AIPriceEntry("internal_reasoning", AIPriceUnit.Token, 0.000001875m),
                            new AIPriceEntry("tool:web_search", AIPriceUnit.Invocation, 0.014m),
                        },
                    },
                    Aliases = new List<string> { "gemini-3.7-flash:batch", "gemini-3-7-flash" },
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemini-3.6-flash",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.AudioInput | AICapability.SpeechInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsBatch = true,
                    Verified = false,
                    Rank = 9950,
                    ContextLimit = 1048576,
                    Created = new DateTime(2026, 7, 21),
                    Pricing = new AIModelPricing
                    {
                        Entries =
                        {
                            new AIPriceEntry("text_input", AIPriceUnit.Token, 0.00000075m),
                            new AIPriceEntry("text_output", AIPriceUnit.Token, 0.00000375m),
                            new AIPriceEntry("image_input", AIPriceUnit.Token, 0.00000075m),
                            new AIPriceEntry("audio_input", AIPriceUnit.Token, 0.00000075m),
                            new AIPriceEntry("audio_cache_read", AIPriceUnit.Token, 0.000000075m),
                            new AIPriceEntry("cache_read", AIPriceUnit.Token, 0.000000075m),
                            new AIPriceEntry("cache_write", AIPriceUnit.Token, 0.0000000416666666666667m),
                            new AIPriceEntry("internal_reasoning", AIPriceUnit.Token, 0.00000375m),
                            new AIPriceEntry("tool:web_search", AIPriceUnit.Invocation, 0.014m),
                            new AIPriceEntry("image_input", AIPriceUnit.Item, 0.00000075m),
                        },
                    },
                    BatchPricing = new AIModelPricing
                    {
                        Entries =
                        {
                            new AIPriceEntry("text_input", AIPriceUnit.Token, 0.000000375m),
                            new AIPriceEntry("text_output", AIPriceUnit.Token, 0.000001875m),
                            new AIPriceEntry("image_input", AIPriceUnit.Item, 0.000000375m),
                            new AIPriceEntry("audio_input", AIPriceUnit.Token, 0.000000375m),
                            new AIPriceEntry("audio_cache_read", AIPriceUnit.Token, 0.0000000375m),
                            new AIPriceEntry("cache_read", AIPriceUnit.Token, 0.0000000375m),
                            new AIPriceEntry("cache_write", AIPriceUnit.Token, 0.0000000416666666666667m),
                            new AIPriceEntry("internal_reasoning", AIPriceUnit.Token, 0.000001875m),
                            new AIPriceEntry("tool:web_search", AIPriceUnit.Invocation, 0.014m),
                        },
                    },
                    Aliases = new List<string> { "gemini-3.6-flash:batch", "gemini-3-6-flash" },
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemini-3.1-flash-lite-image",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.ImageOutput | AICapability.JsonOutput | AICapability.Reasoning,
                    Default = AICapability.Text2Image | AICapability.Image2Image | AICapability.Image2Text,
                    Verified = false,
                    Rank = 9945,
                    ContextLimit = 65536,
                    Created = new DateTime(2026, 6, 30),
                    Pricing = new AIModelPricing
                    {
                        Entries =
                        {
                            new AIPriceEntry("text_input", AIPriceUnit.Token, 0.00000025m),
                            new AIPriceEntry("text_output", AIPriceUnit.Token, 0.0000015m),
                            new AIPriceEntry("image_output", AIPriceUnit.Token, 0.00003m),
                            new AIPriceEntry("tool:web_search", AIPriceUnit.Invocation, 0.014m),
                        },
                    },
                    Aliases = new List<string> { "gemini-3-1-flash-lite-image" },
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemini-3.1-flash-image",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.ImageOutput | AICapability.JsonOutput | AICapability.Reasoning,
                    Verified = false,
                    Rank = 9940,
                    ContextLimit = 131072,
                    Created = new DateTime(2026, 6, 18),
                    Pricing = new AIModelPricing
                    {
                        Entries =
                        {
                            new AIPriceEntry("text_input", AIPriceUnit.Token, 0.0000005m),
                            new AIPriceEntry("text_output", AIPriceUnit.Token, 0.000003m),
                            new AIPriceEntry("image_output", AIPriceUnit.Token, 0.00006m),
                            new AIPriceEntry("tool:web_search", AIPriceUnit.Invocation, 0.014m),
                        },
                    },
                    Aliases = new List<string> { "gemini-3-1-flash-image" },
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemini-3-pro-image",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.ImageOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    Verified = false,
                    Rank = 9935,
                    ContextLimit = 131072,
                    Created = new DateTime(2026, 6, 18),
                    Pricing = new AIModelPricing
                    {
                        Entries =
                        {
                            new AIPriceEntry("text_input", AIPriceUnit.Token, 0.000002m),
                            new AIPriceEntry("text_output", AIPriceUnit.Token, 0.000012m),
                            new AIPriceEntry("image_input", AIPriceUnit.Token, 0.000002m),
                            new AIPriceEntry("image_output", AIPriceUnit.Token, 0.00012m),
                            new AIPriceEntry("audio_input", AIPriceUnit.Token, 0.000002m),
                            new AIPriceEntry("audio_cache_read", AIPriceUnit.Token, 0.0000002m),
                            new AIPriceEntry("cache_read", AIPriceUnit.Token, 0.0000002m),
                            new AIPriceEntry("cache_write", AIPriceUnit.Token, 0.000000375m),
                            new AIPriceEntry("internal_reasoning", AIPriceUnit.Token, 0.000012m),
                            new AIPriceEntry("tool:web_search", AIPriceUnit.Invocation, 0.014m),
                            new AIPriceEntry("image_input", AIPriceUnit.Item, 0.000002m),
                        },
                    },
                },



                // Released between February 2026 and May 2026

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "lyria-3-pro-preview",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.AudioOutput | AICapability.JsonOutput,
                    Verified = false,
                    Rank = 9930,
                    ContextLimit = 1048576,
                    Created = new DateTime(2026, 3, 30),
                    Pricing = new AIModelPricing
                    {
                        Entries =
                        {
                            new AIPriceEntry("text_input", AIPriceUnit.Token, 0m),
                            new AIPriceEntry("text_output", AIPriceUnit.Token, 0m),
                        },
                    },
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "lyria-3-clip-preview",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.AudioOutput | AICapability.JsonOutput,
                    Verified = false,
                    Rank = 9925,
                    ContextLimit = 1048576,
                    Created = new DateTime(2026, 3, 30),
                    Pricing = new AIModelPricing
                    {
                        Entries =
                        {
                            new AIPriceEntry("text_input", AIPriceUnit.Token, 0m),
                            new AIPriceEntry("text_output", AIPriceUnit.Token, 0m),
                        },
                    },
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemma-4-26b-a4b-it",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    Verified = false,
                    Rank = 9920,
                    ContextLimit = 262144,
                    Created = new DateTime(2026, 4, 3),
                    Pricing = new AIModelPricing
                    {
                        Entries =
                        {
                            new AIPriceEntry("text_input", AIPriceUnit.Token, 0.0000000675m),
                            new AIPriceEntry("text_output", AIPriceUnit.Token, 0.000000225m),
                            new AIPriceEntry("cache_read", AIPriceUnit.Token, 0.0000000375m),
                        },
                    },
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemma-4-31b-it",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsBatch = true,
                    Verified = false,
                    Rank = 9915,
                    ContextLimit = 262144,
                    Created = new DateTime(2026, 4, 2),
                    Aliases = new List<string> { "gemma-4-31b-it:batch" },
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemini-3.1-flash-lite",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.AudioInput | AICapability.SpeechInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsBatch = true,
                    Verified = false,
                    Rank = 9910,
                    ContextLimit = 1048576,
                    Created = new DateTime(2026, 5, 7),
                    Pricing = new AIModelPricing
                    {
                        Entries =
                        {
                            new AIPriceEntry("text_input", AIPriceUnit.Token, 0.00000025m),
                            new AIPriceEntry("text_output", AIPriceUnit.Token, 0.0000015m),
                            new AIPriceEntry("image_input", AIPriceUnit.Token, 0.00000025m),
                            new AIPriceEntry("audio_input", AIPriceUnit.Token, 0.0000005m),
                            new AIPriceEntry("audio_cache_read", AIPriceUnit.Token, 0.00000005m),
                            new AIPriceEntry("cache_read", AIPriceUnit.Token, 0.000000025m),
                            new AIPriceEntry("cache_write", AIPriceUnit.Token, 0.0000000833333333333333m),
                            new AIPriceEntry("internal_reasoning", AIPriceUnit.Token, 0.0000015m),
                            new AIPriceEntry("tool:web_search", AIPriceUnit.Invocation, 0.014m),
                            new AIPriceEntry("image_input", AIPriceUnit.Item, 0.00000025m),
                        },
                    },
                    BatchPricing = new AIModelPricing
                    {
                        Entries =
                        {
                            new AIPriceEntry("text_input", AIPriceUnit.Token, 0.000000125m),
                            new AIPriceEntry("text_output", AIPriceUnit.Token, 0.00000075m),
                            new AIPriceEntry("image_input", AIPriceUnit.Item, 0.000000125m),
                            new AIPriceEntry("audio_input", AIPriceUnit.Token, 0.00000025m),
                            new AIPriceEntry("audio_cache_read", AIPriceUnit.Token, 0.000000025m),
                            new AIPriceEntry("cache_read", AIPriceUnit.Token, 0.0000000125m),
                            new AIPriceEntry("internal_reasoning", AIPriceUnit.Token, 0.00000075m),
                            new AIPriceEntry("tool:web_search", AIPriceUnit.Invocation, 0.014m),
                        },
                    },
                    Aliases = new List<string> { "gemini-3.1-flash-lite:batch", "gemini-3-1-flash-lite" },
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemini-3.1-flash-lite-preview",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.AudioInput | AICapability.SpeechInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    Verified = false,
                    Rank = 9905,
                    ContextLimit = 1048576,
                    Created = new DateTime(2026, 3, 3),
                    Pricing = new AIModelPricing
                    {
                        Entries =
                        {
                            new AIPriceEntry("text_input", AIPriceUnit.Token, 0.00000025m),
                            new AIPriceEntry("text_output", AIPriceUnit.Token, 0.0000015m),
                            new AIPriceEntry("image_input", AIPriceUnit.Token, 0.00000025m),
                            new AIPriceEntry("audio_input", AIPriceUnit.Token, 0.0000005m),
                            new AIPriceEntry("audio_cache_read", AIPriceUnit.Token, 0.00000005m),
                            new AIPriceEntry("cache_read", AIPriceUnit.Token, 0.000000025m),
                            new AIPriceEntry("cache_write", AIPriceUnit.Token, 0.0000000833333333333333m),
                            new AIPriceEntry("internal_reasoning", AIPriceUnit.Token, 0.0000015m),
                            new AIPriceEntry("tool:web_search", AIPriceUnit.Invocation, 0.014m),
                            new AIPriceEntry("image_input", AIPriceUnit.Item, 0.00000025m),
                        },
                    },
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemini-3.5-flash",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.AudioInput | AICapability.SpeechInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsBatch = true,
                    Verified = false,
                    Rank = 9900,
                    ContextLimit = 1048576,
                    Created = new DateTime(2026, 5, 19),
                    Pricing = new AIModelPricing
                    {
                        Entries =
                        {
                            new AIPriceEntry("text_input", AIPriceUnit.Token, 0.0000015m),
                            new AIPriceEntry("text_output", AIPriceUnit.Token, 0.000009m),
                            new AIPriceEntry("image_input", AIPriceUnit.Token, 0.0000015m),
                            new AIPriceEntry("audio_input", AIPriceUnit.Token, 0.000003m),
                            new AIPriceEntry("audio_cache_read", AIPriceUnit.Token, 0.0000003m),
                            new AIPriceEntry("cache_read", AIPriceUnit.Token, 0.00000015m),
                            new AIPriceEntry("cache_write", AIPriceUnit.Token, 0.0000000833333333333333m),
                            new AIPriceEntry("internal_reasoning", AIPriceUnit.Token, 0.000009m),
                            new AIPriceEntry("tool:web_search", AIPriceUnit.Invocation, 0.014m),
                            new AIPriceEntry("image_input", AIPriceUnit.Item, 0.0000015m),
                        },
                    },
                    BatchPricing = new AIModelPricing
                    {
                        Entries =
                        {
                            new AIPriceEntry("text_input", AIPriceUnit.Token, 0.00000075m),
                            new AIPriceEntry("text_output", AIPriceUnit.Token, 0.0000045m),
                            new AIPriceEntry("image_input", AIPriceUnit.Item, 0.00000075m),
                            new AIPriceEntry("audio_input", AIPriceUnit.Token, 0.0000015m),
                            new AIPriceEntry("audio_cache_read", AIPriceUnit.Token, 0.00000015m),
                            new AIPriceEntry("cache_read", AIPriceUnit.Token, 0.000000075m),
                            new AIPriceEntry("internal_reasoning", AIPriceUnit.Token, 0.0000045m),
                            new AIPriceEntry("tool:web_search", AIPriceUnit.Invocation, 0.014m),
                        },
                    },
                    Aliases = new List<string> { "gemini-3.5-flash:batch", "gemini-3-5-flash" },
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemini-3.1-pro-preview-customtools",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.AudioInput | AICapability.SpeechInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    Verified = false,
                    Rank = 9895,
                    ContextLimit = 1048576,
                    Created = new DateTime(2026, 2, 25),
                    Pricing = new AIModelPricing
                    {
                        Entries =
                        {
                            new AIPriceEntry("text_input", AIPriceUnit.Token, 0.000002m),
                            new AIPriceEntry("text_output", AIPriceUnit.Token, 0.000012m),
                            new AIPriceEntry("image_input", AIPriceUnit.Token, 0.000002m),
                            new AIPriceEntry("audio_input", AIPriceUnit.Token, 0.000002m),
                            new AIPriceEntry("audio_cache_read", AIPriceUnit.Token, 0.0000002m),
                            new AIPriceEntry("cache_read", AIPriceUnit.Token, 0.0000002m),
                            new AIPriceEntry("cache_write", AIPriceUnit.Token, 0.000000375m),
                            new AIPriceEntry("internal_reasoning", AIPriceUnit.Token, 0.000012m),
                            new AIPriceEntry("tool:web_search", AIPriceUnit.Invocation, 0.014m),
                            new AIPriceEntry("image_input", AIPriceUnit.Item, 0.000002m),
                        },
                    },
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemini-3.1-flash-image-preview",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.ImageOutput | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9890,
                    ContextLimit = 65536,
                    Created = new DateTime(2026, 2, 26),
                    Pricing = new AIModelPricing
                    {
                        Entries =
                        {
                            new AIPriceEntry("text_input", AIPriceUnit.Token, 0.0000005m),
                            new AIPriceEntry("text_output", AIPriceUnit.Token, 0.000003m),
                            new AIPriceEntry("image_output", AIPriceUnit.Token, 0.00006m),
                            new AIPriceEntry("tool:web_search", AIPriceUnit.Invocation, 0.014m),
                        },
                    },
                    Aliases = new List<string> { "gemini-3-1-flash-image-preview" },
                },



                // Released between November 2025 and February 2026

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemini-3-flash-preview",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.AudioInput | AICapability.SpeechInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsBatch = true,
                    Verified = false,
                    Rank = 9885,
                    ContextLimit = 1048576,
                    Created = new DateTime(2025, 12, 17),
                    Pricing = new AIModelPricing
                    {
                        Entries =
                        {
                            new AIPriceEntry("text_input", AIPriceUnit.Token, 0.0000005m),
                            new AIPriceEntry("text_output", AIPriceUnit.Token, 0.000003m),
                            new AIPriceEntry("image_input", AIPriceUnit.Token, 0.0000005m),
                            new AIPriceEntry("audio_input", AIPriceUnit.Token, 0.000001m),
                            new AIPriceEntry("audio_cache_read", AIPriceUnit.Token, 0.0000001m),
                            new AIPriceEntry("cache_read", AIPriceUnit.Token, 0.00000005m),
                            new AIPriceEntry("cache_write", AIPriceUnit.Token, 0.0000000833333333333333m),
                            new AIPriceEntry("internal_reasoning", AIPriceUnit.Token, 0.000003m),
                            new AIPriceEntry("tool:web_search", AIPriceUnit.Invocation, 0.014m),
                            new AIPriceEntry("image_input", AIPriceUnit.Item, 0.0000005m),
                        },
                    },
                    BatchPricing = new AIModelPricing
                    {
                        Entries =
                        {
                            new AIPriceEntry("text_input", AIPriceUnit.Token, 0.00000025m),
                            new AIPriceEntry("text_output", AIPriceUnit.Token, 0.0000015m),
                            new AIPriceEntry("image_input", AIPriceUnit.Item, 0.00000025m),
                            new AIPriceEntry("audio_input", AIPriceUnit.Token, 0.0000005m),
                            new AIPriceEntry("internal_reasoning", AIPriceUnit.Token, 0.0000015m),
                            new AIPriceEntry("tool:web_search", AIPriceUnit.Invocation, 0.014m),
                        },
                    },
                    Aliases = new List<string> { "gemini-3-flash-preview:batch" },
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemini-3.1-pro-preview",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.AudioInput | AICapability.SpeechInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    SupportsBatch = true,
                    Verified = false,
                    Rank = 9880,
                    ContextLimit = 1048576,
                    Created = new DateTime(2026, 2, 19),
                    Pricing = new AIModelPricing
                    {
                        Entries =
                        {
                            new AIPriceEntry("text_input", AIPriceUnit.Token, 0.000002m),
                            new AIPriceEntry("text_output", AIPriceUnit.Token, 0.000012m),
                            new AIPriceEntry("image_input", AIPriceUnit.Token, 0.000002m),
                            new AIPriceEntry("audio_input", AIPriceUnit.Token, 0.000002m),
                            new AIPriceEntry("audio_cache_read", AIPriceUnit.Token, 0.0000002m),
                            new AIPriceEntry("cache_read", AIPriceUnit.Token, 0.0000002m),
                            new AIPriceEntry("cache_write", AIPriceUnit.Token, 0.000000375m),
                            new AIPriceEntry("internal_reasoning", AIPriceUnit.Token, 0.000012m),
                            new AIPriceEntry("tool:web_search", AIPriceUnit.Invocation, 0.014m),
                            new AIPriceEntry("image_input", AIPriceUnit.Item, 0.000002m),
                        },
                    },
                    BatchPricing = new AIModelPricing
                    {
                        Entries =
                        {
                            new AIPriceEntry("text_input", AIPriceUnit.Token, 0.000001m),
                            new AIPriceEntry("text_output", AIPriceUnit.Token, 0.000006m),
                            new AIPriceEntry("image_input", AIPriceUnit.Item, 0.000001m),
                            new AIPriceEntry("audio_input", AIPriceUnit.Token, 0.000001m),
                            new AIPriceEntry("internal_reasoning", AIPriceUnit.Token, 0.000006m),
                            new AIPriceEntry("tool:web_search", AIPriceUnit.Invocation, 0.014m),
                        },
                    },
                    Aliases = new List<string> { "gemini-3.1-pro-preview:batch", "gemini-3-1-pro-preview" },
                },



                // Released between August 2025 and November 2025

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemini-2.5-flash-image",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.ImageOutput | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = true,
                    Rank = 9875,
                    ContextLimit = 32768,
                    Created = new DateTime(2025, 10, 7),
                    Pricing = new AIModelPricing
                    {
                        Entries =
                        {
                            new AIPriceEntry("text_input", AIPriceUnit.Token, 0.0000003m),
                            new AIPriceEntry("text_output", AIPriceUnit.Token, 0.0000025m),
                            new AIPriceEntry("image_input", AIPriceUnit.Token, 0.0000003m),
                            new AIPriceEntry("image_output", AIPriceUnit.Token, 0.00003m),
                            new AIPriceEntry("audio_input", AIPriceUnit.Token, 0.000001m),
                            new AIPriceEntry("audio_cache_read", AIPriceUnit.Token, 0.0000001m),
                            new AIPriceEntry("cache_read", AIPriceUnit.Token, 0.00000003m),
                            new AIPriceEntry("cache_write", AIPriceUnit.Token, 0.0000000833333333333333m),
                            new AIPriceEntry("internal_reasoning", AIPriceUnit.Token, 0.0000025m),
                            new AIPriceEntry("tool:web_search", AIPriceUnit.Invocation, 0.014m),
                            new AIPriceEntry("image_input", AIPriceUnit.Item, 0.0000003m),
                        },
                    },
                    Aliases = new List<string> { "gemini-2-5-flash-image" },
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemini-3-pro-image-preview",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.ImageOutput | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9870,
                    ContextLimit = 65536,
                    Created = new DateTime(2025, 11, 20),
                    Pricing = new AIModelPricing
                    {
                        Entries =
                        {
                            new AIPriceEntry("text_input", AIPriceUnit.Token, 0.000002m),
                            new AIPriceEntry("text_output", AIPriceUnit.Token, 0.000012m),
                            new AIPriceEntry("image_input", AIPriceUnit.Token, 0.000002m),
                            new AIPriceEntry("image_output", AIPriceUnit.Token, 0.00012m),
                            new AIPriceEntry("audio_input", AIPriceUnit.Token, 0.000002m),
                            new AIPriceEntry("audio_cache_read", AIPriceUnit.Token, 0.0000002m),
                            new AIPriceEntry("cache_read", AIPriceUnit.Token, 0.0000002m),
                            new AIPriceEntry("cache_write", AIPriceUnit.Token, 0.000000375m),
                            new AIPriceEntry("internal_reasoning", AIPriceUnit.Token, 0.000012m),
                            new AIPriceEntry("tool:web_search", AIPriceUnit.Invocation, 0.014m),
                            new AIPriceEntry("image_input", AIPriceUnit.Item, 0.000002m),
                        },
                    },
                },



                // Released between May 2025 and August 2025

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemini-2.5-flash",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.AudioInput | AICapability.SpeechInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    SupportsBatch = true,
                    Verified = true,
                    Rank = 9865,
                    ContextLimit = 1048576,
                    Created = new DateTime(2025, 6, 17),
                    Pricing = new AIModelPricing
                    {
                        Entries =
                        {
                            new AIPriceEntry("text_input", AIPriceUnit.Token, 0.0000003m),
                            new AIPriceEntry("text_output", AIPriceUnit.Token, 0.0000025m),
                            new AIPriceEntry("image_input", AIPriceUnit.Token, 0.0000003m),
                            new AIPriceEntry("audio_input", AIPriceUnit.Token, 0.000001m),
                            new AIPriceEntry("audio_cache_read", AIPriceUnit.Token, 0.0000001m),
                            new AIPriceEntry("cache_read", AIPriceUnit.Token, 0.00000003m),
                            new AIPriceEntry("cache_write", AIPriceUnit.Token, 0.0000000833333333333333m),
                            new AIPriceEntry("internal_reasoning", AIPriceUnit.Token, 0.0000025m),
                            new AIPriceEntry("tool:web_search", AIPriceUnit.Invocation, 0.014m),
                            new AIPriceEntry("image_input", AIPriceUnit.Item, 0.0000003m),
                        },
                    },
                    BatchPricing = new AIModelPricing
                    {
                        Entries =
                        {
                            new AIPriceEntry("text_input", AIPriceUnit.Token, 0.00000015m),
                            new AIPriceEntry("text_output", AIPriceUnit.Token, 0.00000125m),
                            new AIPriceEntry("image_input", AIPriceUnit.Token, 0.00000015m),
                            new AIPriceEntry("audio_input", AIPriceUnit.Token, 0.0000005m),
                            new AIPriceEntry("audio_cache_read", AIPriceUnit.Token, 0.0000001m),
                            new AIPriceEntry("cache_read", AIPriceUnit.Token, 0.00000003m),
                            new AIPriceEntry("internal_reasoning", AIPriceUnit.Token, 0.00000125m),
                            new AIPriceEntry("tool:web_search", AIPriceUnit.Invocation, 0.014m),
                        },
                    },
                    Aliases = new List<string> { "gemini-2.5-flash:batch", "gemini-2-5-flash" },
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemini-2.5-pro-preview",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.AudioInput | AICapability.SpeechInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    Verified = false,
                    Rank = 9860,
                    ContextLimit = 1048576,
                    Created = new DateTime(2025, 6, 5),
                    Pricing = new AIModelPricing
                    {
                        Entries =
                        {
                            new AIPriceEntry("text_input", AIPriceUnit.Token, 0.00000125m),
                            new AIPriceEntry("text_output", AIPriceUnit.Token, 0.00001m),
                            new AIPriceEntry("image_input", AIPriceUnit.Token, 0.00000125m),
                            new AIPriceEntry("audio_input", AIPriceUnit.Token, 0.00000125m),
                            new AIPriceEntry("audio_cache_read", AIPriceUnit.Token, 0.000000125m),
                            new AIPriceEntry("cache_read", AIPriceUnit.Token, 0.000000125m),
                            new AIPriceEntry("cache_write", AIPriceUnit.Token, 0.000000375m),
                            new AIPriceEntry("internal_reasoning", AIPriceUnit.Token, 0.00001m),
                            new AIPriceEntry("tool:web_search", AIPriceUnit.Invocation, 0.014m),
                            new AIPriceEntry("image_input", AIPriceUnit.Item, 0.00000125m),
                        },
                    },
                },



                // Released between February 2025 and May 2025

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemma-3-4b-it",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput,
                    Verified = false,
                    Rank = 9855,
                    ContextLimit = 131072,
                    Created = new DateTime(2025, 3, 13),
                    Pricing = new AIModelPricing
                    {
                        Entries =
                        {
                            new AIPriceEntry("text_input", AIPriceUnit.Token, 0.00000004m),
                            new AIPriceEntry("text_output", AIPriceUnit.Token, 0.00000005m),
                            new AIPriceEntry("cache_read", AIPriceUnit.Token, 0.00000002m),
                        },
                    },
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemma-3-12b-it",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    Verified = false,
                    Rank = 9850,
                    ContextLimit = 131072,
                    Created = new DateTime(2025, 3, 13),
                    Pricing = new AIModelPricing
                    {
                        Entries =
                        {
                            new AIPriceEntry("text_input", AIPriceUnit.Token, 0.00000005m),
                            new AIPriceEntry("text_output", AIPriceUnit.Token, 0.00000015m),
                        },
                    },
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemma-3-27b-it",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    Verified = false,
                    Rank = 9845,
                    ContextLimit = 131072,
                    Created = new DateTime(2025, 3, 12),
                    Pricing = new AIModelPricing
                    {
                        Entries =
                        {
                            new AIPriceEntry("text_input", AIPriceUnit.Token, 0.00000008m),
                            new AIPriceEntry("text_output", AIPriceUnit.Token, 0.00000045m),
                            new AIPriceEntry("cache_read", AIPriceUnit.Token, 0.00000004m),
                        },
                    },
                },



                // Released before November 2024 or unknown release date

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemma-2-27b-it",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput,
                    Verified = false,
                    Rank = 9840,
                    ContextLimit = 8192,
                    Created = new DateTime(2024, 7, 13),
                    Pricing = new AIModelPricing
                    {
                        Entries =
                        {
                            new AIPriceEntry("text_input", AIPriceUnit.Token, 0.00000065m),
                            new AIPriceEntry("text_output", AIPriceUnit.Token, 0.00000065m),
                        },
                    },
                },



                // Deprecated models

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemini-2.5-flash-lite",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.AudioInput | AICapability.SpeechInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    SupportsBatch = true,
                    Verified = true,
                    Deprecated = true,
                    Rank = 0,
                    ContextLimit = 1048576,
                    Created = new DateTime(2025, 7, 22),
                    Pricing = new AIModelPricing
                    {
                        Entries =
                        {
                            new AIPriceEntry("text_input", AIPriceUnit.Token, 0.0000001m),
                            new AIPriceEntry("text_output", AIPriceUnit.Token, 0.0000004m),
                            new AIPriceEntry("image_input", AIPriceUnit.Token, 0.0000001m),
                            new AIPriceEntry("audio_input", AIPriceUnit.Token, 0.0000003m),
                            new AIPriceEntry("audio_cache_read", AIPriceUnit.Token, 0.00000003m),
                            new AIPriceEntry("cache_read", AIPriceUnit.Token, 0.00000001m),
                            new AIPriceEntry("cache_write", AIPriceUnit.Token, 0.0000000833333333333333m),
                            new AIPriceEntry("internal_reasoning", AIPriceUnit.Token, 0.0000004m),
                            new AIPriceEntry("tool:web_search", AIPriceUnit.Invocation, 0.014m),
                            new AIPriceEntry("image_input", AIPriceUnit.Item, 0.0000001m),
                        },
                    },
                    BatchPricing = new AIModelPricing
                    {
                        Entries =
                        {
                            new AIPriceEntry("text_input", AIPriceUnit.Token, 0.00000005m),
                            new AIPriceEntry("text_output", AIPriceUnit.Token, 0.0000002m),
                            new AIPriceEntry("image_input", AIPriceUnit.Token, 0.00000005m),
                            new AIPriceEntry("audio_input", AIPriceUnit.Token, 0.00000015m),
                            new AIPriceEntry("audio_cache_read", AIPriceUnit.Token, 0.00000003m),
                            new AIPriceEntry("cache_read", AIPriceUnit.Token, 0.00000001m),
                            new AIPriceEntry("internal_reasoning", AIPriceUnit.Token, 0.0000002m),
                            new AIPriceEntry("tool:web_search", AIPriceUnit.Invocation, 0.014m),
                        },
                    },
                    Aliases = new List<string> { "gemini-2.5-flash-lite:batch", "gemini-2-5-flash-lite" },
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemini-2.5-pro",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.AudioInput | AICapability.SpeechInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    SupportsBatch = true,
                    Verified = true,
                    Deprecated = true,
                    Rank = -5,
                    ContextLimit = 1048576,
                    Created = new DateTime(2025, 6, 17),
                    Pricing = new AIModelPricing
                    {
                        Entries =
                        {
                            new AIPriceEntry("text_input", AIPriceUnit.Token, 0.00000125m),
                            new AIPriceEntry("text_output", AIPriceUnit.Token, 0.00001m),
                            new AIPriceEntry("image_input", AIPriceUnit.Token, 0.00000125m),
                            new AIPriceEntry("audio_input", AIPriceUnit.Token, 0.00000125m),
                            new AIPriceEntry("audio_cache_read", AIPriceUnit.Token, 0.000000125m),
                            new AIPriceEntry("cache_read", AIPriceUnit.Token, 0.000000125m),
                            new AIPriceEntry("cache_write", AIPriceUnit.Token, 0.000000375m),
                            new AIPriceEntry("internal_reasoning", AIPriceUnit.Token, 0.00001m),
                            new AIPriceEntry("tool:web_search", AIPriceUnit.Invocation, 0.014m),
                            new AIPriceEntry("image_input", AIPriceUnit.Item, 0.00000125m),
                        },
                    },
                    BatchPricing = new AIModelPricing
                    {
                        Entries =
                        {
                            new AIPriceEntry("text_input", AIPriceUnit.Token, 0.000000625m),
                            new AIPriceEntry("text_output", AIPriceUnit.Token, 0.000005m),
                            new AIPriceEntry("image_input", AIPriceUnit.Token, 0.000000625m),
                            new AIPriceEntry("audio_input", AIPriceUnit.Token, 0.000000625m),
                            new AIPriceEntry("audio_cache_read", AIPriceUnit.Token, 0.000000125m),
                            new AIPriceEntry("cache_read", AIPriceUnit.Token, 0.000000125m),
                            new AIPriceEntry("internal_reasoning", AIPriceUnit.Token, 0.000005m),
                            new AIPriceEntry("tool:web_search", AIPriceUnit.Invocation, 0.014m),
                        },
                    },
                    Aliases = new List<string> { "gemini-2.5-pro:batch", "gemini-2-5-pro" },
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemini-1.5-flash",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.ImageInput | AICapability.SpeechInput | AICapability.AudioInput,
                    SupportsStreaming = true,
                    Verified = true,
                    Deprecated = true,
                    Rank = -10,
                    ContextLimit = 1000000,
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemini-1.5-pro",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.ImageInput | AICapability.SpeechInput | AICapability.AudioInput,
                    SupportsStreaming = true,
                    Verified = true,
                    Deprecated = true,
                    Rank = -15,
                    ContextLimit = 1000000,
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemini-2.0-flash",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.ImageInput | AICapability.SpeechInput | AICapability.AudioInput,
                    SupportsStreaming = true,
                    Verified = true,
                    Deprecated = true,
                    Rank = -20,
                    ContextLimit = 1000000,
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemini-2.0-flash-001",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.SpeechInput | AICapability.AudioInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    Verified = false,
                    Deprecated = true,
                    Rank = -25,
                    ContextLimit = 1000000,
                    Pricing = new AIModelPricing
                    {
                        Entries =
                        {
                            new AIPriceEntry("text_input", AIPriceUnit.Token, 0.0000001m),
                            new AIPriceEntry("text_output", AIPriceUnit.Token, 0.0000004m),
                            new AIPriceEntry("image_input", AIPriceUnit.Token, 0.0000001m),
                            new AIPriceEntry("audio_input", AIPriceUnit.Token, 0.0000007m),
                            new AIPriceEntry("cache_read", AIPriceUnit.Token, 0.000000025m),
                            new AIPriceEntry("cache_write", AIPriceUnit.Token, 0.00000008333333333333334m),
                            new AIPriceEntry("internal_reasoning", AIPriceUnit.Token, 0.0000004m),
                            new AIPriceEntry("tool:web_search", AIPriceUnit.Invocation, 0.014m),
                        },
                    },
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemini-2.0-flash-lite-001",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.SpeechInput | AICapability.AudioInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    Verified = false,
                    Deprecated = true,
                    Rank = -30,
                    ContextLimit = 1048576,
                    Pricing = new AIModelPricing
                    {
                        Entries =
                        {
                            new AIPriceEntry("text_input", AIPriceUnit.Token, 0.000000075m),
                            new AIPriceEntry("text_output", AIPriceUnit.Token, 0.0000003m),
                            new AIPriceEntry("image_input", AIPriceUnit.Token, 0.000000075m),
                            new AIPriceEntry("audio_input", AIPriceUnit.Token, 0.000000075m),
                            new AIPriceEntry("internal_reasoning", AIPriceUnit.Token, 0.0000003m),
                            new AIPriceEntry("tool:web_search", AIPriceUnit.Invocation, 0.014m),
                        },
                    },
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemini-2.5-flash-lite-preview-09-2025",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.SpeechInput | AICapability.AudioInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    Verified = false,
                    Deprecated = true,
                    Rank = -35,
                    ContextLimit = 1048576,
                    Pricing = new AIModelPricing
                    {
                        Entries =
                        {
                            new AIPriceEntry("text_input", AIPriceUnit.Token, 0.0000001m),
                            new AIPriceEntry("text_output", AIPriceUnit.Token, 0.0000004m),
                            new AIPriceEntry("image_input", AIPriceUnit.Token, 0.0000001m),
                            new AIPriceEntry("audio_input", AIPriceUnit.Token, 0.0000003m),
                            new AIPriceEntry("cache_read", AIPriceUnit.Token, 0.00000001m),
                            new AIPriceEntry("cache_write", AIPriceUnit.Token, 0.00000008333333333333334m),
                            new AIPriceEntry("internal_reasoning", AIPriceUnit.Token, 0.0000004m),
                            new AIPriceEntry("tool:web_search", AIPriceUnit.Invocation, 0.014m),
                        },
                    },
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemini-2.5-flash-preview-tts",
                    Capabilities = AICapability.TextInput | AICapability.SpeechOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -40,
                    ContextLimit = 32000,
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemini-2.5-pro-preview-05-06",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.SpeechInput | AICapability.AudioInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    Verified = false,
                    Deprecated = true,
                    Rank = -45,
                    ContextLimit = 1048576,
                    Pricing = new AIModelPricing
                    {
                        Entries =
                        {
                            new AIPriceEntry("text_input", AIPriceUnit.Token, 0.00000125m),
                            new AIPriceEntry("text_output", AIPriceUnit.Token, 0.00001m),
                            new AIPriceEntry("image_input", AIPriceUnit.Token, 0.00000125m),
                            new AIPriceEntry("audio_input", AIPriceUnit.Token, 0.00000125m),
                            new AIPriceEntry("audio_cache_read", AIPriceUnit.Token, 0.000000125m),
                            new AIPriceEntry("cache_read", AIPriceUnit.Token, 0.000000125m),
                            new AIPriceEntry("cache_write", AIPriceUnit.Token, 0.000000375m),
                            new AIPriceEntry("internal_reasoning", AIPriceUnit.Token, 0.00001m),
                            new AIPriceEntry("tool:web_search", AIPriceUnit.Invocation, 0.014m),
                        },
                    },
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemini-2.5-pro-preview-tts",
                    Capabilities = AICapability.TextInput | AICapability.SpeechOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -50,
                    ContextLimit = 32000,
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemini-3.1-flash-preview",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.ImageInput | AICapability.Reasoning | AICapability.SpeechInput | AICapability.AudioInput,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -55,
                    ContextLimit = 1000000,
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemma-3-12b-it:free",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput,
                    Verified = false,
                    Deprecated = true,
                    Rank = -60,
                    ContextLimit = 32768,
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemma-3-27b-it:free",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput,
                    Verified = false,
                    Deprecated = true,
                    Rank = -65,
                    ContextLimit = 131072,
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemma-3-4b-it:free",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput,
                    Verified = false,
                    Deprecated = true,
                    Rank = -70,
                    ContextLimit = 32768,
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemma-3n-e2b-it",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput,
                    Verified = false,
                    Deprecated = true,
                    Rank = -75,
                    ContextLimit = 8192,
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemma-3n-e2b-it:free",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput,
                    Verified = false,
                    Deprecated = true,
                    Rank = -80,
                    ContextLimit = 8192,
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemma-3n-e4b-it",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput,
                    Verified = false,
                    Deprecated = true,
                    Rank = -85,
                    ContextLimit = 32768,
                    Pricing = new AIModelPricing
                    {
                        Entries =
                        {
                            new AIPriceEntry("text_input", AIPriceUnit.Token, 0.00000006m),
                            new AIPriceEntry("text_output", AIPriceUnit.Token, 0.00000012m),
                        },
                    },
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemma-3n-e4b-it:free",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput,
                    Verified = false,
                    Deprecated = true,
                    Rank = -90,
                    ContextLimit = 8192,
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemma-4-26b-a4b-it:free",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    Verified = false,
                    Deprecated = true,
                    Rank = -95,
                    ContextLimit = 262144,
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemma-4-31b-it:free",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    Verified = false,
                    Deprecated = true,
                    Rank = -100,
                    ContextLimit = 262144,
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "lyria-3-clip",
                    Capabilities = AICapability.TextInput | AICapability.AudioOutput,
                    SupportsStreaming = false,
                    Verified = false,
                    Deprecated = true,
                    Rank = -105,
                    ContextLimit = 32000,
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "lyria-3-pro",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.AudioOutput,
                    SupportsStreaming = false,
                    Verified = false,
                    Deprecated = true,
                    Rank = -110,
                    ContextLimit = 32000,
                }
            };

            return Task.FromResult(models);
        }

        /// <inheritdoc/>
        public override bool SupportsApiModels => true;

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

                var modelsArray = raw["models"] as JArray;

                if (modelsArray == null)
                {
                    return new List<string>();
                }

                var modelNames = new List<string>();

                foreach (var modelObj in modelsArray)
                {
                    var name = modelObj["name"]?.ToString();
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        continue;
                    }

                    if (name.StartsWith("models/", StringComparison.OrdinalIgnoreCase))
                    {
                        name = name.Substring("models/".Length);
                    }

                    modelNames.Add(name);
                }

                return modelNames
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
