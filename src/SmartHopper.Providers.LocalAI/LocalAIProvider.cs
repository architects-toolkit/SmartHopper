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
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SmartHopper.ProviderSdk.AICall.Core;
using SmartHopper.ProviderSdk.AICall.Core.Base;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AICall.Core.Requests;
using SmartHopper.ProviderSdk.AICall.JsonSchemas;
using SmartHopper.ProviderSdk.AICall.Metrics;
using SmartHopper.ProviderSdk.AIModels;
using SmartHopper.ProviderSdk.AIProviders;
using SmartHopper.ProviderSdk.Diagnostics;
using SmartHopper.ProviderSdk.Logging;
using SmartHopper.ProviderSdk.Streaming;

namespace SmartHopper.Providers.LocalAI
{
    /// <summary>
    /// LocalAI provider implementation.
    /// Targets self-hosted LocalAI instances (https://localai.io/) through the
    /// OpenAI-compatible <c>/v1/chat/completions</c> endpoint.
    /// </summary>
    public sealed partial class LocalAIProvider : OpenAICompatibleProvider<LocalAIProvider>
    {
        /// <summary>
        /// The name of the provider. Used by the UI for provider selection.
        /// </summary>
        public static readonly string NameValue = "LocalAI";

        /// <summary>
        /// Fallback default server URL when no Base URL has been configured.
        /// LocalAI defaults to port 8080 on localhost.
        /// </summary>
        public static readonly Uri FallbackServerUrl = new ("http://localhost:8080/v1");

        /// <summary>
        /// Initializes a new instance of the <see cref="LocalAIProvider"/> class.
        /// Private constructor to enforce singleton pattern.
        /// </summary>
        private LocalAIProvider()
        {
            this.Models = new LocalAIProviderModels(this);

            // Register provider-specific JSON schema adapter (object-root wrapper, OpenAI-style)
            JsonSchemaAdapterRegistry.Register(new OpenAICompatibleJsonSchemaAdapter(this.Name));
        }

        /// <inheritdoc/>
        public override string Name => NameValue;

        /// <summary>
        /// Gets the server URL configured for this provider.
        /// Reads the user-provided <c>ServerUrl</c> setting and falls back to <see cref="FallbackServerUrl"/>
        /// when the setting is empty or not a valid absolute HTTP(S) URL.
        /// </summary>
        public override Uri DefaultServerUrl
        {
            get
            {
                var configured = this.GetServerUrlSetting();
                if (!string.IsNullOrWhiteSpace(configured)
                    && Uri.TryCreate(configured, UriKind.Absolute, out var uri)
                    && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
                {
                    return uri;
                }

                return FallbackServerUrl;
            }
        }

        /// <inheritdoc/>
        public override bool IsEnabled => true;

        /// <summary>
        /// LocalAI is configured when a Base URL is available.
        /// The descriptor supplies a default loopback URL, so the provider is considered
        /// configured out of the box unless the URL has been explicitly cleared.
        /// </summary>
        public override bool IsConfigured => !string.IsNullOrWhiteSpace(this.GetServerUrlSetting());

        /// <summary>
        /// Helper to retrieve the configured Base URL setting (raw string).
        /// </summary>
        internal string GetServerUrlSetting()
        {
            return this.GetSetting<string>("ServerUrl");
        }

        /// <inheritdoc/>
        public override Image Icon => this.LoadIconFromResources(Properties.Resources.localai_icon);

        /// <inheritdoc/>
        protected override async Task<AIRequestCall> PreCallCoreAsync(AIRequestCall outbound, CancellationToken cancellationToken = default)
        {
            // First do the base PreCall.
            await base.PreCallCoreAsync(outbound, cancellationToken).ConfigureAwait(false);

            // Setup HTTP method, content type, and authentication.
            outbound.HttpMethod = "POST";
            outbound.ContentType = "application/json";

            // LocalAI API keys are optional (typically configured server-side).
            // Use bearer auth only when the user has supplied a key; otherwise no auth.
            var apiKey = this.GetApiKey();
            outbound.Authentication = string.IsNullOrWhiteSpace(apiKey) ? "none" : "bearer";

            // LocalAI exposes the OpenAI-compatible chat completions endpoint for text and a
            // dedicated images endpoint for diffusion backends.
            outbound.Endpoint = outbound.Capability.HasFlag(AICapability.ImageOutput)
                ? "/images/generations"
                : "/chat/completions";

            return outbound;
        }

        /// <inheritdoc/>
        public override string Encode(AIRequestCall request)
        {
            if (request.HttpMethod == "GET" || request.HttpMethod == "DELETE")
            {
                return "GET and DELETE requests do not use a request body";
            }

            if (request.Endpoint.Contains("/images/generations"))
            {
                return this.FormatImageGenerationRequestBody(request);
            }

            var p = request.Parameters;

            int maxTokens = p?.MaxTokens ?? this.GetSetting<int>("MaxTokens");
            double temperature = p?.Temperature ?? this.GetSetting<double>("Temperature");
            string? toolFilter = request.Body.ToolFilter;

            Logger.For<LocalAIProvider>().Trace($"[LocalAI] Encode - Model: {request.Model}, MaxTokens: {maxTokens}");

            // Simple sequential encoding (same approach as OpenAI/MistralAI/DeepSeek providers).
            var convertedMessages = new JArray();

            // Merge System and Summary interactions before encoding
            var mergedInteractions = this.MergeSystemAndSummary(request.Body.Interactions);

            foreach (var interaction in mergedInteractions)
            {
                try
                {
                    var token = this.EncodeToJToken(interaction);
                    if (token == null)
                    {
                        continue;
                    }

                    var role = token["role"]?.ToString();
                    if (string.IsNullOrEmpty(role))
                    {
                        continue;
                    }

                    convertedMessages.Add(token);
                }
                catch (Exception ex)
                {
                    Logger.For<LocalAIProvider>().Exception(ex, $"[LocalAI] Warning: Could not encode interaction: {ex.Message}");
                }
            }

            // Build request body
            var requestBody = new JObject
            {
                ["model"] = request.Model,
                ["messages"] = convertedMessages,
                ["max_tokens"] = maxTokens,
                ["temperature"] = temperature,
            };

            // Typed request parameters take precedence, then extras (same precedence
            // chain as the other OpenAI-compatible providers).
            var topP = p?.TopP
                ?? (p?.Extras != null && p.Extras.TryGetValue("top_p", out var topPToken)
                    ? topPToken?.Value<double?>()
                    : null);
            if (topP.HasValue)
            {
                requestBody["top_p"] = topP.Value;
            }

            var seed = p?.Seed
                ?? (p?.Extras != null && p.Extras.TryGetValue("seed", out var seedToken)
                    ? seedToken?.Value<int?>()
                    : null);
            if (seed.HasValue)
            {
                requestBody["seed"] = seed.Value;
            }

            if (p?.Extras != null)
            {
                if (p.Extras.TryGetValue("top_k", out var topKToken) && topKToken != null)
                {
                    requestBody["top_k"] = topKToken.Value<int?>();
                }

                if (p.Extras.TryGetValue("presence_penalty", out var ppToken) && ppToken != null)
                {
                    requestBody["presence_penalty"] = ppToken;
                }

                if (p.Extras.TryGetValue("frequency_penalty", out var fpToken) && fpToken != null)
                {
                    requestBody["frequency_penalty"] = fpToken;
                }
            }

            // Add JSON response format if schema is provided (centralized wrapping)
            if (!string.IsNullOrWhiteSpace(request.Body.JsonOutputSchema))
            {
                if (this.TryWrapJsonSchema(request.Body.JsonOutputSchema, out var wrappedSchema, out var wrapperInfo))
                {
                    Logger.For<LocalAIProvider>().Error($"[LocalAI] Schema wrapper info stored (central): IsWrapped={wrapperInfo.IsWrapped}, Type={wrapperInfo.WrapperType}, Property={wrapperInfo.PropertyName}");

                    // LocalAI llama.cpp backends honor json_object response_format
                    requestBody["response_format"] = new JObject { ["type"] = "json_object" };

                    // Add a system guidance message including the wrapped schema
                    var systemMessage = new JObject
                    {
                        ["role"] = "system",
                        ["content"] = "The response must be a valid JSON object that strictly follows this schema: " + wrappedSchema.ToString(Newtonsoft.Json.Formatting.None),
                    };
                    convertedMessages.Insert(0, systemMessage);
                }
                else
                {
                    requestBody["response_format"] = new JObject { ["type"] = "text" };
                }
            }
            else
            {
                JsonSchemaService.Instance.SetCurrentWrapperInfo(new SchemaWrapperInfo { IsWrapped = false });
                requestBody["response_format"] = new JObject { ["type"] = "text" };
            }

            // Add tools if requested
            if (!string.IsNullOrWhiteSpace(toolFilter))
            {
                var tools = this.GetFormattedTools(toolFilter, request.ToolSurface);
                this.ApplyOpenAICompatibleToolChoice(requestBody, request, tools, "LocalAI");
            }

            return requestBody.ToString();
        }

        /// <inheritdoc/>
        /// <summary>
        /// Formats request body for the LocalAI images endpoint (diffusion backends).
        /// LocalAI accepts the OpenAI-compatible fields (model, prompt, size, n,
        /// response_format) plus backend extras (negative_prompt, seed, step).
        /// </summary>
        private string FormatImageGenerationRequestBody(AIRequestCall request)
        {
            var imageInteraction = request.Body.Interactions.FirstOrDefault(i => i is AIInteractionImage) as AIInteractionImage;
            var prompt = imageInteraction?.OriginalPrompt
                ?? request.Body.Interactions.OfType<AIInteractionText>().LastOrDefault()?.Content
                ?? string.Empty;

            var requestBody = new JObject
            {
                ["model"] = request.Model,
                ["prompt"] = prompt,
                ["n"] = 1,
                ["response_format"] = "b64_json",
                ["size"] = ToLocalAISize(imageInteraction?.ImageSize, imageInteraction?.AspectRatio),
            };

            var extras = request.Parameters?.Extras;
            if (extras != null)
            {
                if (extras.TryGetValue("negative_prompt", out var npToken) && npToken != null)
                {
                    requestBody["negative_prompt"] = npToken.ToString();
                }

                if (extras.TryGetValue("seed", out var seedToken) && seedToken != null)
                {
                    requestBody["seed"] = seedToken.Value<long?>();
                }

                // The real diffusion field is "step" (singular). cfg_scale/width/height
                // are not OpenAIRequest fields on LocalAI and are intentionally absent —
                // dimensions ride the shared size parsing above.
                if (extras.TryGetValue("step", out var stepToken) && stepToken != null)
                {
                    requestBody["step"] = stepToken.Value<int?>();
                }
            }

            Logger.For<LocalAIProvider>().Trace($"[LocalAI] ImageGeneration Request: {requestBody}");
            return requestBody.ToString();
        }

        /// <summary>
        /// Maps the shared size/aspect-ratio inputs to a LocalAI <c>size</c> string ("WxH").
        /// Literal dimensions pass through; tier names and ratios resolve to pixel sizes
        /// scaled to the size's largest dimension. Defaults to "1024x1024".
        /// </summary>
        private static string ToLocalAISize(string size, string aspectRatio)
        {
            if (ImageGenerationSpec.TryParseDimensions(size, out var w, out var h))
            {
                return $"{w}x{h}";
            }

            var max = ImageGenerationSpec.ResolveMaxDimension(size) ?? 1024;
            if (ImageGenerationSpec.TryParseRatio(
                    ImageGenerationSpec.ResolveAspectRatio(aspectRatio, null), out var rw, out var rh))
            {
                return rw >= rh
                    ? $"{max}x{Math.Max(1, max * rh / rw)}"
                    : $"{Math.Max(1, max * rw / rh)}x{max}";
            }

            return $"{max}x{max}";
        }

        public override string Encode(IAIInteraction interaction)
        {
            try
            {
                var token = this.EncodeToJToken(interaction);
                return token?.ToString() ?? string.Empty;
            }
            catch (Exception ex)
            {
                Logger.For<LocalAIProvider>().Exception(ex, $"[LocalAI] Encode(IAIInteraction) error: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// Converts a single interaction into a LocalAI/OpenAI-compatible message object.
        /// Returns null for interactions that should not be sent (e.g., UI-only diagnostics).
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

            // Map role
            var role = OpenAICompatibleRoleMapper.MapRole(interaction.Agent);
            if (role == null)
            {
                return null;
            }

            var messageObj = new JObject { ["role"] = role };

            if (interaction is AIInteractionText textInteraction)
            {
                messageObj["content"] = textInteraction.Content ?? string.Empty;
            }
            else if (interaction is AIInteractionToolResult toolResultInteraction)
            {
                messageObj["content"] = toolResultInteraction.Result != null
                    ? JsonConvert.SerializeObject(toolResultInteraction.Result, Formatting.None)
                    : string.Empty;

                if (!string.IsNullOrWhiteSpace(toolResultInteraction.Id))
                {
                    messageObj["tool_call_id"] = toolResultInteraction.Id;
                }

                if (!string.IsNullOrWhiteSpace(toolResultInteraction.Name))
                {
                    messageObj["name"] = toolResultInteraction.Name;
                }
            }
            else if (interaction is AIInteractionToolCall toolCallInteraction)
            {
                var toolCallObj = new JObject
                {
                    ["id"] = toolCallInteraction.Id ?? string.Empty,
                    ["type"] = "function",
                    ["function"] = new JObject
                    {
                        ["name"] = toolCallInteraction.Name ?? string.Empty,
                        ["arguments"] = toolCallInteraction.Arguments?.ToString() ?? "{}",
                    },
                };
                messageObj["tool_calls"] = new JArray { toolCallObj };
                messageObj["content"] = string.Empty;
            }
            else if (interaction is AIInteractionImage imageInteraction)
            {
                // LocalAI vision support depends on backend; fall back to the original prompt
                messageObj["content"] = imageInteraction.OriginalPrompt ?? string.Empty;
            }
            else
            {
                messageObj["content"] = string.Empty;
            }

            return messageObj;
        }

        /// <inheritdoc/>
        public override List<IAIInteraction> Decode(JObject response)
        {
            var interactions = new List<IAIInteraction>();
            if (response == null)
            {
                return interactions;
            }

            try
            {
                // OpenAI-compatible error envelope: {"error": {"message": "...", "type": "..."}}
                if (response["error"] is JObject errorObj)
                {
                    var errMsg = errorObj["message"]?.ToString()
                              ?? errorObj["type"]?.ToString()
                              ?? "Provider returned an error";
                    Logger.For<LocalAIProvider>().Warning($"[LocalAI] Decode: provider error in response body: {errMsg}");
                    interactions.Add(new AIInteractionRuntimeMessage { Severity = SHRuntimeMessageSeverity.Error, Content = errMsg });
                    return interactions;
                }

                // Image generation response: {"created":N, "data":[{"b64_json":"..."|,"url":"..."}]}
                if (response["data"] is JArray dataArray && dataArray.Count > 0)
                {
                    var imageData = dataArray[0];
                    var imageUrl = imageData["url"]?.ToString() ?? string.Empty;
                    var b64Json = imageData["b64_json"]?.ToString() ?? string.Empty;
                    var resultInteraction = new AIInteractionImage
                    {
                        Agent = AIAgent.Assistant,
                    };

                    resultInteraction = resultInteraction.WithResult(
                        imageUrl: imageUrl,
                        imageData: b64Json);

                    interactions.Add(resultInteraction);
                    return interactions;
                }

                var choices = response["choices"] as JArray;
                var firstChoice = choices?.FirstOrDefault() as JObject;
                var message = firstChoice?["message"] as JObject;
                if (message == null)
                {
                    Logger.For<LocalAIProvider>().Trace("[LocalAI] Decode: No message found in response");
                    return interactions;
                }

                // Robust content extraction: handle string, array of parts, or object content
                string content;
                var contentToken = message["content"];
                if (contentToken is JArray contentParts && contentParts.Count > 0)
                {
                    var texts = new List<string>();
                    foreach (var part in contentParts)
                    {
                        var text = part?["text"]?.ToString()
                                   ?? part?["content"]?.ToString()
                                   ?? part?.ToString(Newtonsoft.Json.Formatting.None)
                                   ?? string.Empty;
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            texts.Add(text);
                        }
                    }

                    content = string.Join("\n", texts);
                }
                else
                {
                    content = contentToken?.ToString() ?? string.Empty;
                }

                // Apply centralized unwrapping using stored wrapper info
                var wrapperInfo = JsonSchemaService.Instance.GetCurrentWrapperInfo();
                if (wrapperInfo != null && wrapperInfo.IsWrapped)
                {
                    Logger.For<LocalAIProvider>().Trace($"[LocalAI] Unwrapping response content using wrapper info (central): Type={wrapperInfo.WrapperType}, Property={wrapperInfo.PropertyName}");
                    content = JsonSchemaService.Instance.Unwrap(content, wrapperInfo);
                }

                // LocalAI may also surface reasoning text via reasoning_content (see openai-functions docs)
                var reasoning = message["reasoning_content"]?.ToString();

                var tcs = message["tool_calls"] as JArray;

                // Avoid emitting an empty assistant text interaction for pure tool-call responses
                if (!string.IsNullOrEmpty(content)
                    || !string.IsNullOrEmpty(reasoning)
                    || (tcs != null && tcs.Count > 0))
                {
                    var interaction = new AIInteractionText
                    {
                        Agent = AIAgent.Assistant,
                        Content = content,
                        Reasoning = string.IsNullOrWhiteSpace(reasoning) ? null : reasoning,
                        Metrics = this.DecodeMetrics(response),
                    };
                    interactions.Add(interaction);
                }

                // Add an AIInteractionToolCall for each tool call
                if (tcs != null && tcs.Count > 0)
                {
                    foreach (JObject tc in tcs.OfType<JObject>())
                    {
                        var function = tc["function"] as JObject;
                        var argumentsStr = function?["arguments"]?.ToString() ?? "{}";
                        JObject? argumentsObj = null;
                        try
                        {
                            if (!string.IsNullOrWhiteSpace(argumentsStr))
                            {
                                argumentsObj = JObject.Parse(argumentsStr);
                            }
                        }
                        catch
                        {
                            // Some local backends emit non-JSON arguments; pass them through as-is
                        }

                        var toolCall = new AIInteractionToolCall
                        {
                            Id = tc["id"]?.ToString(),
                            Name = function?["name"]?.ToString(),
                            Arguments = argumentsObj,
                            Reasoning = string.IsNullOrWhiteSpace(reasoning) ? null : reasoning,
                        };
                        interactions.Add(toolCall);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.For<LocalAIProvider>().Exception(ex, $"[LocalAI] Decode error: {ex.Message}");
            }

            return interactions;
        }

        /// <summary>
        /// Decodes the metrics from the response (token usage + finish reason).
        /// </summary>
        private AIMetrics DecodeMetrics(JObject response)
        {
            return this.DecodeOpenAICompatibleMetrics(response) with { Provider = this.Name };
        }

        /// <inheritdoc/>
        public override IEnumerable<AIExtraDescriptor> GetExtraDescriptors()
        {
            return new[]
            {
                new AIExtraDescriptor(
                    "top_p",
                    "Top P",
                    "Nucleus sampling parameter (0.0-1.0). Lower values make output more focused; higher values more diverse. Leave empty to use the backend default.",
                    typeof(double),
                    null),
                new AIExtraDescriptor(
                    "top_k",
                    "Top K",
                    "Limits the next-token candidate pool to the K most likely tokens. Supported by most llama.cpp backends. Leave empty to use the backend default.",
                    typeof(int),
                    null),
                new AIExtraDescriptor(
                    "presence_penalty",
                    "Presence Penalty",
                    "Penalizes tokens already present in the text (-2.0 to 2.0). Positive values encourage new topics.",
                    typeof(double),
                    null),
                new AIExtraDescriptor(
                    "frequency_penalty",
                    "Frequency Penalty",
                    "Penalizes frequent tokens (-2.0 to 2.0). Positive values reduce repetition.",
                    typeof(double),
                    null),

                // LocalAI image generation parameters (/images/generations)
                new AIExtraDescriptor(
                    "negative_prompt",
                    "Negative Prompt",
                    "Describes what must NOT appear in a generated image.",
                    typeof(string),
                    null),
                new AIExtraDescriptor(
                    "seed",
                    "Seed",
                    "Reproducibility seed for image generation.",
                    typeof(int),
                    null),
                new AIExtraDescriptor(
                    "step",
                    "Steps",
                    "Diffusion sampling steps for image generation (higher = slower, more detail).",
                    typeof(int),
                    null),
            };
        }
    }
}
