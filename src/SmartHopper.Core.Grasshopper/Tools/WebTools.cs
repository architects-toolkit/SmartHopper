/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

/*
 * WebTools.cs
 * Defines AI tool for fetching webpage text content, stripping HTML and respecting robots.txt.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Newtonsoft.Json.Linq;
using SmartHopper.Config.Interfaces;
using SmartHopper.Config.Models;

namespace SmartHopper.Core.Grasshopper.Tools
{
    /// <summary>
    /// Provides AI tools for fetching webpage text content,
    /// omitting HTML, scripts, styles, images, and respecting robots.txt rules.
    /// </summary>
    public class WebTools : IAIToolProvider
    {
        /// <summary>
        /// Returns the list of tools provided by this class.
        /// </summary>
        /// <returns></returns>
        #region ToolRegistration
        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: "web_fetch_page_text",
                description: "Retrieve plain text content of a webpage at the given URL, excluding HTML, scripts, styles, and images. Respects robots.txt.",
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
                execute: this.WebFetchPageTextAsync);
            yield return new AITool(
                name: "web_search_rhino_forum",
                description: "Search Rhino Discourse forum posts by query and return up to 10 matching posts.",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""query"": {
                            ""type"": ""string"",
                            ""description"": ""Search query for Rhino Discourse forum.""
                        }
                    },
                    ""required"": [""query""]
                }",
                execute: this.WebSearchRhinoForumAsync);
            yield return new AITool(
                name: "web_get_rhino_forum_post",
                description: "Retrieve full JSON of a Rhino Discourse forum post by ID.",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""id"": {
                            ""type"": ""integer"",
                            ""description"": ""ID of the forum post to fetch.""
                        }
                    },
                    ""required"": [""id""]
                }",
                execute: this.WebGetRhinoForumPostAsync);
        }
        #endregion

        #region WebFetchPageText

        /// <summary>
        /// Fetches the text content of a webpage given its URL, if allowed by robots.txt.
        /// </summary>
        /// <param name="parameters">A JObject containing the URL parameter.</param>
        private async Task<object> WebFetchPageTextAsync(JObject parameters)
        {
            string url = parameters.Value<string>("url") ?? throw new ArgumentException("Missing 'url' parameter.");
            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
            {
                throw new ArgumentException($"Invalid URL: {url}");
            }

            using var httpClient = new HttpClient();

            // Check robots.txt
            Uri robotsUri = new(uri.GetLeftPart(UriPartial.Authority) + "/robots.txt");
            try
            {
                var robotsResponse = await httpClient.GetAsync(robotsUri).ConfigureAwait(false);
                if (robotsResponse.IsSuccessStatusCode)
                {
                    string robotsContent = await robotsResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var robots = new RobotsTxtParser(robotsContent);
                    if (!robots.IsAllowed(uri.PathAndQuery))
                    {
                        throw new InvalidOperationException($"Access to '{uri}' is disallowed by robots.txt.");
                    }
                }
            }
            catch (HttpRequestException)
            {
                // Treat failure to fetch robots.txt as allowed
            }

            // Fetch page HTML
            string html = string.Empty;
            bool usedJson = false;

            // Try JSON endpoint if available
            try
            {
                var jsonUriBuilder = new UriBuilder(uri);
                if (!jsonUriBuilder.Path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    jsonUriBuilder.Path = jsonUriBuilder.Path.TrimEnd('/') + ".json";
                }

                var jsonResponse = await httpClient.GetAsync(jsonUriBuilder.Uri).ConfigureAwait(false);
                if (jsonResponse.IsSuccessStatusCode)
                {
                    var contentText = await jsonResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                    Debug.WriteLine($"[WebTools] Fetched JSON from {jsonUriBuilder.Uri}. Length: {contentText.Length}");
                    try
                    {
                        var jsonObj = JObject.Parse(contentText);
                        var posts = jsonObj.SelectToken("post_stream.posts") as JArray;
                        if (posts != null)
                        {
                            var cookedList = posts.Select(p => p["cooked"]?.ToString() ?? string.Empty);
                            html = string.Concat(cookedList);
                            usedJson = true;
                        }
                        else if (jsonObj["cooked"] != null)
                        {
                            html = jsonObj["cooked"].ToString();
                            usedJson = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[WebTools] JSON parse failed: {ex.Message}");
                    }
                }
            }
            catch (HttpRequestException)
            {
                // JSON endpoint unavailable; fallback to HTML
            }

            if (!usedJson)
            {
                html = await httpClient.GetStringAsync(uri).ConfigureAwait(false);
                Debug.WriteLine($"[WebTools] Fetched HTML from {url}. Length: {html.Length}");
            }

            Debug.WriteLine($"[WebTools] Final HTML length used: {html.Length}");

            // Parse and strip unwanted nodes
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            string[] xpaths = new[]
            {
                "//script", "//style", "//img", "//noscript", "//header",
                "//footer", "//nav", "//aside", "//form", "//svg", "//canvas",
            };
            foreach (string xp in xpaths)
            {
                var nodes = doc.DocumentNode.SelectNodes(xp);
                if (nodes != null)
                {
                    foreach (var node in nodes)
                    {
                        node.Remove();
                    }
                }
            }

            // Preserve links: convert <a> tags to markdown [text](url)
            var links = doc.DocumentNode.SelectNodes("//a[@href]");
            if (links != null)
            {
                foreach (var link in links.ToList())
                {
                    var href = link.GetAttributeValue("href", string.Empty);
                    var linkText = link.InnerText;
                    var md = $"[{linkText}]({href})";
                    var replacement = doc.CreateTextNode(md);
                    link.ParentNode.ReplaceChild(replacement, link);
                }
            }

            // Convert headings to markdown format: <h1> to <h6>
            var headingNodes = doc.DocumentNode.SelectNodes("//h1|//h2|//h3|//h4|//h5|//h6");
            if (headingNodes != null)
            {
                foreach (var heading in headingNodes.ToList())
                {
                    int level = int.Parse(heading.Name.Substring(1));
                    string headingText = heading.InnerText.Trim();
                    string mdHeading = new string('#', level) + " " + headingText + Environment.NewLine;
                    var mdNode = doc.CreateTextNode(mdHeading);
                    heading.ParentNode.ReplaceChild(mdNode, heading);
                }
            }

            Debug.WriteLine($"[WebTools] Raw text length before normalization: {doc.DocumentNode.InnerText.Length}");
            Debug.WriteLine($"[WebTools] Raw snippet: {doc.DocumentNode.InnerText.Substring(0, Math.Min(doc.DocumentNode.InnerText.Length, 200))}");

            // Extract and normalize text
            string text = doc.DocumentNode.InnerText;
            text = Regex.Replace(text, @"\s+", " ").Trim();
            Debug.WriteLine($"[WebTools] Normalized text length: {text.Length}");
            Debug.WriteLine($"[WebTools] Normalized snippet: {text.Substring(0, Math.Min(text.Length, 200))}");

            return text;
        }
        #endregion

        #region WebSearchRhinoForum
        // TODO: take only 5 and return a summary of the posts
        private async Task<object> WebSearchRhinoForumAsync(JObject parameters)
        {
            string query = parameters.Value<string>("query") ?? throw new ArgumentException("Missing 'query' parameter.");
            var httpClient = new HttpClient();
            var searchUri = new Uri($"https://discourse.mcneel.com/search.json?q={Uri.EscapeDataString(query)}");
            var response = await httpClient.GetAsync(searchUri).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var json = JObject.Parse(content);
            var posts = json["posts"] as JArray ?? new JArray();
            posts = new JArray(posts.Take(10));
            var topics = json["topics"] as JArray ?? new JArray();

            // Build a map of topic ID to title
            var topicTitles = topics
                .Where(t => t["id"] != null)
                .ToDictionary(t => (int)t["id"], t => (string)(t["title"] ?? t["fancy_title"] ?? string.Empty));
            var result = new JArray(posts.Select(p =>
            {
                int postId = p.Value<int>("id");
                int topicId = p.Value<int>("topic_id");
                return new JObject
                {
                    ["id"] = postId,
                    ["username"] = p.Value<string>("username"),
                    ["topic_id"] = topicId,
                    ["title"] = topicTitles.GetValueOrDefault(topicId, string.Empty),
                    ["date"] = p.Value<string>("created_at"),
                    ["cooked"] = p.Value<string>("cooked"),
                };
            }));
            return result;
        }
        #endregion

        #region WebGetRhinoForumPost

        /// <summary>
        /// Retrieves full JSON of a Rhino Discourse forum post by ID.
        /// </summary>
        /// <param name="parameters">A JObject containing the ID parameter.</param>
        private async Task<object> WebGetRhinoForumPostAsync(JObject parameters)
        {
            int id = parameters.Value<int>("id");
            var httpClient = new HttpClient();
            var postUri = new Uri($"https://discourse.mcneel.com/posts/{id}.json");
            var response = await httpClient.GetAsync(postUri).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var json = JObject.Parse(content);
            return json;
        }
        #endregion

        #region Helpers
        private class RobotsTxtParser
        {
            private readonly List<string> disallowed = new();

            /// <summary>
            /// Initializes a new instance of the <see cref="RobotsTxtParser"/> class.
            /// Simple robots.txt parser supporting User-agent: * and Disallow directives.
            /// </summary>
            public RobotsTxtParser(string content)
            {
                bool appliesToAll = false;
                foreach (var line in content.Split('\n'))
                {
                    string trimmed = line.Trim();
                    if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#"))
                    {
                        continue;
                    }

                    var parts = trimmed.Split(':', 2);
                    if (parts.Length != 2)
                    {
                        continue;
                    }

                    string field = parts[0].Trim().ToLowerInvariant();
                    string value = parts[1].Trim();
                    if (field == "user-agent")
                    {
                        appliesToAll = value == "*";
                    }
                    else if (field == "disallow" && appliesToAll)
                    {
                        this.disallowed.Add(value);
                    }
                    else if (field == "allow" && appliesToAll)
                    {
                        // Could implement allow rules, but ignored for simplicity
                    }
                }
            }

            /// <summary>
            /// Returns true if the given path is allowed to be fetched.
            /// </summary>
            public bool IsAllowed(string path)
            {
                foreach (var rule in this.disallowed)
                {
                    if (string.IsNullOrEmpty(rule))
                    {
                        continue;
                    }

                    if (path.StartsWith(rule))
                    {
                        return false;
                    }
                }

                return true;
            }
        }
        #endregion
    }
}
