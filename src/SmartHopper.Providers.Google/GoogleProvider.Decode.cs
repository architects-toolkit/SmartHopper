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
using SmartHopper.Infrastructure.AICall.Core.Requests;
using SmartHopper.Infrastructure.AICall.Core.Returns;
using SmartHopper.Infrastructure.AIModels;

namespace SmartHopper.Providers.Google
{
    public partial class GoogleProvider
    {
        /// <inheritdoc/>
        protected override AIReturn Decode(JObject responseObject)
        {
            try
            {
                var result = new AIReturn();

                if (responseObject == null)
                {
                    result.AddError("Response object is null");
                    return result;
                }

                var candidates = responseObject["candidates"] as JArray;
                if (candidates == null || candidates.Count == 0)
                {
                    result.AddError("No candidates in response");
                    return result;
                }

                var candidate = candidates[0] as JObject;
                if (candidate == null)
                {
                    result.AddError("First candidate is not a valid object");
                    return result;
                }

                var content = candidate["content"] as JObject;
                if (content != null)
                {
                    var parts = content["parts"] as JArray;
                    if (parts != null)
                    {
                        var textParts = new List<string>();
                        var reasoningParts = new List<string>();

                        foreach (var part in parts.OfType<JObject>())
                        {
                            if (part.ContainsKey("text"))
                            {
                                var text = part["text"]?.ToString();
                                if (!string.IsNullOrWhiteSpace(text) && part["thought"] != true)
                                {
                                    textParts.Add(text);
                                }
                            }

                            if (part["thought"] == true && part.ContainsKey("text"))
                            {
                                var reasoning = part["text"]?.ToString();
                                if (!string.IsNullOrWhiteSpace(reasoning))
                                {
                                    reasoningParts.Add(reasoning);
                                    result.AddInteraction(new AIInteractionReasoning
                                    {
                                        Agent = AIAgent.Assistant,
                                        Text = reasoning,
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
                                        Agent = AIAgent.Assistant,
                                        Id = funcCall["id"]?.ToString() ?? string.Empty,
                                        Name = funcCall["name"]?.ToString() ?? string.Empty,
                                        Arguments = funcCall["args"]?.ToString() ?? "{}",
                                    };
                                    result.AddInteraction(toolCall);
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
                                        result.AddInteraction(new AIInteractionImage
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
                            result.AddInteraction(new AIInteractionText
                            {
                                Agent = AIAgent.Assistant,
                                Text = string.Join(string.Empty, textParts),
                            });
                        }
                    }
                }

                var finishReason = candidate["finishReason"]?.ToString();
                var usageMetadata = responseObject["usageMetadata"] as JObject;

                if (usageMetadata != null)
                {
                    var metrics = new AIMetrics();

                    if (usageMetadata.TryGetValue("promptTokenCount", out var promptTokens) && int.TryParse(promptTokens.ToString(), out var promptCount))
                    {
                        metrics.InputTokensPrompt = promptCount;
                    }

                    if (usageMetadata.TryGetValue("candidatesTokenCount", out var outputTokens) && int.TryParse(outputTokens.ToString(), out var outputCount))
                    {
                        metrics.OutputTokensGeneration = outputCount;
                    }

                    if (usageMetadata.TryGetValue("thoughtsTokenCount", out var thoughtTokens) && int.TryParse(thoughtTokens.ToString(), out var thoughtCount))
                    {
                        metrics.OutputTokensReasoning = thoughtCount;
                    }

                    if (usageMetadata.TryGetValue("cachedContentTokenCount", out var cachedTokens) && int.TryParse(cachedTokens.ToString(), out var cachedCount))
                    {
                        metrics.InputTokensCached = cachedCount;
                    }

                    if (!string.IsNullOrWhiteSpace(finishReason))
                    {
                        metrics.FinishReason = finishReason;
                    }

                    result.Metrics = metrics;
                }

                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error decoding Google response: {ex.Message}");
                var result = new AIReturn();
                result.AddError($"Decode error: {ex.Message}");
                return result;
            }
        }
    }
}
