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

namespace SmartHopper.ProviderSdk.Tests.Providers.OpenAI
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Newtonsoft.Json.Linq;
    using SmartHopper.Providers.OpenAI;
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
    /// Tests for the OpenAI provider's ChatGPT sign-in credential selection:
    /// OAuth token first, direct API key as fallback, each sufficient alone.
    /// </summary>
    [Collection("ProviderSdk")]
    public sealed class OpenAIProviderChatGptTests : IDisposable
    {
        private const string ApiKey = "sk-test-key";
        private const string AccessToken = "chatgpt-access-token";
        private const string ScopesWithPlanUsage =
            "openid profile email offline_access resource.invoke chatgpt.tokens.use.direct";

        private const string SseBody =
            "data: {\"type\":\"response.output_text.delta\",\"delta\":\"Hello\"}\n\n" +
            "data: {\"type\":\"response.output_text.done\",\"text\":\"Hello\"}\n\n" +
            "data: {\"type\":\"response.completed\",\"response\":{\"id\":\"resp_1\",\"status\":\"completed\"}}\n\n" +
            "data: [DONE]\n\n";

        private readonly OpenAIProvider provider;
        private readonly InMemoryProviderSettingsStore store;
        private readonly IProviderRegistryHost previousRegistry;
        private readonly IProviderHttpClientFactory previousHttpFactory;
        private readonly Func<string, IProviderSettingsStore>? previousStoreFactory;

        /// <summary>
        /// Initializes a new instance of the <see cref="OpenAIProviderChatGptTests"/> class.
        /// </summary>
        public OpenAIProviderChatGptTests()
        {
            this.previousRegistry = ProviderSdkHost.ProviderRegistry;
            this.previousHttpFactory = ProviderSdkHost.HttpClientFactory;
            this.previousStoreFactory = ProviderSdkHost.SettingsStoreFactory;

            this.store = new InMemoryProviderSettingsStore("OpenAI");
            ProviderSdkHost.SettingsStoreFactory = _ => this.store;

            // OpenAIProvider is a singleton: neutralize every managed key first so a
            // previous test's cached values cannot leak into this one.
            this.provider = OpenAIProvider.Instance;
            this.provider.RefreshCachedSettings(BaselineSettings());
            ProviderSdkHost.ProviderRegistry = new FakeProviderRegistryHost(this.provider);

            ProviderSdkTestHelper.ResetCapabilityRegistry();
            AIModelCapabilityRegistry.Instance.SetCapabilities(new AIModelCapabilities
            {
                Provider = "openai",
                Model = "gpt-4o",
                Capabilities = AICapability.Text2Text,
                Default = AICapability.Text2Text,
                SupportsStreaming = true,
            });
        }

        /// <inheritdoc />
        public void Dispose()
        {
            ProviderSdkHost.ProviderRegistry = this.previousRegistry;
            ProviderSdkHost.HttpClientFactory = this.previousHttpFactory;
            ProviderSdkHost.SettingsStoreFactory = this.previousStoreFactory!;
        }

        /// <summary>An API key alone configures the provider.</summary>
        [Fact]
        public void IsConfigured_TrueWithApiKeyOnly()
        {
            this.Seed(new Dictionary<string, object> { ["ApiKey"] = ApiKey });

            Assert.True(this.provider.IsConfigured);
            Assert.False(this.provider.IsChatGptConnected);
        }

        /// <summary>A stored sign-in grant alone configures the provider.</summary>
        [Fact]
        public void IsConfigured_TrueWithGrantOnly()
        {
            this.Seed(GrantSettings());

            Assert.True(this.provider.IsConfigured);
            Assert.True(this.provider.IsChatGptConnected);
            Assert.True(this.provider.HasChatGptPlanUsage);
        }

        /// <summary>Neither credential means the provider is not configured.</summary>
        [Fact]
        public void IsConfigured_FalseWithNoCredentials()
        {
            Assert.False(this.provider.IsConfigured);
        }

        /// <summary>
        /// Without a grant, requests use the direct API key and the standard
        /// (non-streamed) request shape.
        /// </summary>
        [Fact]
        public async Task Call_UsesApiKey_WhenNotConnected()
        {
            this.Seed(new Dictionary<string, object> { ["ApiKey"] = ApiKey });

            string? capturedBody = null;
            string? capturedAuth = null;
            ProviderSdkHost.HttpClientFactory = TestProviderHttpClientFactory.WithResponse(
                async (request, _) =>
                {
                    capturedAuth = request.Headers.Authorization?.Parameter;
                    capturedBody = request.Content == null
                        ? null
                        : await request.Content.ReadAsStringAsync().ConfigureAwait(false);

                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(
                            "{\"status\":\"completed\",\"output\":[{\"type\":\"message\",\"role\":\"assistant\"," +
                            "\"content\":[{\"type\":\"output_text\",\"text\":\"hi\"}]}]," +
                            "\"choices\":[{\"finish_reason\":\"stop\",\"message\":{\"role\":\"assistant\",\"content\":\"hi\"}}]}"),
                    };
                });

            var result = (AIReturn)await this.provider.Call(CreateRequest()).ConfigureAwait(false);

            Assert.Equal(ApiKey, capturedAuth);
            Assert.NotNull(capturedBody);
            var json = JObject.Parse(capturedBody!);
            Assert.NotNull(json["max_output_tokens"]);
            Assert.True(
                result.Success,
                string.Join(" | ", result.Messages?.Select(m => m?.Message ?? string.Empty) ?? Enumerable.Empty<string>()));
        }

        /// <summary>
        /// With a grant, the sign-in token is the primary credential and the request
        /// uses the plan-usage shape: streamed SSE, non-persisted, without the fields
        /// the sign-in route rejects.
        /// </summary>
        [Fact]
        public async Task Call_UsesChatGptToken_WithPlanUsageRequestShape()
        {
            var settings = GrantSettings();
            settings["ApiKey"] = ApiKey;
            settings["Temperature"] = 0.7; // would be sent on the API-key path
            this.Seed(settings);

            string? capturedBody = null;
            string? capturedAuth = null;
            ProviderSdkHost.HttpClientFactory = TestProviderHttpClientFactory.WithResponse(
                async (request, _) =>
                {
                    capturedAuth = request.Headers.Authorization?.Parameter;
                    capturedBody = request.Content == null
                        ? null
                        : await request.Content.ReadAsStringAsync().ConfigureAwait(false);

                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(SseBody),
                    };
                });

            var builder = AIBodyBuilder.Create();
            builder.Add(new AIInteractionText { Agent = AIAgent.System, Content = "Be terse" });
            builder.Add(new AIInteractionText { Agent = AIAgent.User, Content = "Say hi" });
            var request = new AIRequestCall
            {
                Provider = "OpenAI",
                Model = "gpt-4o",
                Endpoint = "/responses",
                Body = builder.Build(),
            };

            var result = (AIReturn)await this.provider.Call(request).ConfigureAwait(false);

            Assert.Equal(AccessToken, capturedAuth);
            Assert.NotNull(capturedBody);
            var json = JObject.Parse(capturedBody!);
            Assert.Equal(true, json["stream"]?.Value<bool>());
            Assert.Equal(false, json["store"]?.Value<bool>());
            Assert.Null(json["max_output_tokens"]);
            Assert.Null(json["temperature"]);
            Assert.Null(json["top_p"]);

            // System messages become developer messages on the sign-in route.
            var input = json["input"] as JArray ?? new JArray();
            var roles = input.Select(i => i["role"]?.ToString()).ToList();
            Assert.Contains("developer", roles);
            Assert.DoesNotContain("system", roles);
        }

        /// <summary>
        /// A rejected sign-in token retries exactly once with the configured API key.
        /// </summary>
        [Fact]
        public async Task Call_FallsBackToApiKey_WhenTokenRejected()
        {
            var settings = GrantSettings();
            settings["ApiKey"] = ApiKey;
            this.Seed(settings);

            var authorizations = new List<string?>();
            ProviderSdkHost.HttpClientFactory = TestProviderHttpClientFactory.WithResponse(
                (request, _) =>
                {
                    authorizations.Add(request.Headers.Authorization?.Parameter);
                    var status = authorizations.Count == 1 ? HttpStatusCode.Unauthorized : HttpStatusCode.OK;
                    return Task.FromResult(new HttpResponseMessage(status)
                    {
                        Content = new StringContent(authorizations.Count == 1 ? "{\"error\":{\"message\":\"bad token\"}}" : SseBody),
                    });
                });

            var result = (AIReturn)await this.provider.Call(CreateRequest()).ConfigureAwait(false);

            Assert.True(
                authorizations.Count == 2,
                $"requests={authorizations.Count}; status={result.Status}; messages=" +
                string.Join(" | ", result.Messages?.Select(m => m?.Message ?? string.Empty) ?? Enumerable.Empty<string>()));
            Assert.Equal(AccessToken, authorizations[0]);
            Assert.Equal(ApiKey, authorizations[1]);
        }

        /// <summary>
        /// With a grant and no API key, the token alone is used and a rejection is
        /// surfaced as a provider error without a second attempt.
        /// </summary>
        [Fact]
        public async Task Call_UsesTokenOnly_WhenNoApiKey()
        {
            this.Seed(GrantSettings());

            var authorizations = new List<string?>();
            ProviderSdkHost.HttpClientFactory = TestProviderHttpClientFactory.WithResponse(
                (request, _) =>
                {
                    authorizations.Add(request.Headers.Authorization?.Parameter);
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)
                    {
                        Content = new StringContent("{\"error\":{\"message\":\"expired\"}}"),
                    });
                });

            var result = (AIReturn)await this.provider.Call(CreateRequest()).ConfigureAwait(false);

            Assert.Single(authorizations);
            Assert.Equal(AccessToken, authorizations[0]);
            Assert.False(result.Success);
        }

        /// <summary>
        /// The auth preparation flags OAuth-served endpoints only: Responses requests
        /// additionally switch to the streaming transport the plan route requires.
        /// </summary>
        [Fact]
        public async Task PrepareChatGptAuthAsync_FlagsEligibleEndpoints()
        {
            this.Seed(GrantSettings());

            var responses = CreateRequest("/responses");
            await this.provider.PrepareChatGptAuthAsync(responses, CancellationToken.None).ConfigureAwait(false);
            Assert.True(this.provider.IsChatGptConnected);
            Assert.Equal(AccessToken, responses.ResolvedCredential);
            Assert.Equal("chatgpt", responses.ResolvedCredentialTag);
            Assert.True(responses.WantsStreaming);

            var models = CreateRequest("/models");
            await this.provider.PrepareChatGptAuthAsync(models, CancellationToken.None).ConfigureAwait(false);
            Assert.Equal(AccessToken, models.ResolvedCredential);
            Assert.Equal("chatgpt", models.ResolvedCredentialTag);
            Assert.False(models.WantsStreaming);

            var audio = CreateRequest("/audio/speech");
            await this.provider.PrepareChatGptAuthAsync(audio, CancellationToken.None).ConfigureAwait(false);
            Assert.Null(audio.ResolvedCredential);
            Assert.Null(audio.ResolvedCredentialTag);
            Assert.False(audio.WantsStreaming);
        }

        /// <summary>
        /// Without a grant the preparation never flags the sign-in credential, even on
        /// eligible endpoints.
        /// </summary>
        [Fact]
        public async Task PrepareChatGptAuthAsync_NoGrant_NoFlag()
        {
            this.Seed(new Dictionary<string, object> { ["ApiKey"] = ApiKey });

            var request = CreateRequest("/responses");
            await this.provider.PrepareChatGptAuthAsync(request, CancellationToken.None).ConfigureAwait(false);

            Assert.Null(request.ResolvedCredential);
            Assert.Null(request.ResolvedCredentialTag);
            Assert.False(request.WantsStreaming);
        }

        /// <summary>
        /// An expired grant triggers a refresh before the request, and the rotated
        /// token is persisted and used.
        /// </summary>
        [Fact]
        public async Task EnsureFreshTokenAsync_RefreshesExpiredGrant()
        {
            var settings = GrantSettings();
            settings["ChatGptAccessTokenExpiresAt"] =
                DateTimeOffset.UtcNow.AddMinutes(-5).ToString("O");
            settings["ChatGptRefreshToken"] = "rt_old";
            settings["ChatGptClientId"] = "oaiapp_abc";
            this.Seed(settings);

            var previousOAuthFactory = ChatGptOAuth.HttpClientFactory;
            try
            {
                ChatGptOAuth.HttpClientFactory = () => new HttpClient(new DelegateHandler(
                    (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(
                            "{\"access_token\":\"at_fresh\",\"refresh_token\":\"rt_new\",\"expires_in\":3600," +
                            "\"scope\":\"" + ScopesWithPlanUsage + "\"}"),
                    })));

                await this.provider.EnsureFreshTokenAsync(CancellationToken.None).ConfigureAwait(false);

                Assert.Equal("at_fresh", this.store.Get<string>("ChatGptAccessToken"));
                Assert.Equal("rt_new", this.store.Get<string>("ChatGptRefreshToken"));
            }
            finally
            {
                ChatGptOAuth.HttpClientFactory = previousOAuthFactory;
            }
        }

        /// <summary>
        /// A rejected refresh clears the stored grant so the API key path takes over.
        /// </summary>
        [Fact]
        public async Task EnsureFreshTokenAsync_RejectedRefresh_ClearsGrant()
        {
            var settings = GrantSettings();
            settings["ChatGptAccessTokenExpiresAt"] =
                DateTimeOffset.UtcNow.AddMinutes(-5).ToString("O");
            settings["ChatGptRefreshToken"] = "rt_stale";
            settings["ChatGptClientId"] = "oaiapp_abc";
            this.Seed(settings);

            var previousOAuthFactory = ChatGptOAuth.HttpClientFactory;
            try
            {
                ChatGptOAuth.HttpClientFactory = () => new HttpClient(new DelegateHandler(
                    (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
                    {
                        Content = new StringContent("{\"error\":\"invalid_grant\"}"),
                    })));

                await this.provider.EnsureFreshTokenAsync(CancellationToken.None).ConfigureAwait(false);

                Assert.False(this.provider.IsChatGptConnected);
                Assert.Equal(string.Empty, this.store.Get<string>("ChatGptAccessToken"));
            }
            finally
            {
                ChatGptOAuth.HttpClientFactory = previousOAuthFactory;
            }
        }

        /// <summary>Disconnecting clears the stored tokens and reports not connected.</summary>
        [Fact]
        public void DisconnectChatGpt_ClearsGrantAndStatus()
        {
            this.Seed(GrantSettings());
            Assert.True(this.provider.IsChatGptConnected);

            this.provider.DisconnectChatGpt();

            Assert.False(this.provider.IsChatGptConnected);
            Assert.Equal("Not connected", this.store.Get<string>("ChatGptStatus"));
        }

        /// <summary>
        /// A persisted busy status left over from a killed process is normalized when
        /// the descriptors are read.
        /// </summary>
        [Fact]
        public void NormalizeConnectionStatus_RepairsStaleBusyState()
        {
            var settings = GrantSettings();
            settings["ChatGptStatus"] = "Connecting — complete sign-in in your browser…";
            settings["ChatGptStatusLevel"] = "busy";
            this.Seed(settings);

            this.provider.NormalizeConnectionStatus();

            Assert.Equal("ok", this.store.Get<string>("ChatGptStatusLevel"));
        }

        /// <summary>Seeds both the persisted store and the provider's settings cache.</summary>
        private void Seed(Dictionary<string, object> values)
        {
            foreach (var kvp in values)
            {
                this.store.Set(kvp.Key, kvp.Value);
            }

            this.provider.RefreshCachedSettings(new Dictionary<string, object>(values));
        }

        /// <summary>Neutralizes every setting a test or the sign-in lifecycle can write.</summary>
        private static Dictionary<string, object> BaselineSettings()
        {
            return new Dictionary<string, object>
            {
                ["ApiKey"] = string.Empty,
                ["ChatGptConnect"] = false,
                ["ChatGptStatus"] = string.Empty,
                ["ChatGptStatusLevel"] = string.Empty,
                ["ChatGptAccessToken"] = string.Empty,
                ["ChatGptRefreshToken"] = string.Empty,
                ["ChatGptIdToken"] = string.Empty,
                ["ChatGptClientId"] = string.Empty,
                ["ChatGptHostId"] = string.Empty,
                ["ChatGptEmail"] = string.Empty,
                ["ChatGptAccessTokenExpiresAt"] = string.Empty,
                ["ChatGptScopes"] = string.Empty,
                ["EnableStreaming"] = true,
                ["MaxTokens"] = 2000,
                ["Temperature"] = "0.5",
                ["ReasoningEffort"] = "low",
                ["ForceChatCompletions"] = false,
            };
        }

        /// <summary>A stored grant with a far-future expiry (no refresh needed).</summary>
        private static Dictionary<string, object> GrantSettings()
        {
            return new Dictionary<string, object>
            {
                ["ChatGptAccessToken"] = AccessToken,
                ["ChatGptScopes"] = ScopesWithPlanUsage,
                ["ChatGptAccessTokenExpiresAt"] =
                    DateTimeOffset.UtcNow.AddHours(1).ToString("O"),
                ["ChatGptEmail"] = "user@example.com",
            };
        }

        /// <summary>Creates a minimal text request for the given endpoint.</summary>
        private static AIRequestCall CreateRequest(string endpoint = "/responses")
        {
            var builder = AIBodyBuilder.Create();
            builder.Add(new AIInteractionText { Agent = AIAgent.User, Content = "Say hi" });
            return new AIRequestCall
            {
                Provider = "OpenAI",
                Model = "gpt-4o",
                Endpoint = endpoint,
                Body = builder.Build(),
            };
        }

        /// <summary>Minimal delegate-based HTTP handler for faking OAuth endpoints.</summary>
        private sealed class DelegateHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler;

            public DelegateHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
            {
                this.handler = handler;
            }

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                return this.handler(request, cancellationToken);
            }
        }
    }
}
