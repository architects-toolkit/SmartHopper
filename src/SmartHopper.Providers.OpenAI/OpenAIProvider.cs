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
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SmartHopper.Config.Interfaces;
using SmartHopper.Config.Managers;
using SmartHopper.Config.Models;
using SmartHopper.Config.Utils;

namespace SmartHopper.Providers.OpenAI
{
    /// <summary>
    /// OpenAI provider implementation for SmartHopper.
    /// </summary>
    public sealed class OpenAIProvider : AIProvider
    {
        private const string NameValue = "OpenAI";
        protected override string ApiURL = "https://api.openai.com/v1/chat/completions";
        private const string DefaultModelValue = "gpt-4o-mini";

        public override string Name => NameValue;

        public override string DefaultModel => DefaultModelValue;

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
                var iconBytes = Properties.Resources.openai_icon;
                using (var ms = new System.IO.MemoryStream(iconBytes))
                {
                    return new Bitmap(ms);
                }
            }
        }

        private static readonly Lazy<OpenAIProvider> InstanceValue = new (() => new OpenAIProvider());

        public static OpenAIProvider Instance => InstanceValue.Value;

        private OpenAIProvider()
        {
        }

        protected override void PreCall(RequestContext context)
        {
            Debug.WriteLine($"[OpenAI] GetResponse - Model: {context.Model}");

            // Format messages for OpenAI API
            var convertedMessages = new JArray();
            foreach (var msg in context.Messages)
            {
                string role = msg["role"]?.ToString().ToLower(System.Globalization.CultureInfo.CurrentCulture) ?? "user";
                string msgContent = msg["content"]?.ToString() ?? string.Empty;
                msgContent = AI.StripThinkTags(msgContent);

                var messageObj = new JObject
                {
                    ["content"] = msgContent,
                };

                if (role == "assistant")
                {
                    // Pass tool_calls if available
                    if (msg["tool_calls"] is JArray toolCalls && toolCalls.Count > 0)
                    {
                        foreach (JObject toolCall in toolCalls)
                        {
                            var function = toolCall["function"] as JObject;
                            if (function != null)
                            {
                                // Ensure 'arguments' is serialized as a JSON string
                                if (function["arguments"] is JObject argumentsObject)
                                {
                                    function["arguments"] = argumentsObject.ToString(Newtonsoft.Json.Formatting.None);
                                }
                            }
                        }

                        messageObj["tool_calls"] = toolCalls;
                    }
                }
                else if (role == "tool")
                {
                    var toolCallId = msg["tool_call_id"]?.ToString();
                    var toolName = msg["name"]?.ToString();
                    if (!string.IsNullOrEmpty(toolCallId))
                    {
                        messageObj["tool_call_id"] = toolCallId;
                    }

                    if (!string.IsNullOrEmpty(toolName))
                    {
                        messageObj["name"] = toolName;
                    }
                }
                else if (role == "tool_call")
                {
                    // Omit it
                    continue;
                }

                messageObj["role"] = role;
                convertedMessages.Add(messageObj);
            }

            // Build request body for the new Responses API
            var requestBody = new JObject
            {
                ["model"] = context.Model,
                ["messages"] = convertedMessages,
                ["max_completion_tokens"] = this.GetSetting<int>("MaxTokens"),
                ["reasoning_effort"] = this.GetSetting<string>("ReasoningEffort") ?? "medium",
            };

            // Add response format if JSON schema is provided
            if (!string.IsNullOrEmpty(context.JsonSchema))
            {
                try
                {
                    var schemaObj = JObject.Parse(context.JsonSchema);
                    requestBody["response_format"] = new JObject
                    {
                        ["type"] = "json_schema",
                        ["schema"] = schemaObj,
                        ["strict"] = true
                    };
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[OpenAI] Failed to parse JSON schema: {ex.Message}");
                    // Continue without schema if parsing fails
                }
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

        protected override void PostCall(RequestContext context)
        {
            if (!context.Response.IsSuccessStatusCode)
            {
                Debug.WriteLine($"[OpenAI] Error response: {context.Response.RawJson}");
                var errorObj = JObject.Parse(context.Response.RawJson);
                var errorMessage = errorObj["error"]?["message"]?.ToString() ?? context.Response.RawJson;
                throw new Exception($"Error from OpenAI API: {context.Response.StatusCode} - {errorMessage}");
            }

            var responseJson = JObject.Parse(context.Response.RawJson);
            Debug.WriteLine($"[OpenAI] Response parsed successfully");

            // Extract response content from the Chat Completions API format
            var message = responseJson["choices"]?[0]?["message"] as JObject;
            var usage = responseJson["usage"] as JObject;

            if (message == null)
            {
                Debug.WriteLine($"[OpenAI] No message in response: {responseJson}");
                throw new Exception("Invalid response from OpenAI API: No message found");
            }


            // extract reasoning_summary and wrap in <think> if present
            var content = message?["content"]?.ToString() ?? string.Empty;
            var summary = responseJson["choices"]?[0]?["reasoning_summary"]?.ToString();
            var combined = !string.IsNullOrWhiteSpace(summary)
                ? $"<think>{summary}</think>{content}"
                : content;
            var aiResponse = new AIResponse
            {
                Response = combined,
                Provider = "OpenAI",
                Model = context.Model,
                FinishReason = responseJson["choices"]?[0]?["finish_reason"]?.ToString() ?? "unknown",
                InTokens = usage?["prompt_tokens"]?.Value<int>() ?? 0,
                OutTokens = usage?["completion_tokens"]?.Value<int>() ?? 0,
            };

            // Handle tool calls if any
            if (message["tool_calls"] is JArray toolCalls && toolCalls.Count > 0)
            {
                aiResponse.ToolCalls = new List<AIToolCall>();
                foreach (JObject toolCall in toolCalls)
                {
                    var function = toolCall["function"] as JObject;
                    if (function != null)
                    {
                        aiResponse.ToolCalls.Add(new AIToolCall
                        {
                            Id = toolCall["id"]?.ToString(),
                            Name = function["name"]?.ToString(),
                            Arguments = function["arguments"]?.ToString(),
                        });
                    }
                }
            }

            Debug.WriteLine($"[OpenAI] Response processed successfully: {aiResponse.Response.Substring(0, Math.Min(50, aiResponse.Response.Length))}...");

            context.Response = aiResponse;
        }

        /// <summary>
        /// Gets the tools formatted for the OpenAI API.
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
                    // Format each tool according to OpenAI's requirements
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

                Debug.WriteLine($"Formatted {toolsArray.Count} tools for OpenAI");
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
