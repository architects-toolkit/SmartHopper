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

namespace SmartHopper.Providers.MistralAI
{
    /// <summary>
    /// MistralAI provider-specific model management implementation.
    /// </summary>
    public class MistralAIProviderModels : AIProviderModels
    {
        private readonly MistralAIProvider mistralProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="MistralAIProviderModels"/> class.
        /// </summary>
        /// <param name="provider">The MistralAI provider instance.</param>
        public MistralAIProviderModels(MistralAIProvider provider)
            : base(provider)
        {
            this.mistralProvider = provider;
        }

        /// <inheritdoc/>
        public override Task<List<AIModelCapabilities>> RetrieveModels()
        {
            var provider = this.mistralProvider.Name.ToLowerInvariant();

            var models = new List<AIModelCapabilities>
            {
                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "mistral-small-latest",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling,
                    Default = AICapability.Text2Text | AICapability.ToolChat | AICapability.Text2Json,
                    SupportsStreaming = true,
                    Verified = true,
                    Rank = 90,
                    DiscouragedForTools = new List<string> { "script_generate", "script_edit" },
                    Aliases = new List<string> { "mistral-small" },
                },
                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "mistral-medium-latest",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling ,
                    SupportsStreaming = true,
                    Verified = true,
                    Rank = 80,
                    Aliases = new List<string> { "mistral-medium" },
                },
                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "mistral-large-latest",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 60,
                    Aliases = new List<string> { "mistral-large" },
                },
                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "ministral-8b-latest",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling,
                    SupportsStreaming = false,
                    Verified = false,
                    Rank = 50,
                },
                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "ministral-3b-latest",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling,
                    SupportsStreaming = false,
                    Verified = false,
                    Rank = 30,
                },
                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "magistral-small-latest",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    Default = AICapability.ToolReasoningChat,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 85,
                },
                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "magistral-medium-latest",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 75,
                },
                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "voxtral-small-latest",
                    Capabilities = AICapability.AudioInput | AICapability.TextOutput,
                    SupportsStreaming = false,
                    Verified = false,
                    Rank = 70,
                },
                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "voxtral-mini-latest",
                    Capabilities = AICapability.AudioInput | AICapability.TextOutput,
                    SupportsStreaming = false,
                    Verified = false,
                    Rank = 60,
                },
            };

            return Task.FromResult(models);
        }

        /// <inheritdoc/>
        public override async Task<List<string>> RetrieveApiModels()
        {
            try
            {
                Debug.WriteLine("[MistralAIProviderModels] RetrieveApiModels: preparing request to /models");
                var request = new AIRequestCall
                {
                    Endpoint = "/models",
                };

                var response = await this.mistralProvider.Call(request).ConfigureAwait(false);

                if (response == null)
                {
                    Debug.WriteLine("[MistralAIProviderModels] RetrieveApiModels: response null; returning empty list");
                    return new List<string>();
                }

                var raw = (response as AIReturn)?.Raw;
                if (raw == null)
                {
                    Debug.WriteLine("[MistralAIProviderModels] RetrieveApiModels: raw payload is null; returning empty list");
                    return new List<string>();
                }

                var data = raw["data"] as JArray;
                if (data == null)
                {
                    Debug.WriteLine($"[MistralAIProviderModels] RetrieveApiModels: 'data' array not found. Raw keys: {string.Join(", ", raw.Properties().Select(p => p.Name))}");
                    return new List<string>();
                }

                Debug.WriteLine($"[MistralAIProviderModels] RetrieveApiModels: data items count = {data.Count}");

                var models = new List<string>();
                int added = 0, skipped = 0, index = 0;
                foreach (var token in data)
                {
                    index++;
                    if (token is not JObject item)
                    {
                        skipped++;
                        Debug.WriteLine($"[MistralAIProviderModels] Item #{index} is not an object (type={token?.Type}); skipping");
                        continue;
                    }

                    try
                    {
                        var id = item["id"]?.ToString();
                        var name = item["name"]?.ToString();
                        var model = !string.IsNullOrWhiteSpace(id) ? id : name;

                        if (!string.IsNullOrWhiteSpace(model))
                        {
                            models.Add(model);
                            added++;
                        }
                        else
                        {
                            skipped++;
                            var keys = string.Join(", ", item.Properties().Select(p => p.Name));
                            Debug.WriteLine($"[MistralAIProviderModels] Item #{index} missing 'id' and 'name'. Keys: [{keys}]");
                        }
                    }
                    catch (Exception exItem)
                    {
                        skipped++;
                        Debug.WriteLine($"[MistralAIProviderModels] Exception parsing item #{index}: {exItem.Message}");
                    }
                }

                var distinctSorted = models
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(m => m, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                Debug.WriteLine($"[MistralAIProviderModels] RetrieveApiModels: extracted {distinctSorted.Count} models (added={added}, skipped={skipped})");

                return distinctSorted;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MistralAIProviderModels] RetrieveApiModels: exception thrown; returning empty list. Details: {ex}");
                return new List<string>();
            }
        }
    }
}
