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
using SmartHopper.Infrastructure.Models;

namespace SmartHopper.Core.AI
{
    public static class AIMessageBuilder
    {
        /// <summary>
        /// Creates a message array from a list of key-value pairs.
        /// </summary>
        /// <returns></returns>
        public static JArray CreateMessage(List<KeyValuePair<string, string>> kvp)
        {
            var messages = new JArray();
            foreach (KeyValuePair<string, string> element in kvp)
            {
                var singleMessage = CreateMessage(element.Key, element.Value);
                if (singleMessage.Count > 0)
                {
                    messages.Add(singleMessage[0]);
                }
            }

            return messages;
        }

        /// <summary>
        /// Creates a message array from a list of ChatMessageModel.
        /// </summary>
        /// <returns></returns>
        public static JArray CreateMessage(List<ChatMessageModel> chatMessages)
        {
            var messages = new JArray();
            foreach (ChatMessageModel element in chatMessages)
            {
                // Handle system and user messages
                if (element.Author == "system" || element.Author == "user")
                {
                    var single = CreateMessage(element.Author, element.Body);
                    if (single.Count > 0) messages.Add(single[0]);
                }
                // Handle tool output messages
                else if (element.Author == "tool")
                {
                    if (element.ToolCalls != null && element.ToolCalls.Count > 0)
                    {
                        foreach (var call in element.ToolCalls)
                        {
                            var arr = CreateMessage("tool", element.Body, call.Id);
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
                        var single = CreateMessage("tool", element.Body);
                        if (single.Count > 0) messages.Add(single[0]);
                    }
                }
                // Handle assistant messages, including tool calls
                else if (element.Author == "assistant")
                {
                    if (element.ToolCalls != null && element.ToolCalls.Count > 0)
                    {
                        var item = new JObject
                        {
                            ["role"] = "assistant",
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
                        var single = CreateMessage(element.Author, element.Body);
                        if (single.Count > 0) messages.Add(single[0]);
                    }
                }
            }

            return messages;
        }

        /// <summary>
        /// Core function to create a single message. Returns an empty array if the message is invalid.
        /// </summary>
        private static JArray CreateMessage(string role, string content, string? toolCallId = null)
        {
            var messages = new JArray();
            if (!string.IsNullOrEmpty(content))
            {
                // Only add roles "system", "user", "assistant" or "tool"
                if (role == "system" || role == "user" || role == "assistant" || role == "tool")
                {
                    var message = new JObject { ["role"] = role, ["content"] = content };
                    if (role == "tool" && !string.IsNullOrEmpty(toolCallId))
                    {
                        message["tool_call_id"] = toolCallId;
                    }

                    messages.Add(message);
                }
            }

            return messages;
        }
    }
}
