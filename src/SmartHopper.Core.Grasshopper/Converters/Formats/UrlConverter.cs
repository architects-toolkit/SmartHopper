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

/*
 * Portions of this code inspired by:
 * https://github.com/deanmalmgren/textract
 * MIT License
 * Copyright (c) Dean Malmgren
 *
 * Key concepts adapted:
 * - URL-based converter using IFileConverter pattern
 * - Dispatcher architecture for content-type routing
 * - Specialized handlers for different URL types
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SmartHopper.Core.Grasshopper.AITools;
using SmartHopper.Core.Grasshopper.Utils.Internal;
using SmartHopper.Infrastructure.Utils;

namespace SmartHopper.Core.Grasshopper.Converters.Formats
{
    /// <summary>
    /// Converter for URLs (web pages).
    /// Fetches content from URLs with specialized handlers for Wikipedia, GitHub, Discourse, Stack Exchange,
    /// and falls back to HtmlConverter for generic pages.
    /// </summary>
    public sealed class UrlConverter : IFileConverter
    {
        private readonly HtmlConverter htmlConverter;
        private readonly Func<HttpClient> httpClientFactory;

        public UrlConverter()
            : this(CreateDefaultHttpClient)
        {
        }

        /// <summary>
        /// Creates a converter with a custom <see cref="HttpClient"/> factory. Intended for unit testing
        /// with a mocked <see cref="HttpMessageHandler"/>; production code should use the parameterless constructor.
        /// </summary>
        internal UrlConverter(Func<HttpClient> httpClientFactory)
        {
            this.htmlConverter = new HtmlConverter();
            this.httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        }

        private static HttpClient CreateDefaultHttpClient()
        {
            var client = new HttpClient();
            string version = VersionHelper.GetDisplayVersion();
            client.DefaultRequestHeaders.UserAgent.ParseAdd($"SmartHopper/{version} (+https://github.com/architects-toolkit/SmartHopper)");
            return client;
        }

        public IEnumerable<string> SupportedExtensions => new[] { ".url" }; // Pseudo-extension for URL handling

        public async Task<FileConversionResult> ConvertAsync(string filePath, FileConversionOptions options)
        {
            // For UrlConverter, filePath is actually a URL
            var url = filePath;

            if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out Uri uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                return FileConversionResult.Failure("url", $"Invalid URL: {url}", FileConversionFailureReason.InvalidInput);
            }

            var effectiveOptions = options ?? new FileConversionOptions();
            var maxDownloadBytes = effectiveOptions.MaxDownloadBytes > 0 ? effectiveOptions.MaxDownloadBytes : FileConversionOptions.DefaultMaxDownloadBytes;
            var minContentLength = effectiveOptions.MinContentLength > 0 ? effectiveOptions.MinContentLength : FileConversionOptions.DefaultMinContentLength;

            try
            {
                using var httpClient = this.httpClientFactory();

                // Check robots.txt
                Uri robotsUri = new (uri.GetLeftPart(UriPartial.Authority) + "/robots.txt");
                try
                {
                    var robotsResponse = await httpClient.GetAsync(robotsUri).ConfigureAwait(false);
                    if (robotsResponse.IsSuccessStatusCode)
                    {
                        string robotsContent = await robotsResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                        var robots = new WebUtilities(robotsContent);
                        if (!robots.IsPathAllowed(uri.PathAndQuery))
                        {
                            return FileConversionResult.Failure("url", $"Access to '{uri}' is disallowed by robots.txt.", FileConversionFailureReason.Other);
                        }
                    }
                }
                catch (HttpRequestException)
                {
                    // Treat failure to fetch robots.txt as allowed
                }

                string? textContent = null;
                string contentFormat = "markdown";
                bool sawPasswordField = false;
                var result = new FileConversionResult { DetectedFormat = "url" };

                // Try specialized fetchers first
                if (IsWikimediaHost(uri))
                {
                    textContent = await this.TryFetchWikimediaMarkdownAsync(uri, httpClient, effectiveOptions).ConfigureAwait(false);
                }

                if (string.IsNullOrWhiteSpace(textContent))
                {
                    var gitResult = await TryFetchGitHostContentAsync(uri, httpClient).ConfigureAwait(false);
                    if (gitResult != null && !string.IsNullOrWhiteSpace(gitResult.Content))
                    {
                        textContent = gitResult.Content;
                        contentFormat = gitResult.Format;
                    }
                }

                if (string.IsNullOrWhiteSpace(textContent))
                {
                    var stackExchangeContent = await TryFetchStackExchangeContentAsync(uri, httpClient).ConfigureAwait(false);
                    if (!string.IsNullOrWhiteSpace(stackExchangeContent))
                    {
                        textContent = stackExchangeContent;
                        contentFormat = "markdown";
                    }
                }

                if (string.IsNullOrWhiteSpace(textContent))
                {
                    var discourseRaw = await TryFetchDiscourseRawContentAsync(uri, httpClient).ConfigureAwait(false);
                    if (!string.IsNullOrWhiteSpace(discourseRaw))
                    {
                        textContent = discourseRaw;
                        contentFormat = "markdown";
                    }
                }

                // Fallback to generic HTML conversion using HtmlConverter
                if (string.IsNullOrWhiteSpace(textContent))
                {
                    using var response = await httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);

                    if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
                    {
                        return FileConversionResult.Failure(
                            "url",
                            $"Access to '{uri}' requires authentication (HTTP {(int)response.StatusCode} {response.StatusCode}).",
                            FileConversionFailureReason.LoginRequired);
                    }

                    if (!response.IsSuccessStatusCode)
                    {
                        return FileConversionResult.Failure(
                            "url",
                            $"Failed to fetch '{uri}': HTTP {(int)response.StatusCode} {response.StatusCode}.",
                            FileConversionFailureReason.NetworkError);
                    }

                    var (html, tooLarge) = await ReadBoundedContentAsync(response, maxDownloadBytes).ConfigureAwait(false);
                    if (tooLarge)
                    {
                        return FileConversionResult.Failure(
                            "url",
                            $"Page content at '{uri}' exceeds the maximum allowed size ({maxDownloadBytes / 1_000_000}MB).",
                            FileConversionFailureReason.ContentTooLarge);
                    }

                    if (html == null)
                    {
                        return FileConversionResult.Failure("url", $"Failed to read content from '{uri}'.", FileConversionFailureReason.NetworkError);
                    }

                    Debug.WriteLine($"[UrlConverter] Fetched HTML from {url}. Length: {html.Length}");

                    sawPasswordField = HasPasswordField(html);

                    if (LooksLikeBotChallenge(html))
                    {
                        return FileConversionResult.Failure(
                            "url",
                            $"The page at '{uri}' returned a bot/human-verification challenge instead of content.",
                            FileConversionFailureReason.BotChallenge);
                    }

                    if (sawPasswordField && HasLoginPhrase(html))
                    {
                        return FileConversionResult.Failure(
                            "url",
                            $"The page at '{uri}' requires authentication (login wall) and did not return readable content.",
                            FileConversionFailureReason.LoginRequired);
                    }

                    // Pass the fetched URL as base URL so relative links/images become absolute.
                    var htmlOptions = effectiveOptions.Clone();
                    htmlOptions.BaseUrl = uri.ToString();

                    // Save HTML to temp file for HtmlConverter
                    var tempFile = Path.GetTempFileName();
                    try
                    {
                        await File.WriteAllTextAsync(tempFile, html).ConfigureAwait(false);
                        var htmlResult = await this.htmlConverter.ConvertAsync(tempFile, htmlOptions).ConfigureAwait(false);
                        textContent = htmlResult.MarkdownContent;
                        result.Metadata = htmlResult.Metadata;
                        result.Warnings = htmlResult.Warnings;
                        result.IsSuccess = htmlResult.IsSuccess;
                    }
                    finally
                    {
                        if (File.Exists(tempFile))
                        {
                            File.Delete(tempFile);
                        }
                    }
                }

                var trimmedLength = textContent?.Trim().Length ?? 0;
                if (trimmedLength < minContentLength)
                {
                    var reason = sawPasswordField ? FileConversionFailureReason.LoginRequired : FileConversionFailureReason.EmptyContent;
                    var message = sawPasswordField
                        ? $"The page at '{uri}' appears to require authentication and returned too little content ({trimmedLength} characters). This is not considered a successful conversion."
                        : $"The page at '{uri}' returned too little content ({trimmedLength} characters). This is not considered a successful conversion.";
                    return FileConversionResult.Failure("url", message, reason);
                }

                if (LooksLikeClientSideRenderingPlaceholder(textContent!))
                {
                    return FileConversionResult.Failure(
                        "url",
                        $"The page at '{uri}' returned a JavaScript SPA loading placeholder instead of real content. The page requires a JavaScript runtime to render. Try fetching via a dedicated API or use a different URL.",
                        FileConversionFailureReason.BotChallenge);
                }

                // Apply the same Markdown post-processing that FileConverterRegistry uses for files,
                // so web URLs also get clean ordered-list numbering and heading/list spacing.
                result.MarkdownContent = MarkdownListRenumberer.Renumber(textContent!);
                result.MarkdownContent = MarkdownStyleCleanup.Cleanup(result.MarkdownContent);
                result.Metadata["source"] = url;
                result.Metadata["format"] = contentFormat;
                result.IsSuccess = true;
                result.FailureReason = FileConversionFailureReason.None;

                return result;
            }
            catch (HttpRequestException ex)
            {
                Debug.WriteLine($"[UrlConverter] Network error: {ex.Message}");
                return FileConversionResult.Failure("url", $"Network error fetching URL: {ex.Message}", FileConversionFailureReason.NetworkError);
            }
            catch (TaskCanceledException ex)
            {
                Debug.WriteLine($"[UrlConverter] Timeout: {ex.Message}");
                return FileConversionResult.Failure("url", $"Timed out fetching URL: {url}", FileConversionFailureReason.NetworkError);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[UrlConverter] Error: {ex.Message}");
                return FileConversionResult.Failure("url", $"Error fetching URL: {ex.Message}", FileConversionFailureReason.Other);
            }
        }

        /// <summary>
        /// Reads the response body up to <paramref name="maxBytes"/>. Returns (null, true) if the declared
        /// or actual content length exceeds the limit, so oversized pages fail fast without buffering
        /// unbounded content in memory.
        /// </summary>
        private static async Task<(string? Content, bool TooLarge)> ReadBoundedContentAsync(HttpResponseMessage response, long maxBytes)
        {
            var declaredLength = response.Content.Headers.ContentLength;
            if (declaredLength.HasValue && declaredLength.Value > maxBytes)
            {
                return (null, true);
            }

            using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            using var memoryStream = new MemoryStream();
            var buffer = new byte[81920];
            long totalRead = 0;
            int bytesRead;
            while ((bytesRead = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length)).ConfigureAwait(false)) > 0)
            {
                totalRead += bytesRead;
                if (totalRead > maxBytes)
                {
                    return (null, true);
                }

                memoryStream.Write(buffer, 0, bytesRead);
            }

            memoryStream.Position = 0;
            using var reader = new StreamReader(memoryStream, Encoding.UTF8);
            var content = await reader.ReadToEndAsync().ConfigureAwait(false);
            return (content, false);
        }

        /// <summary>
        /// Detects common bot/human-verification challenge signatures (CAPTCHA providers and
        /// anti-bot vendor interstitials) in raw HTML, so challenge markup is never mistaken for content.
        /// </summary>
        private static bool LooksLikeBotChallenge(string html)
        {
            foreach (var signature in BotChallengeSignatures)
            {
                if (html.Contains(signature, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Detects whether the extracted Markdown content is a client-side rendering placeholder
        /// (e.g. a React/Vue/Angular SPA loading state) rather than real page content.
        /// These pages require JavaScript execution and return only a skeleton when fetched without a browser.
        /// </summary>
        private static bool LooksLikeClientSideRenderingPlaceholder(string markdown)
        {
            if (string.IsNullOrWhiteSpace(markdown))
            {
                return false;
            }

            foreach (var signature in ClientSideRenderingSignatures)
            {
                if (markdown.Contains(signature, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasPasswordField(string html)
        {
            return PasswordFieldRegex.IsMatch(html);
        }

        private static bool HasLoginPhrase(string html)
        {
            foreach (var phrase in LoginPhraseSignatures)
            {
                if (html.Contains(phrase, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static readonly Regex PasswordFieldRegex = new (@"type\s*=\s*[""']?password[""']?", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly string[] BotChallengeSignatures =
        {
            "g-recaptcha",
            "recaptcha/api.js",
            "www.google.com/recaptcha",
            "hcaptcha.com",
            "h-captcha",
            "cf-turnstile",
            "challenges.cloudflare.com/turnstile",
            "just a moment...",
            "attention required! | cloudflare",
            "checking your browser before accessing",
            "cf-chl-",
            "captcha-delivery.com",
            "_datadome",
            "px-captcha",
            "perimeterx",
            "geetest",
        };

        private static readonly string[] LoginPhraseSignatures =
        {
            "please log in",
            "please sign in",
            "you must be logged in",
            "you must log in",
            "log in to continue",
            "sign in to continue",
            "login required",
            "members only",
            "subscribers only",
            "this content is only available to subscribers",
            "you need to sign in",
            "create an account to continue",
        };

        /// <summary>
        /// Patterns that indicate the extracted Markdown is a JavaScript SPA loading skeleton
        /// rather than real page content. Pages matching these signals require a headless browser.
        /// Only include highly specific strings that do not appear in normal page content.
        /// </summary>
        private static readonly string[] ClientSideRenderingSignatures =
        {
            // GitHub SPA placeholder — unique enough to be unambiguous
            "there was an error while loading. please reload this page.",
            // Standard noscript / CSR fallback messages
            "you need to enable javascript to run this app",
            "please enable javascript to continue",
            "this page requires javascript to function",
            "javascript is required to view this page",
        };

        #region Specialized Fetchers

        private async Task<string?> TryFetchWikimediaMarkdownAsync(Uri pageUri, HttpClient httpClient, FileConversionOptions options)
        {
            if (!TryExtractWikimediaTitle(pageUri, out string? title))
            {
                return null;
            }

            var apiUri = new Uri($"{pageUri.Scheme}://{pageUri.Host}/w/api.php?action=parse&page={Uri.EscapeDataString(title)}&prop=text&format=json");
            try
            {
                var response = await httpClient.GetAsync(apiUri).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                var payload = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var json = JObject.Parse(payload);
                var html = json.SelectToken("parse.text.*")?.ToString();
                if (string.IsNullOrWhiteSpace(html))
                {
                    return null;
                }

                var titleValue = json.SelectToken("parse.title")?.ToString();

                html = CleanupWikimediaHtml(html, pageUri);

                // Wrap the parsed content in a minimal HTML document so the converter can process it.
                var encodedTitle = System.Net.WebUtility.HtmlEncode(titleValue ?? title);
                var wrappedHtml = $"<html><head><title>{encodedTitle}</title></head><body>{html}</body></html>";

                var htmlOptions = new FileConversionOptions
                {
                    PreserveTableStructure = options.PreserveTableStructure,
                    IncludeLinks = options.IncludeLinks,
                    IncludeImages = options.IncludeImages,
                    HtmlReadabilityMode = ReadabilityMode.Off,
                    MaxContentLength = options.MaxContentLength,
                    MinContentLength = options.MinContentLength,
                    MaxDownloadBytes = options.MaxDownloadBytes,
                };

                var htmlResult = await this.htmlConverter.ConvertHtmlStringAsync(wrappedHtml, htmlOptions).ConfigureAwait(false);
                var markdown = htmlResult.MarkdownContent?.Trim();
                if (string.IsNullOrWhiteSpace(markdown))
                {
                    return null;
                }

                if (!string.IsNullOrWhiteSpace(titleValue))
                {
                    markdown = $"# {titleValue.Trim()}\n\n{markdown}";
                }

                return markdown;
            }
            catch (HttpRequestException)
            {
                return null;
            }
            catch (JsonReaderException)
            {
                return null;
            }
            catch (JsonSerializationException)
            {
                return null;
            }
        }

        private static async Task<string?> TryFetchDiscourseRawContentAsync(Uri uri, HttpClient httpClient)
        {
            if (!DiscourseForumService.IsDiscourseUrl(uri))
            {
                return null;
            }

            string baseUrl = $"{uri.Scheme}://{uri.Host}";

            try
            {
                if (DiscourseForumService.TryParsePostUrl(uri, out int postId))
                {
                    var postJson = await DiscourseForumService.FetchPostAsync(httpClient, baseUrl, postId).ConfigureAwait(false);
                    var markdown = DiscourseForumService.FormatPostAsMarkdown(postJson, baseUrl);
                    return string.IsNullOrWhiteSpace(markdown) ? null : markdown;
                }

                if (DiscourseForumService.TryParseTopicUrl(uri, out int topicId, out int? _))
                {
                    var topicJson = await DiscourseForumService.FetchTopicAsync(httpClient, baseUrl, topicId, includeRaw: true).ConfigureAwait(false);
                    var markdown = DiscourseForumService.FormatTopicAsMarkdown(topicJson, baseUrl);
                    return string.IsNullOrWhiteSpace(markdown) ? null : markdown;
                }

                return null;
            }
            catch (HttpRequestException)
            {
                return null;
            }
            catch (JsonReaderException)
            {
                return null;
            }
            catch (JsonSerializationException)
            {
                return null;
            }
        }

        private static async Task<GitContentResult?> TryFetchGitHostContentAsync(Uri uri, HttpClient httpClient)
        {
            if (!LooksLikeGitHost(uri))
            {
                return null;
            }

            Uri? rawUri = null;
            if (uri.Host.Equals("raw.githubusercontent.com", StringComparison.OrdinalIgnoreCase))
            {
                rawUri = uri;
            }
            else if (uri.Host.Contains("github.com", StringComparison.OrdinalIgnoreCase))
            {
                rawUri = BuildGitHubRawUri(uri);
            }
            else if (uri.Host.Contains("gitlab.com", StringComparison.OrdinalIgnoreCase))
            {
                rawUri = BuildGitLabRawUri(uri);
            }

            if (rawUri == null)
            {
                return null;
            }

            try
            {
                var response = await httpClient.GetAsync(rawUri).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var format = DetectFormatFromExtension(rawUri.AbsolutePath);
                return new GitContentResult(content, format);
            }
            catch (HttpRequestException)
            {
                return null;
            }
        }

        private async Task<string?> TryFetchStackExchangeContentAsync(Uri uri, HttpClient httpClient)
        {
            if (!TryExtractStackExchangeQuestion(uri, out string? site, out int questionId))
            {
                return null;
            }

            var apiUri = new Uri($"https://api.stackexchange.com/2.3/questions/{questionId}?order=desc&sort=activity&site={site}&filter=withbody");
            try
            {
                var response = await httpClient.GetAsync(apiUri).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                var payload = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var json = JObject.Parse(payload);
                var items = json["items"] as JArray;
                if (items == null || items.Count == 0)
                {
                    return null;
                }

                var question = items[0];
                var title = question["title"]?.ToString();
                var markdown = question["body_markdown"]?.ToString();
                var htmlBody = question["body"]?.ToString();

                // If body_markdown is empty or contains HTML tags, use body (HTML) and convert it
                if (string.IsNullOrWhiteSpace(markdown) || markdown.Contains("<p>") || markdown.Contains("<div>"))
                {
                    if (!string.IsNullOrWhiteSpace(htmlBody))
                    {
                        // Convert HTML to markdown using HtmlConverter
                        var htmlResult = await this.htmlConverter.ConvertHtmlStringAsync(htmlBody, new FileConversionOptions()).ConfigureAwait(false);
                        markdown = htmlResult.MarkdownContent;
                    }
                }

                if (string.IsNullOrWhiteSpace(markdown))
                {
                    return null;
                }

                var builder = new StringBuilder();
                if (!string.IsNullOrWhiteSpace(title))
                {
                    builder.Append("# ").AppendLine(title.Trim()).AppendLine();
                }

                builder.Append(markdown.Trim());
                return builder.ToString();
            }
            catch (HttpRequestException)
            {
                return null;
            }
            catch (JsonReaderException)
            {
                return null;
            }
        }

        /// <summary>
        /// Removes Wikipedia chrome from the parsed HTML before Markdown conversion:
        /// section edit links, navigation templates, metadata/authority boxes, and hatnotes.
        /// Also converts relative wiki links and image sources to absolute URLs.
        /// </summary>
        private static string CleanupWikimediaHtml(string html, Uri pageUri)
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var selectors = new[]
            {
                "//span[contains(@class,'mw-editsection')]",
                "//table[contains(@class,'navbox')]",
                "//table[contains(@class,'metadata')]",
                "//div[contains(@class,'hatnote')]",
                "//span[contains(@class,'mw-empty-elt')]",
                "//sup[contains(@class,'reference')]",
            };

            foreach (var xpath in selectors)
            {
                var nodes = doc.DocumentNode.SelectNodes(xpath);
                if (nodes != null)
                {
                    foreach (var node in nodes.ToList())
                    {
                        node.Remove();
                    }
                }
            }

            // Convert relative wiki links and image sources to absolute URLs.
            var baseUri = new Uri($"{pageUri.Scheme}://{pageUri.Host}");
            foreach (var node in doc.DocumentNode.SelectNodes("//a[@href] | //img[@src]")?.ToList() ?? Enumerable.Empty<HtmlNode>())
            {
                var attribute = node.Name == "img" ? "src" : "href";
                var value = node.GetAttributeValue(attribute, null);
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                // Avoid touching already-absolute URLs, mailto/tel, and javascript links.
                if (value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    value.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                    value.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) ||
                    value.StartsWith("tel:", StringComparison.OrdinalIgnoreCase) ||
                    value.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                try
                {
                    var absoluteUri = new Uri(baseUri, value);
                    node.SetAttributeValue(attribute, absoluteUri.ToString());
                }
                catch (UriFormatException)
                {
                    // Leave malformed URIs untouched.
                }
            }

            // Remove empty paragraphs that are left behind after removing the elements above.
            var emptyParagraphs = doc.DocumentNode.SelectNodes("//p[not(node()) or normalize-space(.)='']");
            if (emptyParagraphs != null)
            {
                foreach (var p in emptyParagraphs.ToList())
                {
                    p.Remove();
                }
            }

            return doc.DocumentNode.InnerHtml;
        }

        #endregion

        #region Helper Methods

        private static bool LooksLikeGitHost(Uri uri)
        {
            return uri.Host.Contains("github.com", StringComparison.OrdinalIgnoreCase)
                   || uri.Host.Equals("raw.githubusercontent.com", StringComparison.OrdinalIgnoreCase)
                   || uri.Host.Contains("gitlab.com", StringComparison.OrdinalIgnoreCase);
        }

        private static Uri? BuildGitHubRawUri(Uri uri)
        {
            var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length >= 5 && segments[2].Equals("blob", StringComparison.OrdinalIgnoreCase))
            {
                var owner = segments[0];
                var repo = segments[1];
                var branch = segments[3];
                var filePath = string.Join('/', segments.Skip(4));
                var rawUri = new Uri($"https://raw.githubusercontent.com/{owner}/{repo}/{branch}/{filePath}");
                return rawUri;
            }

            return null;
        }

        private static Uri? BuildGitLabRawUri(Uri uri)
        {
            var builder = new UriBuilder(uri);
            var path = builder.Path;
            if (path.Contains("/-/blob/", StringComparison.OrdinalIgnoreCase))
            {
                path = path.Replace("/-/blob/", "/-/raw/", StringComparison.OrdinalIgnoreCase);
            }
            else if (path.Contains("/blob/", StringComparison.OrdinalIgnoreCase))
            {
                path = path.Replace("/blob/", "/raw/", StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                return null;
            }

            builder.Path = path;
            builder.Query = "inline=false";
            return builder.Uri;
        }

        private static string DetectFormatFromExtension(string path)
        {
            var extension = Path.GetExtension(path)?.ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(extension))
            {
                return "plain_text";
            }

            return extension switch
            {
                ".md" => "markdown",
                ".markdown" => "markdown",
                ".mdown" => "markdown",
                ".rst" => "markdown",
                _ => "plain_text",
            };
        }

        private static bool TryExtractStackExchangeQuestion(Uri uri, out string? site, out int questionId)
        {
            site = null;
            questionId = 0;

            var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length < 2)
            {
                return false;
            }

            int idIndex = -1;
            if (segments[0].Equals("questions", StringComparison.OrdinalIgnoreCase))
            {
                idIndex = 1;
            }
            else if (segments[0].Equals("q", StringComparison.OrdinalIgnoreCase))
            {
                idIndex = 1;
            }

            if (idIndex == -1 || !int.TryParse(segments[idIndex], NumberStyles.Integer, CultureInfo.InvariantCulture, out questionId))
            {
                return false;
            }

            site = GetStackExchangeSiteToken(uri.Host);
            return !string.IsNullOrWhiteSpace(site);
        }

        private static string? GetStackExchangeSiteToken(string host)
        {
            host = host.ToLowerInvariant();
            if (host.EndsWith(".stackexchange.com", StringComparison.Ordinal))
            {
                return host[..host.IndexOf(".stackexchange.com", StringComparison.Ordinal)];
            }

            return host switch
            {
                "stackoverflow.com" => "stackoverflow",
                "serverfault.com" => "serverfault",
                "superuser.com" => "superuser",
                "askubuntu.com" => "askubuntu",
                "mathoverflow.net" => "mathoverflow",
                "stackapps.com" => "stackapps",
                _ => null,
            };
        }

        private static bool TryExtractWikimediaTitle(Uri uri, out string? title)
        {
            title = null;
            if (!IsWikimediaHost(uri))
            {
                return false;
            }

            var path = uri.AbsolutePath;
            if (string.IsNullOrWhiteSpace(path) || path == "/")
            {
                return false;
            }

            var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0)
            {
                return false;
            }

            if (segments[0].Equals("wiki", StringComparison.OrdinalIgnoreCase) && segments.Length > 1)
            {
                title = string.Join('/', segments.Skip(1));
            }
            else
            {
                title = segments[^1];
            }

            title = Uri.UnescapeDataString(title);
            return !string.IsNullOrWhiteSpace(title);
        }

        private static bool IsWikimediaHost(Uri uri)
        {
            string host = uri.Host;
            foreach (var domain in WikimediaRootDomains)
            {
                if (host.EndsWith(domain, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private sealed record GitContentResult(string Content, string Format);

        private static readonly string[] WikimediaRootDomains =
        {
            "wikipedia.org",
            "wikimedia.org",
            "wiktionary.org",
            "wikibooks.org",
            "wikinews.org",
            "wikiquote.org",
            "wikisource.org",
            "wikiversity.org",
            "wikivoyage.org",
        };

        #endregion
    }
}
