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
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SmartHopper.Infrastructure.AICall.Core;
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AICall.Core.Requests;
using SmartHopper.Infrastructure.AICall.Core.Returns;
using SmartHopper.Infrastructure.AIModels;
using SmartHopper.Infrastructure.AIProviders;
using SmartHopper.Infrastructure.AITools;
using SmartHopper.Infrastructure.Settings;

namespace SmartHopper.Providers.Gemini
{
    /// <summary>
    /// Google Gemini AI provider implementation.
    /// </summary>
    public partial class GeminiProvider : AIProvider<GeminiProvider>
    {
        private static readonly Lazy<GeminiProvider> LazyInstance = new Lazy<GeminiProvider>(() => new GeminiProvider());

        public static GeminiProvider Instance => LazyInstance.Value;

        /// <inheritdoc/>
        public override string Name => "Gemini";

        /// <inheritdoc/>
        public override Image Icon => null;

        /// <inheritdoc/>
        public override bool IsEnabled => true;

        /// <inheritdoc/>
        public override Uri DefaultServerUrl => new Uri("https://generativelanguage.googleapis.com/v1beta");

        public GeminiProvider()
        {
            this.Models = new GeminiProviderModels(this);
        }

        /// <summary>
        /// Gets the default model for the specified capability.
        /// </summary>
        public string GetDefaultModel(AICapability requiredCapability = AICapability.Text2Text, bool useSettings = true)
        {
            return "gemini-2.5-flash";
        }

        /// <inheritdoc/>
        public override IEnumerable<AIExtraDescriptor> GetExtraDescriptors()
        {
            return new[]
            {
                new AIExtraDescriptor(
                    key: "thinking_level",
                    displayName: "Thinking Level",
                    description: "Gemini 3: minimal/low/medium/high; Gemini 2.5: integer budget as string (0=off)",
                    type: typeof(string),
                    defaultValue: string.Empty),
                new AIExtraDescriptor(
                    key: "batch_priority",
                    displayName: "Batch Priority",
                    description: "Batch job priority (0=default, higher=higher priority)",
                    type: typeof(int),
                    defaultValue: 0),
                new AIExtraDescriptor(
                    key: "image_aspect_ratio",
                    displayName: "Image Aspect Ratio",
                    description: "Image generation aspect ratio (e.g., 16:9, 1:1)",
                    type: typeof(string),
                    defaultValue: string.Empty),
                new AIExtraDescriptor(
                    key: "image_size",
                    displayName: "Image Size",
                    description: "Image generation size (e.g., 1K, 2K, 4K)",
                    type: typeof(string),
                    defaultValue: string.Empty),
                new AIExtraDescriptor(
                    key: "top_k",
                    displayName: "Top-K Sampling",
                    description: "Top-K sampling parameter",
                    type: typeof(int),
                    defaultValue: 0),
                new AIExtraDescriptor(
                    key: "top_p",
                    displayName: "Top-P Sampling",
                    description: "Top-P (nucleus) sampling parameter",
                    type: typeof(double),
                    defaultValue: 0.0),
                new AIExtraDescriptor(
                    key: "seed",
                    displayName: "Random Seed",
                    description: "Random seed for determinism",
                    type: typeof(int),
                    defaultValue: 0),
                new AIExtraDescriptor(
                    key: "safety_level",
                    displayName: "Safety Level",
                    description: "Safety filter level",
                    type: typeof(string),
                    defaultValue: "BLOCK_MEDIUM_AND_ABOVE"),
            };
        }

        /// <inheritdoc/>
        public override AIRequestCall PreCall(AIRequestCall request)
        {
            request.Authentication = "x-goog-api-key";

            if (string.Equals(request.Endpoint, "/models", StringComparison.Ordinal))
            {
                request.HttpMethod = "GET";
                request.RequestKind = AIRequestKind.Backoffice;
                return request;
            }

            if (!string.IsNullOrWhiteSpace(request.Endpoint))
            {
                request.HttpMethod ??= "POST";
                request.ContentType ??= "application/json";
                return request;
            }

            if (request.Capability.HasFlag(AICapability.ImageOutput))
            {
                request.Endpoint = $"/models/{request.Model}:generateContent";
            }
            else if (this.GetSetting<bool>("EnableStreaming"))
            {
                request.Endpoint = $"/models/{request.Model}:streamGenerateContent";
            }
            else
            {
                request.Endpoint = $"/models/{request.Model}:generateContent";
            }

            request.HttpMethod = "POST";
            request.ContentType = "application/json";
            return request;
        }

        /// <inheritdoc/>
        public override string Encode(IAIInteraction interaction)
        {
            // For single interaction, wrap in a request and encode
            var body = AIBodyBuilder.Create().Add(interaction).Build();
            var request = new AIRequestCall();
            request.Body = body;
            return this.Encode(request);
        }

        /// <inheritdoc/>
        public override string Encode(AIRequestCall request)
        {
            var jObject = new JObject();

            var interactions = request.Body?.Interactions;
            if (interactions != null && interactions.Count > 0)
            {
                var systemParts = new List<JObject>();
                var contents = new JArray();

                foreach (var interaction in interactions)
                {
                    if (interaction.Agent == AIAgent.System || interaction.Agent == AIAgent.Context)
                    {
                        var text = (interaction as AIInteractionText)?.Content;
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            systemParts.Add(new JObject { { "text", text } });
                        }
                    }
                }

                if (systemParts.Count > 0)
                {
                    jObject["system_instruction"] = new JObject
                    {
                        { "parts", new JArray(systemParts) },
                    };
                }

                foreach (var interaction in interactions)
                {
                    if (interaction.Agent == AIAgent.System || interaction.Agent == AIAgent.Context)
                    {
                        continue;
                    }

                    var role = interaction.Agent switch
                    {
                        AIAgent.User => "user",
                        AIAgent.Assistant => "model",
                        AIAgent.ToolCall => "model",
                        AIAgent.ToolResult => "user",
                        _ => "user",
                    };

                    var parts = new JArray();

                    if (interaction is AIInteractionText textInteraction)
                    {
                        if (!string.IsNullOrWhiteSpace(textInteraction.Content))
                        {
                            parts.Add(new JObject { { "text", textInteraction.Content } });
                        }

                        if (!string.IsNullOrWhiteSpace(textInteraction.Reasoning))
                        {
                            parts.Add(new JObject
                            {
                                { "text", textInteraction.Reasoning },
                                { "thought", true },
                            });
                        }
                    }
                    else if (interaction is AIInteractionToolResult toolResultInteraction)
                    {
                        parts.Add(new JObject
                        {
                            {
                                "functionResponse", new JObject
                                {
                                    { "id", toolResultInteraction.Id ?? string.Empty },
                                    { "name", toolResultInteraction.Name ?? string.Empty },
                                    { "response", toolResultInteraction.Result ?? new JObject() },
                                }
                            },
                        });
                    }
                    else if (interaction is AIInteractionToolCall toolCallInteraction)
                    {
                        parts.Add(new JObject
                        {
                            {
                                "functionCall", new JObject
                                {
                                    { "id", toolCallInteraction.Id ?? string.Empty },
                                    { "name", toolCallInteraction.Name ?? string.Empty },
                                    { "args", toolCallInteraction.Arguments ?? new JObject() },
                                }
                            },
                        });
                    }
                    else if (interaction is AIInteractionImage imageInteraction)
                    {
                        if (!string.IsNullOrWhiteSpace(imageInteraction.ImageData))
                        {
                            parts.Add(new JObject
                            {
                                {
                                    "inlineData", new JObject
                                    {
                                        { "mimeType", imageInteraction.MimeType ?? "image/png" },
                                        { "data", imageInteraction.ImageData },
                                    }
                                },
                            });
                        }
                        else if (imageInteraction.ImageUrl != null)
                        {
                            parts.Add(new JObject { { "text", imageInteraction.ImageUrl.ToString() } });
                        }
                    }

                    if (parts.Count > 0)
                    {
                        contents.Add(new JObject
                        {
                            { "role", role },
                            { "parts", parts },
                        });
                    }
                }

                jObject["contents"] = contents;
            }

            var generationConfig = new JObject();

            if (request.Parameters?.MaxTokens > 0)
            {
                generationConfig["maxOutputTokens"] = request.Parameters.MaxTokens;
            }

            var temperature = request.Parameters?.Temperature;
            if (temperature == null)
            {
                var configuredTemperature = this.GetSetting<string>("Temperature");
                if (!string.IsNullOrWhiteSpace(configuredTemperature) && double.TryParse(configuredTemperature, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedTemperature))
                {
                    temperature = parsedTemperature;
                }
            }

            if (temperature != null)
            {
                generationConfig["temperature"] = temperature.Value;
            }

            if (request.Capability.HasFlag(AICapability.JsonOutput))
            {
                generationConfig["responseMimeType"] = "application/json";

                if (!string.IsNullOrWhiteSpace(request.Body?.JsonOutputSchema))
                {
                    var schema = JObject.Parse(request.Body.JsonOutputSchema).DeepClone() as JObject;
                    this.StripUnsupportedSchemaKeywords(schema);

                    if (this.IsGemini20Model(request.Model))
                    {
                        this.InjectPropertyOrdering(schema);
                    }

                    generationConfig["responseSchema"] = schema;
                }
            }

            if (request.Capability.HasFlag(AICapability.ImageOutput))
            {
                var modalities = new JArray { "IMAGE" };
                if (request.Capability.HasFlag(AICapability.TextOutput))
                {
                    modalities.Insert(0, "TEXT");
                }

                generationConfig["responseModalities"] = modalities;

                if (request.Parameters?.Extras != null)
                {
                    var imageConfig = new JObject();
                    if (request.Parameters.Extras.TryGetValue("image_aspect_ratio", out var aspectRatio) && aspectRatio != null)
                    {
                        imageConfig["aspectRatio"] = aspectRatio.ToString();
                    }

                    if (request.Parameters.Extras.TryGetValue("image_size", out var imageSize) && imageSize != null)
                    {
                        imageConfig["imageSize"] = imageSize.ToString();
                    }

                    if (imageConfig.Count > 0)
                    {
                        generationConfig["imageConfig"] = imageConfig;
                    }
                }
            }

            this.ApplyThinkingConfig(generationConfig, request);
            this.ApplySamplingParams(generationConfig, request);

            if (generationConfig.Count > 0)
            {
                jObject["generationConfig"] = generationConfig;
            }

            if (!string.IsNullOrWhiteSpace(request.Body?.ToolFilter))
            {
                var toolsArray = this.GetFormattedTools(request.Body.ToolFilter);
                if (toolsArray != null)
                {
                    jObject["tools"] = new JArray
                    {
                        new JObject { { "functionDeclarations", toolsArray } },
                    };
                }
            }

            return jObject.ToString();
        }

        private void ApplyThinkingConfig(JObject generationConfig, AIRequestCall request)
        {
            if (request.Parameters?.Extras == null || !request.Parameters.Extras.TryGetValue("thinking_level", out var thinkingValue))
            {
                return;
            }

            if (thinkingValue == null || string.IsNullOrWhiteSpace(thinkingValue.ToString()))
            {
                return;
            }

            var thinkingStr = thinkingValue.ToString();
            var thinkingConfig = new JObject();

            if (this.IsGemini3Model(request.Model))
            {
                thinkingConfig["thinkingLevel"] = thinkingStr;
            }
            else if (this.IsGemini25Model(request.Model))
            {
                if (int.TryParse(thinkingStr, out var budget))
                {
                    thinkingConfig["thinkingBudget"] = budget;
                }
            }

            if (thinkingConfig.Count > 0)
            {
                generationConfig["thinkingConfig"] = thinkingConfig;
            }
        }

        private void ApplySamplingParams(JObject generationConfig, AIRequestCall request)
        {
            if (request.Parameters?.Extras == null)
            {
                return;
            }

            if (request.Parameters.Extras.TryGetValue("top_k", out var topK) && topK != null && int.TryParse(topK.ToString(), out var topKValue) && topKValue > 0)
            {
                generationConfig["topK"] = topKValue;
            }

            if (request.Parameters.Extras.TryGetValue("top_p", out var topP) && topP != null && double.TryParse(topP.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var topPValue) && topPValue > 0)
            {
                generationConfig["topP"] = topPValue;
            }

            if (request.Parameters.Extras.TryGetValue("seed", out var seed) && seed != null && int.TryParse(seed.ToString(), out var seedValue) && seedValue > 0)
            {
                generationConfig["seed"] = seedValue;
            }
        }

        private void StripUnsupportedSchemaKeywords(JObject schema)
        {
            if (schema == null)
            {
                return;
            }

            var unsupported = new[] { "$schema", "$defs", "$ref", "definitions", "examples", "default" };
            foreach (var key in unsupported)
            {
                schema.Remove(key);
            }

            foreach (var prop in schema.Properties().ToList())
            {
                if (prop.Value is JObject propObj)
                {
                    this.StripUnsupportedSchemaKeywords(propObj);
                }
                else if (prop.Value is JArray propArray)
                {
                    foreach (var item in propArray.OfType<JObject>())
                    {
                        this.StripUnsupportedSchemaKeywords(item);
                    }
                }
            }
        }

        private void InjectPropertyOrdering(JObject schema)
        {
            if (schema == null || !schema.ContainsKey("properties"))
            {
                return;
            }

            var properties = schema["properties"] as JObject;
            if (properties == null)
            {
                return;
            }

            var ordering = new JArray(properties.Properties().Select(p => p.Name));
            schema["propertyOrdering"] = ordering;
        }

        private bool IsGemini3Model(string model)
        {
            return !string.IsNullOrWhiteSpace(model) && Regex.IsMatch(model, @"^gemini-3", RegexOptions.IgnoreCase);
        }

        private bool IsGemini25Model(string model)
        {
            return !string.IsNullOrWhiteSpace(model) && Regex.IsMatch(model, @"^gemini-2\.5", RegexOptions.IgnoreCase);
        }

        private bool IsGemini20Model(string model)
        {
            return !string.IsNullOrWhiteSpace(model) && Regex.IsMatch(model, @"^gemini-2\.0", RegexOptions.IgnoreCase);
        }

        private JArray GetFormattedTools(string toolFilter)
        {
            try
            {
                var tools = base.GetFormattedTools(toolFilter);
                if (tools == null || tools.Count == 0)
                {
                    return null;
                }

                var declarations = new JArray();
                foreach (var tool in tools.OfType<JObject>())
                {
                    var function = tool["function"] as JObject;
                    if (function == null)
                    {
                        continue;
                    }

                    declarations.Add(new JObject
                    {
                        { "name", function["name"]?.ToString() ?? string.Empty },
                        { "description", function["description"]?.ToString() ?? string.Empty },
                        { "parameters", function["parameters"] as JObject ?? new JObject { { "type", "object" } } },
                    });
                }

                Debug.WriteLine($"[GetFormattedTools] {declarations.Count} Gemini tools formatted");

                return declarations;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error formatting tools: {ex.Message}");
                return null;
            }
        }
    }
}
