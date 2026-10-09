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
using SmartHopper.ProviderSdk.AICall.Core.Base;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AICall.Metrics;
using SmartHopper.ProviderSdk.Diagnostics;

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

                // Handle provider error responses (e.g. batch items with status_code 4xx/5xx).
                // Gemini error shape: {"error": {"code": N, "message": "...", "status": "..."}}
                if (responseObject["error"] is JObject errorObj)
                {
                    var errMsg = errorObj["message"]?.ToString()
                              ?? errorObj["status"]?.ToString()
                              ?? "Provider returned an error";
                    Debug.WriteLine($"[Gemini] Decode: provider error in response body: {errMsg}");
                    result.Add(new AIInteractionRuntimeMessage { Severity = SHRuntimeMessageSeverity.Error, Content = errMsg });
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

                            var inlineData = part["inlineData"] as JObject ?? part["inline_data"] as JObject;
                            if (inlineData != null)
                            {
                                var data = inlineData["data"]?.ToString();
                                var mimeType = inlineData["mimeType"]?.ToString()
                                               ?? inlineData["mime_type"]?.ToString()
                                               ?? "image/jpeg";

                                if (!string.IsNullOrWhiteSpace(data))
                                {
                                    // Check if this is audio data
                                    if (mimeType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase))
                                    {
                                        byte[]? audioBytes = null;
                                        try
                                        {
                                            audioBytes = Convert.FromBase64String(data);
                                        }
                                        catch (FormatException)
                                        {
                                            // Malformed base64; still surface the interaction without data.
                                        }

                                        // Gemini TTS returns raw PCM (audio/L16); wrap it in a WAV
                                        // container so downstream consumers get a playable format.
                                        var effectiveMimeType = mimeType;
                                        if (audioBytes != null && mimeType.StartsWith("audio/L16", StringComparison.OrdinalIgnoreCase))
                                        {
                                            var rate = ExtractSampleRate(mimeType, 24000);
                                            audioBytes = WrapPcmInWav(audioBytes, rate);
                                            effectiveMimeType = "audio/wav";
                                        }

                                        // Dedicated audio responses must carry a finish reason so
                                        // aggregated call metrics validate.
                                        var finishReason = candidate["finishReason"]?.ToString();
                                        var audioMetrics = new AIMetrics
                                        {
                                            FinishReason = string.IsNullOrWhiteSpace(finishReason)
                                                ? "stop"
                                                : finishReason.ToLowerInvariant(),
                                        };

                                        result.Add(new AIInteractionAudio
                                        {
                                            Agent = AIAgent.Assistant,
                                            Data = audioBytes,
                                            MimeType = effectiveMimeType,
                                            Metrics = audioMetrics,
                                        });
                                        Debug.WriteLine($"[GeminiProvider] Decoded audio response: {mimeType}");
                                    }
                                    else
                                    {
                                        // Image data
                                        result.Add(new AIInteractionImage
                                        {
                                            Agent = AIAgent.Assistant,
                                            ImageData = data,
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
        /// Extracts the <c>rate</c> parameter from an <c>audio/L16</c> MIME type
        /// (e.g. "audio/L16;codec=pcm;rate=24000").
        /// </summary>
        /// <param name="mimeType">The reported MIME type.</param>
        /// <param name="defaultRate">The sample rate to use when none is declared.</param>
        /// <returns>The declared or default sample rate in Hz.</returns>
        private static int ExtractSampleRate(string mimeType, int defaultRate)
        {
            if (!string.IsNullOrWhiteSpace(mimeType))
            {
                foreach (var segment in mimeType.Split(';'))
                {
                    var trimmed = segment.Trim();
                    if (trimmed.StartsWith("rate=", StringComparison.OrdinalIgnoreCase) &&
                        int.TryParse(trimmed.Substring("rate=".Length), out var rate) &&
                        rate > 0)
                    {
                        return rate;
                    }
                }
            }

            return defaultRate;
        }

        /// <summary>
        /// Wraps raw 16-bit mono PCM samples (Gemini's <c>audio/L16</c> payload) in a standard
        /// RIFF/WAV container so the audio is playable by regular consumers.
        /// </summary>
        /// <param name="pcmData">Little-endian signed 16-bit PCM samples.</param>
        /// <param name="sampleRate">The sample rate in Hz.</param>
        /// <returns>A complete WAV file byte array.</returns>
        private static byte[] WrapPcmInWav(byte[] pcmData, int sampleRate)
        {
            const int numChannels = 1;
            const int bitsPerSample = 16;
            var byteRate = sampleRate * numChannels * bitsPerSample / 8;
            var blockAlign = (short)(numChannels * bitsPerSample / 8);
            var dataSize = pcmData?.Length ?? 0;

            using (var stream = new System.IO.MemoryStream(44 + dataSize))
            using (var writer = new System.IO.BinaryWriter(stream))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(36 + dataSize);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
                writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
                writer.Write(16);
                writer.Write((short)1); // PCM format
                writer.Write((short)numChannels);
                writer.Write(sampleRate);
                writer.Write(byteRate);
                writer.Write(blockAlign);
                writer.Write((short)bitsPerSample);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                writer.Write(dataSize);
                if (dataSize > 0)
                {
                    writer.Write(pcmData!);
                }

                writer.Flush();
                return stream.ToArray();
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
                    metrics = metrics with { InputTokensPrompt = promptTokenCount };
                }

                var candidatesTokenCount = usageMetadata["candidatesTokenCount"]?.Value<int>() ?? 0;
                if (candidatesTokenCount > 0)
                {
                    metrics = metrics with { OutputTokensGeneration = candidatesTokenCount };
                }

                // Extract thinking tokens (thoughtsTokenCount) for models that support thinking
                var thoughtsTokenCount = usageMetadata["thoughtsTokenCount"]?.Value<int>() ?? 0;
                if (thoughtsTokenCount > 0)
                {
                    metrics = metrics with { OutputTokensReasoning = thoughtsTokenCount };
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
