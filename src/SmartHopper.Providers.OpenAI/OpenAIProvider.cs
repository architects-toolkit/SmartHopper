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
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SmartHopper.Infrastructure.AICall.Batch;
using SmartHopper.Infrastructure.AICall.Core;
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AICall.Core.Requests;
using SmartHopper.Infrastructure.AICall.Core.Returns;
using SmartHopper.Infrastructure.AICall.JsonSchemas;
using SmartHopper.Infrastructure.AICall.Metrics;
using SmartHopper.Infrastructure.AIModels;
using SmartHopper.Infrastructure.AIProviders;
using SmartHopper.Infrastructure.Diagnostics;
using SmartHopper.Infrastructure.Streaming;
using SmartHopper.Infrastructure.Utilities;

namespace SmartHopper.Providers.OpenAI
{
    /// <summary>
    /// OpenAI provider implementation for SmartHopper.
    /// </summary>
    public partial class OpenAIProvider : AIProvider<OpenAIProvider>, IAIBatchProvider
    {
        #region Compiled Regex Patterns

        /// <summary>
        /// Regex pattern for detecting o-series models (o1, o3, o4, etc.).
        /// </summary>
        [GeneratedRegex(@"^o[0-9]", RegexOptions.IgnoreCase)]
        private static partial Regex OSeriesModelRegex();

        /// <summary>
        /// Regex pattern for detecting GPT-5 models.
        /// </summary>
        [GeneratedRegex(@"^gpt-5", RegexOptions.IgnoreCase)]
        private static partial Regex Gpt5ModelRegex();

        #endregion

        // Schema wrapper information is centralized in JsonSchemaService via AsyncLocal.

        /// <summary>
        /// Initializes a new instance of the <see cref="OpenAIProvider"/> class.
        /// </summary>
        private OpenAIProvider()
        {
            this.Models = new OpenAIProviderModels(this);

            // Register provider-specific JSON schema adapter
            JsonSchemaAdapterRegistry.Register(new OpenAIJsonSchemaAdapter());
        }

        /// <summary>
        /// Gets the name of the provider.
        /// </summary>
        public override string Name => "OpenAI";

        /// <summary>
        /// Gets the default server URL for the provider.
        /// </summary>
        public override Uri DefaultServerUrl => new Uri("https://api.openai.com/v1");

        /// <summary>
        /// Gets a value indicating whether this provider is enabled and should be available for use.
        /// </summary>
        public override bool IsEnabled => true;

        /// <summary>
        /// Gets a value indicating whether this provider is configured in the current environment.
        /// OpenAI requires a non-empty API key.
        /// </summary>
        public override bool IsConfigured => this.IsSettingConfigured("ApiKey");

        /// <summary>
        /// Helper to retrieve the configured API key for this provider.
        /// Exposed to nested streaming adapter to avoid protected access issues.
        /// </summary>
        internal string GetApiKey()
        {
            return this.GetSetting<string>("ApiKey");
        }

        /// <summary>
        /// Gets the provider's icon.
        /// </summary>
        public override Image Icon
        {
            get
            {
                var iconBytes = Properties.Resources.openai_icon;
                using (var ms = new System.IO.MemoryStream(iconBytes))
                {
                    return new Bitmap(ms);
                }
            }
        }

        /// <inheritdoc/>
        protected override IStreamingAdapter CreateStreamingAdapter()
        {
            return new OpenAIStreamingAdapter(this);
        }

        /// <inheritdoc/>
        public override AIRequestCall PreCall(AIRequestCall request)
        {
            // First do the base PreCall
            request = base.PreCall(request);

            // Setup HTTP method, content type, and authentication
            request.HttpMethod = "POST";
            request.ContentType = "application/json";
            request.Authentication = "bearer";

            // Determine endpoint based on request capability and settings.
            // The Responses API is a superset of Chat Completions and is recommended by OpenAI
            // for all new projects. We default to /v1/responses for text, vision, tools, and JSON.
            // Only legacy endpoints (images, audio) and explicit overrides use other paths.
            if (request.Capability.HasFlag(AICapability.ImageOutput))
            {
                request.Endpoint = "/images/generations";
            }
            else if (request.Capability.HasFlag(AICapability.SpeechInput))
            {
                // Speech-to-Text (STT) endpoint
                request.Endpoint = "/audio/transcriptions";
            }
            else if (request.Capability.HasFlag(AICapability.SpeechOutput))
            {
                // Text-to-Speech (TTS) endpoint
                request.Endpoint = "/audio/speech";
            }
            else if (request.Endpoint == "/models")
            {
                request.HttpMethod = "GET";
                request.RequestKind = AIRequestKind.Backoffice;
            }
            else if (string.Equals(request.Endpoint, "/chat/completions", StringComparison.OrdinalIgnoreCase)
                     || this.GetSetting<bool>("ForceChatCompletions"))
            {
                // Explicit Chat Completions override or user forcing legacy endpoint
                request.Endpoint = "/chat/completions";
            }
            else
            {
                // Default to Responses API for all text, vision, tool, and JSON requests.
                // This includes reasoning models, structured outputs, and function calling.
                request.Endpoint = "/responses";
            }

            return request;
        }

        /// <inheritdoc/>
        public override string Encode(AIRequestCall request)
        {
            if (request.HttpMethod == "GET" || request.HttpMethod == "DELETE")
            {
                return "GET and DELETE requests do not use a request body";
            }

            // Handle different endpoints
            if (request.Endpoint.Contains("/images/generations"))
            {
                return this.FormatImageGenerationRequestBody(request);
            }
            else if (request.Endpoint.Contains("/audio/transcriptions"))
            {
                return this.FormatAudioTranscriptionRequestBody(request);
            }
            else if (request.Endpoint.Contains("/audio/speech"))
            {
                return this.FormatAudioSpeechRequestBody(request);
            }
            else if (request.Endpoint.Contains("/responses"))
            {
                var input = this.BuildResponsesInput(request.Body.Interactions);
                return this.FormatResponsesApiRequestBody(request, input);
            }
            else if (request.Endpoint.Contains("/chat/completions"))
            {
                var messages = this.BuildChatCompletionMessages(request.Body.Interactions);
                return this.FormatChatCompletionsRequestBody(request, messages);
            }
            else
            {
                throw new NotSupportedException($"Unsupported OpenAI endpoint: {request.Endpoint}");
            }
        }

        /// <inheritdoc/>
        public override string Encode(IAIInteraction interaction)
        {
            var messageObj = this.EncodeToJToken(interaction, OpenAIRequestFormat.Responses);
            return messageObj?.ToString();
        }

        /// <summary>
        /// Defines which OpenAI API format to use when encoding interactions.
        /// </summary>
        private enum OpenAIRequestFormat
        {
            ChatCompletions,
            Responses,
        }

        /// <inheritdoc/>
        private JToken? EncodeToJToken(IAIInteraction interaction, OpenAIRequestFormat format)
        {
            // Skip UI-only diagnostics
            if (interaction is AIInteractionRuntimeMessage)
            {
                return null;
            }

            // Responses API represents tool calls and tool results as standalone
            // items (no role / no content field) with their own type.
            // NOTE: AIInteractionToolResult inherits from AIInteractionToolCall, so it
            // must be checked first; otherwise tool results would be emitted as function_call.
            if (format == OpenAIRequestFormat.Responses)
            {
                if (interaction is AIInteractionToolResult toolResultResp)
                {
                    return new JObject
                    {
                        ["type"] = "function_call_output",
                        ["call_id"] = toolResultResp.Id,
                        ["output"] = toolResultResp.Result?.ToString() ?? string.Empty,
                    };
                }

                if (interaction is AIInteractionToolCall toolCallResp)
                {
                    return new JObject
                    {
                        ["type"] = "function_call",
                        ["call_id"] = toolCallResp.Id,
                        ["name"] = toolCallResp.Name,
                        ["arguments"] = toolCallResp.Arguments?.ToString() ?? "{}",
                    };
                }
            }

            var messageObj = new JObject();

            switch (interaction.Agent)
            {
                case AIAgent.System:
                    messageObj["role"] = "system";
                    break;
                case AIAgent.Context:
                    messageObj["role"] = "system";
                    break;
                case AIAgent.User:
                    messageObj["role"] = "user";
                    break;
                case AIAgent.Assistant:
                    messageObj["role"] = "assistant";
                    break;
                case AIAgent.ToolCall:
                    messageObj["role"] = "assistant";
                    break;
                case AIAgent.ToolResult:
                    messageObj["role"] = "tool";
                    break;
                default:
                    throw new ArgumentException($"Agent {interaction.Agent} not supported by OpenAI");
            }

            // Handle different interaction types
            if (interaction is AIInteractionText textInteraction)
            {
                if (format == OpenAIRequestFormat.Responses)
                {
                    // Responses API requires 'output_text' for assistant messages and
                    // 'input_text' for user/system/developer messages.
                    var contentType = interaction.Agent == AIAgent.Assistant
                        ? "output_text"
                        : "input_text";
                    var contentArray = new JArray
                    {
                        new JObject
                        {
                            ["type"] = contentType,
                            ["text"] = textInteraction.Content ?? string.Empty,
                        },
                    };
                    messageObj["content"] = contentArray;
                }
                else
                {
                    messageObj["content"] = textInteraction.Content ?? string.Empty;
                }
            }
            else if (interaction is AIInteractionToolResult toolResultInteraction)
            {
                messageObj["tool_call_id"] = toolResultInteraction.Id;
                if (!string.IsNullOrWhiteSpace(toolResultInteraction.Name))
                {
                    messageObj["name"] = toolResultInteraction.Name;
                }

                messageObj["content"] = toolResultInteraction.Result?.ToString() ?? string.Empty;
            }
            else if (interaction is AIInteractionToolCall toolCallInteraction)
            {
                var toolCallObj = new JObject
                {
                    ["id"] = toolCallInteraction.Id,
                    ["type"] = "function",
                    ["function"] = new JObject
                    {
                        ["name"] = toolCallInteraction.Name,

                        // OpenAI requires arguments as a JSON string; never null
                        ["arguments"] = toolCallInteraction.Arguments?.ToString() ?? "{}",
                    },
                };
                messageObj["tool_calls"] = new JArray { toolCallObj };
            }
            else if (interaction is AIInteractionImage imageInteraction)
            {
                // Handle image interactions (for vision models)
                string imageUrlValue;
                if (imageInteraction.ImageUrl != null)
                {
                    imageUrlValue = imageInteraction.ImageUrl.ToString();
                }
                else if (!string.IsNullOrWhiteSpace(imageInteraction.ImageData))
                {
                    // Construct a data URI from base64 data
                    var mimeType = imageInteraction.MimeType ?? "image/png";
                    imageUrlValue = $"data:{mimeType};base64,{imageInteraction.ImageData}";
                }
                else
                {
                    // No image data available; fall back to prompt text
                    messageObj["content"] = imageInteraction.OriginalPrompt ?? string.Empty;
                    imageUrlValue = null;
                }

                if (imageUrlValue != null)
                {
                    JObject imageBlock;
                    if (format == OpenAIRequestFormat.Responses)
                    {
                        imageBlock = new JObject
                        {
                            ["type"] = "input_image",
                            ["image_url"] = imageUrlValue,
                        };
                    }
                    else
                    {
                        imageBlock = new JObject
                        {
                            ["type"] = "image_url",
                            ["image_url"] = new JObject
                            {
                                ["url"] = imageUrlValue,
                            },
                        };
                    }

                    var contentArray = new JArray { imageBlock };
                    messageObj["content"] = contentArray;
                }
            }
            else
            {
                // Fallback to empty string for unknown types
                messageObj["content"] = string.Empty;
            }

            return messageObj;
        }

        /// <summary>
        /// Builds the OpenAI <c>messages</c> array for the Chat Completions API.
        /// </summary>
        private JArray BuildChatCompletionMessages(IReadOnlyList<IAIInteraction> interactions)
        {
            return this.BuildFormattedMessages(interactions, OpenAIRequestFormat.ChatCompletions);
        }

        /// <summary>
        /// Builds the OpenAI <c>input</c> array for the Responses API.
        /// </summary>
        private JArray BuildResponsesInput(IReadOnlyList<IAIInteraction> interactions)
        {
            return this.BuildFormattedMessages(interactions, OpenAIRequestFormat.Responses);
        }

        /// <summary>
        /// Builds the formatted message/input array from SmartHopper interactions.
        /// </summary>
        private JArray BuildFormattedMessages(IReadOnlyList<IAIInteraction> interactions, OpenAIRequestFormat format)
        {
            var messages = new JArray();
            string formatName = format == OpenAIRequestFormat.Responses ? "Responses" : "ChatCompletions";

#if DEBUG
            try
            {
                int cnt = interactions?.Count ?? 0;
                int tc = interactions?.Count(i => i is AIInteractionToolCall) ?? 0;
                int tr = interactions?.Count(i => i is AIInteractionToolResult) ?? 0;
                int tx = interactions?.Count(i => i is AIInteractionText) ?? 0;
                Debug.WriteLine($"[OpenAI] BuildFormattedMessages ({formatName}): interactions={cnt} (toolCalls={tc}, toolResults={tr}, text={tx})");

                Debug.WriteLine($"[OpenAI] Full interaction sequence:");
                for (int debugIdx = 0; debugIdx < interactions.Count; debugIdx++)
                {
                    var it = interactions[debugIdx];
                    var typeStr = it switch
                    {
                        AIInteractionToolResult trDbg => $"ToolResult(id={trDbg.Id}, name={trDbg.Name})",
                        AIInteractionToolCall tcDbg => $"ToolCall(id={tcDbg.Id}, name={tcDbg.Name})",
                        AIInteractionText txtDbg => $"Text(agent={txtDbg.Agent}, len={txtDbg.Content?.Length ?? 0})",
                        AIInteractionRuntimeMessage rm => $"Diagnostic({rm.Severity})",
                        _ => it?.GetType().Name ?? "null"
                    };
                    Debug.WriteLine($"  [{debugIdx}] {typeStr}");
                }
            }
            catch
            {
                /* logging only */
            }

#endif

            // Merge System and Summary interactions before encoding
            var mergedInteractions = this.MergeSystemAndSummary(interactions);

            foreach (var interaction in mergedInteractions)
            {
                var token = this.EncodeToJToken(interaction, format);
                if (token != null)
                {
                    messages.Add(token);
                }
            }

#if DEBUG
            try
            {
                Debug.WriteLine($"[OpenAI] Final encoded {formatName} array ({messages.Count} items):");
                for (int idx = 0; idx < messages.Count; idx++)
                {
                    var msg = messages[idx] as JObject;
                    var role = msg?["role"]?.ToString() ?? "?";
                    var hasToolCalls = msg?["tool_calls"] != null;
                    var toolCallId = msg?["tool_call_id"]?.ToString();
                    var content = msg?["content"]?.ToString();
                    var preview = content != null ? (content.Length > 50 ? content.Substring(0, 50) + "..." : content) : string.Empty;

                    if (hasToolCalls)
                    {
                        var tcArray = msg?["tool_calls"] as JArray;
                        var tcCount = tcArray?.Count ?? 0;
                        var tcIds = tcArray?.Select(tc => tc?["id"]?.ToString()).Where(id => !string.IsNullOrEmpty(id)).ToList();
                        Debug.WriteLine($"  [{idx}] role={role}, tool_calls={tcCount}, ids=[{string.Join(", ", tcIds ?? new List<string>())}]");
                    }
                    else if (!string.IsNullOrEmpty(toolCallId))
                    {
                        Debug.WriteLine($"  [{idx}] role={role}, tool_call_id={toolCallId}, content={preview}");
                    }
                    else
                    {
                        Debug.WriteLine($"  [{idx}] role={role}, content={preview}");
                    }
                }
            }
            catch
            {
                // Intentionally empty
            }
#endif

            return messages;
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
                // Handle provider error responses (e.g. batch items with status_code 4xx/5xx)
                // Format: {"error": {"message": "...", "type": "...", "code": "..."}}
                if (response["error"] is JObject errorObj)
                {
                    var msg = errorObj["message"]?.ToString()
                              ?? errorObj["type"]?.ToString()
                              ?? "Provider returned an error";
                    Debug.WriteLine($"[OpenAI] Decode: provider error in response body: {msg}");
                    interactions.Add(new AIInteractionRuntimeMessage { Severity = SHRuntimeMessageSeverity.Error, Content = msg });
                    return interactions;
                }

                // Handle different response types based on the response structure
                if (response["data"] != null)
                {
                    // Image generation response - create a dummy request for processing
                    var dummyRequest = new AIRequestCall();
                    var body = AIBodyBuilder.Create()
                        .Add(new AIInteractionImage { Agent = AIAgent.Assistant })
                        .Build();
                    dummyRequest.Body = body;
                    return this.ProcessImageGenerationResponseData(response, dummyRequest);
                }
                else if (response["text"] != null && response["task"] != null)
                {
                    // Audio transcription response (STT)
                    return this.ProcessAudioTranscriptionResponseData(response);
                }
                else if (response["output"] != null)
                {
                    // Responses API output (e.g., reasoning models with reasoning summaries)
                    return this.ProcessResponsesApiData(response);
                }
                else if (response["choices"] != null)
                {
                    // Chat completion response
                    return this.ProcessChatCompletionsResponseData(response);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OpenAI] Decode error: {ex.Message}");
            }

            return interactions;
        }

        /// <summary>
        /// Recursively adds additionalProperties=false to all object-type schemas in a JSON schema.
        /// OpenAI requires this for strict mode response_format.
        /// </summary>
        private static void InjectAdditionalPropertiesFalse(JToken token)
        {
            if (token is JObject obj)
            {
                var type = obj["type"]?.ToString();
                if (type == "object")
                {
                    // Only add if not already present
                    if (!obj.ContainsKey("additionalProperties"))
                    {
                        obj["additionalProperties"] = false;
                    }
                }

                // Recurse into all properties
                foreach (var property in obj.Properties().ToList())
                {
                    InjectAdditionalPropertiesFalse(property.Value);
                }
            }
            else if (token is JArray arr)
            {
                foreach (var item in arr)
                {
                    InjectAdditionalPropertiesFalse(item);
                }
            }
        }

        /// <summary>
        /// Recursively ensures every object-type schema node lists all its property keys in <c>required</c>.
        /// OpenAI strict mode requires <c>required</c> to include every key present in <c>properties</c>.
        /// Returns true if any injection was performed (to allow callers to emit a warning).
        /// </summary>
        private static bool InjectRequiredForAllProperties(JToken token)
        {
            var injected = false;

            if (token is JObject obj)
            {
                var type = obj["type"]?.ToString();
                if (type == "object" && obj["properties"] is JObject props)
                {
                    var existingRequired = obj["required"] as JArray ?? new JArray();
                    var existingKeys = new System.Collections.Generic.HashSet<string>(
                        existingRequired.Select(k => k.ToString()),
                        StringComparer.Ordinal);

                    var allKeys = props.Properties().Select(p => p.Name).ToList();
                    var missingKeys = allKeys.Where(k => !existingKeys.Contains(k)).ToList();

                    if (missingKeys.Count > 0)
                    {
                        var newRequired = new JArray(existingRequired);
                        foreach (var key in missingKeys)
                        {
                            newRequired.Add(key);
                        }

                        obj["required"] = newRequired;
                        injected = true;
                    }
                }

                // Recurse into all child tokens
                foreach (var property in obj.Properties().ToList())
                {
                    if (InjectRequiredForAllProperties(property.Value))
                    {
                        injected = true;
                    }
                }
            }
            else if (token is JArray arr)
            {
                foreach (var item in arr)
                {
                    if (InjectRequiredForAllProperties(item))
                    {
                        injected = true;
                    }
                }
            }

            return injected;
        }

        /// <summary>
        /// Formats request body for chat completions endpoint.
        /// </summary>
        private string FormatChatCompletionsRequestBody(AIRequestCall request, JArray messages)
        {
            var p = request.Parameters;

            int maxTokens = p?.MaxTokens ?? this.GetSetting<int>("MaxTokens");
            double temperature = p?.Temperature ?? this.GetSetting<double>("Temperature");
            string reasoningEffort = p?.Extras != null && p.Extras.TryGetValue("reasoning_effort", out var reToken)
                ? reToken?.ToString() ?? this.GetSetting<string>("ReasoningEffort") ?? "medium"
                : this.GetSetting<string>("ReasoningEffort") ?? "medium";

            string jsonSchema = request.Body.JsonOutputSchema;
            string? toolFilter = request.Body.ToolFilter;
            bool hasTools = !string.IsNullOrWhiteSpace(toolFilter);

            Debug.WriteLine($"[OpenAI] FormatRequestBody - Model: {request.Model}, MaxTokens: {maxTokens}, HasTools: {hasTools}");

            // Build request body for chat completions
            var requestBody = new JObject
            {
                ["model"] = request.Model,
                ["messages"] = messages,
            };

            // Configure tokens and parameters based on model family
            // - o-series (o1/o3/o4...) and gpt-5: use max_completion_tokens and reasoning_effort; omit temperature
            // - others: use max_tokens and temperature
            // reasoning_effort is officially supported alongside tools on o-series and gpt-5 models
            // (see https://platform.openai.com/docs/api-reference/chat/create and the GPT-5 cookbook).
            if (OSeriesModelRegex().IsMatch(request.Model) || Gpt5ModelRegex().IsMatch(request.Model))
            {
                requestBody["reasoning_effort"] = reasoningEffort;
                requestBody["max_completion_tokens"] = maxTokens;
            }
            else
            {
                requestBody["max_tokens"] = maxTokens;
                requestBody["temperature"] = temperature;
            }

            // Apply all optional parameters from extras only
            if (p?.Extras != null)
            {
                if (p.Extras.TryGetValue("top_p", out var topPToken) && topPToken != null)
                {
                    requestBody["top_p"] = topPToken.Value<double?>();
                }

                if (p.Extras.TryGetValue("presence_penalty", out var ppToken) && ppToken != null)
                {
                    requestBody["presence_penalty"] = ppToken;
                }

                if (p.Extras.TryGetValue("frequency_penalty", out var fpToken) && fpToken != null)
                {
                    requestBody["frequency_penalty"] = fpToken;
                }

                if (p.Extras.TryGetValue("logprobs", out var logprobsToken) && logprobsToken != null)
                {
                    requestBody["logprobs"] = logprobsToken.Value<bool?>();
                }

                if (p.Extras.TryGetValue("top_logprobs", out var topLogprobsToken) && topLogprobsToken != null)
                {
                    requestBody["top_logprobs"] = topLogprobsToken.Value<int?>();
                }

                if (p.Extras.TryGetValue("prompt_cache_retention", out var cacheRetentionToken) && cacheRetentionToken != null)
                {
                    requestBody["prompt_cache_retention"] = cacheRetentionToken.ToString();
                }

                if (p.Extras.TryGetValue("prompt_cache_key", out var cacheKeyToken) && cacheKeyToken != null)
                {
                    requestBody["prompt_cache_key"] = cacheKeyToken.ToString();
                }
            }

            // Add response format if JSON schema is provided
            if (!string.IsNullOrEmpty(jsonSchema))
            {
                try
                {
                    var schemaObj = JObject.Parse(jsonSchema);

                    // OpenAI requires additionalProperties=false on all object schemas in strict mode
                    InjectAdditionalPropertiesFalse(schemaObj);

                    // OpenAI strict mode requires every property key to appear in required.
                    // Auto-inject missing keys and record a warning so it surfaces in the component.
                    if (InjectRequiredForAllProperties(schemaObj))
                    {
                        request.Messages.Add(new SHRuntimeMessage(
                            SHRuntimeMessageSeverity.Warning,
                            SHRuntimeMessageOrigin.Provider,
                            SHMessageCode.SchemaRequiredAutoAdded,
                            "Schema automatically updated: 'required' was extended to include all properties to comply with OpenAI strict mode."));
                        Debug.WriteLine("[OpenAI] InjectRequiredForAllProperties: auto-added missing keys to required arrays");
                    }

                    var svc = JsonSchemaService.Instance;
                    var (wrappedSchema, wrapperInfo) = svc.WrapForProvider(schemaObj, this.Name);

                    // Store wrapper info for response unwrapping centrally
                    svc.SetCurrentWrapperInfo(wrapperInfo);
                    Debug.WriteLine($"[OpenAI] Schema wrapper info stored (central): IsWrapped={wrapperInfo.IsWrapped}, Type={wrapperInfo.WrapperType}, Property={wrapperInfo.PropertyName}");

                    requestBody["response_format"] = new JObject
                    {
                        ["type"] = "json_schema",
                        ["json_schema"] = new JObject
                        {
                            ["name"] = "response_schema",
                            ["schema"] = wrappedSchema,
                            ["strict"] = true,
                        },
                    };
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[OpenAI] Failed to parse JSON schema: {ex.Message}");

                    // Continue without schema if parsing fails
                    JsonSchemaService.Instance.SetCurrentWrapperInfo(new SchemaWrapperInfo { IsWrapped = false });
                }
            }
            else
            {
                // No schema, so no wrapping needed
                JsonSchemaService.Instance.SetCurrentWrapperInfo(new SchemaWrapperInfo { IsWrapped = false });
            }

            // Add tools if requested
            if (hasTools)
            {
                var tools = this.GetFormattedTools(toolFilter);
                if (tools != null && tools.Count > 0)
                {
                    requestBody["tools"] = tools;

                    // Handle forced tool call: OpenAI uses tool_choice with type and function name
                    if (request.ForceToolCall && !string.IsNullOrWhiteSpace(request.ForceToolName))
                    {
                        requestBody["tool_choice"] = new JObject
                        {
                            ["type"] = "function",
                            ["function"] = new JObject { ["name"] = request.ForceToolName, },
                        };
                        Debug.WriteLine($"[OpenAI] Forcing tool call: {request.ForceToolName}");
                    }
                    else
                    {
                        requestBody["tool_choice"] = "auto";
                    }
                }
            }

            // Debug.WriteLine($"[OpenAI] ChatCompletions Request: {requestBody}");
            return requestBody.ToString();
        }

        /// <summary>
        /// Formats request body for the Responses API endpoint.
        /// Supports reasoning models with reasoning summaries.
        /// </summary>
        private string FormatResponsesApiRequestBody(AIRequestCall request, JArray input)
        {
            var p = request.Parameters;

            int maxTokens = p?.MaxTokens ?? this.GetSetting<int>("MaxTokens");
            string reasoningEffort = p?.Extras != null && p.Extras.TryGetValue("reasoning_effort", out var reToken)
                ? reToken?.ToString() ?? this.GetSetting<string>("ReasoningEffort") ?? "medium"
                : this.GetSetting<string>("ReasoningEffort") ?? "medium";
            string jsonSchema = request.Body.JsonOutputSchema;
            string? toolFilter = request.Body.ToolFilter;
            bool hasTools = !string.IsNullOrWhiteSpace(toolFilter);

            var requestBody = new JObject
            {
                ["model"] = request.Model,
                ["input"] = input,
                ["max_output_tokens"] = maxTokens,
            };

            // Reasoning configuration is only supported by o-series and GPT-5 models on the Responses API.
            // Sending "reasoning" to other models (e.g. gpt-4o-mini) produces an unsupported_parameter error.
            if (OSeriesModelRegex().IsMatch(request.Model) || Gpt5ModelRegex().IsMatch(request.Model))
            {
                var reasoningObj = new JObject
                {
                    ["effort"] = reasoningEffort,
                    ["summary"] = "auto",
                };

                requestBody["reasoning"] = reasoningObj;
            }

            // Apply optional parameters from extras
            if (p?.Extras != null)
            {
                if (p.Extras.TryGetValue("top_p", out var topPToken) && topPToken != null)
                {
                    requestBody["top_p"] = topPToken.Value<double?>();
                }

                if (p.Extras.TryGetValue("presence_penalty", out var ppToken) && ppToken != null)
                {
                    requestBody["presence_penalty"] = ppToken;
                }

                if (p.Extras.TryGetValue("frequency_penalty", out var fpToken) && fpToken != null)
                {
                    requestBody["frequency_penalty"] = fpToken;
                }

                if (p.Extras.TryGetValue("temperature", out var tempToken) && tempToken != null)
                {
                    requestBody["temperature"] = tempToken.Value<double?>();
                }

                if (p.Extras.TryGetValue("prompt_cache_retention", out var cacheRetentionToken) && cacheRetentionToken != null)
                {
                    requestBody["prompt_cache_retention"] = cacheRetentionToken.ToString();
                }

                if (p.Extras.TryGetValue("prompt_cache_key", out var cacheKeyToken) && cacheKeyToken != null)
                {
                    requestBody["prompt_cache_key"] = cacheKeyToken.ToString();
                }
            }

            // Add response format if JSON schema is provided
            if (!string.IsNullOrEmpty(jsonSchema))
            {
                try
                {
                    var schemaObj = JObject.Parse(jsonSchema);
                    InjectAdditionalPropertiesFalse(schemaObj);

                    if (InjectRequiredForAllProperties(schemaObj))
                    {
                        request.Messages.Add(new SHRuntimeMessage(
                            SHRuntimeMessageSeverity.Warning,
                            SHRuntimeMessageOrigin.Provider,
                            SHMessageCode.SchemaRequiredAutoAdded,
                            "Schema automatically updated: 'required' was extended to include all properties to comply with OpenAI strict mode."));
                    }

                    var svc = JsonSchemaService.Instance;
                    var (wrappedSchema, wrapperInfo) = svc.WrapForProvider(schemaObj, this.Name);
                    svc.SetCurrentWrapperInfo(wrapperInfo);

                    requestBody["text"] = new JObject
                    {
                        ["format"] = new JObject
                        {
                            ["type"] = "json_schema",
                            ["name"] = "response_schema",
                            ["schema"] = wrappedSchema,
                            ["strict"] = true,
                        },
                    };
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[OpenAI] Failed to parse JSON schema for Responses API: {ex.Message}");
                    JsonSchemaService.Instance.SetCurrentWrapperInfo(new SchemaWrapperInfo { IsWrapped = false });
                }
            }
            else
            {
                JsonSchemaService.Instance.SetCurrentWrapperInfo(new SchemaWrapperInfo { IsWrapped = false });
            }

            // Add tools if requested
            if (hasTools)
            {
                var tools = this.GetFormattedTools(toolFilter);
                if (tools != null && tools.Count > 0)
                {
                    requestBody["tools"] = this.ConvertToolsToResponsesFormat(tools);

                    if (request.ForceToolCall && !string.IsNullOrWhiteSpace(request.ForceToolName))
                    {
                        requestBody["tool_choice"] = new JObject
                        {
                            ["type"] = "function",
                            ["function"] = new JObject { ["name"] = request.ForceToolName, },
                        };
                        Debug.WriteLine($"[OpenAI] Forcing tool call via Responses API: {request.ForceToolName}");
                    }
                    else
                    {
                        requestBody["tool_choice"] = "auto";
                    }
                }
            }

            Debug.WriteLine($"[OpenAI] Responses API Request: {requestBody}");
            return requestBody.ToString();
        }

        /// <summary>
        /// Converts Chat Completions style tool definitions to Responses API format.
        /// Keeps non-function tools unchanged.
        /// </summary>
        /// <param name="tools">Tools formatted in generic provider shape.</param>
        /// <returns>Tools compatible with OpenAI Responses API.</returns>
        private JArray ConvertToolsToResponsesFormat(JArray tools)
        {
            var converted = new JArray();
            foreach (var token in tools)
            {
                if (token is not JObject tool)
                {
                    continue;
                }

                var type = tool["type"]?.ToString();
                if (!string.Equals(type, "function", StringComparison.OrdinalIgnoreCase))
                {
                    converted.Add(tool.DeepClone());
                    continue;
                }

                // Generic formatter emits { type:"function", function:{ ... } }.
                // Responses expects flattened function fields at the tool root.
                if (tool["function"] is JObject functionObj)
                {
                    var flattened = new JObject
                    {
                        ["type"] = "function",
                    };

                    if (functionObj["name"] != null)
                    {
                        flattened["name"] = functionObj["name"]!.DeepClone();
                    }

                    if (functionObj["description"] != null)
                    {
                        flattened["description"] = functionObj["description"]!.DeepClone();
                    }

                    if (functionObj["parameters"] != null)
                    {
                        // Tool function schemas are passed as-is. We do not enable
                        // OpenAI strict mode for function tools, so the full JSON-Schema
                        // vocabulary (default, format, patternProperties, etc.) is allowed.
                        flattened["parameters"] = functionObj["parameters"]!.DeepClone();
                    }

                    converted.Add(flattened);
                    continue;
                }

                converted.Add(tool.DeepClone());
            }

            return converted;
        }

        /// <summary>
        /// Formats request body for image generation endpoint.
        /// </summary>
        private string FormatImageGenerationRequestBody(AIRequestCall request)
        {
            // Define default values
            string prompt = string.Empty;
            string size = "1024x1024";
            string quality = "standard";
            string style = "vivid";

            // Get parameters from AIInteractionImage in interactions
            if (request.Body.Interactions.Count > 0)
            {
                var interaction = request.Body.Interactions.FirstOrDefault(i => i is AIInteractionImage);
                if (interaction is AIInteractionImage imageRequest)
                {
                    prompt = imageRequest.OriginalPrompt ?? string.Empty;
                    size = imageRequest.ImageSize ?? "1024x1024";
                    quality = imageRequest.ImageQuality ?? "standard";
                    style = imageRequest.ImageStyle ?? "vivid";
                    Debug.WriteLine($"[OpenAI] Image parameters extracted: prompt='{prompt.Substring(0, Math.Min(50, prompt.Length))}...', size={size}, quality={quality}, style={style}");
                }
                else
                {
                    // Fallback: treat first interaction content as prompt
                    var firstInteraction = request.Body.Interactions.First();
                    if (firstInteraction is AIInteractionText textInteraction)
                    {
                        prompt = textInteraction.Content ?? string.Empty;
                        Debug.WriteLine($"[OpenAI] Fallback: using text interaction as prompt: '{prompt.Substring(0, Math.Min(50, prompt.Length))}...'");
                    }
                }
            }
            else
            {
                Debug.WriteLine($"[OpenAI] No interactions found");
            }

            // Build request payload for image generation
            var requestPayload = new JObject
            {
                ["model"] = request.Model,
                ["prompt"] = prompt,
                ["n"] = 1,
                ["size"] = size,
            };

            // Add quality and style for DALL-E 3 models
            if (request.Model.Contains("dall-e-3", StringComparison.OrdinalIgnoreCase))
            {
                requestPayload["quality"] = quality;
                requestPayload["style"] = style;
            }

            Debug.WriteLine($"[OpenAI] ImageGeneration Request: {requestPayload}");
            return requestPayload.ToString();
        }

        /// <summary>
        /// Formats request body for audio transcription (STT) endpoint.
        /// </summary>
        private string FormatAudioTranscriptionRequestBody(AIRequestCall request)
        {
            // For audio transcription, the API expects multipart/form-data
            // but we prepare the JSON structure here for the request body metadata
            var requestPayload = new JObject
            {
                ["model"] = request.Model,
                ["response_format"] = "json",
            };

            // Add optional language hint if provided in extras
            if (request.Parameters?.Extras != null)
            {
                if (request.Parameters.Extras.TryGetValue("language", out var langToken) && langToken != null)
                {
                    requestPayload["language"] = langToken.ToString();
                }

                if (request.Parameters.Extras.TryGetValue("prompt", out var promptToken) && promptToken != null)
                {
                    requestPayload["prompt"] = promptToken.ToString();
                }

                if (request.Parameters.Extras.TryGetValue("temperature", out var tempToken) && tempToken != null)
                {
                    requestPayload["temperature"] = tempToken.Value<double?>();
                }
            }

            Debug.WriteLine($"[OpenAI] AudioTranscription Request: {requestPayload}");
            return requestPayload.ToString();
        }

        /// <summary>
        /// Formats request body for audio speech (TTS) endpoint.
        /// </summary>
        private string FormatAudioSpeechRequestBody(AIRequestCall request)
        {
            // Get the text input from the request body
            string input = string.Empty;
            if (request.Body.Interactions.Count > 0)
            {
                var firstInteraction = request.Body.Interactions.FirstOrDefault(i => i is AIInteractionText);
                if (firstInteraction is AIInteractionText textInteraction)
                {
                    input = textInteraction.Content ?? string.Empty;
                }
            }

            // Default voice
            string voice = "alloy";
            string responseFormat = "mp3";
            double speed = 1.0;

            // Get voice and format from extras if provided
            if (request.Parameters?.Extras != null)
            {
                if (request.Parameters.Extras.TryGetValue("voice", out var voiceToken) && voiceToken != null)
                {
                    voice = voiceToken.ToString();
                }

                if (request.Parameters.Extras.TryGetValue("response_format", out var formatToken) && formatToken != null)
                {
                    responseFormat = formatToken.ToString();
                }

                if (request.Parameters.Extras.TryGetValue("speed", out var speedToken) && speedToken != null)
                {
                    speed = speedToken.Value<double?>() ?? 1.0;
                }
            }

            var requestPayload = new JObject
            {
                ["model"] = request.Model,
                ["input"] = input,
                ["voice"] = voice,
                ["response_format"] = responseFormat,
                ["speed"] = speed,
            };

            Debug.WriteLine($"[OpenAI] AudioSpeech Request: model={request.Model}, voice={voice}, input length={input.Length}");
            return requestPayload.ToString();
        }

        /// <summary>
        /// Decodes metrics from OpenAI response.
        /// </summary>
        private AIMetrics DecodeMetrics(JObject response)
        {
            var metrics = new AIMetrics();

            if (response == null)
            {
                return metrics;
            }

            try
            {
                var usage = response["usage"] as JObject;

                if (usage != null)
                {
                    // Support both Chat Completions API and Responses API token field names
                    var totalPromptTokens = usage["prompt_tokens"]?.Value<int>()
                        ?? usage["input_tokens"]?.Value<int>()
                        ?? 0;

                    // Extract cached tokens from nested details object
                    var promptDetails = usage["prompt_tokens_details"] as JObject;
                    var inputDetails = usage["input_tokens_details"] as JObject;
                    metrics.InputTokensCached = promptDetails?["cached_tokens"]?.Value<int>()
                        ?? inputDetails?["cached_tokens"]?.Value<int>()
                        ?? 0;
                    metrics.InputTokensPrompt = totalPromptTokens - metrics.InputTokensCached;

                    metrics.OutputTokensGeneration = usage["completion_tokens"]?.Value<int>()
                        ?? usage["output_tokens"]?.Value<int>()
                        ?? metrics.OutputTokensGeneration;

                    // Extract reasoning tokens from nested completion/output token details (o1/o3/GPT-5 models)
                    var completionDetails = usage["completion_tokens_details"] as JObject;
                    var outputDetails = usage["output_tokens_details"] as JObject;
                    var reasoningTokens = completionDetails?["reasoning_tokens"]?.Value<int>()
                        ?? outputDetails?["reasoning_tokens"]?.Value<int>()
                        ?? 0;
                    metrics.OutputTokensReasoning = reasoningTokens;
                }

                // Handle finish reason for chat completions
                var choices = response["choices"] as JArray;
                var firstChoice = choices?.FirstOrDefault() as JObject;
                if (firstChoice != null)
                {
                    metrics.FinishReason = firstChoice["finish_reason"]?.ToString() ?? metrics.FinishReason;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OpenAI] DecodeMetrics error: {ex.Message}");
            }

            return metrics;
        }

        /// <summary>
        /// Processes chat completions response data and converts to interactions.
        /// </summary>
        private List<IAIInteraction> ProcessChatCompletionsResponseData(JObject responseJson)
        {
            var interactions = new List<IAIInteraction>();

            try
            {
                Debug.WriteLine($"[OpenAI] ProcessChatCompletionsResponseData - Processing response");

                // Extract response content
                var choices = responseJson["choices"] as JArray;
                var firstChoice = choices?.FirstOrDefault() as JObject;
                var message = firstChoice?["message"] as JObject;

                if (message == null)
                {
                    Debug.WriteLine($"[OpenAI] No message in response: {responseJson}");
                    return interactions;
                }

                // Extract content from Chat Completions response.
                // Per official OpenAI docs, Chat Completions message.content is a string in responses.
                // Content arrays (if present) only contain text parts; reasoning is not exposed here.
                // Reasoning text is only available via the Responses API output array.
                string content = string.Empty;

                var contentToken = message["content"];
                if (contentToken is JArray contentArray)
                {
                    var contentParts = new List<string>();

                    foreach (var part in contentArray.OfType<JObject>())
                    {
                        var type = part["type"]?.ToString();
                        if (string.Equals(type, "text", StringComparison.OrdinalIgnoreCase))
                        {
                            var textVal = part["text"]?.ToString() ?? part["content"]?.ToString();
                            if (!string.IsNullOrEmpty(textVal)) contentParts.Add(textVal);
                        }
                        else if (string.Equals(type, "output_text", StringComparison.OrdinalIgnoreCase))
                        {
                            // OpenAI Responses-style content item; treat as normal assistant text
                            var textVal = part["text"]?.ToString();
                            if (!string.IsNullOrEmpty(textVal)) contentParts.Add(textVal);
                        }
                    }

                    content = string.Join(string.Empty, contentParts).Trim();
                }
                else if (contentToken != null)
                {
                    content = contentToken.ToString();
                }

                // Implement schema unwrapping if needed
                var wrapperInfo = JsonSchemaService.Instance.GetCurrentWrapperInfo();
                if (wrapperInfo != null && wrapperInfo.IsWrapped)
                {
                    Debug.WriteLine($"[OpenAI] Unwrapping response content using wrapper info (central): Type={wrapperInfo.WrapperType}, Property={wrapperInfo.PropertyName}");
                    content = JsonSchemaService.Instance.Unwrap(content, wrapperInfo);
                    Debug.WriteLine($"[OpenAI] Content after unwrapping: {content.Substring(0, Math.Min(100, content.Length))}...");
                }

                var interaction = new AIInteractionText();
                interaction.SetResult(
                    agent: AIAgent.Assistant,
                    content: content,
                    reasoning: null);

                var metrics = this.DecodeMetrics(responseJson);

                interaction.Metrics = metrics;

                interactions.Add(interaction);

                // Add an AIInteractionToolCall for each tool call
                if (message["tool_calls"] is JArray tcs && tcs.Count > 0)
                {
                    foreach (JObject tc in tcs)
                    {
                        // OpenAI returns function.arguments as a JSON string; parse it into a JObject
                        JObject argsObj = null;
                        try
                        {
                            var argsToken = tc["function"]?["arguments"];
                            string argsStr = argsToken?.Type == JTokenType.String
                                ? argsToken.ToString()
                                : argsToken?.ToString();
                            if (!string.IsNullOrWhiteSpace(argsStr))
                            {
                                argsObj = JObject.Parse(argsStr);
                            }
                            else
                            {
                                // Prefer an empty object over null to simplify tool-side logic
                                argsObj = new JObject();
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[OpenAI] Warning: failed to parse tool_call arguments: {ex.Message}");

                            // Fallback to empty object to avoid null reference issues in tools
                            argsObj = new JObject();
                        }

                        var toolCall = new AIInteractionToolCall
                        {
                            Id = tc["id"]?.ToString(),
                            Name = tc["function"]?["name"]?.ToString(),
                            Arguments = argsObj,

                            // Chat Completions does not expose reasoning text
                            Reasoning = null,
                        };
                        interactions.Add(toolCall);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OpenAI] ProcessChatCompletionsResponseData error: {ex.Message}");
            }

            return interactions;
        }

        /// <summary>
        /// Processes Responses API output data and converts to interactions.
        /// Handles reasoning items with summary arrays and message items with output_text content.
        /// </summary>
        private List<IAIInteraction> ProcessResponsesApiData(JObject responseJson)
        {
            var interactions = new List<IAIInteraction>();

            try
            {
                Debug.WriteLine("[OpenAI] ProcessResponsesApiData - Processing response");

                var output = responseJson["output"] as JArray;
                if (output == null)
                {
                    Debug.WriteLine("[OpenAI] No output array in response");
                    return interactions;
                }

                var contentParts = new List<string>();
                var reasoningParts = new List<string>();
                var refusalParts = new List<string>();
                var toolCalls = new List<AIInteractionToolCall>();

                foreach (var item in output.OfType<JObject>())
                {
                    var type = item["type"]?.ToString();

                    if (string.Equals(type, "reasoning", StringComparison.OrdinalIgnoreCase))
                    {
                        // Extract reasoning summaries: summary array of {type, text}
                        var summary = item["summary"] as JArray;
                        if (summary != null)
                        {
                            foreach (var summaryItem in summary.OfType<JObject>())
                            {
                                var summaryText = summaryItem["text"]?.ToString();
                                if (!string.IsNullOrEmpty(summaryText))
                                {
                                    reasoningParts.Add(summaryText);
                                }
                            }
                        }

                        // Also support direct reasoning_text or text fields
                        var directText = item["reasoning_text"]?.ToString() ?? item["text"]?.ToString();
                        if (!string.IsNullOrEmpty(directText))
                        {
                            reasoningParts.Add(directText);
                        }
                    }
                    else if (string.Equals(type, "message", StringComparison.OrdinalIgnoreCase))
                    {
                        var role = item["role"]?.ToString();
                        var messageContent = item["content"] as JArray;
                        if (messageContent != null)
                        {
                            foreach (var contentItem in messageContent.OfType<JObject>())
                            {
                                var contentType = contentItem["type"]?.ToString();
                                if (string.Equals(contentType, "output_text", StringComparison.OrdinalIgnoreCase) ||
                                    string.Equals(contentType, "text", StringComparison.OrdinalIgnoreCase))
                                {
                                    var text = contentItem["text"]?.ToString();
                                    var parsed = contentItem["parsed"];
                                    if (string.IsNullOrEmpty(text) && parsed != null && parsed.Type != JTokenType.Null)
                                    {
                                        text = parsed.Type == JTokenType.String
                                            ? parsed.ToString()
                                            : parsed.ToString(Newtonsoft.Json.Formatting.None);
                                    }

                                    if (!string.IsNullOrEmpty(text))
                                    {
                                        contentParts.Add(text);
                                    }
                                }
                                else if (string.Equals(contentType, "refusal", StringComparison.OrdinalIgnoreCase))
                                {
                                    var refusal = contentItem["refusal"]?.ToString();
                                    if (!string.IsNullOrEmpty(refusal))
                                    {
                                        refusalParts.Add(refusal);
                                    }
                                }
                            }
                        }
                        else
                        {
                            // Fallback: direct content string
                            var directContent = item["content"]?.ToString();
                            if (!string.IsNullOrEmpty(directContent))
                            {
                                contentParts.Add(directContent);
                            }
                        }
                    }
                    else if (string.Equals(type, "function_call", StringComparison.OrdinalIgnoreCase))
                    {
                        // Responses API function call format
                        var callId = item["call_id"]?.ToString() ?? item["id"]?.ToString();
                        var name = item["name"]?.ToString();
                        var arguments = item["arguments"]?.ToString() ?? "{}";
                        if (string.IsNullOrWhiteSpace(name))
                        {
                            Debug.WriteLine($"[OpenAI] Warning: function_call item '{callId ?? "(unknown id)"}' has no name.");
                        }

                        JObject argsObj;
                        try
                        {
                            argsObj = JObject.Parse(arguments);
                        }
                        catch
                        {
                            argsObj = new JObject();
                        }

                        toolCalls.Add(new AIInteractionToolCall
                        {
                            Id = callId,
                            Name = name,
                            Arguments = argsObj,
                        });
                    }
                }

                string content = string.Join(string.Empty, contentParts).Trim();
                if (string.IsNullOrWhiteSpace(content) && refusalParts.Count > 0)
                {
                    content = string.Join("\n\n", refusalParts).Trim();
                }

                string reasoning = string.Join("\n\n", reasoningParts).Trim();

                if (!string.IsNullOrEmpty(content) || !string.IsNullOrEmpty(reasoning) || toolCalls.Count > 0)
                {
                    var interaction = new AIInteractionText();
                    interaction.SetResult(
                        agent: AIAgent.Assistant,
                        content: content,
                        reasoning: string.IsNullOrWhiteSpace(reasoning) ? null : reasoning);

                    var metrics = this.DecodeMetrics(responseJson);
                    interaction.Metrics = metrics;

                    interactions.Add(interaction);
                }

                foreach (var toolCall in toolCalls)
                {
                    toolCall.Reasoning = string.IsNullOrWhiteSpace(reasoning) ? null : reasoning;
                    interactions.Add(toolCall);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OpenAI] ProcessResponsesApiData error: {ex.Message}");
            }

            return interactions;
        }

        /// <summary>
        /// Processes image generation response data and converts to interactions.
        /// </summary>
        private List<IAIInteraction> ProcessImageGenerationResponseData(JObject responseJson, AIRequestCall request)
        {
            try
            {
                Debug.WriteLine($"[OpenAI] PostCall - ImageGeneration response parsed successfully");

                var dataArray = responseJson["data"] as JArray;
                if (dataArray == null || dataArray.Count == 0)
                {
                    throw new Exception("No image data returned from OpenAI");
                }

                var imageData = dataArray[0];
                var imageUrl = imageData["url"]?.ToString() ?? string.Empty;
                var revisedPrompt = imageData["revised_prompt"]?.ToString() ?? string.Empty;

                // Get original image request parameters
                var originalImageInteraction = request.Body.Interactions.FirstOrDefault(i => i is AIInteractionImage) as AIInteractionImage;

                // Create result interaction
                var resultInteraction = new AIInteractionImage
                {
                    Agent = AIAgent.Assistant,
                    OriginalPrompt = originalImageInteraction?.OriginalPrompt ?? string.Empty,
                    ImageSize = originalImageInteraction?.ImageSize ?? "1024x1024",
                    ImageQuality = originalImageInteraction?.ImageQuality ?? "standard",
                    ImageStyle = originalImageInteraction?.ImageStyle ?? "vivid",
                };

                // Set the result data from the API response
                resultInteraction.SetResult(
                    imageUrl: imageUrl,
                    revisedPrompt: revisedPrompt);

                Debug.WriteLine($"[OpenAI] Final AIInteractionImage result: URL={imageUrl}, revisedPrompt='{revisedPrompt?.Substring(0, Math.Min(50, revisedPrompt?.Length ?? 0))}...'");

                return new List<IAIInteraction> { resultInteraction };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OpenAI] PostCall - Exception: {ex.Message}");
                throw new Exception($"Error processing OpenAI image response: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Processes audio transcription response data (STT) and converts to interactions.
        /// </summary>
        private List<IAIInteraction> ProcessAudioTranscriptionResponseData(JObject responseJson)
        {
            var interactions = new List<IAIInteraction>();

            try
            {
                Debug.WriteLine($"[OpenAI] ProcessAudioTranscriptionResponseData - Processing response");

                // Extract transcribed text from response
                // OpenAI transcription response: {"text": "transcribed text", "task": "transcribe", ...}
                var text = responseJson["text"]?.ToString() ?? string.Empty;

                if (string.IsNullOrEmpty(text))
                {
                    Debug.WriteLine($"[OpenAI] No transcription text in response: {responseJson}");
                    return interactions;
                }

                var interaction = new AIInteractionText();
                interaction.SetResult(
                    agent: AIAgent.Assistant,
                    content: text);

                interactions.Add(interaction);

                Debug.WriteLine($"[OpenAI] Transcription result: '{text.Substring(0, Math.Min(50, text.Length))}...'");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OpenAI] ProcessAudioTranscriptionResponseData error: {ex.Message}");
            }

            return interactions;
        }

        /// <summary>
        /// Provider-scoped streaming adapter for OpenAI Chat Completions SSE.
        /// </summary>
        private sealed class OpenAIStreamingAdapter : AIProviderStreamingAdapter, IStreamingAdapter
        {
            public OpenAIStreamingAdapter(OpenAIProvider provider)
                : base(provider)
            {
            }

            public async IAsyncEnumerable<AIReturn> StreamAsync(
                AIRequestCall request,
                StreamingOptions options,
                [EnumeratorCancellation] CancellationToken cancellationToken = default)
            {
                if (request == null)
                {
                    yield break;
                }

                // Prepare request via provider (sets endpoint, method, auth kind)
                request = this.Prepare(request);

                // Determine which API we're streaming against
                bool isResponsesApi = request.Endpoint.Contains("/responses", StringComparison.OrdinalIgnoreCase);
                bool isChatCompApi = request.Endpoint.Contains("/chat/completions", StringComparison.OrdinalIgnoreCase);

                // Only chat completions and responses are supported for streaming for now
                if (!isResponsesApi && !isChatCompApi)
                {
                    var unsupported = new AIReturn();
                    unsupported.CreateProviderError("Streaming is only supported for /responses and /chat/completion in this adapter.", request);
                    yield return unsupported;
                    yield break;
                }

                // Build request body and enable streaming
                JObject body;
                try
                {
                    body = JObject.Parse(this.Provider.Encode(request));
                }
                catch
                {
                    body = new JObject();
                }

                body["stream"] = true;

                // stream_options is only supported by Chat Completions
                if (isChatCompApi)
                {
                    // Ask OpenAI to include token usage in the final streaming chunk
                    // See: stream_options.include_usage = true
                    body["stream_options"] = new JObject
                    {
                        ["include_usage"] = true,
                    };
                }

                // Build URL with helper
                var fullUrl = this.BuildFullUrl(request.Endpoint);

                // Configure HTTP client and authentication via helpers
                using var httpClient = this.CreateHttpClient();
                AIReturn? authError = null;
                try
                {
                    var apiKey = ((OpenAIProvider)this.Provider).GetApiKey();
                    this.ApplyAuthentication(httpClient, request.Authentication, apiKey);
                }
                catch (Exception ex)
                {
                    authError = new AIReturn();
                    authError.CreateProviderError(ex.Message, request);
                }

                if (authError != null)
                {
                    yield return authError;
                    yield break;
                }

                using var httpRequest = this.CreateSsePost(fullUrl, body.ToString(), "application/json");

                HttpResponseMessage response;
                AIReturn? sendError = null;
                try
                {
                    response = await this.SendForStreamAsync(httpClient, httpRequest, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    sendError = new AIReturn();
                    sendError.CreateNetworkError(ex.InnerException?.Message ?? ex.Message, request);
                    response = null!;
                }

                if (sendError != null)
                {
                    yield return sendError;
                    yield break;
                }

                if (!response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                    var (message, isNetworkLike) = AIProvider.ClassifyHttpError((int)response.StatusCode, response.ReasonPhrase, content, this.Provider.Name);
                    var err = new AIReturn();
                    if (isNetworkLike)
                    {
                        err.CreateNetworkError(message, request);
                    }
                    else
                    {
                        err.CreateProviderError(message, request);
                    }

                    yield return err;
                    yield break;
                }

                // Streaming state
                var buffer = new StringBuilder();
                var lastEmit = DateTime.UtcNow;
                var firstChunk = true;
                bool hadReasoningOnlySegment = false; // Track if we emitted reasoning-only
                bool responsesTextSegmentClosed = false;
                string finalFinishReason = string.Empty;
                int promptTokens = 0;
                int completionTokens = 0;

                // Provider-local aggregate of assistant text
                var assistantAggregate = new AIInteractionText
                {
                    Agent = AIAgent.Assistant,
                    Content = string.Empty,
                    Reasoning = string.Empty,
                    Metrics = new AIMetrics { Provider = this.Provider.Name, Model = request.Model },
                };

                // Tool call accumulation (index -> partial)
                var toolCalls = new Dictionary<int, (string Id, string Name, StringBuilder Args)>();

                // Responses API tool call accumulation (item_id -> partial)
                var responsesToolCalls = new Dictionary<string, (string Id, string Name, StringBuilder Args)>();
                var responsesRefusalBuffer = new StringBuilder();

                // Helper local function to emit text chunk
                async IAsyncEnumerable<AIReturn> EmitAsync(string text, bool streamingStatus)
                {
                    if (string.IsNullOrEmpty(text)) yield break;

                    // Append to provider-local aggregate
                    assistantAggregate.AppendDelta(contentDelta: text);

                    // Emit a snapshot copy to avoid aliasing across yields
                    var snapshot = new AIInteractionText
                    {
                        Agent = assistantAggregate.Agent,
                        Content = assistantAggregate.Content,
                        Reasoning = assistantAggregate.Reasoning,
                        Metrics = new AIMetrics
                        {
                            Provider = assistantAggregate.Metrics.Provider,
                            Model = assistantAggregate.Metrics.Model,
                            FinishReason = assistantAggregate.Metrics.FinishReason,
                            InputTokensCached = assistantAggregate.Metrics.InputTokensCached,
                            InputTokensPrompt = assistantAggregate.Metrics.InputTokensPrompt,
                            OutputTokensReasoning = assistantAggregate.Metrics.OutputTokensReasoning,
                            OutputTokensGeneration = assistantAggregate.Metrics.OutputTokensGeneration,
                            CompletionTime = assistantAggregate.Metrics.CompletionTime,
                        },
                    };

                    var delta = new AIReturn
                    {
                        Request = request,
                        Status = streamingStatus ? AICallStatus.Streaming : AICallStatus.Processing,
                    };
                    delta.SetBody(new List<IAIInteraction> { snapshot });
                    yield return delta;
                    await Task.Yield();
                }

                // Helper to maybe flush buffer per options and return deltas to emit
                async Task<List<AIReturn>> FlushAsync(bool force)
                {
                    var results = new List<AIReturn>();
                    if (buffer.Length == 0) return results;
                    var elapsed = (DateTime.UtcNow - lastEmit).TotalMilliseconds;
                    if (force || !options.CoalesceTokens || buffer.Length >= options.PreferredChunkSize || elapsed >= options.CoalesceDelayMs)
                    {
                        var text = buffer.ToString();
                        buffer.Clear();
                        lastEmit = DateTime.UtcNow;
                        await foreach (var d in EmitAsync(text, streamingStatus: true).WithCancellation(cancellationToken))
                        {
                            results.Add(d);
                        }
                    }

                    return results;
                }

                // Yield initial processing state (optional)
                {
                    var initial = new AIReturn { Request = request, Status = AICallStatus.Processing };
                    initial.SetBody(new List<IAIInteraction>());
                    yield return initial;
                }

                // Determine idle timeout from request; fall back to shared default if invalid.
                var idleTimeout = TimeSpan.FromSeconds((double)(request.TimeoutSeconds > 0 ? request.TimeoutSeconds : TimeoutDefaults.DefaultTimeoutSeconds));
                await foreach (var data in this.ReadSseDataAsync(
                    response,
                    idleTimeout,
                    null,
                    cancellationToken).WithCancellation(cancellationToken))
                {
                    JObject parsed;
                    try
                    {
                        parsed = JObject.Parse(data);
                    }
                    catch
                    {
                        // Skip malformed chunk
                        continue;
                    }

                    // --- Responses API streaming format ---
                    var eventType = parsed["type"]?.ToString();
                    if (!string.IsNullOrEmpty(eventType))
                    {
                        if (string.Equals(eventType, "response.output_text.delta", StringComparison.OrdinalIgnoreCase))
                        {
                            responsesTextSegmentClosed = false;
                            var textDelta = parsed["delta"]?.ToString();
                            if (!string.IsNullOrEmpty(textDelta))
                            {
                                buffer.Append(textDelta);
                                if (firstChunk)
                                {
                                    firstChunk = false;
                                    var emitted = await FlushAsync(force: true).ConfigureAwait(false);
                                    foreach (var d in emitted)
                                    {
                                        yield return d;
                                    }
                                }
                                else
                                {
                                    var emitted = await FlushAsync(force: false).ConfigureAwait(false);
                                    foreach (var d in emitted)
                                    {
                                        yield return d;
                                    }
                                }
                            }
                        }
                        else if (string.Equals(eventType, "response.content.delta", StringComparison.OrdinalIgnoreCase))
                        {
                            responsesTextSegmentClosed = false;
                            var textDelta = parsed["delta"]?.ToString();
                            if (!string.IsNullOrEmpty(textDelta))
                            {
                                buffer.Append(textDelta);
                                if (firstChunk)
                                {
                                    firstChunk = false;
                                    var emitted = await FlushAsync(force: true).ConfigureAwait(false);
                                    foreach (var d in emitted)
                                    {
                                        yield return d;
                                    }
                                }
                                else
                                {
                                    var emitted = await FlushAsync(force: false).ConfigureAwait(false);
                                    foreach (var d in emitted)
                                    {
                                        yield return d;
                                    }
                                }
                            }
                        }
                        else if (string.Equals(eventType, "response.output_text.done", StringComparison.OrdinalIgnoreCase))
                        {
                            // Explicitly close current text interaction segment (before potential tool calls).
                            var textDone = parsed["text"]?.ToString();
                            if (!string.IsNullOrEmpty(textDone) && buffer.Length == 0)
                            {
                                buffer.Append(textDone);
                            }

                            var emitted = await FlushAsync(force: true).ConfigureAwait(false);
                            foreach (var d in emitted)
                            {
                                yield return d;
                            }

                            if (!responsesTextSegmentClosed &&
                                (!string.IsNullOrEmpty(assistantAggregate.Content) || !string.IsNullOrEmpty(assistantAggregate.Reasoning)))
                            {
                                var completeSnapshot = new AIInteractionText
                                {
                                    Agent = assistantAggregate.Agent,
                                    Content = assistantAggregate.Content,
                                    Reasoning = assistantAggregate.Reasoning,
                                    Time = DateTime.UtcNow,
                                    Metrics = new AIMetrics
                                    {
                                        Provider = assistantAggregate.Metrics.Provider,
                                        Model = assistantAggregate.Metrics.Model,
                                        FinishReason = assistantAggregate.Metrics.FinishReason,
                                        InputTokensCached = assistantAggregate.Metrics.InputTokensCached,
                                        InputTokensPrompt = assistantAggregate.Metrics.InputTokensPrompt,
                                        OutputTokensReasoning = assistantAggregate.Metrics.OutputTokensReasoning,
                                        OutputTokensGeneration = assistantAggregate.Metrics.OutputTokensGeneration,
                                        CompletionTime = assistantAggregate.Metrics.CompletionTime,
                                    },
                                };

                                var completeDelta = new AIReturn
                                {
                                    Request = request,
                                    Status = AICallStatus.Finished,
                                };
                                completeDelta.SetBody(new List<IAIInteraction> { completeSnapshot });
                                yield return completeDelta;
                                responsesTextSegmentClosed = true;
                                await Task.Yield();
                            }
                        }
                        else if (string.Equals(eventType, "response.refusal.delta", StringComparison.OrdinalIgnoreCase))
                        {
                            responsesTextSegmentClosed = false;
                            var refusalDelta = parsed["delta"]?.ToString();
                            if (!string.IsNullOrEmpty(refusalDelta))
                            {
                                responsesRefusalBuffer.Append(refusalDelta);
                                buffer.Append(refusalDelta);

                                if (firstChunk)
                                {
                                    firstChunk = false;
                                    var emitted = await FlushAsync(force: true).ConfigureAwait(false);
                                    foreach (var d in emitted)
                                    {
                                        yield return d;
                                    }
                                }
                                else
                                {
                                    var emitted = await FlushAsync(force: false).ConfigureAwait(false);
                                    foreach (var d in emitted)
                                    {
                                        yield return d;
                                    }
                                }
                            }
                        }
                        else if (string.Equals(eventType, "response.refusal.done", StringComparison.OrdinalIgnoreCase))
                        {
                            var refusalDone = parsed["refusal"]?.ToString();
                            if (string.IsNullOrEmpty(refusalDone) && responsesRefusalBuffer.Length > 0)
                            {
                                refusalDone = responsesRefusalBuffer.ToString();
                            }

                            if (!string.IsNullOrEmpty(refusalDone) && buffer.Length == 0)
                            {
                                buffer.Append(refusalDone);
                            }

                            var emitted = await FlushAsync(force: true).ConfigureAwait(false);
                            foreach (var d in emitted)
                            {
                                yield return d;
                            }

                            if (!responsesTextSegmentClosed &&
                                (!string.IsNullOrEmpty(assistantAggregate.Content) || !string.IsNullOrEmpty(assistantAggregate.Reasoning)))
                            {
                                var completeSnapshot = new AIInteractionText
                                {
                                    Agent = assistantAggregate.Agent,
                                    Content = assistantAggregate.Content,
                                    Reasoning = assistantAggregate.Reasoning,
                                    Time = DateTime.UtcNow,
                                    Metrics = new AIMetrics
                                    {
                                        Provider = assistantAggregate.Metrics.Provider,
                                        Model = assistantAggregate.Metrics.Model,
                                        FinishReason = assistantAggregate.Metrics.FinishReason,
                                        InputTokensCached = assistantAggregate.Metrics.InputTokensCached,
                                        InputTokensPrompt = assistantAggregate.Metrics.InputTokensPrompt,
                                        OutputTokensReasoning = assistantAggregate.Metrics.OutputTokensReasoning,
                                        OutputTokensGeneration = assistantAggregate.Metrics.OutputTokensGeneration,
                                        CompletionTime = assistantAggregate.Metrics.CompletionTime,
                                    },
                                };

                                var completeDelta = new AIReturn
                                {
                                    Request = request,
                                    Status = AICallStatus.Finished,
                                };
                                completeDelta.SetBody(new List<IAIInteraction> { completeSnapshot });
                                yield return completeDelta;
                                responsesTextSegmentClosed = true;
                                await Task.Yield();
                            }
                        }
                        else if (string.Equals(eventType, "response.completed", StringComparison.OrdinalIgnoreCase))
                        {
                            var responseObj = parsed["response"] as JObject;
                            var respUsage = responseObj?["usage"] as JObject;
                            if (respUsage != null)
                            {
                                var pt = respUsage["input_tokens"]?.Value<int?>();
                                var ct = respUsage["output_tokens"]?.Value<int?>();
                                if (pt.HasValue) promptTokens = pt.Value;
                                if (ct.HasValue) completionTokens = ct.Value;
                                assistantAggregate.AppendDelta(metricsDelta: new AIMetrics
                                {
                                    Provider = this.Provider.Name,
                                    Model = request.Model,
                                    InputTokensPrompt = pt ?? 0,
                                    OutputTokensGeneration = ct ?? 0,
                                });
                            }

                            finalFinishReason = "stop";
                        }
                        else if (string.Equals(eventType, "error", StringComparison.OrdinalIgnoreCase))
                        {
                            var err = parsed["error"] as JObject;
                            var errMsg = err?["message"]?.ToString() ?? "Unknown streaming error";
                            var errorReturn = new AIReturn();
                            errorReturn.CreateProviderError(errMsg, request);
                            yield return errorReturn;
                            yield break;
                        }
                        else if (string.Equals(eventType, "response.output_item.added", StringComparison.OrdinalIgnoreCase))
                        {
                            var item = parsed["item"] as JObject;
                            if (string.Equals(item?["type"]?.ToString(), "function_call", StringComparison.OrdinalIgnoreCase))
                            {
                                var itemId = item["id"]?.ToString();
                                var callId = item["call_id"]?.ToString();
                                var name = item["name"]?.ToString();
                                if (string.IsNullOrWhiteSpace(name))
                                {
                                    Debug.WriteLine($"[OpenAI] Warning: streaming function_call item '{callId ?? itemId ?? "(unknown id)"}' has no name.");
                                }

                                if (!string.IsNullOrEmpty(itemId))
                                {
                                    responsesToolCalls[itemId] = (Id: callId ?? itemId, Name: name ?? string.Empty, Args: new StringBuilder());
                                }
                            }
                        }
                        else if (string.Equals(eventType, "response.function_call_arguments.delta", StringComparison.OrdinalIgnoreCase))
                        {
                            var itemId = parsed["item_id"]?.ToString();
                            var funcArgsDelta = parsed["delta"]?.ToString();
                            if (!string.IsNullOrEmpty(itemId) && responsesToolCalls.TryGetValue(itemId, out var entry))
                            {
                                if (!string.IsNullOrEmpty(funcArgsDelta)) entry.Args.Append(funcArgsDelta);
                                responsesToolCalls[itemId] = entry;

                                // Flush text before switching to tool calls
                                var flushed = await FlushAsync(force: true).ConfigureAwait(false);
                                foreach (var d in flushed) yield return d;

                                // Emit current tool call snapshot
                                var interactions = new List<IAIInteraction>();
                                foreach (var kv in responsesToolCalls.OrderBy(k => k.Key))
                                {
                                    var (id, name, argsSb) = kv.Value;
                                    JObject argsObj = null;
                                    var argsStr = argsSb.ToString();
                                    try
                                    {
                                        if (!string.IsNullOrWhiteSpace(argsStr))
                                        {
                                            argsObj = JObject.Parse(argsStr);
                                        }
                                    }
                                    catch
                                    {
                                        // Partial JSON, ignore
                                    }

                                    interactions.Add(new AIInteractionToolCall { Id = id, Name = name, Arguments = argsObj });
                                }

                                var tcDelta = new AIReturn { Request = request, Status = AICallStatus.CallingTools };
                                tcDelta.SetBody(interactions);
                                yield return tcDelta;
                            }
                        }
                        else if (string.Equals(eventType, "response.output_item.done", StringComparison.OrdinalIgnoreCase))
                        {
                            var item = parsed["item"] as JObject;
                            if (string.Equals(item?["type"]?.ToString(), "function_call", StringComparison.OrdinalIgnoreCase))
                            {
                                var itemId = item["id"]?.ToString();
                                var finalArgs = item["arguments"]?.ToString();
                                if (!string.IsNullOrEmpty(itemId) && responsesToolCalls.TryGetValue(itemId, out var entry))
                                {
                                    if (!string.IsNullOrEmpty(finalArgs))
                                    {
                                        entry.Args.Clear();
                                        entry.Args.Append(finalArgs);
                                        responsesToolCalls[itemId] = entry;
                                    }

                                    // Emit final snapshot for all tool calls
                                    var interactions = new List<IAIInteraction>();
                                    foreach (var kv in responsesToolCalls.OrderBy(k => k.Key))
                                    {
                                        var (id, name, argsSb) = kv.Value;
                                        JObject argsObj = null;
                                        var argsStr = argsSb.ToString();
                                        try
                                        {
                                            if (!string.IsNullOrWhiteSpace(argsStr))
                                            {
                                                argsObj = JObject.Parse(argsStr);
                                            }
                                        }
                                        catch
                                        {
                                            // Partial JSON, ignore
                                        }

                                        interactions.Add(new AIInteractionToolCall { Id = id, Name = name, Arguments = argsObj });
                                    }

                                    var tcDelta = new AIReturn { Request = request, Status = AICallStatus.CallingTools };
                                    tcDelta.SetBody(interactions);
                                    yield return tcDelta;
                                }
                            }
                            else if (string.Equals(item?["type"]?.ToString(), "message", StringComparison.OrdinalIgnoreCase))
                            {
                                // Structured outputs may provide parsed content on message done.
                                var messageContent = item?["content"] as JArray;
                                if (messageContent != null)
                                {
                                    foreach (var contentItem in messageContent.OfType<JObject>())
                                    {
                                        var contentType = contentItem["type"]?.ToString();
                                        if (string.Equals(contentType, "output_text", StringComparison.OrdinalIgnoreCase) ||
                                            string.Equals(contentType, "text", StringComparison.OrdinalIgnoreCase))
                                        {
                                            var parsedContent = contentItem["parsed"];
                                            if (parsedContent != null && parsedContent.Type != JTokenType.Null)
                                            {
                                                var structured = parsedContent.Type == JTokenType.String
                                                    ? parsedContent.ToString()
                                                    : parsedContent.ToString(Newtonsoft.Json.Formatting.None);

                                                if (!string.IsNullOrWhiteSpace(structured) && buffer.Length == 0)
                                                {
                                                    buffer.Append(structured);
                                                    var emitted = await FlushAsync(force: true).ConfigureAwait(false);
                                                    foreach (var d in emitted)
                                                    {
                                                        yield return d;
                                                    }
                                                }
                                            }
                                        }
                                        else if (string.Equals(contentType, "refusal", StringComparison.OrdinalIgnoreCase))
                                        {
                                            var refusal = contentItem["refusal"]?.ToString();
                                            if (!string.IsNullOrWhiteSpace(refusal))
                                            {
                                                responsesRefusalBuffer.Append(refusal);
                                                if (buffer.Length == 0)
                                                {
                                                    buffer.Append(refusal);
                                                    var emitted = await FlushAsync(force: true).ConfigureAwait(false);
                                                    foreach (var d in emitted)
                                                    {
                                                        yield return d;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        continue;
                    }

                    var choices = parsed["choices"] as JArray;
                    var choice = choices?.FirstOrDefault() as JObject;
                    var delta = choice?["delta"] as JObject;
                    var finishReason = choice?["finish_reason"]?.ToString();
                    bool hasFinish = !string.IsNullOrEmpty(finishReason);
                    if (hasFinish) finalFinishReason = finishReason;

                    // Usage metrics (only present in final chunk when include_usage=true)
                    var usage = parsed["usage"] as JObject;
                    if (usage != null)
                    {
                        var pt = usage["prompt_tokens"]?.Value<int?>();
                        var ct = usage["completion_tokens"]?.Value<int?>();
                        if (pt.HasValue) promptTokens = pt.Value;
                        if (ct.HasValue) completionTokens = ct.Value;

                        // Extract cached tokens from nested prompt_tokens_details object
                        var promptDetails = usage["prompt_tokens_details"] as JObject;
                        var cachedTokens = promptDetails?["cached_tokens"]?.Value<int>() ?? 0;

                        // Extract reasoning tokens from nested completion_tokens_details object (o1/o3/GPT-5 models)
                        var completionDetails = usage["completion_tokens_details"] as JObject;
                        var rt = completionDetails?["reasoning_tokens"]?.Value<int?>() ?? 0;

                        // Update aggregate metrics
                        assistantAggregate.AppendDelta(metricsDelta: new AIMetrics
                        {
                            Provider = this.Provider.Name,
                            Model = request.Model,
                            InputTokensCached = cachedTokens,
                            InputTokensPrompt = (pt ?? 0) - cachedTokens,
                            OutputTokensGeneration = ct ?? 0,
                            OutputTokensReasoning = rt,
                        });
                    }

                    // Content streaming - handle both plain strings and structured content arrays
                    var contentToken = delta?["content"];
                    if (contentToken != null)
                    {
                        bool hasReasoningUpdate = false;
                        bool hasContentUpdate = false;

                        // Check if content is a structured array (o-series models with reasoning)
                        if (contentToken is JArray contentArray)
                        {
                            foreach (var part in contentArray.OfType<JObject>())
                            {
                                var type = part["type"]?.ToString();
                                if (string.Equals(type, "reasoning", StringComparison.OrdinalIgnoreCase) ||
                                    string.Equals(type, "thinking", StringComparison.OrdinalIgnoreCase))
                                {
                                    // Extract reasoning/thinking content
                                    var reasoningText = part["text"]?.ToString() ?? part["content"]?.ToString();
                                    if (!string.IsNullOrEmpty(reasoningText))
                                    {
                                        assistantAggregate.AppendDelta(reasoningDelta: reasoningText);
                                        hasReasoningUpdate = true;
                                        Debug.WriteLine($"[OpenAI] Streaming reasoning chunk: {reasoningText.Substring(0, Math.Min(50, reasoningText.Length))}...");
                                    }
                                }
                                else if (string.Equals(type, "text", StringComparison.OrdinalIgnoreCase) ||
                                         string.Equals(type, "output_text", StringComparison.OrdinalIgnoreCase))
                                {
                                    // Extract regular text content
                                    var textVal = part["text"]?.ToString() ?? part["content"]?.ToString();
                                    if (!string.IsNullOrEmpty(textVal))
                                    {
                                        buffer.Append(textVal);
                                        hasContentUpdate = true;
                                    }
                                }
                            }
                        }
                        else
                        {
                            // Plain string content (non-reasoning models)
                            var contentDelta = contentToken.ToString();
                            if (!string.IsNullOrEmpty(contentDelta))
                            {
                                buffer.Append(contentDelta);
                                hasContentUpdate = true;
                            }
                        }

                        // Flush if we have content OR emit reasoning-only snapshot
                        if (hasContentUpdate)
                        {
                            // If we had a reasoning-only segment, complete it first to trigger segmentation
                            if (hadReasoningOnlySegment)
                            {
                                // Emit completed reasoning-only interaction to set boundary flag
                                var reasoningComplete = new AIInteractionText
                                {
                                    Agent = assistantAggregate.Agent,
                                    Content = string.Empty,
                                    Reasoning = assistantAggregate.Reasoning,
                                    Time = DateTime.UtcNow,
                                    Metrics = new AIMetrics
                                    {
                                        Provider = assistantAggregate.Metrics.Provider,
                                        Model = assistantAggregate.Metrics.Model,
                                        OutputTokensReasoning = assistantAggregate.Metrics.OutputTokensReasoning,
                                    },
                                };

                                var completeDelta = new AIReturn
                                {
                                    Request = request,
                                    Status = AICallStatus.Finished,
                                };
                                completeDelta.SetBody(new List<IAIInteraction> { reasoningComplete });
                                yield return completeDelta;
                                await Task.Yield();

                                hadReasoningOnlySegment = false;
                            }

                            if (firstChunk)
                            {
                                firstChunk = false;

                                // Force immediate first emit for snappy UX
                                var emitted = await FlushAsync(force: true).ConfigureAwait(false);
                                foreach (var d in emitted)
                                {
                                    yield return d;
                                }
                            }
                            else
                            {
                                var emitted = await FlushAsync(force: false).ConfigureAwait(false);
                                foreach (var d in emitted)
                                {
                                    yield return d;
                                }
                            }
                        }
                        else if (hasReasoningUpdate)
                        {
                            // Emit reasoning-only snapshot (no text content yet)
                            var snapshot = new AIInteractionText
                            {
                                Agent = assistantAggregate.Agent,
                                Content = assistantAggregate.Content,
                                Reasoning = assistantAggregate.Reasoning,
                                Metrics = new AIMetrics
                                {
                                    Provider = assistantAggregate.Metrics.Provider,
                                    Model = assistantAggregate.Metrics.Model,
                                    FinishReason = assistantAggregate.Metrics.FinishReason,
                                    InputTokensCached = assistantAggregate.Metrics.InputTokensCached,
                                    InputTokensPrompt = assistantAggregate.Metrics.InputTokensPrompt,
                                    OutputTokensReasoning = assistantAggregate.Metrics.OutputTokensReasoning,
                                    OutputTokensGeneration = assistantAggregate.Metrics.OutputTokensGeneration,
                                    CompletionTime = assistantAggregate.Metrics.CompletionTime,
                                },
                            };

                            var reasoningDelta = new AIReturn
                            {
                                Request = request,
                                Status = AICallStatus.Streaming,
                            };
                            reasoningDelta.SetBody(new List<IAIInteraction> { snapshot });
                            yield return reasoningDelta;
                            await Task.Yield();

                            hadReasoningOnlySegment = true; // Mark that we have a reasoning segment
                        }
                    }

                    // Tool calls streaming
                    var tcArray = delta?["tool_calls"] as JArray;
                    if (tcArray != null)
                    {
                        foreach (var t in tcArray.OfType<JObject>())
                        {
                            var idx = t["index"]?.Value<int?>() ?? 0;
                            if (!toolCalls.TryGetValue(idx, out var entry))
                            {
                                entry = (Id: string.Empty, Name: string.Empty, Args: new StringBuilder());
                                toolCalls[idx] = entry;
                            }

                            // id
                            var idVal = t["id"]?.ToString();
                            if (!string.IsNullOrEmpty(idVal)) entry.Id = idVal;

                            var func = t["function"] as JObject;
                            if (func != null)
                            {
                                var name = func["name"]?.ToString();
                                if (!string.IsNullOrEmpty(name)) entry.Name = name;
                                var args = func["arguments"]?.ToString();
                                if (!string.IsNullOrEmpty(args)) entry.Args.Append(args);
                            }

                            toolCalls[idx] = entry; // update
                        }

                        // If we know we're heading to tool calls, flush text first
                        var emittedTc = await FlushAsync(force: true).ConfigureAwait(false);
                        foreach (var d in emittedTc)
                        {
                            yield return d;
                        }

                        // Emit current tool call snapshot with CallingTools status
                        var interactions = new List<IAIInteraction>();
                        foreach (var kv in toolCalls.OrderBy(k => k.Key))
                        {
                            var (id, name, argsSb) = kv.Value;
                            JObject argsObj = null;
                            var argsStr = argsSb.ToString();
                            try
                            {
                                if (!string.IsNullOrWhiteSpace(argsStr))
                                {
                                    argsObj = JObject.Parse(argsStr);
                                }
                            }
                            catch
                            {
                                // Partial JSON, ignore
                            }

                            interactions.Add(new AIInteractionToolCall { Id = id, Name = name, Arguments = argsObj });
                        }

                        var tcDelta = new AIReturn { Request = request, Status = AICallStatus.CallingTools };
                        tcDelta.SetBody(interactions);
                        yield return tcDelta;
                    }
                }

                // Final flush
                var finalEmitted = await FlushAsync(force: true).ConfigureAwait(false);
                foreach (var d in finalEmitted)
                {
                    yield return d;
                }

                // Emit final Finished marker with the complete assistant interaction
                var final = new AIReturn
                {
                    Request = request,
                    Status = AICallStatus.Finished,
                };
                var finalMetrics = new AIMetrics
                {
                    Provider = this.Provider.Name,
                    Model = request.Model,
                    FinishReason = string.IsNullOrEmpty(finalFinishReason) ? "stop" : finalFinishReason,
                    InputTokensPrompt = promptTokens,
                    OutputTokensGeneration = completionTokens,
                };

                // Align aggregate metrics finish reason as well
                assistantAggregate.AppendDelta(metricsDelta: new AIMetrics { FinishReason = finalMetrics.FinishReason });

                // Build final body with text and tool calls
                var finalBuilder = AIBodyBuilder.Create();

                // Add text interaction if present
                if (!string.IsNullOrEmpty(assistantAggregate.Content) || !string.IsNullOrEmpty(assistantAggregate.Reasoning))
                {
                    var finalSnapshot = new AIInteractionText
                    {
                        Agent = assistantAggregate.Agent,
                        Content = assistantAggregate.Content,
                        Reasoning = assistantAggregate.Reasoning,
                        Metrics = new AIMetrics
                        {
                            Provider = assistantAggregate.Metrics.Provider,
                            Model = assistantAggregate.Metrics.Model,
                            FinishReason = assistantAggregate.Metrics.FinishReason,
                            InputTokensCached = assistantAggregate.Metrics.InputTokensCached,
                            InputTokensPrompt = assistantAggregate.Metrics.InputTokensPrompt,
                            OutputTokensReasoning = assistantAggregate.Metrics.OutputTokensReasoning,
                            OutputTokensGeneration = assistantAggregate.Metrics.OutputTokensGeneration,
                            CompletionTime = assistantAggregate.Metrics.CompletionTime,
                        },
                    };
                    finalBuilder.Add(finalSnapshot, markAsNew: false);
                }

                // Add tool calls if present (already marked as NOT new since they were yielded)
                foreach (var kv in toolCalls.OrderBy(k => k.Key))
                {
                    var (id, name, argsSb) = kv.Value;
                    JObject argsObj = null;
                    var argsStr = argsSb.ToString();
                    try
                    {
                        if (!string.IsNullOrWhiteSpace(argsStr))
                        {
                            argsObj = JObject.Parse(argsStr);
                        }
                    }
                    catch
                    {
                        // Partial JSON
                    }

                    finalBuilder.Add(new AIInteractionToolCall { Id = id, Name = name, Arguments = argsObj }, markAsNew: false);
                }

                // Add Responses API tool calls if present
                foreach (var kv in responsesToolCalls.OrderBy(k => k.Key))
                {
                    var (id, name, argsSb) = kv.Value;
                    JObject argsObj = null;
                    var argsStr = argsSb.ToString();
                    try
                    {
                        if (!string.IsNullOrWhiteSpace(argsStr))
                        {
                            argsObj = JObject.Parse(argsStr);
                        }
                    }
                    catch
                    {
                        // Partial JSON
                    }

                    finalBuilder.Add(new AIInteractionToolCall { Id = id, Name = name, Arguments = argsObj }, markAsNew: false);
                }

                final.SetBody(finalBuilder.Build());
                yield return final;
            }
        }

        #region IAIBatchProvider

        /// <inheritdoc/>
        public async Task<AIBatchSubmission> SubmitBatchAsync(IReadOnlyList<(string CustomId, AIRequestCall Request)> items, CancellationToken cancellationToken = default)
        {
            if (items == null || items.Count == 0) throw new ArgumentException("At least one item is required", nameof(items));

            // Build multi-line JSONL: one line per request
            var jsonlBuilder = new System.Text.StringBuilder();
            string firstEncodedBody = null;
            var customIds = new List<string>();

            string batchEndpoint = null;

            foreach (var (customId, request) in items)
            {
                var preparedRequest = this.PreCall(request);
                var encodedBody = this.Encode(preparedRequest);
                if (firstEncodedBody == null) firstEncodedBody = encodedBody;
                var bodyObj = JObject.Parse(encodedBody);

                // Derive the endpoint from the prepared request (e.g. /responses or /chat/completions).
                // OpenAI batch requires all lines to target the same endpoint.
                var endpoint = preparedRequest.Endpoint;
                if (!endpoint.StartsWith("/v1/", StringComparison.OrdinalIgnoreCase))
                {
                    endpoint = "/v1" + endpoint;
                }

                if (batchEndpoint == null)
                {
                    batchEndpoint = endpoint;
                }
                else if (!string.Equals(batchEndpoint, endpoint, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"[OpenAI] Batch items must all use the same endpoint. Expected '{batchEndpoint}', found '{endpoint}'.");
                }

                var batchLine = new JObject
                {
                    ["custom_id"] = customId,
                    ["method"] = "POST",
                    ["url"] = endpoint,
                    ["body"] = bodyObj,
                };
                jsonlBuilder.AppendLine(batchLine.ToString(Newtonsoft.Json.Formatting.None));
                customIds.Add(customId);
            }

            var jsonlBytes = Encoding.UTF8.GetBytes(jsonlBuilder.ToString());

            var apiKey = this.GetApiKey();
            using var client = this.CreateBatchHttpClient();
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

            // Step 1: Upload JSONL file to /v1/files
            using var multipart = new MultipartFormDataContent();
            multipart.Add(new StringContent("batch"), "purpose");
            var fileByteContent = new ByteArrayContent(jsonlBytes);
            fileByteContent.Headers.ContentType =
                new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
            multipart.Add(fileByteContent, "file", "batch_input.jsonl");

            var uploadUrl = this.BuildFullUrl("/files");
            var uploadResponse = await client.PostAsync(uploadUrl, multipart, cancellationToken).ConfigureAwait(false);
            var uploadContent = await uploadResponse.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!uploadResponse.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"[OpenAI] Batch file upload failed ({(int)uploadResponse.StatusCode}): {uploadContent}");
            }

            var uploadResult = JObject.Parse(uploadContent);
            var fileId = uploadResult["id"]?.ToString()
                ?? throw new InvalidOperationException("[OpenAI] Batch file upload response missing 'id'");

            // Step 2: Create batch job
            var batchBody = new JObject
            {
                ["input_file_id"] = fileId,
                ["endpoint"] = batchEndpoint ?? "/v1/responses",
                ["completion_window"] = "24h",
            };

            var batchUrl = this.BuildFullUrl("/batches");
            var batchResponse = await client.PostAsync(
                batchUrl,
                new StringContent(batchBody.ToString(), Encoding.UTF8, "application/json"),
                cancellationToken).ConfigureAwait(false);
            var batchContent = await batchResponse.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!batchResponse.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"[OpenAI] Batch creation failed ({(int)batchResponse.StatusCode}): {batchContent}");
            }

            var batchResult = JObject.Parse(batchContent);
            var batchId = batchResult["id"]?.ToString()
                ?? throw new InvalidOperationException("[OpenAI] Batch creation response missing 'id'");

            Debug.WriteLine($"[OpenAI] Batch submitted: batchId={batchId}, count={items.Count}");
            return new AIBatchSubmission(batchId, this.Name, firstEncodedBody, (IReadOnlyList<string>)customIds.AsReadOnly());
        }

        /// <inheritdoc/>
        public async Task<AIBatchStatus> GetBatchStatusAsync(AIBatchSubmission submission, CancellationToken cancellationToken = default)
        {
            if (submission == null) throw new ArgumentNullException(nameof(submission));

            var apiKey = this.GetApiKey();
            using var client = this.CreateBatchHttpClient();
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

            var statusUrl = this.BuildFullUrl($"/batches/{submission.BatchId}");
            var response = await client.GetAsync(statusUrl, cancellationToken).ConfigureAwait(false);
            var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return new AIBatchStatus(submission.BatchId, AIBatchState.Failed, $"HTTP {(int)response.StatusCode}: {content}");
            }

            var json = JObject.Parse(content);
            var status = json["status"]?.ToString() ?? string.Empty;
            Debug.WriteLine($"[OpenAI] Batch status check: id={submission.BatchId}, status={status}");

            switch (status.ToLowerInvariant())
            {
                case "validating":
                case "in_progress":
                case "finalizing":
                {
                    var requestCounts = json["request_counts"] as JObject;
                    int? completedCount = requestCounts?["completed"]?.Value<int>();
                    return new AIBatchStatus(submission.BatchId, AIBatchState.InProgress, completedCount: completedCount);
                }

                case "completed":
                {
                    var outputFileId = json["output_file_id"]?.ToString();
                    var errorFileId = json["error_file_id"]?.ToString();
                    if (string.IsNullOrEmpty(outputFileId) && string.IsNullOrEmpty(errorFileId))
                    {
                        return new AIBatchStatus(submission.BatchId, AIBatchState.Failed, "Batch completed but neither output_file_id nor error_file_id is present");
                    }

                    // Download both output and error files via the public interface method;
                    // parsing/merging is delegated to ParseBatchResultsFiles.
                    IReadOnlyList<string> files;
                    try
                    {
                        files = await this.DownloadBatchResultsAsync(submission, cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        return new AIBatchStatus(submission.BatchId, AIBatchState.Failed, $"Failed to download batch result files: {ex.Message}");
                    }

                    var parsed = this.ParseBatchResultsFiles(files, submission.BatchId);
                    if ((parsed.Results?.Count ?? 0) == 0 && (parsed.Messages?.Count ?? 0) == 0)
                    {
                        return new AIBatchStatus(submission.BatchId, AIBatchState.Failed, "No results found in batch output/error files");
                    }

                    return parsed;
                }

                case "failed":
                    return new AIBatchStatus(submission.BatchId, AIBatchState.Failed, json["errors"]?.ToString());

                case "expired":
                    return new AIBatchStatus(submission.BatchId, AIBatchState.Expired);

                case "cancelling":
                case "cancelled":
                    return new AIBatchStatus(submission.BatchId, AIBatchState.Cancelled);

                default:
                    return new AIBatchStatus(submission.BatchId, AIBatchState.Submitted);
            }
        }

        /// <inheritdoc/>
        public async Task CancelBatchAsync(AIBatchSubmission submission, CancellationToken cancellationToken = default)
        {
            if (submission == null) throw new ArgumentNullException(nameof(submission));

            var apiKey = this.GetApiKey();
            using var client = this.CreateBatchHttpClient();
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

            var cancelUrl = this.BuildFullUrl($"/batches/{submission.BatchId}/cancel");
            var response = await client.PostAsync(cancelUrl, null, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                Debug.WriteLine($"[OpenAI] Batch cancel failed ({(int)response.StatusCode}): {content}");
            }
            else
            {
                Debug.WriteLine($"[OpenAI] Batch {submission.BatchId} cancelled successfully");
            }
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<string>> DownloadBatchResultsAsync(AIBatchSubmission submission, CancellationToken cancellationToken = default)
        {
            if (submission == null) throw new ArgumentNullException(nameof(submission));

            var apiKey = this.GetApiKey();
            using var client = this.CreateBatchHttpClient();
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

            // Re-fetch batch status to obtain output_file_id and error_file_id.
            var statusUrl = this.BuildFullUrl($"/batches/{submission.BatchId}");
            var statusResponse = await client.GetAsync(statusUrl, cancellationToken).ConfigureAwait(false);
            var statusContent = await statusResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (!statusResponse.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"HTTP {(int)statusResponse.StatusCode}: {statusContent}");
            }

            var statusJson = JObject.Parse(statusContent);
            var outputFileId = statusJson["output_file_id"]?.ToString();
            var errorFileId = statusJson["error_file_id"]?.ToString();

            var files = new List<string>(2);

            // Canonical order: success output first, error file last.
            foreach (var fileId in new[] { outputFileId, errorFileId })
            {
                if (string.IsNullOrEmpty(fileId))
                {
                    continue;
                }

                var fileUrl = this.BuildFullUrl($"/files/{fileId}/content");
                var fileResponse = await client.GetAsync(fileUrl, cancellationToken).ConfigureAwait(false);
                var fileContent = await fileResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!fileResponse.IsSuccessStatusCode)
                {
                    throw new InvalidOperationException($"Failed to download file '{fileId}' ({(int)fileResponse.StatusCode}): {fileContent}");
                }

                if (!string.IsNullOrWhiteSpace(fileContent))
                {
                    files.Add(fileContent);
                }
            }

            return files;
        }

        /// <inheritdoc/>
        public AIBatchStatus ParseBatchResultsFiles(IReadOnlyList<string> fileContents, string batchId = null)
        {
            if (fileContents == null || fileContents.Count == 0)
            {
                return new AIBatchStatus(batchId, AIBatchState.Failed, "No file contents provided");
            }

            var merged = new Dictionary<string, JObject>();
            var messages = new List<SHRuntimeMessage>();

            foreach (var content in fileContents)
            {
                AIBatchStatusMerge.MergeInto(this.ParseSingleBatchResultFile(content, batchId), merged, messages);
            }

            return new AIBatchStatus(
                batchId,
                new System.Collections.ObjectModel.ReadOnlyDictionary<string, JObject>(merged),
                messages.Count > 0 ? messages.AsReadOnly() : null);
        }

        /// <summary>
        /// Parses a single OpenAI batch result JSONL file. Each line has the shape
        /// <c>{"custom_id":"sh-...", "response":{"status_code":N, "body":{...}}, "error":...}</c>.
        /// Success bodies (2xx) populate <see cref="AIBatchStatus.Results"/>; non-2xx lines emit
        /// provider-origin error <see cref="SHRuntimeMessage"/>s. Finish reasons (e.g., "length")
        /// are extracted from successful responses and surfaced as warnings.
        /// </summary>
        private AIBatchStatus ParseSingleBatchResultFile(string content, string batchId)
        {
            var results = new Dictionary<string, JObject>();
            var messages = new List<SHRuntimeMessage>();

            foreach (var resultLine in JsonFormatHelper.ParseJsonLines(content))
            {
                var lineCustomId = resultLine["custom_id"]?.ToString();
                if (string.IsNullOrEmpty(lineCustomId))
                {
                    continue;
                }

                var responseObj = resultLine["response"] as JObject;
                var statusCode = responseObj?["status_code"]?.Value<int>() ?? 0;
                var resultBody = responseObj?["body"] as JObject;

                if (statusCode >= 200 && statusCode < 300 && resultBody != null)
                {
                    results[lineCustomId] = resultBody;

                    // Extract finish/status warnings for both Chat Completions and Responses API
                    string finishReason = null;

                    // Chat Completions format: choices[0].finish_reason
                    var choices = resultBody["choices"] as JArray;
                    var firstChoice = choices?.FirstOrDefault() as JObject;
                    finishReason = firstChoice?["finish_reason"]?.ToString();

                    // Responses API format: status field (e.g. "completed", "incomplete", "failed")
                    if (string.IsNullOrEmpty(finishReason))
                    {
                        var status = resultBody["status"]?.ToString();
                        if (!string.IsNullOrEmpty(status) && status != "completed")
                        {
                            finishReason = status;
                        }
                    }

                    if (!string.IsNullOrEmpty(finishReason) && finishReason != "stop")
                    {
                        messages.Add(new SHRuntimeMessage(
                            SHRuntimeMessageSeverity.Warning,
                            SHRuntimeMessageOrigin.Provider,
                            SHMessageCode.BatchItemFinishReason,
                            $"Batch item {lineCustomId}: completed with finish_reason='{finishReason}'"));
                    }
                }
                else
                {
                    var errorMsg = resultBody?["error"]?["message"]?.ToString()
                        ?? resultLine["error"]?["message"]?.ToString()
                        ?? $"HTTP {statusCode}";
                    messages.Add(new SHRuntimeMessage(
                        SHRuntimeMessageSeverity.Error,
                        SHRuntimeMessageOrigin.Provider,
                        SHMessageCode.BatchItemError,
                        $"Batch item {lineCustomId}: {errorMsg}"));
                }
            }

            return new AIBatchStatus(batchId, results, messages);
        }

        #endregion

        /// <inheritdoc/>
        public override IEnumerable<AIExtraDescriptor> GetExtraDescriptors()
        {
            return new[]
            {
                // OpenAI-specific parameters
                new AIExtraDescriptor(
                    "reasoning_effort",
                    "Reasoning Effort",
                    "Reasoning token budget for o-series and gpt-5 models. 'low' is fastest, 'high' is most thorough.",
                    typeof(string),
                    "medium",
                    new[] { "low", "medium", "high" }),

                // General parameters (shared across providers)
                new AIExtraDescriptor(
                    "top_p",
                    "Top P",
                    "Nucleus sampling parameter (0.0–1.0). Lower values make output more focused; higher values more diverse. Leave empty to use default.",
                    typeof(double),
                    null),
                new AIExtraDescriptor(
                    "frequency_penalty",
                    "Frequency Penalty",
                    "Penalizes frequent tokens (-2.0 to 2.0). Positive values reduce repetition.",
                    typeof(double),
                    null),
                new AIExtraDescriptor(
                    "presence_penalty",
                    "Presence Penalty",
                    "Penalizes tokens already in the text (-2.0 to 2.0). Positive values encourage new topics.",
                    typeof(double),
                    null),
                new AIExtraDescriptor(
                    "logprobs",
                    "Log Probabilities",
                    "Return log probabilities of output tokens. Useful for analyzing model confidence.",
                    typeof(bool),
                    null),
                new AIExtraDescriptor(
                    "top_logprobs",
                    "Top Logprobs",
                    "Number of most likely tokens to return log probabilities for (0–20). Requires logprobs=true.",
                    typeof(int),
                    null),

                // OpenAI prompt caching parameters
                new AIExtraDescriptor(
                    "prompt_cache_retention",
                    "Cache Retention",
                    "Cache retention policy for repeated prompt prefixes. 'in_memory' (5-10 min, default) or '24h' (extended, for gpt-4.1+). Recommended for batch jobs.",
                    typeof(string),
                    null,
                    new[] { "in_memory", "24h" }),
                new AIExtraDescriptor(
                    "prompt_cache_key",
                    "Cache Key",
                    "Optional string to improve cache routing when many requests share a long common prefix. Combine with '24h' retention for batch jobs.",
                    typeof(string),
                    null),
            };
        }
    }
}
