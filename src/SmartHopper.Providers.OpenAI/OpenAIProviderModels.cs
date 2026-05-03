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
                // Released between February 2026 and May 2026

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-5.4-nano-2026-03-17",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 1000,
                    ContextLimit = 400000,
                    Aliases = new List<string> { "gpt-5.4-nano" },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-5.4-mini-2026-03-17",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 995,
                    ContextLimit = 400000,
                    Aliases = new List<string> { "gpt-5.4-mini" },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-5.3-chat-latest",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 990,
                    ContextLimit = 400000,
                    Aliases = new List<string> { "gpt-5.3-chat" },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-5.3-codex",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 985,
                    ContextLimit = 400000,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-5.4-2026-03-05",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 980,
                    ContextLimit = 1050000,
                    Aliases = new List<string> { "gpt-5.4" },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-5.5-2026-04-23",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 975,
                    ContextLimit = 1050000,
                    Aliases = new List<string> { "gpt-5.5" },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-5.4-pro-2026-03-05",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 970,
                    ContextLimit = 1050000,
                    Aliases = new List<string> { "gpt-5.4-pro" },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-5.5-pro-2026-04-23",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 965,
                    ContextLimit = 1050000,
                    Aliases = new List<string> { "gpt-5.5-pro" },
                },



                // Released between November 2025 and February 2026

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-5.1-codex-mini",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 960,
                    ContextLimit = 400000,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-audio-mini-2025-12-15",
                    Capabilities = AICapability.TextInput | AICapability.AudioInput | AICapability.TextOutput | AICapability.AudioOutput | AICapability.FunctionCalling,
                    SupportsStreaming = false,
                    Verified = false,
                    Rank = 955,
                    ContextLimit = 128000,
                    Aliases = new List<string> { "gpt-audio-mini" },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-5.1-2025-11-13",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 950,
                    ContextLimit = 400000,
                    Aliases = new List<string> { "gpt-5.1" },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-5.1-chat-latest",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 945,
                    ContextLimit = 128000,
                    Aliases = new List<string> { "gpt-5.1-chat" },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-5.1-codex",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 940,
                    ContextLimit = 400000,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-5.1-codex-max",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 935,
                    ContextLimit = 400000,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-5.2-2025-12-11",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 930,
                    ContextLimit = 400000,
                    Aliases = new List<string> { "gpt-5.2" },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-5.2-chat-latest",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 925,
                    ContextLimit = 128000,
                    Aliases = new List<string> { "gpt-5.2-chat" },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-5.2-codex",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 920,
                    ContextLimit = 400000,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-audio-2025-08-28",
                    Capabilities = AICapability.TextInput | AICapability.AudioInput | AICapability.TextOutput | AICapability.AudioOutput | AICapability.FunctionCalling,
                    SupportsStreaming = false,
                    Verified = false,
                    Rank = 915,
                    ContextLimit = 128000,
                    Aliases = new List<string> { "gpt-audio" },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-5.2-pro-2025-12-11",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 910,
                    ContextLimit = 400000,
                    Aliases = new List<string> { "gpt-5.2-pro" },
                },



                // Released between August 2025 and November 2025

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-5-nano-2025-08-07",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 905,
                    ContextLimit = 400000,
                    Aliases = new List<string> { "gpt-5-nano" },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-5-mini-2025-08-07",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = true,
                    Rank = 900,
                    ContextLimit = 400000,
                    Aliases = new List<string> { "gpt-5-mini" },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "o4-mini-deep-research-2025-06-26",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 895,
                    ContextLimit = 200000,
                    Aliases = new List<string> { "o4-mini-deep-research" },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-5-2025-08-07",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 890,
                    ContextLimit = 400000,
                    Aliases = new List<string> { "gpt-5" },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-5-chat-latest",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.ImageInput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 885,
                    ContextLimit = 128000,
                    Aliases = new List<string> { "gpt-5-chat" },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-5-codex",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 880,
                    ContextLimit = 400000,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "o3-deep-research-2025-06-26",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 875,
                    ContextLimit = 200000,
                    Aliases = new List<string> { "o3-deep-research" },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-4o-audio-preview-2025-06-03",
                    Capabilities = AICapability.TextInput | AICapability.AudioInput | AICapability.TextOutput | AICapability.AudioOutput | AICapability.FunctionCalling,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 870,
                    ContextLimit = 128000,
                    Aliases = new List<string> { "gpt-4o-audio-preview" },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-5-pro-2025-10-06",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 865,
                    ContextLimit = 400000,
                    Aliases = new List<string> { "gpt-5-pro" },
                },



                // Released between May 2025 and August 2025

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "o3-pro-2025-06-10",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 860,
                    ContextLimit = 200000,
                    Aliases = new List<string> { "o3-pro" },
                },



                // Released between February 2025 and May 2025

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-4.1-nano-2025-04-14",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 855,
                    ContextLimit = 1047576,
                    Aliases = new List<string> { "gpt-4.1-nano" },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-4o-mini-search-preview-2025-03-11",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 850,
                    ContextLimit = 128000,
                    Aliases = new List<string> { "gpt-4o-mini-search-preview" },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-4.1-mini-2025-04-14",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 845,
                    ContextLimit = 1047576,
                    Aliases = new List<string> { "gpt-4.1-mini" },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "o4-mini-2025-04-16",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 840,
                    ContextLimit = 200000,
                    Aliases = new List<string> { "o4-mini" },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-4.1-2025-04-14",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 835,
                    ContextLimit = 1047576,
                    Aliases = new List<string> { "gpt-4.1" },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "o3-2025-04-16",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 830,
                    ContextLimit = 200000,
                    Aliases = new List<string> { "o3" },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-4o-search-preview-2025-03-11",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 825,
                    ContextLimit = 128000,
                    Aliases = new List<string> { "gpt-4o-search-preview" },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "o1-pro-2025-03-19",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 820,
                    ContextLimit = 200000,
                    Aliases = new List<string> { "o1-pro" },
                },



                // Released between November 2024 and February 2025

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "o3-mini-2025-01-31",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 815,
                    ContextLimit = 200000,
                    Aliases = new List<string> { "o3-mini" },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "o1-2024-12-17",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 810,
                    ContextLimit = 200000,
                    Aliases = new List<string> { "o1" },
                },



                // Released between August 2024 and November 2024

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-4o-2024-08-06",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 805,
                    ContextLimit = 128000,
                },



                // Released between May 2024 and August 2024

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-4o-mini-2024-07-18",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.ImageInput | AICapability.JsonOutput | AICapability.FunctionCalling,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 800,
                    ContextLimit = 128000,
                    Aliases = new List<string> { "gpt-4o-mini" },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-4o-2024-05-13",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 795,
                    ContextLimit = 128000,
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
                    Rank = 790,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-3.5-turbo-instruct",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 785,
                    ContextLimit = 4095,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-3.5-turbo-16k",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 780,
                    ContextLimit = 16385,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "chatgpt-image-latest",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 775,
                    ContextLimit = 128000,
                    Aliases = new List<string> { "chatgpt-image" },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-4o-audio-preview-2024-12-17",
                    Capabilities = AICapability.TextInput | AICapability.AudioInput | AICapability.TextOutput | AICapability.AudioOutput | AICapability.FunctionCalling,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 770,
                    ContextLimit = 128000,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-4o-mini-audio-preview-2024-12-17",
                    Capabilities = AICapability.TextInput | AICapability.AudioInput | AICapability.TextOutput | AICapability.AudioOutput | AICapability.FunctionCalling,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 765,
                    ContextLimit = 128000,
                    Aliases = new List<string> { "gpt-4o-mini-audio-preview" },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-4o-mini-realtime-preview-2024-12-17",
                    Capabilities = AICapability.TextInput | AICapability.AudioInput | AICapability.TextOutput | AICapability.AudioOutput | AICapability.FunctionCalling,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 760,
                    ContextLimit = 128000,
                    Aliases = new List<string> { "gpt-4o-mini-realtime-preview" },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-4o-mini-transcribe-2025-03-20",
                    Capabilities = AICapability.AudioInput | AICapability.TextOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 755,
                    ContextLimit = 16000,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-4o-mini-transcribe-2025-12-15",
                    Capabilities = AICapability.AudioInput | AICapability.TextOutput,
                    SupportsStreaming = false,
                    Verified = false,
                    Rank = 750,
                    ContextLimit = 16000,
                    Aliases = new List<string> { "gpt-4o-mini-transcribe" },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-4o-mini-tts-2025-03-20",
                    Capabilities = AICapability.TextInput | AICapability.AudioOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 745,
                    ContextLimit = 2000,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-4o-mini-tts-2025-12-15",
                    Capabilities = AICapability.TextInput | AICapability.AudioOutput,
                    SupportsStreaming = false,
                    Verified = false,
                    Rank = 740,
                    ContextLimit = 2000,
                    Aliases = new List<string> { "gpt-4o-mini-tts" },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-4o-realtime-preview-2024-12-17",
                    Capabilities = AICapability.TextInput | AICapability.AudioInput | AICapability.TextOutput | AICapability.AudioOutput | AICapability.FunctionCalling,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 735,
                    ContextLimit = 128000,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-4o-realtime-preview-2025-06-03",
                    Capabilities = AICapability.TextInput | AICapability.AudioInput | AICapability.TextOutput | AICapability.AudioOutput | AICapability.FunctionCalling,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 730,
                    ContextLimit = 128000,
                    Aliases = new List<string> { "gpt-4o-realtime-preview" },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-4o-transcribe",
                    Capabilities = AICapability.AudioInput | AICapability.TextOutput,
                    SupportsStreaming = false,
                    Verified = false,
                    Rank = 725,
                    ContextLimit = 16000,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-4o-transcribe-diarize",
                    Capabilities = AICapability.AudioInput | AICapability.TextOutput,
                    SupportsStreaming = false,
                    Verified = false,
                    Rank = 720,
                    ContextLimit = 16000,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-5-search-api-2025-10-14",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 715,
                    ContextLimit = 400000,
                    Aliases = new List<string> { "gpt-5-search-api" },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-audio-1.5",
                    Capabilities = AICapability.TextInput | AICapability.AudioInput | AICapability.TextOutput | AICapability.AudioOutput | AICapability.FunctionCalling,
                    SupportsStreaming = false,
                    Verified = false,
                    Rank = 710,
                    ContextLimit = 128000,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-audio-mini-2025-10-06",
                    Capabilities = AICapability.TextInput | AICapability.AudioInput | AICapability.TextOutput | AICapability.AudioOutput | AICapability.FunctionCalling,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 705,
                    ContextLimit = 128000,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-image-1",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.ImageOutput,
                    SupportsStreaming = false,
                    Verified = false,
                    Rank = 700,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-image-1-mini",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.ImageOutput,
                    Default = AICapability.Text2Image | AICapability.Image2Image,
                    SupportsStreaming = false,
                    Verified = false,
                    Rank = 695,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-image-1.5",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.ImageOutput,
                    SupportsStreaming = false,
                    Verified = false,
                    Rank = 690,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-image-2-2026-04-21",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.ImageOutput,
                    SupportsStreaming = false,
                    Verified = false,
                    Rank = 685,
                    Aliases = new List<string> { "gpt-image-2" },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-realtime-1.5",
                    Capabilities = AICapability.TextInput | AICapability.AudioInput | AICapability.TextOutput | AICapability.AudioOutput | AICapability.FunctionCalling,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 680,
                    ContextLimit = 128000,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-realtime-2025-08-28",
                    Capabilities = AICapability.TextInput | AICapability.AudioInput | AICapability.TextOutput | AICapability.AudioOutput | AICapability.FunctionCalling,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 675,
                    ContextLimit = 128000,
                    Aliases = new List<string> { "gpt-realtime" },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-realtime-mini-2025-10-06",
                    Capabilities = AICapability.TextInput | AICapability.AudioInput | AICapability.TextOutput | AICapability.AudioOutput | AICapability.FunctionCalling,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 670,
                    ContextLimit = 128000,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-realtime-mini-2025-12-15",
                    Capabilities = AICapability.TextInput | AICapability.AudioInput | AICapability.TextOutput | AICapability.AudioOutput | AICapability.FunctionCalling,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 665,
                    ContextLimit = 128000,
                    Aliases = new List<string> { "gpt-realtime-mini" },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "omni-moderation-2024-09-26",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput,
                    SupportsStreaming = false,
                    Verified = false,
                    Rank = 660,
                    Aliases = new List<string> { "omni-moderation-latest", "omni-moderation" },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "sora-2",
                    Capabilities = AICapability.TextInput | AICapability.VideoOutput,
                    SupportsStreaming = false,
                    Verified = false,
                    Rank = 655,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "sora-2-pro",
                    Capabilities = AICapability.TextInput | AICapability.VideoOutput,
                    SupportsStreaming = false,
                    Verified = false,
                    Rank = 650,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "text-embedding-3-large",
                    Capabilities = AICapability.Embedding,
                    SupportsStreaming = false,
                    Verified = false,
                    Rank = 645,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "text-embedding-3-small",
                    Capabilities = AICapability.Embedding,
                    SupportsStreaming = false,
                    Verified = false,
                    Rank = 640,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "tts-1",
                    Capabilities = AICapability.TextInput | AICapability.AudioOutput,
                    SupportsStreaming = false,
                    Verified = false,
                    Rank = 635,
                    ContextLimit = 2000,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "tts-1-1106",
                    Capabilities = AICapability.TextInput | AICapability.AudioOutput,
                    SupportsStreaming = false,
                    Verified = false,
                    Rank = 630,
                    ContextLimit = 2000,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "tts-1-hd",
                    Capabilities = AICapability.TextInput | AICapability.AudioOutput,
                    SupportsStreaming = false,
                    Verified = false,
                    Rank = 625,
                    ContextLimit = 2000,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "tts-1-hd-1106",
                    Capabilities = AICapability.TextInput | AICapability.AudioOutput,
                    SupportsStreaming = false,
                    Verified = false,
                    Rank = 620,
                    ContextLimit = 2000,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "whisper-1",
                    Capabilities = AICapability.AudioInput | AICapability.TextOutput,
                    SupportsStreaming = false,
                    Verified = false,
                    Rank = 615,
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
                    Aliases = new List<string> { "gpt-4o" },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-3.5-turbo",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput,
                    SupportsStreaming = false,
                    Verified = false,
                    Deprecated = true,
                    Rank = -5,
                    ContextLimit = 16385,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-4-turbo-2024-04-09",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -10,
                    ContextLimit = 128000,
                    Aliases = new List<string> { "gpt-4-turbo" },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-4",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -15,
                    ContextLimit = 8192,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "babbage-002",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput,
                    SupportsStreaming = false,
                    Verified = false,
                    Deprecated = true,
                    Rank = -20,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "chatgpt-4o-latest",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.ImageInput,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -25,
                    ContextLimit = 128000,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "codex-mini-latest",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -30,
                    ContextLimit = 200000,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "dall-e-2",
                    Capabilities = AICapability.TextInput | AICapability.ImageOutput,
                    SupportsStreaming = false,
                    Verified = false,
                    Deprecated = true,
                    Rank = -35,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "davinci-002",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -40,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-3.5-turbo-0125",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -45,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-3.5-turbo-1106",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -50,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-3.5-turbo-instruct-0914",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -55,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-4-0613",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -60,
                    ContextLimit = 8192,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "text-embedding-ada-002",
                    Capabilities = AICapability.Embedding,
                    SupportsStreaming = false,
                    Verified = false,
                    Deprecated = true,
                    Rank = -65,
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

