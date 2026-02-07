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
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AICall.Core.Requests;
using SmartHopper.Infrastructure.AICall.Core.Returns;
using SmartHopper.Infrastructure.AICall.JsonSchemas;
using SmartHopper.Infrastructure.AICall.Metrics;
using SmartHopper.Infrastructure.AIModels;
using SmartHopper.Infrastructure.AIProviders;
using SmartHopper.Infrastructure.Streaming;
using SmartHopper.Infrastructure.Utils;

namespace SmartHopper.Providers.Anthropic
{
    /// <summary>
    /// Provider implementation for Anthropic's Messages API, including schema wrapping,
    /// tool call encoding, and streaming support via SSE.
    /// </summary>
    public sealed class AnthropicProvider : AIProvider<AnthropicProvider>
    {
        private AnthropicProvider()
        {
            this.Models = new AnthropicProviderModels(this);

            // No provider-specific schema adapter required at the moment.
        }

        /// <summary>
        /// Gets the name of the provider.
        /// </summary>
        public override string Name => "Anthropic";

        /// <summary>
        /// Gets the default server URL for the provider.
        /// </summary>
        public override Uri DefaultServerUrl => new Uri("https://api.anthropic.com/v1");

        /// <summary>
        /// Gets a value indicating whether this provider is enabled.
        /// </summary>
        public override bool IsEnabled => true;


        /// <summary>
        /// Helper to retrieve the configured API key for this provider.
        /// Exposed to nested streaming adapter to avoid protected access issues.
        /// </summary>
        /// <returns>The API key string stored in settings; may be empty if not configured.</returns>
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
                try
                {
                    var bytes = Properties.Resources.anthropic_icon;
                    if (bytes != null && bytes.Length > 0)
                    {
                        using (var ms = new MemoryStream(bytes))
                        using (var img = Image.FromStream(ms))
                        {
                            // Create a decoupled Bitmap so the MemoryStream can be disposed safely
                            return new Bitmap(img);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Anthropic] Icon load error: {ex.Message}");
                }

                return new Bitmap(1, 1);
            }
        }

        /// <summary>
        /// Returns a streaming adapter for Anthropic that yields incremental AIReturn deltas.
        /// <inheritdoc/>
        protected override IStreamingAdapter CreateStreamingAdapter()
        {
            return new AnthropicStreamingAdapter(this);
        }

        /// <inheritdoc/>
        public override AIRequestCall PreCall(AIRequestCall request)
        {
            request = base.PreCall(request);

            switch (request.Endpoint)
            {
                case "/models":
                    request.HttpMethod = "GET";
                    request.RequestKind = AIRequestKind.Backoffice;
                    break;
                default:
                    request.HttpMethod = "POST";
                    request.Endpoint = "/messages"; // Anthropic Messages API
                    break;
            }

            request.ContentType = "application/json";
            request.Authentication = "x-api-key";

            if (!request.Headers.ContainsKey("anthropic-version"))
            {
                // Default version if not provided by caller
                request.Headers["anthropic-version"] = "2023-06-01";
            }

            this.ApplyStructuredOutputsBetaHeader(request);

            return request;
        }

        // No CustomizeHttpClientHeaders override; headers are provided in request.Headers.

        /// <inheritdoc/>
        public override string Encode(IAIInteraction interaction)
        {
            try
            {
                // Use new helper methods to create a single message
                string role = this.GetRoleForAgent(interaction?.Agent ?? AIAgent.User);
                if (string.IsNullOrEmpty(role))
                {
                    return string.Empty;
                }

                var contentBlock = this.CreateContentBlock(interaction);
                if (contentBlock == null)
                {
                    return string.Empty;
                }

                var message = new JObject
                {
                    ["role"] = role,
                    ["content"] = new JArray { contentBlock }
                };

                return message.ToString();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Anthropic] Encode(IAIInteraction) error: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// Sorts content blocks according to Anthropic API requirements.
        /// Text blocks must come before tool_use blocks in the same message.
        /// </summary>
        private JArray SortContentBlocks(JArray contentBlocks)
        {
            if (contentBlocks == null || contentBlocks.Count <= 1)
            {
                return contentBlocks;
            }

            var sorted = new JArray();
            var textBlocks = new List<JToken>();
            var toolUseBlocks = new List<JToken>();
            var otherBlocks = new List<JToken>();

            foreach (var block in contentBlocks)
            {
                if (block is JObject obj)
                {
                    var type = obj["type"]?.ToString();
                    if (string.Equals(type, "text", StringComparison.OrdinalIgnoreCase))
                    {
                        textBlocks.Add(block);
                    }
                    else if (string.Equals(type, "tool_use", StringComparison.OrdinalIgnoreCase))
                    {
                        toolUseBlocks.Add(block);
                    }
                    else
                    {
                        otherBlocks.Add(block);
                    }
                }
                else
                {
                    otherBlocks.Add(block);
                }
            }

            // Add in order: text, tool_use, others
            foreach (var block in textBlocks) sorted.Add(block);
            foreach (var block in toolUseBlocks) sorted.Add(block);
            foreach (var block in otherBlocks) sorted.Add(block);

#if DEBUG
            if (textBlocks.Count > 0 && toolUseBlocks.Count > 0)
            {
                Debug.WriteLine($"[Anthropic] SortContentBlocks: Reordered {textBlocks.Count} text + {toolUseBlocks.Count} tool_use blocks");
            }

#endif

            return sorted;
        }

        /// <summary>
        /// Maps an agent to the corresponding Anthropic role.
        /// Returns null for agents that should not be sent as messages.
        /// </summary>
        private string GetRoleForAgent(AIAgent agent)
        {
            switch (agent)
            {
                case AIAgent.System:
                case AIAgent.Context:
                    return null; // System messages go in top-level "system" field
                case AIAgent.User:
                    return "user";
                case AIAgent.Assistant:
                case AIAgent.ToolCall:
                    return "assistant";
                case AIAgent.ToolResult:
                    return "user"; // Tool results are sent as user messages
                default:
                    return null;
            }
        }

        /// <summary>
        /// Creates a content block (JObject) for an interaction.
        /// Returns null if the interaction should not be sent or has no content.
        /// </summary>
        private JObject CreateContentBlock(IAIInteraction interaction)
        {
            if (interaction == null)
            {
                return null;
            }

            // UI-only diagnostics must not be sent to providers
            if (interaction is AIInteractionError)
            {
                return null;
            }

            // Handle different interaction types
            if (interaction is AIInteractionText textInteraction)
            {
                var text = textInteraction.Content ?? string.Empty;
                if (string.IsNullOrEmpty(text))
                {
                    return null;
                }

                return new JObject
                {
                    ["type"] = "text",
                    ["text"] = text,
                };
            }
            else if (interaction is AIInteractionToolResult toolResultInteraction)
            {
                var resultText = toolResultInteraction.Result?.ToString(Formatting.None) ?? string.Empty;
                return new JObject
                {
                    ["type"] = "tool_result",
                    ["tool_use_id"] = toolResultInteraction.Id ?? string.Empty,
                    ["content"] = resultText,
                };
            }
            else if (interaction is AIInteractionToolCall toolCallInteraction)
            {
                return new JObject
                {
                    ["type"] = "tool_use",
                    ["id"] = toolCallInteraction.Id ?? string.Empty,
                    ["name"] = toolCallInteraction.Name ?? string.Empty,
                    ["input"] = toolCallInteraction.Arguments ?? new JObject(),
                };
            }
            else if (interaction is AIInteractionImage imageInteraction)
            {
                // Anthropic does not support image generation; fallback to prompt as text
                var prompt = imageInteraction.OriginalPrompt ?? string.Empty;
                if (string.IsNullOrEmpty(prompt))
                {
                    return null;
                }

                return new JObject
                {
                    ["type"] = "text",
                    ["text"] = prompt,
                };
            }

            // Unknown interaction type - skip
            return null;
        }

        /// <summary>
        /// Extracts the textual representation of a tool_result content block, preserving non-text JSON as stringified JSON.
        /// </summary>
        private static string ExtractToolResultText(JToken resultContent)
        {
            if (resultContent == null) return string.Empty;
            if (resultContent is JArray rcArr)
            {
                var parts = new List<string>();
                foreach (var item in rcArr)
                {
                    if (item is JObject o)
                    {
                        var ttype = o["type"]?.ToString();
                        if (string.Equals(ttype, "text", StringComparison.OrdinalIgnoreCase))
                        {
                            var t = o["text"]?.ToString();
                            if (!string.IsNullOrEmpty(t)) parts.Add(t);
                        }
                        else
                        {
                            parts.Add(o.ToString(Formatting.None));
                        }
                    }
                    else
                    {
                        parts.Add(item?.ToString(Formatting.None) ?? string.Empty);
                    }
                }

                return string.Join(string.Empty, parts);
            }

            // Could be a plain string or structured JSON
            return resultContent.Type == JTokenType.Object || resultContent.Type == JTokenType.Array
                ? resultContent.ToString(Formatting.None)
                : resultContent.ToString();
        }

        /// <inheritdoc/>
        public override string Encode(AIRequestCall request)
        {
            if (request.HttpMethod == "GET" || request.HttpMethod == "DELETE")
            {
                return "GET and DELETE requests do not use a request body";
            }

            int maxTokens = this.GetSetting<int>("MaxTokens");
            double temperature = this.GetSetting<double>("Temperature");
            if (double.IsNaN(temperature) || temperature <= 0) temperature = 0.5;

            string jsonSchema = request.Body.JsonOutputSchema;
            string? toolFilter = request.Body.ToolFilter;
            bool requiresJsonOutput = !string.IsNullOrWhiteSpace(jsonSchema);
            bool supportsStructuredOutputs = requiresJsonOutput && SupportsStructuredOutputs(request.Model);

            Debug.WriteLine($"[Anthropic] Encode - Model: {request.Model}, MaxTokens: {maxTokens}");

#if DEBUG
            // Log interaction sequence for debugging
            try
            {
                int cnt = request.Body.Interactions?.Count ?? 0;
                int tc = request.Body.Interactions?.Count(i => i is AIInteractionToolCall) ?? 0;
                int tr = request.Body.Interactions?.Count(i => i is AIInteractionToolResult) ?? 0;
                int tx = request.Body.Interactions?.Count(i => i is AIInteractionText) ?? 0;
                Debug.WriteLine($"[Anthropic] BuildMessages: interactions={cnt} (toolCalls={tc}, toolResults={tr}, text={tx})");
            }
            catch { }
#endif

            // Collect system texts for top-level "system" field
            var systemTexts = new List<string>();

            // Group consecutive interactions by role to avoid consecutive messages with the same role
            // Anthropic requires alternating user/assistant roles, but allows multiple content blocks per message
            var messages = new JArray();
            string currentRole = null;
            JArray currentContentBlocks = null;

            // Merge System and Summary interactions before encoding
            var mergedInteractions = this.MergeSystemAndSummary(request.Body.Interactions);

            foreach (var interaction in mergedInteractions)
            {
                try
                {
                    // Collect system/context messages separately
                    if (interaction.Agent == AIAgent.System || interaction.Agent == AIAgent.Context)
                    {
                        if (interaction is AIInteractionText sysText)
                        {
                            systemTexts.Add(sysText.Content ?? string.Empty);
                        }

                        continue; // System messages don't go in messages array
                    }

                    // Get role for this interaction
                    string role = this.GetRoleForAgent(interaction.Agent);
                    if (string.IsNullOrEmpty(role))
                    {
                        continue; // Skip interactions without a role
                    }

                    // Get content block for this interaction
                    var contentBlock = this.CreateContentBlock(interaction);
                    if (contentBlock == null)
                    {
                        continue; // Skip if no content
                    }

                    // Check if we need to start a new message or continue current one
                    if (currentRole != role)
                    {
                        // Role changed: finalize previous message if exists
                        if (currentRole != null && currentContentBlocks != null && currentContentBlocks.Count > 0)
                        {
                            // Sort content blocks: text must come before tool_use for Anthropic API
                            var sortedContent = this.SortContentBlocks(currentContentBlocks);
                            messages.Add(new JObject
                            {
                                ["role"] = currentRole,
                                ["content"] = sortedContent
                            });
                        }

                        // Start new message
                        currentRole = role;
                        currentContentBlocks = new JArray { contentBlock };
                    }
                    else
                    {
                        // Same role: append to current content blocks
                        currentContentBlocks?.Add(contentBlock);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Anthropic] Warning: Could not encode interaction: {ex.Message}");
                }
            }

            // Finalize last message if exists
            if (currentRole != null && currentContentBlocks != null && currentContentBlocks.Count > 0)
            {
                // Sort content blocks: text must come before tool_use for Anthropic API
                var sortedContent = this.SortContentBlocks(currentContentBlocks);
                messages.Add(new JObject
                {
                    ["role"] = currentRole,
                    ["content"] = sortedContent
                });
            }

#if DEBUG
            // Log final messages array for debugging
            try
            {
                Debug.WriteLine($"[Anthropic] Final encoded messages array ({messages.Count} messages):");
                for (int idx = 0; idx < messages.Count; idx++)
                {
                    var msg = messages[idx] as JObject;
                    var role = msg?["role"]?.ToString() ?? "?";
                    var content = msg?["content"] as JArray;
                    var blockTypes = content?.Select(b => (b as JObject)?["type"]?.ToString()).Where(t => !string.IsNullOrEmpty(t)).ToList();
                    Debug.WriteLine($"  [{idx}] role={role}, blocks=[{string.Join(", ", blockTypes ?? new List<string>())}]");
                }
            }
            catch { }
#endif

            var requestBody = new JObject
            {
                ["model"] = request.Model,
                ["messages"] = messages,
                ["max_tokens"] = maxTokens,
                ["temperature"] = temperature,
            };

            // Add JSON schema if provided (centralized wrapping)
            if (requiresJsonOutput)
            {
                try
                {
                    var schemaObj = JObject.Parse(jsonSchema);
                    var svc = JsonSchemaService.Instance;
                    var (wrappedSchema, wrapperInfo) = svc.WrapForProvider(schemaObj, this.Name);
                    svc.SetCurrentWrapperInfo(wrapperInfo);

                    // Add schema constraint as a top-level system instruction (merged with other system texts)
                    var schemaInstructionText = "The response must be a valid JSON object that strictly follows this schema: " + wrappedSchema.ToString(Formatting.None);
                    systemTexts.Add(schemaInstructionText);

                    if (supportsStructuredOutputs)
                    {
                        requestBody["output_format"] = new JObject
                        {
                            ["type"] = "json_schema",
                            ["schema"] = wrappedSchema,
                        };
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Anthropic] Failed to parse JSON schema: {ex.Message}");
                    JsonSchemaService.Instance.SetCurrentWrapperInfo(new SchemaWrapperInfo { IsWrapped = false });
                }
            }
            else
            {
                JsonSchemaService.Instance.SetCurrentWrapperInfo(new SchemaWrapperInfo { IsWrapped = false });
            }

            // After collecting both conversation system texts and optional schema instruction,
            // set the top-level system string, joining entries with "\n---\n".
            if (systemTexts.Count > 0)
            {
                var combinedSystem = string.Join("\n---\n", systemTexts.Where(s => !string.IsNullOrWhiteSpace(s)));
                if (!string.IsNullOrWhiteSpace(combinedSystem))
                {
                    requestBody["system"] = combinedSystem;
                }
            }

            // Add tools if requested: map OpenAI-style tools to Anthropic 'tools'
            if (!string.IsNullOrWhiteSpace(toolFilter))
            {
                try
                {
                    var toolsOpenAI = this.GetFormattedTools(toolFilter);
                    if (toolsOpenAI != null && toolsOpenAI.Count > 0)
                    {
                        var toolsAnthropic = new JArray();
                        foreach (var t in toolsOpenAI.OfType<JObject>())
                        {
                            if (!string.Equals(t["type"]?.ToString(), "function", StringComparison.OrdinalIgnoreCase)) continue;
                            var fn = t["function"] as JObject;
                            if (fn == null) continue;
                            var toolObj = new JObject
                            {
                                ["name"] = fn["name"],
                                ["description"] = fn["description"],
                                ["input_schema"] = fn["parameters"],
                            };
                            toolsAnthropic.Add(toolObj);
                        }

                        if (toolsAnthropic.Count > 0)
                        {
                            requestBody["tools"] = toolsAnthropic;
                            requestBody["tool_choice"] = new JObject { ["type"] = "auto" };
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Anthropic] Tools mapping error: {ex.Message}");
                }
            }

#if DEBUG
            try
            {
                Debug.WriteLine($"[Anthropic] Request body:");
                Debug.WriteLine(requestBody.ToString(Formatting.Indented));
            }
            catch { }
#endif

            return requestBody.ToString();
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
                // Anthropic message response has top-level 'content' array and 'role': 'assistant'
                var content = response["content"] as JArray;
                string contentText = string.Empty;
                var toolCalls = new List<AIInteractionToolCall>();
                var toolResults = new List<AIInteractionToolResult>();

                if (content != null)
                {
                    var textParts = new List<string>();
                    foreach (var block in content.OfType<JObject>())
                    {
                        var type = block["type"]?.ToString();
                        if (string.Equals(type, "text", StringComparison.OrdinalIgnoreCase))
                        {
                            var t = block["text"]?.ToString();
                            if (!string.IsNullOrEmpty(t)) textParts.Add(t);
                        }
                        else if (string.Equals(type, "tool_use", StringComparison.OrdinalIgnoreCase))
                        {
                            // Each new interaction gets a fresh DateTime.UtcNow from AIInteractionBase
                            var toolCall = new AIInteractionToolCall
                            {
                                Id = block["id"]?.ToString(),
                                Name = block["name"]?.ToString(),
                                Arguments = block["input"] as JObject ?? new JObject(),
                                Agent = AIAgent.ToolCall,
                            };
                            Debug.WriteLine($"[Anthropic] Decoded tool_use: id={toolCall.Id}, name={toolCall.Name}");
                            toolCalls.Add(toolCall);
                        }
                        else if (string.Equals(type, "tool_result", StringComparison.OrdinalIgnoreCase))
                        {
                            // Each new interaction gets a fresh DateTime.UtcNow from AIInteractionBase
                            var tr = new AIInteractionToolResult
                            {
                                Id = block["tool_use_id"]?.ToString() ?? block["id"]?.ToString(),
                                Agent = AIAgent.ToolResult,
                            };

                            // Attempt to map back the tool name/args from a prior tool_use in the same message
                            if (!string.IsNullOrEmpty(tr.Id))
                            {
                                var matchingCall = toolCalls.FirstOrDefault(c => string.Equals(c.Id, tr.Id, StringComparison.Ordinal));
                                if (matchingCall != null)
                                {
                                    tr.Name = matchingCall.Name;
                                    tr.Arguments = matchingCall.Arguments;
                                }
                            }

                            // Extract result content; it can be string or array of text blocks
                            var resultText = ExtractToolResultText(block["content"]);

                            // Convert to JObject; if not JSON, wrap as { "value": "..." }
                            try
                            {
                                tr.Result = string.IsNullOrWhiteSpace(resultText) ? new JObject() : JObject.Parse(resultText);
                            }
                            catch
                            {
                                tr.Result = new JObject { ["value"] = resultText ?? string.Empty };
                            }

                            toolResults.Add(tr);
                        }
                    }

                    contentText = string.Join(string.Empty, textParts);
                }

                // Unwrap schema if wrapped centrally
                var wrapperInfo = JsonSchemaService.Instance.GetCurrentWrapperInfo();
                if (wrapperInfo != null && wrapperInfo.IsWrapped)
                {
                    contentText = JsonSchemaService.Instance.Unwrap(contentText, wrapperInfo);
                }

                // Each new interaction gets a fresh DateTime.UtcNow from AIInteractionBase
                var interaction = new AIInteractionText();
                interaction.SetResult(agent: AIAgent.Assistant, content: contentText, reasoning: null);
                interaction.Metrics = this.DecodeMetrics(response);

                Debug.WriteLine($"[Anthropic] Decode creating text interaction: content='{contentText.Substring(0, Math.Min(50, contentText.Length))}...', toolCalls={toolCalls.Count}, toolResults={toolResults.Count}");

                interactions.Add(interaction);

                if (toolCalls.Count > 0)
                {
                    Debug.WriteLine($"[Anthropic] Decode adding {toolCalls.Count} tool calls");
                    interactions.AddRange(toolCalls.Cast<IAIInteraction>());
                }

                if (toolResults.Count > 0)
                {
                    Debug.WriteLine($"[Anthropic] Decode adding {toolResults.Count} tool results");
                    interactions.AddRange(toolResults.Cast<IAIInteraction>());
                }

                Debug.WriteLine($"[Anthropic] Decode returning {interactions.Count} total interactions");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Anthropic] Decode error: {ex.Message}");
            }

            return interactions;
        }

        private AIMetrics DecodeMetrics(JObject response)
        {
            var m = new AIMetrics();
            if (response == null) return m;
            try
            {
                if (response["usage"] is JObject usage)
                {
                    m.InputTokensPrompt = usage["input_tokens"]?.Value<int>() ?? m.InputTokensPrompt;
                    m.OutputTokensGeneration = usage["output_tokens"]?.Value<int>() ?? m.OutputTokensGeneration;
                }

                m.FinishReason = response["stop_reason"]?.ToString() ?? m.FinishReason;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Anthropic] DecodeMetrics error: {ex.Message}");
            }

            return m;
        }

        /// <summary>
        /// Streaming adapter for Anthropic messages using SSE.
        /// </summary>
        private sealed class AnthropicStreamingAdapter : AIProviderStreamingAdapter, IStreamingAdapter
        {
            private readonly AnthropicProvider provider;

            public AnthropicStreamingAdapter(AnthropicProvider provider) : base(provider)
            {
                this.provider = provider;
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

                request = this.Prepare(request);

                if (!string.Equals(request.Endpoint, "/messages", StringComparison.Ordinal))
                {
                    var unsupported = new AIReturn();
                    unsupported.CreateError($"Streaming not supported for endpoint '{request.Endpoint}'. Use /messages.", request);
                    yield return unsupported;
                    yield break;
                }

                JObject bodyObj;
                AIReturn? bodyError = null;
                try
                {
                    var body = this.provider.Encode(request);
                    bodyObj = string.IsNullOrWhiteSpace(body) ? new JObject() : JObject.Parse(body);
                }
                catch (Exception ex)
                {
                    bodyObj = new JObject();
                    bodyError = new AIReturn();
                    bodyError.CreateProviderError($"Failed to prepare streaming body: {ex.Message}", request);
                }

                if (bodyError != null)
                {
                    yield return bodyError;
                    yield break;
                }

                bodyObj["stream"] = true;

                var url = this.BuildFullUrl(request.Endpoint);
                using var client = this.CreateHttpClient();
                AIReturn? authError = null;
                try
                {
                    this.ApplyAuthentication(client, request.Authentication, this.provider.GetApiKey());
                    HttpHeadersHelper.ApplyExtraHeaders(client, request.Headers);
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

                using var httpReq = this.CreateSsePost(url, bodyObj.ToString());
                HttpResponseMessage responseMsg;
                AIReturn? sendError = null;
                try
                {
                    responseMsg = await this.SendForStreamAsync(client, httpReq, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    sendError = new AIReturn();
                    sendError.CreateNetworkError(ex.InnerException?.Message ?? ex.Message, request);
                    responseMsg = null!;
                }

                if (sendError != null)
                {
                    yield return sendError;
                    yield break;
                }

                if (!responseMsg.IsSuccessStatusCode)
                {
                    var content = await responseMsg.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                    var err = new AIReturn();
                    err.CreateProviderError($"HTTP {(int)responseMsg.StatusCode}: {content}", request);
                    yield return err;
                    yield break;
                }

                var initial = new AIReturn { Request = request, Status = AICallStatus.Processing };
                initial.SetBody(new List<IAIInteraction>());
                yield return initial;

                var textBuffer = new StringBuilder();
                var streamMetrics = new AIMetrics { Provider = this.provider.Name, Model = request.Model };
                string? lastFinishReason = null;

                // Track tool calls being built during streaming
                var toolCalls = new List<AIInteractionToolCall>();
                AIInteractionToolCall? currentToolCall = null;
                var toolArgsBuffer = new StringBuilder();

                // Determine idle timeout from request (fallback to 60s if invalid)
                var idleTimeout = TimeSpan.FromSeconds(request.TimeoutSeconds > 0 ? request.TimeoutSeconds : 60);
                await foreach (var data in this.ReadSseDataAsync(
                    responseMsg,
                    idleTimeout,
                    p => p != null && p.Contains("\"type\":\"message_stop\"", StringComparison.OrdinalIgnoreCase),
                    cancellationToken).WithCancellation(cancellationToken))
                {
                    JObject parsed;
                    try
                    {
                        parsed = JObject.Parse(data);
                    }
                    catch
                    {
                        continue;
                    }

                    var type = parsed["type"]?.ToString();
                    Debug.WriteLine($"[Anthropic] Streaming event type: {type}");

                    if (string.Equals(type, "content_block_start", StringComparison.OrdinalIgnoreCase))
                    {
                        // Anthropic sends tool_use as content_block_start events
                        var contentBlock = parsed["content_block"] as JObject;
                        var blockType = contentBlock?["type"]?.ToString();
                        var blockIndex = parsed["index"]?.Value<int>();
                        Debug.WriteLine($"[Anthropic] content_block_start: index={blockIndex}, type={blockType}");

                        if (contentBlock != null && string.Equals(blockType, "tool_use", StringComparison.OrdinalIgnoreCase))
                        {
                            // Start a new tool call - arguments will come in subsequent deltas
                            currentToolCall = new AIInteractionToolCall
                            {
                                Id = contentBlock["id"]?.ToString(),
                                Name = contentBlock["name"]?.ToString(),
                                Arguments = new JObject(), // Will be populated from input_json_delta events
                                Agent = AIAgent.ToolCall,
                            };

                            toolArgsBuffer.Clear();
                            Debug.WriteLine($"[Anthropic] Tool call initialized: id={currentToolCall.Id}, name={currentToolCall.Name}, currentToolCall set to non-null");
                        }
                    }
                    else if (string.Equals(type, "content_block_delta", StringComparison.OrdinalIgnoreCase))
                    {
                        var delta = parsed["delta"] as JObject;
                        var deltaType = delta?["type"]?.ToString();

                        // Handle text deltas
                        if (string.Equals(deltaType, "text_delta", StringComparison.OrdinalIgnoreCase))
                        {
                            var t = delta?["text"]?.ToString();
                            if (!string.IsNullOrEmpty(t))
                            {
                                textBuffer.Append(t);

                                // Each new snapshot gets a fresh DateTime.UtcNow from AIInteractionBase
                                var snapshot = new AIInteractionText
                                {
                                    Agent = AIAgent.Assistant,
                                    Content = textBuffer.ToString(),
                                    Reasoning = string.Empty,
                                    Metrics = new AIMetrics { Provider = this.provider.Name, Model = request.Model },
                                };

                                var deltaRet = new AIReturn { Request = request, Status = AICallStatus.Streaming };
                                deltaRet.SetBody(new List<IAIInteraction> { snapshot });
                                yield return deltaRet;
                            }
                        }

                        // Handle tool input deltas (partial arguments)
                        else if (string.Equals(deltaType, "input_json_delta", StringComparison.OrdinalIgnoreCase))
                        {
                            var partialJson = delta?["partial_json"]?.ToString();
                            if (!string.IsNullOrEmpty(partialJson))
                            {
                                toolArgsBuffer.Append(partialJson);
                                Debug.WriteLine($"[Anthropic] Streaming tool input delta: {partialJson}");
                            }
                        }
                    }
                    else if (string.Equals(type, "content_block_stop", StringComparison.OrdinalIgnoreCase))
                    {
                        var blockIndex = parsed["index"]?.Value<int>();
                        Debug.WriteLine($"[Anthropic] content_block_stop: index={blockIndex}, hasCurrentToolCall={currentToolCall != null}");

                        // Tool arguments are complete - parse and store
                        if (currentToolCall != null)
                        {
                            var argsJson = toolArgsBuffer.ToString();
                            Debug.WriteLine($"[Anthropic] Tool arguments complete: {argsJson}");

                            try
                            {
                                currentToolCall.Arguments = string.IsNullOrEmpty(argsJson) ? new JObject() : JObject.Parse(argsJson);
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"[Anthropic] Failed to parse tool arguments: {ex.Message}");
                                currentToolCall.Arguments = new JObject();
                            }

                            toolCalls.Add(currentToolCall);
                            Debug.WriteLine($"[Anthropic] Added tool call to list: id={currentToolCall.Id}, name={currentToolCall.Name}, args={currentToolCall.Arguments}");

                            // Yield the tool call
                            var tcDelta = new AIReturn { Request = request, Status = AICallStatus.CallingTools };
                            tcDelta.SetBody(new List<IAIInteraction> { currentToolCall });
                            yield return tcDelta;

                            currentToolCall = null;
                            toolArgsBuffer.Clear();
                        }
                        else
                        {
                            Debug.WriteLine($"[Anthropic] content_block_stop but no currentToolCall (likely text block)");
                        }
                    }
                    else if (string.Equals(type, "message_delta", StringComparison.OrdinalIgnoreCase))
                    {
                        Debug.WriteLine($"[Anthropic] message_delta full event: {parsed}");

                        if (parsed["usage"] is JObject usage)
                        {
                            streamMetrics.InputTokensPrompt = usage["input_tokens"]?.Value<int>() ?? streamMetrics.InputTokensPrompt;
                            streamMetrics.OutputTokensGeneration = usage["output_tokens"]?.Value<int>() ?? streamMetrics.OutputTokensGeneration;
                        }

                        // The stop_reason is nested under "delta" in message_delta events
                        var delta = parsed["delta"] as JObject;
                        var stopReason = delta?["stop_reason"]?.ToString();
                        Debug.WriteLine($"[Anthropic] message_delta delta object: {delta}");
                        Debug.WriteLine($"[Anthropic] message_delta stop_reason from delta: {stopReason}");

                        if (!string.IsNullOrWhiteSpace(stopReason))
                        {
                            lastFinishReason = stopReason;
                            Debug.WriteLine($"[Anthropic] Setting lastFinishReason to: {stopReason}");
                        }
                    }
                    else if (string.Equals(type, "message_stop", StringComparison.OrdinalIgnoreCase))
                    {
                        lastFinishReason = lastFinishReason ?? "stop";
                        Debug.WriteLine($"[Anthropic] message_stop, final stop_reason: {lastFinishReason}");
                        break;
                    }
                }

                // Determine final status based on finish reason
                var finalStatus = string.Equals(lastFinishReason, "tool_use", StringComparison.OrdinalIgnoreCase)
                    ? AICallStatus.CallingTools
                    : AICallStatus.Finished;

                var final = new AIReturn { Request = request, Status = finalStatus };
                streamMetrics.FinishReason = lastFinishReason ?? streamMetrics.FinishReason;

                Debug.WriteLine($"[Anthropic] Stream complete: textLen={textBuffer.Length}, toolCalls={toolCalls.Count}, finishReason={streamMetrics.FinishReason}, in={streamMetrics.InputTokensPrompt}, out={streamMetrics.OutputTokensGeneration}");

                // Build final body with text and tool calls
                var finalBuilder = AIBodyBuilder.Create();

                // Add text if present
                if (textBuffer.Length > 0)
                {
                    var finalInteraction = new AIInteractionText
                    {
                        Agent = AIAgent.Assistant,
                        Content = textBuffer.ToString(),
                        Reasoning = string.Empty,
                        Metrics = streamMetrics,
                    };
                    finalBuilder.Add(finalInteraction, markAsNew: false);
                }

                // Add tool calls if present (already marked as NOT new since they were yielded)
                foreach (var tc in toolCalls)
                {
                    Debug.WriteLine($"[Anthropic] Including tool call in final: {tc.Name}");
                    finalBuilder.Add(tc, markAsNew: false);
                }

                final.SetBody(finalBuilder.Build());
                yield return final;
            }
        }

        private void ApplyStructuredOutputsBetaHeader(AIRequestCall request)
        {
            if (request?.Body?.RequiresJsonOutput != true)
            {
                return;
            }

            if (!SupportsStructuredOutputs(request.Model))
            {
                return;
            }

            const string betaHeaderName = "anthropic-beta";
            const string betaValue = "structured-outputs-2025-11-13";

            if (!request.Headers.TryGetValue(betaHeaderName, out var existing) || string.IsNullOrWhiteSpace(existing))
            {
                request.Headers[betaHeaderName] = betaValue;
                return;
            }

            if (!existing.Contains(betaValue, StringComparison.OrdinalIgnoreCase))
            {
                request.Headers[betaHeaderName] = existing.EndsWith(",", StringComparison.Ordinal)
                    ? existing + betaValue
                    : string.Concat(existing, ",", betaValue);
            }
        }

        private static bool SupportsStructuredOutputs(string model)
        {
            if (string.IsNullOrWhiteSpace(model))
            {
                return false;
            }

            var caps = ModelManager.Instance.GetCapabilities("Anthropic", model);
            return caps?.HasCapability(AICapability.Text2Json) == true;
        }
    }
}
