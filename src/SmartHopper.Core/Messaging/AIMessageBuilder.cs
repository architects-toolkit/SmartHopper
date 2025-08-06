/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using SmartHopper.Infrastructure.AICall;
using SmartHopper.Infrastructure.AITools;

namespace SmartHopper.Core.Messaging
{
    internal static class AIMessageBuilder
    {
        /// <summary>
        /// Creates a message array from a list of key-value pairs.
        /// </summary>
        /// <returns></returns>
        internal static JArray CreateMessage(List<KeyValuePair<string, string>> kvp)
        {
            var messages = new JArray();
            foreach (KeyValuePair<string, string> element in kvp)
            {
                var interaction = new AIInteraction<string>
                {
                    Agent = AIAgentExtensions.FromString(element.Key),
                    Body = element.Value,
                };

                var singleMessage = CreateMessage(interaction);
                if (singleMessage.Count > 0)
                {
                    messages.Add(singleMessage[0]);
                }
            }

            return messages;
        }

        /// <summary>
        /// Creates a message array from a list of AIInteraction.
        /// </summary>
        /// <returns></returns>
        internal static JArray CreateMessage(List<AIInteraction<string>> chatMessages)
        {
            var messages = new JArray();
            foreach (AIInteraction<string> element in chatMessages)
            {
                // Handle system and user messages
                if (element.Agent == AIAgent.System || element.Agent == AIAgent.User)
                {
                    var single = CreateMessage(element);
                    if (single.Count > 0) messages.Add(single[0]);
                }

                // Handle tool output messages
                else if (element.Agent == AIAgent.ToolResult)
                {
                    if (element.ToolCalls != null && element.ToolCalls.Count > 0)
                    {
                        foreach (var call in element.ToolCalls)
                        {
                            var arr = CreateMessage(element.Agent, element.Body, call.Id);
                            if (arr.Count > 0)
                            {
                                var msg = arr[0];
                                msg["name"] = call.Name;
                                messages.Add(msg);
                            }
                        }
                    }
                    else
                    {
                        var single = CreateMessage(element);
                        if (single.Count > 0) messages.Add(single[0]);
                    }
                }

                // Handle assistant messages, including tool calls
                else if (element.Agent == AIAgent.Assistant)
                {
                    if (element.ToolCalls != null && element.ToolCalls.Count > 0)
                    {
                        var item = new JObject
                        {
                            ["role"] = element.Agent.ToString(),
                            ["content"] = element.Body ?? string.Empty,
                        };
                        var toolCallsArray = new JArray();
                        int idx = 0;
                        foreach (var call in element.ToolCalls)
                        {
                            var functionObj = new JObject
                            {
                                ["name"] = call.Name,
                                ["arguments"] = string.IsNullOrEmpty(call.Arguments)
                                    ? new JObject()
                                    : JObject.Parse(call.Arguments),
                            };
                            var toolObj = new JObject
                            {
                                ["index"] = idx++,
                                ["id"] = call.Id,
                                ["type"] = "function",
                                ["function"] = functionObj,
                            };
                            toolCallsArray.Add(toolObj);
                        }
                        item["tool_calls"] = toolCallsArray;
                        messages.Add(item);
                    }
                    else
                    {
                        var single = CreateMessage(element);
                        if (single.Count > 0) messages.Add(single[0]);
                    }
                }
            }

            return messages;
        }

        /// <summary>
        /// Core function to create a single message. Returns an empty array if the message is invalid.
        /// </summary>
        private static JArray CreateMessage(AIAgent agent, string content, string? toolCallId = null)
        {
            return CreateMessage(new AIInteraction<string>
            {
                Agent = agent,
                Body = content,
                ToolCalls = new List <AIToolCall>() { new AIToolCall { Id = toolCallId } },
            });
        }

        /// <summary>
        /// Core function to create a single message. Returns an empty array if the message is invalid.
        /// </summary>
        private static JArray CreateMessage(AIInteraction<string> interaction)
        {
            var messages = new JArray();
            if (!string.IsNullOrEmpty(interaction.Body))
            {
                if (interaction.Agent == AIAgent.System || interaction.Agent == AIAgent.User || interaction.Agent == AIAgent.Assistant || interaction.Agent == AIAgent.ToolResult)
                {
                    var message = new JObject { ["role"] = interaction.Agent.ToString(), ["content"] = interaction.Body };
                    if (interaction.Agent == AIAgent.ToolResult && !string.IsNullOrEmpty(interaction.ToolCalls[0].Id))
                    {
                        message["tool_call_id"] = interaction.ToolCalls[0].Id;
                    }

                    messages.Add(message);
                }
            }

            return messages;
        }
    }
}
