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

namespace SmartHopper.ProviderSdk.Tests.AICall.Core.Interactions
{
    using Newtonsoft.Json.Linq;
    using SmartHopper.ProviderSdk.AICall.Core.Base;
    using SmartHopper.ProviderSdk.AICall.Core.Interactions;
    using SmartHopper.ProviderSdk.AICall.Utilities;
    using Xunit;

    /// <summary>
    /// Tests for <see cref="ToolResultMediaExtractor"/>, <see cref="AIInteractionToolResult.Images"/>,
    /// and <see cref="OpenAICompatibleImageCodec"/>.
    /// </summary>
    [Collection("ProviderSdk")]
    public class ToolResultImageTests
    {
        [Fact(DisplayName = "TrySplit extracts the image payload and compacts the result")]
        public void TrySplit_ExtractsImageAndCompacts()
        {
            var result = new JObject
            {
                ["imageBase64"] = "QUJD",
                ["mimeType"] = "image/png",
                ["imageAudience"] = "model",
                ["width"] = 800,
                ["height"] = 600,
                ["savedTo"] = "C:/tmp/x.png",
            };

            var split = ToolResultMediaExtractor.TrySplit(result, out var compact, out var images);

            Assert.True(split);
            Assert.Single(images);
            Assert.Equal("QUJD", images[0].ImageData);
            Assert.Equal("image/png", images[0].MimeType);
            Assert.True(images[0].SendToModel);
            Assert.Equal(800, images[0].Width);
            Assert.Equal(600, images[0].Height);

            Assert.NotNull(compact);
            Assert.Null(compact["imageBase64"]);
            Assert.Equal("model", compact["imageAudience"]?.ToString());
            Assert.True(compact["imageAttached"]?.Value<bool>());
            Assert.Equal("image/png", compact["mimeType"]?.ToString());
            Assert.Equal(800, compact["width"]?.Value<int>());

            // Original result object is untouched
            Assert.Equal("QUJD", result["imageBase64"]?.ToString());
        }

        [Fact(DisplayName = "TrySplit defaults missing/unknown audience to display-only")]
        public void TrySplit_AudienceDefaultsToDisplay()
        {
            var result = new JObject
            {
                ["imageBase64"] = "QUJD",
                ["mimeType"] = "image/png",
            };

            Assert.True(ToolResultMediaExtractor.TrySplit(result, out _, out var images));
            Assert.False(images[0].SendToModel);
        }

        [Fact(DisplayName = "TrySplit ignores results without a valid image payload")]
        public void TrySplit_NoImagePayload()
        {
            var plain = new JObject { ["success"] = true };
            Assert.False(ToolResultMediaExtractor.TrySplit(plain, out var compact, out var images));
            Assert.Same(plain, compact);
            Assert.Empty(images);

            var wrongMime = new JObject
            {
                ["imageBase64"] = "QUJD",
                ["mimeType"] = "application/json",
            };
            Assert.False(ToolResultMediaExtractor.TrySplit(wrongMime, out _, out var images2));
            Assert.Empty(images2);
        }

        [Fact(DisplayName = "Tool result render appends a markdown image per attached image")]
        public void ToolResultRender_AppendsImageMarkdown()
        {
            var interaction = new AIInteractionToolResult
            {
                Id = "call_1",
                Name = "canvas_screenshot",
                Result = new JObject { ["imageAttached"] = true, ["width"] = 10 },
                Images = new System.Collections.Generic.List<ToolResultImage>
                {
                    new ToolResultImage { ImageData = "QUJD", MimeType = "image/webp" },
                },
            };

            var render = interaction.GetRawContentForRender();

            Assert.Contains("\"imageAttached\": true", render);
            Assert.Contains("![canvas_screenshot image](data:image/webp;base64,QUJD)", render);
        }

        [Fact(DisplayName = "GetModelImages filters display-only and empty images")]
        public void GetModelImages_FiltersSendToModel()
        {
            var interaction = new AIInteractionToolResult
            {
                Images = new System.Collections.Generic.List<ToolResultImage>
                {
                    new ToolResultImage { ImageData = "QQ==", SendToModel = true },
                    new ToolResultImage { ImageData = "Qg==", SendToModel = false },
                    new ToolResultImage { ImageData = string.Empty, SendToModel = true },
                },
            };

            var images = interaction.GetModelImages();

            Assert.Single(images);
            Assert.Equal("QQ==", images[0].ImageData);
        }

        [Fact(DisplayName = "ToUserImageMessage builds an OpenAI-compatible user image message")]
        public void ToUserImageMessage_BuildsUserMessage()
        {
            var message = OpenAICompatibleImageCodec.ToUserImageMessage(new[]
            {
                new ToolResultImage { ImageData = "QUJD", MimeType = "image/png", SendToModel = true },
            });

            Assert.NotNull(message);
            Assert.Equal("user", message["role"]?.ToString());
            var content = message["content"] as JArray;
            Assert.NotNull(content);
            Assert.Single(content);
            Assert.Equal("image_url", content[0]?["type"]?.ToString());
            Assert.Equal(
                "data:image/png;base64,QUJD",
                content[0]?["image_url"]?["url"]?.ToString());
        }

        [Fact(DisplayName = "ToUserImageMessage returns null when no image has data")]
        public void ToUserImageMessage_NullWithoutData()
        {
            Assert.Null(OpenAICompatibleImageCodec.ToUserImageMessage(null));
            Assert.Null(OpenAICompatibleImageCodec.ToUserImageMessage(new[]
            {
                new ToolResultImage { ImageData = string.Empty },
            }));
        }
    }
}
