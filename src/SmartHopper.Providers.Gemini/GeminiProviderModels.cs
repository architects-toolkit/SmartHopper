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
                // Released between June 2026 and September 2026

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemini-3.5-flash-lite",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.AudioInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsBatch = true,
                    Verified = false,
                    Rank = 10000,
                    ContextLimit = 1048576,
                    Created = new DateTime(2026, 7, 21),
                    Pricing = new AIModelPricing
                    {
                        TextInput = 0.0000003m,
                        TextOutput = 0.0000025m,
                        ImageInput = 0.0000003m,
                        AudioInput = 0.0000003m,
                        AudioCacheRead = 0.00000003m,
                        CacheRead = 0.00000003m,
                        CacheWrite = 0.0000000833333333333333m,
                        InternalReasoning = 0.0000025m,
                        WebSearch = 0.014m,
                    },
                    BatchPricing = new AIModelPricing
                    {
                        TextInput = 0.00000015m,
                        TextOutput = 0.00000125m,
                        ImageInput = 0.00000015m,
                        AudioInput = 0.00000015m,
                        AudioCacheRead = 0.000000015m,
                        CacheRead = 0.000000015m,
                        InternalReasoning = 0.00000125m,
                        WebSearch = 0.014m,
                    },
                    Aliases = new List<string> { "gemini-3.5-flash-lite:batch" },
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemini-3.8-flash",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.AudioInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsBatch = true,
                    Verified = false,
                    Rank = 9995,
                    ContextLimit = 1048576,
                    Created = new DateTime(2026, 9, 2),
                    Pricing = new AIModelPricing
                    {
                        TextInput = 0.00000075m,
                        TextOutput = 0.00000375m,
                        ImageInput = 0.00000075m,
                        AudioInput = 0.00000075m,
                        AudioCacheRead = 0.000000075m,
                        CacheRead = 0.000000075m,
                        CacheWrite = 0.0000000416666666666667m,
                        InternalReasoning = 0.00000375m,
                        WebSearch = 0.014m,
                    },
                    BatchPricing = new AIModelPricing
                    {
                        TextInput = 0.000000375m,
                        TextOutput = 0.000001875m,
                        ImageInput = 0.000000375m,
                        AudioInput = 0.000000375m,
                        AudioCacheRead = 0.0000000375m,
                        CacheRead = 0.0000000375m,
                        CacheWrite = 0.0000000416666666666667m,
                        InternalReasoning = 0.000001875m,
                        WebSearch = 0.014m,
                    },
                    Aliases = new List<string> { "gemini-3.8-flash:batch" },
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemini-3.7-flash",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.AudioInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsBatch = true,
                    Verified = false,
                    Rank = 9990,
                    ContextLimit = 1048576,
                    Created = new DateTime(2026, 8, 13),
                    Pricing = new AIModelPricing
                    {
                        TextInput = 0.00000075m,
                        TextOutput = 0.00000375m,
                        ImageInput = 0.00000075m,
                        AudioInput = 0.00000075m,
                        AudioCacheRead = 0.000000075m,
                        CacheRead = 0.000000075m,
                        CacheWrite = 0.0000000416666666666667m,
                        InternalReasoning = 0.00000375m,
                        WebSearch = 0.014m,
                    },
                    BatchPricing = new AIModelPricing
                    {
                        TextInput = 0.000000375m,
                        TextOutput = 0.000001875m,
                        ImageInput = 0.000000375m,
                        AudioInput = 0.000000375m,
                        AudioCacheRead = 0.0000000375m,
                        CacheRead = 0.0000000375m,
                        CacheWrite = 0.0000000416666666666667m,
                        InternalReasoning = 0.000001875m,
                        WebSearch = 0.014m,
                    },
                    Aliases = new List<string> { "gemini-3.7-flash:batch" },
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemini-3.6-flash",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.SpeechInput | AICapability.AudioInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsBatch = true,
                    Verified = false,
                    Rank = 9985,
                    ContextLimit = 1048576,
                    Created = new DateTime(2026, 7, 21),
                    Pricing = new AIModelPricing
                    {
                        TextInput = 0.00000075m,
                        TextOutput = 0.00000375m,
                        ImageInput = 0.00000075m,
                        AudioInput = 0.00000075m,
                        AudioCacheRead = 0.000000075m,
                        CacheRead = 0.000000075m,
                        CacheWrite = 0.0000000416666666666667m,
                        InternalReasoning = 0.00000375m,
                        WebSearch = 0.014m,
                    },
                    BatchPricing = new AIModelPricing
                    {
                        TextInput = 0.000000375m,
                        TextOutput = 0.000001875m,
                        ImageInput = 0.000000375m,
                        AudioInput = 0.000000375m,
                        AudioCacheRead = 0.0000000375m,
                        CacheRead = 0.0000000375m,
                        CacheWrite = 0.0000000416666666666667m,
                        InternalReasoning = 0.000001875m,
                        WebSearch = 0.014m,
                    },
                    Aliases = new List<string> { "gemini-3.6-flash:batch" },
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemini-3.1-flash-lite-image",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.ImageOutput | AICapability.JsonOutput | AICapability.Reasoning,
                    Verified = false,
                    Rank = 9980,
                    ContextLimit = 65536,
                    Created = new DateTime(2026, 6, 30),
                    Pricing = new AIModelPricing
                    {
                        TextInput = 0.00000025m,
                        TextOutput = 0.0000015m,
                        ImageOutput = 0.00003m,
                        WebSearch = 0.014m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemini-3.1-flash-image",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.ImageOutput | AICapability.JsonOutput | AICapability.Reasoning,
                    Verified = false,
                    Rank = 9975,
                    ContextLimit = 131072,
                    Created = new DateTime(2026, 6, 18),
                    Pricing = new AIModelPricing
                    {
                        TextInput = 0.0000005m,
                        TextOutput = 0.000003m,
                        ImageOutput = 0.00006m,
                        WebSearch = 0.014m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemini-3-pro-image",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.ImageOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    Verified = false,
                    Rank = 9970,
                    ContextLimit = 131072,
                    Created = new DateTime(2026, 6, 18),
                    Pricing = new AIModelPricing
                    {
                        TextInput = 0.000002m,
                        TextOutput = 0.000012m,
                        ImageInput = 0.000002m,
                        ImageOutput = 0.00012m,
                        AudioInput = 0.000002m,
                        AudioCacheRead = 0.0000002m,
                        CacheRead = 0.0000002m,
                        CacheWrite = 0.000000375m,
                        InternalReasoning = 0.000012m,
                        WebSearch = 0.014m,
                    },
                },



                // Released between March 2026 and June 2026

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "lyria-3-pro-preview",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.AudioOutput | AICapability.JsonOutput,
                    Verified = false,
                    Rank = 9965,
                    ContextLimit = 1048576,
                    Created = new DateTime(2026, 3, 30),
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "lyria-3-clip-preview",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.AudioOutput | AICapability.JsonOutput,
                    Verified = false,
                    Rank = 9960,
                    ContextLimit = 1048576,
                    Created = new DateTime(2026, 3, 30),
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemma-4-26b-a4b-it",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    Verified = false,
                    Rank = 9955,
                    ContextLimit = 262144,
                    Created = new DateTime(2026, 4, 3),
                    Pricing = new AIModelPricing
                    {
                        TextInput = 0.00000007m,
                        TextOutput = 0.00000034m,
                        CacheRead = 0.00000005m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemma-4-31b-it",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsBatch = true,
                    Verified = false,
                    Rank = 9950,
                    ContextLimit = 262144,
                    Created = new DateTime(2026, 4, 2),
                    Pricing = new AIModelPricing
                    {
                        TextInput = 0.00000009m,
                        TextOutput = 0.00000034m,
                        CacheRead = 0.00000005m,
                    },
                    BatchPricing = new AIModelPricing
                    {
                        TextInput = 0.00000039m,
                        TextOutput = 0.00000097m,
                    },
                    Aliases = new List<string> { "gemma-4-31b-it:batch" },
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemini-3.1-flash-lite",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.SpeechInput | AICapability.AudioInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsBatch = true,
                    Verified = false,
                    Rank = 9945,
                    ContextLimit = 1048576,
                    Created = new DateTime(2026, 5, 7),
                    Pricing = new AIModelPricing
                    {
                        TextInput = 0.00000025m,
                        TextOutput = 0.0000015m,
                        ImageInput = 0.00000025m,
                        AudioInput = 0.0000005m,
                        AudioCacheRead = 0.00000005m,
                        CacheRead = 0.000000025m,
                        CacheWrite = 0.0000000833333333333333m,
                        InternalReasoning = 0.0000015m,
                        WebSearch = 0.014m,
                    },
                    BatchPricing = new AIModelPricing
                    {
                        TextInput = 0.000000125m,
                        TextOutput = 0.00000075m,
                        ImageInput = 0.000000125m,
                        AudioInput = 0.00000025m,
                        AudioCacheRead = 0.000000025m,
                        CacheRead = 0.0000000125m,
                        InternalReasoning = 0.00000075m,
                        WebSearch = 0.014m,
                    },
                    Aliases = new List<string> { "gemini-3.1-flash-lite:batch" },
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemini-3.5-flash",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.SpeechInput | AICapability.AudioInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsBatch = true,
                    Verified = false,
                    Rank = 9940,
                    ContextLimit = 1048576,
                    Created = new DateTime(2026, 5, 19),
                    Pricing = new AIModelPricing
                    {
                        TextInput = 0.0000015m,
                        TextOutput = 0.000009m,
                        ImageInput = 0.0000015m,
                        AudioInput = 0.000003m,
                        AudioCacheRead = 0.0000003m,
                        CacheRead = 0.00000015m,
                        CacheWrite = 0.0000000833333333333333m,
                        InternalReasoning = 0.000009m,
                        WebSearch = 0.014m,
                    },
                    BatchPricing = new AIModelPricing
                    {
                        TextInput = 0.00000075m,
                        TextOutput = 0.0000045m,
                        ImageInput = 0.00000075m,
                        AudioInput = 0.0000015m,
                        AudioCacheRead = 0.00000015m,
                        CacheRead = 0.000000075m,
                        InternalReasoning = 0.0000045m,
                        WebSearch = 0.014m,
                    },
                    Aliases = new List<string> { "gemini-3.5-flash:batch" },
                },



                // Released between December 2025 and March 2026

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemini-3.1-flash-lite-preview",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.AudioInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    Verified = false,
                    Rank = 9935,
                    ContextLimit = 1048576,
                    Created = new DateTime(2026, 3, 3),
                    Pricing = new AIModelPricing
                    {
                        TextInput = 0.00000025m,
                        TextOutput = 0.0000015m,
                        ImageInput = 0.00000025m,
                        AudioInput = 0.0000005m,
                        AudioCacheRead = 0.00000005m,
                        CacheRead = 0.000000025m,
                        CacheWrite = 0.0000000833333333333333m,
                        InternalReasoning = 0.0000015m,
                        WebSearch = 0.014m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemini-3-flash-preview",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.SpeechInput | AICapability.AudioInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsBatch = true,
                    Verified = false,
                    Rank = 9930,
                    ContextLimit = 1048576,
                    Created = new DateTime(2025, 12, 17),
                    Pricing = new AIModelPricing
                    {
                        TextInput = 0.0000005m,
                        TextOutput = 0.000003m,
                        ImageInput = 0.0000005m,
                        AudioInput = 0.000001m,
                        AudioCacheRead = 0.0000001m,
                        CacheRead = 0.00000005m,
                        CacheWrite = 0.0000000833333333333333m,
                        InternalReasoning = 0.000003m,
                        WebSearch = 0.014m,
                    },
                    BatchPricing = new AIModelPricing
                    {
                        TextInput = 0.00000025m,
                        TextOutput = 0.0000015m,
                        ImageInput = 0.00000025m,
                        AudioInput = 0.0000005m,
                        InternalReasoning = 0.0000015m,
                        WebSearch = 0.014m,
                    },
                    Aliases = new List<string> { "gemini-3-flash-preview:batch" },
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemini-3.1-pro-preview-customtools",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.AudioInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    Verified = false,
                    Rank = 9925,
                    ContextLimit = 1048576,
                    Created = new DateTime(2026, 2, 25),
                    Pricing = new AIModelPricing
                    {
                        TextInput = 0.000002m,
                        TextOutput = 0.000012m,
                        ImageInput = 0.000002m,
                        AudioInput = 0.000002m,
                        AudioCacheRead = 0.0000002m,
                        CacheRead = 0.0000002m,
                        CacheWrite = 0.000000375m,
                        InternalReasoning = 0.000012m,
                        WebSearch = 0.014m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemini-3.1-pro-preview",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.SpeechInput | AICapability.AudioInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    SupportsBatch = true,
                    Verified = false,
                    Rank = 9920,
                    ContextLimit = 1048576,
                    Created = new DateTime(2026, 2, 19),
                    Pricing = new AIModelPricing
                    {
                        TextInput = 0.000002m,
                        TextOutput = 0.000012m,
                        ImageInput = 0.000002m,
                        AudioInput = 0.000002m,
                        AudioCacheRead = 0.0000002m,
                        CacheRead = 0.0000002m,
                        CacheWrite = 0.000000375m,
                        InternalReasoning = 0.000012m,
                        WebSearch = 0.014m,
                    },
                    BatchPricing = new AIModelPricing
                    {
                        TextInput = 0.000001m,
                        TextOutput = 0.000006m,
                        ImageInput = 0.000001m,
                        AudioInput = 0.000001m,
                        InternalReasoning = 0.000006m,
                        WebSearch = 0.014m,
                    },
                    Aliases = new List<string> { "gemini-3.1-pro-preview:batch" },
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemini-3.1-flash-image-preview",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.ImageOutput | AICapability.JsonOutput | AICapability.Reasoning,
                    Default = AICapability.Text2Image,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9915,
                    ContextLimit = 65536,
                    Created = new DateTime(2026, 2, 26),
                    Pricing = new AIModelPricing
                    {
                        TextInput = 0.0000005m,
                        TextOutput = 0.000003m,
                        ImageOutput = 0.00006m,
                        WebSearch = 0.014m,
                    },
                },



                // Released between September 2025 and December 2025

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemini-2.5-flash-image",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.ImageOutput | AICapability.JsonOutput,
                    Default = AICapability.Text2Image,
                    SupportsStreaming = true,
                    Verified = true,
                    Rank = 9910,
                    ContextLimit = 32768,
                    Created = new DateTime(2025, 10, 7),
                    Pricing = new AIModelPricing
                    {
                        TextInput = 0.0000003m,
                        TextOutput = 0.0000025m,
                        ImageInput = 0.0000003m,
                        ImageOutput = 0.00003m,
                        AudioInput = 0.000001m,
                        AudioCacheRead = 0.0000001m,
                        CacheRead = 0.00000003m,
                        CacheWrite = 0.0000000833333333333333m,
                        InternalReasoning = 0.0000025m,
                        WebSearch = 0.014m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemini-3-pro-image-preview",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.ImageOutput | AICapability.JsonOutput | AICapability.Reasoning,
                    Default = AICapability.Text2Image | AICapability.Image2Image,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9905,
                    ContextLimit = 65536,
                    Created = new DateTime(2025, 11, 20),
                    Pricing = new AIModelPricing
                    {
                        TextInput = 0.000002m,
                        TextOutput = 0.000012m,
                        ImageInput = 0.000002m,
                        ImageOutput = 0.00012m,
                        AudioInput = 0.000002m,
                        AudioCacheRead = 0.0000002m,
                        CacheRead = 0.0000002m,
                        CacheWrite = 0.000000375m,
                        InternalReasoning = 0.000012m,
                        WebSearch = 0.014m,
                    },
                },



                // Released between June 2025 and September 2025

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemini-2.5-flash-lite",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.SpeechInput | AICapability.AudioInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    SupportsBatch = true,
                    Verified = true,
                    Rank = 9900,
                    ContextLimit = 1048576,
                    Created = new DateTime(2025, 7, 22),
                    Pricing = new AIModelPricing
                    {
                        TextInput = 0.0000001m,
                        TextOutput = 0.0000004m,
                        ImageInput = 0.0000001m,
                        AudioInput = 0.0000003m,
                        AudioCacheRead = 0.00000003m,
                        CacheRead = 0.00000001m,
                        CacheWrite = 0.0000000833333333333333m,
                        InternalReasoning = 0.0000004m,
                        WebSearch = 0.014m,
                    },
                    BatchPricing = new AIModelPricing
                    {
                        TextInput = 0.00000005m,
                        TextOutput = 0.0000002m,
                        ImageInput = 0.00000005m,
                        AudioInput = 0.00000015m,
                        AudioCacheRead = 0.00000003m,
                        CacheRead = 0.00000001m,
                        InternalReasoning = 0.0000002m,
                        WebSearch = 0.014m,
                    },
                    Aliases = new List<string> { "gemini-2.5-flash-lite:batch" },
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemini-2.5-flash",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.SpeechInput | AICapability.AudioInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    Default = AICapability.Text2Text | AICapability.Text2Json | AICapability.ReasoningChat | AICapability.ToolReasoningChat,
                    SupportsStreaming = true,
                    SupportsBatch = true,
                    Verified = true,
                    Rank = 9895,
                    ContextLimit = 1048576,
                    Created = new DateTime(2025, 6, 17),
                    Pricing = new AIModelPricing
                    {
                        TextInput = 0.0000003m,
                        TextOutput = 0.0000025m,
                        ImageInput = 0.0000003m,
                        AudioInput = 0.000001m,
                        AudioCacheRead = 0.0000001m,
                        CacheRead = 0.00000003m,
                        CacheWrite = 0.0000000833333333333333m,
                        InternalReasoning = 0.0000025m,
                        WebSearch = 0.014m,
                    },
                    BatchPricing = new AIModelPricing
                    {
                        TextInput = 0.00000015m,
                        TextOutput = 0.00000125m,
                        ImageInput = 0.00000015m,
                        AudioInput = 0.0000005m,
                        AudioCacheRead = 0.0000001m,
                        CacheRead = 0.00000003m,
                        InternalReasoning = 0.00000125m,
                        WebSearch = 0.014m,
                    },
                    Aliases = new List<string> { "gemini-2.5-flash:batch" },
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemini-2.5-pro",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.SpeechInput | AICapability.AudioInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    SupportsBatch = true,
                    Verified = true,
                    Rank = 9890,
                    ContextLimit = 1048576,
                    Created = new DateTime(2025, 6, 17),
                    Pricing = new AIModelPricing
                    {
                        TextInput = 0.00000125m,
                        TextOutput = 0.00001m,
                        ImageInput = 0.00000125m,
                        AudioInput = 0.00000125m,
                        AudioCacheRead = 0.000000125m,
                        CacheRead = 0.000000125m,
                        CacheWrite = 0.000000375m,
                        InternalReasoning = 0.00001m,
                        WebSearch = 0.014m,
                    },
                    BatchPricing = new AIModelPricing
                    {
                        TextInput = 0.000000625m,
                        TextOutput = 0.000005m,
                        ImageInput = 0.000000625m,
                        AudioInput = 0.000000625m,
                        AudioCacheRead = 0.000000125m,
                        CacheRead = 0.000000125m,
                        InternalReasoning = 0.000005m,
                        WebSearch = 0.014m,
                    },
                    Aliases = new List<string> { "gemini-2.5-pro:batch" },
                },



                // Released between March 2025 and June 2025

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemma-3-4b-it",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput,
                    Verified = false,
                    Rank = 9885,
                    ContextLimit = 131072,
                    Created = new DateTime(2025, 3, 13),
                    Pricing = new AIModelPricing
                    {
                        TextInput = 0.00000005m,
                        TextOutput = 0.0000001m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemma-3-12b-it",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    Verified = false,
                    Rank = 9880,
                    ContextLimit = 131072,
                    Created = new DateTime(2025, 3, 13),
                    Pricing = new AIModelPricing
                    {
                        TextInput = 0.00000005m,
                        TextOutput = 0.00000015m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemma-3-27b-it",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    Verified = false,
                    Rank = 9875,
                    ContextLimit = 131072,
                    Created = new DateTime(2025, 3, 12),
                    Pricing = new AIModelPricing
                    {
                        TextInput = 0.00000008m,
                        TextOutput = 0.00000045m,
                        CacheRead = 0.00000004m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemini-2.5-pro-preview",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.AudioInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    Verified = false,
                    Rank = 9870,
                    ContextLimit = 1048576,
                    Created = new DateTime(2025, 6, 5),
                    Pricing = new AIModelPricing
                    {
                        TextInput = 0.00000125m,
                        TextOutput = 0.00001m,
                        ImageInput = 0.00000125m,
                        AudioInput = 0.00000125m,
                        AudioCacheRead = 0.000000125m,
                        CacheRead = 0.000000125m,
                        CacheWrite = 0.000000375m,
                        InternalReasoning = 0.00001m,
                        WebSearch = 0.014m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemini-2.5-pro-preview-05-06",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.SpeechInput | AICapability.AudioInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    Verified = false,
                    Rank = 9865,
                    ContextLimit = 1048576,
                    Created = new DateTime(2025, 5, 7),
                    Pricing = new AIModelPricing
                    {
                        TextInput = 0.00000125m,
                        TextOutput = 0.00001m,
                        ImageInput = 0.00000125m,
                        AudioInput = 0.00000125m,
                        AudioCacheRead = 0.000000125m,
                        CacheRead = 0.000000125m,
                        CacheWrite = 0.000000375m,
                        InternalReasoning = 0.00001m,
                        WebSearch = 0.014m,
                    },
                },



                // Released before September 2024 or unknown release date

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemma-2-27b-it",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput,
                    Verified = false,
                    Rank = 9860,
                    ContextLimit = 8192,
                    Created = new DateTime(2024, 7, 13),
                    Pricing = new AIModelPricing
                    {
                        TextInput = 0.00000065m,
                        TextOutput = 0.00000065m,
                    },
                },



                // Deprecated models

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemini-1.5-flash",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.ImageInput | AICapability.SpeechInput | AICapability.AudioInput,
                    SupportsStreaming = true,
                    Verified = true,
                    Deprecated = true,
                    Rank = 0,
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
                    Rank = -5,
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
                    Rank = -10,
                    ContextLimit = 1000000,
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemini-2.0-flash-001",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.SpeechInput | AICapability.AudioInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    Verified = false,
                    Deprecated = true,
                    Rank = -15,
                    ContextLimit = 1000000,
                    Pricing = new AIModelPricing
                    {
                        TextInput = 0.0000001m,
                        TextOutput = 0.0000004m,
                        ImageInput = 0.0000001m,
                        AudioInput = 0.0000007m,
                        CacheRead = 0.000000025m,
                        CacheWrite = 0.00000008333333333333334m,
                        InternalReasoning = 0.0000004m,
                        WebSearch = 0.014m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemini-2.0-flash-lite-001",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.SpeechInput | AICapability.AudioInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    Verified = false,
                    Deprecated = true,
                    Rank = -20,
                    ContextLimit = 1048576,
                    Pricing = new AIModelPricing
                    {
                        TextInput = 0.000000075m,
                        TextOutput = 0.0000003m,
                        ImageInput = 0.000000075m,
                        AudioInput = 0.000000075m,
                        InternalReasoning = 0.0000003m,
                        WebSearch = 0.014m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemini-2.5-flash-lite-preview-09-2025",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.SpeechInput | AICapability.AudioInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    Verified = false,
                    Deprecated = true,
                    Rank = -25,
                    ContextLimit = 1048576,
                    Pricing = new AIModelPricing
                    {
                        TextInput = 0.0000001m,
                        TextOutput = 0.0000004m,
                        ImageInput = 0.0000001m,
                        AudioInput = 0.0000003m,
                        CacheRead = 0.00000001m,
                        CacheWrite = 0.00000008333333333333334m,
                        InternalReasoning = 0.0000004m,
                        WebSearch = 0.014m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemini-2.5-flash-preview-tts",
                    Capabilities = AICapability.TextInput | AICapability.SpeechOutput,
                    Default = AICapability.Text2Speech,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -30,
                    ContextLimit = 32000,
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemini-2.5-pro-preview-tts",
                    Capabilities = AICapability.TextInput | AICapability.SpeechOutput,
                    Default = AICapability.Text2Speech,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -35,
                    ContextLimit = 32000,
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemini-3.1-flash-preview",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.ImageInput | AICapability.Reasoning | AICapability.SpeechInput | AICapability.AudioInput,
                    Default = AICapability.Text2Text | AICapability.Text2Json | AICapability.ReasoningChat | AICapability.ToolReasoningChat,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -40,
                    ContextLimit = 1000000,
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemma-3-12b-it:free",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput,
                    Verified = false,
                    Deprecated = true,
                    Rank = -45,
                    ContextLimit = 32768,
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemma-3-27b-it:free",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput,
                    Verified = false,
                    Deprecated = true,
                    Rank = -50,
                    ContextLimit = 131072,
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemma-3-4b-it:free",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput,
                    Verified = false,
                    Deprecated = true,
                    Rank = -55,
                    ContextLimit = 32768,
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemma-3n-e2b-it",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput,
                    Verified = false,
                    Deprecated = true,
                    Rank = -60,
                    ContextLimit = 8192,
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemma-3n-e2b-it:free",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput,
                    Verified = false,
                    Deprecated = true,
                    Rank = -65,
                    ContextLimit = 8192,
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemma-3n-e4b-it",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput,
                    Verified = false,
                    Deprecated = true,
                    Rank = -70,
                    ContextLimit = 32768,
                    Pricing = new AIModelPricing
                    {
                        TextInput = 0.00000006m,
                        TextOutput = 0.00000012m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemma-3n-e4b-it:free",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput,
                    Verified = false,
                    Deprecated = true,
                    Rank = -75,
                    ContextLimit = 8192,
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemma-4-26b-a4b-it:free",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    Verified = false,
                    Deprecated = true,
                    Rank = -80,
                    ContextLimit = 262144,
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemma-4-31b-it:free",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    Verified = false,
                    Deprecated = true,
                    Rank = -85,
                    ContextLimit = 262144,
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "lyria-3-clip",
                    Capabilities = AICapability.TextInput | AICapability.AudioOutput,
                    Default = AICapability.Text2Audio,
                    SupportsStreaming = false,
                    Verified = false,
                    Deprecated = true,
                    Rank = -90,
                    ContextLimit = 32000,
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "lyria-3-pro",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.AudioOutput,
                    Default = AICapability.Text2Audio,
                    SupportsStreaming = false,
                    Verified = false,
                    Deprecated = true,
                    Rank = -95,
                    ContextLimit = 32000,
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
