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

namespace SmartHopper.ProviderSdk.Tests.AICall.Core.Returns
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using SmartHopper.ProviderSdk.AICall.Core.Base;
    using SmartHopper.ProviderSdk.AICall.Core.Interactions;
    using SmartHopper.ProviderSdk.AICall.Core.Requests;
    using SmartHopper.ProviderSdk.AICall.Core.Returns;
    using SmartHopper.ProviderSdk.AICall.Metrics;
    using SmartHopper.ProviderSdk.AIModels;
    using SmartHopper.ProviderSdk.Diagnostics;
    using Xunit;

    /// <summary>
    /// Tests for <see cref="AIReturn"/>.
    /// </summary>
    [Collection("ProviderSdk")]
    public class AIReturnTests
    {
#if NET7_WINDOWS
        [Fact(DisplayName = "Constructor_DefaultValues [Windows]")]
#else
        [Fact(DisplayName = "Constructor_DefaultValues [Core]")]
#endif
        public void Constructor_DefaultValues()
        {
            var ret = new AIReturn();

            Assert.Equal(AICallStatus.Idle, ret.Status);
            Assert.Equal(AIBody.Empty, ret.Body);
            Assert.Null(ret.Request);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "SetBody_AIBody_AssignsBody [Windows]")]
#else
        [Fact(DisplayName = "SetBody_AIBody_AssignsBody [Core]")]
#endif
        public void SetBody_AIBody_AssignsBody()
        {
            var body = AIBodyBuilder.Create()
                .AddText(AIAgent.Assistant, "hello")
                .Build();

            var ret = new AIReturn();
            ret.SetBody(body);

            Assert.Equal(body, ret.Body);
            Assert.NotEqual(AIBody.Empty, ret.Body);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "SetBody_Interactions_BuildsBody [Windows]")]
#else
        [Fact(DisplayName = "SetBody_Interactions_BuildsBody [Core]")]
#endif
        public void SetBody_Interactions_BuildsBody()
        {
            var interactions = new List<IAIInteraction>
            {
                new AIInteractionText { Agent = AIAgent.Assistant, Content = "hello" },
            };

            var ret = new AIReturn();
            ret.SetBody(interactions);

            Assert.NotEqual(AIBody.Empty, ret.Body);
            Assert.Equal(1, ret.Body.InteractionsCount);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "AddRuntimeMessage_AddsToMessages [Windows]")]
#else
        [Fact(DisplayName = "AddRuntimeMessage_AddsToMessages [Core]")]
#endif
        public void AddRuntimeMessage_AddsToMessages()
        {
            var ret = new AIReturn
            {
                SkipRequestValidation = true,
                SkipMetricsValidation = true,
            };

            ret.AddRuntimeMessage(SHRuntimeMessageSeverity.Warning, SHRuntimeMessageOrigin.Return, "a warning");

            Assert.Single(ret.Messages);
            Assert.Contains("a warning", ret.Messages[0].Message, StringComparison.Ordinal);
            Assert.Equal(SHRuntimeMessageSeverity.Warning, ret.Messages[0].Severity);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "AddRuntimeMessage_Error_MakesSuccessFalse [Windows]")]
#else
        [Fact(DisplayName = "AddRuntimeMessage_Error_MakesSuccessFalse [Core]")]
#endif
        public void AddRuntimeMessage_Error_MakesSuccessFalse()
        {
            var ret = new AIReturn
            {
                SkipRequestValidation = true,
                SkipMetricsValidation = true,
            };

            ret.AddRuntimeMessage(SHRuntimeMessageSeverity.Error, SHRuntimeMessageOrigin.Return, "an error");

            Assert.False(ret.Success);
            Assert.Contains(ret.Messages, m => m.Severity == SHRuntimeMessageSeverity.Error);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "AddRuntimeMessage_Warning_KeepsSuccessTrue [Windows]")]
#else
        [Fact(DisplayName = "AddRuntimeMessage_Warning_KeepsSuccessTrue [Core]")]
#endif
        public void AddRuntimeMessage_Warning_KeepsSuccessTrue()
        {
            var ret = new AIReturn
            {
                SkipRequestValidation = true,
                SkipMetricsValidation = true,
            };

            ret.AddRuntimeMessage(SHRuntimeMessageSeverity.Warning, SHRuntimeMessageOrigin.Return, "a warning");

            Assert.True(ret.Success);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "Messages_IncludesBodyMessages [Windows]")]
#else
        [Fact(DisplayName = "Messages_IncludesBodyMessages [Core]")]
#endif
        public void Messages_IncludesBodyMessages()
        {
            var image = new AIInteractionImage
            {
                Agent = AIAgent.User,
                OriginalPrompt = "test image",
                Messages = new List<SHRuntimeMessage>
                {
                    new SHRuntimeMessage(SHRuntimeMessageSeverity.Warning, SHRuntimeMessageOrigin.Tool, SHMessageCode.Unknown, "body warning"),
                },
            };

            var body = AIBodyBuilder.Create()
                .Add(image)
                .Build();

            var ret = new AIReturn
            {
                Request = CreateFakeRequest(),
                SkipRequestValidation = true,
                SkipMetricsValidation = true,
            };
            ret.SetBody(body);

            Assert.Contains(ret.Messages, m => m.Message == "body warning" && m.Severity == SHRuntimeMessageSeverity.Warning);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "Messages_IncludesRequestMessages [Windows]")]
#else
        [Fact(DisplayName = "Messages_IncludesRequestMessages [Core]")]
#endif
        public void Messages_IncludesRequestMessages()
        {
            var request = CreateFakeRequest();
            request.Messages = new List<SHRuntimeMessage>
            {
                new SHRuntimeMessage(SHRuntimeMessageSeverity.Info, SHRuntimeMessageOrigin.Request, SHMessageCode.Unknown, "request info"),
            };

            var body = AIBodyBuilder.Create()
                .AddText(AIAgent.Assistant, "hello", new AIMetrics { Provider = "openai", Model = "gpt-test", FinishReason = "stop" })
                .Build();

            var ret = new AIReturn
            {
                Request = request,
                SkipRequestValidation = true,
                SkipMetricsValidation = true,
            };
            ret.SetBody(body);

            Assert.Contains(ret.Messages, m => m.Message == "request info" && m.Severity == SHRuntimeMessageSeverity.Info);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "Messages_IncludesValidationErrors [Windows]")]
#else
        [Fact(DisplayName = "Messages_IncludesValidationErrors [Core]")]
#endif
        public void Messages_IncludesValidationErrors()
        {
            var interaction = new AIInteractionText { Agent = AIAgent.User, Content = "missing turn id" };
            var invalidBody = new AIBody(
                new List<IAIInteraction> { interaction },
                "-*",
                "-*",
                null!,
                new List<int>());

            var request = CreateFakeRequest();
            request.Body = invalidBody;

            var body = AIBodyBuilder.Create()
                .AddText(AIAgent.Assistant, "hello", new AIMetrics { Provider = "openai", Model = "gpt-test", FinishReason = "stop" })
                .Build();

            var ret = new AIReturn
            {
                Request = request,
            };
            ret.SetBody(body);

            Assert.Contains(ret.Messages, m => m.Message.Contains("TurnId", StringComparison.Ordinal));
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "Messages_SortsBySeverity [Windows]")]
#else
        [Fact(DisplayName = "Messages_SortsBySeverity [Core]")]
#endif
        public void Messages_SortsBySeverity()
        {
            var image = new AIInteractionImage
            {
                Agent = AIAgent.User,
                OriginalPrompt = "test image",
                Messages = new List<SHRuntimeMessage>
                {
                    new SHRuntimeMessage(SHRuntimeMessageSeverity.Warning, SHRuntimeMessageOrigin.Tool, SHMessageCode.Unknown, "body warning"),
                },
            };

            var body = AIBodyBuilder.Create()
                .Add(image)
                .Build();

            var request = CreateFakeRequest();
            request.Messages = new List<SHRuntimeMessage>
            {
                new SHRuntimeMessage(SHRuntimeMessageSeverity.Info, SHRuntimeMessageOrigin.Request, SHMessageCode.Unknown, "request info"),
            };

            var ret = new AIReturn
            {
                Request = request,
                SkipRequestValidation = true,
                SkipMetricsValidation = true,
            };
            ret.SetBody(body);
            ret.AddRuntimeMessage(SHRuntimeMessageSeverity.Error, SHRuntimeMessageOrigin.Return, "private error");

            var messages = ret.Messages;

            Assert.Equal(SHRuntimeMessageSeverity.Error, messages[0].Severity);
            Assert.Equal(SHRuntimeMessageSeverity.Warning, messages[1].Severity);
            Assert.Equal(SHRuntimeMessageSeverity.Info, messages[2].Severity);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "IsValid_ValidWithNonEmptyBodyAndSkipFlags [Windows]")]
#else
        [Fact(DisplayName = "IsValid_ValidWithNonEmptyBodyAndSkipFlags [Core]")]
#endif
        public void IsValid_ValidWithNonEmptyBodyAndSkipFlags()
        {
            var body = AIBodyBuilder.Create()
                .AddText(AIAgent.Assistant, "hello")
                .Build();

            var ret = new AIReturn
            {
                SkipRequestValidation = true,
                SkipMetricsValidation = true,
            };
            ret.SetBody(body);

            var (isValid, _) = ret.IsValid();

            Assert.True(isValid);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "IsValid_ValidWithNonEmptyBodyAndValidMetrics [Windows]")]
#else
        [Fact(DisplayName = "IsValid_ValidWithNonEmptyBodyAndValidMetrics [Core]")]
#endif
        public void IsValid_ValidWithNonEmptyBodyAndValidMetrics()
        {
            var body = AIBodyBuilder.Create()
                .AddText(
                    AIAgent.Assistant,
                    "hello",
                    new AIMetrics
                    {
                        Provider = "openai",
                        Model = "gpt-test",
                        FinishReason = "stop",
                        InputTokensPrompt = 1,
                        OutputTokensGeneration = 1,
                    })
                .Build();

            var ret = new AIReturn
            {
                Request = CreateFakeRequest(),
            };
            ret.SetBody(body);

            var (isValid, _) = ret.IsValid();

            Assert.True(isValid);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "IsValid_InvalidWhenBodyEmptyAndNoPrivateMessages [Windows]")]
#else
        [Fact(DisplayName = "IsValid_InvalidWhenBodyEmptyAndNoPrivateMessages [Core]")]
#endif
        public void IsValid_InvalidWhenBodyEmptyAndNoPrivateMessages()
        {
            var ret = new AIReturn
            {
                Request = CreateFakeRequest(),
                SkipRequestValidation = true,
                SkipMetricsValidation = true,
            };

            var (isValid, errors) = ret.IsValid();

            Assert.False(isValid);
            Assert.Contains(errors, e => e.Message.Contains("Either body or messages must be set", StringComparison.Ordinal));
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "CreateSuccess_SetsStatusBodyAndMetrics [Windows]")]
#else
        [Fact(DisplayName = "CreateSuccess_SetsStatusBodyAndMetrics [Core]")]
#endif
        public void CreateSuccess_SetsStatusBodyAndMetrics()
        {
            var body = AIBodyBuilder.Create()
                .AddText(AIAgent.Assistant, "hello")
                .Build();

            var request = CreateFakeRequest();
            request.Provider = "test-provider";

            var ret = new AIReturn();
            ret.CreateSuccess(body, request);

            Assert.Equal(AICallStatus.Finished, ret.Status);
            Assert.NotEqual(AIBody.Empty, ret.Body);
            Assert.Equal(1, ret.Body.InteractionsCount);
            Assert.Equal("test-provider", ret.Body.Interactions[0].Metrics.Provider);
            Assert.Equal("test-model", ret.Body.Interactions[0].Metrics.Model);
            Assert.Equal("test-provider", ret.Metrics.Provider);
            Assert.Equal("test-model", ret.Metrics.Model);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "CreateError_AddsErrorAndSetsStatusFinished [Windows]")]
#else
        [Fact(DisplayName = "CreateError_AddsErrorAndSetsStatusFinished [Core]")]
#endif
        public void CreateError_AddsErrorAndSetsStatusFinished()
        {
            var ret = new AIReturn();
            ret.CreateError("something went wrong", CreateFakeRequest());

            ret.Request = CreateFakeRequest();
            ret.SkipRequestValidation = true;
            ret.SkipMetricsValidation = true;

            Assert.Equal(AICallStatus.Finished, ret.Status);
            Assert.False(ret.Success);
            Assert.Contains(ret.Messages, m => m.Severity == SHRuntimeMessageSeverity.Error && m.Message.Contains("something went wrong", StringComparison.Ordinal));
        }

        private static AIRequestBase CreateFakeRequest()
        {
            return new AIRequestBase
            {
                Capability = AICapability.None,
                Provider = null,
                WantsStreaming = false,
                Body = AIBody.Empty,
                Model = "test-model",
            };
        }
    }
}
