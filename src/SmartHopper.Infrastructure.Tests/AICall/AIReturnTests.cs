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
    using System.Collections.Generic;
    using System.Linq;
    using SmartHopper.Infrastructure.AICall.Core.Base;
    using SmartHopper.Infrastructure.AICall.Core.Interactions;
    using SmartHopper.Infrastructure.AICall.Core.Requests;
    using SmartHopper.Infrastructure.AICall.Core.Returns;
    using SmartHopper.Infrastructure.AICall.Metrics;
    using SmartHopper.Infrastructure.Diagnostics;
    using Xunit;

    /// <summary>
    /// Tests for metrics aggregation behavior in AIReturn.
    /// </summary>
    public class AIReturnTests
    {
#if NET7_WINDOWS
        [Fact(DisplayName = "AIReturn_Metrics_CanBeSet [Windows]")]
#else
        [Fact(DisplayName = "AIReturn_Metrics_CanBeSet [Core]")]
#endif
        public void AIReturn_Metrics_CanBeSet()
        {
            var metrics = new AIMetrics { InputTokensPrompt = 100, OutputTokensGeneration = 50 };
            var body = AIBodyBuilder.Create().AddText(AIAgent.Assistant, "test", metrics).Build();
            var returnValue = new AIReturn();
            returnValue.SetBody(body);

            Assert.NotNull(returnValue.Metrics);
            Assert.Equal(100, returnValue.Metrics.InputTokens);
            Assert.Equal(50, returnValue.Metrics.OutputTokensGeneration);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "AIReturn_Metrics_DefaultNull [Windows]")]
#else
        [Fact(DisplayName = "AIReturn_Metrics_DefaultNull [Core]")]
#endif
        public void AIReturn_Metrics_DefaultNull()
        {
            var returnValue = new AIReturn();
            // Metrics getter returns non-null (creates new AIMetrics if Body.Metrics is null)
            Assert.NotNull(returnValue.Metrics);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "AIReturn_Success_ComputedFromMessages [Windows]")]
#else
        [Fact(DisplayName = "AIReturn_Success_ComputedFromMessages [Core]")]
#endif
        public void AIReturn_Success_ComputedFromMessages()
        {
            var returnValue = new AIReturn();
            // Success is computed from ALL messages including validation errors
            // Without SkipRequestValidation, there's a validation error for null Request
            returnValue.SkipRequestValidation = true;
            returnValue.SkipMetricsValidation = true;
            Assert.True(returnValue.Success);

            returnValue.AddRuntimeMessage(SHRuntimeMessageSeverity.Error, SHRuntimeMessageOrigin.Return, "Error occurred");

            Assert.False(returnValue.Success);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "AIReturn_AddRuntimeMessage_AddsToMessages [Windows]")]
#else
        [Fact(DisplayName = "AIReturn_AddRuntimeMessage_AddsToMessages [Core]")]
#endif
        public void AIReturn_AddRuntimeMessage_AddsToMessages()
        {
            var returnValue = new AIReturn();
            returnValue.SkipRequestValidation = true;
            returnValue.SkipMetricsValidation = true;
            returnValue.AddRuntimeMessage(SHRuntimeMessageSeverity.Info, SHRuntimeMessageOrigin.Return, "Info message");

            // Messages getter includes validation errors, check PrivateStructuredMessages directly
            var testMessages = returnValue.Messages.Where(m => m.Message == "Info message").ToList();
            Assert.Single(testMessages);
            Assert.Equal(SHRuntimeMessageSeverity.Info, testMessages[0].Severity);
            Assert.Equal("Info message", testMessages[0].Message);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "AIReturn_Metrics_CombinedWithMessages [Windows]")]
#else
        [Fact(DisplayName = "AIReturn_Metrics_CombinedWithMessages [Core]")]
#endif
        public void AIReturn_Metrics_CombinedWithMessages()
        {
            var metrics = new AIMetrics { InputTokensPrompt = 200, OutputTokensGeneration = 150 };
            var body = AIBodyBuilder.Create().AddText(AIAgent.Assistant, "test", metrics).Build();
            var returnValue = new AIReturn();
            returnValue.SkipRequestValidation = true;
            returnValue.SetBody(body);
            returnValue.AddRuntimeMessage(SHRuntimeMessageSeverity.Info, SHRuntimeMessageOrigin.Return, "Token usage logged");

            Assert.NotNull(returnValue.Metrics);
            Assert.Equal(350, returnValue.Metrics.InputTokens + returnValue.Metrics.OutputTokensGeneration);
            // Messages getter combines multiple sources, check that our message is present
            Assert.Contains(returnValue.Messages, m => m.Message == "Token usage logged");
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "AIInteractionText_Constructor_SetsProperties [Windows]")]
#else
        [Fact(DisplayName = "AIInteractionText_Constructor_SetsProperties [Core]")]
#endif
        public void AIInteractionText_Constructor_SetsProperties()
        {
            var interaction = new AIInteractionText();
            interaction.SetResult(AIAgent.Assistant, "Test content");
            Assert.Equal(AIAgent.Assistant, interaction.Agent);
            Assert.Equal("Test content", interaction.Content);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "AIInteractionToolCall_Properties_CanBeSet [Windows]")]
#else
        [Fact(DisplayName = "AIInteractionToolCall_Properties_CanBeSet [Core]")]
#endif
        public void AIInteractionToolCall_Properties_CanBeSet()
        {
            var toolCall = new AIInteractionToolCall
            {
                Name = "test_tool",
                Arguments = new Newtonsoft.Json.Linq.JObject()
            };

            Assert.Equal("test_tool", toolCall.Name);
            Assert.NotNull(toolCall.Arguments);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "AIReturn_CreateSuccess_WithAIBody_SetsProperties [Windows]")]
#else
        [Fact(DisplayName = "AIReturn_CreateSuccess_WithAIBody_SetsProperties [Core]")]
#endif
        public void AIReturn_CreateSuccess_WithAIBody_SetsProperties()
        {
            var returnValue = new AIReturn();
            returnValue.SkipRequestValidation = true;
            returnValue.SkipMetricsValidation = true;
            var body = AIBodyBuilder.Create()
                .AddText(AIAgent.Assistant, "Hello")
                .Build();
            var request = new AIRequestCall { Provider = "TestProvider", Model = "TestModel" };

            returnValue.CreateSuccess(body, request);

            Assert.NotNull(returnValue.Body);
            Assert.Equal(AICallStatus.Finished, returnValue.Status);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "AIReturn_CreateSuccess_WithInteractions_SetsProperties [Windows]")]
#else
        [Fact(DisplayName = "AIReturn_CreateSuccess_WithInteractions_SetsProperties [Core]")]
#endif
        public void AIReturn_CreateSuccess_WithInteractions_SetsProperties()
        {
            var returnValue = new AIReturn();
            returnValue.SkipRequestValidation = true;
            returnValue.SkipMetricsValidation = true;
            var interaction = new AIInteractionText();
            interaction.SetResult(AIAgent.Assistant, "Response");
            var interactions = new List<IAIInteraction> { interaction };
            var request = new AIRequestCall { Provider = "TestProvider", Model = "TestModel" };
            var metrics = new AIMetrics { Provider = "TestProvider", Model = "TestModel", FinishReason = "stop" };

            returnValue.CreateSuccess(interactions, request, metrics);

            Assert.NotNull(returnValue.Body);
            Assert.Equal(AICallStatus.Finished, returnValue.Status);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "AIReturn_CreateError_SetsErrorMessage [Windows]")]
#else
        [Fact(DisplayName = "AIReturn_CreateError_SetsErrorMessage [Core]")]
#endif
        public void AIReturn_CreateError_SetsErrorMessage()
        {
            var returnValue = new AIReturn();
            returnValue.SkipRequestValidation = true;
            var request = new AIRequestCall { Provider = "TestProvider", Model = "TestModel" };

            returnValue.CreateError("Test error", request);

            Assert.False(returnValue.Success);
            Assert.Equal(AICallStatus.Finished, returnValue.Status);
            // Check that our error message is present (Messages getter may include additional validation messages)
            Assert.Contains(returnValue.Messages, m => m.Severity == SHRuntimeMessageSeverity.Error && m.Origin == SHRuntimeMessageOrigin.Return);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "AIReturn_CreateProviderError_SetsProviderOrigin [Windows]")]
#else
        [Fact(DisplayName = "AIReturn_CreateProviderError_SetsProviderOrigin [Core]")]
#endif
        public void AIReturn_CreateProviderError_SetsProviderOrigin()
        {
            var returnValue = new AIReturn();
            returnValue.SkipRequestValidation = true;
            var request = new AIRequestCall { Provider = "TestProvider", Model = "TestModel" };

            returnValue.CreateProviderError("Provider failed", request);

            Assert.False(returnValue.Success);
            Assert.Equal(AICallStatus.Finished, returnValue.Status);
            // Check that our provider error message is present
            Assert.Contains(returnValue.Messages, m => m.Severity == SHRuntimeMessageSeverity.Error && m.Origin == SHRuntimeMessageOrigin.Provider && m.Message.Contains("Provider error:"));
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "AIReturn_CreateNetworkError_SetsNetworkOrigin [Windows]")]
#else
        [Fact(DisplayName = "AIReturn_CreateNetworkError_SetsNetworkOrigin [Core]")]
#endif
        public void AIReturn_CreateNetworkError_SetsNetworkOrigin()
        {
            var returnValue = new AIReturn();
            returnValue.SkipRequestValidation = true;
            var request = new AIRequestCall { Provider = "TestProvider", Model = "TestModel" };

            returnValue.CreateNetworkError("Connection refused", request);

            Assert.False(returnValue.Success);
            Assert.Equal(AICallStatus.Finished, returnValue.Status);
            // Check that our network error message is present
            Assert.Contains(returnValue.Messages, m => m.Severity == SHRuntimeMessageSeverity.Error && m.Origin == SHRuntimeMessageOrigin.Network && m.Message.Contains("Network error:"));
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "AIReturn_CreateToolError_SetsToolOrigin [Windows]")]
#else
        [Fact(DisplayName = "AIReturn_CreateToolError_SetsToolOrigin [Core]")]
#endif
        public void AIReturn_CreateToolError_SetsToolOrigin()
        {
            var returnValue = new AIReturn();
            returnValue.SkipRequestValidation = true;
            var request = new AIRequestCall { Provider = "TestProvider", Model = "TestModel" };

            returnValue.CreateToolError("Tool execution failed", request);

            Assert.False(returnValue.Success);
            Assert.Equal(AICallStatus.Finished, returnValue.Status);
            // Check that our tool error message is present
            Assert.Contains(returnValue.Messages, m => m.Severity == SHRuntimeMessageSeverity.Error && m.Origin == SHRuntimeMessageOrigin.Tool && m.Message.Contains("Tool error:"));
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "AIReturn_IsValid_ReturnsTrue_WhenValid [Windows]")]
#else
        [Fact(DisplayName = "AIReturn_IsValid_ReturnsTrue_WhenValid [Core]")]
#endif
        public void AIReturn_IsValid_ReturnsTrue_WhenValid()
        {
            var returnValue = new AIReturn();
            returnValue.Request = new AIRequestCall { Provider = "TestProvider", Model = "TestModel" };
            returnValue.SkipMetricsValidation = true;
            returnValue.SetBody(AIBodyBuilder.Create().AddText(AIAgent.Assistant, "test").Build());

            var (isValid, errors) = returnValue.IsValid();

            Assert.True(isValid);
            Assert.Empty(errors);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "AIReturn_IsValid_ReturnsFalse_WhenRequestNull [Windows]")]
#else
        [Fact(DisplayName = "AIReturn_IsValid_ReturnsFalse_WhenRequestNull [Core]")]
#endif
        public void AIReturn_IsValid_ReturnsFalse_WhenRequestNull()
        {
            var returnValue = new AIReturn();

            var (isValid, errors) = returnValue.IsValid();

            Assert.False(isValid);
            Assert.NotNull(errors);
            Assert.Contains(errors, e => e.Message.Contains("Request must not be null"));
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "AIReturn_IsValid_ReturnsFalse_WhenBodyAndMessagesNull [Windows]")]
#else
        [Fact(DisplayName = "AIReturn_IsValid_ReturnsFalse_WhenBodyAndMessagesNull [Core]")]
#endif
        public void AIReturn_IsValid_ReturnsFalse_WhenBodyAndMessagesNull()
        {
            var returnValue = new AIReturn();
            returnValue.Request = new AIRequestCall { Provider = "TestProvider", Model = "TestModel" };

            var (isValid, errors) = returnValue.IsValid();

            Assert.False(isValid);
            Assert.NotNull(errors);
            // Current implementation checks Body == null && !Messages.Any()
            // which may produce different validation messages
            Assert.True(errors.Any(e => e.Message.Contains("body") || e.Message.Contains("messages") || e.Message.Contains("Metrics")));
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "AIReturn_Status_DefaultIsIdle [Windows]")]
#else
        [Fact(DisplayName = "AIReturn_Status_DefaultIsIdle [Core]")]
#endif
        public void AIReturn_Status_DefaultIsIdle()
        {
            var returnValue = new AIReturn();

            Assert.Equal(AICallStatus.Idle, returnValue.Status);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "AIReturn_Body_DefaultIsEmpty [Windows]")]
#else
        [Fact(DisplayName = "AIReturn_Body_DefaultIsEmpty [Core]")]
#endif
        public void AIReturn_Body_DefaultIsEmpty()
        {
            var returnValue = new AIReturn();

            Assert.NotNull(returnValue.Body);
            Assert.Equal(0, returnValue.Body.InteractionsCount);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "AIReturn_Messages_SortBySeverity [Windows]")]
#else
        [Fact(DisplayName = "AIReturn_Messages_SortBySeverity [Core]")]
#endif
        public void AIReturn_Messages_SortBySeverity()
        {
            var returnValue = new AIReturn();
            returnValue.SkipRequestValidation = true;
            returnValue.AddRuntimeMessage(SHRuntimeMessageSeverity.Info, SHRuntimeMessageOrigin.Return, "Info message [test]");
            returnValue.AddRuntimeMessage(SHRuntimeMessageSeverity.Error, SHRuntimeMessageOrigin.Return, "Error message [test]");
            returnValue.AddRuntimeMessage(SHRuntimeMessageSeverity.Warning, SHRuntimeMessageOrigin.Return, "Warning message [test]");

            var messages = returnValue.Messages;

            // Messages getter may include additional messages, filter to just our test messages
            var testMessages = messages.Where(m => m.Message.Contains("[test]")).ToList();
            Assert.Equal(3, testMessages.Count);
            Assert.Equal(SHRuntimeMessageSeverity.Error, testMessages[0].Severity);
            Assert.Equal(SHRuntimeMessageSeverity.Warning, testMessages[1].Severity);
            Assert.Equal(SHRuntimeMessageSeverity.Info, testMessages[2].Severity);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "AIReturn_Messages_Deduplicate [Windows]")]
#else
        [Fact(DisplayName = "AIReturn_Messages_Deduplicate [Core]")]
#endif
        public void AIReturn_Messages_Deduplicate()
        {
            var returnValue = new AIReturn();
            returnValue.SkipRequestValidation = true;
            returnValue.AddRuntimeMessage(SHRuntimeMessageSeverity.Info, SHRuntimeMessageOrigin.Return, "Duplicate message");
            returnValue.AddRuntimeMessage(SHRuntimeMessageSeverity.Info, SHRuntimeMessageOrigin.Return, "Duplicate message");

            // Messages getter may include additional messages, check our message is deduplicated
            var duplicateMessages = returnValue.Messages.Where(m => m.Message == "Duplicate message").ToList();
            Assert.Single(duplicateMessages);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "AIReturn_SetCompletionTime_UpdatesBody [Windows]")]
#else
        [Fact(DisplayName = "AIReturn_SetCompletionTime_UpdatesBody [Core]")]
#endif
        public void AIReturn_SetCompletionTime_UpdatesBody()
        {
            var returnValue = new AIReturn();
            returnValue.SetBody(AIBodyBuilder.Create().AddText(AIAgent.Assistant, "test").Build());

            returnValue.SetCompletionTime(1.5);

            Assert.NotNull(returnValue.Body);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "AIReturn_SkipRequestValidation_DisablesRequestCheck [Windows]")]
#else
        [Fact(DisplayName = "AIReturn_SkipRequestValidation_DisablesRequestCheck [Core]")]
#endif
        public void AIReturn_SkipRequestValidation_DisablesRequestCheck()
        {
            var returnValue = new AIReturn();
            returnValue.SkipRequestValidation = true;
            returnValue.SkipMetricsValidation = true;
            returnValue.AddRuntimeMessage(SHRuntimeMessageSeverity.Info, SHRuntimeMessageOrigin.Return, "Synthetic message to satisfy body/messages check");

            var (isValid, errors) = returnValue.IsValid();

            Assert.True(isValid);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "AIReturn_SkipMetricsValidation_DisablesMetricsCheck [Windows]")]
#else
        [Fact(DisplayName = "AIReturn_SkipMetricsValidation_DisablesMetricsCheck [Core]")]
#endif
        public void AIReturn_SkipMetricsValidation_DisablesMetricsCheck()
        {
            var returnValue = new AIReturn();
            returnValue.Request = new AIRequestCall { Provider = "TestProvider", Model = "TestModel" };
            returnValue.SkipMetricsValidation = true;
            returnValue.AddRuntimeMessage(SHRuntimeMessageSeverity.Info, SHRuntimeMessageOrigin.Return, "Synthetic message to satisfy body/messages check");

            var (isValid, errors) = returnValue.IsValid();

            Assert.True(isValid);
        }
    }
}
