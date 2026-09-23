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
            Assert.Equal(40, metrics.OutputTokensGeneration);
            Assert.Equal(15, metrics.OutputTokensReasoning);
            Assert.Equal("stop", metrics.FinishReason);
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
    }
}
}
