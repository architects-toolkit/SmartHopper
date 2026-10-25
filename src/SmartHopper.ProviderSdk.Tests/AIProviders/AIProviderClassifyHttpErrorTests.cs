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
    using SmartHopper.ProviderSdk.AIProviders;
    using SmartHopper.ProviderSdk.Tests.TestHelpers;
    using Xunit;

    /// <summary>Tests shared HTTP error message classification.</summary>
    [Collection("ProviderSdk")]
    public sealed class AIProviderClassifyHttpErrorTests
    {
        [Theory]
        [InlineData(401, "Unauthorized", "TestProvider", "{\"error\":{\"message\":\"Incorrect API key provided\",\"type\":\"invalid_request_error\",\"param\":null,\"code\":\"invalid_api_key\"}}", "HTTP 401: Authentication failed for TestProvider. Check your API key. Incorrect API key provided (invalid_api_key)", false)]
        [InlineData(401, "Unauthorized", "TestProvider", "{\"error\":{\"message\":\"Incorrect API key provided\",\"type\":\"invalid_request_error\",\"param\":null,\"code\":null}}", "HTTP 401: Authentication failed for TestProvider. Check your API key. Incorrect API key provided (invalid_request_error)", false)]
        [InlineData(429, "Too Many Requests", "TestProvider", "{\"type\":\"error\",\"error\":{\"type\":\"rate_limit_error\",\"message\":\"Number of requests has exceeded your rate limit\"}}", "HTTP 429 Too Many Requests: Rate limit exceeded for TestProvider. Please retry after a delay. Number of requests has exceeded your rate limit (rate_limit_error)", true)]
        [InlineData(402, "Payment Required", "TestProvider", "{\"error\":{\"code\":\"payment_required\",\"message\":\"The account balance is zero or negative.\"}}", "HTTP 402 Payment Required: The account balance is zero or negative. (payment_required)", false)]
        [InlineData(502, "Bad Gateway", "TestProvider", "Bad Gateway upstream", "HTTP 502 Bad Gateway: TestProvider gateway error. The upstream server returned an invalid response. Response: Bad Gateway upstream", true)]
        [InlineData(400, "Bad Request", "TestProvider", "{\"detail\":\"nope\"}", "HTTP 400 Bad Request: {\"detail\":\"nope\"}", false)]
        [InlineData(401, "Unauthorized", "TestProvider", "{\"error\":\"unauthorized\"}", "HTTP 401: Authentication failed for TestProvider. Check your API key. Response: {\"error\":\"unauthorized\"}", false)]
        public void ClassifyHttpError_UsesJsonMessageWhenAvailable(
            int statusCode,
            string reasonPhrase,
            string providerName,
            string content,
            string expectedMessage,
            bool expectedNetworkLike)
        {
            var (message, isNetworkLike) = AIProvider.ClassifyHttpError(statusCode, reasonPhrase, content, providerName);

            Assert.Equal(expectedMessage, message);
            Assert.Equal(expectedNetworkLike, isNetworkLike);
        }

        [Theory]
        [InlineData(401)]
        public void ClassifyHttpError_UsesSafeAuthenticationOverride(int statusCode)
        {
            var provider = new FakeAIProvider
            {
                AuthenticationErrorMessageOverride = " Sign in again from the provider settings. ",
            };
            var body = "{\"error\":{\"message\":\"https://evil.example/sign-in\"}}";

            var (message, isNetworkLike) = AIProvider.ClassifyHttpError(statusCode, "Unauthorized", body, provider);

            Assert.Equal($"HTTP {statusCode}: Sign in again from the provider settings.", message);
            Assert.DoesNotContain("evil.example", message, System.StringComparison.Ordinal);
            Assert.False(isNetworkLike);
        }

        [Fact]
        public void ClassifyHttpError_AcceptsThreeHundredCharacterAuthenticationOverride()
        {
            var safeMessage = new string('a', 300);
            var provider = new FakeAIProvider { AuthenticationErrorMessageOverride = safeMessage };

            var (message, isNetworkLike) = AIProvider.ClassifyHttpError(401, "Unauthorized", "server body", provider);

            Assert.Equal($"HTTP 401: {safeMessage}", message);
            Assert.False(isNetworkLike);
        }

        [Fact]
        public void SafeOverride_IsIgnoredFor403()
        {
            var provider = new FakeAIProvider
            {
                AuthenticationErrorMessageOverride = "Sign in again from the provider settings.",
            };
            var body = "{\"error\":{\"code\":\"access_required\",\"message\":\"No access.\"}}";

            var actual = AIProvider.ClassifyHttpError(403, "Forbidden", body, provider);
            var expected = AIProvider.ClassifyHttpError(403, "Forbidden", body, FakeAIProvider.ProviderName);

            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData("Sign in again. Then retry.")]
        [InlineData("Your provider sign-in has expired or was revoked. Use Connect in the provider settings to sign in again.")]
        public void ClassifyHttpError_AcceptsSafeAuthenticationOverrides(string safeMessage)
        {
            var provider = new FakeAIProvider { AuthenticationErrorMessageOverride = safeMessage };

            var (message, isNetworkLike) = AIProvider.ClassifyHttpError(401, "Unauthorized", "server body", provider);

            Assert.Equal($"HTTP 401: {safeMessage}", message);
            Assert.False(isNetworkLike);
        }

        [Theory]
        [InlineData("Visit https://x.example")]
        [InlineData("go to WWW.x.example")]
        [InlineData("Javascript:alert(1)")]
        [InlineData("mailto:a")]
        [InlineData("see data:text/html")]
        [InlineData("mail admin@evil.example")]
        [InlineData("Sign in again at smarthopper-login.com")]
        [InlineData("Go to account.example.org/login")]
        [InlineData("SIGNIN.EXAMPLE.NET")]
        [InlineData("line1\nline2")]
        [InlineData("tab\there")]
        [InlineData("Sign in\u202Eagain")]
        [InlineData("Sign\u200Bin again")]
        [InlineData("")]
        [InlineData("   ")]
        public void ClassifyHttpError_UnsafeAuthenticationOverrideFallsBack(string unsafeMessage)
        {
            var provider = new FakeAIProvider { AuthenticationErrorMessageOverride = unsafeMessage };

            var actual = AIProvider.ClassifyHttpError(401, "Unauthorized", "server body", provider);
            var expected = AIProvider.ClassifyHttpError(401, "Unauthorized", "server body", FakeAIProvider.ProviderName);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void ClassifyHttpError_RejectsAuthenticationOverrideOverThreeHundredCharacters()
        {
            var provider = new FakeAIProvider { AuthenticationErrorMessageOverride = new string('a', 301) };

            var actual = AIProvider.ClassifyHttpError(401, "Unauthorized", "server body", provider);
            var expected = AIProvider.ClassifyHttpError(401, "Unauthorized", "server body", FakeAIProvider.ProviderName);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void ClassifyHttpError_NullOverrideAndProviderUseNameFallback()
        {
            var provider = new FakeAIProvider { AuthenticationErrorMessageOverride = null };
            var providerFallback = AIProvider.ClassifyHttpError(403, "Forbidden", "server body", provider);
            var expectedProviderFallback = AIProvider.ClassifyHttpError(403, "Forbidden", "server body", FakeAIProvider.ProviderName);
            var nullProviderFallback = AIProvider.ClassifyHttpError(403, "Forbidden", "server body", (IAIProvider?)null);
            var expectedNullProviderFallback = AIProvider.ClassifyHttpError(403, "Forbidden", "server body", "provider");

            Assert.Equal(expectedProviderFallback, providerFallback);
            Assert.Equal(expectedNullProviderFallback, nullProviderFallback);
        }

        [Theory]
        [InlineData(400, "Bad Request")]
        [InlineData(402, "Payment Required")]
        [InlineData(429, "Too Many Requests")]
        [InlineData(500, "Internal Server Error")]
        public void ClassifyHttpError_UsesNameFallbackForOtherStatuses(int statusCode, string reasonPhrase)
        {
            var provider = new FakeAIProvider { AuthenticationErrorMessageOverride = "Sign in again in provider settings." };

            var actual = AIProvider.ClassifyHttpError(statusCode, reasonPhrase, "server body", provider);
            var expected = AIProvider.ClassifyHttpError(statusCode, reasonPhrase, "server body", FakeAIProvider.ProviderName);

            Assert.Equal(expected, actual);
        }
    }
}
