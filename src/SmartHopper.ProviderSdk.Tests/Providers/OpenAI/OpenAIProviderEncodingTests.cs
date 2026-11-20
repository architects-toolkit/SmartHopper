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

namespace SmartHopper.ProviderSdk.Tests.Providers.OpenAI
{
    using System;
    using System.Collections.Generic;
    using Newtonsoft.Json.Linq;
    using SmartHopper.Providers.OpenAI;
    using SmartHopper.ProviderSdk.AICall.Core;
    using SmartHopper.ProviderSdk.AICall.Core.Base;
    using SmartHopper.ProviderSdk.AICall.Core.Interactions;
    using SmartHopper.ProviderSdk.AICall.Core.Requests;
    using SmartHopper.ProviderSdk.AIModels;
    using SmartHopper.ProviderSdk.Hosting;
    using SmartHopper.ProviderSdk.Tests.TestHelpers;
    using Xunit;

    /// <summary>
    /// Encoding tests for the OpenAI provider, covering the sampling-parameter gate
    /// for the reasoning family (o-series and gpt-5 models): caller-supplied extras
    /// such as temperature/top_p/penalties/logprobs must not leak into the request,
    /// matching the main path that already omits temperature for those models.
    /// </summary>
    [Collection("ProviderSdk")]
    public sealed class OpenAIProviderEncodingTests : IDisposable
    {
        private readonly IProviderRegistryHost previousRegistry;

        public OpenAIProviderEncodingTests()
        {
            this.previousRegistry = ProviderSdkHost.ProviderRegistry;
            ProviderSdkHost.ProviderRegistry = new FakeProviderRegistryHost(OpenAIProvider.Instance);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            ProviderSdkHost.ProviderRegistry = this.previousRegistry;
        }

        private static AIRequestCall BuildRequest(string model, string endpoint, IReadOnlyDictionary<string, JToken> extras)
        {
            var body = AIBodyBuilder.Create()
                .Add(new AIInteractionText { Agent = AIAgent.User, Content = "Hello." })
                .Build();

            return new AIRequestCall
            {
                Provider = OpenAIProvider.Instance.Name,
                Model = model,
                Endpoint = endpoint,
                Body = body,
                Capability = AICapability.Text2Text,
                Parameters = new AIRequestParameters { Extras = extras },
            };
        }

        private static IReadOnlyDictionary<string, JToken> SamplingExtras()
            => new Dictionary<string, JToken>
            {
                ["temperature"] = new JValue(0.9),
                ["top_p"] = new JValue(0.9),
                ["presence_penalty"] = new JValue(0.5),
                ["frequency_penalty"] = new JValue(0.5),
                ["logprobs"] = new JValue(true),
                ["top_logprobs"] = new JValue(3),
            };

        private static void RefreshSettings(int maxTokens = 1024)
        {
            OpenAIProvider.Instance.RefreshCachedSettings(new Dictionary<string, object>
            {
                { "ApiKey", "test-key" },
                { "MaxTokens", maxTokens },
                { "Temperature", 0.7 },
                { "ReasoningEffort", "medium" },
            });
        }

        /// <summary>
        /// Reasoning-family models reject sampling parameters with HTTP 400 on the
        /// Responses API; Encode must drop them even when supplied via extras.
        /// </summary>
        [Theory]
        [InlineData("gpt-5.2")]
        [InlineData("o3")]
        public void Encode_ResponsesApi_ReasoningFamily_DropsSamplingExtras(string model)
        {
            RefreshSettings();

            var request = BuildRequest(model, "/responses", SamplingExtras());
            var requestObj = JObject.Parse(OpenAIProvider.Instance.Encode(request));

            Assert.Null(requestObj["temperature"]);
            Assert.Null(requestObj["top_p"]);
            Assert.Null(requestObj["presence_penalty"]);
            Assert.Null(requestObj["frequency_penalty"]);
            Assert.Null(requestObj["logprobs"]);
            Assert.Null(requestObj["top_logprobs"]);
        }

        /// <summary>
        /// Non-reasoning models keep receiving caller-supplied sampling extras.
        /// </summary>
        [Fact]
        public void Encode_ResponsesApi_LegacyFamily_KeepsSamplingExtras()
        {
            RefreshSettings();

            var request = BuildRequest("gpt-4o", "/responses", SamplingExtras());
            var requestObj = JObject.Parse(OpenAIProvider.Instance.Encode(request));

            Assert.Equal(0.9, requestObj["temperature"]?.Value<double>());
            Assert.Equal(0.9, requestObj["top_p"]?.Value<double>());
            Assert.Equal(0.5, requestObj["presence_penalty"]?.Value<double>());
            Assert.Equal(0.5, requestObj["frequency_penalty"]?.Value<double>());
        }

        /// <summary>
        /// Chat Completions follows the same rule: the reasoning family gets
        /// reasoning_effort/max_completion_tokens while sampling extras are dropped.
        /// </summary>
        [Theory]
        [InlineData("gpt-5.2")]
        [InlineData("o3")]
        public void Encode_ChatCompletions_ReasoningFamily_DropsSamplingExtras(string model)
        {
            RefreshSettings();

            var request = BuildRequest(model, "/chat/completions", SamplingExtras());
            var requestObj = JObject.Parse(OpenAIProvider.Instance.Encode(request));

            Assert.Null(requestObj["temperature"]);
            Assert.Null(requestObj["top_p"]);
            Assert.Null(requestObj["presence_penalty"]);
            Assert.Null(requestObj["frequency_penalty"]);
            Assert.Null(requestObj["logprobs"]);
            Assert.Null(requestObj["top_logprobs"]);
            Assert.NotNull(requestObj["reasoning_effort"]);
            Assert.NotNull(requestObj["max_completion_tokens"]);
        }

        private static AIRequestCall BuildImageRequest(string model, IReadOnlyDictionary<string, JToken> extras, string size = "1024x1024", string aspectRatio = null)
        {
            var body = AIBodyBuilder.Create()
                .AddImageRequest("a cube", size, aspectRatio)
                .Build();

            return new AIRequestCall
            {
                Provider = OpenAIProvider.Instance.Name,
                Model = model,
                Endpoint = "/images/generations",
                Body = body,
                Capability = AICapability.TextInput | AICapability.ImageOutput,
                Parameters = new AIRequestParameters { Extras = extras },
            };
        }

        /// <summary>
        /// The shared size/aspect-ratio concepts normalize to OpenAI's size vocabulary:
        /// gpt-image picks 1536x1024/1024x1536 by orientation, dall-e-3 the 1792 variants.
        /// </summary>
        [Theory]
        [InlineData("gpt-image-1", "1024x1024", null, "1024x1024")]
        [InlineData("gpt-image-1", "1024x1024", "16:9", "1536x1024")]
        [InlineData("gpt-image-1", "1024x1024", "9:16", "1024x1536")]
        [InlineData("gpt-image-1", "auto", null, "auto")]
        [InlineData("dall-e-3", "1024x1024", "16:9", "1792x1024")]
        [InlineData("dall-e-3", "1024x1024", "9:16", "1024x1792")]
        public void Encode_ImageRequest_NormalizesSize(string model, string size, string aspectRatio, string expected)
        {
            var request = BuildImageRequest(model, null, size, aspectRatio);
            var requestObj = JObject.Parse(OpenAIProvider.Instance.Encode(request));

            Assert.Equal(expected, requestObj["size"]?.ToString());
            Assert.Equal("a cube", requestObj["prompt"]?.ToString());
            Assert.Null(requestObj["style"]);
        }

        /// <summary>
        /// OpenAI image extras map to the images API fields; other extras
        /// are ignored.
        /// </summary>
        [Fact]
        public void Encode_ImageRequest_AppliesOpenAiExtras()
        {
            var request = BuildImageRequest("gpt-image-1", new Dictionary<string, JToken>
            {
                ["quality"] = new JValue("high"),
                ["background"] = new JValue("transparent"),
                ["output_format"] = new JValue("png"),
                ["output_compression"] = new JValue(80),
                ["unrelated_extra"] = new JValue("ignored"),
            });

            var requestObj = JObject.Parse(OpenAIProvider.Instance.Encode(request));

            Assert.Equal("high", requestObj["quality"]?.ToString());
            Assert.Equal("transparent", requestObj["background"]?.ToString());
            Assert.Equal("png", requestObj["output_format"]?.ToString());
            Assert.Equal(80, requestObj["output_compression"]?.Value<int?>());
            Assert.Null(requestObj["unrelated_extra"]);
            Assert.Null(requestObj["style"]);
        }

        /// <summary>
        /// Non-reasoning models on Chat Completions keep temperature and extras.
        /// </summary>
        [Fact]
        public void Encode_ChatCompletions_LegacyFamily_KeepsSamplingExtras()
        {
            RefreshSettings();

            var request = BuildRequest("gpt-4o", "/chat/completions", SamplingExtras());
            var requestObj = JObject.Parse(OpenAIProvider.Instance.Encode(request));

            Assert.Equal(0.9, requestObj["top_p"]?.Value<double>());
            Assert.Equal(0.5, requestObj["presence_penalty"]?.Value<double>());
            Assert.Equal(0.5, requestObj["frequency_penalty"]?.Value<double>());
            Assert.Equal(true, requestObj["logprobs"]?.Value<bool>());
            Assert.Equal(3, requestObj["top_logprobs"]?.Value<int>());
        }

        /// <summary>
        /// A MaxTokens setting of 0 means unlimited: Chat Completions omits the cap
        /// entirely (neither max_tokens nor max_completion_tokens is sent).
        /// </summary>
        [Theory]
        [InlineData("gpt-4o")]
        [InlineData("o3")]
        public void Encode_ChatCompletions_MaxTokensZero_OmitsTokenCap(string model)
        {
            RefreshSettings(maxTokens: 0);

            var request = BuildRequest(model, "/chat/completions", null);
            var requestObj = JObject.Parse(OpenAIProvider.Instance.Encode(request));

            Assert.Null(requestObj["max_tokens"]);
            Assert.Null(requestObj["max_completion_tokens"]);
        }

        /// <summary>
        /// A MaxTokens setting of 0 means unlimited on the Responses API too:
        /// max_output_tokens is omitted.
        /// </summary>
        [Fact]
        public void Encode_ResponsesApi_MaxTokensZero_OmitsMaxOutputTokens()
        {
            RefreshSettings(maxTokens: 0);

            var request = BuildRequest("gpt-4o", "/responses", null);
            var requestObj = JObject.Parse(OpenAIProvider.Instance.Encode(request));

            Assert.Null(requestObj["max_output_tokens"]);
        }
    }
}

#endif
