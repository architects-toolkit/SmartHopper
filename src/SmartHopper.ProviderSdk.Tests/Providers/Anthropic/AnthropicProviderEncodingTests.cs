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
    using Newtonsoft.Json.Linq;
    using SmartHopper.Providers.Anthropic;
    using SmartHopper.ProviderSdk.AICall.Core;
    using SmartHopper.ProviderSdk.AICall.Core.Base;
    using SmartHopper.ProviderSdk.AICall.Core.Interactions;
    using SmartHopper.ProviderSdk.AICall.Core.Requests;
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

        private static AIRequestCall BuildRequest(string model, IReadOnlyDictionary<string, JToken> extras = null)
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
                Parameters = new AIRequestParameters { Extras = extras },
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
        /// Models older than the effort family keep receiving the configured
        /// temperature plus caller-supplied top_p/top_k extras.
        /// </summary>
        [Theory]
        [InlineData("claude-sonnet-4-5-20250929")]
        [InlineData("claude-opus-4-5-20251101")]
        [InlineData("claude-haiku-4-5-20251001")]
        [InlineData("claude-3-5-haiku-20241022")]
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
    }
}

#endif
