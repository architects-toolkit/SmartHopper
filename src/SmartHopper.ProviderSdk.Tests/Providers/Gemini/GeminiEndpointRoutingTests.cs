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
namespace SmartHopper.ProviderSdk.Tests.Providers.Gemini
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Newtonsoft.Json.Linq;
    using SmartHopper.Providers.Gemini;
    using SmartHopper.ProviderSdk.AICall.Core;
    using SmartHopper.ProviderSdk.AICall.Core.Base;
    using SmartHopper.ProviderSdk.AICall.Core.Interactions;
    using SmartHopper.ProviderSdk.AICall.Core.Requests;
    using SmartHopper.ProviderSdk.AICall.Metrics;
    using SmartHopper.ProviderSdk.AIModels;
    using SmartHopper.ProviderSdk.AIProviders;
    using SmartHopper.ProviderSdk.Hosting;
    using Xunit;

    /// <summary>
    /// Unit tests for <see cref="GeminiProvider"/> endpoint routing in <c>PreCallAsync</c>:
    /// native Gemini API paths are preserved while foreign endpoint hints fall
    /// through to capability-based routing.
    /// </summary>
    [Collection("ProviderSdk")]
    public class GeminiEndpointRoutingTests : IDisposable
    {
        private readonly IProviderRegistryHost previousRegistry = ProviderSdkHost.ProviderRegistry;

        public GeminiEndpointRoutingTests()
        {
            // request.Model resolves through the provider registry; without a host
            // the model is empty and the routed endpoint loses the model segment.
            ProviderSdkHost.ProviderRegistry = new SingleProviderRegistry(GeminiProvider.Instance);
        }

        public void Dispose()
        {
            ProviderSdkHost.ProviderRegistry = this.previousRegistry;
        }

        /// <summary>
        /// The OpenAI-style "/images/generations" wire-path override is not a Gemini
        /// API path and must be remapped to the model's generateContent route for
        /// image-capable requests.
        /// </summary>
        [Fact]
        public async Task PreCallAsync_ImagesGenerationsHint_RoutesToGenerateContent()
        {
            var request = new AIRequestCall();
            request.Initialize(
                provider: "Gemini",
                model: string.Empty,
                systemPrompt: string.Empty,
                origin: string.Empty,
                capability: AICapability.TextInput | AICapability.ImageOutput);
            request.Endpoint = "/images/generations";

            var result = await GeminiProvider.Instance.PreCallAsync(request).ConfigureAwait(false);

            Assert.NotEqual("/images/generations", result.Endpoint);
            Assert.StartsWith("/models/", result.Endpoint, StringComparison.Ordinal);
            Assert.EndsWith(":generateContent", result.Endpoint, StringComparison.Ordinal);
            Assert.Equal("POST", result.HttpMethod);
            Assert.Equal("application/json", result.ContentType);
        }

        /// <summary>Real Gemini API paths supplied as wire-path overrides are preserved as given.</summary>
        [Fact]
        public async Task PreCallAsync_NativeModelsPath_IsPreserved()
        {
            var request = new AIRequestCall();
            request.Initialize(
                provider: "Gemini",
                model: string.Empty,
                systemPrompt: string.Empty,
                origin: string.Empty,
                capability: AICapability.TextInput | AICapability.TextOutput);
            request.Endpoint = "/models/gemini-test:generateContent";

            var result = await GeminiProvider.Instance.PreCallAsync(request).ConfigureAwait(false);

            Assert.Equal("/models/gemini-test:generateContent", result.Endpoint);
        }

        /// <summary>
        /// A "flex" service tier must serialize as the top-level camelCase
        /// <c>serviceTier</c> field with the lowercase enum spelling the v1beta
        /// ServiceTier enum accepts (flex/priority/standard/unspecified) —
        /// verified against the published REST contract after uppercase
        /// "FLEX" and "SERVICE_TIER_FLEX" were both rejected live.
        /// </summary>
        [Theory]
        [InlineData("flex", "flex")]
        [InlineData("priority", "priority")]
        [InlineData("FLEX", "flex")]
        [InlineData("SERVICE_TIER_FLEX", "flex")]
        [InlineData("TIER_PRIORITY", "priority")]
        public void Encode_ServiceTierExtra_SerializesGeminiContract(string extraValue, string expected)
        {
            var request = new AIRequestCall();
            request.Initialize(
                provider: "Gemini",
                model: string.Empty,
                systemPrompt: string.Empty,
                origin: null,
                capability: AICapability.TextInput | AICapability.TextOutput);
            request.Parameters = AIRequestParameters.Create()
                .WithExtra("service_tier", extraValue)
                .Build();

            var body = JObject.Parse(GeminiProvider.Instance.Encode(request));

            Assert.Equal(expected, body["serviceTier"]?.ToString());
            Assert.Null(body["service_tier"]);
        }

        /// <summary>
        /// An image-generation request interaction (prompt, no image data yet) must
        /// still produce a contents entry — Gemini requires a non-empty contents array.
        /// Previously the image branch emitted zero parts for request-mode
        /// interactions, producing "GenerateContentRequest.contents: contents is not specified".
        /// </summary>
        [Fact]
        public void Encode_ImageRequest_EmitsPromptAsTextPart()
        {
            var request = new AIRequestCall();
            request.Initialize(
                provider: "Gemini",
                model: string.Empty,
                body: AIBodyBuilder.Create()
                    .AddImageRequest(prompt: "a red cube")
                    .Build(),
                origin: string.Empty,
                capability: AICapability.TextInput | AICapability.ImageOutput);

            var body = JObject.Parse(GeminiProvider.Instance.Encode(request));

            var contents = body["contents"] as JArray;
            Assert.NotNull(contents);
            Assert.Single(contents);
            Assert.Equal("a red cube", contents[0]?["parts"]?[0]?["text"]?.ToString());
        }

        /// <summary>
        /// Shared size/aspect-ratio inputs must normalize to Gemini's imageConfig
        /// vocabulary: imageSize becomes a 1K/2K/4K tier by largest dimension and
        /// aspectRatio snaps to the closest supported ratio.
        /// </summary>
        [Theory]
        [InlineData("1024x1024", null, "1K", "1:1")]
        [InlineData("4096x4096", null, "4K", "1:1")]
        [InlineData("2K", "16:9", "2K", "16:9")]
        [InlineData("512x512", "7:4", "1K", "16:9")]
        public void Encode_ImageRequest_NormalizesImageConfig(string size, string aspectRatio, string expectedSize, string expectedRatio)
        {
            var request = new AIRequestCall();
            request.Initialize(
                provider: "Gemini",
                model: string.Empty,
                body: AIBodyBuilder.Create()
                    .AddImageRequest(prompt: "a cube", size: size, aspectRatio: aspectRatio)
                    .Build(),
                origin: string.Empty,
                capability: AICapability.TextInput | AICapability.ImageOutput);

            var body = JObject.Parse(GeminiProvider.Instance.Encode(request));

            var imageConfig = body["generationConfig"]?["imageConfig"] as JObject;
            Assert.NotNull(imageConfig);
            Assert.Equal(expectedSize, imageConfig["imageSize"]?.ToString());
            Assert.Equal(expectedRatio, imageConfig["aspectRatio"]?.ToString());
        }

        /// <summary>
        /// The negative prompt folds into the request's text content because the
        /// generateContent imageConfig contract has no dedicated negative-prompt
        /// field. Imagen-style fields (personGeneration/addWatermark/guidanceScale)
        /// are rejected by this API and are not emitted.
        /// </summary>
        [Fact]
        public void Encode_ImageRequest_AppliesGeminiExtras()
        {
            var request = new AIRequestCall();
            request.Initialize(
                provider: "Gemini",
                model: string.Empty,
                body: AIBodyBuilder.Create()
                    .AddImageRequest(prompt: "a cube")
                    .Build(),
                origin: string.Empty,
                capability: AICapability.TextInput | AICapability.ImageOutput);
            request.Parameters = AIRequestParameters.Create()
                .WithExtra("negative_prompt", "blurry edges")
                .Build();

            var body = JObject.Parse(GeminiProvider.Instance.Encode(request));

            var imageConfig = body["generationConfig"]?["imageConfig"] as JObject;
            Assert.NotNull(imageConfig);
            Assert.Null(imageConfig["negativePrompt"]);

            var parts = body["contents"]?[0]?["parts"] as JArray;
            Assert.Contains(parts, p => p["text"]?.ToString() == "Do not include: blurry edges");
        }

        /// <summary>
        /// media_resolution normalizes to the MEDIA_RESOLUTION_* enum spelling
        /// on generationConfig, controlling vision token cost for media inputs.
        /// The field only exists on Gemini 3+ models — the 2.x family rejects it
        /// with HTTP 400, so it is emitted only for the 3+ family.
        /// </summary>
        [Theory]
        [InlineData("low", "MEDIA_RESOLUTION_LOW")]
        [InlineData("MEDIA_RESOLUTION_HIGH", "MEDIA_RESOLUTION_HIGH")]
        [InlineData("ultra_high", "MEDIA_RESOLUTION_ULTRA_HIGH")]
        public void Encode_MediaResolutionExtra_SerializesEnum(string extraValue, string expected)
        {
            var request = new AIRequestCall();
            request.Initialize(
                provider: "Gemini",
                model: "gemini-3.8-flash",
                body: AIBodyBuilder.Create()
                    .Add(new AIInteractionText { Agent = AIAgent.User, Content = "describe this" })
                    .Build(),
                origin: string.Empty,
                capability: AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput);
            request.Parameters = AIRequestParameters.Create()
                .WithExtra("media_resolution", extraValue)
                .Build();

            var body = JObject.Parse(GeminiProvider.Instance.Encode(request));

            Assert.Equal(expected, body["generationConfig"]?["mediaResolution"]?.ToString());
        }

        /// <summary>
        /// media_resolution is suppressed on the Gemini 2.x family, which
        /// rejects the field outright, but is emitted for Gemini 3+ models.
        /// </summary>
        [Theory]
        [InlineData("gemini-2.5-flash", false)]
        [InlineData("gemini-2.0-flash", false)]
        [InlineData("gemini-3.1-flash-image", true)]
        [InlineData("gemini-4-argon", true)]
        public void Encode_MediaResolutionExtra_GatedToGemini3Plus(string model, bool emitted)
        {
            var request = new AIRequestCall();
            request.Initialize(
                provider: "Gemini",
                model: model,
                body: AIBodyBuilder.Create()
                    .Add(new AIInteractionText { Agent = AIAgent.User, Content = "describe this" })
                    .Build(),
                origin: string.Empty,
                capability: AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput);
            request.Parameters = AIRequestParameters.Create()
                .WithExtra("media_resolution", "low")
                .Build();

            var body = JObject.Parse(GeminiProvider.Instance.Encode(request));

            Assert.Equal(emitted, body["generationConfig"]?["mediaResolution"] != null);
        }

        /// <summary>
        /// The global ThinkingLevel setting feeds generationConfig.thinkingConfig when
        /// no per-request 'thinking_level' extra is present.
        /// </summary>
        [Fact]
        public void Encode_ThinkingLevelSetting_EmitsThinkingConfig()
        {
            var provider = GeminiProvider.Instance;
            provider.RefreshCachedSettings(new Dictionary<string, object> { ["ThinkingLevel"] = "high" });
            try
            {
                var request = new AIRequestCall();
                request.Initialize(
                    provider: "Gemini",
                    model: "gemini-3.1-flash",
                    body: AIBodyBuilder.Create()
                        .Add(new AIInteractionText { Agent = AIAgent.User, Content = "think" })
                        .Build(),
                    origin: string.Empty,
                    capability: AICapability.TextInput | AICapability.TextOutput);

                var body = JObject.Parse(provider.Encode(request));
                var thinkingConfig = body["generationConfig"]?["thinkingConfig"];

                Assert.Equal("high", thinkingConfig?["thinkingLevel"]?.ToString());
                Assert.Equal(true, thinkingConfig?["includeThoughts"]?.Value<bool>());
            }
            finally
            {
                provider.RefreshCachedSettings(new Dictionary<string, object> { ["ThinkingLevel"] = string.Empty });
            }
        }

        /// <summary>
        /// A per-request 'thinking_level' extra wins over the global ThinkingLevel setting.
        /// </summary>
        [Fact]
        public void Encode_ThinkingLevelExtra_OverridesSetting()
        {
            var provider = GeminiProvider.Instance;
            provider.RefreshCachedSettings(new Dictionary<string, object> { ["ThinkingLevel"] = "low" });
            try
            {
                var request = new AIRequestCall();
                request.Initialize(
                    provider: "Gemini",
                    model: "gemini-3.1-flash",
                    body: AIBodyBuilder.Create()
                        .Add(new AIInteractionText { Agent = AIAgent.User, Content = "think" })
                        .Build(),
                    origin: string.Empty,
                    capability: AICapability.TextInput | AICapability.TextOutput);
                request.Parameters = AIRequestParameters.Create()
                    .WithExtra("thinking_level", "high")
                    .Build();

                var body = JObject.Parse(provider.Encode(request));

                Assert.Equal("high", body["generationConfig"]?["thinkingConfig"]?["thinkingLevel"]?.ToString());
            }
            finally
            {
                provider.RefreshCachedSettings(new Dictionary<string, object> { ["ThinkingLevel"] = string.Empty });
            }
        }

        /// <summary>
        /// With neither setting nor extra, no thinkingConfig is emitted (API default).
        /// </summary>
        [Fact]
        public void Encode_NoThinkingLevel_OmitsThinkingConfig()
        {
            var provider = GeminiProvider.Instance;
            provider.RefreshCachedSettings(new Dictionary<string, object> { ["ThinkingLevel"] = string.Empty });

            var request = new AIRequestCall();
            request.Initialize(
                provider: "Gemini",
                model: "gemini-3.1-flash",
                body: AIBodyBuilder.Create()
                    .Add(new AIInteractionText { Agent = AIAgent.User, Content = "hi" })
                    .Build(),
                origin: string.Empty,
                capability: AICapability.TextInput | AICapability.TextOutput);

            var body = JObject.Parse(provider.Encode(request));

            Assert.Null(body["generationConfig"]?["thinkingConfig"]);
        }

        /// <summary>
        /// usageMetadata.cachedContentTokenCount is a subset of promptTokenCount: it maps to
        /// the cache_read quantity and is subtracted from text_input so cached tokens bill
        /// at the cache rate instead of the text rate.
        /// </summary>
        [Fact]
        public void Decode_CachedContentTokens_MapToCacheRead()
        {
            var response = JObject.Parse(@"{
                'candidates': [ { 'content': { 'role': 'model', 'parts': [ { 'text': 'ok' } ] }, 'finishReason': 'STOP' } ],
                'usageMetadata': { 'promptTokenCount': 100, 'candidatesTokenCount': 10, 'cachedContentTokenCount': 40 }
            }");

            var interactions = GeminiProvider.Instance.Decode(response);
            var metrics = interactions.OfType<AIInteractionText>()
                .Select(i => i.Metrics)
                .Where(m => m != null)
                .Aggregate(new AIMetrics(), (acc, m) => acc.WithCombined(m));

            Assert.Equal(40, metrics.Quantity(AIQuantityKey.CacheRead, AIPriceUnit.Token));
            Assert.Equal(60, metrics.Quantity(AIQuantityKey.TextInput, AIPriceUnit.Token));
            Assert.Equal(10, metrics.Quantity(AIQuantityKey.TextOutput, AIPriceUnit.Token));
        }

        /// <summary>
        /// Gemini correlates functionResponse by name and rejects an empty one (HTTP 400):
        /// when the interaction carries only an Id, the encoder must fall back to it.
        /// </summary>
        [Fact]
        public void Encode_FunctionResponse_FallsBackToIdWhenNameMissing()
        {
            var body = AIBodyBuilder.Create()
                .Add(new AIInteractionToolCall { Id = "call_1", Name = "text2boolean" })
                .Add(new AIInteractionToolResult { Id = "text2boolean", Result = new JObject { ["result"] = true } })
                .Build();

            var request = new AIRequestCall();
            request.Initialize(
                provider: "Gemini",
                model: "gemini-3.1-flash",
                body: body,
                origin: string.Empty,
                capability: AICapability.TextInput | AICapability.TextOutput);

            var encoded = JObject.Parse(GeminiProvider.Instance.Encode(request));
            var functionResponse = encoded["contents"]?
                .SelectMany(c => c["parts"] ?? new JArray())
                .Select(p => p["functionResponse"])
                .FirstOrDefault(f => f != null);

            Assert.NotNull(functionResponse);
            Assert.Equal("text2boolean", functionResponse!["name"]?.ToString());
        }

        /// <summary>
        /// Minimal registry that resolves every provider lookup to the given provider.
        /// </summary>
        private sealed class SingleProviderRegistry : IProviderRegistryHost
        {
            private readonly AIProvider provider;

            public SingleProviderRegistry(AIProvider provider)
            {
                this.provider = provider;
            }

            public IAIProvider GetProvider(string providerName) => this.provider;

            public IAIProviderSettings GetProviderSettings(string providerName) =>
                new GeminiProviderSettings(GeminiProvider.Instance);
        }
    }
}
#endif
