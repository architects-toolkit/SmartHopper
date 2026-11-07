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

namespace SmartHopper.ProviderSdk.Tests.Logging
{
    using SmartHopper.ProviderSdk.Logging;
    using Xunit;

    /// <summary>
    /// Tests for <see cref="SmartHopperLogScrubber"/>.
    /// </summary>
    public sealed class SmartHopperLogScrubberTests
    {
        [Theory]
        [InlineData("api_key=secret123", "api_key=<redacted>")]
        [InlineData("token: abc-def_ghi", "token=<redacted>")]
        [InlineData("password=\"my$pass\"", "password=<redacted>")]
        [InlineData("?api_key=secret123&other=value", "?api_key=<redacted>&other=value")]
        [InlineData("{ \"token\": \"bearer_token\" }", "{ \"token\": \"<redacted>\" }")]
        [InlineData("Authorization: Bearer abc123", "Authorization: <redacted>")]
        [InlineData("Authorization=Bearer abc123", "Authorization=<redacted>")]
        [InlineData("Authorization: Basic dXNlcjpwYXNz", "Authorization: <redacted>")]
        [InlineData("Bearer standalone_token", "Bearer <redacted>")]
        [InlineData("X-Api-Key: sk-ant-abc123", "X-Api-Key=<redacted>")]
        [InlineData("x-goog-api-key: AIza...", "x-goog-api-key=<redacted>")]
        [InlineData("BearerToken: mcp-secret", "BearerToken=<redacted>")]
        [InlineData("{ \"x-api-key\": \"sk-ant-abc\" }", "{ \"x-api-key\": \"<redacted>\" }")]
        [InlineData("https://user:pass@host/path", "https://<redacted>@host/path")]
        [InlineData("Contact me at user@example.com", "Contact me at <email>")]
        [InlineData("Server at 1.2.3.4:8080", "Server at <ip>:8080")]
        [InlineData("{ \"password\": \"my$pass\" }", "{ \"password\": \"<redacted>\" }")]
        [InlineData("apikey=secret", "apikey=<redacted>")]
        [InlineData("API_KEY=secret", "API_KEY=<redacted>")]
        [InlineData("x-api-key=sk-ant-abc", "x-api-key=<redacted>")]
        [InlineData("auth=token123", "auth=<redacted>")]
        [InlineData("auth_token=secret", "auth_token=<redacted>")]
        [InlineData("pwd=123456", "pwd=<redacted>")]
        [InlineData("passwd=123456", "passwd=<redacted>")]
        [InlineData("private_key=MIICdwIBADANBgkqhkiG9w0BAQEFAASCX", "private_key=<redacted>")]
        [InlineData("database_url=postgresql://user:pass@host/db", "database_url=<redacted>")]
        [InlineData("bearer token123", "bearer <redacted>")]
        [InlineData("api_key=foo&token=bar", "api_key=<redacted>&token=<redacted>")]
        [InlineData("email user@example.com and ip 1.2.3.4", "email <email> and ip <ip>")]
        public void Scrub_RedactsSensitiveValues(string input, string expected)
        {
            var actual = SmartHopperLogScrubber.Scrub(input);
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Scrub_PreservesNonSensitiveText()
        {
            var input = "model=gpt-4, count=5, ready=true";
            var actual = SmartHopperLogScrubber.Scrub(input);
            Assert.Equal(input, actual);
        }

        [Fact]
        public void Scrub_NullOrEmpty_ReturnsEmptyString()
        {
            Assert.Equal(string.Empty, SmartHopperLogScrubber.Scrub(null));
            Assert.Equal(string.Empty, SmartHopperLogScrubber.Scrub(string.Empty));
        }
    }
}
