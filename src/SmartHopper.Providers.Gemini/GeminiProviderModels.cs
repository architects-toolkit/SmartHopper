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
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Requests;
using SmartHopper.Infrastructure.AICall.Core.Returns;
using SmartHopper.Infrastructure.AIModels;
using SmartHopper.Infrastructure.AIProviders;

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
                // Released between February 2026 and May 2026

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "lyria-3-clip-preview",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.AudioOutput | AICapability.JsonOutput,
                    Verified = false,
                    Rank = 10000,
                    ContextLimit = 1048576,
                    Created = new DateTime(2026, 3, 30),
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "lyria-3-pro-preview",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.AudioOutput | AICapability.JsonOutput,
                    Verified = false,
                    Rank = 9995,
                    ContextLimit = 1048576,
                    Created = new DateTime(2026, 3, 30),
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemma-4-26b-a4b-it",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    Verified = false,
                    Rank = 9990,
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
                    Provider = providerName,
                    Model = "gemma-4-31b-it",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    Verified = false,
                    Rank = 9985,
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
                    Provider = providerName,
                    Model = "gemini-3.1-flash-lite-preview",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.AudioInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    Verified = false,
                    Rank = 9980,
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
                    Provider = providerName,
                    Model = "gemini-3.1-flash-image-preview",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.ImageOutput | AICapability.JsonOutput | AICapability.Reasoning,
                    Default = AICapability.Text2Image,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9975,
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
                    Provider = providerName,
                    Model = "gemini-3.1-pro-preview",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.AudioInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9970,
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
                    Provider = providerName,
                    Model = "gemini-3.1-pro-preview-customtools",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.AudioInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    Verified = false,
                    Rank = 9965,
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



                // Released between November 2025 and February 2026

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemini-3-flash-preview",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.AudioInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    Verified = false,
                    Rank = 9960,
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
                    Provider = providerName,
                    Model = "gemini-3-pro-image-preview",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.ImageOutput | AICapability.JsonOutput | AICapability.Reasoning,
                    Default = AICapability.Text2Image | AICapability.Image2Image,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9955,
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



                // Released between August 2025 and November 2025

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemini-2.5-flash-image",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.ImageOutput | AICapability.JsonOutput,
                    Default = AICapability.Text2Image,
                    SupportsStreaming = true,
                    Verified = true,
                    Rank = 9950,
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
                    Provider = providerName,
                    Model = "gemini-2.5-flash-lite-preview-09-2025",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.AudioInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    Verified = false,
                    Rank = 9945,
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



                // Released between May 2025 and August 2025

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemini-2.5-flash-lite",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.AudioInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = true,
                    Rank = 9940,
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
                    Provider = providerName,
                    Model = "gemini-2.5-flash",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.AudioInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    Default = AICapability.Text2Text | AICapability.Text2Json | AICapability.ReasoningChat | AICapability.ToolReasoningChat,
                    SupportsStreaming = true,
                    Verified = true,
                    Rank = 9935,
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
                    Provider = providerName,
                    Model = "gemini-2.5-pro",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.AudioInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = true,
                    Rank = 9930,
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
                    Provider = providerName,
                    Model = "gemma-3n-e2b-it",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput,
                    Verified = false,
                    Rank = 9925,
                    ContextLimit = 8192,
                    Created = new DateTime(2025, 7, 9),
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemma-3n-e4b-it",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput,
                    Verified = false,
                    Rank = 9920,
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
                    Provider = providerName,
                    Model = "gemini-2.5-pro-preview",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.AudioInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    Verified = false,
                    Rank = 9915,
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
                    Provider = providerName,
                    Model = "gemini-2.5-pro-preview-05-06",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.AudioInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    Verified = false,
                    Rank = 9910,
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



                // Released between February 2025 and May 2025

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemma-3-4b-it",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput,
                    Verified = false,
                    Rank = 9905,
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
                    Provider = providerName,
                    Model = "gemma-3-12b-it",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    Verified = false,
                    Rank = 9900,
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
                    Provider = providerName,
                    Model = "gemma-3-27b-it",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    Verified = false,
                    Rank = 9895,
                    ContextLimit = 131072,
                    Created = new DateTime(2025, 3, 12),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000008m,
                        Completion = 0.00000016m,
                    },
                },



                // Released between May 2024 and August 2024

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemma-2-27b-it",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput,
                    Verified = false,
                    Rank = 9890,
                    ContextLimit = 8192,
                    Created = new DateTime(2024, 7, 13),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000065m,
                        Completion = 0.00000065m,
                    },
                },



                // Deprecated models

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemini-2.0-flash-lite-001",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.AudioInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    Verified = false,
                    Deprecated = true,
                    Rank = 0,
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
                    Provider = providerName,
                    Model = "gemini-2.0-flash-001",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.AudioInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    Verified = false,
                    Deprecated = true,
                    Rank = -5,
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
                    Provider = providerName,
                    Model = "gemini-1.5-flash",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.ImageInput | AICapability.AudioInput,
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
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.ImageInput | AICapability.AudioInput,
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
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.ImageInput | AICapability.AudioInput,
                    SupportsStreaming = true,
                    Verified = true,
                    Deprecated = true,
                    Rank = -20,
                    ContextLimit = 1000000,
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
                    Rank = -25,
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
                    Rank = -30,
                    ContextLimit = 32000,
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemini-3.1-flash-preview",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.ImageInput | AICapability.Reasoning | AICapability.AudioInput,
                    Default = AICapability.Text2Text | AICapability.Text2Json | AICapability.ReasoningChat | AICapability.ToolReasoningChat,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -35,
                    ContextLimit = 1000000,
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemma-3-12b-it:free",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput,
                    Verified = false,
                    Deprecated = true,
                    Rank = -40,
                    ContextLimit = 32768,
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemma-3-27b-it:free",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput,
                    Verified = false,
                    Deprecated = true,
                    Rank = -45,
                    ContextLimit = 131072,
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemma-3-4b-it:free",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput,
                    Verified = false,
                    Deprecated = true,
                    Rank = -50,
                    ContextLimit = 32768,
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemma-3n-e2b-it:free",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput,
                    Verified = false,
                    Deprecated = true,
                    Rank = -55,
                    ContextLimit = 8192,
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemma-3n-e4b-it:free",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput,
                    Verified = false,
                    Deprecated = true,
                    Rank = -60,
                    ContextLimit = 8192,
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemma-4-26b-a4b-it:free",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    Verified = false,
                    Deprecated = true,
                    Rank = -65,
                    ContextLimit = 262144,
                },

                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemma-4-31b-it:free",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.VideoInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    Verified = false,
                    Deprecated = true,
                    Rank = -70,
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
                    Rank = -75,
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
                    Rank = -80,
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
