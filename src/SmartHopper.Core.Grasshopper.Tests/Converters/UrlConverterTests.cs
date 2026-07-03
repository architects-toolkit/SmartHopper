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

namespace SmartHopper.Core.Grasshopper.Tests.Converters
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Reflection;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using SmartHopper.Core.Grasshopper.Converters;
    using SmartHopper.Core.Grasshopper.Converters.Formats;
    using Xunit;

    /// <summary>
    /// Failure-shape tests for <see cref="UrlConverter"/>, covering the negative cases raised in
    /// GitHub issue #540: invalid URL, login-only pages, bot/human-verification challenges,
    /// oversized pages, and empty/thin pages. Each case must produce an explicit failure with a
    /// classified <see cref="FileConversionFailureReason"/> rather than a false success.
    /// </summary>
    public class UrlConverterTests
    {
        /// <summary>
        /// Minimal <see cref="HttpMessageHandler"/> that returns a canned response for every request,
        /// regardless of the requested URI (robots.txt included), so tests are fully offline and deterministic.
        /// </summary>
        private sealed class StubHttpMessageHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> responder;

            public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
            {
                this.responder = responder;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return Task.FromResult(this.responder(request));
            }
        }

        private static UrlConverter CreateConverterWithStubbedHtml(string html, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            HttpResponseMessage Responder(HttpRequestMessage request)
            {
                // Deny robots.txt so it doesn't affect the test (treated as "not found" -> allowed).
                if (request.RequestUri!.AbsolutePath.EndsWith("/robots.txt", StringComparison.OrdinalIgnoreCase))
                {
                    return new HttpResponseMessage(HttpStatusCode.NotFound);
                }

                return new HttpResponseMessage(statusCode)
                {
                    Content = new StringContent(html, Encoding.UTF8, "text/html"),
                };
            }

            var handler = new StubHttpMessageHandler(Responder);
            var factory = new Func<HttpClient>(() => new HttpClient(handler));

            var ctor = typeof(UrlConverter).GetConstructor(
                BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                new[] { typeof(Func<HttpClient>) },
                null);

            return (UrlConverter)ctor!.Invoke(new object[] { factory });
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "UrlConverter_InvalidUrl_ReturnsInvalidInputFailure [Windows]")]
#else
        [Fact(DisplayName = "UrlConverter_InvalidUrl_ReturnsInvalidInputFailure [Core]")]
#endif
        public async Task UrlConverter_InvalidUrl_ReturnsInvalidInputFailure()
        {
            var converter = new UrlConverter();

            var result = await converter.ConvertAsync("not-a-valid-url", new FileConversionOptions());

            Assert.False(result.IsSuccess);
            Assert.Equal(FileConversionFailureReason.InvalidInput, result.FailureReason);
            Assert.Empty(result.MarkdownContent);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "UrlConverter_NonHttpScheme_ReturnsInvalidInputFailure [Windows]")]
#else
        [Fact(DisplayName = "UrlConverter_NonHttpScheme_ReturnsInvalidInputFailure [Core]")]
#endif
        public async Task UrlConverter_NonHttpScheme_ReturnsInvalidInputFailure()
        {
            var converter = new UrlConverter();

            var result = await converter.ConvertAsync("file:///etc/passwd", new FileConversionOptions());

            Assert.False(result.IsSuccess);
            Assert.Equal(FileConversionFailureReason.InvalidInput, result.FailureReason);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "UrlConverter_LoginWallPage_ReturnsLoginRequiredFailure [Windows]")]
#else
        [Fact(DisplayName = "UrlConverter_LoginWallPage_ReturnsLoginRequiredFailure [Core]")]
#endif
        public async Task UrlConverter_LoginWallPage_ReturnsLoginRequiredFailure()
        {
            const string html = @"<html><head><title>Sign in</title></head><body>
                <h1>Please sign in</h1>
                <form>
                    <input type='email' name='email' />
                    <input type='password' name='password' />
                    <button type='submit'>Sign In</button>
                </form>
            </body></html>";

            var converter = CreateConverterWithStubbedHtml(html);

            var result = await converter.ConvertAsync("https://example.com/members", new FileConversionOptions());

            Assert.False(result.IsSuccess);
            Assert.Equal(FileConversionFailureReason.LoginRequired, result.FailureReason);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "UrlConverter_HttpUnauthorized_ReturnsLoginRequiredFailure [Windows]")]
#else
        [Fact(DisplayName = "UrlConverter_HttpUnauthorized_ReturnsLoginRequiredFailure [Core]")]
#endif
        public async Task UrlConverter_HttpUnauthorized_ReturnsLoginRequiredFailure()
        {
            var converter = CreateConverterWithStubbedHtml("<html><body>Unauthorized</body></html>", HttpStatusCode.Unauthorized);

            var result = await converter.ConvertAsync("https://example.com/private", new FileConversionOptions());

            Assert.False(result.IsSuccess);
            Assert.Equal(FileConversionFailureReason.LoginRequired, result.FailureReason);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "UrlConverter_BotChallengePage_ReturnsBotChallengeFailure [Windows]")]
#else
        [Fact(DisplayName = "UrlConverter_BotChallengePage_ReturnsBotChallengeFailure [Core]")]
#endif
        public async Task UrlConverter_BotChallengePage_ReturnsBotChallengeFailure()
        {
            const string html = @"<html><head><title>Just a moment...</title></head><body>
                <div class='cf-turnstile' data-sitekey='abc'></div>
                <p>Checking your browser before accessing example.com.</p>
            </body></html>";

            var converter = CreateConverterWithStubbedHtml(html);

            var result = await converter.ConvertAsync("https://example.com/article", new FileConversionOptions());

            Assert.False(result.IsSuccess);
            Assert.Equal(FileConversionFailureReason.BotChallenge, result.FailureReason);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "UrlConverter_RecaptchaPage_ReturnsBotChallengeFailure [Windows]")]
#else
        [Fact(DisplayName = "UrlConverter_RecaptchaPage_ReturnsBotChallengeFailure [Core]")]
#endif
        public async Task UrlConverter_RecaptchaPage_ReturnsBotChallengeFailure()
        {
            const string html = @"<html><body>
                <div class='g-recaptcha' data-sitekey='abc'></div>
                <script src='https://www.google.com/recaptcha/api.js'></script>
            </body></html>";

            var converter = CreateConverterWithStubbedHtml(html);

            var result = await converter.ConvertAsync("https://example.com/form", new FileConversionOptions());

            Assert.False(result.IsSuccess);
            Assert.Equal(FileConversionFailureReason.BotChallenge, result.FailureReason);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "UrlConverter_HugePage_ReturnsContentTooLargeFailure [Windows]")]
#else
        [Fact(DisplayName = "UrlConverter_HugePage_ReturnsContentTooLargeFailure [Core]")]
#endif
        public async Task UrlConverter_HugePage_ReturnsContentTooLargeFailure()
        {
            var hugeBody = new string('a', 2000);
            var html = $"<html><body><p>{hugeBody}</p></body></html>";

            var converter = CreateConverterWithStubbedHtml(html);
            var options = new FileConversionOptions { MaxDownloadBytes = 500 };

            var result = await converter.ConvertAsync("https://example.com/huge", options);

            Assert.False(result.IsSuccess);
            Assert.Equal(FileConversionFailureReason.ContentTooLarge, result.FailureReason);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "UrlConverter_EmptyPage_ReturnsEmptyContentFailure [Windows]")]
#else
        [Fact(DisplayName = "UrlConverter_EmptyPage_ReturnsEmptyContentFailure [Core]")]
#endif
        public async Task UrlConverter_EmptyPage_ReturnsEmptyContentFailure()
        {
            const string html = "<html><head><title></title></head><body></body></html>";

            var converter = CreateConverterWithStubbedHtml(html);

            var result = await converter.ConvertAsync("https://example.com/empty", new FileConversionOptions());

            Assert.False(result.IsSuccess);
            Assert.Equal(FileConversionFailureReason.EmptyContent, result.FailureReason);
            Assert.Empty(result.MarkdownContent);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "UrlConverter_ThinPage_ReturnsEmptyContentFailure [Windows]")]
#else
        [Fact(DisplayName = "UrlConverter_ThinPage_ReturnsEmptyContentFailure [Core]")]
#endif
        public async Task UrlConverter_ThinPage_ReturnsEmptyContentFailure()
        {
            // Well below the default MinContentLength (40 chars) threshold.
            const string html = "<html><body><p>Hi</p></body></html>";

            var converter = CreateConverterWithStubbedHtml(html);

            var result = await converter.ConvertAsync("https://example.com/thin", new FileConversionOptions());

            Assert.False(result.IsSuccess);
            Assert.Equal(FileConversionFailureReason.EmptyContent, result.FailureReason);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "UrlConverter_NotFoundStatus_ReturnsNetworkErrorFailure [Windows]")]
#else
        [Fact(DisplayName = "UrlConverter_NotFoundStatus_ReturnsNetworkErrorFailure [Core]")]
#endif
        public async Task UrlConverter_NotFoundStatus_ReturnsNetworkErrorFailure()
        {
            var converter = CreateConverterWithStubbedHtml("<html><body>Not found</body></html>", HttpStatusCode.NotFound);

            var result = await converter.ConvertAsync("https://example.com/missing", new FileConversionOptions());

            Assert.False(result.IsSuccess);
            Assert.Equal(FileConversionFailureReason.NetworkError, result.FailureReason);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "UrlConverter_GenuinePage_ReturnsSuccessWithNoFailureReason [Windows]")]
#else
        [Fact(DisplayName = "UrlConverter_GenuinePage_ReturnsSuccessWithNoFailureReason [Core]")]
#endif
        public async Task UrlConverter_GenuinePage_ReturnsSuccessWithNoFailureReason()
        {
            const string html = @"<html><head><title>Sample Article</title></head><body>
                <article>
                    <h1>Sample Article</h1>
                    <p>This is a reasonably long paragraph of genuine article content that should
                    comfortably clear the minimum content length threshold used to detect empty or
                    thin pages, while containing no bot-challenge or login-wall signatures at all.</p>
                </article>
            </body></html>";

            var converter = CreateConverterWithStubbedHtml(html);

            var result = await converter.ConvertAsync("https://example.com/article", new FileConversionOptions());

            Assert.True(result.IsSuccess);
            Assert.Equal(FileConversionFailureReason.None, result.FailureReason);
            Assert.NotEmpty(result.MarkdownContent);
        }
    }
}
