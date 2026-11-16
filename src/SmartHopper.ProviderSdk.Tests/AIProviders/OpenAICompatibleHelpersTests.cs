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

namespace SmartHopper.ProviderSdk.Tests.AIProviders
{
    using Newtonsoft.Json.Linq;
    using SmartHopper.ProviderSdk.AICall.Core.Requests;
    using SmartHopper.ProviderSdk.AICall.JsonSchemas;
    using SmartHopper.ProviderSdk.AICall.Metrics;
    using SmartHopper.ProviderSdk.Tests.TestHelpers;
    using Xunit;

    /// <summary>
    /// Tests shared request and usage helpers for OpenAI-compatible providers.
    /// </summary>
    [Collection("ProviderSdk")]
    public sealed class OpenAICompatibleHelpersTests
    {
        /// <summary>
        /// The shared decoder supports both API token field families and nested details.
        /// </summary>
        [Fact]
        public void DecodeCompatibleMetrics_ReadsCachedAndReasoningTokens()
        {
            var provider = new FakeOpenAICompatibleProvider();
            var response = new JObject
            {
                ["choices"] = new JArray(new JObject { ["finish_reason"] = "stop" }),
                ["usage"] = new JObject
                {
                    ["input_tokens"] = 100,
                    ["input_tokens_details"] = new JObject { ["cached_tokens"] = 25 },
                    ["output_tokens"] = 40,
                    ["output_tokens_details"] = new JObject { ["reasoning_tokens"] = 15 },
                },
            };

            AIMetrics metrics = provider.DecodeCompatibleMetrics(response);

            Assert.Equal(25, metrics.InputTokensCached);
            Assert.Equal(75, metrics.InputTokensPrompt);
            Assert.Equal(25, metrics.OutputTokensGeneration);
            Assert.Equal(15, metrics.OutputTokensReasoning);
            Assert.Equal(40, metrics.OutputTokens);
            Assert.Equal("stop", metrics.FinishReason);
        }

        /// <summary>
        /// The shared decoder separates reasoning tokens from total chat-completion output tokens.
        /// </summary>
        [Fact]
        public void DecodeCompatibleMetrics_ChatCompletionsShape_SplitsReasoningFromGeneration()
        {
            var provider = new FakeOpenAICompatibleProvider();
            var response = new JObject
            {
                ["usage"] = new JObject
                {
                    ["prompt_tokens"] = 200,
                    ["prompt_tokens_details"] = new JObject { ["cached_tokens"] = 50 },
                    ["completion_tokens"] = 120,
                    ["completion_tokens_details"] = new JObject { ["reasoning_tokens"] = 100 },
                },
            };

            AIMetrics metrics = provider.DecodeCompatibleMetrics(response);

            Assert.Equal(150, metrics.InputTokensPrompt);
            Assert.Equal(50, metrics.InputTokensCached);
            Assert.Equal(20, metrics.OutputTokensGeneration);
            Assert.Equal(100, metrics.OutputTokensReasoning);
            Assert.Equal(120, metrics.OutputTokens);
        }

        /// <summary>
        /// JSON <c>null</c> usage fields must not abort the decode: providers occasionally
        /// return explicit nulls for token counters (e.g. batch result items), and a single
        /// null value previously threw mid-decode, dropping every valid metric including
        /// the finish reason.
        /// </summary>
        [Fact]
        public void DecodeCompatibleMetrics_NullUsageFields_KeepValidMetrics()
        {
            var provider = new FakeOpenAICompatibleProvider();
            var response = new JObject
            {
                ["choices"] = new JArray(new JObject { ["finish_reason"] = "stop" }),
                ["usage"] = new JObject
                {
                    ["prompt_tokens"] = null,
                    ["prompt_tokens_details"] = null,
                    ["completion_tokens"] = 50,
                    ["completion_tokens_details"] = new JObject { ["reasoning_tokens"] = null },
                },
            };

            AIMetrics metrics = provider.DecodeCompatibleMetrics(response);

            Assert.Equal(0, metrics.InputTokens);
            Assert.Equal(50, metrics.OutputTokens);
            Assert.Equal("stop", metrics.FinishReason);
        }

        /// <summary>
        /// A completely null <c>usage</c> node decodes to zeroed metrics instead of throwing.
        /// </summary>
        [Fact]
        public void DecodeCompatibleMetrics_NullUsageNode_ReturnsZeroedMetrics()
        {
            var provider = new FakeOpenAICompatibleProvider();
            var response = new JObject
            {
                ["choices"] = new JArray(new JObject { ["finish_reason"] = "stop" }),
                ["usage"] = null,
            };

            AIMetrics metrics = provider.DecodeCompatibleMetrics(response);

            Assert.Equal(0, metrics.InputTokens);
            Assert.Equal(0, metrics.OutputTokens);
            Assert.Equal("stop", metrics.FinishReason);
        }

        /// <summary>
        /// Responses API bodies carry no <c>choices</c>; the top-level <c>status</c> is
        /// the finish signal. <c>completed</c> maps to the normal-termination
        /// equivalent <c>stop</c>.
        /// </summary>
        [Fact]
        public void DecodeCompatibleMetrics_ResponsesApiStatus_MapsCompletedToStop()
        {
            var provider = new FakeOpenAICompatibleProvider();
            var response = new JObject
            {
                ["status"] = "completed",
                ["usage"] = new JObject
                {
                    ["input_tokens"] = 10,
                    ["output_tokens"] = 5,
                },
            };

            AIMetrics metrics = provider.DecodeCompatibleMetrics(response);

            Assert.Equal("stop", metrics.FinishReason);
        }

        /// <summary>
        /// A non-completed Responses API <c>status</c> is a real abnormal reason and
        /// is surfaced verbatim rather than normalized away.
        /// </summary>
        [Fact]
        public void DecodeCompatibleMetrics_ResponsesApiStatus_KeepsAbnormalStatus()
        {
            var provider = new FakeOpenAICompatibleProvider();
            var response = new JObject
            {
                ["status"] = "incomplete",
                ["usage"] = new JObject
                {
                    ["input_tokens"] = 10,
                    ["output_tokens"] = 5,
                },
            };

            AIMetrics metrics = provider.DecodeCompatibleMetrics(response);

            Assert.Equal("incomplete", metrics.FinishReason);
        }

        /// <summary>
        /// <c>incomplete_details.reason = "max_output_tokens"</c> is the Responses API
        /// equivalent of Chat Completions <c>length</c> and normalizes to it.
        /// </summary>
        [Fact]
        public void DecodeCompatibleMetrics_ResponsesApiStatus_MapsMaxOutputTokensToLength()
        {
            var provider = new FakeOpenAICompatibleProvider();
            var response = new JObject
            {
                ["status"] = "incomplete",
                ["incomplete_details"] = new JObject { ["reason"] = "max_output_tokens" },
                ["usage"] = new JObject
                {
                    ["input_tokens"] = 10,
                    ["output_tokens"] = 5,
                },
            };

            AIMetrics metrics = provider.DecodeCompatibleMetrics(response);

            Assert.Equal("length", metrics.FinishReason);
        }

        /// <summary>
        /// Chat Completions <c>choices[0].finish_reason</c> takes precedence over a
        /// Responses API <c>status</c> when a body carries both.
        /// </summary>
        [Fact]
        public void DecodeCompatibleMetrics_ChatChoiceWinsOverStatus()
        {
            var provider = new FakeOpenAICompatibleProvider();
            var response = new JObject
            {
                ["choices"] = new JArray(new JObject { ["finish_reason"] = "length" }),
                ["status"] = "completed",
                ["usage"] = new JObject
                {
                    ["input_tokens"] = 10,
                    ["output_tokens"] = 5,
                },
            };

            AIMetrics metrics = provider.DecodeCompatibleMetrics(response);

            Assert.Equal("length", metrics.FinishReason);
        }

        [Fact]
        public void ApplyCompatibleToolChoice_FormatsForcedToolCall()
        {
            var provider = new FakeOpenAICompatibleProvider();
            var request = new AIRequestCall
            {
                ForceToolCall = true,
                ForceToolName = "lookup",
            };
            var requestBody = new JObject();
            var tools = new JArray(new JObject { ["type"] = "function" });

            provider.ApplyCompatibleToolChoice(requestBody, request, tools);

            Assert.Same(tools, requestBody["tools"]);
            Assert.Equal("function", requestBody["tool_choice"]?["type"]?.ToString());
            Assert.Equal("lookup", requestBody["tool_choice"]?["function"]?["name"]?.ToString());
        }

        /// <summary>
        /// The shared schema wrapper returns the transformed schema and stores matching wrapper metadata.
        /// </summary>
        [Fact]
        public void TryWrapJsonSchema_ValidSchema_ReturnsWrappedSchemaAndStoresWrapperInfo()
        {
            var provider = new FakeOpenAICompatibleProvider();
            var schema = "{\"type\":\"object\",\"properties\":{\"a\":{\"type\":\"string\"}}}";

            var result = provider.TryWrapCompatibleJsonSchema(schema, out var wrappedSchema, out var wrapperInfo);

            Assert.True(result);
            Assert.NotNull(wrappedSchema);
            Assert.Equal(FakeOpenAICompatibleProvider.ProviderName, wrapperInfo.ProviderName);

            var currentWrapperInfo = JsonSchemaService.Instance.GetCurrentWrapperInfo();
            Assert.NotNull(currentWrapperInfo);
            Assert.Equal(wrapperInfo.IsWrapped, currentWrapperInfo.IsWrapped);
            Assert.Equal(wrapperInfo.ProviderName, currentWrapperInfo.ProviderName);
        }

        /// <summary>
        /// The shared schema wrapper rejects invalid JSON and resets wrapper metadata.
        /// </summary>
        [Fact]
        public void TryWrapJsonSchema_InvalidJson_ReturnsFalseAndResetsWrapperInfo()
        {
            var provider = new FakeOpenAICompatibleProvider();

            var result = provider.TryWrapCompatibleJsonSchema("{not json", out var wrappedSchema, out var wrapperInfo);

            Assert.False(result);
            Assert.Null(wrappedSchema);
            Assert.False(wrapperInfo.IsWrapped);

            var currentWrapperInfo = JsonSchemaService.Instance.GetCurrentWrapperInfo();
            Assert.NotNull(currentWrapperInfo);
            Assert.False(currentWrapperInfo.IsWrapped);
        }
    }
}
