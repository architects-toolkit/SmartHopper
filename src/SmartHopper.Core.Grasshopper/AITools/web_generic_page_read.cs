/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
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
using SmartHopper.Core.Grasshopper.Utils;
using SmartHopper.Infrastructure.AICall;
using SmartHopper.Infrastructure.AITools;

namespace SmartHopper.Core.Grasshopper.AITools
{
    /// <summary>
    /// Provides AI tools for fetching webpage text content,
    /// omitting HTML, scripts, styles, images, and respecting robots.txt rules.
    /// </summary>
    public class web_generic_page_read : IAIToolProvider
    {
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
                description: "Retrieve plain text content of a webpage at the given URL, excluding HTML, scripts, styles, and images. Respects robots.txt.",
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
                // Extract parameters
                AIInteractionToolCall toolInfo = toolCall.Body.PendingToolCallsList().First();
                string url = toolInfo.Arguments["url"]?.ToString();
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

                // Check robots.txt
                Uri robotsUri = new(uri.GetLeftPart(UriPartial.Authority) + "/robots.txt");
                try
                {
                    var robotsResponse = await httpClient.GetAsync(robotsUri).ConfigureAwait(false);
                    if (robotsResponse.IsSuccessStatusCode)
                    {
                        string robotsContent = await robotsResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                        var robots = new WebTools(robotsContent);
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
                            output.CreateError($"JSON parse failed: {ex.Message}");
                            return output;
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

                var toolResult = new JObject
                {
                    ["content"] = text,
                    ["url"] = url,
                    ["length"] = text.Length
                };

                var toolBody = new AIBody();
                toolBody.AddInteractionToolResult(toolResult);

                output.CreateSuccess(toolBody);
                return output;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebTools] Error in GenericPageReadAsync: {ex.Message}");
                output.CreateError($"Error: {ex.Message}");
                return output;
            }
        }
    }
}
