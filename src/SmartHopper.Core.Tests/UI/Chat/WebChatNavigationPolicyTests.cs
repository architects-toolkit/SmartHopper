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

namespace SmartHopper.Core.Tests.UI.Chat
{
    using System;
    using SmartHopper.Core.UI.Chat;
    using Xunit;

    /// <summary>
    /// Unit tests for <see cref="WebChatNavigationPolicy"/>: which documents may own the
    /// privileged bridge, which schemes are bridge events, and which links go external.
    /// </summary>
    public class WebChatNavigationPolicyTests
    {
        [Theory(DisplayName = "IsBridgeScheme detects the JS-to-host schemes")]
        [InlineData("sh://event?type=send", true)]
        [InlineData("SH://event?type=send", true)]
        [InlineData("clipboard://copy?text=x", true)]
        [InlineData("https://example.com", false)]
        [InlineData("about:blank", false)]
        public void IsBridgeScheme_ClassifiesSchemes(string url, bool expected)
        {
            Assert.Equal(expected, WebChatNavigationPolicy.IsBridgeScheme(new Uri(url)));
        }

        [Fact(DisplayName = "IsBridgeScheme rejects null")]
        public void IsBridgeScheme_Null_ReturnsFalse()
        {
            Assert.False(WebChatNavigationPolicy.IsBridgeScheme(null));
        }

        [Theory(DisplayName = "IsExternalLink only allows http(s) to leave via the system browser")]
        [InlineData("https://example.com/page", true)]
        [InlineData("http://example.com", true)]

        // Loopback is still an external link once the document is loaded; only the
        // pending initial document load is allowed through DocumentLoading.
        [InlineData("http://localhost:50001/", true)]
        [InlineData("file:///etc/passwd", false)]
        [InlineData("javascript:alert(1)", false)]
        [InlineData("data:text/html,<p>x</p>", false)]
        [InlineData("sh://event?type=send", false)]
        public void IsExternalLink_ClassifiesLinks(string url, bool expected)
        {
            Assert.Equal(expected, WebChatNavigationPolicy.IsExternalLink(new Uri(url)));
        }

        [Theory(DisplayName = "IsTrustedDocumentUri accepts only embedded/local documents")]
        [InlineData("https://smarthopper.local/", true)]
        [InlineData("https://smarthopper.local/chat", true)]
        [InlineData("https://SMARTHOPPER.LOCAL/", true)]
        [InlineData("about:blank", true)]
        [InlineData("file:///tmp/chat.html", true)]
        [InlineData("data:text/html,<p>injected</p>", true)]

        // Eto's Windows WebView2 handler serves LoadHtml via a loopback HttpListener.
        [InlineData("http://localhost:50001/", true)]
        [InlineData("http://localhost:50001/index.html", true)]
        [InlineData("http://LOCALHOST:50001/", true)]
        [InlineData("http://127.0.0.1:50001/", true)]
        [InlineData("http://[::1]:50001/", true)]
        [InlineData("https://localhost:50001/", false)]
        [InlineData("http://localhost.evil.com/", false)]
        [InlineData("http://mylocalhost/", false)]
        [InlineData("https://evil.example.com/", false)]
        [InlineData("http://smarthopper.local/", false)]
        [InlineData("https://smarthopper.local.evil.com/", false)]
        [InlineData("sh://event?type=send", false)]
        [InlineData("clipboard://copy?text=x", false)]
        [InlineData("javascript:alert(1)", false)]
        public void IsTrustedDocumentUri_ClassifiesDocuments(string url, bool expected)
        {
            Assert.Equal(expected, WebChatNavigationPolicy.IsTrustedDocumentUri(new Uri(url)));
        }

        [Fact(DisplayName = "IsTrustedDocumentUri treats a missing URI as local LoadHtml output")]
        public void IsTrustedDocumentUri_Null_ReturnsTrue()
        {
            Assert.True(WebChatNavigationPolicy.IsTrustedDocumentUri(null));
        }

        [Theory(DisplayName = "BridgeTokenMatches requires an exact non-empty token")]
        [InlineData("abc123", "abc123", true)]
        [InlineData("abc123", "ABC123", false)]
        [InlineData("abc123", "", false)]
        [InlineData("abc123", null, false)]
        [InlineData("abc123", "abc123 ", false)]
        [InlineData("", "", false)]
        [InlineData(null, null, false)]
        public void BridgeTokenMatches_ValidatesTokens(string expected, string actual, bool result)
        {
            Assert.Equal(result, WebChatNavigationPolicy.BridgeTokenMatches(expected, actual));
        }
    }
}
