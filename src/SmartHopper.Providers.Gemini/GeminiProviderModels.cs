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
                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemini-3.1-pro-preview",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.ImageInput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 75,
                    ContextLimit = 1000000,
                },
                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemini-3.1-flash-preview",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.ImageInput | AICapability.Reasoning,
                    Default = AICapability.Text2Text | AICapability.Text2Json | AICapability.ReasoningChat | AICapability.ToolReasoningChat,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 80,
                    ContextLimit = 1000000,
                },
                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemini-3-pro-image-preview",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.ImageOutput | AICapability.Reasoning,
                    Default = AICapability.Text2Image | AICapability.Image2Image,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 70,
                    ContextLimit = 100000,
                },
                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemini-3.1-flash-image-preview",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.ImageOutput,
                    Default = AICapability.Text2Image,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 75,
                    ContextLimit = 100000,
                },
                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemini-2.5-pro",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.ImageInput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = true,
                    Rank = 85,
                    ContextLimit = 1000000,
                },
                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemini-2.5-flash",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.ImageInput | AICapability.Reasoning,
                    Default = AICapability.Text2Text | AICapability.Text2Json | AICapability.ReasoningChat | AICapability.ToolReasoningChat,
                    SupportsStreaming = true,
                    Verified = true,
                    Rank = 90,
                    ContextLimit = 1000000,
                },
                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemini-2.5-flash-image",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.ImageOutput,
                    Default = AICapability.Text2Image,
                    SupportsStreaming = true,
                    Verified = true,
                    Rank = 85,
                    ContextLimit = 100000,
                },
                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemini-2.5-flash-lite",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.ImageInput,
                    SupportsStreaming = true,
                    Verified = true,
                    Rank = 95,
                    ContextLimit = 100000,
                },
                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemini-2.0-flash",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.ImageInput,
                    SupportsStreaming = true,
                    Verified = true,
                    Rank = 80,
                    ContextLimit = 1000000,
                },
                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemini-1.5-pro",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.ImageInput,
                    SupportsStreaming = true,
                    Verified = true,
                    Deprecated = true,
                    Rank = 50,
                    ContextLimit = 1000000,
                },
                new AIModelCapabilities
                {
                    Provider = providerName,
                    Model = "gemini-1.5-flash",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.ImageInput,
                    SupportsStreaming = true,
                    Verified = true,
                    Deprecated = true,
                    Rank = 55,
                    ContextLimit = 1000000,
                },
            };

            return Task.FromResult(models);
        }

        /// <inheritdoc/>
        public override async Task<List<AIModelCapabilities>> RetrieveApiModels()
        {
            try
            {
                var request = new AIRequestCall
                {
                    Endpoint = "/models",
                    HttpMethod = "GET",
                    Authentication = "x-goog-api-key",
                };

                var response = await this.provider.CallApiAsync(request);

                if (!response.IsSuccess)
                {
                    return await this.RetrieveModels();
                }

                var jObject = JObject.Parse(response.Body);
                var modelsArray = jObject["models"] as JArray;

                if (modelsArray == null)
                {
                    return await this.RetrieveModels();
                }

                var providerName = this.provider.Name.ToLowerInvariant();
                var models = new List<AIModelCapabilities>();

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

                    var staticModel = (await this.RetrieveModels()).FirstOrDefault(m => m.Model == name);
                    if (staticModel != null)
                    {
                        models.Add(staticModel);
                    }
                }

                return models.Count > 0 ? models : await this.RetrieveModels();
            }
            catch
            {
                return await this.RetrieveModels();
            }
        }
    }
}
