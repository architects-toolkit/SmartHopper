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
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SmartHopper.Core.Grasshopper.Utils.Internal;

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

        public UrlConverter()
        {
            this.htmlConverter = new HtmlConverter();
        }

        public IEnumerable<string> SupportedExtensions => new[] { ".url" }; // Pseudo-extension for URL handling

        public async Task<FileConversionResult> ConvertAsync(string filePath, FileConversionOptions options)
        {
            // For UrlConverter, filePath is actually a URL
            var url = filePath;

            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
            {
                return FileConversionResult.Failure("url", $"Invalid URL: {url}");
            }

            try
            {
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("SmartHopper/1.5 (+https://github.com/architects-toolkit/SmartHopper)");

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
                            return FileConversionResult.Failure("url", $"Access to '{uri}' is disallowed by robots.txt.");
                        }
                    }
                }
                catch (HttpRequestException)
                {
                    // Treat failure to fetch robots.txt as allowed
                }

                string? textContent = null;
                string contentFormat = "markdown";
                var result = new FileConversionResult { DetectedFormat = "url" };

                // Try specialized fetchers first
                if (IsWikimediaHost(uri))
                {
                    textContent = await TryFetchWikimediaPlainTextAsync(uri, httpClient).ConfigureAwait(false);
                    if (!string.IsNullOrWhiteSpace(textContent))
                    {
                        contentFormat = "plain_text";
                    }
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
                    var html = await httpClient.GetStringAsync(uri).ConfigureAwait(false);
                    Debug.WriteLine($"[UrlConverter] Fetched HTML from {url}. Length: {html.Length}");

                    // Pass the fetched URL as base URL so relative links/images become absolute.
                    var htmlOptions = options?.Clone() ?? new FileConversionOptions();
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

                if (string.IsNullOrWhiteSpace(textContent))
                {
                    return FileConversionResult.Failure("url", "The requested page did not return any readable content.");
                }

                result.MarkdownContent = textContent;
                result.Metadata["source"] = url;
                result.Metadata["format"] = contentFormat;
                result.IsSuccess = true;

                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[UrlConverter] Error: {ex.Message}");
                return FileConversionResult.Failure("url", $"Error fetching URL: {ex.Message}");
            }
        }

        #region Specialized Fetchers

        private static async Task<string?> TryFetchWikimediaPlainTextAsync(Uri pageUri, HttpClient httpClient)
        {
            if (!TryExtractWikimediaTitle(pageUri, out string? title))
            {
                return null;
            }

            var apiUri = new Uri($"{pageUri.Scheme}://{pageUri.Host}/w/api.php?action=query&prop=extracts&explaintext=1&redirects=1&format=json&titles={Uri.EscapeDataString(title)}");
            try
            {
                var response = await httpClient.GetAsync(apiUri).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                var payload = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var json = JObject.Parse(payload);
                var pagesToken = json.SelectToken("query.pages");
                if (pagesToken == null)
                {
                    return null;
                }

                foreach (var page in pagesToken.Children<JProperty>())
                {
                    var extract = page.Value?["extract"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(extract))
                    {
                        var titleValue = page.Value?["title"]?.ToString();
                        var builder = new StringBuilder();
                        if (!string.IsNullOrWhiteSpace(titleValue))
                        {
                            builder.Append('#').Append(' ').Append(titleValue.Trim()).AppendLine().AppendLine();
                        }

                        builder.Append(extract.Trim());
                        return builder.ToString();
                    }
                }
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

            return null;
        }

        private static async Task<string?> TryFetchDiscourseRawContentAsync(Uri uri, HttpClient httpClient)
        {
            if (!LooksLikeDiscourse(uri))
            {
                return null;
            }

            Uri? jsonUri = null;
            if (TryGetDiscoursePostId(uri, out int postId))
            {
                jsonUri = new Uri($"{uri.Scheme}://{uri.Host}/posts/{postId}.json");
            }
            else
            {
                jsonUri = BuildDiscourseJsonUri(uri);
            }

            if (jsonUri == null)
            {
                return null;
            }

            try
            {
                var jsonResponse = await httpClient.GetAsync(jsonUri).ConfigureAwait(false);
                if (!jsonResponse.IsSuccessStatusCode)
                {
                    return null;
                }

                var contentText = await jsonResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                var jsonObj = JObject.Parse(contentText);

                if (jsonObj["raw"] != null)
                {
                    return jsonObj["raw"]?.ToString();
                }

                var posts = jsonObj.SelectToken("post_stream.posts") as JArray;
                if (posts != null && posts.Count > 0)
                {
                    var builder = new StringBuilder();
                    foreach (var post in posts)
                    {
                        var username = post?["username"]?.ToString();
                        var created = post?["created_at"]?.ToString();
                        var raw = post?["raw"]?.ToString();
                        if (string.IsNullOrWhiteSpace(raw))
                        {
                            continue;
                        }

                        if (!string.IsNullOrWhiteSpace(username) || !string.IsNullOrWhiteSpace(created))
                        {
                            builder.Append("## ");
                            if (!string.IsNullOrWhiteSpace(username))
                            {
                                builder.Append(username);
                            }

                            if (!string.IsNullOrWhiteSpace(created))
                            {
                                builder.Append(" – ").Append(created);
                            }

                            builder.AppendLine();
                        }

                        builder.AppendLine();
                        builder.AppendLine(raw.Trim());
                        builder.AppendLine();
                    }

                    var combined = builder.ToString().Trim();
                    return string.IsNullOrWhiteSpace(combined) ? null : combined;
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

        #endregion

        #region Helper Methods

        private static bool LooksLikeDiscourse(Uri uri)
        {
            return uri.Host.Contains("discourse", StringComparison.OrdinalIgnoreCase)
                   || uri.Host.Contains("mcneel", StringComparison.OrdinalIgnoreCase)
                   || uri.AbsolutePath.StartsWith("/t/", StringComparison.OrdinalIgnoreCase)
                   || uri.AbsolutePath.StartsWith("/p/", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryGetDiscoursePostId(Uri uri, out int postId)
        {
            postId = 0;
            var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length >= 2)
            {
                if (segments[0].Equals("p", StringComparison.OrdinalIgnoreCase) || segments[0].Equals("posts", StringComparison.OrdinalIgnoreCase))
                {
                    return int.TryParse(segments[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out postId);
                }
            }

            if (segments.Length >= 3 && segments[0].Equals("t", StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(segments[^1], NumberStyles.Integer, CultureInfo.InvariantCulture, out postId))
                {
                    return true;
                }
            }

            return false;
        }

        private static Uri? BuildDiscourseJsonUri(Uri uri)
        {
            if (uri.AbsolutePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                return uri;
            }

            var builder = new UriBuilder(uri)
            {
                Path = uri.AbsolutePath.TrimEnd('/') + ".json",
            };
            return builder.Uri;
        }

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
