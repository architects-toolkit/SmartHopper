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

namespace SmartHopper.ProviderSdk.Tests.Providers.Anthropic
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Newtonsoft.Json.Linq;
    using SmartHopper.Providers.Anthropic;
    using SmartHopper.ProviderSdk.AICall.Core;
    using SmartHopper.ProviderSdk.AICall.Core.Base;
    using SmartHopper.ProviderSdk.AICall.Core.Interactions;
    using SmartHopper.ProviderSdk.AICall.Core.Requests;
    using SmartHopper.ProviderSdk.AICall.Metrics;
    using SmartHopper.ProviderSdk.AIModels;
    using SmartHopper.ProviderSdk.Hosting;
    using SmartHopper.ProviderSdk.Tests.TestHelpers;
    using Xunit;

    /// <summary>
    /// Encoding tests for the Anthropic provider, covering the sampling-parameter
    /// gate for the effort-capable model family: temperature/top_p/top_k are
    /// rejected with HTTP 400 on those models, so Encode must omit them entirely.
    /// </summary>
    [Collection("ProviderSdk")]
    public sealed class AnthropicProviderEncodingTests : IDisposable
    {
        private readonly IProviderRegistryHost previousRegistry;

        public AnthropicProviderEncodingTests()
        {
            this.previousRegistry = ProviderSdkHost.ProviderRegistry;
            ProviderSdkHost.ProviderRegistry = new FakeProviderRegistryHost(AnthropicProvider.Instance);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            ProviderSdkHost.ProviderRegistry = this.previousRegistry;
        }

        private static AIRequestCall BuildRequest(string model, IReadOnlyDictionary<string, JToken> extras = null, int? maxTokens = null)
        {
            var body = AIBodyBuilder.Create()
                .Add(new AIInteractionText { Agent = AIAgent.User, Content = "Hello." })
                .Build();

            return new AIRequestCall
            {
                Provider = AnthropicProvider.Instance.Name,
                Model = model,
                Body = body,
                Capability = AICapability.Text2Text,
                Parameters = new AIRequestParameters { Extras = extras, MaxTokens = maxTokens },
            };
        }

        private static void RefreshSettings()
        {
            AnthropicProvider.Instance.RefreshCachedSettings(new Dictionary<string, object>
            {
                { "ApiKey", "test-key" },
                { "MaxTokens", 1024 },
                { "Temperature", 0.7 },
            });
        }

        /// <summary>
        /// Effort-capable models (5.x plus opus-4-6/4-7/4-8 and sonnet-4-6) reject
        /// non-default sampling parameters with HTTP 400; Encode must drop the
        /// configured temperature and any top_p/top_k extras.
        /// </summary>
        [Theory]
        [InlineData("claude-opus-4-8")]
        [InlineData("claude-opus-4-7")]
        [InlineData("claude-opus-4-6")]
        [InlineData("claude-sonnet-4-6")]
        [InlineData("claude-opus-5")]
        [InlineData("claude-sonnet-5-5")]
        [InlineData("claude-haiku-5-5")]
        [InlineData("claude-fable-5-1")]
        public void Encode_EffortFamilyModel_OmitsSamplingParameters(string model)
        {
            RefreshSettings();

            var request = BuildRequest(model, new Dictionary<string, JToken>
            {
                ["top_p"] = new JValue(0.9),
                ["top_k"] = new JValue(40),
            });

            var requestObj = JObject.Parse(AnthropicProvider.Instance.Encode(request));

            Assert.Null(requestObj["temperature"]);
            Assert.Null(requestObj["top_p"]);
            Assert.Null(requestObj["top_k"]);
        }

        /// <summary>
        /// Models older than the thinking family (3.5 and below) keep receiving
        /// the configured temperature plus caller-supplied top_p/top_k extras.
        /// Extended-thinking models (4.5/3.7) are covered separately: thinking
        /// is on by default and forces temperature=1 with sampling extras removed.
        /// </summary>
        [Theory]
        [InlineData("claude-3-5-haiku-20241022")]
        [InlineData("claude-3-haiku-20240307")]
        public void Encode_LegacyFamilyModel_EmitsSamplingParameters(string model)
        {
            RefreshSettings();

            var request = BuildRequest(model, new Dictionary<string, JToken>
            {
                ["top_p"] = new JValue(0.9),
                ["top_k"] = new JValue(40),
            });

            var requestObj = JObject.Parse(AnthropicProvider.Instance.Encode(request));

            Assert.Equal(0.7, requestObj["temperature"]?.Value<double>());
            Assert.Equal(0.9, requestObj["top_p"]?.Value<double>());
            Assert.Equal(40, requestObj["top_k"]?.Value<int>());
        }

        /// <summary>
        /// cache_ttl adds a ttl to every emitted cache_control object —
        /// both the top-level automatic breakpoint and the system-block breakpoint.
        /// </summary>
        [Fact]
        public void Encode_CacheTtl_EmitsTtlOnCacheControl()
        {
            RefreshSettings();

            var body = AIBodyBuilder.Create()
                .Add(new AIInteractionText { Agent = AIAgent.System, Content = "Stable system prompt." })
                .Add(new AIInteractionText { Agent = AIAgent.User, Content = "Hello." })
                .Build();

            var request = new AIRequestCall
            {
                Provider = AnthropicProvider.Instance.Name,
                Model = "claude-sonnet-4-5-20250929",
                Body = body,
                Capability = AICapability.Text2Text,
                Parameters = new AIRequestParameters
                {
                    Extras = new Dictionary<string, JToken>
                    {
                        ["enable_caching"] = new JValue(true),
                        ["cache_ttl"] = new JValue("1h"),
                    },
                },
            };

            var requestObj = JObject.Parse(AnthropicProvider.Instance.Encode(request));

            Assert.Equal("ephemeral", requestObj["cache_control"]?["type"]?.ToString());
            Assert.Equal("1h", requestObj["cache_control"]?["ttl"]?.ToString());
            var systemBlock = requestObj["system"]?[0] as JObject;
            Assert.Equal("1h", systemBlock?["cache_control"]?["ttl"]?.ToString());
        }

        private static void RefreshSettingsWithEffort(string effort = "medium")
        {
            AnthropicProvider.Instance.RefreshCachedSettings(new Dictionary<string, object>
            {
                { "ApiKey", "test-key" },
                { "MaxTokens", 16384 },
                { "Temperature", 0.7 },
                { "ReasoningEffort", effort },
            });
        }

        /// <summary>
        /// Adaptive families (4.6+, 5.x, fable/mythos) require a request-side
        /// thinking object — without it they never think (4.6-4.8) or think
        /// invisibly (5.x display defaults to "omitted").
        /// </summary>
        [Theory]
        [InlineData("claude-sonnet-4-6")]
        [InlineData("claude-opus-4-7")]
        [InlineData("claude-opus-4-8")]
        [InlineData("claude-opus-5")]
        [InlineData("claude-sonnet-5-5")]
        [InlineData("claude-haiku-5-5")]
        [InlineData("claude-fable-5-1")]
        public void Encode_AdaptiveFamily_EmitsAdaptiveThinkingSummarized(string model)
        {
            RefreshSettingsWithEffort("high");

            var requestObj = JObject.Parse(AnthropicProvider.Instance.Encode(BuildRequest(model)));

            Assert.Equal("adaptive", requestObj["thinking"]?["type"]?.ToString());
            Assert.Equal("summarized", requestObj["thinking"]?["display"]?.ToString());
            Assert.Null(requestObj["thinking"]?["budget_tokens"]);
        }

        /// <summary>
        /// 4.5-era and 3.7 models only accept manual extended thinking:
        /// thinking:{type:"enabled",budget_tokens} with temperature forced to 1
        /// and top_p/top_k removed.
        /// </summary>
        [Theory]
        [InlineData("claude-sonnet-4-5-20250929", 8192)]
        [InlineData("claude-haiku-4-5-20251001", 8192)]
        [InlineData("claude-opus-4-5-20251101", 8192)]
        [InlineData("claude-3-7-sonnet-20250219", 8192)]
        public void Encode_ExtendedFamily_EmitsEnabledThinkingBudget(string model, int expectedBudget)
        {
            RefreshSettingsWithEffort("high");

            var request = BuildRequest(model, new Dictionary<string, JToken>
            {
                ["top_p"] = new JValue(0.9),
                ["top_k"] = new JValue(40),
            });
            var requestObj = JObject.Parse(AnthropicProvider.Instance.Encode(request));

            Assert.Equal("enabled", requestObj["thinking"]?["type"]?.ToString());
            Assert.Equal(expectedBudget, requestObj["thinking"]?["budget_tokens"]?.Value<int>());
            Assert.Equal(1, requestObj["temperature"]?.Value<int>());
            Assert.Null(requestObj["top_p"]);
            Assert.Null(requestObj["top_k"]);
        }

        /// <summary>
        /// Effort maps onto the extended-thinking budget (low→1024, medium→4096,
        /// high→8192, xhigh→16384, max→32768), always clamped below max_tokens.
        /// </summary>
        [Theory]
        [InlineData("low", 1024)]
        [InlineData("medium", 4096)]
        [InlineData("xhigh", 16384)]
        [InlineData("max", 32768)]
        public void Encode_ExtendedFamily_MapsEffortToBudget(string effort, int expectedBudget)
        {
            AnthropicProvider.Instance.RefreshCachedSettings(new Dictionary<string, object>
            {
                { "ApiKey", "test-key" },
                { "MaxTokens", 40000 },
                { "ReasoningEffort", effort },
            });

            var requestObj = JObject.Parse(AnthropicProvider.Instance.Encode(BuildRequest("claude-sonnet-4-5-20250929")));

            Assert.Equal(expectedBudget, requestObj["thinking"]?["budget_tokens"]?.Value<int>());
        }

        /// <summary>
        /// Budget must stay strictly below max_tokens — when max_tokens leaves no
        /// room for the 1024 minimum, the thinking field is omitted entirely.
        /// </summary>
        [Fact]
        public void Encode_ExtendedFamily_DropsThinkingWhenBudgetUnsatisfiable()
        {
            AnthropicProvider.Instance.RefreshCachedSettings(new Dictionary<string, object>
            {
                { "ApiKey", "test-key" },
                { "MaxTokens", 1024 },
                { "ReasoningEffort", "medium" },
            });

            var requestObj = JObject.Parse(AnthropicProvider.Instance.Encode(BuildRequest("claude-sonnet-4-5-20250929")));

            Assert.Null(requestObj["thinking"]);
        }

        /// <summary>
        /// Models too old for thinking (3.5 and earlier) receive no thinking field.
        /// </summary>
        [Theory]
        [InlineData("claude-3-5-haiku-20241022")]
        [InlineData("claude-3-haiku-20240307")]
        public void Encode_NoThinkingFamily_OmitsThinkingField(string model)
        {
            RefreshSettingsWithEffort("high");

            var requestObj = JObject.Parse(AnthropicProvider.Instance.Encode(BuildRequest(model)));

            Assert.Null(requestObj["thinking"]);
        }

        /// <summary>
        /// Thinking blocks decode onto the Reasoning channel and are captured
        /// verbatim (signature included) into ProviderData for replay.
        /// </summary>
        [Fact]
        public void Decode_ThinkingBlocks_CaptureReasoningAndSignature()
        {
            var response = JObject.Parse(@"{
                'content': [
                    { 'type': 'thinking', 'thinking': 'First I consider...', 'signature': 'sig-abc' },
                    { 'type': 'redacted_thinking', 'data': 'opaque-blob' },
                    { 'type': 'text', 'text': 'Final answer.' }
                ],
                'stop_reason': 'end_turn',
                'usage': { 'input_tokens': 10, 'output_tokens': 50, 'output_tokens_details': { 'thinking_tokens': 30 } }
            }");

            var interactions = AnthropicProvider.Instance.Decode(response);
            var text = Assert.IsType<AIInteractionText>(Assert.Single(interactions, i => i is AIInteractionText));

            Assert.Equal("Final answer.", text.Content);
            Assert.Contains("First I consider...", text.Reasoning);
            Assert.Contains("[redacted thinking]", text.Reasoning);

            var thinking = text.ProviderData?["thinking"] as JArray;
            Assert.NotNull(thinking);
            Assert.Equal(2, thinking.Count);
            Assert.Equal("sig-abc", thinking[0]?["signature"]?.ToString());
            Assert.Equal("opaque-blob", thinking[1]?["data"]?.ToString());
            Assert.Equal(30, text.Metrics.Quantity(AIQuantityKey.InternalReasoning, AIPriceUnit.Token));
        }

        /// <summary>
        /// Stored thinking blocks replay verbatim ahead of the text block in the
        /// assistant message — Anthropic requires them echoed back unchanged.
        /// </summary>
        [Fact]
        public void Encode_ProviderDataThinking_ReplaysBlocksAheadOfText()
        {
            RefreshSettingsWithEffort("medium");

            var body = AIBodyBuilder.Create()
                .Add(new AIInteractionText { Agent = AIAgent.User, Content = "Q" })
                .Add(new AIInteractionText
                {
                    Agent = AIAgent.Assistant,
                    Content = "A",
                    ProviderData = new JObject
                    {
                        ["thinking"] = new JArray
                        {
                            new JObject { ["type"] = "thinking", ["thinking"] = "…", ["signature"] = "sig-xyz" },
                            new JObject { ["type"] = "redacted_thinking", ["data"] = "blob" },
                        },
                    },
                })
                .Build();

            var request = new AIRequestCall
            {
                Provider = AnthropicProvider.Instance.Name,
                Model = "claude-sonnet-4-6",
                Body = body,
                Capability = AICapability.Text2Text,
            };

            var requestObj = JObject.Parse(AnthropicProvider.Instance.Encode(request));
            var assistantMsg = requestObj["messages"]?.FirstOrDefault(m => m["role"]?.ToString() == "assistant");
            var blocks = assistantMsg?["content"] as JArray;

            Assert.NotNull(blocks);
            Assert.Equal("thinking", blocks[0]?["type"]?.ToString());
            Assert.Equal("sig-xyz", blocks[0]?["signature"]?.ToString());
            Assert.Equal("redacted_thinking", blocks[1]?["type"]?.ToString());
            Assert.Equal("text", blocks[2]?["type"]?.ToString());
        }

        /// <summary>
        /// Anthropic's API requires max_tokens, so a request-level MaxTokens of 0
        /// (unlimited elsewhere) degrades to the configured provider setting
        /// instead of emitting a literal 0.
        /// </summary>
        [Fact]
        public void Encode_RequestMaxTokensZero_FallsBackToConfiguredSetting()
        {
            RefreshSettings();

            var requestObj = JObject.Parse(AnthropicProvider.Instance.Encode(BuildRequest("claude-sonnet-4-6", maxTokens: 0)));

            Assert.Equal(1024, requestObj["max_tokens"]?.Value<int>());
        }
    }
}

#endif
