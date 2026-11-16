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
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SmartHopper.Providers.Gemini.Properties;
using SmartHopper.ProviderSdk.AICall.Core;
using SmartHopper.ProviderSdk.AICall.Core.Base;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AICall.Core.Requests;
using SmartHopper.ProviderSdk.AICall.Core.Returns;
using SmartHopper.ProviderSdk.AIModels;
using SmartHopper.ProviderSdk.AIProviders;
using SmartHopper.ProviderSdk.Logging;

namespace SmartHopper.Providers.Gemini
{
    /// <summary>
    /// Google Gemini AI provider implementation.
    /// </summary>
    public sealed partial class GeminiProvider : AIProvider<GeminiProvider>
    {
        /// <inheritdoc/>
        public override string Name => "Gemini";

        /// <inheritdoc/>
        public override Image Icon => this.LoadIconFromResources(Properties.Resources.gemini_icon);

        /// <inheritdoc/>
        public override bool IsEnabled => true;

        /// <summary>
        /// Gets a value indicating whether this provider is configured in the current environment.
        /// Gemini requires a non-empty API key.
        /// </summary>
        public override bool IsConfigured => this.IsSettingConfigured("ApiKey");

        /// <inheritdoc/>
        public override Uri DefaultServerUrl => new Uri("https://generativelanguage.googleapis.com/v1beta");

        /// <summary>
        /// Initializes a new instance of the <see cref="GeminiProvider"/> class.
        /// </summary>
        private GeminiProvider()
        {
            this.Models = new GeminiProviderModels(this);
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
                    key: "seed",
                    displayName: "Random Seed",
                    description: "Random seed for determinism",
                    type: typeof(int),
                    defaultValue: 0),
                new AIExtraDescriptor(
                    key: "top_p",
                    displayName: "Top-P Sampling",
                    description: "Top-P (nucleus) sampling parameter",
                    type: typeof(double),
                    defaultValue: 0.0),
                new AIExtraDescriptor(
                    key: "top_k",
                    displayName: "Top-K Sampling",
                    description: "Top-K sampling parameter",
                    type: typeof(int),
                    defaultValue: 0),
                new AIExtraDescriptor(
                    key: "service_tier",
                    displayName: "Service Tier",
                    description: "Inference tier override: standard (default), flex (50% discount), priority (premium, lower latency). Overrides global provider setting.",
                    type: typeof(string),
                    defaultValue: string.Empty),
                new AIExtraDescriptor(
                    key: "batch_priority",
                    displayName: "Batch Priority",
                    description: "Batch job priority (0=default, higher=higher priority)",
                    type: typeof(int),
                    defaultValue: 0),
                new AIExtraDescriptor(
                    key: "safety_level",
                    displayName: "Safety Level",
                    description: "Safety filter level",
                    type: typeof(string),
                    defaultValue: "BLOCK_MEDIUM_AND_ABOVE"),
                new AIExtraDescriptor(
                    key: "negative_prompt",
                    displayName: "Negative Prompt",
                    description: "Describes what must NOT appear in a generated image. Folded into the request text (generateContent has no dedicated field).",
                    type: typeof(string),
                    defaultValue: null),
                new AIExtraDescriptor(
                    key: "media_resolution",
                    displayName: "Media Resolution",
                    description: "Vision token cost/quality for media inputs on Gemini 3+ models (2.x rejects the field).",
                    type: typeof(string),
                    defaultValue: null,
                    allowedValues: new[] { "low", "medium", "high", "ultra_high" }),
            };
        }

        /// <inheritdoc/>
        protected override async Task<AIRequestCall> PreCallCoreAsync(AIRequestCall outbound, CancellationToken cancellationToken = default)
        {
            await base.PreCallCoreAsync(outbound, cancellationToken).ConfigureAwait(false);
            outbound.Authentication = "x-goog-api-key";

            if (string.Equals(outbound.Endpoint, "/models", StringComparison.Ordinal))
            {
                outbound.HttpMethod = "GET";
                outbound.RequestKind = AIRequestKind.Backoffice;
                return outbound;
            }

            // Honor explicit wire-path overrides ("/models/...", "/tunedModels/...") — only
            // Gemini-native API paths may be sent to this API. Any other value falls
            // through to capability-based routing below.
            if (!string.IsNullOrWhiteSpace(outbound.Endpoint) &&
                (outbound.Endpoint.StartsWith("/models/", StringComparison.Ordinal) ||
                 outbound.Endpoint.StartsWith("/tunedModels/", StringComparison.Ordinal)))
            {
                outbound.HttpMethod ??= "POST";
                outbound.ContentType ??= "application/json";
                return outbound;
            }

            if (outbound.Capability.HasFlag(AICapability.ImageOutput) ||
                outbound.Capability.HasFlag(AICapability.SpeechOutput) ||
                outbound.Capability.HasFlag(AICapability.AudioOutput))
            {
                // Media outputs are single-shot responses; force the non-streaming
                // generateContent endpoint so the shared JSON pipeline can parse it.
                outbound.Endpoint = $"/models/{outbound.Model}:generateContent";
            }
            else if (outbound.WantsStreaming && this.GetSetting<bool>("EnableStreaming"))
            {
                // Only requests that actually stream may use the SSE endpoint; non-streaming
                // calls go through CallApi which expects a single JSON document.
                outbound.Endpoint = $"/models/{outbound.Model}:streamGenerateContent?alt=sse";
            }
            else
            {
                outbound.Endpoint = $"/models/{outbound.Model}:generateContent";
            }

            outbound.HttpMethod = "POST";
            outbound.ContentType = "application/json";
            return outbound;
        }

        /// <inheritdoc/>
        public override string Encode(IAIInteraction interaction)
        {
            var token = this.EncodeToJToken(interaction);
            return token?.ToString() ?? string.Empty;
        }

        /// <summary>
        /// Converts a single interaction to a Gemini content object (JToken).
        /// Returns null for interactions that should not be sent (e.g., UI-only errors).
        /// </summary>
        private JToken? EncodeToJToken(IAIInteraction interaction)
        {
            if (interaction == null)
            {
                return null;
            }

            // UI-only diagnostics must not be sent to providers
            if (interaction is AIInteractionRuntimeMessage)
            {
                return null;
            }

            // Skip system/context interactions - they are handled via system_instruction in Encode(AIRequestCall)
            if (interaction.Agent == AIAgent.System || interaction.Agent == AIAgent.Context)
            {
                return null;
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

                // Model-bound images ride as inline_data parts in the same user-role message
                foreach (var img in toolResultInteraction.GetModelImages())
                {
                    parts.Add(new JObject
                    {
                        {
                            "inline_data", new JObject
                            {
                                { "mime_type", img.MimeType ?? "image/png" },
                                { "data", img.ImageData },
                            }
                        },
                    });
                }
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
                // Image generation request: emit the prompt as a text part.
                if (string.IsNullOrWhiteSpace(imageInteraction.ImageData) && imageInteraction.ImageUrl == null && !string.IsNullOrWhiteSpace(imageInteraction.OriginalPrompt))
                {
                    parts.Add(new JObject { { "text", imageInteraction.OriginalPrompt } });
                }
                else if (!string.IsNullOrWhiteSpace(imageInteraction.ImageData))
                {
                    parts.Add(new JObject
                    {
                        {
                            "inline_data", new JObject
                            {
                                { "mime_type", imageInteraction.MimeType ?? "image/png" },
                                { "data", imageInteraction.ImageData },
                            }
                        },
                    });
                }
                else if (imageInteraction.ImageUrl != null)
                {
                    // Fetch image from URL and convert to base64 inline data
                    var (base64Data, mimeType) = this.FetchImageFromUrl(imageInteraction.ImageUrl);
                    if (!string.IsNullOrWhiteSpace(base64Data))
                    {
                        parts.Add(new JObject
                        {
                            {
                                "inline_data", new JObject
                                {
                                    { "mime_type", mimeType },
                                    { "data", base64Data },
                                }
                            },
                        });
                    }
                    else
                    {
                        // Fallback: add URL as text if fetch fails
                        parts.Add(new JObject { { "text", imageInteraction.ImageUrl.ToString() } });
                        Logger.For<GeminiProvider>().Warning($"[GeminiProvider] Warning: Failed to fetch image from URL, sending URL as text: {imageInteraction.ImageUrl}");
                    }
                }
            }
            else if (interaction is AIInteractionAudio audioInteraction)
            {
                // Handle audio input for STT
                string base64Data = null;
                string mimeType = audioInteraction.MimeType ?? "audio/wav";

                if (audioInteraction.Data != null && audioInteraction.Data.Length > 0)
                {
                    base64Data = Convert.ToBase64String(audioInteraction.Data);
                }
                else if (!string.IsNullOrWhiteSpace(audioInteraction.FilePath))
                {
                    try
                    {
                        var audioBytes = System.IO.File.ReadAllBytes(audioInteraction.FilePath);
                        base64Data = Convert.ToBase64String(audioBytes);

                        // Try to infer MIME type from file extension if not set
                        if (string.IsNullOrWhiteSpace(audioInteraction.MimeType))
                        {
                            var ext = System.IO.Path.GetExtension(audioInteraction.FilePath).ToLowerInvariant();
                            mimeType = ext switch
                            {
                                ".wav" => "audio/wav",
                                ".mp3" => "audio/mpeg",
                                ".ogg" => "audio/ogg",
                                ".flac" => "audio/flac",
                                ".m4a" => "audio/mp4",
                                _ => "audio/wav",
                            };
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.For<GeminiProvider>().Exception(ex, $"[GeminiProvider] Error reading audio file: {ex.Message}");
                    }
                }

                if (!string.IsNullOrWhiteSpace(base64Data))
                {
                    parts.Add(new JObject
                    {
                        {
                            "inline_data", new JObject
                            {
                                { "mime_type", mimeType },
                                { "data", base64Data },
                            }
                        },
                    });
                    Logger.For<GeminiProvider>().Trace($"[GeminiProvider] Encoded audio input: {mimeType}");
                }
            }

            if (parts.Count > 0)
            {
                // Echo back provider-owned continuation data captured at decode time.
                // Gemini requires thoughtSignature on model parts (especially
                // functionCall parts) for multi-turn tool calling.
                var signature = interaction.ProviderData?["thoughtSignature"]?.ToString();
                if (!string.IsNullOrWhiteSpace(signature) && role == "model" && parts[0] is JObject firstPart)
                {
                    firstPart["thoughtSignature"] = signature;
                }

                return new JObject
                {
                    { "role", role },
                    { "parts", parts },
                };
            }

            return null;
        }

        /// <inheritdoc/>
        public override string Encode(AIRequestCall request)
        {
            var jObject = new JObject();

            // Convert projected interactions (e.g. document events) into provider-encodable user text.
            var interactions = InteractionModelProjection.Project(request.Body?.Interactions);
            if (interactions.Count > 0)
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

                // Provider extra: negative_prompt expresses what must NOT appear in the
                // generated image. The generateContent imageConfig contract has no dedicated
                // negative-prompt field, so it is folded into the request's text content.
                var negativePrompt = request.Parameters?.Extras != null &&
                    request.Parameters.Extras.TryGetValue("negative_prompt", out var npToken) &&
                    npToken != null
                        ? npToken.ToString()
                        : null;

                foreach (var interaction in interactions)
                {
                    // EncodeToJToken returns null for System/Context (handled above as system_instruction)
                    // and for UI-only diagnostics (AIInteractionError), so no extra filtering needed here.
                    var content = this.EncodeToJToken(interaction);
                    if (content != null)
                    {
                        if (!string.IsNullOrWhiteSpace(negativePrompt) &&
                            interaction is AIInteractionImage img &&
                            string.IsNullOrWhiteSpace(img.ImageData) && img.ImageUrl == null &&
                            content["parts"] is JArray encodedParts)
                        {
                            encodedParts.Add(new JObject { { "text", $"Do not include: {negativePrompt}" } });
                        }

                        contents.Add(content);
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

                    generationConfig["responseJsonSchema"] = schema;
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

                // Extract image configuration from the image generation request interaction.
                // The shared size/aspect-ratio concepts are normalized to Gemini's imageConfig
                // vocabulary (aspectRatio as "W:H", imageSize as "1K"/"2K"/"4K" tier).
                var imageInteraction = request.Body.Interactions.FirstOrDefault(i => i is AIInteractionImage) as AIInteractionImage;
                if (imageInteraction != null)
                {
                    var imageConfig = new JObject();

                    var geminiRatio = ToGeminiAspectRatio(imageInteraction.AspectRatio, imageInteraction.ImageSize);
                    if (!string.IsNullOrWhiteSpace(geminiRatio))
                    {
                        imageConfig["aspectRatio"] = geminiRatio;
                    }

                    var geminiSize = ToGeminiImageSize(imageInteraction.ImageSize);
                    if (!string.IsNullOrWhiteSpace(geminiSize))
                    {
                        imageConfig["imageSize"] = geminiSize;
                    }

                    if (imageConfig.Count > 0)
                    {
                        generationConfig["imageConfig"] = imageConfig;
                        Logger.For<GeminiProvider>().Trace($"[GeminiProvider] Image config: {imageConfig}");
                    }
                }
            }
            else if (request.Capability.HasFlag(AICapability.SpeechOutput))
            {
                // TTS: Text-to-Speech output
                var modalities = new JArray { "AUDIO" };
                if (request.Capability.HasFlag(AICapability.TextOutput))
                {
                    modalities.Insert(0, "TEXT");
                }

                generationConfig["responseModalities"] = modalities;

                // Add speech configuration for TTS
                var voiceName = "Kore"; // Default voice
                if (request.Parameters?.Extras != null &&
                    request.Parameters.Extras.TryGetValue("voice", out var voiceToken) &&
                    voiceToken != null)
                {
                    voiceName = voiceToken.ToString();
                }

                generationConfig["speechConfig"] = new JObject
                {
                    ["voiceConfig"] = new JObject
                    {
                        ["prebuiltVoiceConfig"] = new JObject
                        {
                            ["voiceName"] = voiceName,
                        },
                    },
                };
            }
            else if (request.Capability.HasFlag(AICapability.AudioOutput))
            {
                // Lyra music generation
                var modalities = new JArray { "AUDIO" };
                if (request.Capability.HasFlag(AICapability.TextOutput))
                {
                    modalities.Insert(0, "TEXT");
                }

                generationConfig["responseModalities"] = modalities;
            }

            this.ApplyThinkingConfig(generationConfig, request);
            this.ApplySamplingParams(generationConfig, request);

            // Provider extra: media_resolution controls vision token cost/quality for
            // media inputs. Gemini 2.x models reject the field with HTTP 400
            // ("MediaResolution is not supported"), so it is only emitted for the
            // Gemini 3+ family. Accepts "low"/"medium"/"high"/"ultra_high" (with or
            // without the MEDIA_RESOLUTION_ prefix); emitted as the API's enum spelling.
            if (this.IsGemini3OrLaterModel(request.Model) &&
                request.Parameters?.Extras != null &&
                request.Parameters.Extras.TryGetValue("media_resolution", out var mrToken) && mrToken != null)
            {
                var resolution = mrToken.ToString().Trim().ToUpperInvariant();
                if (!resolution.StartsWith("MEDIA_RESOLUTION_", StringComparison.Ordinal))
                {
                    resolution = "MEDIA_RESOLUTION_" + resolution;
                }

                generationConfig["mediaResolution"] = resolution;
            }

            if (generationConfig.Count > 0)
            {
                jObject["generationConfig"] = generationConfig;
            }

            if (!string.IsNullOrWhiteSpace(request.Body?.ToolFilter))
            {
                var toolsArray = this.GetFormattedTools(request.Body.ToolFilter, request.ToolSurface);
                if (toolsArray != null)
                {
                    jObject["tools"] = new JArray
                    {
                        new JObject { { "functionDeclarations", toolsArray } },
                    };

                    // Handle forced tool call: Gemini uses function_calling_config with mode and allowed_function_names
                    if (request.ForceToolCall && !string.IsNullOrWhiteSpace(request.ForceToolName))
                    {
                        jObject["toolConfig"] = new JObject
                        {
                            ["functionCallingConfig"] = new JObject
                            {
                                ["mode"] = "ANY",
                                ["allowedFunctionNames"] = new JArray { request.ForceToolName }
                            }
                        };
                        Logger.For<GeminiProvider>().Trace($"[Gemini] Forcing tool call: {request.ForceToolName}");
                    }
                    else
                    {
                        jObject["toolConfig"] = new JObject
                        {
                            ["functionCallingConfig"] = new JObject
                            {
                                ["mode"] = "AUTO"
                            }
                        };
                    }
                }
            }

            this.ApplySafetySettings(jObject, request);
            this.ApplyServiceTier(jObject, request);

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

            // Enable thought summaries to be returned in the response
            thinkingConfig["includeThoughts"] = true;

            if (this.IsGemini3OrLaterModel(request.Model))
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

        private void ApplySafetySettings(JObject jObject, AIRequestCall request)
        {
            var safetyLevel = request.Parameters?.Extras != null &&
                              request.Parameters.Extras.TryGetValue("safety_level", out var safetyValue) &&
                              safetyValue != null
                              ? safetyValue.ToString()
                              : this.GetSetting<string>("SafetyLevel");

            if (string.IsNullOrWhiteSpace(safetyLevel))
            {
                safetyLevel = "BLOCK_MEDIUM_AND_ABOVE";
            }

            var safetyCategories = new[]
            {
                "HARM_CATEGORY_HARASSMENT",
                "HARM_CATEGORY_HATE_SPEECH",
                "HARM_CATEGORY_SEXUALLY_EXPLICIT",
                "HARM_CATEGORY_DANGEROUS_CONTENT",
                "HARM_CATEGORY_CIVIC_INTEGRITY",
            };

            var safetySettings = new JArray();
            foreach (var category in safetyCategories)
            {
                safetySettings.Add(new JObject
                {
                    { "category", category },
                    { "threshold", safetyLevel },
                });
            }

            jObject["safetySettings"] = safetySettings;
        }

        private void ApplyServiceTier(JObject jObject, AIRequestCall request)
        {
            // Priority: 1) Extra settings per-request, 2) Global provider setting
            var serviceTier = request.Parameters?.Extras != null &&
                              request.Parameters.Extras.TryGetValue("service_tier", out var tierValue) &&
                              tierValue != null
                              ? tierValue.ToString()
                              : this.GetSetting<string>("ServiceTier");

            if (string.IsNullOrWhiteSpace(serviceTier))
            {
                return;
            }

            // Normalize to the wire value: the v1beta serviceTier enum takes
            // lowercase spellings (flex/priority/standard/unspecified) — tolerate
            // prefixed enum forms (SERVICE_TIER_*, TIER_*) and any casing.
            var normalized = serviceTier;
            foreach (var prefix in new[] { "SERVICE_TIER_", "TIER_" })
            {
                if (normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    normalized = normalized.Substring(prefix.Length);
                    break;
                }
            }

            normalized = normalized.ToLowerInvariant();

            // Only add to request if explicitly set (not empty and not the API default)
            if (!string.IsNullOrWhiteSpace(normalized) &&
                !string.Equals(normalized, "standard", StringComparison.Ordinal) &&
                !string.Equals(normalized, "unspecified", StringComparison.Ordinal))
            {
                jObject["serviceTier"] = normalized;
                Logger.For<GeminiProvider>().Trace($"[GeminiProvider] Using service tier: {normalized}");
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

        /// <summary>
        /// True for the Gemini 3+ model family (gemini-3.x, gemini-4.x, and later
        /// numeric majors). Used to gate fields the 2.x generation rejects with
        /// HTTP 400, such as <c>generationConfig.mediaResolution</c>.
        /// </summary>
        private bool IsGemini3OrLaterModel(string model)
        {
            return !string.IsNullOrWhiteSpace(model) && Regex.IsMatch(model, @"^gemini-[3-9]|^gemini-\d{2,}", RegexOptions.IgnoreCase);
        }

        private bool IsGemini25Model(string model)
        {
            return !string.IsNullOrWhiteSpace(model) && Regex.IsMatch(model, @"^gemini-2\.5", RegexOptions.IgnoreCase);
        }

        private bool IsGemini20Model(string model)
        {
            return !string.IsNullOrWhiteSpace(model) && Regex.IsMatch(model, @"^gemini-2\.0", RegexOptions.IgnoreCase);
        }

        /// <summary>Aspect ratios accepted by Gemini <c>imageConfig.aspectRatio</c>.</summary>
        private static readonly string[] GeminiImageAspectRatios =
        {
            "1:1", "3:2", "2:3", "3:4", "4:3", "4:5", "5:4", "9:16", "16:9", "21:9",
        };

        /// <summary>
        /// Maps the shared size input to a Gemini <c>imageConfig.imageSize</c> tier
        /// (<c>1K</c>/<c>2K</c>/<c>4K</c>) by largest dimension; tier names pass through.
        /// </summary>
        private static string ToGeminiImageSize(string size)
        {
            var max = ImageGenerationSpec.ResolveMaxDimension(size);
            if (max == null)
            {
                return null;
            }

            return max <= 1536 ? "1K" : max <= 3072 ? "2K" : "4K";
        }

        /// <summary>
        /// Maps the shared size/aspect-ratio inputs to a Gemini <c>imageConfig.aspectRatio</c>
        /// value, snapping unsupported ratios to the closest supported one.
        /// </summary>
        private static string ToGeminiAspectRatio(string aspectRatio, string size)
        {
            var ratio = ImageGenerationSpec.ResolveAspectRatio(aspectRatio, size);
            if (ratio == null)
            {
                return null;
            }

            if (Array.IndexOf(GeminiImageAspectRatios, ratio) >= 0)
            {
                return ratio;
            }

            return ImageGenerationSpec.SnapToClosestRatio(ratio, GeminiImageAspectRatios);
        }

        private JArray GetFormattedTools(string toolFilter, SmartHopper.ProviderSdk.Hosting.AIToolSurface surface)
        {
            try
            {
                var tools = base.GetFormattedTools(toolFilter, surface);
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

                    // Tool schemas are canonical JSON Schema (draft-07+): e.g. "type" may be a
                    // string array. Gemini's `parameters` field maps to its restricted Schema
                    // proto and rejects those shapes; `parametersJsonSchema` is the documented
                    // full-JSON-Schema field and is mutually exclusive with `parameters`.
                    var parameters = function["parameters"] as JObject ?? new JObject();
                    if (parameters["type"] == null)
                    {
                        parameters = (JObject)parameters.DeepClone();
                        parameters["type"] = "object";
                    }

                    declarations.Add(new JObject
                    {
                        { "name", function["name"]?.ToString() ?? string.Empty },
                        { "description", function["description"]?.ToString() ?? string.Empty },
                        { "parametersJsonSchema", parameters },
                    });
                }

                Logger.For<GeminiProvider>().Trace($"[GetFormattedTools] {declarations.Count} Gemini tools formatted");

                return declarations;
            }
            catch (Exception ex)
            {
                Logger.For<GeminiProvider>().Exception(ex, $"Error formatting tools: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Maximum image size in bytes accepted for inlining (Gemini's inline_data limit is ~20 MB).
        /// </summary>
        private const long MaxInlineImageBytes = 20L * 1024 * 1024;

        /// <summary>
        /// Shared <see cref="HttpClient"/> for image downloads. Reused to avoid socket exhaustion.
        /// Per-request timeout is enforced via a <see cref="System.Threading.CancellationTokenSource"/>
        /// rather than <see cref="HttpClient.Timeout"/> so the static instance can be reused safely.
        /// </summary>
        private static readonly HttpClient ImageFetchClient = new HttpClient();

        /// <summary>
        /// Fetches an image from a URL and returns it as base64-encoded data with MIME type.
        /// Gemini's REST API requires arbitrary HTTP(S) images to be inlined as base64 (file_data
        /// only accepts Gemini File API URIs or YouTube), so a download is unavoidable here.
        /// Uses a shared <see cref="HttpClient"/> with a timeout and a size cap. Encode() is
        /// synchronous, so the async call is bridged with GetAwaiter().GetResult(); this runs on
        /// the AI worker thread and is bounded by <see cref="TimeoutDefaults.DefaultTimeoutSeconds"/>.
        /// </summary>
        /// <param name="imageUrl">The URL of the image to fetch.</param>
        /// <returns>Tuple of (base64Data, mimeType). Base64 data is empty if the fetch fails.</returns>
        private (string base64Data, string mimeType) FetchImageFromUrl(Uri imageUrl)
        {
            try
            {
                Logger.For<GeminiProvider>().Error($"[GeminiProvider] Fetching image from URL: {imageUrl}");

                using var cts = new System.Threading.CancellationTokenSource(
                    TimeSpan.FromSeconds(TimeoutDefaults.DefaultTimeoutSeconds));

                using var response = ImageFetchClient
                    .GetAsync(imageUrl, HttpCompletionOption.ResponseHeadersRead, cts.Token)
                    .GetAwaiter().GetResult();
                response.EnsureSuccessStatusCode();

                if (response.Content.Headers.ContentLength is long contentLength &&
                    contentLength > MaxInlineImageBytes)
                {
                    Logger.For<GeminiProvider>().Error($"[GeminiProvider] Image at {imageUrl} exceeds {MaxInlineImageBytes} bytes (Content-Length={contentLength}); skipping inline.");
                    return (string.Empty, "image/png");
                }

                byte[] imageBytes = response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                if (imageBytes.LongLength > MaxInlineImageBytes)
                {
                    Logger.For<GeminiProvider>().Error($"[GeminiProvider] Image at {imageUrl} exceeds {MaxInlineImageBytes} bytes after download ({imageBytes.LongLength}); skipping inline.");
                    return (string.Empty, "image/png");
                }

                string mimeType = response.Content.Headers.ContentType?.MediaType;
                if (string.IsNullOrWhiteSpace(mimeType) || !mimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                {
                    mimeType = this.GetMimeTypeFromUrl(imageUrl);
                }

                string base64Data = Convert.ToBase64String(imageBytes);

                Logger.For<GeminiProvider>().Trace($"[GeminiProvider] Successfully fetched image: {imageBytes.Length} bytes, MIME type: {mimeType}");

                return (base64Data, mimeType);
            }
            catch (OperationCanceledException)
            {
                Logger.For<GeminiProvider>().Error($"[GeminiProvider] Timed out fetching image from URL {imageUrl} after {TimeoutDefaults.DefaultTimeoutSeconds}s");
                return (string.Empty, "image/png");
            }
            catch (Exception ex)
            {
                Logger.For<GeminiProvider>().Exception(ex, $"[GeminiProvider] Error fetching image from URL {imageUrl}: {ex.Message}");
                return (string.Empty, "image/png");
            }
        }

        /// <summary>
        /// Determines MIME type from URL extension or defaults to image/png.
        /// </summary>
        private string GetMimeTypeFromUrl(Uri url)
        {
            string path = url?.LocalPath?.ToLowerInvariant() ?? string.Empty;
            string extension = Path.GetExtension(path);

            return extension switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".webp" => "image/webp",
                ".heic" => "image/heic",
                ".heif" => "image/heif",
                ".gif" => "image/gif",
                ".bmp" => "image/bmp",
                ".tiff" or ".tif" => "image/tiff",
                _ => "image/png",
            };
        }
    }
}
