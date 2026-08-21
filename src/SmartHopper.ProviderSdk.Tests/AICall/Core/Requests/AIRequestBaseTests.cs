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

namespace SmartHopper.ProviderSdk.Tests.AICall.Core.Requests
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using SmartHopper.ProviderSdk.AICall.Core.Base;
    using SmartHopper.ProviderSdk.AICall.Core.Interactions;
    using SmartHopper.ProviderSdk.AICall.Core.Requests;
    using SmartHopper.ProviderSdk.AIModels;
    using SmartHopper.ProviderSdk.Diagnostics;
    using Xunit;

    /// <summary>
    /// Tests for <see cref="AIRequestBase"/>.
    /// </summary>
    [Collection("ProviderSdk")]
    public class AIRequestBaseTests
    {
#if NET7_WINDOWS
        [Fact(DisplayName = "Constructor_DefaultValues [Windows]")]
#else
        [Fact(DisplayName = "Constructor_DefaultValues [Core]")]
#endif
        public void Constructor_DefaultValues()
        {
            var request = new AIRequestBase();

            Assert.Equal(AICapability.None, request.Capability);
            Assert.False(request.WantsStreaming);
            Assert.Equal(AIRequestKind.Generation, request.RequestKind);
            Assert.Equal(AIBody.Empty, request.Body);
            Assert.Equal(string.Empty, request.Model);
            Assert.Null(request.Provider);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "ForceToolCall_ForceToolNameTriggersIt [Windows]")]
#else
        [Fact(DisplayName = "ForceToolCall_ForceToolNameTriggersIt [Core]")]
#endif
        public void ForceToolCall_ForceToolNameTriggersIt()
        {
            var request = new AIRequestBase();

            Assert.False(request.ForceToolCall);
            Assert.True(string.IsNullOrEmpty(request.ForceToolName));

            request.ForceToolName = "gh_get";
            Assert.True(request.ForceToolCall);

            request.ForceToolName = string.Empty;
            Assert.False(request.ForceToolCall);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "ForceToolCall_ExplicitSetterWorks [Windows]")]
#else
        [Fact(DisplayName = "ForceToolCall_ExplicitSetterWorks [Core]")]
#endif
        public void ForceToolCall_ExplicitSetterWorks()
        {
            var request = new AIRequestBase();

            request.ForceToolCall = true;
            Assert.True(request.ForceToolCall);

            request.ForceToolCall = false;
            Assert.False(request.ForceToolCall);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "ForceToolCall_NameTakesPrecedenceOverExplicitFalse [Windows]")]
#else
        [Fact(DisplayName = "ForceToolCall_NameTakesPrecedenceOverExplicitFalse [Core]")]
#endif
        public void ForceToolCall_NameTakesPrecedenceOverExplicitFalse()
        {
            var request = new AIRequestBase();

            request.ForceToolName = "gh_get";
            request.ForceToolCall = false;

            Assert.True(request.ForceToolCall);

            request.ForceToolName = string.Empty;
            Assert.False(request.ForceToolCall);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "Initialize_SetsProviderModelEndpointBodyCapabilityAndParameters [Windows]")]
#else
        [Fact(DisplayName = "Initialize_SetsProviderModelEndpointBodyCapabilityAndParameters [Core]")]
#endif
        public void Initialize_SetsProviderModelEndpointBodyCapabilityAndParameters()
        {
            var body = AIBodyBuilder.Create()
                .AddUser("hello")
                .Build();

            var request = new AIRequestBase();
            request.Initialize(
                provider: null!,
                model: "test-model",
                body: body,
                endpoint: "https://test/endpoint",
                capability: AICapability.None);

            Assert.Null(request.Provider);
            Assert.Equal("test-model", request.Model);
            Assert.Equal("https://test/endpoint", request.Endpoint);
            Assert.Same(body, request.Body);
            Assert.Equal(AICapability.None, request.Capability);
            Assert.NotNull(request.Parameters);
            Assert.Equal("test-model", request.Parameters.Model);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "IsValid_ValidBodyWithTurnIds [Windows]")]
#else
        [Fact(DisplayName = "IsValid_ValidBodyWithTurnIds [Core]")]
#endif
        public void IsValid_ValidBodyWithTurnIds()
        {
            var request = new AIRequestBase();
            request.Body = AIBodyBuilder.Create()
                .AddUser("hello")
                .Build();

            var (isValid, errors) = request.IsValid();

            Assert.True(isValid);
            Assert.Empty(errors);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "IsValid_InvalidBodyMissingTurnId [Windows]")]
#else
        [Fact(DisplayName = "IsValid_InvalidBodyMissingTurnId [Core]")]
#endif
        public void IsValid_InvalidBodyMissingTurnId()
        {
            var request = new AIRequestBase();
            request.Body = new AIBody(
                new List<IAIInteraction>
                {
                    new AIInteractionText { Agent = AIAgent.User, Content = "no turn id" },
                },
                "-*",
                "-*",
                null!,
                new List<int>());

            var (isValid, errors) = request.IsValid();

            Assert.False(isValid);
            Assert.Contains(errors, e => e.Message.Contains("TurnId", StringComparison.Ordinal));
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "Messages_SortsBySeverity [Windows]")]
#else
        [Fact(DisplayName = "Messages_SortsBySeverity [Core]")]
#endif
        public void Messages_SortsBySeverity()
        {
            var request = new AIRequestBase();
            request.Body = AIBodyBuilder.Create()
                .AddUser("hello")
                .Build();
            request.Messages = new List<SHRuntimeMessage>
            {
                new SHRuntimeMessage(SHRuntimeMessageSeverity.Info, SHRuntimeMessageOrigin.Request, SHMessageCode.Unknown, "info"),
                new SHRuntimeMessage(SHRuntimeMessageSeverity.Error, SHRuntimeMessageOrigin.Request, SHMessageCode.Unknown, "error"),
                new SHRuntimeMessage(SHRuntimeMessageSeverity.Warning, SHRuntimeMessageOrigin.Request, SHMessageCode.Unknown, "warning"),
            };

            var messages = request.Messages;

            Assert.Equal(SHRuntimeMessageSeverity.Error, messages[0].Severity);
            Assert.Equal(SHRuntimeMessageSeverity.Warning, messages[1].Severity);
            Assert.Equal(SHRuntimeMessageSeverity.Info, messages[2].Severity);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "Exec_ThrowsNotImplementedException [Windows]")]
#else
        [Fact(DisplayName = "Exec_ThrowsNotImplementedException [Core]")]
#endif
        public async Task Exec_ThrowsNotImplementedException()
        {
            var request = new AIRequestBase();

            var ex = await Assert.ThrowsAsync<NotImplementedException>(() => request.Exec()).ConfigureAwait(false);
            Assert.Contains("Exec()", ex.Message, StringComparison.Ordinal);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "SkipMetricsValidation_CanBeSet [Windows]")]
#else
        [Fact(DisplayName = "SkipMetricsValidation_CanBeSet [Core]")]
#endif
        public void SkipMetricsValidation_CanBeSet()
        {
            var request = new AIRequestBase();

            Assert.False(request.SkipMetricsValidation);

            request.SkipMetricsValidation = true;

            Assert.True(request.SkipMetricsValidation);
        }
    }
}
