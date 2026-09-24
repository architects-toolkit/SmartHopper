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
using System.Linq;
using Newtonsoft.Json.Linq;
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
