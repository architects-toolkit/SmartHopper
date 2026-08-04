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
    using System.Collections.Generic;
    using Newtonsoft.Json.Linq;
    using SmartHopper.ProviderSdk.AICall.Core.Base;
    using SmartHopper.ProviderSdk.AICall.Core.Interactions;
    using SmartHopper.ProviderSdk.AICall.Metrics;
    using SmartHopper.ProviderSdk.Diagnostics;
    using Xunit;

    /// <summary>
    /// Tests for <see cref="AIBody"/> validation, messages and metrics aggregation.
    /// </summary>
    [Collection("ProviderSdk")]
    public class AIBodyValidationTests
    {
#if NET7_WINDOWS
        [Fact(DisplayName = "Empty_Body_HasExpectedDefaults [Windows]")]
#else
        [Fact(DisplayName = "Empty_Body_HasExpectedDefaults [Core]")]
#endif
        public void Empty_Body_HasExpectedDefaults()
        {
            var body = AIBody.Empty;

            Assert.Equal(0, body.InteractionsCount);
            Assert.Equal("-*", body.ToolFilter);
            Assert.Equal("-*", body.ContextFilter);
            Assert.Null(body.JsonOutputSchema);
            Assert.False(body.RequiresJsonOutput);
            Assert.Empty(body.InteractionsNew);
            Assert.True(body.AreTurnIdsValid());
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "InteractionsCount_ReflectsInteractionCount [Windows]")]
#else
        [Fact(DisplayName = "InteractionsCount_ReflectsInteractionCount [Core]")]
#endif
        public void InteractionsCount_ReflectsInteractionCount()
        {
            var body = AIBodyBuilder.Create()
                .AddUser("u")
                .AddAssistant("a")
                .Build();

            Assert.Equal(2, body.InteractionsCount);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "RequiresJsonOutput_TrueWhenSchemaPresent [Windows]")]
#else
        [Fact(DisplayName = "RequiresJsonOutput_TrueWhenSchemaPresent [Core]")]
#endif
        public void RequiresJsonOutput_TrueWhenSchemaPresent()
        {
            var withSchema = AIBodyBuilder.Create()
                .WithJsonOutputSchema("{\"type\":\"object\"}")
                .Build();

            Assert.True(withSchema.RequiresJsonOutput);

            var withoutSchema = AIBodyBuilder.Create().Build();
            Assert.False(withoutSchema.RequiresJsonOutput);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "AreTurnIdsValid_True_WhenAllHaveTurnId [Windows]")]
#else
        [Fact(DisplayName = "AreTurnIdsValid_True_WhenAllHaveTurnId [Core]")]
#endif
        public void AreTurnIdsValid_True_WhenAllHaveTurnId()
        {
            var body = AIBodyBuilder.Create()
                .AddUser("u")
                .AddAssistant("a")
                .Build();

            Assert.True(body.AreTurnIdsValid());
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "AreTurnIdsValid_False_WhenMissingTurnId [Windows]")]
#else
        [Fact(DisplayName = "AreTurnIdsValid_False_WhenMissingTurnId [Core]")]
#endif
        public void AreTurnIdsValid_False_WhenMissingTurnId()
        {
            var text = new AIInteractionText
            {
                Agent = AIAgent.User,
                Content = "u",
                TurnId = null!,
            };

            var body = new AIBody(
                new List<IAIInteraction> { text },
                "-*",
                "-*",
                null!,
                new List<int>());

            Assert.False(body.AreTurnIdsValid());
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "ResetNew_ClearsNewMarkers [Windows]")]
#else
        [Fact(DisplayName = "ResetNew_ClearsNewMarkers [Core]")]
#endif
        public void ResetNew_ClearsNewMarkers()
        {
            var body = AIBodyBuilder.Create()
                .AddUser("u")
                .Build();

            Assert.Single(body.InteractionsNew);

            body.ResetNew();

            Assert.Empty(body.InteractionsNew);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "Messages_AggregatesFromToolResultImageAndRuntimeMessage [Windows]")]
#else
        [Fact(DisplayName = "Messages_AggregatesFromToolResultImageAndRuntimeMessage [Core]")]
#endif
        public void Messages_AggregatesFromToolResultImageAndRuntimeMessage()
        {
            var toolResult = new AIInteractionToolResult
            {
                Id = "call-1",
                Name = "test_tool",
                Result = new JObject(new JProperty("ok", true)),
                Messages = new List<SHRuntimeMessage>
                {
                    new SHRuntimeMessage(SHRuntimeMessageSeverity.Info, SHRuntimeMessageOrigin.Provider, SHMessageCode.Unknown, "tool message"),
                },
            };

            var image = new AIInteractionImage
            {
                Agent = AIAgent.User,
                OriginalPrompt = "a red cube",
                Messages = new List<SHRuntimeMessage>
                {
                    new SHRuntimeMessage(SHRuntimeMessageSeverity.Warning, SHRuntimeMessageOrigin.Provider, SHMessageCode.Unknown, "image message"),
                },
            };

            var runtime = new AIInteractionRuntimeMessage
            {
                Severity = SHRuntimeMessageSeverity.Info,
                Content = "runtime message",
            };

            var body = new AIBody(
                new List<IAIInteraction> { toolResult, image, runtime },
                "-*",
                "-*",
                null!,
                new List<int>());

            var messages = body.Messages;

            Assert.Equal(3, messages.Count);
            Assert.Contains(messages, m => m.Message == "tool message");
            Assert.Contains(messages, m => m.Message == "image message");
            Assert.Contains(messages, m => m.Message == "runtime message");
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "Messages_DeduplicatesByMessageText [Windows]")]
#else
        [Fact(DisplayName = "Messages_DeduplicatesByMessageText [Core]")]
#endif
        public void Messages_DeduplicatesByMessageText()
        {
            var toolResult = new AIInteractionToolResult
            {
                Result = new JObject(),
                Messages = new List<SHRuntimeMessage>
                {
                    new SHRuntimeMessage(SHRuntimeMessageSeverity.Info, SHRuntimeMessageOrigin.Provider, SHMessageCode.Unknown, "duplicate"),
                },
            };

            var image = new AIInteractionImage
            {
                Agent = AIAgent.User,
                Messages = new List<SHRuntimeMessage>
                {
                    new SHRuntimeMessage(SHRuntimeMessageSeverity.Warning, SHRuntimeMessageOrigin.Provider, SHMessageCode.Unknown, "duplicate"),
                },
            };

            var runtime = new AIInteractionRuntimeMessage
            {
                Severity = SHRuntimeMessageSeverity.Info,
                Content = "duplicate",
            };

            var body = new AIBody(
                new List<IAIInteraction> { toolResult, image, runtime },
                "-*",
                "-*",
                null!,
                new List<int>());

            var messages = body.Messages;

            Assert.Single(messages);
            Assert.Equal("duplicate", messages[0].Message);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "Metrics_AggregatesAcrossInteractions [Windows]")]
#else
        [Fact(DisplayName = "Metrics_AggregatesAcrossInteractions [Core]")]
#endif
        public void Metrics_AggregatesAcrossInteractions()
        {
            var toolResult = new AIInteractionToolResult
            {
                Result = new JObject(),
                Metrics = new AIMetrics
                {
                    Provider = "openai",
                    Model = "gpt-test",
                    InputTokensPrompt = 10,
                    OutputTokensGeneration = 5,
                },
            };

            var image = new AIInteractionImage
            {
                Agent = AIAgent.User,
                Metrics = new AIMetrics
                {
                    InputTokensPrompt = 5,
                    OutputTokensReasoning = 2,
                },
            };

            var text = new AIInteractionText
            {
                Agent = AIAgent.User,
                Content = "u",
                Metrics = new AIMetrics
                {
                    InputTokensCached = 3,
                },
            };

            var body = new AIBody(
                new List<IAIInteraction> { toolResult, image, text },
                "-*",
                "-*",
                null!,
                new List<int>());

            Assert.Equal(15, body.Metrics.InputTokensPrompt);
            Assert.Equal(3, body.Metrics.InputTokensCached);
            Assert.Equal(18, body.Metrics.InputTokens);
            Assert.Equal(5, body.Metrics.OutputTokensGeneration);
            Assert.Equal(2, body.Metrics.OutputTokensReasoning);
            Assert.Equal(7, body.Metrics.OutputTokens);
            Assert.Equal(25, body.Metrics.TotalTokens);
            Assert.Equal("openai", body.Metrics.Provider);
            Assert.Equal("gpt-test", body.Metrics.Model);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "Metrics_IterationsCount_DefaultsToOne [Windows]")]
#else
        [Fact(DisplayName = "Metrics_IterationsCount_DefaultsToOne [Core]")]
#endif
        public void Metrics_IterationsCount_DefaultsToOne()
        {
            var body = new AIBody(
                new List<IAIInteraction>
                {
                    new AIInteractionText { Agent = AIAgent.User, Content = "u" },
                    new AIInteractionText { Agent = AIAgent.Assistant, Content = "a" },
                },
                "-*",
                "-*",
                null!,
                new List<int>());

            Assert.Equal(1, body.Metrics.IterationsCount);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "Metrics_IterationsCount_AggregatesExplicitValues [Windows]")]
#else
        [Fact(DisplayName = "Metrics_IterationsCount_AggregatesExplicitValues [Core]")]
#endif
        public void Metrics_IterationsCount_AggregatesExplicitValues()
        {
            var first = new AIInteractionText
            {
                Agent = AIAgent.User,
                Content = "u",
                Metrics = new AIMetrics { IterationsCount = 2 },
            };

            var second = new AIInteractionText
            {
                Agent = AIAgent.Assistant,
                Content = "a",
                Metrics = new AIMetrics { IterationsCount = 3 },
            };

            var body = new AIBody(
                new List<IAIInteraction> { first, second },
                "-*",
                "-*",
                null!,
                new List<int>());

            Assert.Equal(5, body.Metrics.IterationsCount);
        }
    }
}
