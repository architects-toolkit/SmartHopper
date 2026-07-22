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

namespace SmartHopper.Core.Grasshopper.Tests.Utils
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using SmartHopper.Core.Grasshopper.Utils.Internal;
    using SmartHopper.Infrastructure.AICall.Tools;
    using Xunit;

    /// <summary>
    /// Unit tests for <see cref="ImageProcessingService"/>.
    /// </summary>
    public class ImageProcessingServiceTests
    {
        /// <summary>
        /// Verifies that link mode returns the original Markdown image reference.
        /// </summary>
        [Fact]
        public void BuildMarkdownReplacement_LinkMode_ReturnsMarkdownLink()
        {
            string result = ImageProcessingService.BuildMarkdownReplacement(
                "link",
                aiText: string.Empty,
                altText: "diagram",
                url: "https://example.com/image.png",
                mimeType: "image/png",
                base64Data: "data",
                imageId: "img-1",
                context: "page 1");

            Assert.Equal("![diagram](https://example.com/image.png)", result);
        }

        /// <summary>
        /// Verifies that embed mode returns a base64 data URI.
        /// </summary>
        [Fact]
        public void BuildMarkdownReplacement_EmbedMode_ReturnsBase64DataUri()
        {
            string result = ImageProcessingService.BuildMarkdownReplacement(
                "embed",
                aiText: "A chart",
                altText: "diagram",
                url: "https://example.com/image.png",
                mimeType: "image/png",
                base64Data: "abc123",
                imageId: "img-1",
                context: "page 1");

            Assert.Equal("![A chart](data:image/png;base64,abc123)", result);
        }

        /// <summary>
        /// Verifies that describe mode returns a text block with the image id and context.
        /// </summary>
        [Fact]
        public void BuildMarkdownReplacement_DescribeMode_ReturnsDescriptionBlock()
        {
            string result = ImageProcessingService.BuildMarkdownReplacement(
                "describe",
                aiText: "A detailed chart.",
                altText: "diagram",
                url: "https://example.com/image.png",
                mimeType: "image/png",
                base64Data: "abc123",
                imageId: "img-1",
                context: "page 1");

            Assert.Contains("**[img-1 — page 1]**", result);
            Assert.Contains("A detailed chart.", result);
        }

        /// <summary>
        /// Verifies that ProcessMarkdownImagesAsync replaces file placeholders with link references.
        /// </summary>
        [Fact]
        public async Task ProcessMarkdownImagesAsync_FilePlaceholders_LinkMode_ReplacesAll()
        {
            string markdown = "See [image 1] and [image 2].";
            var items = new List<ImageProcessingItem>
            {
                new ImageProcessingItem
                {
                    Id = "img-1",
                    Context = "page 1",
                    MimeType = "image/png",
                    Base64Data = "abc",
                    AltText = "first",
                    Placeholder = "[image 1]",
                },
                new ImageProcessingItem
                {
                    Id = "img-2",
                    Context = "page 2",
                    MimeType = "image/jpeg",
                    Base64Data = "def",
                    AltText = "second",
                    Placeholder = "[image 2]",
                },
            };

            string result = await ImageProcessingService.ProcessMarkdownImagesAsync(markdown, items, "link", new AIToolCall()).ConfigureAwait(false);

            Assert.Contains("![first]()", result);
            Assert.Contains("![second]()", result);
            Assert.DoesNotContain("[image 1]", result);
            Assert.DoesNotContain("[image 2]", result);
        }

        /// <summary>
        /// Verifies that ProcessMarkdownImagesAsync replaces web image references with link references.
        /// </summary>
        [Fact]
        public async Task ProcessMarkdownImagesAsync_WebReferences_LinkMode_ReplacesAll()
        {
            string markdown = "Here is ![alt text](https://example.com/a.png) and another ![b](https://example.com/b.jpg).";
            var items = new List<ImageProcessingItem>
            {
                new ImageProcessingItem
                {
                    Id = "web-img-1",
                    Context = "https://example.com/a.png",
                    AltText = "alt text",
                    Url = "https://example.com/a.png",
                    Placeholder = "![alt text](https://example.com/a.png)",
                },
                new ImageProcessingItem
                {
                    Id = "web-img-2",
                    Context = "https://example.com/b.jpg",
                    AltText = "b",
                    Url = "https://example.com/b.jpg",
                    Placeholder = "![b](https://example.com/b.jpg)",
                },
            };

            string result = await ImageProcessingService.ProcessMarkdownImagesAsync(markdown, items, "link", new AIToolCall()).ConfigureAwait(false);

            Assert.Equal(markdown, result);
        }

        /// <summary>
        /// Verifies that ProcessMarkdownImagesAsync returns the original Markdown when no items are provided.
        /// </summary>
        [Fact]
        public async Task ProcessMarkdownImagesAsync_NoItems_ReturnsOriginalMarkdown()
        {
            string markdown = "No images here.";
            string result = await ImageProcessingService.ProcessMarkdownImagesAsync(markdown, new List<ImageProcessingItem>(), "link", new AIToolCall()).ConfigureAwait(false);

            Assert.Equal(markdown, result);
        }

        /// <summary>
        /// Verifies that ProcessMarkdownImagesAsync skips items with empty placeholders.
        /// </summary>
        [Fact]
        public async Task ProcessMarkdownImagesAsync_EmptyPlaceholder_SkipsItem()
        {
            string markdown = "Only [image 1] should be replaced.";
            var items = new List<ImageProcessingItem>
            {
                new ImageProcessingItem
                {
                    Id = "img-1",
                    Context = "page 1",
                    MimeType = "image/png",
                    Base64Data = "abc",
                    AltText = "first",
                    Placeholder = "[image 1]",
                },
                new ImageProcessingItem
                {
                    Id = "img-2",
                    Context = "page 2",
                    Placeholder = string.Empty,
                },
            };

            string result = await ImageProcessingService.ProcessMarkdownImagesAsync(markdown, items, "link", new AIToolCall()).ConfigureAwait(false);

            Assert.Contains("![first]()", result);
            Assert.DoesNotContain("[image 1]", result);
        }
    }
}
