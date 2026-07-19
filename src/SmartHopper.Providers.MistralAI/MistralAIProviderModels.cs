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
                // Released between April 2026 and July 2026

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "mistral-medium-latest",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 10000,
                    ContextLimit = 262144,
                    Created = new DateTime(2026, 4, 30),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000015m,
                        Completion = 0.0000075m,
                    },
                    Aliases = new List<string> { "mistral-medium-3-5-0", "mistral-medium", "mistral-medium-3-5", "mistral-medium-3.5", "mistral-medium-3", "mistral-medium-2604", "mistral-vibe-cli-latest", "mistral-vibe-cli-with-tools" },
                },



                // Released between January 2026 and April 2026

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "mistral-small-2603",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    Default = AICapability.Text2Text | AICapability.ToolChat | AICapability.Text2Json | AICapability.Image2Text,
                    SupportsStreaming = true,
                    Verified = true,
                    Rank = 9995,
                    ContextLimit = 131072,
                    Created = new DateTime(2026, 3, 16),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000015m,
                        Completion = 0.0000006m,
                        InputCacheRead = 0.000000015m,
                    },
                    Aliases = new List<string> { "mistral-small", "mistral-small-latest", "magistral-small-latest", "mistral-vibe-cli-fast" },
                    DiscouragedForTools = new List<string> { "script_generate", "script_edit" },
                },



                // Released between October 2025 and January 2026

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "ministral-3b-2512",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling,
                    SupportsStreaming = false,
                    Verified = false,
                    Rank = 9990,
                    ContextLimit = 131072,
                    Created = new DateTime(2025, 12, 2),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000001m,
                        Completion = 0.0000001m,
                        InputCacheRead = 0.00000001m,
                    },
                    Aliases = new List<string> { "ministral-3b-latest" },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "ministral-8b-2512",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling,
                    SupportsStreaming = false,
                    Verified = false,
                    Rank = 9985,
                    ContextLimit = 131072,
                    Created = new DateTime(2025, 12, 2),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000015m,
                        Completion = 0.00000015m,
                        InputCacheRead = 0.000000015m,
                    },
                    Aliases = new List<string> { "ministral-8b-latest" },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "ministral-14b-2512",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = false,
                    Verified = false,
                    Rank = 9980,
                    ContextLimit = 262144,
                    Created = new DateTime(2025, 12, 2),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000002m,
                        Completion = 0.0000002m,
                        InputCacheRead = 0.00000002m,
                    },
                    Aliases = new List<string> { "ministral-14b-latest" },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "mistral-large-2512",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9975,
                    ContextLimit = 131072,
                    Created = new DateTime(2025, 12, 1),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000005m,
                        Completion = 0.0000015m,
                        InputCacheRead = 0.00000005m,
                    },
                    Aliases = new List<string> { "mistral-large", "mistral-large-latest", "mistral-large-veteran-2512" },
                },



                // Released between July 2025 and October 2025

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "codestral-2508",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9970,
                    ContextLimit = 256000,
                    Created = new DateTime(2025, 8, 1),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000003m,
                        Completion = 0.0000009m,
                        InputCacheRead = 0.00000003m,
                    },
                    Aliases = new List<string> { "codestral-latest", "mistral-code-latest", "mistral-code-fim-latest" },
                },



                // Released before July 2024 or unknown release date

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "codestral-embed",
                    Capabilities = AICapability.TextInput | AICapability.EmbedOutput,
                    SupportsStreaming = false,
                    Verified = false,
                    Rank = 9965,
                    Aliases = new List<string> { "codestral-embed-2505" },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "labs-leanstral-1-5-1",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.ImageInput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9960,
                    Aliases = new List<string> { "labs-leanstral-1-5" },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "mistral-embed-2312",
                    Capabilities = AICapability.TextInput | AICapability.EmbedOutput,
                    SupportsStreaming = false,
                    Verified = false,
                    Rank = 9955,
                    Aliases = new List<string> { "mistral-embed" },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "mistral-ocr-2512",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.ImageInput | AICapability.ImageOutput,
                    SupportsStreaming = false,
                    Verified = false,
                    Rank = 9950,
                    Aliases = new List<string> { "mistral-ocr-3-0", "mistral-ocr-3" },
                    DiscouragedForTools = new List<string> { "*" },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "mistral-ocr-latest",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.ImageInput | AICapability.ImageOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9945,
                    Aliases = new List<string> { "mistral-ocr-4-0", "mistral-ocr-4" },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "voxtral-mini-2602",
                    Capabilities = AICapability.AudioInput | AICapability.TextOutput,
                    Default = AICapability.Speech2Text,
                    SupportsStreaming = false,
                    Verified = false,
                    Rank = 9940,
                    ContextLimit = 32000,
                    Aliases = new List<string> { "voxtral-mini-transcribe-2602", "voxtral-mini-latest" },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "voxtral-mini-tts-2603",
                    Capabilities = AICapability.TextInput | AICapability.AudioInput | AICapability.AudioOutput,
                    Default = AICapability.Text2Speech,
                    SupportsStreaming = false,
                    Verified = false,
                    Rank = 9935,
                    Aliases = new List<string> { "voxtral-mini-tts-latest" },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "voxtral-mini-tts-mellon-greek-2606-solutions",
                    Capabilities = AICapability.None, // TODO: retrieve capabilities
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9930,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "voxtral-small-2507",
                    Capabilities = AICapability.AudioInput | AICapability.TextOutput,
                    SupportsStreaming = false,
                    Verified = false,
                    Rank = 9925,
                    ContextLimit = 32000,
                    Aliases = new List<string> { "voxtral-small-latest" },
                },



                // Deprecated models

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "devstral-2512",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = 0,
                    ContextLimit = 262144,
                    Created = new DateTime(2025, 12, 9),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.0000004m,
                        Completion = 0.000002m,
                        InputCacheRead = 0.00000004m,
                    },
                    Aliases = new List<string> { "devstral-medium-latest", "devstral-latest", "devstral-medium-251121", "mistral-code-agent-latest" },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "mistral-medium-2508",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling,
                    SupportsStreaming = true,
                    Verified = true,
                    Deprecated = true,
                    Rank = -5,
                    ContextLimit = 131072,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "devstral-medium-2507",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -10,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "devstral-small-2507",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -15,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "labs-leanstral-2603",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.ImageInput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -20,
                    ContextLimit = 256000,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "magistral-medium-2509",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -25,
                    ContextLimit = 40000,
                    Aliases = new List<string> { "magistral-medium-latest" },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "magistral-small-2509",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -30,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "mistral-large-2411",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -35,
                    ContextLimit = 131072,
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
                    Model = "mistral-medium-2505",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.ImageInput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -40,
                    ContextLimit = 128000,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "mistral-moderation-2411",
                    Capabilities = AICapability.TextInput,
                    SupportsStreaming = false,
                    Verified = false,
                    Deprecated = true,
                    Rank = -45,
                    ContextLimit = 128000,
                    Aliases = new List<string> { "mistral-moderation-latest" },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "mistral-moderation-2603",
                    Capabilities = AICapability.TextInput,
                    SupportsStreaming = false,
                    Verified = false,
                    Deprecated = true,
                    Rank = -50,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "mistral-ocr-2505",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.ImageInput | AICapability.ImageOutput,
                    SupportsStreaming = false,
                    Verified = false,
                    Deprecated = true,
                    Rank = -55,
                    DiscouragedForTools = new List<string> { "*" },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "mistral-small-2506",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling,
                    SupportsStreaming = false,
                    Verified = false,
                    Deprecated = true,
                    Rank = -60,
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "open-mistral-nemo",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = false,
                    Verified = false,
                    Deprecated = true,
                    Rank = -65,
                    ContextLimit = 128000,
                    Aliases = new List<string> { "open-mistral-nemo-2407", "mistral-tiny-2407", "mistral-tiny-latest" },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "pixtral-large-2411",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = false,
                    Verified = false,
                    Deprecated = true,
                    Rank = -70,
                    ContextLimit = 131072,
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.000002m,
                        Completion = 0.000006m,
                        InputCacheRead = 0.0000002m,
                    },
                    Aliases = new List<string> { "pixtral-large-latest", "mistral-large-pixtral-2411" },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "voxtral-mini-2507",
                    Capabilities = AICapability.AudioInput | AICapability.TextOutput,
                    SupportsStreaming = true,
                    Verified = false,
                    Deprecated = true,
                    Rank = -75,
                    Aliases = new List<string> { "voxtral-mini-latest" },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "voxtral-mini-transcribe-2507",
                    Capabilities = AICapability.AudioInput | AICapability.TextOutput,
                    SupportsStreaming = false,
                    Verified = false,
                    Deprecated = true,
                    Rank = -80,
                    Aliases = new List<string> { "voxtral-mini-2507" },
                }
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
