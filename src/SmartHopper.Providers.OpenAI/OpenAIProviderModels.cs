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
                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-5-nano",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    Default = AICapability.Text2Text,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 70,
                },
                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-5-mini",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    Default = AICapability.ToolChat | AICapability.Text2Json | AICapability.ToolReasoningChat,
                    SupportsStreaming = true,
                    Verified = true,
                    Rank = 95,
                },
                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-5",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 70,
                },
                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "codex-mini-latest",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = 60,
                },
                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-5-codex",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 70,
                },
                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-5.1",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 90,
                },
                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-5.1-chat-latest",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 80,
                },
                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-5.1-codex",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 85,
                },
                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-5.1-codex-mini",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 85,
                },
                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-4.1",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 75,
                },
                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-4.1-mini",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 85,
                },
                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-4.1-nano",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 65,
                },
                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-4-turbo",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 40,
                },
                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-4",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = 40,
                },
                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-3.5-turbo",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput,
                    SupportsStreaming = false,
                    Verified = false,
                    Deprecated = true,
                    Rank = 40,
                },
                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "o4-mini",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 70,
                },
                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "o3",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.ImageInput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = 40,
                },
                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "o3-mini",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = 50,
                },

                // ChatGPT
                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-5-chat-latest",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.ImageInput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 40,
                },
                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-4o-mini",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.ImageInput | AICapability.JsonOutput | AICapability.FunctionCalling,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 30,
                },
                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-4o",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.ImageInput | AICapability.JsonOutput | AICapability.FunctionCalling,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 30,
                },
                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "chatgpt-4o-latest",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.ImageInput,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = 30,
                },

                // Image
                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "dall-e-3",
                    Capabilities = AICapability.TextInput | AICapability.ImageOutput,
                    Default = AICapability.Text2Image,
                    SupportsStreaming = false,
                    Verified = true,
                    Rank = 80,
                },
                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "dall-e-2",
                    Capabilities = AICapability.TextInput | AICapability.ImageOutput,
                    SupportsStreaming = false,
                    Verified = false,
                    Deprecated = true,
                    Rank = 60,
                },
                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-image-1-mini",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.ImageOutput,
                    Default = AICapability.Text2Image | AICapability.Image2Image,
                    SupportsStreaming = false,
                    Verified = false,
                    Rank = 70,
                },
                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-image-1",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.ImageOutput,
                    SupportsStreaming = false,
                    Verified = false,
                    Rank = 60,
                },

                // Audio
                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-4o-mini-tts",
                    Capabilities = AICapability.TextInput | AICapability.AudioOutput,
                    SupportsStreaming = false,
                    Verified = false,
                    Rank = 60,
                },
                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-4o-mini-transcribe",
                    Capabilities = AICapability.AudioInput | AICapability.TextOutput,
                    SupportsStreaming = false,
                    Verified = false,
                    Rank = 70,
                },
                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-4o-transcribe",
                    Capabilities = AICapability.AudioInput | AICapability.TextOutput,
                    SupportsStreaming = false,
                    Verified = false,
                    Rank = 60,
                },
                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-audio-mini",
                    Capabilities = AICapability.TextInput | AICapability.AudioInput | AICapability.TextOutput | AICapability.AudioOutput | AICapability.FunctionCalling,
                    SupportsStreaming = false,
                    Verified = false,
                    Rank = 60,
                },
                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-audio",
                    Capabilities = AICapability.TextInput | AICapability.AudioInput | AICapability.TextOutput | AICapability.AudioOutput | AICapability.FunctionCalling,
                    SupportsStreaming = false,
                    Verified = false,
                    Rank = 40,
                },
                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-4o-mini-audio-preview",
                    Capabilities = AICapability.TextInput | AICapability.AudioInput | AICapability.TextOutput | AICapability.AudioOutput | AICapability.FunctionCalling,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = 45,
                },
                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "gpt-4o-audio-preview",
                    Capabilities = AICapability.TextInput | AICapability.AudioInput | AICapability.TextOutput | AICapability.AudioOutput | AICapability.FunctionCalling,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = 40,
                },
                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "whisper-1",
                    Capabilities = AICapability.AudioInput | AICapability.TextOutput,
                    SupportsStreaming = false,
                    Verified = false,
                    Rank = 40,
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
