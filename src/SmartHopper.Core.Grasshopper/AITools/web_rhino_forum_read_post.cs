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
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SmartHopper.Infrastructure.AITools;

namespace SmartHopper.Core.Grasshopper.AITools
{
    /// <summary>
    /// Provides AI tools for fetching webpage text content,
    /// omitting HTML, scripts, styles, images, and respecting robots.txt rules.
    /// </summary>
    public class web_rhino_forum_read_post : IAIToolProvider
    {
        /// <summary>
        /// Returns the list of tools provided by this class.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: "web_rhino_forum_read_post",
                description: "Retrieve a full Rhino Discourse forum post by ID.",
                category: "Knowledge",
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
                execute: this.WebRhinoForumReadPostAsync);
        }

        /// <summary>
        /// Retrieves a full Rhino Discourse forum post by ID.
        /// </summary>
        /// <param name="parameters">A JObject containing the ID parameter.</param>
        private async Task<AIToolCall> WebRhinoForumReadPostAsync(AIToolCall toolCall)
        {
            int id = toolCall.Arguments["id"]?.Value<int>();
            var httpClient = new HttpClient();
            var postUri = new Uri($"https://discourse.mcneel.com/posts/{id}.json");
            var response = await httpClient.GetAsync(postUri).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var json = JObject.Parse(content);
            toolCall.Result = json;
            return toolCall;
        }
    }
}
