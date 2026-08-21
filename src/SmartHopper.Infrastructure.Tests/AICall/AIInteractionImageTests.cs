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

namespace SmartHopper.Infrastructure.Tests.AICall
{
    using System;
    using SmartHopper.ProviderSdk.AICall.Core.Interactions;
    using Xunit;

    public class AIInteractionImageTests
    {
        [Fact(DisplayName = "CreateVisionInput_WithValidUri_SetsImageUrl")]
        public void CreateVisionInput_WithValidUri_SetsImageUrl()
        {
            var interaction = new AIInteractionImage();
            var uri = new Uri("https://example.com/image.png");
            interaction.CreateVisionInput(uri);
            Assert.Equal(uri, interaction.ImageUrl);
        }

        [Fact(DisplayName = "CreateVisionInput_WithNullUri_ThrowsArgumentNull")]
        public void CreateVisionInput_WithNullUri_ThrowsArgumentNull()
        {
            var interaction = new AIInteractionImage();
            Assert.Throws<ArgumentNullException>(() => interaction.CreateVisionInput((Uri)null));
        }

        [Fact(DisplayName = "CreateVisionInput_WithValidString_SetsImageUrl")]
        public void CreateVisionInput_WithValidString_SetsImageUrl()
        {
            var interaction = new AIInteractionImage();
            interaction.CreateVisionInput("https://example.com/image.png");
            Assert.NotNull(interaction.ImageUrl);
            Assert.Equal("https://example.com/image.png", interaction.ImageUrl.ToString());
        }

        [Fact(DisplayName = "CreateVisionInput_WithInvalidString_ThrowsArgument")]
        public void CreateVisionInput_WithInvalidString_ThrowsArgument()
        {
            var interaction = new AIInteractionImage();
            Assert.Throws<ArgumentException>(() => interaction.CreateVisionInput("not a valid url"));
        }

        [Fact(DisplayName = "CreateVisionInput_WithEmptyString_ThrowsArgument")]
        public void CreateVisionInput_WithEmptyString_ThrowsArgument()
        {
            var interaction = new AIInteractionImage();
            Assert.Throws<ArgumentException>(() => interaction.CreateVisionInput(string.Empty));
        }

        [Fact(DisplayName = "CreateVisionInputFromBase64_SetsDataAndMime")]
        public void CreateVisionInputFromBase64_SetsDataAndMime()
        {
            var interaction = new AIInteractionImage();
            var base64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";
            interaction.CreateVisionInputFromBase64(base64, "image/jpeg");
            Assert.Equal(base64, interaction.ImageData);
            Assert.Equal("image/jpeg", interaction.MimeType);
        }

        [Fact(DisplayName = "CreateVisionInputFromBase64_WithNullData_Throws")]
        public void CreateVisionInputFromBase64_WithNullData_Throws()
        {
            var interaction = new AIInteractionImage();
            Assert.Throws<ArgumentException>(() => interaction.CreateVisionInputFromBase64(null));
        }

        [Fact(DisplayName = "CreateVisionInputFromBase64_NullMime_DefaultsToImagePng")]
        public void CreateVisionInputFromBase64_NullMime_DefaultsToImagePng()
        {
            var interaction = new AIInteractionImage();
            var base64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";
            interaction.CreateVisionInputFromBase64(base64, null);
            Assert.Equal("image/png", interaction.MimeType);
        }

        [Fact(DisplayName = "MimeType_IsCorrectlySet_AfterCreateVisionInputFromBase64")]
        public void MimeType_IsCorrectlySet_AfterCreateVisionInputFromBase64()
        {
            var interaction = new AIInteractionImage();
            var base64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";
            interaction.CreateVisionInputFromBase64(base64, "image/webp");
            Assert.Equal("image/webp", interaction.MimeType);
        }

        [Fact(DisplayName = "SetResult_NullBothArgs_Throws")]
        public void SetResult_NullBothArgs_Throws()
        {
            var interaction = new AIInteractionImage();
            Assert.Throws<ArgumentNullException>(() => interaction.SetResult((string)null, null));
        }

        [Fact(DisplayName = "SetResult_WithUrl_SetsImageUrl")]
        public void SetResult_WithUrl_SetsImageUrl()
        {
            var interaction = new AIInteractionImage();
            interaction.SetResult("https://example.com/generated.png");
            Assert.NotNull(interaction.ImageUrl);
            Assert.Equal("https://example.com/generated.png", interaction.ImageUrl.ToString());
        }

        [Fact(DisplayName = "SetResult_WithData_SetsImageData")]
        public void SetResult_WithData_SetsImageData()
        {
            var interaction = new AIInteractionImage();
            var base64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";
            interaction.SetResult((string)null, base64);
            Assert.Equal(base64, interaction.ImageData);
        }

        [Fact(DisplayName = "SetResult_InvalidUrl_NoData_Throws")]
        public void SetResult_InvalidUrl_NoData_Throws()
        {
            var interaction = new AIInteractionImage();
            var ex = Assert.Throws<ArgumentException>(() => interaction.SetResult("not-a-url"));
            Assert.Equal("imageUrl", ex.ParamName);
            Assert.Null(interaction.ImageUrl);
            Assert.Null(interaction.ImageData);
        }

        [Fact(DisplayName = "SetResult_InvalidUrl_WithData_SetsImageData")]
        public void SetResult_InvalidUrl_WithData_SetsImageData()
        {
            var interaction = new AIInteractionImage();
            var base64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";
            interaction.SetResult("not-a-url", base64);
            Assert.Null(interaction.ImageUrl);
            Assert.Equal(base64, interaction.ImageData);
        }

        [Fact(DisplayName = "SetResult_WithRevisedPrompt_SetsRevisedPrompt")]
        public void SetResult_WithRevisedPrompt_SetsRevisedPrompt()
        {
            var interaction = new AIInteractionImage();
            interaction.SetResult("https://example.com/image.png", null, "A beautiful sunset over the ocean");
            Assert.Equal("A beautiful sunset over the ocean", interaction.RevisedPrompt);
        }

        [Fact(DisplayName = "CreateRequest_SetsPromptAndDefaults")]
        public void CreateRequest_SetsPromptAndDefaults()
        {
            var interaction = new AIInteractionImage();
            interaction.CreateRequest("A cat wearing sunglasses");
            Assert.Equal("A cat wearing sunglasses", interaction.OriginalPrompt);
            Assert.Equal("1024x1024", interaction.ImageSize);
            Assert.Equal("standard", interaction.ImageQuality);
            Assert.Equal("vivid", interaction.ImageStyle);
        }

        [Fact(DisplayName = "CreateRequest_WithCustomSize_OverridesDefault")]
        public void CreateRequest_WithCustomSize_OverridesDefault()
        {
            var interaction = new AIInteractionImage();
            interaction.CreateRequest("A cat", "512x512");
            Assert.Equal("512x512", interaction.ImageSize);
        }

        [Fact(DisplayName = "GetStreamKey_WithTurnId_IncludesTurnPrefix")]
        public void GetStreamKey_WithTurnId_IncludesTurnPrefix()
        {
            var interaction = new AIInteractionImage();
            interaction.TurnId = "turn-123";
            interaction.CreateRequest("A cat");
            var key = interaction.GetStreamKey();
            Assert.StartsWith("turn:turn-123:image:", key);
        }

        [Fact(DisplayName = "GetStreamKey_WithoutTurnId_NoPrefix")]
        public void GetStreamKey_WithoutTurnId_NoPrefix()
        {
            var interaction = new AIInteractionImage();
            interaction.CreateRequest("A cat");
            var key = interaction.GetStreamKey();
            Assert.StartsWith("image:", key);
        }

        [Fact(DisplayName = "GetRawContentForRender_WithUrl_ReturnsMarkdownImage")]
        public void GetRawContentForRender_WithUrl_ReturnsMarkdownImage()
        {
            var interaction = new AIInteractionImage();
            interaction.SetResult("https://example.com/image.png");
            var content = interaction.GetRawContentForRender();
            Assert.Contains("![generated image]", content);
            Assert.Contains("https://example.com/image.png", content);
        }

        [Fact(DisplayName = "GetRawContentForRender_WithData_ReturnsDataUriMarkdown")]
        public void GetRawContentForRender_WithData_ReturnsDataUriMarkdown()
        {
            var interaction = new AIInteractionImage();
            var base64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";
            interaction.SetResult((string)null, base64);
            var content = interaction.GetRawContentForRender();
            Assert.Contains("data:image/png;base64,", content);
        }

        [Fact(DisplayName = "GetRawReasoningForRender_ReturnsEmpty")]
        public void GetRawReasoningForRender_ReturnsEmpty()
        {
            var interaction = new AIInteractionImage();
            var reasoning = interaction.GetRawReasoningForRender();
            Assert.Empty(reasoning);
        }

        [Fact(DisplayName = "GetDisplayNameForRender_ReturnsAssistant")]
        public void GetDisplayNameForRender_ReturnsAssistant()
        {
            var interaction = new AIInteractionImage();
            var displayName = interaction.GetDisplayNameForRender();
            Assert.NotEmpty(displayName);
        }

        [Fact(DisplayName = "GetRoleClassForRender_ReturnsLowercase")]
        public void GetRoleClassForRender_ReturnsLowercase()
        {
            var interaction = new AIInteractionImage();
            var role = interaction.GetRoleClassForRender();
            Assert.Equal(role, role.ToLowerInvariant());
        }
    }
}
