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
using Newtonsoft.Json.Linq;
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AICall.Metrics;

namespace SmartHopper.Providers.Gemini
{
    public sealed partial class GeminiProvider
    {
        /// <inheritdoc/>
        public override List<IAIInteraction> Decode(JObject responseObject)
        {
            try
            {
                var result = new List<IAIInteraction>();

                if (responseObject == null)
                {
                    return result;
                }

                // Extract usage metadata from response
                this.ExtractUsageMetadata(result, responseObject);

                var candidates = responseObject["candidates"] as JArray;
                if (candidates == null || candidates.Count == 0)
                {
                    return result;
                }

                var candidate = candidates[0] as JObject;
                if (candidate == null)
                {
                    return result;
                }

                var content = candidate["content"] as JObject;
                if (content != null)
                {
                    var parts = content["parts"] as JArray;
                    if (parts != null)
                    {
                        var textParts = new List<string>();

                        foreach (var part in parts.OfType<JObject>())
                        {
                            var isThought = part["thought"]?.Value<bool>() == true;

                            if (part.ContainsKey("text"))
                            {
                                var text = part["text"]?.ToString();
                                if (!string.IsNullOrWhiteSpace(text) && !isThought)
                                {
                                    textParts.Add(text);
                                }
                            }

                            if (isThought && part.ContainsKey("text"))
                            {
                                var reasoning = part["text"]?.ToString();
                                if (!string.IsNullOrWhiteSpace(reasoning))
                                {
                                    result.Add(new AIInteractionText
                                    {
                                        Agent = AIAgent.Assistant,
                                        Reasoning = reasoning,
                                    });
                                }
                            }

                            if (part.ContainsKey("functionCall"))
                            {
                                var funcCall = part["functionCall"] as JObject;
                                if (funcCall != null)
                                {
                                    var toolCall = new AIInteractionToolCall
                                    {
                                        Agent = AIAgent.ToolCall,
                                        Id = funcCall["id"]?.ToString() ?? string.Empty,
                                        Name = funcCall["name"]?.ToString() ?? string.Empty,
                                        Arguments = funcCall["args"] as JObject ?? new JObject(),
                                    };
                                    result.Add(toolCall);
                                }
                            }

                            if (part.ContainsKey("inlineData"))
                            {
                                var inlineData = part["inlineData"] as JObject;
                                if (inlineData != null)
                                {
                                    var imageData = inlineData["data"]?.ToString();
                                    var mimeType = inlineData["mimeType"]?.ToString() ?? "image/jpeg";

                                    if (!string.IsNullOrWhiteSpace(imageData))
                                    {
                                        result.Add(new AIInteractionImage
                                        {
                                            Agent = AIAgent.Assistant,
                                            ImageData = imageData,
                                            MimeType = mimeType,
                                        });
                                    }
                                }
                            }
                        }

                        if (textParts.Count > 0)
                        {
                            result.Add(new AIInteractionText
                            {
                                Agent = AIAgent.Assistant,
                                Content = string.Join(string.Empty, textParts),
                            });
                        }
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error decoding Gemini response: {ex.Message}");
                return new List<IAIInteraction>();
            }
        }

        /// <summary>
        /// Extracts usage metadata from the Gemini response and attaches it to the last interaction.
        /// </summary>
        private void ExtractUsageMetadata(List<IAIInteraction> interactions, JObject jObject)
        {
            try
            {
                var usageMetadata = jObject["usageMetadata"] as JObject;
                if (usageMetadata == null)
                {
                    return;
                }

                var metrics = new AIMetrics();

                var promptTokenCount = usageMetadata["promptTokenCount"]?.Value<int>() ?? 0;
                if (promptTokenCount > 0)
                {
                    metrics.InputTokensPrompt = promptTokenCount;
                }

                var candidatesTokenCount = usageMetadata["candidatesTokenCount"]?.Value<int>() ?? 0;
                if (candidatesTokenCount > 0)
                {
                    metrics.OutputTokensGeneration = candidatesTokenCount;
                }

                // Extract thinking tokens (thoughtsTokenCount) for models that support thinking
                var thoughtsTokenCount = usageMetadata["thoughtsTokenCount"]?.Value<int>() ?? 0;
                if (thoughtsTokenCount > 0)
                {
                    metrics.OutputTokensReasoning = thoughtsTokenCount;
                }

                if (metrics.InputTokensPrompt > 0 || metrics.OutputTokensGeneration > 0)
                {
                    // Store metrics in a temporary interaction that will be merged with the actual response
                    var metricsInteraction = new AIInteractionText
                    {
                        Agent = AIAgent.Assistant,
                        Content = string.Empty,
                        Metrics = metrics,
                    };
                    interactions.Add(metricsInteraction);
                    Debug.WriteLine($"[GeminiProvider] Extracted usage metadata: InputTokens={metrics.InputTokensPrompt}, OutputTokens={metrics.OutputTokensGeneration}, ReasoningTokens={metrics.OutputTokensReasoning}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GeminiProvider] Error extracting usage metadata: {ex.Message}");
            }
        }
    }
}
