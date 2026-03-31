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
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SmartHopper.Infrastructure.AICall.Core.Requests;
using SmartHopper.Infrastructure.AICall.Core.Returns;
using SmartHopper.Infrastructure.AIModels;
using SmartHopper.Infrastructure.AIProviders;
using SmartHopper.Infrastructure.Settings;

namespace SmartHopper.Providers.Google
{
    /// <summary>
    /// Google Gemini AI provider implementation.
    /// </summary>
    public partial class GoogleProvider : AIProvider<GoogleProviderSettings>
    {
        private static readonly Lazy<GoogleProvider> LazyInstance = new Lazy<GoogleProvider>(() => new GoogleProvider());

        public static GoogleProvider Instance => LazyInstance.Value;

        public GoogleProvider()
            : base("Google", "https://generativelanguage.googleapis.com/v1beta")
        {
            this.Models = new GoogleProviderModels(this);
            this.Settings = new GoogleProviderSettings(this);
        }

        /// <inheritdoc/>
        public override string GetDefaultModel()
        {
            return "gemini-2.5-flash";
        }

        /// <inheritdoc/>
        public override IEnumerable<AIExtraDescriptor> GetExtraDescriptors()
        {
            return new[]
            {
                new AIExtraDescriptor
                {
                    Name = "thinking_level",
                    Type = typeof(string),
                    DefaultValue = string.Empty,
                    DisplayName = "Thinking Level",
                    Description = "Gemini 3: minimal/low/medium/high; Gemini 2.5: integer budget as string (0=off)",
                },
                new AIExtraDescriptor
                {
                    Name = "batch_priority",
                    Type = typeof(int),
                    DefaultValue = 0,
                    DisplayName = "Batch Priority",
                    Description = "Batch job priority (0=default, higher=higher priority)",
                },
                new AIExtraDescriptor
                {
                    Name = "image_aspect_ratio",
                    Type = typeof(string),
                    DefaultValue = string.Empty,
                    DisplayName = "Image Aspect Ratio",
                    Description = "Image generation aspect ratio (e.g., 16:9, 1:1)",
                },
                new AIExtraDescriptor
                {
                    Name = "image_size",
                    Type = typeof(string),
                    DefaultValue = string.Empty,
                    DisplayName = "Image Size",
                    Description = "Image generation size (e.g., 1K, 2K, 4K)",
                },
                new AIExtraDescriptor
                {
                    Name = "top_k",
                    Type = typeof(int),
                    DefaultValue = 0,
                    DisplayName = "Top-K Sampling",
                    Description = "Top-K sampling parameter",
                },
                new AIExtraDescriptor
                {
                    Name = "top_p",
                    Type = typeof(double),
                    DefaultValue = 0.0,
                    DisplayName = "Top-P Sampling",
                    Description = "Top-P (nucleus) sampling parameter",
                },
                new AIExtraDescriptor
                {
                    Name = "seed",
                    Type = typeof(int),
                    DefaultValue = 0,
                    DisplayName = "Random Seed",
                    Description = "Random seed for determinism",
                },
                new AIExtraDescriptor
                {
                    Name = "safety_level",
                    Type = typeof(string),
                    DefaultValue = "BLOCK_MEDIUM_AND_ABOVE",
                    DisplayName = "Safety Level",
                    Description = "Safety filter level",
                },
            };
        }

        /// <inheritdoc/>
        protected override void PreCall(AIRequestCall request)
        {
            request.Authentication = "x-goog-api-key";

            if (request.Capability.HasFlag(AICapability.ImageOutput))
            {
                request.Endpoint = $"/models/{request.Model}:generateContent";
            }
            else if (request.EnableStreaming)
            {
                request.Endpoint = $"/models/{request.Model}:streamGenerateContent";
            }
            else
            {
                request.Endpoint = $"/models/{request.Model}:generateContent";
            }

            request.HttpMethod = "POST";
            request.ContentType = "application/json";
        }

        /// <inheritdoc/>
        protected override string Encode(AIRequestCall request)
        {
            var jObject = new JObject();

            if (request.Interactions != null && request.Interactions.Count > 0)
            {
                var systemParts = new List<JObject>();
                var contents = new JArray();

                foreach (var interaction in request.Interactions)
                {
                    if (interaction.Agent == AIAgent.System || interaction.Agent == AIAgent.Context)
                    {
                        if (!string.IsNullOrWhiteSpace(interaction.Text))
                        {
                            systemParts.Add(new JObject { { "text", interaction.Text } });
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

                foreach (var interaction in request.Interactions)
                {
                    if (interaction.Agent == AIAgent.System || interaction.Agent == AIAgent.Context)
                    {
                        continue;
                    }

                    var role = interaction.Agent switch
                    {
                        AIAgent.User => "user",
                        AIAgent.Assistant => "model",
                        _ => "user",
                    };

                    var parts = new JArray();

                    if (!string.IsNullOrWhiteSpace(interaction.Text))
                    {
                        parts.Add(new JObject { { "text", interaction.Text } });
                    }

                    if (interaction.ToolCalls != null && interaction.ToolCalls.Count > 0)
                    {
                        foreach (var toolCall in interaction.ToolCalls)
                        {
                            var argsObj = new JObject();
                            if (!string.IsNullOrWhiteSpace(toolCall.Arguments))
                            {
                                try
                                {
                                    argsObj = JObject.Parse(toolCall.Arguments);
                                }
                                catch
                                {
                                    argsObj["raw"] = toolCall.Arguments;
                                }
                            }

                            parts.Add(new JObject
                            {
                                {
                                    "functionCall", new JObject
                                    {
                                        { "id", toolCall.Id },
                                        { "name", toolCall.Name },
                                        { "args", argsObj },
                                    }
                                },
                            });
                        }
                    }

                    if (interaction.ToolResults != null && interaction.ToolResults.Count > 0)
                    {
                        foreach (var toolResult in interaction.ToolResults)
                        {
                            var responseObj = new JObject();
                            if (!string.IsNullOrWhiteSpace(toolResult.Result))
                            {
                                try
                                {
                                    responseObj = JObject.Parse(toolResult.Result);
                                }
                                catch
                                {
                                    responseObj["text"] = toolResult.Result;
                                }
                            }

                            parts.Add(new JObject
                            {
                                {
                                    "functionResponse", new JObject
                                    {
                                        { "id", toolResult.ToolCallId },
                                        { "name", toolResult.ToolName },
                                        { "response", responseObj },
                                    }
                                },
                            });
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

            if (request.MaxTokens > 0)
            {
                generationConfig["maxOutputTokens"] = request.MaxTokens;
            }

            var temperature = this.GetSetting<string>("Temperature");
            if (!string.IsNullOrWhiteSpace(temperature) && double.TryParse(temperature, NumberStyles.Any, CultureInfo.InvariantCulture, out var tempValue))
            {
                generationConfig["temperature"] = tempValue;
            }

            if (request.Capability.HasFlag(AICapability.JsonOutput))
            {
                generationConfig["responseMimeType"] = "application/json";

                if (request.JsonSchema != null)
                {
                    var schema = request.JsonSchema.DeepClone() as JObject;
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

                if (request.Extras != null)
                {
                    var imageConfig = new JObject();
                    if (request.Extras.TryGetValue("image_aspect_ratio", out var aspectRatio) && aspectRatio != null)
                    {
                        imageConfig["aspectRatio"] = aspectRatio.ToString();
                    }

                    if (request.Extras.TryGetValue("image_size", out var imageSize) && imageSize != null)
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

            if (request.Tools != null && request.Tools.Count > 0)
            {
                var toolsArray = this.GetFormattedTools(request.Tools);
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
            if (request.Extras == null || !request.Extras.TryGetValue("thinking_level", out var thinkingValue))
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
            if (request.Extras == null)
            {
                return;
            }

            if (request.Extras.TryGetValue("top_k", out var topK) && topK != null && int.TryParse(topK.ToString(), out var topKValue) && topKValue > 0)
            {
                generationConfig["topK"] = topKValue;
            }

            if (request.Extras.TryGetValue("top_p", out var topP) && topP != null && double.TryParse(topP.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var topPValue) && topPValue > 0)
            {
                generationConfig["topP"] = topPValue;
            }

            if (request.Extras.TryGetValue("seed", out var seed) && seed != null && int.TryParse(seed.ToString(), out var seedValue) && seedValue > 0)
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

        private JArray GetFormattedTools(List<AITool> tools)
        {
            try
            {
                var toolsArray = new JArray();

                foreach (var tool in tools)
                {
                    var parameters = new JObject
                    {
                        { "type", "object" },
                        { "properties", new JObject() },
                    };

                    var required = new JArray();

                    if (tool.Parameters != null && tool.Parameters.Count > 0)
                    {
                        var props = parameters["properties"] as JObject;
                        foreach (var param in tool.Parameters)
                        {
                            var paramObj = new JObject
                            {
                                { "type", param.Type ?? "string" },
                            };

                            if (!string.IsNullOrWhiteSpace(param.Description))
                            {
                                paramObj["description"] = param.Description;
                            }

                            props[param.Name] = paramObj;

                            if (param.Required)
                            {
                                required.Add(param.Name);
                            }
                        }
                    }

                    if (required.Count > 0)
                    {
                        parameters["required"] = required;
                    }

                    var toolObj = new JObject
                    {
                        { "name", tool.Name },
                        { "description", tool.Description ?? string.Empty },
                        { "parameters", parameters },
                    };

                    toolsArray.Add(toolObj);
                }

                Debug.WriteLine($"[GetFormattedTools] {toolsArray.Count} tools formatted");

                return toolsArray;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error formatting tools: {ex.Message}");
                return null;
            }
        }
    }
}
