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
    using Newtonsoft.Json.Linq;
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

        private static UrlConverter CreateConverterWithStubbedResponder(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            var handler = new StubHttpMessageHandler(responder);
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

#if NET7_WINDOWS
        [Fact(DisplayName = "UrlConverter_DiscourseTopicUrl_ReturnsRawMarkdown [Windows]")]
#else
        [Fact(DisplayName = "UrlConverter_DiscourseTopicUrl_ReturnsRawMarkdown [Core]")]
#endif
        public async Task UrlConverter_DiscourseTopicUrl_ReturnsRawMarkdown()
        {
            var topicJson = new JObject
            {
                ["id"] = 204893,
                ["title"] = "G2 Feature Requests: Grasshopper2 Icon Editor",
                ["slug"] = "g2-feature-requests-grasshopper2-icon-editor",
                ["post_stream"] = new JObject
                {
                    ["posts"] = new JArray
                    {
                        new JObject
                        {
                            ["username"] = "archinate1",
                            ["created_at"] = "2025-05-22T14:50:51.740Z",
                            ["raw"] = "I have been working with the Icon editor...",
                        },
                    },
                },
            };

            var converter = CreateConverterWithStubbedResponder(request =>
            {
                if (request.RequestUri!.AbsolutePath.EndsWith("/robots.txt", StringComparison.OrdinalIgnoreCase))
                {
                    return new HttpResponseMessage(HttpStatusCode.NotFound);
                }

                if (request.RequestUri.AbsolutePath.EndsWith(".json"))
                {
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(topicJson.ToString(), Encoding.UTF8, "application/json"),
                    };
                }

                return new HttpResponseMessage(HttpStatusCode.NotFound);
            });

            var result = await converter.ConvertAsync(
                "https://discourse.mcneel.com/t/g2-feature-requests-grasshopper2-icon-editor/204893",
                new FileConversionOptions());

            Assert.True(result.IsSuccess);
            Assert.Equal(FileConversionFailureReason.None, result.FailureReason);
            Assert.Contains("# G2 Feature Requests: Grasshopper2 Icon Editor", result.MarkdownContent);
            Assert.Contains("## archinate1 – 2025-05-22T14:50:51.740Z", result.MarkdownContent);
            Assert.Contains("I have been working with the Icon editor...", result.MarkdownContent);
            Assert.DoesNotContain("Related topics", result.MarkdownContent);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "UrlConverter_DiscoursePostUrl_ReturnsRawMarkdown [Windows]")]
#else
        [Fact(DisplayName = "UrlConverter_DiscoursePostUrl_ReturnsRawMarkdown [Core]")]
#endif
        public async Task UrlConverter_DiscoursePostUrl_ReturnsRawMarkdown()
        {
            var postJson = new JObject
            {
                ["id"] = 1073507,
                ["username"] = "archinate1",
                ["created_at"] = "2025-05-22T14:50:51.740Z",
                ["raw"] = "I have been working with the Icon editor...",
                ["topic_id"] = 204893,
                ["topic_slug"] = "g2-feature-requests-grasshopper2-icon-editor",
                ["post_number"] = 1,
            };

            var converter = CreateConverterWithStubbedResponder(request =>
            {
                if (request.RequestUri!.AbsolutePath.EndsWith("/robots.txt", StringComparison.OrdinalIgnoreCase))
                {
                    return new HttpResponseMessage(HttpStatusCode.NotFound);
                }

                if (request.RequestUri.AbsolutePath.Contains("/posts/"))
                {
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(postJson.ToString(), Encoding.UTF8, "application/json"),
                    };
                }

                return new HttpResponseMessage(HttpStatusCode.NotFound);
            });

            var result = await converter.ConvertAsync(
                "https://discourse.mcneel.com/posts/1073507",
                new FileConversionOptions());

            Assert.True(result.IsSuccess);
            Assert.Contains("## archinate1 – 2025-05-22T14:50:51.740Z", result.MarkdownContent);
            Assert.Contains("I have been working with the Icon editor...", result.MarkdownContent);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "UrlConverter_DiscourseTopicUrlWithPostNumber_ReturnsRawMarkdown [Windows]")]
#else
        [Fact(DisplayName = "UrlConverter_DiscourseTopicUrlWithPostNumber_ReturnsRawMarkdown [Core]")]
#endif
        public async Task UrlConverter_DiscourseTopicUrlWithPostNumber_ReturnsRawMarkdown()
        {
            var topicJson = new JObject
            {
                ["id"] = 204893,
                ["title"] = "G2 Feature Requests",
                ["slug"] = "g2-feature-requests",
                ["post_stream"] = new JObject
                {
                    ["posts"] = new JArray
                    {
                        new JObject
                        {
                            ["username"] = "archinate1",
                            ["created_at"] = "2025-05-22T14:50:51.740Z",
                            ["raw"] = "First post",
                        },
                    },
                },
            };

            var converter = CreateConverterWithStubbedResponder(request =>
            {
                if (request.RequestUri!.AbsolutePath.EndsWith("/robots.txt", StringComparison.OrdinalIgnoreCase))
                {
                    return new HttpResponseMessage(HttpStatusCode.NotFound);
                }

                if (request.RequestUri.AbsolutePath.EndsWith("/t/204893.json"))
                {
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(topicJson.ToString(), Encoding.UTF8, "application/json"),
                    };
                }

                return new HttpResponseMessage(HttpStatusCode.NotFound);
            });

            var result = await converter.ConvertAsync(
                "https://discourse.mcneel.com/t/g2-feature-requests/204893/1",
                new FileConversionOptions());

            Assert.True(result.IsSuccess);
            Assert.Contains("# G2 Feature Requests", result.MarkdownContent);
            Assert.Contains("First post", result.MarkdownContent);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "UrlConverter_WikipediaUrl_ReturnsMarkdownWithHeadingsAndTables [Windows]")]
#else
        [Fact(DisplayName = "UrlConverter_WikipediaUrl_ReturnsMarkdownWithHeadingsAndTables [Core]")]
#endif
        public async Task UrlConverter_WikipediaUrl_ReturnsMarkdownWithHeadingsAndTables()
        {
            var htmlContent =
                "<div class=\"mw-parser-output\">" +
                "<h2><span class=\"mw-headline\" id=\"Goals\">Goals</span><span class=\"mw-editsection\">[<a href=\"/w/...\">edit</a>]</span></h2>\n" +
                "<p>General goals overview.</p>\n" +
                "<h3><span class=\"mw-headline\" id=\"Reasoning\">Reasoning and problem-solving</span><span class=\"mw-editsection\">[<a href=\"/w/...\">edit</a>]</span></h3>\n" +
                "<p>Reasoning content.<sup class=\"reference\"><a href=\"#cite_note-1\">[1]</a></sup></p>\n" +
                "<table>\n" +
                "<tr><th>Technique</th><th>Description</th></tr>\n" +
                "<tr><td>Search</td><td>Finds paths</td></tr>\n" +
                "</table>\n" +
                "<div class=\"hatnote\">For other uses, see Test.</div>\n" +
                "<table class=\"navbox\"><tr><td>Navbox content</td></tr></table>\n" +
                "</div>";

            var parseJson = new JObject
            {
                ["parse"] = new JObject
                {
                    ["title"] = "Artificial intelligence",
                    ["text"] = new JObject
                    {
                        ["*"] = htmlContent,
                    },
                },
            };

            var converter = CreateConverterWithStubbedResponder(request =>
            {
                if (request.RequestUri!.AbsolutePath.EndsWith("/robots.txt", StringComparison.OrdinalIgnoreCase))
                {
                    return new HttpResponseMessage(HttpStatusCode.NotFound);
                }

                if (request.RequestUri.AbsolutePath.Contains("/api.php"))
                {
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(parseJson.ToString(), Encoding.UTF8, "application/json"),
                    };
                }

                return new HttpResponseMessage(HttpStatusCode.NotFound);
            });

            var result = await converter.ConvertAsync(
                "https://en.wikipedia.org/wiki/Artificial_intelligence",
                new FileConversionOptions());

            Assert.True(result.IsSuccess);
            Assert.Contains("# Artificial intelligence", result.MarkdownContent);
            Assert.Contains("## Goals", result.MarkdownContent);
            Assert.Contains("### Reasoning and problem-solving", result.MarkdownContent);
            Assert.Contains("| Technique | Description |", result.MarkdownContent);
            Assert.Contains("| Search | Finds paths |", result.MarkdownContent);
            Assert.DoesNotContain("== Goals ==", result.MarkdownContent);
            Assert.DoesNotContain("[edit]", result.MarkdownContent);
            Assert.DoesNotContain("Navbox content", result.MarkdownContent);
            Assert.DoesNotContain("For other uses", result.MarkdownContent);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "UrlConverter_WikipediaUrl_ConvertsRelativeLinksToAbsolute [Windows]")]
#else
        [Fact(DisplayName = "UrlConverter_WikipediaUrl_ConvertsRelativeLinksToAbsolute [Core]")]
#endif
        public async Task UrlConverter_WikipediaUrl_ConvertsRelativeLinksToAbsolute()
        {
            var htmlContent =
                "<div class=\"mw-parser-output\">" +
                "<p>See <a href=\"/wiki/Computer_vision\" title=\"Computer vision\">computer vision</a> and " +
                "<a href=\"//en.wikipedia.org/wiki/Machine_learning\">machine learning</a>.</p>" +
                "<img src=\"/wiki/Special:FilePath/Sample.png\" alt=\"sample\" />" +
                "</div>";

            var parseJson = new JObject
            {
                ["parse"] = new JObject
                {
                    ["title"] = "Artificial intelligence",
                    ["text"] = new JObject
                    {
                        ["*"] = htmlContent,
                    },
                },
            };

            var converter = CreateConverterWithStubbedResponder(request =>
            {
                if (request.RequestUri!.AbsolutePath.EndsWith("/robots.txt", StringComparison.OrdinalIgnoreCase))
                {
                    return new HttpResponseMessage(HttpStatusCode.NotFound);
                }

                if (request.RequestUri.AbsolutePath.Contains("/api.php"))
                {
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(parseJson.ToString(), Encoding.UTF8, "application/json"),
                    };
                }

                return new HttpResponseMessage(HttpStatusCode.NotFound);
            });

            var result = await converter.ConvertAsync(
                "https://en.wikipedia.org/wiki/Artificial_intelligence",
                new FileConversionOptions());

            Assert.True(result.IsSuccess);
            Assert.Contains("https://en.wikipedia.org/wiki/Computer_vision", result.MarkdownContent);
            Assert.Contains("https://en.wikipedia.org/wiki/Machine_learning", result.MarkdownContent);
            Assert.Contains("https://en.wikipedia.org/wiki/Special:FilePath/Sample.png", result.MarkdownContent);
            Assert.DoesNotContain("href=\"/wiki/Computer_vision\"", result.MarkdownContent);
        }
    }
}
