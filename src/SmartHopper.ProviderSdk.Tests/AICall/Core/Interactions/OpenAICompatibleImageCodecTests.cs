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
    using Xunit;

    /// <summary>
    /// Tests for <see cref="OpenAICompatibleImageCodec"/> response-side helpers.
    /// </summary>
    [Collection("ProviderSdk")]
    public class OpenAICompatibleImageCodecTests
    {
        /// <summary>A data-URI image entry decodes into ImageData + MimeType.</summary>
        [Fact]
        public void ExtractImages_DataUri_ReturnsBase64Image()
        {
            var message = JObject.Parse(@"{
                ""role"": ""assistant"",
                ""images"": [{
                    ""type"": ""image_url"",
                    ""image_url"": { ""url"": ""data:image/png;base64,iVBORw0KGgoAAAANS"" }
                }]
            }");

            var images = OpenAICompatibleImageCodec.ExtractImages(message);

            var image = Assert.Single(images);
            Assert.Equal("iVBORw0KGgoAAAANS", image.ImageData);
            Assert.Equal("image/png", image.MimeType);
            Assert.Equal(AIAgent.Assistant, image.Agent);
            Assert.Null(image.ImageUrl);
        }

        /// <summary>A remote URL entry decodes into ImageUrl.</summary>
        [Fact]
        public void ExtractImages_RemoteUrl_ReturnsLinkedImage()
        {
            var message = JObject.Parse(@"{
                ""role"": ""assistant"",
                ""images"": [{
                    ""type"": ""image_url"",
                    ""image_url"": { ""url"": ""https://files.example.com/img/abc.png"" }
                }]
            }");

            var images = OpenAICompatibleImageCodec.ExtractImages(message);

            var image = Assert.Single(images);
            Assert.Equal("https://files.example.com/img/abc.png", image.ImageUrl?.ToString());
            Assert.Equal(AIAgent.Assistant, image.Agent);
            Assert.Null(image.ImageData);
        }

        /// <summary>A message without an images array yields no interactions.</summary>
        [Fact]
        public void ExtractImages_NoImages_ReturnsEmpty()
        {
            var message = JObject.Parse(@"{ ""role"": ""assistant"", ""content"": ""hello"" }");

            Assert.Empty(OpenAICompatibleImageCodec.ExtractImages(message));
        }

        /// <summary>Malformed entries are skipped without failing the rest.</summary>
        [Fact]
        public void ExtractImages_SkipsUndecodableEntries()
        {
            var message = JObject.Parse(@"{
                ""images"": [
                    { ""type"": ""image_url"" },
                    { ""type"": ""image_url"", ""image_url"": { ""url"": ""data:image/jpeg;base64,QUJD"" } },
                    { ""type"": ""image_url"", ""image_url"": { ""url"": ""not a uri"" } }
                ]
            }");

            var images = OpenAICompatibleImageCodec.ExtractImages(message);

            var image = Assert.Single(images);
            Assert.Equal("QUJD", image.ImageData);
            Assert.Equal("image/jpeg", image.MimeType);
        }
    }
}
