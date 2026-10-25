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
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using SmartHopper.Providers.OpenAI;
    using Xunit;

    /// <summary>
    /// Tests for the OAuth/PKCE mechanics of the ChatGPT sign-in flow.
    /// </summary>
    [Collection("ProviderSdk")]
    public sealed class ChatGptOAuthTests : IDisposable
    {
        private readonly Func<HttpClient> previousFactory;

        /// <summary>
        /// Initializes a new instance of the <see cref="ChatGptOAuthTests"/> class.
        /// </summary>
        public ChatGptOAuthTests()
        {
            this.previousFactory = ChatGptOAuth.HttpClientFactory;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            ChatGptOAuth.HttpClientFactory = this.previousFactory;
        }

        /// <summary>
        /// PKCE verifiers must be high-entropy base64url strings without padding.
        /// </summary>
        [Fact]
        public void GenerateCodeVerifier_ProducesBase64UrlString()
        {
            var verifier = ChatGptOAuth.GenerateCodeVerifier();

            // 32 random bytes → ceil(256 / 6) = 43 base64url characters, no padding.
            Assert.Equal(43, verifier.Length);
            Assert.Matches("^[A-Za-z0-9_-]+$", verifier);
            Assert.NotEqual(verifier, ChatGptOAuth.GenerateCodeVerifier());
        }

        /// <summary>
        /// The challenge must be the base64url-encoded SHA-256 of the verifier (S256).
        /// </summary>
        [Fact]
        public void GenerateCodeChallenge_MatchesSha256OfVerifier()
        {
            var verifier = "test-verifier-12345";
            var expected = Convert.ToBase64String(
                    System.Security.Cryptography.SHA256.HashData(
                        System.Text.Encoding.UTF8.GetBytes(verifier)))
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');

            Assert.Equal(expected, ChatGptOAuth.GenerateCodeChallenge(verifier));
        }

        /// <summary>
        /// First-time registrations must use the dynamic client with the agent name hint
        /// and carry the stable host identifier plus the loopback redirect.
        /// </summary>
        [Fact]
        public void BuildAuthorizeUrl_FirstRegistration_UsesDynamicClientAndHostId()
        {
            var url = ChatGptOAuth.BuildAuthorizeUrl(
                new Uri("http://127.0.0.1:5050/auth/callback"),
                "challenge-1",
                "state-1",
                "nonce-1",
                "urn:uuid:host-1",
                existingClientId: null,
                idTokenHint: null,
                loginHint: null);

            var uri = new Uri(url);
            Assert.Equal(
                "https://auth.openai.com/api/accounts/authorize",
                uri.GetLeftPart(UriPartial.Path));

            var query = ChatGptOAuth.ParseQuery(uri.Query);
            Assert.Equal("dynamic_agent_client", ChatGptOAuth.GetQueryValue(query, "client_id"));
            Assert.Equal("SmartHopper", ChatGptOAuth.GetQueryValue(query, "agent_name_hint"));
            Assert.Equal("urn:uuid:host-1", ChatGptOAuth.GetQueryValue(query, "ext_agent_host_id"));
            Assert.Equal("http://127.0.0.1:5050/auth/callback", ChatGptOAuth.GetQueryValue(query, "redirect_uri"));
            Assert.Equal("code", ChatGptOAuth.GetQueryValue(query, "response_type"));
            Assert.Equal("state-1", ChatGptOAuth.GetQueryValue(query, "state"));
            Assert.Equal("nonce-1", ChatGptOAuth.GetQueryValue(query, "nonce"));
            Assert.Equal("challenge-1", ChatGptOAuth.GetQueryValue(query, "code_challenge"));
            Assert.Equal("S256", ChatGptOAuth.GetQueryValue(query, "code_challenge_method"));
            Assert.Equal("https://api.openai.com/v1", ChatGptOAuth.GetQueryValue(query, "resource"));

            var scope = ChatGptOAuth.GetQueryValue(query, "scope") ?? string.Empty;
            Assert.Contains("openid", scope, StringComparison.Ordinal);
            Assert.Contains("offline_access", scope, StringComparison.Ordinal);
            Assert.Contains("chatgpt.tokens.use.direct", scope, StringComparison.Ordinal);

            Assert.Null(ChatGptOAuth.GetQueryValue(query, "id_token_hint"));
            Assert.Null(ChatGptOAuth.GetQueryValue(query, "login_hint"));
        }

        /// <summary>
        /// Re-authorizations must reuse the issued client ID and may carry the
        /// id_token/login hints; the dynamic-registration parameters must be absent.
        /// </summary>
        [Fact]
        public void BuildAuthorizeUrl_Reauthorization_ReusesIssuedClientAndHints()
        {
            var url = ChatGptOAuth.BuildAuthorizeUrl(
                new Uri("http://127.0.0.1:5050/auth/callback"),
                "challenge-2",
                "state-2",
                "nonce-2",
                "urn:uuid:host-1",
                existingClientId: "oaiapp_abc123",
                idTokenHint: "id-token-value",
                loginHint: "user@example.com");

            var query = ChatGptOAuth.ParseQuery(new Uri(url).Query);
            Assert.Equal("oaiapp_abc123", ChatGptOAuth.GetQueryValue(query, "client_id"));
            Assert.Null(ChatGptOAuth.GetQueryValue(query, "agent_name_hint"));
            Assert.Equal("id-token-value", ChatGptOAuth.GetQueryValue(query, "id_token_hint"));
            Assert.Equal("user@example.com", ChatGptOAuth.GetQueryValue(query, "login_hint"));
        }

        /// <summary>
        /// Query parsing must percent-decode keys and values and tolerate the leading '?'.
        /// </summary>
        [Fact]
        public void ParseQuery_DecodesEscapedPairs()
        {
            var parsed = ChatGptOAuth.ParseQuery("?code=abc%20def&state=x%3Dy&bare");

            Assert.Equal("abc def", ChatGptOAuth.GetQueryValue(parsed, "code"));
            Assert.Equal("x=y", ChatGptOAuth.GetQueryValue(parsed, "state"));
            Assert.Null(ChatGptOAuth.GetQueryValue(parsed, "bare"));
            Assert.Null(ChatGptOAuth.GetQueryValue(parsed, "missing"));
        }

        /// <summary>
        /// Only the request target of a well-formed GET line is extracted.
        /// </summary>
        [Fact]
        public void ExtractRequestTarget_HandlesGetLines()
        {
            Assert.Equal(
                "/auth/callback?code=x&state=y",
                ChatGptOAuth.ExtractRequestTarget(
                    "GET /auth/callback?code=x&state=y HTTP/1.1\r\nHost: 127.0.0.1\r\n\r\n"));

            // Absolute-URI form (some clients send it to proxies).
            Assert.Equal(
                "/auth/callback?code=z",
                ChatGptOAuth.ExtractRequestTarget(
                    "GET http://127.0.0.1:5050/auth/callback?code=z HTTP/1.1\r\n\r\n"));

            Assert.Null(ChatGptOAuth.ExtractRequestTarget("POST /auth/callback HTTP/1.1\r\n\r\n"));
            Assert.Null(ChatGptOAuth.ExtractRequestTarget(string.Empty));
            Assert.Null(ChatGptOAuth.ExtractRequestTarget("garbage-without-target"));
        }

        /// <summary>
        /// Only the fixed callback path terminates the wait loop.
        /// </summary>
        [Fact]
        public void IsCallbackTarget_MatchesOnlyCallbackPath()
        {
            Assert.True(ChatGptOAuth.IsCallbackTarget("/auth/callback"));
            Assert.True(ChatGptOAuth.IsCallbackTarget("/auth/callback?code=x"));
            Assert.False(ChatGptOAuth.IsCallbackTarget("/favicon.ico"));
            Assert.False(ChatGptOAuth.IsCallbackTarget(null));
        }

        /// <summary>
        /// A refresh grant must parse the rotated token set and keep the issued client ID.
        /// </summary>
        [Fact]
        public async Task RefreshAsync_ParsesRotatedTokenSet()
        {
            string? capturedBody = null;
            ChatGptOAuth.HttpClientFactory = () => new HttpClient(new DelegateHandler(
                async (request, _) =>
                {
                    capturedBody = await request.Content!.ReadAsStringAsync().ConfigureAwait(false);
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(
                            "{\"access_token\":\"at_new\",\"refresh_token\":\"rt_new\",\"expires_in\":3600," +
                            "\"scope\":\"openid profile email offline_access resource.invoke chatgpt.tokens.use.direct\"}"),
                    };
                }));

            var grant = await ChatGptOAuth.RefreshAsync("oaiapp_abc", "rt_old").ConfigureAwait(false);

            Assert.Equal("at_new", grant.AccessToken);
            Assert.Equal("rt_new", grant.RefreshToken);
            Assert.Equal("oaiapp_abc", grant.ClientId);
            Assert.Equal(3600, grant.ExpiresIn);
            Assert.Contains("chatgpt.tokens.use.direct", grant.Scopes, StringComparison.Ordinal);

            Assert.NotNull(capturedBody);
            Assert.Contains("grant_type=refresh_token", capturedBody!, StringComparison.Ordinal);
            Assert.Contains("client_id=oaiapp_abc", capturedBody!, StringComparison.Ordinal);
            Assert.Contains("refresh_token=rt_old", capturedBody!, StringComparison.Ordinal);
        }

        /// <summary>
        /// A 4xx on the refresh grant is terminal and surfaced for a new sign-in.
        /// </summary>
        [Fact]
        public async Task RefreshAsync_ClientError_ThrowsInvalidOperation()
        {
            ChatGptOAuth.HttpClientFactory = () => new HttpClient(new DelegateHandler(
                (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent("{\"error\":\"invalid_grant\"}"),
                })));

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => ChatGptOAuth.RefreshAsync("oaiapp_abc", "rt_stale")).ConfigureAwait(false);
        }

        /// <summary>
        /// A 5xx on the refresh grant is transient and surfaced as an HTTP failure.
        /// </summary>
        [Fact]
        public async Task RefreshAsync_ServerError_ThrowsHttpRequestException()
        {
            ChatGptOAuth.HttpClientFactory = () => new HttpClient(new DelegateHandler(
                (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent("server error"),
                })));

            await Assert.ThrowsAsync<HttpRequestException>(
                () => ChatGptOAuth.RefreshAsync("oaiapp_abc", "rt_stale")).ConfigureAwait(false);
        }

        /// <summary>
        /// A refresh response without a rotated refresh token is rejected.
        /// </summary>
        [Fact]
        public async Task RefreshAsync_MissingRefreshToken_Throws()
        {
            ChatGptOAuth.HttpClientFactory = () => new HttpClient(new DelegateHandler(
                (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"access_token\":\"at_new\",\"expires_in\":60}"),
                })));

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => ChatGptOAuth.RefreshAsync("oaiapp_abc", "rt_old")).ConfigureAwait(false);
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
