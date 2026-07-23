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
using SmartHopper.Infrastructure.Utils;
using SmartHopper.ProviderSdk.AICall.Batch;
using SmartHopper.ProviderSdk.AICall.Core;
using SmartHopper.ProviderSdk.AICall.Core.Base;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AICall.Core.Requests;
using SmartHopper.ProviderSdk.AICall.Core.Returns;
using SmartHopper.ProviderSdk.AICall.JsonSchemas;
using SmartHopper.ProviderSdk.AICall.Metrics;
using SmartHopper.ProviderSdk.AIModels;
using SmartHopper.ProviderSdk.AIProviders;
using SmartHopper.ProviderSdk.Diagnostics;
using SmartHopper.ProviderSdk.Streaming;
using SmartHopper.ProviderSdk.Utilities;
using SmartHopper.ProviderSdk.Utils;

namespace SmartHopper.Providers.Anthropic
{
    /// <summary>
    /// Provider implementation for Anthropic's Messages API, including schema wrapping,
    /// tool call encoding, and streaming support via SSE.
    /// </summary>
    public sealed class AnthropicProvider : AIProvider<AnthropicProvider>, IAIBatchProvider
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
        /// Gets a value indicating whether this provider is configured in the current environment.
        /// Anthropic requires a non-empty API key.
        /// </summary>
        public override bool IsConfigured => this.IsSettingConfigured("ApiKey");

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
                    ["content"] = new JArray { contentBlock, },
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
            foreach (var block in textBlocks)
            {
                sorted.Add(block);
            }

            foreach (var block in toolUseBlocks)
            {
                sorted.Add(block);
            }

            foreach (var block in otherBlocks)
            {
                sorted.Add(block);
            }

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
            if (interaction is AIInteractionRuntimeMessage)
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
                // Vision input: send image for understanding/description
                if (!string.IsNullOrWhiteSpace(imageInteraction.ImageData))
                {
                    // Base64-encoded image data
                    var mimeType = imageInteraction.MimeType ?? "image/png";
                    return new JObject
                    {
                        ["type"] = "image",
                        ["source"] = new JObject
                        {
                            ["type"] = "base64",
                            ["media_type"] = mimeType,
                            ["data"] = imageInteraction.ImageData,
                        },
                    };
                }
                else if (imageInteraction.ImageUrl != null)
                {
                    // URL-based image
                    return new JObject
                    {
                        ["type"] = "image",
                        ["source"] = new JObject
                        {
                            ["type"] = "url",
                            ["url"] = imageInteraction.ImageUrl.ToString(),
                        },
                    };
                }
                else
                {
                    // No image data; fall back to prompt text if available
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
                            if (!string.IsNullOrEmpty(t))
                            {
                                parts.Add(t);
                            }
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

            var p = request.Parameters;

            int maxTokens = p?.MaxTokens ?? this.GetSetting<int>("MaxTokens");
            double temperature = p?.Temperature ?? this.GetSetting<double>("Temperature");
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
            catch
            {
                // Intentionally empty
            }
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
                                ["content"] = sortedContent,
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
            catch
            {
                // Intentionally empty
            }
#endif

            var requestBody = new JObject
            {
                ["model"] = request.Model,
                ["messages"] = messages,
                ["max_tokens"] = maxTokens,
                ["temperature"] = temperature,
            };

            // Apply optional parameters from extras only
            // Note: effort goes inside output_config, container goes at top level
            JObject outputConfig = null;
            if (p?.Extras != null)
            {
                if (p.Extras.TryGetValue("seed", out var seedToken) && seedToken != null)
                {
                    requestBody["seed"] = seedToken.Value<int?>();
                }

                if (p.Extras.TryGetValue("top_p", out var topPToken) && topPToken != null)
                {
                    requestBody["top_p"] = topPToken.Value<double?>();
                }

                if (p.Extras.TryGetValue("top_k", out var topKToken) && topKToken != null)
                {
                    requestBody["top_k"] = topKToken.Value<int?>();
                }

                if (p.Extras.TryGetValue("service_tier", out var stToken) && stToken != null)
                {
                    requestBody["service_tier"] = stToken;
                }

                // container is a top-level parameter (not inside output_config)
                if (p.Extras.TryGetValue("container", out var containerToken) && containerToken != null)
                {
                    requestBody["container"] = containerToken;
                }

                // effort goes inside output_config
                if (p.Extras.TryGetValue("effort", out var effortToken) && effortToken != null)
                {
                    outputConfig ??= new JObject();
                    outputConfig["effort"] = effortToken;
                }
            }

            // Add JSON schema if provided (centralized wrapping)
            if (requiresJsonOutput)
            {
                try
                {
                    var schemaObj = JObject.Parse(jsonSchema);

                    // Anthropic requires additionalProperties=false on all object schemas in structured output mode
                    InjectAdditionalPropertiesFalse(schemaObj);

                    var svc = JsonSchemaService.Instance;
                    var (wrappedSchema, wrapperInfo) = svc.WrapForProvider(schemaObj, this.Name);
                    svc.SetCurrentWrapperInfo(wrapperInfo);

                    // Add schema constraint as a top-level system instruction (merged with other system texts)
                    var schemaInstructionText = "The response must be a valid JSON object that strictly follows this schema: " + wrappedSchema.ToString(Formatting.None);
                    systemTexts.Add(schemaInstructionText);

                    if (supportsStructuredOutputs)
                    {
                        // Merge with existing outputConfig if it has effort
                        if (outputConfig == null)
                        {
                            outputConfig = new JObject();
                        }

                        outputConfig["format"] = new JObject
                        {
                            ["type"] = "json_schema",
                            ["schema"] = wrappedSchema,
                        };
                        requestBody["output_config"] = outputConfig;
                    }
                    else if (outputConfig != null)
                    {
                        // No structured output, but we still have effort to add
                        requestBody["output_config"] = outputConfig;
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

                // If we have effort but no JSON schema, still need to add output_config
                if (outputConfig != null)
                {
                    requestBody["output_config"] = outputConfig;
                }
            }

            // Determine whether prompt caching is enabled via extras.
            bool enableCaching = p?.Extras != null
                && p.Extras.TryGetValue("enable_caching", out var ecToken)
                && ecToken?.Value<bool>() == true;

            // After collecting both conversation system texts and optional schema instruction,
            // set the top-level system field, joining entries with "\n---\n".
            if (systemTexts.Count > 0)
            {
                var combinedSystem = string.Join("\n---\n", systemTexts.Where(s => !string.IsNullOrWhiteSpace(s)));
                if (!string.IsNullOrWhiteSpace(combinedSystem))
                {
                    if (enableCaching)
                    {
                        // When caching is enabled, emit system as a content-block array with an
                        // explicit cache breakpoint on the last (stable) system block. This makes
                        // single-shot and batch requests sharing the same tools+system prefix hit
                        // the cache even when the user message varies per request.
                        requestBody["system"] = new JArray
                        {
                            new JObject
                            {
                                ["type"] = "text",
                                ["text"] = combinedSystem,
                                ["cache_control"] = new JObject { ["type"] = "ephemeral" },
                            },
                        };
                    }
                    else
                    {
                        requestBody["system"] = combinedSystem;
                    }
                }
            }

            // Apply automatic prompt caching when enable_caching=true:
            // Adds top-level cache_control so Anthropic automatically advances the cache
            // breakpoint over the growing message history in multi-turn conversations.
            // This is complementary to the explicit system breakpoint above and is a no-op
            // when the last cacheable block already carries the same cache_control TTL.
            if (enableCaching)
            {
                requestBody["cache_control"] = new JObject { ["type"] = "ephemeral" };
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
                            if (!string.Equals(t["type"]?.ToString(), "function", StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }

                            var fn = t["function"] as JObject;
                            if (fn == null)
                            {
                                continue;
                            }

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

                            // Handle forced tool call: Anthropic uses tool_choice with type and name
                            if (request.ForceToolCall && !string.IsNullOrWhiteSpace(request.ForceToolName))
                            {
                                requestBody["tool_choice"] = new JObject
                                {
                                    ["type"] = "tool",
                                    ["name"] = request.ForceToolName,
                                };
                                Debug.WriteLine($"[Anthropic] Forcing tool call: {request.ForceToolName}");
                            }
                            else
                            {
                                // Use string value for auto (not wrapped in JObject) per Anthropic docs
                                requestBody["tool_choice"] = "auto";
                            }
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
            catch
            {
                // Intentionally empty
            }
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
                // Handle provider error responses (e.g. batch items with status_code 4xx/5xx)
                // Format: {"type": "error", "error": {"type": "...", "message": "..."}}
                if (response["error"] is JObject errorObj)
                {
                    var msg = errorObj["message"]?.ToString()
                              ?? errorObj["type"]?.ToString()
                              ?? "Provider returned an error";
                    Debug.WriteLine($"[Anthropic] Decode: provider error in response body: {msg}");
                    interactions.Add(new AIInteractionRuntimeMessage { Severity = SHRuntimeMessageSeverity.Error, Content = msg });
                    return interactions;
                }

                // Anthropic message response has top-level 'content' array and 'role': 'assistant'
                var content = response["content"] as JArray;
                string contentText = string.Empty;
                string reasoningText = string.Empty;
                var toolCalls = new List<AIInteractionToolCall>();
                var toolResults = new List<AIInteractionToolResult>();

                if (content != null)
                {
                    var textParts = new List<string>();
                    var thinkingParts = new List<string>();

                    foreach (var block in content.OfType<JObject>())
                    {
                        var type = block["type"]?.ToString();
                        if (string.Equals(type, "text", StringComparison.OrdinalIgnoreCase))
                        {
                            var t = block["text"]?.ToString();
                            if (!string.IsNullOrEmpty(t))
                            {
                                textParts.Add(t);
                            }
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
                    reasoningText = thinkingParts.Count > 0 ? string.Join("\n\n", thinkingParts) : null;
                }

                // Unwrap schema if wrapped centrally
                var wrapperInfo = JsonSchemaService.Instance.GetCurrentWrapperInfo();
                if (wrapperInfo != null && wrapperInfo.IsWrapped)
                {
                    contentText = JsonSchemaService.Instance.Unwrap(contentText, wrapperInfo);
                }

                // Each new interaction gets a fresh DateTime.UtcNow from AIInteractionBase
                var interaction = new AIInteractionText();
                interaction.SetResult(agent: AIAgent.Assistant, content: contentText, reasoning: reasoningText);
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
            if (response == null)
            {
                return m;
            }

            try
            {
                if (response["usage"] is JObject usage)
                {
                    m.InputTokensPrompt = usage["input_tokens"]?.Value<int>() ?? m.InputTokensPrompt;
                    m.OutputTokensGeneration = usage["output_tokens"]?.Value<int>() ?? m.OutputTokensGeneration;
                    m.InputTokensCached = usage["cache_read_input_tokens"]?.Value<int>() ?? m.InputTokensCached;
                    m.InputTokensCacheWrite = usage["cache_creation_input_tokens"]?.Value<int>() ?? m.InputTokensCacheWrite;
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

            public AnthropicStreamingAdapter(AnthropicProvider provider)
                : base(provider)
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
                    var (message, isNetworkLike) = AIProvider.ClassifyHttpError((int)responseMsg.StatusCode, responseMsg.ReasonPhrase, content, this.Provider.Name);
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

                // Determine idle timeout from request; fall back to shared default if invalid.
                var idleTimeout = TimeSpan.FromSeconds((double)(request.TimeoutSeconds > 0 ? request.TimeoutSeconds : TimeoutDefaults.DefaultTimeoutSeconds));
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
                            streamMetrics.InputTokensCached = usage["cache_read_input_tokens"]?.Value<int>() ?? streamMetrics.InputTokensCached;
                            streamMetrics.InputTokensCacheWrite = usage["cache_creation_input_tokens"]?.Value<int>() ?? streamMetrics.InputTokensCacheWrite;
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

        private static bool SupportsStructuredOutputs(string model)
        {
            if (string.IsNullOrWhiteSpace(model))
            {
                return false;
            }

            var caps = AIModelCapabilityRegistry.Instance.GetCapabilities("Anthropic", model);
            return caps?.HasCapability(AICapability.Text2Json) == true;
        }

        /// <summary>
        /// Recursively adds additionalProperties=false to all object-type schemas in a JSON schema.
        /// Anthropic requires this for structured output mode with output_config.format.schema.
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

        #region IAIBatchProvider

        /// <inheritdoc/>
        public async Task<AIBatchSubmission> SubmitBatchAsync(IReadOnlyList<(string CustomId, AIRequestCall Request)> items, CancellationToken cancellationToken = default)
        {
            if (items == null || items.Count == 0) throw new ArgumentException("At least one item is required", nameof(items));

            // Anthropic Batch API: POST /v1/messages/batches
            // Each request: { "custom_id": "...", "params": { ...Messages API params... } }
            var requestsArray = new JArray();
            string firstEncodedBody = null;
            var customIds = new List<string>();

            foreach (var (customId, request) in items)
            {
                var preparedRequest = this.PreCall(request);
                var encodedBody = this.Encode(preparedRequest);
                if (firstEncodedBody == null)
                {
                    firstEncodedBody = encodedBody;
                }

                var paramsObj = JObject.Parse(encodedBody);
                requestsArray.Add(new JObject
                {
                    ["custom_id"] = customId,
                    ["params"] = paramsObj,
                });
                customIds.Add(customId);
            }

            var batchRequest = new JObject { ["requests"] = requestsArray };

            var apiKey = this.GetApiKey();
            using var client = this.CreateBatchHttpClient();
            client.DefaultRequestHeaders.TryAddWithoutValidation("x-api-key", apiKey);
            client.DefaultRequestHeaders.TryAddWithoutValidation("anthropic-version", "2023-06-01");

            var batchUrl = this.BuildFullUrl("/messages/batches");
            var response = await client.PostAsync(
                batchUrl,
                new StringContent(batchRequest.ToString(), Encoding.UTF8, "application/json"),
                cancellationToken).ConfigureAwait(false);
            var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"[Anthropic] Batch creation failed ({(int)response.StatusCode}): {content}");
            }

            var result = JObject.Parse(content);
            var batchId = result["id"]?.ToString()
                ?? throw new InvalidOperationException("[Anthropic] Batch creation response missing 'id'");

            Debug.WriteLine($"[Anthropic] Batch submitted: id={batchId}, count={items.Count}");
            return new AIBatchSubmission(batchId, this.Name, firstEncodedBody, (IReadOnlyList<string>)customIds.AsReadOnly());
        }

        /// <inheritdoc/>
        public async Task<AIBatchStatus> GetBatchStatusAsync(AIBatchSubmission submission, CancellationToken cancellationToken = default)
        {
            if (submission == null) throw new ArgumentNullException(nameof(submission));

            var apiKey = this.GetApiKey();
            using var client = this.CreateBatchHttpClient();
            client.DefaultRequestHeaders.TryAddWithoutValidation("x-api-key", apiKey);
            client.DefaultRequestHeaders.TryAddWithoutValidation("anthropic-version", "2023-06-01");

            var statusUrl = this.BuildFullUrl($"/messages/batches/{submission.BatchId}");
            var response = await client.GetAsync(statusUrl, cancellationToken).ConfigureAwait(false);
            var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return new AIBatchStatus(submission.BatchId, AIBatchState.Failed, $"HTTP {(int)response.StatusCode}: {content}");
            }

            var json = JObject.Parse(content);
            var processingStatus = json["processing_status"]?.ToString() ?? string.Empty;
            Debug.WriteLine($"[Anthropic] Batch status check: id={submission.BatchId}, processing_status={processingStatus}");

            switch (processingStatus.ToLowerInvariant())
            {
                case "in_progress":
                case "canceling":
                {
                    var requestCounts = json["request_counts"] as JObject;
                    int? completedCount = requestCounts?["succeeded"]?.Value<int>();
                    return new AIBatchStatus(submission.BatchId, AIBatchState.InProgress, completedCount: completedCount);
                }

                case "ended":
                {
                    // Determine if it ended due to cancellation
                    var cancelInitiatedAt = json["cancel_initiated_at"]?.ToString();
                    var requestCounts = json["request_counts"] as JObject;
                    var succeeded = requestCounts?["succeeded"]?.Value<int>() ?? 0;
                    var errored = requestCounts?["errored"]?.Value<int>() ?? 0;

                    if (!string.IsNullOrEmpty(cancelInitiatedAt) && succeeded == 0 && errored == 0)
                    {
                        return new AIBatchStatus(submission.BatchId, AIBatchState.Cancelled);
                    }

                    // Delegate download + parse to the interface methods
                    IReadOnlyList<string> files;
                    try
                    {
                        files = await this.DownloadBatchResultsAsync(submission, cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        return new AIBatchStatus(submission.BatchId, AIBatchState.Failed, $"Failed to download batch results: {ex.Message}");
                    }

                    var parsed = this.ParseBatchResultsFiles(files, submission.BatchId);
                    if ((parsed.Results?.Count ?? 0) == 0 && (parsed.Messages?.Count ?? 0) == 0)
                    {
                        return new AIBatchStatus(submission.BatchId, AIBatchState.Failed, "No results found in batch output");
                    }

                    return parsed;
                }

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
            client.DefaultRequestHeaders.TryAddWithoutValidation("x-api-key", apiKey);
            client.DefaultRequestHeaders.TryAddWithoutValidation("anthropic-version", "2023-06-01");

            var cancelUrl = this.BuildFullUrl($"/messages/batches/{submission.BatchId}/cancel");
            var response = await client.PostAsync(cancelUrl, null, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                Debug.WriteLine($"[Anthropic] Batch cancel failed ({(int)response.StatusCode}): {content}");
            }
            else
            {
                Debug.WriteLine($"[Anthropic] Batch {submission.BatchId} cancelled successfully");
            }
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<string>> DownloadBatchResultsAsync(AIBatchSubmission submission, CancellationToken cancellationToken = default)
        {
            if (submission == null) throw new ArgumentNullException(nameof(submission));

            var apiKey = this.GetApiKey();
            using var client = this.CreateBatchHttpClient();
            client.DefaultRequestHeaders.TryAddWithoutValidation("x-api-key", apiKey);
            client.DefaultRequestHeaders.TryAddWithoutValidation("anthropic-version", "2023-06-01");

            // Re-fetch batch status to obtain results_url.
            var statusUrl = this.BuildFullUrl($"/messages/batches/{submission.BatchId}");
            var statusResponse = await client.GetAsync(statusUrl, cancellationToken).ConfigureAwait(false);
            var statusContent = await statusResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (!statusResponse.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"HTTP {(int)statusResponse.StatusCode}: {statusContent}");
            }

            var statusJson = JObject.Parse(statusContent);
            var resultsUrl = statusJson["results_url"]?.ToString();
            if (string.IsNullOrEmpty(resultsUrl))
            {
                throw new InvalidOperationException("Batch ended but results_url is missing");
            }

            var resultsResponse = await client.GetAsync(resultsUrl, cancellationToken).ConfigureAwait(false);
            var resultsContent = await resultsResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (!resultsResponse.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"Failed to download results ({(int)resultsResponse.StatusCode}): {resultsContent}");
            }

            return string.IsNullOrWhiteSpace(resultsContent)
                ? Array.Empty<string>()
                : new[] { resultsContent };
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
        /// Parses a single Anthropic batch results JSONL file. Each line:
        /// <c>{"custom_id":"sh-...", "result":{"type":"succeeded|errored|canceled|expired", "message"?, "error"?}}</c>.
        /// Stop reasons (e.g., "max_tokens") are extracted from successful responses and surfaced as warnings.
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

                var resultObj = resultLine["result"] as JObject;
                var resultType = resultObj?["type"]?.ToString();

                if (string.Equals(resultType, "succeeded", StringComparison.OrdinalIgnoreCase))
                {
                    var messageResult = resultObj["message"] as JObject;
                    if (messageResult != null)
                    {
                        results[lineCustomId] = messageResult;

                        // Extract stop_reason and surface as warning for non-end reasons
                        var stopReason = messageResult["stop_reason"]?.ToString();
                        if (!string.IsNullOrEmpty(stopReason) && stopReason != "end_turn")
                        {
                            messages.Add(new SHRuntimeMessage(
                                SHRuntimeMessageSeverity.Warning,
                                SHRuntimeMessageOrigin.Provider,
                                SHMessageCode.BatchItemFinishReason,
                                $"Batch item {lineCustomId}: completed with stop_reason='{stopReason}'"));
                        }
                    }
                }
                else if (string.Equals(resultType, "errored", StringComparison.OrdinalIgnoreCase))
                {
                    var errorMsg = resultObj?["error"]?.ToString();
                    if (resultObj?["error"] is JObject errorObj)
                    {
                        errorMsg = errorObj["error"] is JObject innerError
                            ? innerError["message"]?.ToString()
                            : errorObj["message"]?.ToString() ?? errorObj.ToString();
                    }

                    messages.Add(new SHRuntimeMessage(
                        SHRuntimeMessageSeverity.Error,
                        SHRuntimeMessageOrigin.Provider,
                        SHMessageCode.BatchItemError,
                        $"Batch item {lineCustomId}: {errorMsg ?? "Unknown error"}"));
                }
                else if (string.Equals(resultType, "canceled", StringComparison.OrdinalIgnoreCase))
                {
                    messages.Add(new SHRuntimeMessage(
                        SHRuntimeMessageSeverity.Error,
                        SHRuntimeMessageOrigin.Provider,
                        SHMessageCode.BatchItemCanceled,
                        $"Batch item {lineCustomId}: Request was canceled before it could be processed"));
                }
                else if (string.Equals(resultType, "expired", StringComparison.OrdinalIgnoreCase))
                {
                    messages.Add(new SHRuntimeMessage(
                        SHRuntimeMessageSeverity.Warning,
                        SHRuntimeMessageOrigin.Provider,
                        SHMessageCode.BatchItemExpired,
                        $"Batch item {lineCustomId}: Request expired before it could be sent to the model (batch exceeded 24-hour limit)"));
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
                // Anthropic-specific parameters
                new AIExtraDescriptor(
                    "effort",
                    "Effort",
                    "The amount of effort to use in the output. 'low' is fastest, 'max' is most thorough. Overrides global provider setting.",
                    typeof(string),
                    "low",
                    new[] { "low", "medium", "high", "xhigh", "max" }),

                // General parameters (shared across providers)
                new AIExtraDescriptor(
                    "seed",
                    "Seed",
                    "Reproducibility seed for deterministic sampling. Use the same seed to get similar outputs. Leave empty for random.",
                    typeof(int),
                    null),
                new AIExtraDescriptor(
                    "top_p",
                    "Top P",
                    "Nucleus sampling parameter (0.0–1.0). Lower values make output more focused; higher values more diverse. Leave empty to use default.",
                    typeof(double),
                    null),
                new AIExtraDescriptor(
                    "top_k",
                    "Top K",
                    "Only sample from the top K options for each token. Lower values make output more focused.",
                    typeof(int),
                    null),

                // Anthropic-specific parameters
                new AIExtraDescriptor(
                    "effort",
                    "Effort",
                    "The amount of effort to use in the output. 'low' is fastest, 'high' is most thorough.",
                    typeof(string),
                    "medium",
                    new[] { "low", "medium", "high" }),
                new AIExtraDescriptor(
                    "container",
                    "Container",
                    "Container type for the response format. Anthropic-specific.",
                    typeof(string),
                    null),
                new AIExtraDescriptor(
                    "service_tier",
                    "Service Tier",
                    "Service tier for request processing. 'auto' uses Priority Tier when available, 'standard_only' uses only standard tier.",
                    typeof(string),
                    "auto",
                    new[] { "auto", "standard_only" }),

                // Anthropic prompt caching parameters
                new AIExtraDescriptor(
                    "enable_caching",
                    "Enable Prompt Caching",
                    "Automatically caches the longest stable prompt prefix (>1024 tokens for Sonnet, >4096 tokens for Opus and Haiku). Reduces latency and cost on repeated calls sharing the same context. Highly recommended for batch processing.",
                    typeof(bool),
                    null),
            };
        }
    }
}