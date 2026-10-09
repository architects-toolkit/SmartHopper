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
    using System;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Newtonsoft.Json.Linq;
    using SmartHopper.ProviderSdk.AICall.Core;
    using SmartHopper.ProviderSdk.AICall.Core.Base;
    using SmartHopper.ProviderSdk.AICall.Core.Interactions;
    using SmartHopper.ProviderSdk.AICall.Core.Requests;
    using SmartHopper.ProviderSdk.AICall.Core.Returns;
    using SmartHopper.ProviderSdk.AIModels;
    using SmartHopper.ProviderSdk.Hosting;
    using SmartHopper.ProviderSdk.Tests.TestHelpers;
    using Xunit;

    /// <summary>
    /// Contract tests for the audio-specific paths of the <see cref="AIProvider"/> HTTP
    /// pipeline: multipart transcription uploads and binary audio response normalization.
    /// Uses a fake OpenAI-compatible provider and an in-memory HTTP client factory.
    /// </summary>
    [Collection("ProviderSdk")]
    public sealed class AIProviderAudioCallTests : IDisposable
    {
        private readonly FakeOpenAICompatibleProvider provider;
        private readonly IProviderRegistryHost previousRegistry;
        private readonly IProviderHttpClientFactory previousFactory;

        /// <summary>
        /// Initializes a new instance of the <see cref="AIProviderAudioCallTests"/> class.
        /// </summary>
        public AIProviderAudioCallTests()
        {
            this.provider = new FakeOpenAICompatibleProvider();
            this.provider.Configure("test-api-key", "fake-model");

            ProviderSdkTestHelper.ResetCapabilityRegistry();
            AIModelCapabilityRegistry.Instance.SetCapabilities(new AIModelCapabilities
            {
                Provider = FakeOpenAICompatibleProvider.ProviderName.ToLowerInvariant(),
                Model = "fake-model",
                Capabilities = AICapability.Speech2Text | AICapability.Text2Speech | AICapability.Text2Text,
                Default = AICapability.Text2Text,
            });

            this.previousRegistry = ProviderSdkHost.ProviderRegistry;
            this.previousFactory = ProviderSdkHost.HttpClientFactory;

            ProviderSdkHost.ProviderRegistry = new FakeProviderRegistryHost(this.provider);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            ProviderSdkHost.ProviderRegistry = this.previousRegistry;
            ProviderSdkHost.HttpClientFactory = this.previousFactory;
        }

        /// <summary>
        /// Requests to <c>/audio/transcriptions</c> must upload the audio bytes as
        /// <c>multipart/form-data</c> (file part plus scalar metadata), not JSON.
        /// </summary>
        [Fact]
        public async Task Call_TranscriptionEndpoint_SendsMultipartFormData()
        {
            HttpRequestMessage? capturedRequest = null;
            string? capturedContentType = null;
            string? capturedBody = null;

            ProviderSdkHost.HttpClientFactory = TestProviderHttpClientFactory.WithResponse(
                async (request, cancellationToken) =>
                {
                    capturedRequest = request;
                    capturedContentType = request.Content?.Headers?.ContentType?.MediaType;
                    capturedBody = request.Content != null
                        ? await request.Content.ReadAsStringAsync().ConfigureAwait(false)
                        : null;

                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("{\"text\":\"hello world\"}"),
                    };
                });

            var audioBytes = new byte[] { 0x52, 0x49, 0x46, 0x46, 0x10, 0x00 };
            var request = CreateRequest(
                endpoint: "/audio/transcriptions",
                capability: AICapability.Speech2Text,
                interactions: new AIInteractionAudio
                {
                    Agent = AIAgent.User,
                    Data = audioBytes,
                    MimeType = "audio/wav",
                    LanguageHint = "en",
                });

            var result = (AIReturn)await this.provider.Call(request).ConfigureAwait(false);

            Assert.True(result.Success, string.Join(" | ", result.Messages?.Select(m => m.Message) ?? Enumerable.Empty<string>()));
            Assert.NotNull(capturedRequest);
            Assert.Equal("multipart/form-data", capturedContentType);
            Assert.NotNull(capturedBody);

            // The multipart body carries a "file" part plus scalar metadata fields.
            // Content-Disposition names may render quoted or unquoted depending on the runtime.
            var normalizedBody = capturedBody!.Replace("\"", string.Empty);
            Assert.Contains("name=file", normalizedBody, StringComparison.Ordinal);
            Assert.Contains("filename=", normalizedBody, StringComparison.Ordinal);
            Assert.Contains("name=model", normalizedBody, StringComparison.Ordinal);
            Assert.Contains("fake-model", capturedBody, StringComparison.Ordinal);
            Assert.Contains("name=language", normalizedBody, StringComparison.Ordinal);

            // The transcription response decodes to assistant text.
            var text = result.Body?.Interactions?.OfType<AIInteractionText>().FirstOrDefault();
            Assert.Equal("hello world", text?.Content);
        }

        /// <summary>
        /// Requests to <c>/audio/transcriptions</c> without resolvable audio bytes must
        /// not crash; the pipeline falls back to the regular JSON content.
        /// </summary>
        [Fact]
        public async Task Call_TranscriptionEndpointWithoutAudio_FallsBackToJson()
        {
            HttpRequestMessage? capturedRequest = null;
            string? capturedContentType = null;

            ProviderSdkHost.HttpClientFactory = TestProviderHttpClientFactory.WithResponse(
                (request, cancellationToken) =>
                {
                    capturedRequest = request;
                    capturedContentType = request.Content?.Headers?.ContentType?.MediaType;

                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("{\"text\":\"done\"}"),
                    });
                });

            var request = CreateRequest(
                endpoint: "/audio/transcriptions",
                capability: AICapability.Speech2Text,
                interactions: new AIInteractionAudio
                {
                    Agent = AIAgent.User,
                    MimeType = "audio/wav",
                });

            var result = (AIReturn)await this.provider.Call(request).ConfigureAwait(false);

            Assert.True(result.Success);
            Assert.NotNull(capturedRequest);
            Assert.Equal("application/json", capturedContentType);
        }

        /// <summary>
        /// A dedicated speech endpoint returning raw binary audio (<c>audio/mpeg</c>) must be
        /// normalized into the <c>audio_data</c> JSON envelope and decoded as an
        /// <see cref="AIInteractionAudio"/> instead of failing JSON parsing.
        /// </summary>
        [Fact]
        public async Task Call_SpeechEndpoint_BinaryResponse_ReturnsAudioInteraction()
        {
            var audioBytes = new byte[] { 0xFF, 0xFB, 0x90, 0x00, 0x11, 0x22 };

            ProviderSdkHost.HttpClientFactory = TestProviderHttpClientFactory.WithResponse(
                (request, cancellationToken) =>
                {
                    var content = new ByteArrayContent(audioBytes);
                    content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/mpeg");

                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = content,
                    });
                });

            var request = CreateRequest(
                endpoint: "/audio/speech",
                capability: AICapability.Text2Speech,
                interactions: new AIInteractionText
                {
                    Agent = AIAgent.User,
                    Content = "Say hello",
                });

            var result = (AIReturn)await this.provider.Call(request).ConfigureAwait(false);

            Assert.True(result.Success);
            var audio = result.Body?.Interactions?.OfType<AIInteractionAudio>().FirstOrDefault();
            Assert.NotNull(audio);
            Assert.Equal(AIAgent.Assistant, audio!.Agent);
            Assert.Equal(audioBytes, audio.Data);
            Assert.Equal("audio/mpeg", audio.MimeType);
        }

        private static AIRequestCall CreateRequest(string endpoint, AICapability capability, params IAIInteraction[] interactions)
        {
            var builder = AIBodyBuilder.Create();
            foreach (var interaction in interactions)
            {
                builder.Add(interaction);
            }

            return new AIRequestCall
            {
                Provider = FakeOpenAICompatibleProvider.ProviderName,
                Model = "fake-model",
                Endpoint = endpoint,
                Capability = capability,
                Body = builder.Build(),
            };
        }
    }
}
