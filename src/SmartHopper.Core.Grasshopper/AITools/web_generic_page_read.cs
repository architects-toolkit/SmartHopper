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
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SmartHopper.Core.Grasshopper.Utils.Internal;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AICall.Core.Returns;
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.Infrastructure.AITools;

namespace SmartHopper.Core.Grasshopper.AITools
{
    /// <summary>
    /// Provides AI tools for fetching webpage text content,
    /// omitting HTML, scripts, styles, images, and respecting robots.txt rules.
    /// </summary>
    public partial class web_generic_page_read : IAIToolProvider
    {
        #region Compiled Regex Patterns

        /// <summary>
        /// Regex pattern for normalizing whitespace to single spaces.
        /// </summary>
        [GeneratedRegex(@"\s+")]
        private static partial Regex WhitespaceRegex();

        #endregion

        /// <summary>
        /// Name of the AI tool provided by this class.
        /// </summary>
        private readonly string toolName = "web_generic_page_read";
        /// <summary>
        /// Returns the list of tools provided by this class.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: this.toolName,
                description: "Retrieve plain text or markdown content of a webpage at the given URL (supports Wikipedia/Wikimedia, Discourse forums, GitHub/GitLab files, Stack Exchange questions, and generic webpages), excluding HTML, scripts, styles, and images. Respects robots.txt. Use this when you already have a URL and need page content before reasoning or summarizing.",
                category: "Knowledge",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""url"": {
                            ""type"": ""string"",
                            ""format"": ""uri"",
                            ""description"": ""The URL of the webpage to fetch.""
                        }
                    },
                    ""required"": [""url""]
                }",
                execute: this.GenericPageReadAsync);
        }

        /// <summary>
        /// Fetches the text content of a webpage given its URL, if allowed by robots.txt.
        /// </summary>
        /// <param name="parameters">A JObject containing the URL parameter.</param>
        private async Task<AIReturn> GenericPageReadAsync(AIToolCall toolCall)
        {
            // Prepare the output
            var output = new AIReturn()
            {
                Request = toolCall,
            };

            try
            {
                // Local tool: skip metrics validation (provider/model/finish_reason not required)
                toolCall.SkipMetricsValidation = true;

                // Extract parameters
                AIInteractionToolCall toolInfo = toolCall.GetToolCall();
                var args = toolInfo.Arguments ?? new JObject();
                string url = args["url"]?.ToString();
                if (string.IsNullOrEmpty(url))
                {
                    output.CreateError("Missing 'url' parameter.");
                    return output;
                }

                if (!Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
                {
                    output.CreateError($"Invalid URL: {url}");
                    return output;
                }

                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("SmartHopper/1.0 (+https://github.com/architects-toolkit/SmartHopper)");

                // Check robots.txt
                Uri robotsUri = new(uri.GetLeftPart(UriPartial.Authority) + "/robots.txt");
                try
                {
                    var robotsResponse = await httpClient.GetAsync(robotsUri).ConfigureAwait(false);
                    if (robotsResponse.IsSuccessStatusCode)
                    {
                        string robotsContent = await robotsResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                        var robots = new WebUtilities(robotsContent);
                        if (!robots.IsPathAllowed(uri.PathAndQuery))
                        {
                            output.CreateError($"Access to '{uri}' is disallowed by robots.txt.");
                            return output;
                        }
                    }
                }
                catch (HttpRequestException)
                {
                    // Treat failure to fetch robots.txt as allowed
                }

                string? textContent = null;
                string contentFormat = "markdown";

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

                if (string.IsNullOrWhiteSpace(textContent))
                {
                    var html = await httpClient.GetStringAsync(uri).ConfigureAwait(false);
                    Debug.WriteLine($"[WebTools] Fetched HTML from {url}. Length: {html.Length}");
                    textContent = ConvertHtmlToMarkdown(html);
                }

                if (string.IsNullOrWhiteSpace(textContent))
                {
                    output.CreateError("The requested page did not return any readable content.");
                    return output;
                }

                var toolResult = new JObject
                {
                    ["content"] = textContent,
                    ["format"] = contentFormat,
                    ["source"] = url,
                };

                var builder = AIBodyBuilder.Create();
                builder.AddToolResult(toolResult, toolInfo.Id, toolInfo.Name);
                var immutable = builder.Build();
                output.CreateSuccess(immutable, toolCall);
                return output;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebTools] Error in GenericPageReadAsync: {ex.Message}");
                output.CreateError($"Error: {ex.Message}");
                return output;
            }
        }

        /// <summary>
        /// Attempts to retrieve clean plain text for Wikimedia-powered pages using their API.
        /// </summary>
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

        /// <summary>
        /// Attempts to fetch raw markdown posts from Discourse forums.
        /// </summary>
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

        private static async Task<string?> TryFetchStackExchangeContentAsync(Uri uri, HttpClient httpClient)
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
                var markdown = question["body_markdown"]?.ToString() ?? question["body"]?.ToString();
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

        private static string ConvertHtmlToMarkdown(string html)
        {
            if (string.IsNullOrWhiteSpace(html))
            {
                return string.Empty;
            }

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            string[] xpaths =
            {
                "//script", "//style", "//img", "//noscript", "//header",
                "//footer", "//nav", "//aside", "//form", "//svg", "//canvas",
            };

            foreach (string xp in xpaths)
            {
                var nodes = doc.DocumentNode.SelectNodes(xp);
                if (nodes == null)
                {
                    continue;
                }

                foreach (var node in nodes)
                {
                    node.Remove();
                }
            }

            var links = doc.DocumentNode.SelectNodes("//a[@href]");
            if (links != null)
            {
                foreach (var link in links.ToList())
                {
                    var href = link.GetAttributeValue("href", string.Empty);
                    var linkText = link.InnerText.Trim();
                    if (string.IsNullOrWhiteSpace(linkText))
                    {
                        link.Remove();
                        continue;
                    }

                    var md = $"[{linkText}]({href})";
                    var replacement = doc.CreateTextNode(md);
                    link.ParentNode?.ReplaceChild(replacement, link);
                }
            }

            var headingNodes = doc.DocumentNode.SelectNodes("//h1|//h2|//h3|//h4|//h5|//h6");
            if (headingNodes != null)
            {
                foreach (var heading in headingNodes.ToList())
                {
                    int level = int.Parse(heading.Name.Substring(1), NumberStyles.Integer, CultureInfo.InvariantCulture);
                    string headingText = heading.InnerText.Trim();
                    string mdHeading = new string('#', level) + " " + headingText + Environment.NewLine + Environment.NewLine;
                    var mdNode = doc.CreateTextNode(mdHeading);
                    heading.ParentNode?.ReplaceChild(mdNode, heading);
                }
            }

            var paragraphNodes = doc.DocumentNode.SelectNodes("//p");
            if (paragraphNodes != null)
            {
                foreach (var paragraph in paragraphNodes.ToList())
                {
                    var paragraphText = paragraph.InnerText.Trim();
                    if (string.IsNullOrWhiteSpace(paragraphText))
                    {
                        paragraph.Remove();
                        continue;
                    }

                    var mdParagraph = doc.CreateTextNode(paragraphText + Environment.NewLine + Environment.NewLine);
                    paragraph.ParentNode?.ReplaceChild(mdParagraph, paragraph);
                }
            }

            string text = doc.DocumentNode.InnerText;
            text = WhitespaceRegex().Replace(text, " ").Trim();
            return text;
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
    }
}
