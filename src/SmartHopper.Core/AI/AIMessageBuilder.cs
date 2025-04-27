/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024 Marc Roca Musach
 * 
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using SmartHopper.Config.Models;

namespace SmartHopper.Core.AI
{
    public static class AIMessageBuilder
    {
        //public static Dictionary<string, string> GetRoleReplacement()
        //{
        //    return new Dictionary<string, string>()
        //    {
        //        { OpenAI._name, "assistant" },
        //        { MistralAI._name, "assistant" },
        //        { "User", "user" }
        //    };
        //}

        /// <summary>
        /// Creates a message array from a list of key-value pairs.
        /// </summary>
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
        /// Creates a message array from a list of TextChatModel.
        /// </summary>
        public static JArray CreateMessage(List<TextChatModel> chatMessages)
        {
            var messages = new JArray();
            foreach (TextChatModel element in chatMessages)
            {
                if (!string.IsNullOrEmpty(element.Body))
                {
                    if (element.Author == "system" || element.Author == "user" || element.Author == "tool")
                    {
                        var singleMessage = element.Author == "tool"
                            ? CreateMessage(element.Author, element.Body, element.ToolCallId)
                            : CreateMessage(element.Author, element.Body);
                        if (singleMessage.Count > 0)
                        {
                            messages.Add(singleMessage[0]);
                        }
                    }
                    else if (element.Author == "assistant")
                    {
                        if (!string.IsNullOrEmpty(element.ToolName) || !string.IsNullOrEmpty(element.ToolArgs))
                        {
                            var item = new JObject();
                            item["role"] = element.Author;
                            item["tool_calls"] = new JArray
                            {
                                new JObject
                                {
                                    ["function"] = new JObject()
                                }
                            };

                            item["tool_calls"][0]["type"] = "function";
                            item["tool_calls"][0]["id"] = !string.IsNullOrEmpty(element.ToolCallId) ? element.ToolCallId : Guid.NewGuid().ToString("N").Substring(0, 9).ToUpper();

                            if (!string.IsNullOrEmpty(element.ToolName))
                            {
                                item["tool_calls"][0]["function"]["name"] = element.ToolName;
                            }

                            if (!string.IsNullOrEmpty(element.ToolArgs))
                            {
                                item["tool_calls"][0]["function"]["arguments"] = JObject.Parse(element.ToolArgs);
                            }
                            messages.Add(item);
                        }
                        else
                        {
                            var singleMessage = CreateMessage(element.Author, element.Body);
                            if (singleMessage.Count > 0)
                            {
                                messages.Add(singleMessage[0]);
                            }
                        }
                    }
                }
            }
            return messages;
        }

        /// <summary>
        /// Core function to create a single message. Returns an empty array if the message is invalid.
        /// </summary>
        private static JArray CreateMessage(string role, string content, string toolCallId = null)
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
