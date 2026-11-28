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

using System;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace SmartHopper.Providers.OpenAI
{
    /// <summary>
    /// Validated identity extracted from a ChatGPT sign-in ID token.
    /// </summary>
    public sealed class ChatGptIdentity
    {
        /// <summary>The account subject (<c>sub</c> claim).</summary>
        public string Subject { get; set; } = string.Empty;

        /// <summary>The account email (<c>email</c> claim), when present.</summary>
        public string Email { get; set; } = string.Empty;
    }

    /// <summary>
    /// Validates OpenAI ID tokens: RS256 signature against the published JWKS,
    /// issuer, audience, expiry, and the per-attempt nonce.
    /// </summary>
    public static class ChatGptIdToken
    {
        internal const string Issuer = "https://auth.openai.com";
        private const string DiscoveryUrl = "https://auth.openai.com/.well-known/openid-configuration";
        private static readonly TimeSpan ExpiryLeeway = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Fetches the JWKS URI advertised by the OpenAI discovery document.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The absolute JWKS endpoint URI.</returns>
        internal static async Task<Uri> GetJwksUriAsync(CancellationToken cancellationToken)
        {
            using var httpClient = ChatGptOAuth.HttpClientFactory();
            var json = await httpClient.GetStringAsync(DiscoveryUrl, cancellationToken).ConfigureAwait(false);
            var jwksUri = JObject.Parse(json)["jwks_uri"]?.ToString();
            if (string.IsNullOrWhiteSpace(jwksUri))
            {
                throw new InvalidOperationException("OpenAI discovery document did not include a jwks_uri.");
            }

            return new Uri(jwksUri, UriKind.Absolute);
        }

        /// <summary>
        /// Fetches the revocation endpoint advertised by the OpenAI discovery document.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The revocation endpoint URI, or <c>null</c> when not advertised.</returns>
        internal static async Task<Uri?> GetRevocationEndpointAsync(CancellationToken cancellationToken)
        {
            using var httpClient = ChatGptOAuth.HttpClientFactory();
            var json = await httpClient.GetStringAsync(DiscoveryUrl, cancellationToken).ConfigureAwait(false);
            var endpoint = JObject.Parse(json)["revocation_endpoint"]?.ToString();
            return string.IsNullOrWhiteSpace(endpoint) ? null : new Uri(endpoint, UriKind.Absolute);
        }

        /// <summary>
        /// Validates an ID token and returns the verified identity.
        /// </summary>
        /// <param name="idToken">The raw JWT from the token response.</param>
        /// <param name="expectedNonce">The nonce generated for this authorization attempt.</param>
        /// <param name="expectedClientId">The issued client ID the token must be addressed to.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The verified <see cref="ChatGptIdentity"/>.</returns>
        /// <exception cref="InvalidOperationException">The token is malformed, unsigned, expired, or addressed to another issuer/audience/nonce.</exception>
        public static async Task<ChatGptIdentity> ValidateAsync(
            string idToken,
            string expectedNonce,
            string expectedClientId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(idToken))
            {
                throw new InvalidOperationException("The sign-in response did not include an ID token.");
            }

            var parts = idToken.Split('.');
            if (parts.Length != 3)
            {
                throw new InvalidOperationException("The ID token is not a JWT.");
            }

            var header = JObject.Parse(Encoding.UTF8.GetString(Base64UrlDecode(parts[0])));
            var payload = JObject.Parse(Encoding.UTF8.GetString(Base64UrlDecode(parts[1])));

            var algorithm = header["alg"]?.ToString();
            if (!string.Equals(algorithm, "RS256", StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Unsupported ID token algorithm '{algorithm}'.");
            }

            var kid = header["kid"]?.ToString();
            if (string.IsNullOrWhiteSpace(kid))
            {
                throw new InvalidOperationException("The ID token header did not include a key id.");
            }

            var key = await FindKeyAsync(kid, cancellationToken).ConfigureAwait(false);

            using var rsa = RSA.Create();
            rsa.ImportParameters(new RSAParameters
            {
                Modulus = Base64UrlDecode(key["n"]!.ToString()),
                Exponent = Base64UrlDecode(key["e"]!.ToString()),
            });

            var signedBytes = Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}");
            var signature = Base64UrlDecode(parts[2]);
            if (!rsa.VerifyData(signedBytes, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
            {
                throw new InvalidOperationException("The ID token signature is invalid.");
            }

            var issuer = payload["iss"]?.ToString();
            if (!string.Equals(issuer, Issuer, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Unexpected ID token issuer '{issuer}'.");
            }

            if (!AudienceMatches(payload["aud"], expectedClientId))
            {
                throw new InvalidOperationException("The ID token audience does not match the registered client.");
            }

            var expiresUnix = payload["exp"]?.Value<long?>();
            if (expiresUnix == null)
            {
                throw new InvalidOperationException("The ID token did not include an expiry.");
            }

            if (DateTimeOffset.FromUnixTimeSeconds(expiresUnix.Value) + ExpiryLeeway < DateTimeOffset.UtcNow)
            {
                throw new InvalidOperationException("The ID token is expired.");
            }

            var nonce = payload["nonce"]?.ToString();
            if (!string.IsNullOrWhiteSpace(expectedNonce)
                && !string.Equals(nonce, expectedNonce, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The ID token nonce does not match the authorization attempt.");
            }

            var subject = payload["sub"]?.ToString();
            if (string.IsNullOrWhiteSpace(subject))
            {
                throw new InvalidOperationException("The ID token did not include a subject.");
            }

            return new ChatGptIdentity
            {
                Subject = subject,
                Email = payload["email"]?.ToString() ?? string.Empty,
            };
        }

        private static async Task<JToken> FindKeyAsync(string kid, CancellationToken cancellationToken)
        {
            var jwksUri = await GetJwksUriAsync(cancellationToken).ConfigureAwait(false);
            using var httpClient = ChatGptOAuth.HttpClientFactory();
            var json = await httpClient.GetStringAsync(jwksUri, cancellationToken).ConfigureAwait(false);
            var key = JObject.Parse(json)["keys"]?
                .FirstOrDefault(k => string.Equals(k["kid"]?.ToString(), kid, StringComparison.Ordinal));
            if (key == null)
            {
                throw new InvalidOperationException("The ID token signing key was not found in OpenAI's JWKS.");
            }

            return key;
        }

        private static bool AudienceMatches(JToken? audience, string expectedClientId)
        {
            if (audience == null || string.IsNullOrWhiteSpace(expectedClientId))
            {
                return false;
            }

            if (audience.Type == JTokenType.Array)
            {
                return audience.Any(a => string.Equals(a.ToString(), expectedClientId, StringComparison.Ordinal));
            }

            return string.Equals(audience.ToString(), expectedClientId, StringComparison.Ordinal);
        }

        internal static byte[] Base64UrlDecode(string value)
        {
            var base64 = value.Replace('-', '+').Replace('_', '/');
            switch (base64.Length % 4)
            {
                case 2: base64 += "=="; break;
                case 3: base64 += "="; break;
            }

            return Convert.FromBase64String(base64);
        }
    }
}
