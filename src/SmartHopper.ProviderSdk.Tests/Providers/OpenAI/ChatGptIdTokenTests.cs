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
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Newtonsoft.Json.Linq;
    using SmartHopper.Providers.OpenAI;
    using Xunit;

    /// <summary>
    /// Tests for ID-token validation: signature against the advertised JWKS, issuer,
    /// audience, expiry and nonce. Discovery and JWKS fetches go through a fake
    /// <see cref="ChatGptOAuth.HttpClientFactory"/> so no network is required.
    /// </summary>
    [Collection("ProviderSdk")]
    public sealed class ChatGptIdTokenTests : IDisposable
    {
        private const string KeyId = "test-key";
        private const string ClientId = "oaiapp_testclient";

        private readonly Func<HttpClient> previousFactory;

        /// <summary>
        /// Initializes a new instance of the <see cref="ChatGptIdTokenTests"/> class.
        /// </summary>
        public ChatGptIdTokenTests()
        {
            this.previousFactory = ChatGptOAuth.HttpClientFactory;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            ChatGptOAuth.HttpClientFactory = this.previousFactory;
        }

        /// <summary>
        /// A correctly signed token with matching claims validates and yields the identity.
        /// </summary>
        [Fact]
        public async Task ValidateAsync_ValidToken_ReturnsIdentity()
        {
            using var rsa = RSA.Create(2048);
            ChatGptOAuth.HttpClientFactory = JwksFactory(rsa);

            var token = SignJwt(rsa, Payload());
            var identity = await ChatGptIdToken.ValidateAsync(token, "nonce-1", ClientId).ConfigureAwait(false);

            Assert.Equal("user-subject-1", identity.Subject);
            Assert.Equal("user@example.com", identity.Email);
        }

        /// <summary>A token that is not a three-part JWT is rejected before any fetch.</summary>
        [Fact]
        public async Task ValidateAsync_NotJwt_Throws()
        {
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => ChatGptIdToken.ValidateAsync("not-a-jwt", "n", ClientId)).ConfigureAwait(false);
        }

        /// <summary>Algorithms other than RS256 are rejected.</summary>
        [Fact]
        public async Task ValidateAsync_UnsupportedAlgorithm_Throws()
        {
            var header = B64Url("{\"alg\":\"none\",\"kid\":\"test-key\"}");
            var payload = B64Url("{}");
            var token = $"{header}.{payload}.x";

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => ChatGptIdToken.ValidateAsync(token, "n", ClientId)).ConfigureAwait(false);
        }

        /// <summary>A mismatched nonce is rejected.</summary>
        [Fact]
        public async Task ValidateAsync_WrongNonce_Throws()
        {
            using var rsa = RSA.Create(2048);
            ChatGptOAuth.HttpClientFactory = JwksFactory(rsa);

            var token = SignJwt(rsa, Payload());
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => ChatGptIdToken.ValidateAsync(token, "different-nonce", ClientId)).ConfigureAwait(false);
        }

        /// <summary>A token addressed to another client (audience) is rejected.</summary>
        [Fact]
        public async Task ValidateAsync_WrongAudience_Throws()
        {
            using var rsa = RSA.Create(2048);
            ChatGptOAuth.HttpClientFactory = JwksFactory(rsa);

            var token = SignJwt(rsa, Payload());
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => ChatGptIdToken.ValidateAsync(token, "nonce-1", "oaiapp_other")).ConfigureAwait(false);
        }

        /// <summary>A token from another issuer is rejected.</summary>
        [Fact]
        public async Task ValidateAsync_WrongIssuer_Throws()
        {
            using var rsa = RSA.Create(2048);
            ChatGptOAuth.HttpClientFactory = JwksFactory(rsa);

            var payload = Payload();
            payload["iss"] = "https://evil.example";
            var token = SignJwt(rsa, payload);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => ChatGptIdToken.ValidateAsync(token, "nonce-1", ClientId)).ConfigureAwait(false);
        }

        /// <summary>An expired token is rejected.</summary>
        [Fact]
        public async Task ValidateAsync_Expired_Throws()
        {
            using var rsa = RSA.Create(2048);
            ChatGptOAuth.HttpClientFactory = JwksFactory(rsa);

            var payload = Payload();
            payload["exp"] = DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeSeconds();
            var token = SignJwt(rsa, payload);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => ChatGptIdToken.ValidateAsync(token, "nonce-1", ClientId)).ConfigureAwait(false);
        }

        /// <summary>A signature that does not match the advertised key is rejected.</summary>
        [Fact]
        public async Task ValidateAsync_BadSignature_Throws()
        {
            using var signingKey = RSA.Create(2048);
            using var otherKey = RSA.Create(2048);
            ChatGptOAuth.HttpClientFactory = JwksFactory(otherKey); // JWKS advertises a different key

            var token = SignJwt(signingKey, Payload());
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => ChatGptIdToken.ValidateAsync(token, "nonce-1", ClientId)).ConfigureAwait(false);
        }

        /// <summary>A kid absent from the JWKS is rejected.</summary>
        [Fact]
        public async Task ValidateAsync_UnknownKeyId_Throws()
        {
            using var rsa = RSA.Create(2048);
            ChatGptOAuth.HttpClientFactory = JwksFactory(rsa, advertisedKid: "other-key");

            var token = SignJwt(rsa, Payload());
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => ChatGptIdToken.ValidateAsync(token, "nonce-1", ClientId)).ConfigureAwait(false);
        }

        /// <summary>Builds the default valid claim set.</summary>
        private static JObject Payload()
        {
            return new JObject
            {
                ["iss"] = "https://auth.openai.com",
                ["aud"] = ClientId,
                ["exp"] = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds(),
                ["nonce"] = "nonce-1",
                ["sub"] = "user-subject-1",
                ["email"] = "user@example.com",
            };
        }

        /// <summary>Signs a RS256 JWT with the given key and claims.</summary>
        private static string SignJwt(RSA rsa, JObject payload)
        {
            var header = B64Url($"{{\"alg\":\"RS256\",\"kid\":\"{KeyId}\",\"typ\":\"JWT\"}}");
            var body = B64Url(payload.ToString(Newtonsoft.Json.Formatting.None));
            var signature = rsa.SignData(
                Encoding.ASCII.GetBytes($"{header}.{body}"),
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);
            return $"{header}.{body}.{B64Url(signature)}";
        }

        /// <summary>
        /// A client factory that serves the discovery document and a JWKS advertising
        /// <paramref name="rsa"/>'s public parameters under <paramref name="advertisedKid"/>.
        /// </summary>
        private static Func<HttpClient> JwksFactory(RSA rsa, string advertisedKid = KeyId)
        {
            var parameters = rsa.ExportParameters(includePrivateParameters: false);
            var jwks = "{\"keys\":[{\"kty\":\"RSA\",\"kid\":\"" + advertisedKid +
                       "\",\"n\":\"" + B64Url(parameters.Modulus!) +
                       "\",\"e\":\"" + B64Url(parameters.Exponent!) + "\"}]}";

            return () => new HttpClient(new DelegateHandler((request, _) =>
            {
                var body = request.RequestUri!.ToString().Contains("openid-configuration", StringComparison.Ordinal)
                    ? "{\"jwks_uri\":\"https://auth.openai.com/jwks\"}"
                    : jwks;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(body),
                });
            }));
        }

        /// <summary>Encodes raw bytes to unpadded base64url.</summary>
        private static string B64Url(byte[] data)
        {
            return Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        /// <summary>Encodes a UTF-8 string to unpadded base64url.</summary>
        private static string B64Url(string value)
        {
            return B64Url(Encoding.UTF8.GetBytes(value));
        }

        /// <summary>Minimal delegate-based HTTP handler for faking discovery/JWKS.</summary>
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
