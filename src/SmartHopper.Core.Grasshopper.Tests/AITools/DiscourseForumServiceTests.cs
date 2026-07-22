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

namespace SmartHopper.Core.Grasshopper.Tests.AITools
{
    using System;
    using Newtonsoft.Json.Linq;
    using SmartHopper.Core.Grasshopper.AITools;
    using Xunit;

    /// <summary>
    /// Unit tests for <see cref="DiscourseForumService"/>.
    /// </summary>
    public class DiscourseForumServiceTests
    {
        [Theory]
        [InlineData("https://discourse.mcneel.com/t/slug/204893", true)]
        [InlineData("https://discourse.mcneel.com/t/slug/204893/1", true)]
        [InlineData("https://discourse.mcneel.com/p/1073507", true)]
        [InlineData("https://discourse.mcneel.com/posts/1073507", true)]
        [InlineData("https://example.com/t/slug/204893", true)]
        [InlineData("https://example.com/p/1073507", true)]
        [InlineData("https://example.com/posts/1073507", true)]
        [InlineData("https://discourse.example.com/t/slug/204893", true)]
        [InlineData("https://mcneel.com/t/slug/204893", true)]
        [InlineData("https://mcneel.com/some/page", false)]
        [InlineData("https://discourse.mcneel.com/", true)]
        [InlineData("https://example.com/some/page", false)]
        [InlineData("https://example.com/", false)]
        public void IsDiscourseUrl_ReturnsExpectedResult(string url, bool expected)
        {
            var uri = new Uri(url);
            Assert.Equal(expected, DiscourseForumService.IsDiscourseUrl(uri));
        }

        [Theory]
        [InlineData("https://discourse.mcneel.com/t/slug/204893", 204893, null)]
        [InlineData("https://discourse.mcneel.com/t/slug/204893/1", 204893, 1)]
        [InlineData("https://discourse.mcneel.com/t/g2-feature-requests-grasshopper2-icon-editor/204893", 204893, null)]
        [InlineData("https://discourse.mcneel.com/t/g2-feature-requests-grasshopper2-icon-editor/204893/5", 204893, 5)]
        [InlineData("https://discourse.mcneel.com/p/1073507", 0, null)]
        [InlineData("https://example.com/not-a-topic/204893", 0, null)]
        public void TryParseTopicUrl_ReturnsExpectedResult(string url, int expectedTopicId, int? expectedPostNumber)
        {
            var uri = new Uri(url);
            bool result = DiscourseForumService.TryParseTopicUrl(uri, out int topicId, out int? postNumber);

            if (expectedTopicId > 0)
            {
                Assert.True(result);
                Assert.Equal(expectedTopicId, topicId);
                Assert.Equal(expectedPostNumber, postNumber);
            }
            else
            {
                Assert.False(result);
            }
        }

        [Theory]
        [InlineData("https://discourse.mcneel.com/p/1073507", 1073507)]
        [InlineData("https://discourse.mcneel.com/posts/1073507", 1073507)]
        [InlineData("https://discourse.mcneel.com/t/slug/204893", 0)]
        [InlineData("https://example.com/not-a-post/1073507", 0)]
        public void TryParsePostUrl_ReturnsExpectedResult(string url, int expectedPostId)
        {
            var uri = new Uri(url);
            bool result = DiscourseForumService.TryParsePostUrl(uri, out int postId);

            if (expectedPostId > 0)
            {
                Assert.True(result);
                Assert.Equal(expectedPostId, postId);
            }
            else
            {
                Assert.False(result);
            }
        }

        [Theory]
        [InlineData("https://discourse.mcneel.com", 204893, true, "https://discourse.mcneel.com/t/204893.json?include_raw=1")]
        [InlineData("https://discourse.mcneel.com/", 204893, false, "https://discourse.mcneel.com/t/204893.json")]
        public void BuildTopicJsonUrl_ReturnsExpectedUrl(string baseUrl, int topicId, bool includeRaw, string expected)
        {
            Assert.Equal(expected, DiscourseForumService.BuildTopicJsonUrl(baseUrl, topicId, includeRaw));
        }

        [Fact]
        public void BuildPostJsonUrl_ReturnsExpectedUrl()
        {
            Assert.Equal(
                "https://discourse.mcneel.com/posts/1073507.json",
                DiscourseForumService.BuildPostJsonUrl("https://discourse.mcneel.com", 1073507));
        }

        [Fact]
        public void FormatTopicAsMarkdown_IncludesTitleSourceAndPosts()
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

            string markdown = DiscourseForumService.FormatTopicAsMarkdown(topicJson, "https://discourse.mcneel.com");

            Assert.Contains("# G2 Feature Requests: Grasshopper2 Icon Editor", markdown);
            Assert.Contains("source: https://discourse.mcneel.com/t/g2-feature-requests-grasshopper2-icon-editor/204893", markdown);
            Assert.Contains("## archinate1 – 2025-05-22T14:50:51.740Z", markdown);
            Assert.Contains("I have been working with the Icon editor...", markdown);
        }

        [Fact]
        public void FormatTopicAsMarkdown_HonorsMaxPosts()
        {
            var topicJson = new JObject
            {
                ["id"] = 1,
                ["title"] = "Topic",
                ["slug"] = "topic",
                ["post_stream"] = new JObject
                {
                    ["posts"] = new JArray
                    {
                        new JObject { ["username"] = "a", ["created_at"] = "2025-01-01", ["raw"] = "first" },
                        new JObject { ["username"] = "b", ["created_at"] = "2025-01-02", ["raw"] = "second" },
                        new JObject { ["username"] = "c", ["created_at"] = "2025-01-03", ["raw"] = "third" },
                    },
                },
            };

            string markdown = DiscourseForumService.FormatTopicAsMarkdown(topicJson, "https://discourse.mcneel.com", maxPosts: 2);

            Assert.Contains("## a", markdown);
            Assert.Contains("## b", markdown);
            Assert.DoesNotContain("## c", markdown);
        }

        [Fact]
        public void FormatPostAsMarkdown_IncludesSourceAndContent()
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

            string markdown = DiscourseForumService.FormatPostAsMarkdown(postJson, "https://discourse.mcneel.com");

            Assert.Contains("source: https://discourse.mcneel.com/t/g2-feature-requests-grasshopper2-icon-editor/204893/1", markdown);
            Assert.Contains("## archinate1 – 2025-05-22T14:50:51.740Z", markdown);
            Assert.Contains("I have been working with the Icon editor...", markdown);
        }
    }
}
