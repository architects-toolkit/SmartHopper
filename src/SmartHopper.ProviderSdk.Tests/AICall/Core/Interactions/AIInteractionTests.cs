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
    using System;
    using System.Collections.Generic;
    using System.IO;
    using Newtonsoft.Json.Linq;
    using SmartHopper.ProviderSdk.AICall.Core.Base;
    using SmartHopper.ProviderSdk.AICall.Core.Interactions;
    using SmartHopper.ProviderSdk.AICall.Metrics;
    using SmartHopper.ProviderSdk.AICall.Utilities;
    using SmartHopper.ProviderSdk.Diagnostics;
    using Xunit;

    /// <summary>
    /// Tests for the <see cref="AIInteractionText"/>, <see cref="AIInteractionToolCall"/>,
    /// <see cref="AIInteractionToolResult"/>, <see cref="AIInteractionImage"/>,
    /// <see cref="AIInteractionRuntimeMessage"/> and <see cref="AIInteractionAudio"/> interactions.
    /// </summary>
    [Collection("ProviderSdk")]
    public class AIInteractionTests
    {
#if NET7_WINDOWS
        private const string PlatformSuffix = " [Windows]";
#else
        private const string PlatformSuffix = " [Core]";
#endif

        #region AIInteractionText

        [Fact(DisplayName = nameof(Text_SetResult) + PlatformSuffix)]
        public void Text_SetResult()
        {
            var interaction = new AIInteractionText();
            interaction.SetResult(AIAgent.Assistant, "content", "reasoning");

            Assert.Equal(AIAgent.Assistant, interaction.Agent);
            Assert.Equal("content", interaction.Content);
            Assert.Equal("reasoning", interaction.Reasoning);
        }

        [Fact(DisplayName = nameof(Text_AppendDeltaCombinesContentReasoningAndMetrics) + PlatformSuffix)]
        public void Text_AppendDeltaCombinesContentReasoningAndMetrics()
        {
            var interaction = new AIInteractionText();
            interaction.SetResult(AIAgent.Assistant, "Hello", "think");

            var metrics = new AIMetrics
            {
                InputTokensPrompt = 10,
                OutputTokensGeneration = 5,
            };

            interaction.AppendDelta(
                contentDelta: " world",
                reasoningDelta: " more",
                metricsDelta: metrics);

            Assert.Equal("Hello world", interaction.Content);
            Assert.Equal("think more", interaction.Reasoning);
            Assert.Equal(10, interaction.Metrics.InputTokensPrompt);
            Assert.Equal(5, interaction.Metrics.OutputTokensGeneration);
            Assert.Equal(15, interaction.Metrics.TotalTokens);
        }

        [Fact(DisplayName = nameof(Text_ToStringWithoutReasoning) + PlatformSuffix)]
        public void Text_ToStringWithoutReasoning()
        {
            var interaction = new AIInteractionText();
            interaction.SetResult(AIAgent.Assistant, "only content");

            Assert.Equal("only content", interaction.ToString());
        }

        [Fact(DisplayName = nameof(Text_ToStringWithReasoning) + PlatformSuffix)]
        public void Text_ToStringWithReasoning()
        {
            var interaction = new AIInteractionText();
            interaction.SetResult(AIAgent.Assistant, "answer", "thinking");

            var result = interaction.ToString();

            Assert.Contains("<think>", result);
            Assert.Contains("<think>", result);
            Assert.Contains("thinking", result);
            Assert.Contains("answer", result);
        }

        [Fact(DisplayName = nameof(Text_GetStreamKeyWithoutTurnId) + PlatformSuffix)]
        public void Text_GetStreamKeyWithoutTurnId()
        {
            var interaction = new AIInteractionText();
            interaction.SetResult(AIAgent.Assistant, "content");

            Assert.Equal("text:assistant", interaction.GetStreamKey());
        }

        [Fact(DisplayName = nameof(Text_GetStreamKeyWithTurnId) + PlatformSuffix)]
        public void Text_GetStreamKeyWithTurnId()
        {
            var interaction = new AIInteractionText
            {
                TurnId = "turn-1",
            };
            interaction.SetResult(AIAgent.User, "content");

            Assert.Equal("turn:turn-1:user", interaction.GetStreamKey());
        }

        [Fact(DisplayName = nameof(Text_GetDedupKey) + PlatformSuffix)]
        public void Text_GetDedupKey()
        {
            var interaction = new AIInteractionText
            {
                TurnId = "turn-1",
            };
            interaction.SetResult(AIAgent.Assistant, "content");

            var hash = HashUtility.ComputeShortHash("turn-1:assistant:content");
            var expected = $"turn:turn-1:assistant:{hash}";

            Assert.Equal(expected, interaction.GetDedupKey());
        }

        [Fact(DisplayName = nameof(Text_GetRoleClassForRender) + PlatformSuffix)]
        public void Text_GetRoleClassForRender()
        {
            var assistant = new AIInteractionText();
            assistant.SetResult(AIAgent.Assistant, "a");

            var user = new AIInteractionText();
            user.SetResult(AIAgent.User, "b");

            Assert.Equal("assistant", assistant.GetRoleClassForRender());
            Assert.Equal("user", user.GetRoleClassForRender());
        }

        [Fact(DisplayName = nameof(Text_GetDisplayNameForRender) + PlatformSuffix)]
        public void Text_GetDisplayNameForRender()
        {
            var assistant = new AIInteractionText();
            assistant.SetResult(AIAgent.Assistant, "a");

            Assert.Equal("Assistant", assistant.GetDisplayNameForRender());
        }

        [Fact(DisplayName = nameof(Text_GetRawContentForRender) + PlatformSuffix)]
        public void Text_GetRawContentForRender()
        {
            var interaction = new AIInteractionText();
            interaction.SetResult(AIAgent.Assistant, "content");

            Assert.Equal("content", interaction.GetRawContentForRender());
        }

        [Fact(DisplayName = nameof(Text_GetRawReasoningForRender) + PlatformSuffix)]
        public void Text_GetRawReasoningForRender()
        {
            var interaction = new AIInteractionText();
            interaction.SetResult(AIAgent.Assistant, "content", "reasoning");

            Assert.Equal("reasoning", interaction.GetRawReasoningForRender());

            var emptyReasoning = new AIInteractionText();
            Assert.Equal(string.Empty, emptyReasoning.GetRawReasoningForRender());
        }

        #endregion

        #region AIInteractionToolCall

        [Fact(DisplayName = nameof(ToolCall_ToStringWithoutIdNameAndArguments) + PlatformSuffix)]
        public void ToolCall_ToStringWithoutIdNameAndArguments()
        {
            var interaction = new AIInteractionToolCall();

            Assert.Equal("Calling tool", interaction.ToString());
        }

        [Fact(DisplayName = nameof(ToolCall_ToStringWithId) + PlatformSuffix)]
        public void ToolCall_ToStringWithId()
        {
            var interaction = new AIInteractionToolCall
            {
                Id = "tc_1",
            };

            Assert.Equal("Calling tool (tc_1)", interaction.ToString());
        }

        [Fact(DisplayName = nameof(ToolCall_ToStringWithName) + PlatformSuffix)]
        public void ToolCall_ToStringWithName()
        {
            var interaction = new AIInteractionToolCall
            {
                Name = "my_tool",
            };

            Assert.Equal("Calling tool my_tool", interaction.ToString());
        }

        [Fact(DisplayName = nameof(ToolCall_ToStringWithArguments) + PlatformSuffix)]
        public void ToolCall_ToStringWithArguments()
        {
            var interaction = new AIInteractionToolCall
            {
                Id = "tc_1",
                Name = "my_tool",
                Arguments = new JObject { ["x"] = 1 },
            };

            var result = interaction.ToString();

            Assert.Contains("Calling tool (tc_1) my_tool", result);
            Assert.Contains("with the following arguments:", result);
            Assert.Contains("\"x\": 1", result);
        }

        [Fact(DisplayName = nameof(ToolCall_GetStreamKeyWithoutTurnId) + PlatformSuffix)]
        public void ToolCall_GetStreamKeyWithoutTurnId()
        {
            var byId = new AIInteractionToolCall { Id = "tc_1" };
            var byName = new AIInteractionToolCall { Name = "my_tool" };
            var empty = new AIInteractionToolCall();

            Assert.Equal("tool.call:tc_1", byId.GetStreamKey());
            Assert.Equal("tool.call:my_tool", byName.GetStreamKey());
            Assert.Equal("tool.call:", empty.GetStreamKey());
        }

        [Fact(DisplayName = nameof(ToolCall_GetStreamKeyWithTurnId) + PlatformSuffix)]
        public void ToolCall_GetStreamKeyWithTurnId()
        {
            var interaction = new AIInteractionToolCall
            {
                TurnId = "turn-1",
                Id = "tc_1",
            };

            Assert.Equal("turn:turn-1:tool.call:tc_1", interaction.GetStreamKey());
        }

        [Fact(DisplayName = nameof(ToolCall_GetDedupKey) + PlatformSuffix)]
        public void ToolCall_GetDedupKey()
        {
            var interaction = new AIInteractionToolCall
            {
                TurnId = "turn-1",
                Id = "tc_1",
                Arguments = new JObject { ["x"] = 1 },
            };

            var argsHash = HashUtility.ComputeShortHash(interaction.Arguments.ToString(Newtonsoft.Json.Formatting.None));
            var expected = $"turn:turn-1:tool.call:tc_1:{argsHash}";

            Assert.Equal(expected, interaction.GetDedupKey());
        }

        [Fact(DisplayName = nameof(ToolCall_GetDedupKeyWithNoArguments) + PlatformSuffix)]
        public void ToolCall_GetDedupKeyWithNoArguments()
        {
            var interaction = new AIInteractionToolCall
            {
                Id = "tc_1",
            };

            Assert.Equal("tool.call:tc_1:none", interaction.GetDedupKey());
        }

        [Fact(DisplayName = nameof(ToolCall_GetRoleClassForRender) + PlatformSuffix)]
        public void ToolCall_GetRoleClassForRender()
        {
            var interaction = new AIInteractionToolCall();

            Assert.Equal("tool", interaction.GetRoleClassForRender());
        }

        [Fact(DisplayName = nameof(ToolCall_GetDisplayNameForRender) + PlatformSuffix)]
        public void ToolCall_GetDisplayNameForRender()
        {
            var unnamed = new AIInteractionToolCall();
            var named = new AIInteractionToolCall { Name = "my_tool" };

            Assert.Equal("Tool Call", unnamed.GetDisplayNameForRender());
            Assert.Equal("Tool Call: my_tool", named.GetDisplayNameForRender());
        }

        [Fact(DisplayName = nameof(ToolCall_GetRawContentForRender) + PlatformSuffix)]
        public void ToolCall_GetRawContentForRender()
        {
            var interaction = new AIInteractionToolCall
            {
                Arguments = new JObject { ["x"] = 1 },
            };

            var result = interaction.GetRawContentForRender();

            Assert.Contains("\"x\": 1", result);

            var empty = new AIInteractionToolCall();

            Assert.Equal(string.Empty, empty.GetRawContentForRender());
        }

        [Fact(DisplayName = nameof(ToolCall_GetRawReasoningForRender) + PlatformSuffix)]
        public void ToolCall_GetRawReasoningForRender()
        {
            var interaction = new AIInteractionToolCall
            {
                Reasoning = "some thought",
            };

            Assert.Equal("some thought", interaction.GetRawReasoningForRender());

            var emptyReasoning = new AIInteractionToolCall();
            Assert.Equal(string.Empty, emptyReasoning.GetRawReasoningForRender());
        }

        #endregion

        #region AIInteractionToolResult

        [Fact(DisplayName = nameof(ToolResult_ToString) + PlatformSuffix)]
        public void ToolResult_ToString()
        {
            var interaction = new AIInteractionToolResult
            {
                Name = "my_tool",
                Result = new JObject { ["ok"] = true },
            };

            var result = interaction.ToString();

            Assert.Contains("Tool result from my_tool", result);
            Assert.Contains("\"ok\": true", result);
        }

        [Fact(DisplayName = nameof(ToolResult_GetStreamKey) + PlatformSuffix)]
        public void ToolResult_GetStreamKey()
        {
            var withoutTurn = new AIInteractionToolResult
            {
                Id = "tr_1",
            };

            var withTurn = new AIInteractionToolResult
            {
                TurnId = "turn-1",
                Name = "my_tool",
            };

            Assert.Equal("tool.result:tr_1", withoutTurn.GetStreamKey());
            Assert.Equal("turn:turn-1:tool.result:my_tool", withTurn.GetStreamKey());
        }

        [Fact(DisplayName = nameof(ToolResult_GetDedupKey) + PlatformSuffix)]
        public void ToolResult_GetDedupKey()
        {
            var interaction = new AIInteractionToolResult
            {
                Id = "tr_1",
                Result = new JObject { ["ok"] = true },
            };

            var key = interaction.GetDedupKey();

            Assert.StartsWith("tool.result:tr_1:", key);
            Assert.Equal("tool.result:tr_1:".Length + 16, key.Length);
        }

        [Fact(DisplayName = nameof(ToolResult_MessagesPropagation) + PlatformSuffix)]
        public void ToolResult_MessagesPropagation()
        {
            var messages = new List<SHRuntimeMessage>
            {
                new SHRuntimeMessage(
                    SHRuntimeMessageSeverity.Warning,
                    SHRuntimeMessageOrigin.Tool,
                    SHMessageCode.ToolExecutionError,
                    "warning message"),
            };

            var interaction = new AIInteractionToolResult
            {
                Messages = messages,
            };

            Assert.Single(interaction.Messages);
            Assert.Equal("warning message", interaction.Messages[0].Message);
        }

        #endregion

        #region AIInteractionImage

        [Fact(DisplayName = nameof(Image_CreateVisionInputWithValidUrl) + PlatformSuffix)]
        public void Image_CreateVisionInputWithValidUrl()
        {
            var interaction = new AIInteractionImage();
            interaction.CreateVisionInput("https://example.com/image.png");

            Assert.Equal("https://example.com/image.png", interaction.ImageUrl.ToString());
            Assert.Contains("[vision input]", interaction.ToString());
        }

        [Fact(DisplayName = nameof(Image_CreateVisionInputWithInvalidUrlThrows) + PlatformSuffix)]
        public void Image_CreateVisionInputWithInvalidUrlThrows()
        {
            var interaction = new AIInteractionImage();

            Assert.Throws<ArgumentException>(() => interaction.CreateVisionInput("not a valid url"));
        }

        [Fact(DisplayName = nameof(Image_CreateVisionInputWithNullOrEmptyThrows) + PlatformSuffix)]
        public void Image_CreateVisionInputWithNullOrEmptyThrows()
        {
            var interaction = new AIInteractionImage();

            Assert.Throws<ArgumentException>(() => interaction.CreateVisionInput(string.Empty));
            Assert.Throws<ArgumentException>(() => interaction.CreateVisionInput("   "));
        }

        [Fact(DisplayName = nameof(Image_CreateVisionInputFromBase64) + PlatformSuffix)]
        public void Image_CreateVisionInputFromBase64()
        {
            var interaction = new AIInteractionImage();
            interaction.CreateVisionInputFromBase64("base64data");

            Assert.Equal("base64data", interaction.ImageData);
            Assert.Equal("image/png", interaction.MimeType);

            var withMime = new AIInteractionImage();
            withMime.CreateVisionInputFromBase64("base64data", "image/jpeg");

            Assert.Equal("image/jpeg", withMime.MimeType);
        }

        [Fact(DisplayName = nameof(Image_CreateRequestSetsProperties) + PlatformSuffix)]
        public void Image_CreateRequestSetsProperties()
        {
            var interaction = new AIInteractionImage();
            interaction.CreateRequest(
                prompt: "a red apple",
                size: "512x512",
                quality: "hd",
                style: "natural",
                aspectRatio: "16:9");

            Assert.Equal("a red apple", interaction.OriginalPrompt);
            Assert.Equal("512x512", interaction.ImageSize);
            Assert.Equal("hd", interaction.ImageQuality);
            Assert.Equal("natural", interaction.ImageStyle);
            Assert.Equal("16:9", interaction.AspectRatio);
        }

        [Fact(DisplayName = nameof(Image_SetResultWithValidUrl) + PlatformSuffix)]
        public void Image_SetResultWithValidUrl()
        {
            var interaction = new AIInteractionImage();
            interaction.SetResult("https://example.com/image.png");

            Assert.Equal("https://example.com/image.png", interaction.ImageUrl.ToString());
        }

        [Fact(DisplayName = nameof(Image_SetResultInvalidUrlWithoutImageDataThrows) + PlatformSuffix)]
        public void Image_SetResultInvalidUrlWithoutImageDataThrows()
        {
            var interaction = new AIInteractionImage();

            Assert.Throws<ArgumentException>(() => interaction.SetResult("not a valid url"));
        }

        [Fact(DisplayName = nameof(Image_SetResultInvalidUrlWithImageDataSucceeds) + PlatformSuffix)]
        public void Image_SetResultInvalidUrlWithImageDataSucceeds()
        {
            var interaction = new AIInteractionImage();
            interaction.CreateRequest("a cat");
            interaction.SetResult("not a valid url", "base64data");

            Assert.Null(interaction.ImageUrl);
            Assert.Equal("base64data", interaction.ImageData);
            Assert.Contains("generated from 'a cat'", interaction.ToString());
            Assert.Contains("data:image/png;base64,base64data", interaction.GetRawContentForRender());
        }

        [Fact(DisplayName = nameof(Image_ToStringForVisionInput) + PlatformSuffix)]
        public void Image_ToStringForVisionInput()
        {
            var interaction = new AIInteractionImage();
            interaction.CreateVisionInput("https://example.com/image.png");

            Assert.Equal("AIInteractionImage (1024x1024) [vision input]", interaction.ToString());
        }

        [Fact(DisplayName = nameof(Image_ToStringForGeneratedImage) + PlatformSuffix)]
        public void Image_ToStringForGeneratedImage()
        {
            var shortPrompt = new AIInteractionImage();
            shortPrompt.CreateRequest("a cat");

            Assert.Equal("AIInteractionImage (1024x1024) generated from 'a cat'", shortPrompt.ToString());

            var longPrompt = new AIInteractionImage();
            longPrompt.CreateRequest("a very long prompt that should be truncated in the output");

            Assert.Contains("generated from '", longPrompt.ToString());
            Assert.Contains("...", longPrompt.ToString());
        }

        [Fact(DisplayName = nameof(Image_GetRoleClassForRender) + PlatformSuffix)]
        public void Image_GetRoleClassForRender()
        {
            var defaultImage = new AIInteractionImage();
            var userImage = new AIInteractionImage { Agent = AIAgent.User };

            Assert.Equal("assistant", defaultImage.GetRoleClassForRender());
            Assert.Equal("user", userImage.GetRoleClassForRender());
        }

        [Fact(DisplayName = nameof(Image_GetDisplayNameForRender) + PlatformSuffix)]
        public void Image_GetDisplayNameForRender()
        {
            var defaultImage = new AIInteractionImage();
            var userImage = new AIInteractionImage { Agent = AIAgent.User };

            Assert.Equal("Assistant", defaultImage.GetDisplayNameForRender());
            Assert.Equal("User", userImage.GetDisplayNameForRender());
        }

        [Fact(DisplayName = nameof(Image_GetRawContentForRender) + PlatformSuffix)]
        public void Image_GetRawContentForRender()
        {
            var withUrl = new AIInteractionImage();
            withUrl.SetResult("https://example.com/image.png");

            Assert.Equal("![generated image](https://example.com/image.png)", withUrl.GetRawContentForRender());

            var withData = new AIInteractionImage();
            withData.CreateVisionInputFromBase64("data");

            Assert.Equal("![generated image](data:image/png;base64,data)", withData.GetRawContentForRender());

            var neither = new AIInteractionImage();
            neither.CreateRequest("a cat");

            Assert.Contains("generated from 'a cat'", neither.GetRawContentForRender());
        }

        #endregion

        #region AIInteractionRuntimeMessage

        [Fact(DisplayName = nameof(RuntimeMessage_CreateDebugIsNonSurfaceable) + PlatformSuffix)]
        public void RuntimeMessage_CreateDebugIsNonSurfaceable()
        {
            var interaction = AIInteractionRuntimeMessage.CreateDebug("debug content");

            Assert.Equal(SHRuntimeMessageSeverity.Debug, interaction.Severity);
            Assert.False(interaction.Surfaceable);
            Assert.Equal("debug content", interaction.Content);
            Assert.Equal(AIAgent.Debug, interaction.Agent);
        }

        [Fact(DisplayName = nameof(RuntimeMessage_FromRuntimeMessageNullReturnsNull) + PlatformSuffix)]
        public void RuntimeMessage_FromRuntimeMessageNullReturnsNull()
        {
            Assert.Null(AIInteractionRuntimeMessage.FromRuntimeMessage(null!));
        }

        [Fact(DisplayName = nameof(RuntimeMessage_FromRuntimeMessageRoundTrip) + PlatformSuffix)]
        public void RuntimeMessage_FromRuntimeMessageRoundTrip()
        {
            var message = new SHRuntimeMessage(
                SHRuntimeMessageSeverity.Warning,
                SHRuntimeMessageOrigin.Provider,
                SHMessageCode.NetworkTimeout,
                "timeout",
                false);

            var interaction = AIInteractionRuntimeMessage.FromRuntimeMessage(message);

            Assert.Equal(SHRuntimeMessageSeverity.Warning, interaction.Severity);
            Assert.Equal(SHRuntimeMessageOrigin.Provider, interaction.Origin);
            Assert.Equal(SHMessageCode.NetworkTimeout, interaction.Code);
            Assert.Equal("timeout", interaction.Content);
            Assert.False(interaction.Surfaceable);
            Assert.Equal(AIAgent.Warning, interaction.Agent);
        }

        [Fact(DisplayName = nameof(RuntimeMessage_AgentDerivedFromSeverity) + PlatformSuffix)]
        public void RuntimeMessage_AgentDerivedFromSeverity()
        {
            var error = new AIInteractionRuntimeMessage { Severity = SHRuntimeMessageSeverity.Error };
            var warning = new AIInteractionRuntimeMessage { Severity = SHRuntimeMessageSeverity.Warning };
            var info = new AIInteractionRuntimeMessage { Severity = SHRuntimeMessageSeverity.Info };
            var debug = new AIInteractionRuntimeMessage { Severity = SHRuntimeMessageSeverity.Debug };

            Assert.Equal(AIAgent.Error, error.Agent);
            Assert.Equal(AIAgent.Warning, warning.Agent);
            Assert.Equal(AIAgent.Info, info.Agent);
            Assert.Equal(AIAgent.Debug, debug.Agent);
        }

        [Fact(DisplayName = nameof(RuntimeMessage_ToRuntimeMessage) + PlatformSuffix)]
        public void RuntimeMessage_ToRuntimeMessage()
        {
            var interaction = new AIInteractionRuntimeMessage
            {
                Severity = SHRuntimeMessageSeverity.Error,
                Origin = SHRuntimeMessageOrigin.Tool,
                Code = SHMessageCode.ToolExecutionError,
                Content = "error content",
                Surfaceable = false,
            };

            var message = interaction.ToRuntimeMessage();

            Assert.Equal(SHRuntimeMessageSeverity.Error, message.Severity);
            Assert.Equal(SHRuntimeMessageOrigin.Tool, message.Origin);
            Assert.Equal(SHMessageCode.ToolExecutionError, message.Code);
            Assert.Equal("error content", message.Message);
            Assert.False(message.Surfaceable);
        }

        [Fact(DisplayName = nameof(RuntimeMessage_GetRoleClassForRender) + PlatformSuffix)]
        public void RuntimeMessage_GetRoleClassForRender()
        {
            Assert.Equal("error", new AIInteractionRuntimeMessage { Severity = SHRuntimeMessageSeverity.Error }.GetRoleClassForRender());
            Assert.Equal("warning", new AIInteractionRuntimeMessage { Severity = SHRuntimeMessageSeverity.Warning }.GetRoleClassForRender());
            Assert.Equal("info", new AIInteractionRuntimeMessage { Severity = SHRuntimeMessageSeverity.Info }.GetRoleClassForRender());
            Assert.Equal("debug", new AIInteractionRuntimeMessage { Severity = SHRuntimeMessageSeverity.Debug }.GetRoleClassForRender());
        }

        [Fact(DisplayName = nameof(RuntimeMessage_GetDisplayNameForRender) + PlatformSuffix)]
        public void RuntimeMessage_GetDisplayNameForRender()
        {
            Assert.Equal("Error", new AIInteractionRuntimeMessage { Severity = SHRuntimeMessageSeverity.Error }.GetDisplayNameForRender());
            Assert.Equal("Warning", new AIInteractionRuntimeMessage { Severity = SHRuntimeMessageSeverity.Warning }.GetDisplayNameForRender());
            Assert.Equal("Info", new AIInteractionRuntimeMessage { Severity = SHRuntimeMessageSeverity.Info }.GetDisplayNameForRender());
            Assert.Equal("Debug", new AIInteractionRuntimeMessage { Severity = SHRuntimeMessageSeverity.Debug }.GetDisplayNameForRender());
        }

        [Fact(DisplayName = nameof(RuntimeMessage_GetRawContentForRender) + PlatformSuffix)]
        public void RuntimeMessage_GetRawContentForRender()
        {
            var interaction = new AIInteractionRuntimeMessage { Content = "content" };

            Assert.Equal("content", interaction.GetRawContentForRender());

            var emptyContent = new AIInteractionRuntimeMessage();
            Assert.Equal(string.Empty, emptyContent.GetRawContentForRender());
        }

        [Fact(DisplayName = nameof(RuntimeMessage_GetRawReasoningForRender) + PlatformSuffix)]
        public void RuntimeMessage_GetRawReasoningForRender()
        {
            var interaction = new AIInteractionRuntimeMessage();

            Assert.Equal(string.Empty, interaction.GetRawReasoningForRender());
        }

        [Fact(DisplayName = nameof(RuntimeMessage_GetStreamKeyWithoutTurnId) + PlatformSuffix)]
        public void RuntimeMessage_GetStreamKeyWithoutTurnId()
        {
            var interaction = new AIInteractionRuntimeMessage
            {
                Severity = SHRuntimeMessageSeverity.Info,
                Content = "info message",
            };

            var hash = HashUtility.ComputeShortHash("info message");

            Assert.Equal($"info:{hash}", interaction.GetStreamKey());
        }

        [Fact(DisplayName = nameof(RuntimeMessage_GetStreamKeyWithTurnId) + PlatformSuffix)]
        public void RuntimeMessage_GetStreamKeyWithTurnId()
        {
            var interaction = new AIInteractionRuntimeMessage
            {
                TurnId = "turn-1",
                Severity = SHRuntimeMessageSeverity.Warning,
                Content = "warning message",
            };

            var hash = HashUtility.ComputeShortHash("warning message");

            Assert.Equal($"turn:turn-1:warning:{hash}", interaction.GetStreamKey());
        }

        [Fact(DisplayName = nameof(RuntimeMessage_GetDedupKey) + PlatformSuffix)]
        public void RuntimeMessage_GetDedupKey()
        {
            var interaction = new AIInteractionRuntimeMessage
            {
                Severity = SHRuntimeMessageSeverity.Info,
                Content = "info message",
            };

            Assert.Equal(interaction.GetStreamKey(), interaction.GetDedupKey());
        }

        #endregion

        #region AIInteractionAudio

        [Fact(DisplayName = nameof(Audio_GetAudioSizeWithData) + PlatformSuffix)]
        public void Audio_GetAudioSizeWithData()
        {
            var interaction = new AIInteractionAudio
            {
                Data = new byte[] { 1, 2, 3 },
            };

            Assert.Equal(3, interaction.GetAudioSize());
        }

        [Fact(DisplayName = nameof(Audio_GetAudioSizeWithMissingFile) + PlatformSuffix)]
        public void Audio_GetAudioSizeWithMissingFile()
        {
            var interaction = new AIInteractionAudio
            {
                FilePath = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid()}.wav"),
            };

            Assert.Equal(0, interaction.GetAudioSize());
        }

        [Fact(DisplayName = nameof(Audio_GetAudioSizeWithNoData) + PlatformSuffix)]
        public void Audio_GetAudioSizeWithNoData()
        {
            var interaction = new AIInteractionAudio();

            Assert.Equal(0, interaction.GetAudioSize());
        }

        [Fact(DisplayName = nameof(Audio_GetStreamKeyForData) + PlatformSuffix)]
        public void Audio_GetStreamKeyForData()
        {
            var data = new byte[] { 1, 2, 3 };
            var interaction = new AIInteractionAudio
            {
                Data = data,
            };

            var hash = HashUtility.ComputeShortHash(Convert.ToBase64String(data));

            Assert.Equal($"audio:{hash}", interaction.GetStreamKey());
        }

        [Fact(DisplayName = nameof(Audio_GetStreamKeyForFile) + PlatformSuffix)]
        public void Audio_GetStreamKeyForFile()
        {
            var interaction = new AIInteractionAudio
            {
                FilePath = @"C:\audio.wav",
            };

            Assert.Equal("audio:C:\\audio.wav", interaction.GetStreamKey());
        }

        [Fact(DisplayName = nameof(Audio_GetStreamKeyForEmpty) + PlatformSuffix)]
        public void Audio_GetStreamKeyForEmpty()
        {
            var interaction = new AIInteractionAudio();

            Assert.Equal("audio:empty", interaction.GetStreamKey());
        }

        [Fact(DisplayName = nameof(Audio_GetStreamKeyWithTurnId) + PlatformSuffix)]
        public void Audio_GetStreamKeyWithTurnId()
        {
            var interaction = new AIInteractionAudio
            {
                TurnId = "turn-1",
                FilePath = @"C:\audio.wav",
            };

            Assert.Equal("turn:turn-1:audio:C:\\audio.wav", interaction.GetStreamKey());
        }

        [Fact(DisplayName = nameof(Audio_GetDedupKey) + PlatformSuffix)]
        public void Audio_GetDedupKey()
        {
            var interaction = new AIInteractionAudio
            {
                FilePath = @"C:\audio.wav",
                MimeType = "audio/wav",
            };

            Assert.Equal("audio:C:\\audio.wav:audio/wav", interaction.GetDedupKey());

            var unknownMime = new AIInteractionAudio
            {
                FilePath = @"C:\audio.wav",
            };

            Assert.Equal("audio:C:\\audio.wav:unknown", unknownMime.GetDedupKey());
        }

        [Fact(DisplayName = nameof(Audio_ToString) + PlatformSuffix)]
        public void Audio_ToString()
        {
            var inMemory = new AIInteractionAudio
            {
                Data = new byte[] { 1, 2, 3 },
                MimeType = "audio/wav",
                LanguageHint = "en",
            };

            Assert.Equal("Audio(audio/wav, in-memory (3 bytes)) [en]", inMemory.ToString());

            var fromFile = new AIInteractionAudio
            {
                FilePath = @"C:\audio.wav",
                MimeType = "audio/wav",
            };

            Assert.Equal("Audio(audio/wav, C:\\audio.wav)", fromFile.ToString());

            var empty = new AIInteractionAudio { MimeType = "audio/wav" };

            Assert.Equal("Audio(audio/wav, unknown)", empty.ToString());
        }

        #endregion
    }
}
