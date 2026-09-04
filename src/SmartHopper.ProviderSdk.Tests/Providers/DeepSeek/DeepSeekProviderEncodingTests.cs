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

#if NET7_WINDOWS

namespace SmartHopper.ProviderSdk.Tests.Providers.DeepSeek
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Newtonsoft.Json.Linq;
    using SmartHopper.Providers.DeepSeek;
    using SmartHopper.ProviderSdk.AICall.Core.Base;
    using SmartHopper.ProviderSdk.AICall.Core.Interactions;
    using SmartHopper.ProviderSdk.AICall.Core.Requests;
    using SmartHopper.ProviderSdk.AIModels;
    using Xunit;

    /// <summary>
    /// Encoding tests for the DeepSeek provider, focusing on tool-call message ordering.
    /// </summary>
    [Collection("ProviderSdk")]
    public sealed class DeepSeekProviderEncodingTests
    {
        /// <summary>
        /// DeepSeek rejects an assistant <c>tool_calls</c> message that is not immediately
        /// followed by matching <c>role: tool</c> messages. This test captures that contract.
        /// </summary>
        [Fact]
        public void Encode_SingleToolCallFollowedByToolResult_ProducesValidMessageSequence()
        {
            var body = AIBodyBuilder.Create()
                .Add(new AIInteractionText { Agent = AIAgent.System, Content = "You are helpful." })
                .Add(new AIInteractionText { Agent = AIAgent.User, Content = "Call the tool." })
                .Add(new AIInteractionToolCall
                {
                    Id = "call_123",
                    Name = "test_tool",
                    Arguments = new JObject { ["value"] = 42 },
                })
                .Add(new AIInteractionToolResult
                {
                    Id = "call_123",
                    Name = "test_tool",
                    Result = new JObject { ["result"] = "ok" },
                })
                .Build();

            var request = new AIRequestCall
            {
                Provider = DeepSeekProvider.Instance.Name,
                Model = "deepseek-chat",
                Body = body,
                Capability = AICapability.Text2Text,
            };

            DeepSeekProvider.Instance.RefreshCachedSettings(new Dictionary<string, object>
            {
                { "ApiKey", "test-key" },
                { "MaxTokens", 1024 },
                { "Temperature", 0.7 },
                { "TopP", 1.0 },
                { "ReasoningEffort", "none" },
            });

            var encodedJson = DeepSeekProvider.Instance.Encode(request);
            var requestObj = JObject.Parse(encodedJson);
            var messages = requestObj["messages"] as JArray;

            Assert.NotNull(messages);

            var assistantWithToolCallsIndex = -1;
            for (int i = 0; i < messages.Count; i++)
            {
                var msg = messages[i] as JObject;
                if (string.Equals(msg?["role"]?.ToString(), "assistant", StringComparison.OrdinalIgnoreCase) &&
                    msg?["tool_calls"] is JArray tcArray && tcArray.Count > 0)
                {
                    assistantWithToolCallsIndex = i;
                    break;
                }
            }

            Assert.True(assistantWithToolCallsIndex >= 0, "Expected an assistant message with tool_calls");

            var toolCallIds = (messages[assistantWithToolCallsIndex]["tool_calls"] as JArray)
                .OfType<JObject>()
                .Select(tc => tc["id"]?.ToString())
                .Where(id => !string.IsNullOrEmpty(id))
                .ToList();

            var remainingIds = new HashSet<string>(toolCallIds!, StringComparer.Ordinal);
            for (int i = assistantWithToolCallsIndex + 1; i < messages.Count; i++)
            {
                var msg = messages[i] as JObject;
                var role = msg?["role"]?.ToString();
                if (!string.Equals(role, "tool", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                var toolCallId = msg?["tool_call_id"]?.ToString() ?? string.Empty;
                remainingIds.Remove(toolCallId);
            }

            Assert.Empty(remainingIds);
        }

        /// <summary>
        /// Validates that an assistant text interaction preceding a tool-call interaction
        /// is merged into a single assistant message and that matching tool results follow.
        /// This mirrors the order produced by non-streaming response decoding.
        /// </summary>
        [Fact]
        public void Encode_TextAndToolCallFollowedByToolResult_ProducesValidMessageSequence()
        {
            var body = AIBodyBuilder.Create()
                .Add(new AIInteractionText { Agent = AIAgent.System, Content = "You are helpful." })
                .Add(new AIInteractionText { Agent = AIAgent.User, Content = "Call the tool." })
                .Add(new AIInteractionText { Agent = AIAgent.Assistant, Content = "I will call it." })
                .Add(new AIInteractionToolCall
                {
                    Id = "call_123",
                    Name = "test_tool",
                    Arguments = new JObject { ["value"] = 42 },
                })
                .Add(new AIInteractionToolResult
                {
                    Id = "call_123",
                    Name = "test_tool",
                    Result = new JObject { ["result"] = "ok" },
                })
                .Build();

            var request = new AIRequestCall
            {
                Provider = DeepSeekProvider.Instance.Name,
                Model = "deepseek-chat",
                Body = body,
                Capability = AICapability.Text2Text,
            };

            DeepSeekProvider.Instance.RefreshCachedSettings(new Dictionary<string, object>
            {
                { "ApiKey", "test-key" },
                { "MaxTokens", 1024 },
                { "Temperature", 0.7 },
                { "TopP", 1.0 },
                { "ReasoningEffort", "none" },
            });

            var encodedJson = DeepSeekProvider.Instance.Encode(request);
            var requestObj = JObject.Parse(encodedJson);
            var messages = requestObj["messages"] as JArray;

            Assert.NotNull(messages);

            var assistantWithToolCallsIndex = -1;
            for (int i = 0; i < messages.Count; i++)
            {
                var msg = messages[i] as JObject;
                if (string.Equals(msg?["role"]?.ToString(), "assistant", StringComparison.OrdinalIgnoreCase) &&
                    msg?["tool_calls"] is JArray tcArray && tcArray.Count > 0)
                {
                    assistantWithToolCallsIndex = i;
                    break;
                }
            }

            Assert.True(assistantWithToolCallsIndex >= 0, "Expected an assistant message with tool_calls");

            var toolCallIds = (messages[assistantWithToolCallsIndex]["tool_calls"] as JArray)
                .OfType<JObject>()
                .Select(tc => tc["id"]?.ToString())
                .Where(id => !string.IsNullOrEmpty(id))
                .ToList();

            var remainingIds = new HashSet<string>(toolCallIds, StringComparer.Ordinal);
            for (int i = assistantWithToolCallsIndex + 1; i < messages.Count; i++)
            {
                var msg = messages[i] as JObject;
                var role = msg?["role"]?.ToString();
                if (!string.Equals(role, "tool", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                var toolCallId = msg?["tool_call_id"]?.ToString() ?? string.Empty;
                remainingIds.Remove(toolCallId);
            }

            Assert.Empty(remainingIds);
        }

        /// <summary>
        /// Regression for the 1.4.2 bug: consecutive tool results were merged into one
        /// <c>role: tool</c> message, losing all but the first <c>tool_call_id</c>.
        /// DeepSeek rejects that with "insufficient tool messages following tool_calls".
        /// </summary>
        [Fact]
        public void Encode_MultipleToolResults_AreNotMergedIntoSingleMessage()
        {
            var body = AIBodyBuilder.Create()
                .Add(new AIInteractionText { Agent = AIAgent.System, Content = "You are helpful." })
                .Add(new AIInteractionText { Agent = AIAgent.User, Content = "Call two tools." })
                .Add(new AIInteractionToolCall
                {
                    Id = "call_1",
                    Name = "tool_a",
                    Arguments = new JObject { ["value"] = 1 },
                })
                .Add(new AIInteractionToolCall
                {
                    Id = "call_2",
                    Name = "tool_b",
                    Arguments = new JObject { ["value"] = 2 },
                })
                .Add(new AIInteractionToolResult
                {
                    Id = "call_1",
                    Name = "tool_a",
                    Result = new JObject { ["result"] = "a" },
                })
                .Add(new AIInteractionToolResult
                {
                    Id = "call_2",
                    Name = "tool_b",
                    Result = new JObject { ["result"] = "b" },
                })
                .Build();

            var request = new AIRequestCall
            {
                Provider = DeepSeekProvider.Instance.Name,
                Model = "deepseek-chat",
                Body = body,
                Capability = AICapability.Text2Text,
            };

            DeepSeekProvider.Instance.RefreshCachedSettings(new Dictionary<string, object>
            {
                { "ApiKey", "test-key" },
                { "MaxTokens", 1024 },
                { "Temperature", 0.7 },
                { "TopP", 1.0 },
                { "ReasoningEffort", "none" },
            });

            var encodedJson = DeepSeekProvider.Instance.Encode(request);
            var requestObj = JObject.Parse(encodedJson);
            var messages = requestObj["messages"] as JArray;

            Assert.NotNull(messages);

            var assistantWithToolCallsIndex = -1;
            for (int i = 0; i < messages.Count; i++)
            {
                var msg = messages[i] as JObject;
                if (string.Equals(msg?["role"]?.ToString(), "assistant", StringComparison.OrdinalIgnoreCase) &&
                    msg?["tool_calls"] is JArray tcArray && tcArray.Count > 0)
                {
                    assistantWithToolCallsIndex = i;
                    break;
                }
            }

            Assert.True(assistantWithToolCallsIndex >= 0, "Expected an assistant message with tool_calls");

            var toolCallIds = (messages[assistantWithToolCallsIndex]["tool_calls"] as JArray)
                .OfType<JObject>()
                .Select(tc => tc["id"]?.ToString())
                .Where(id => !string.IsNullOrEmpty(id))
                .ToList();

            Assert.Equal(2, toolCallIds.Count);
            Assert.Contains("call_1", toolCallIds);
            Assert.Contains("call_2", toolCallIds);

            var toolMessages = messages
                .Skip(assistantWithToolCallsIndex + 1)
                .TakeWhile(m =>
                {
                    var msg = m as JObject;
                    return string.Equals(msg?["role"]?.ToString(), "tool", StringComparison.OrdinalIgnoreCase);
                })
                .ToList();

            Assert.Equal(2, toolMessages.Count);
            var resultIds = toolMessages
                .Select(m => (m as JObject)?["tool_call_id"]?.ToString())
                .Where(id => !string.IsNullOrEmpty(id))
                .ToList();

            Assert.Contains("call_1", resultIds);
            Assert.Contains("call_2", resultIds);
        }
    }
}

#endif
