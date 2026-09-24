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

#if NET7_WINDOWS

namespace SmartHopper.ProviderSdk.Tests.Providers.OpenRouter
{
    using Newtonsoft.Json.Linq;
    using SmartHopper.Providers.OpenRouter;
    using SmartHopper.ProviderSdk.AICall.Core.Base;
    using SmartHopper.ProviderSdk.AICall.Core.Interactions;
    using SmartHopper.ProviderSdk.AICall.Core.Requests;
    using Xunit;

    /// <summary>
    /// Verifies that model-bound <see cref="ToolResultImage"/> payloads are emitted as a
    /// trailing user-role image message in the OpenAI-compatible wire format.
    /// </summary>
    [Collection("ProviderSdk")]
    public class OpenRouterToolResultImageTests
    {
        /// <summary>
        /// A tool result carrying a model-bound image must expand to a tool message
        /// followed by a user message containing the image_url part.
        /// </summary>
        [Fact]
        public void Encode_ToolResultImage_EmitsTrailingUserImageMessage()
        {
            var provider = OpenRouterProvider.Instance;
            var request = new AIRequestCall();
            request.Body = AIBodyBuilder.Create()
                .Add(new AIInteractionText { Agent = AIAgent.User, Content = "look at this" })
                .Add(new AIInteractionToolResult
                {
                    Id = "call_1",
                    Name = "canvas_screenshot",
                    Result = new JObject { ["imageAttached"] = true, ["width"] = 10 },
                    Images = new System.Collections.Generic.List<ToolResultImage>
                    {
                        new ToolResultImage
                        {
                            ImageData = "QUJD",
                            MimeType = "image/png",
                            SendToModel = true,
                        },
                    },
                })
                .Build();

            var encoded = provider.Encode(request);
            var body = JObject.Parse(encoded);
            var messages = body["messages"] as JArray;

            Assert.NotNull(messages);
            Assert.Equal(3, messages.Count);
            Assert.Equal("user", messages[0]?["role"]?.ToString());
            Assert.Equal("tool", messages[1]?["role"]?.ToString());
            Assert.Equal("call_1", messages[1]?["tool_call_id"]?.ToString());

            var imageMessage = messages[2] as JObject;
            Assert.NotNull(imageMessage);
            Assert.Equal("user", imageMessage["role"]?.ToString());
            var parts = imageMessage["content"] as JArray;
            Assert.NotNull(parts);
            Assert.Single(parts);
            Assert.Equal("image_url", parts[0]?["type"]?.ToString());
            Assert.Equal(
                "data:image/png;base64,QUJD",
                parts[0]?["image_url"]?["url"]?.ToString());
        }

        /// <summary>
        /// Display-only images are never emitted to the provider.
        /// </summary>
        [Fact]
        public void Encode_DisplayOnlyImage_NotEmitted()
        {
            var provider = OpenRouterProvider.Instance;
            var request = new AIRequestCall();
            request.Body = AIBodyBuilder.Create()
                .Add(new AIInteractionToolResult
                {
                    Id = "call_2",
                    Name = "canvas_hi-res_screenshot",
                    Result = new JObject { ["imageAttached"] = true },
                    Images = new System.Collections.Generic.List<ToolResultImage>
                    {
                        new ToolResultImage
                        {
                            ImageData = "QUJD",
                            MimeType = "image/png",
                            SendToModel = false,
                        },
                    },
                })
                .Build();

            var encoded = provider.Encode(request);
            var body = JObject.Parse(encoded);
            var messages = body["messages"] as JArray;

            Assert.NotNull(messages);
            Assert.Single(messages);
            Assert.Equal("tool", messages[0]?["role"]?.ToString());
            Assert.DoesNotContain("QUJD", encoded);
        }
    }
}

#endif
