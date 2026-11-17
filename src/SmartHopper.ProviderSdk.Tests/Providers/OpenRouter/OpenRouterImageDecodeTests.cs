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

namespace SmartHopper.ProviderSdk.Tests.Providers.OpenRouter
{
    using System.Linq;
    using Newtonsoft.Json.Linq;
    using SmartHopper.Providers.OpenRouter;
    using SmartHopper.ProviderSdk.AICall.Core.Interactions;
    using Xunit;

    /// <summary>
    /// Tests that <see cref="OpenRouterProvider"/> decodes generated images
    /// carried in <c>message.images</c> on chat-completion responses.
    /// </summary>
    [Collection("ProviderSdk")]
    public class OpenRouterImageDecodeTests
    {
        /// <summary>An image-generation response yields an AIInteractionImage alongside the text.</summary>
        [Fact]
        public void Decode_MessageImages_ReturnsImageInteraction()
        {
            var response = JObject.Parse(@"{
                ""id"": ""gen-1"",
                ""model"": ""google/gemini-3-pro-image-preview"",
                ""choices"": [{
                    ""index"": 0,
                    ""message"": {
                        ""role"": ""assistant"",
                        ""content"": ""Here is your image."",
                        ""images"": [{
                            ""type"": ""image_url"",
                            ""image_url"": { ""url"": ""data:image/png;base64,iVBORw0KGgoAAAANS"" }
                        }]
                    },
                    ""finish_reason"": ""stop""
                }]
            }");

            var interactions = OpenRouterProvider.Instance.Decode(response);

            var image = interactions.OfType<AIInteractionImage>().SingleOrDefault();
            Assert.NotNull(image);
            Assert.Equal("iVBORw0KGgoAAAANS", image.ImageData);
            Assert.Equal("image/png", image.MimeType);

            var text = interactions.OfType<AIInteractionText>().SingleOrDefault();
            Assert.NotNull(text);
            Assert.Equal("Here is your image.", text.Content);
        }
    }
}
