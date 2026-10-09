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
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using Newtonsoft.Json.Linq;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AICall.Core.Requests;
using SmartHopper.ProviderSdk.AICall.JsonSchemas;
using SmartHopper.ProviderSdk.AICall.Metrics;

namespace SmartHopper.ProviderSdk.AIProviders
{
    /// <summary>Base class for providers speaking the OpenAI Chat Completions / Responses wire format.</summary>
    /// <typeparam name="T">The derived provider type.</typeparam>
    public abstract class OpenAICompatibleProvider<T> : AIProvider<T>
        where T : OpenAICompatibleProvider<T>
    {
        /// <summary>
        /// Applies the shared tool payload and OpenAI-compatible tool choice.
        /// </summary>
        /// <param name="requestBody">The request body to update.</param>
        /// <param name="request">The request containing tool-call preferences.</param>
        /// <param name="tools">The formatted tools to include.</param>
        /// <param name="logPrefix">The provider-specific log prefix.</param>
        protected internal void ApplyOpenAICompatibleToolChoice(
            JObject requestBody,
            AIRequestCall request,
            JArray tools,
            string logPrefix)
        {
            if (tools == null || tools.Count == 0)
            {
                return;
            }

            requestBody["tools"] = tools;
            if (request.ForceToolCall && !string.IsNullOrWhiteSpace(request.ForceToolName))
            {
                requestBody["tool_choice"] = new JObject
                {
                    ["type"] = "function",
                    ["function"] = new JObject { ["name"] = request.ForceToolName, },
                };
                Debug.WriteLine($"[{logPrefix}] Forcing tool call: {request.ForceToolName}");
            }
            else
            {
                requestBody["tool_choice"] = "auto";
            }
        }

        /// <summary>
        /// Decodes token usage fields shared by OpenAI-compatible APIs.
        /// OpenAI-compatible APIs report reasoning tokens as a breakdown of completion/output
        /// tokens, so generation tokens are returned net of reasoning tokens.
        /// </summary>
        /// <param name="response">The provider response.</param>
        /// <returns>Normalized token usage and finish reason metrics, with generation tokens net of reasoning tokens.</returns>
        protected internal AIMetrics DecodeOpenAICompatibleMetrics(JObject response)
        {
            if (response == null)
            {
                return new AIMetrics();
            }

            try
            {
                var usage = response["usage"] as JObject;
                var totalPromptTokens = usage?["prompt_tokens"]?.Value<int>()
                    ?? usage?["input_tokens"]?.Value<int>()
                    ?? 0;
                var promptDetails = usage?["prompt_tokens_details"] as JObject;
                var inputDetails = usage?["input_tokens_details"] as JObject;
                var inputTokensCached = promptDetails?["cached_tokens"]?.Value<int>()
                    ?? inputDetails?["cached_tokens"]?.Value<int>()
                    ?? 0;
                var totalOutputTokens = usage?["completion_tokens"]?.Value<int>()
                    ?? usage?["output_tokens"]?.Value<int>()
                    ?? 0;
                var completionDetails = usage?["completion_tokens_details"] as JObject;
                var outputDetails = usage?["output_tokens_details"] as JObject;
                var outputTokensReasoning = completionDetails?["reasoning_tokens"]?.Value<int>()
                    ?? outputDetails?["reasoning_tokens"]?.Value<int>()
                    ?? 0;
                var choices = response["choices"] as JArray;
                var firstChoice = choices?.FirstOrDefault() as JObject;

                return new AIMetrics
                {
                    InputTokensCached = inputTokensCached,
                    InputTokensPrompt = totalPromptTokens - inputTokensCached,
                    OutputTokensGeneration = Math.Max(0, totalOutputTokens - outputTokensReasoning),
                    OutputTokensReasoning = outputTokensReasoning,
                    FinishReason = firstChoice?["finish_reason"]?.ToString(),
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[{this.Name}] DecodeOpenAICompatibleMetrics error: {ex.Message}");
                return new AIMetrics();
            }
        }

        /// <summary>
        /// Builds the HTTP content for the request. OpenAI-compatible providers send JSON by
        /// default; dedicated <c>/audio/transcriptions</c> endpoints require
        /// <c>multipart/form-data</c> uploads carrying the audio file plus scalar request fields.
        /// </summary>
        /// <param name="request">The prepared request being executed.</param>
        /// <returns>The HTTP content to send, or <c>null</c> for a bodiless request.</returns>
        protected override HttpContent? BuildRequestContent(AIRequestCall request)
        {
            if (request?.Endpoint?.Contains("/audio/transcriptions", StringComparison.OrdinalIgnoreCase) == true)
            {
                var multipart = this.BuildAudioTranscriptionContent(request);
                if (multipart != null)
                {
                    return multipart;
                }
            }

            return base.BuildRequestContent(request);
        }

        /// <summary>
        /// Builds a <c>multipart/form-data</c> upload for OpenAI-compatible
        /// <c>/audio/transcriptions</c> endpoints. The audio bytes come from the request's last
        /// <see cref="AIInteractionAudio"/>; scalar fields are copied from the provider-encoded
        /// JSON metadata body (<see cref="AIRequestCall.EncodedRequestBody"/>), so the metadata
        /// contract stays defined by the provider's request encoder.
        /// </summary>
        /// <param name="request">The prepared request being executed.</param>
        /// <returns>Multipart content carrying the audio file and metadata.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the request carries no usable audio source: neither resolvable
        /// <see cref="AIInteractionAudio"/> bytes nor a <c>file_url</c> metadata field.
        /// </exception>
        protected virtual HttpContent? BuildAudioTranscriptionContent(AIRequestCall request)
        {
            // Read the provider-encoded metadata first: it declares both the scalar form
            // fields and whether a remote file source ('file_url') is available.
            JObject? metadata = null;
            try
            {
                var encoded = request!.EncodedRequestBody;
                if (!string.IsNullOrWhiteSpace(encoded))
                {
                    metadata = JObject.Parse(encoded);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[{this.Name}] Failed to parse transcription metadata body: {ex.Message}");
            }

            var hasFileUrl = !string.IsNullOrWhiteSpace(metadata?["file_url"]?.ToString());

            var audioInteraction = request?.Body?.Interactions?.OfType<AIInteractionAudio>().LastOrDefault();
            OpenAICompatibleAudioCodec.TryResolveAudioBytes(audioInteraction, out var audioBytes, out var format);
            var hasAudioBytes = audioBytes is { Length: > 0 };

            if (!hasAudioBytes && !hasFileUrl)
            {
                // Without 'file' bytes or a 'file_url', every transcription endpoint rejects the
                // call; fail locally with an actionable error instead of an opaque HTTP 4xx.
                throw new InvalidOperationException(
                    $"Audio transcription request for '{this.Name}' has no audio source: the request carries no resolvable audio data and no 'file_url'.");
            }

            var multipart = new MultipartFormDataContent();

            if (hasFileUrl)
            {
                // 'file_url' (set explicitly via request extras) is the audio source. Endpoints
                // accept exactly one source, so no 'file' part is attached even when the request
                // body also carries audio data.
                if (hasAudioBytes)
                {
                    Debug.WriteLine($"[{this.Name}] Transcription request has both audio data and 'file_url'; using 'file_url' as the audio source.");
                }
            }
            else
            {
                var fileContent = new ByteArrayContent(audioBytes!);
                var mimeType = string.IsNullOrWhiteSpace(audioInteraction!.MimeType)
                    ? $"audio/{format}"
                    : audioInteraction.MimeType;
                fileContent.Headers.ContentType = new MediaTypeHeaderValue(mimeType);

                var fileName = !string.IsNullOrWhiteSpace(audioInteraction.FilePath)
                    ? Path.GetFileName(audioInteraction.FilePath)
                    : $"audio.{format}";
                multipart.Add(fileContent, "file", fileName);
            }

            if (metadata != null)
            {
                foreach (var property in metadata.Properties())
                {
                    if (property.Value is JValue scalar && scalar.Type != JTokenType.Null)
                    {
                        multipart.Add(new StringContent(FormatFormScalar(scalar)), property.Name);
                    }
                    else if (property.Value is JArray array)
                    {
                        // Form-array convention: repeat "<name>[]" for each scalar element.
                        foreach (var element in array.OfType<JValue>())
                        {
                            multipart.Add(new StringContent(FormatFormScalar(element)), $"{property.Name}[]");
                        }
                    }
                    else if (property.Value is JObject nested)
                    {
                        // Nested objects (e.g. chunking_strategy) are sent as compact JSON strings.
                        multipart.Add(new StringContent(nested.ToString(Newtonsoft.Json.Formatting.None)), property.Name);
                    }
                }
            }

            if (metadata?["model"] == null && !string.IsNullOrWhiteSpace(request!.Model))
            {
                multipart.Add(new StringContent(request.Model), "model");
            }

            // Map the interaction language hint onto the standard 'language' field when the
            // provider encoder did not already emit one.
            if (metadata?["language"] == null && !string.IsNullOrWhiteSpace(audioInteraction.LanguageHint))
            {
                multipart.Add(new StringContent(audioInteraction.LanguageHint), "language");
            }

            request!.ContentType = "multipart/form-data";
            return multipart;
        }

        /// <summary>
        /// Formats a JSON scalar as a multipart form field value. Booleans are rendered as
        /// lowercase JSON literals (<c>true</c>/<c>false</c>) and numbers with the invariant
        /// culture, since <see cref="JValue.ToString()"/> is culture-sensitive for those types
        /// (e.g. <c>0.5</c> rendering as <c>0,5</c> under ca-ES/de-DE locales).
        /// </summary>
        /// <param name="value">The scalar token to format.</param>
        /// <returns>The form-safe string representation.</returns>
        private static string FormatFormScalar(JValue value)
        {
            return value.Type switch
            {
                JTokenType.Boolean => value.Value<bool>() ? "true" : "false",
                JTokenType.Float or JTokenType.Integer =>
                    Convert.ToString(value.Value, CultureInfo.InvariantCulture),
                _ => value.ToString(),
            };
        }

        /// <summary>
        /// Parses and wraps a provider JSON schema while keeping wrapper state synchronized.
        /// </summary>
        /// <param name="jsonSchema">The schema JSON.</param>
        /// <param name="wrappedSchema">The provider-ready schema.</param>
        /// <param name="wrapperInfo">The wrapper metadata.</param>
        /// <param name="prepareSchema">Optional preparation applied to the parsed schema before wrapping.</param>
        /// <returns>True when the schema was parsed and wrapped successfully.</returns>
        protected internal bool TryWrapJsonSchema(
            string jsonSchema,
            out JToken wrappedSchema,
            out SchemaWrapperInfo wrapperInfo,
            Action<JObject> prepareSchema = null)
        {
            wrappedSchema = null;
            wrapperInfo = new SchemaWrapperInfo { IsWrapped = false, ProviderName = this.Name };
            var service = JsonSchemaService.Instance;
            if (string.IsNullOrWhiteSpace(jsonSchema))
            {
                service.SetCurrentWrapperInfo(wrapperInfo);
                return false;
            }

            try
            {
                var schema = JObject.Parse(jsonSchema);
                prepareSchema?.Invoke(schema);
                (wrappedSchema, wrapperInfo) = service.WrapForProvider(schema, this.Name);
                service.SetCurrentWrapperInfo(wrapperInfo);
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[{this.Name}] Failed to parse/handle JSON schema: {ex.Message}");
                service.SetCurrentWrapperInfo(wrapperInfo);
                return false;
            }
        }
    }
}
