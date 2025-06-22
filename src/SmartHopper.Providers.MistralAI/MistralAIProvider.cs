/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SmartHopper.Config.Interfaces;
using SmartHopper.Config.Managers;
using SmartHopper.Config.Models;
using SmartHopper.Config.Utils;

namespace SmartHopper.Providers.MistralAI
{
    public sealed class MistralAIProvider : AIProvider
    {
        private const string NameValue = "MistralAI";

        private const string DefaultModelValue = "mistral-small-latest";

        public override string Name => NameValue;

        public override string DefaultModel => DefaultModelValue;

        protected override string ApiURL => "https://api.mistral.ai/v1/chat/completions";

        /// <summary>
        /// Gets a value indicating whether gets whether this provider is enabled and should be available for use.
        /// </summary>
        public override bool IsEnabled => true;

        public override bool SupportsStreaming => true;

        /// <summary>
        /// Gets the provider's icon.
        /// </summary>
        public override Image Icon
        {
            get
            {
                var iconBytes = Properties.Resources.mistralai_icon;
                using (var ms = new System.IO.MemoryStream(iconBytes))
                {
                    return new Bitmap(ms);
                }
            }
        }

        private static readonly Lazy<MistralAIProvider> InstanceValue = new (() => new MistralAIProvider());

        public static MistralAIProvider Instance => InstanceValue.Value;

        private MistralAIProvider()
        {
        }

        protected override void PreCall(RequestContext context)
        {
            Debug.WriteLine($"[MistralAI] GetResponse - Model: {context.Model}");

            // Format messages for Mistral API
            var convertedMessages = new JArray();
            foreach (var msg in context.Messages)
            {
                // Provider-specific: propagate assistant tool_call messages unmodified
                string role = msg["role"]?.ToString().ToLower(System.Globalization.CultureInfo.CurrentCulture) ?? "user";
                string msgContent = msg["content"]?.ToString() ?? string.Empty;
                msgContent = AI.StripThinkTags(msgContent);

                var messageObj = new JObject
                {
                    ["content"] = msgContent,
                };

                // Map role names
                if (role == "system")
                {
                    // Mistral uses system role
                }
                else if (role == "assistant")
                {
                    // Mistral uses assistant role

                    // Pass tool_calls if available
                    if (msg["tool_calls"] != null)
                    {
                        messageObj["tool_calls"] = msg["tool_calls"];
                    }
                }
                else if (role == "tool")
                {
                    // Propagate tool_call ID and name from incoming message
                    if (msg["name"] != null)
                    {
                        messageObj["name"] = msg["name"];
                    }

                    if (msg["tool_call_id"] != null)
                    {
                        messageObj["tool_call_id"] = msg["tool_call_id"];
                    }
                }
                else if (role == "tool_call")
                {
                    // Omit it
                    continue;
                }
                else if (role == "user")
                {
                    // Mistral uses user role
                }
                else
                {
                    role = "system"; // Default to system
                }

                messageObj["role"] = role;

                convertedMessages.Add(messageObj);
            }

            // Build request body
            var requestBody = new JObject
            {
                ["model"] = context.Model,
                ["messages"] = convertedMessages,
                ["max_tokens"] = this.GetSetting<int>("MaxTokens"),
            };

            // Add JSON response format if schema is provided
            if (!string.IsNullOrWhiteSpace(context.JsonSchema))
            {
                // Add response format for structured output
                requestBody["response_format"] = new JObject
                {
                    ["type"] = "json_object"
                };

                // Add schema as a system message to guide the model
                var systemMessage = new JObject
                {
                    ["role"] = "system",
                    ["content"] = "You are a helpful assistant that returns responses in JSON format. " +
                                    "The response must be a valid JSON object that follows this schema exactly: " +
                                    context.JsonSchema
                };
                convertedMessages.Insert(0, systemMessage);
            }

            // Add tools if requested
            if (context.IncludeToolDefinitions)
            {
                var tools = GetFormattedTools();
                if (tools != null && tools.Count > 0)
                {
                    requestBody["tools"] = tools;
                    requestBody["tool_choice"] = "auto";
                }
            }

            context.Body = requestBody;
        }

        protected override async Task CallStreamingAsync(RequestContext context)
        {
            // Ensure streaming flag set in body
            context.Body["stream"] = true;
            using var client = new HttpClient();
            var apiKey = this.GetSetting<string>("ApiKey");
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
            var request = new HttpRequestMessage(HttpMethod.Post, ApiURL)
            {
                Content = new StringContent(context.Body.ToString(), Encoding.UTF8, "application/json"),
            };
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
            using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            using var reader = new StreamReader(stream);
            context.AccumulatedText = string.Empty;
            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync().ConfigureAwait(false);
Debug.WriteLine($"[MistralAI] Raw line: '{line}'");
                if (string.IsNullOrWhiteSpace(line)) continue;
                var trimmed = line.Trim();
Debug.WriteLine($"[MistralAI] Trimmed line: '{trimmed}'");
                if (trimmed.StartsWith("data:")) trimmed = trimmed.Substring("data:".Length).Trim();
                if (trimmed == "[DONE]") { Debug.WriteLine("[MistralAI] Received [DONE] marker, exiting stream loop"); break; }
                Debug.WriteLine($"[MistralAI] Content chunk: {trimmed}");
                JObject chunkJson;
                try { chunkJson = JObject.Parse(trimmed); } catch { continue; }
                var delta = chunkJson["choices"]?[0]?["delta"] as JObject;
                var content = delta?["content"]?.ToString();
                var finishReasonToken = chunkJson["choices"]?[0]?["finish_reason"];
Debug.WriteLine($"[MistralAI] finishReasonToken type: {finishReasonToken?.Type}, raw value: {finishReasonToken}");
var finishReason = finishReasonToken?.ToString();
Debug.WriteLine($"[MistralAI] Parsed content: '{content}', finishReason: '{finishReason}'");
                if (!string.IsNullOrEmpty(content))
                {
                    context.AccumulatedText += content;
                    context.Progress?.Report(new ChatChunk { Content = content, IsFinal = finishReason != null });
Debug.WriteLine($"[MistralAI] Reporting chunk via Progress: '{content}', IsFinal: {finishReason != null}");
                }
                else if (finishReasonToken != null && finishReasonToken.Type != JTokenType.Null)
                {
                    context.Progress?.Report(new ChatChunk { Content = string.Empty, IsFinal = true });
                    Debug.WriteLine("[MistralAI] Reporting final chunk via Progress and breaking (finishReason: '" + finishReason + "')");
                    break;
                }
            }
        }

        protected override void PostCall(RequestContext context)
        {
            if (context.DoStreaming)
            {
                context.Response = new AIResponse
                {
                    Response = context.AccumulatedText ?? string.Empty,
                    Provider = this.Name,
                    Model = context.Model,
                    FinishReason = "streaming",
                    InTokens = 0,
                    OutTokens = 0,
                };
                return;
            }

            if (context.RawJson == null)
            {
                context.Response = new AIResponse
                {
                    Response = "Error: No response received from MistralAI",
                    Provider = this.Name,
                    Model = context.Model,
                    FinishReason = "error",
                    InTokens = 0,
                    OutTokens = 0,
                };
                return;
            }

            var json = JObject.Parse(context.RawJson!);
            var choices = json["choices"] as JArray;
            var firstChoice = choices?.FirstOrDefault() as JObject;
            var msg = firstChoice?["message"] as JObject;
            var usage = json["usage"] as JObject;

            if (msg == null) throw new Exception("Invalid response from MistralAI: no message");

            var aiResp = new AIResponse
            {
                Response = msg["content"]?.ToString() ?? string.Empty,
                Provider = this.Name,
                Model = context.Model,
                FinishReason = firstChoice?["finish_reason"]?.ToString() ?? "unknown",
                InTokens = usage?["prompt_tokens"]?.Value<int>() ?? 0,
                OutTokens = usage?["completion_tokens"]?.Value<int>() ?? 0,
            };

            if (msg["tool_calls"] is JArray tcs && tcs.Count > 0)
            {
                aiResp.ToolCalls = new List<AIToolCall>();
                foreach (JObject tc in tcs)
                {
                    var fn = tc["function"] as JObject;
                    if (fn != null)
                    {
                        aiResp.ToolCalls.Add(new AIToolCall
                        {
                            Id = tc["id"]?.ToString(),
                            Name = fn["name"]?.ToString(),
                            Arguments = fn["arguments"]?.ToString()
                        });
                    }
                }
            }

            context.Response = aiResp;
        }

        /// <summary>
        /// Gets the tools formatted for the MistralAI API.
        /// </summary>
        /// <returns>JArray of formatted tools.</returns>
        private static new JArray? GetFormattedTools()
        {
            try
            {
                // Ensure tools are discovered
                AIToolManager.DiscoverTools();

                // Get all available tools
                var tools = AIToolManager.GetTools();
                if (tools.Count == 0)
                {
                    Debug.WriteLine("No tools available.");
                    return null;
                }

                var toolsArray = new JArray();

                foreach (var tool in tools)
                {
                    // Format each tool according to MistralAI's requirements
                    var toolObject = new JObject
                    {
                        ["type"] = "function",
                        ["function"] = new JObject
                        {
                            ["name"] = tool.Value.Name,
                            ["description"] = tool.Value.Description,
                            ["parameters"] = JObject.Parse(tool.Value.ParametersSchema),
                        },
                    };

                    toolsArray.Add(toolObject);
                }

                Debug.WriteLine($"Formatted {toolsArray.Count} tools for MistralAI");
                return toolsArray;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error formatting tools: {ex.Message}");
                return null;
            }
        }
    }
}
