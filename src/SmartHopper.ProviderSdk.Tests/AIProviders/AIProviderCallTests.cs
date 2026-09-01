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
    using SmartHopper.ProviderSdk.AIProviders;
    using SmartHopper.ProviderSdk.Hosting;
    using SmartHopper.ProviderSdk.Tests.TestHelpers;
    using Xunit;

    /// <summary>
    /// Contract and round-trip tests for the <see cref="AIProvider"/> HTTP call pipeline.
    /// Uses a fake provider and an in-memory HTTP client factory so no network is required.
    /// </summary>
    [Collection("ProviderSdk")]
    public sealed class AIProviderCallTests : IDisposable
    {
        private readonly FakeAIProvider provider;
        private readonly IProviderRegistryHost previousRegistry;
        private readonly IProviderHttpClientFactory previousFactory;

        /// <summary>
        /// Initializes a new instance of the <see cref="AIProviderCallTests"/> class.
        /// </summary>
        public AIProviderCallTests()
        {
            this.provider = new FakeAIProvider();
            this.provider.Configure("test-api-key", "fake-model");

            ProviderSdkTestHelper.ResetCapabilityRegistry();
            AIModelCapabilityRegistry.Instance.SetCapabilities(new AIModelCapabilities
            {
                Provider = FakeAIProvider.ProviderName.ToLowerInvariant(),
                Model = "fake-model",
                Capabilities = AICapability.Text2Text,
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
        /// A successful provider call must encode the request, send it through the HTTP factory,
        /// and decode the response body into interactions.
        /// </summary>
        [Fact]
        public async Task Call_TextRoundTrip_ReturnsDecodedAssistantText()
        {
            var responseJson = new JObject
            {
                ["id"] = "chatcmpl-test",
                ["choices"] = new JArray
                {
                    new JObject
                    {
                        ["message"] = new JObject
                        {
                            ["role"] = "assistant",
                            ["content"] = "Hello from the fake provider",
                        },
                    },
                },
            };

            ProviderSdkHost.HttpClientFactory = TestProviderHttpClientFactory.WithResponse(
                HttpStatusCode.OK,
                responseJson);

            var request = CreateRequest(new AIInteractionText
            {
                Agent = AIAgent.User,
                Content = "Say hello",
            });

            var result = (AIReturn)await this.provider.Call(request).ConfigureAwait(false);

            Assert.True(result.Success);
            Assert.NotNull(result.Body);
            var body = result.Body!;
            Assert.Equal(1, body.InteractionsCount);

            var text = body.Interactions.OfType<AIInteractionText>().FirstOrDefault();
            Assert.NotNull(text);
            Assert.Equal("Hello from the fake provider", text!.Content);
        }

        /// <summary>
        /// The encoded request body must include the provider-specific JSON produced by
        /// <see cref="AIProvider.Encode(AIRequestCall)"/>.
        /// </summary>
        [Fact]
        public async Task Call_EncodesRequestAndSendsAuthorizationHeader()
        {
            HttpRequestMessage? capturedRequest = null;

            ProviderSdkHost.HttpClientFactory = TestProviderHttpClientFactory.WithResponse(
                async (request, cancellationToken) =>
                {
                    capturedRequest = request;

                    var response = new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":\"ok\"}}]}"),
                    };

                    return response;
                });

            var request = CreateRequest(new AIInteractionText
            {
                Agent = AIAgent.User,
                Content = "Hi",
            });

            await this.provider.Call(request).ConfigureAwait(false);

            Assert.NotNull(capturedRequest);
            Assert.Equal(HttpMethod.Post, capturedRequest!.Method);
            Assert.Equal("https://fake.example/v1/chat/completions", capturedRequest.RequestUri!.ToString());
            Assert.Equal("Bearer", capturedRequest.Headers.Authorization!.Scheme);
            Assert.Equal("test-api-key", capturedRequest.Headers.Authorization!.Parameter);

            var body = await capturedRequest.Content!.ReadAsStringAsync().ConfigureAwait(false);
            var json = JObject.Parse(body);
            Assert.Equal("fake-model", json["model"]?.ToString());

            var messages = json["messages"] as JArray ?? new JArray();
            Assert.Single(messages);
        }

        /// <summary>
        /// Provider errors (non-2xx) must be surfaced as <see cref="AIReturn"/> messages
        /// rather than thrown exceptions.
        /// </summary>
        [Fact]
        public async Task Call_ProviderError_ReturnsErrorReturn()
        {
            var errorJson = new JObject
            {
                ["error"] = new JObject
                {
                    ["message"] = "Invalid API key",
                },
            };

            ProviderSdkHost.HttpClientFactory = TestProviderHttpClientFactory.WithResponse(
                HttpStatusCode.Unauthorized,
                errorJson);

            var request = CreateRequest(new AIInteractionText
            {
                Agent = AIAgent.User,
                Content = "Hi",
            });

            var result = (AIReturn)await this.provider.Call(request).ConfigureAwait(false);

            Assert.False(result.Success);
            Assert.Contains(result.Messages, m => m.Message.Contains("Invalid API key"));
        }

        /// <summary>
        /// A round-trip with a tool-call response must decode the tool calls correctly.
        /// </summary>
        [Fact]
        public async Task Call_ToolCallRoundTrip_ReturnsDecodedToolCall()
        {
            var responseJson = new JObject
            {
                ["choices"] = new JArray
                {
                    new JObject
                    {
                        ["message"] = new JObject
                        {
                            ["role"] = "assistant",
                            ["content"] = JValue.CreateNull(),
                            ["tool_calls"] = new JArray
                            {
                                new JObject
                                {
                                    ["id"] = "call_1",
                                    ["function"] = new JObject
                                    {
                                        ["name"] = "get_weather",
                                        ["arguments"] = "{\"location\":\"Barcelona\"}",
                                    },
                                },
                            },
                        },
                    },
                },
            };

            ProviderSdkHost.HttpClientFactory = TestProviderHttpClientFactory.WithResponse(
                HttpStatusCode.OK,
                responseJson);

            var request = CreateRequest(new AIInteractionText
            {
                Agent = AIAgent.User,
                Content = "What's the weather?",
            });

            var result = (AIReturn)await this.provider.Call(request).ConfigureAwait(false);

            Assert.True(result.Success);
            Assert.NotNull(result.Body);
            var toolCall = result.Body!.Interactions.OfType<AIInteractionToolCall>().FirstOrDefault();
            Assert.NotNull(toolCall);
            Assert.Equal("get_weather", toolCall!.Name);
            Assert.Equal("call_1", toolCall.Id);
            Assert.NotNull(toolCall.Arguments);
            Assert.Equal("Barcelona", toolCall.Arguments!["location"]?.ToString());
        }

        private static AIRequestCall CreateRequest(params IAIInteraction[] interactions)
        {
            var builder = AIBodyBuilder.Create();
            foreach (var interaction in interactions)
            {
                builder.Add(interaction);
            }

            return new AIRequestCall
            {
                Provider = FakeAIProvider.ProviderName,
                Model = "fake-model",
                Endpoint = "/v1/chat/completions",
                Body = builder.Build(),
            };
        }
    }
}
