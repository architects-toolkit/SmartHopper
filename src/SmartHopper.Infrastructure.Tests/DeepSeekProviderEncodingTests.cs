#if NET7_WINDOWS

namespace SmartHopper.Infrastructure.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Newtonsoft.Json.Linq;
    using SmartHopper.Infrastructure.AICall.Core.Base;
    using SmartHopper.Infrastructure.AICall.Core.Interactions;
    using SmartHopper.Infrastructure.AICall.Core.Requests;
    using SmartHopper.Infrastructure.AIModels;
    using SmartHopper.Providers.DeepSeek;
    using Xunit;

    public class DeepSeekProviderEncodingTests
    {
        [Fact]
        public void Encode_CompleteToolSequence_PreservesCallsAndResults()
        {
            var messages = Encode(
                new AIInteractionToolCall { Id = "call_1", Name = "tool_a", Arguments = new JObject() },
                new AIInteractionToolCall { Id = "call_2", Name = "tool_b", Arguments = new JObject() },
                new AIInteractionToolResult { Id = "call_1", Result = new JObject { ["result"] = "a" } },
                new AIInteractionToolResult { Id = "call_2", Result = new JObject { ["result"] = "b" } });

            AssertValidToolSequences(messages);
            Assert.Equal(2, messages.OfType<JObject>().Count(IsToolMessage));
        }

        [Fact]
        public void Encode_DanglingToolCall_RemovesIncompleteSequence()
        {
            var messages = Encode(
                new AIInteractionToolCall { Id = "call_1", Name = "tool_a", Arguments = new JObject() },
                new AIInteractionText { Agent = AIAgent.User, Content = "Continue without the tool." });

            AssertValidToolSequences(messages);
            Assert.DoesNotContain(messages.OfType<JObject>(), HasToolCalls);
        }

        [Fact]
        public void Encode_PartiallyAnsweredToolCalls_RemovesIncompleteSequence()
        {
            var messages = Encode(
                new AIInteractionToolCall { Id = "call_1", Name = "tool_a", Arguments = new JObject() },
                new AIInteractionToolCall { Id = "call_2", Name = "tool_b", Arguments = new JObject() },
                new AIInteractionToolResult { Id = "call_1", Result = new JObject { ["result"] = "a" } },
                new AIInteractionText { Agent = AIAgent.User, Content = "Continue." });

            AssertValidToolSequences(messages);
            Assert.DoesNotContain(messages.OfType<JObject>(), HasToolCalls);
            Assert.DoesNotContain(messages.OfType<JObject>(), IsToolMessage);
        }

        [Fact]
        public void Encode_ToolResultWithoutId_RemovesIncompleteSequence()
        {
            var messages = Encode(
                new AIInteractionToolCall { Id = "call_1", Name = "tool_a", Arguments = new JObject() },
                new AIInteractionToolResult { Result = new JObject { ["result"] = "a" } });

            AssertValidToolSequences(messages);
            Assert.DoesNotContain(messages.OfType<JObject>(), HasToolCalls);
            Assert.DoesNotContain(messages.OfType<JObject>(), IsToolMessage);
        }

        [Fact]
        public void Encode_ToolCallWithoutId_RemovesIncompleteSequence()
        {
            var messages = Encode(
                new AIInteractionToolCall { Name = "tool_a", Arguments = new JObject() },
                new AIInteractionToolResult { Result = new JObject { ["result"] = "a" } });

            AssertValidToolSequences(messages);
            Assert.DoesNotContain(messages.OfType<JObject>(), HasToolCalls);
            Assert.DoesNotContain(messages.OfType<JObject>(), IsToolMessage);
        }

        [Fact]
        public void Encode_DuplicateToolCallIds_RemovesIncompleteSequence()
        {
            var messages = Encode(
                new AIInteractionToolCall { Id = "call_1", Name = "tool_a", Arguments = new JObject() },
                new AIInteractionToolCall { Id = "call_1", Name = "tool_b", Arguments = new JObject() },
                new AIInteractionToolResult { Id = "call_1", Result = new JObject { ["result"] = "a" } },
                new AIInteractionToolResult { Id = "call_1", Result = new JObject { ["result"] = "b" } });

            AssertValidToolSequences(messages);
            Assert.DoesNotContain(messages.OfType<JObject>(), HasToolCalls);
            Assert.DoesNotContain(messages.OfType<JObject>(), IsToolMessage);
        }

        [Fact]
        public void Encode_MismatchedToolResultId_RemovesIncompleteSequence()
        {
            var messages = Encode(
                new AIInteractionToolCall { Id = "call_1", Name = "tool_a", Arguments = new JObject() },
                new AIInteractionToolResult { Id = "call_2", Result = new JObject { ["result"] = "a" } });

            AssertValidToolSequences(messages);
            Assert.DoesNotContain(messages.OfType<JObject>(), HasToolCalls);
            Assert.DoesNotContain(messages.OfType<JObject>(), IsToolMessage);
        }

        [Fact]
        public void Encode_OrphanToolResult_RemovesResult()
        {
            var messages = Encode(
                new AIInteractionToolResult { Id = "call_1", Result = new JObject { ["result"] = "a" } },
                new AIInteractionText { Agent = AIAgent.User, Content = "Continue." });

            AssertValidToolSequences(messages);
            Assert.DoesNotContain(messages.OfType<JObject>(), IsToolMessage);
        }

        private static JArray Encode(params IAIInteraction[] interactions)
        {
            var builder = AIBodyBuilder.Create()
                .Add(new AIInteractionText { Agent = AIAgent.System, Content = "You are helpful." })
                .Add(new AIInteractionText { Agent = AIAgent.User, Content = "Use tools when needed." });
            builder.AddRange(interactions);

            var request = new AIRequestCall
            {
                Provider = DeepSeekProvider.Instance.Name,
                Model = "deepseek-chat",
                Body = builder.Build(),
                Capability = AICapability.Text2Text,
            };

            DeepSeekProvider.Instance.RefreshCachedSettings(new Dictionary<string, object>
            {
                { "ApiKey", "test-key" },
                { "MaxTokens", 1024 },
                { "Temperature", 0.7 },
                { "TopP", 1.0 },
                { "ReasoningEffort", "none" },
            });

            var messages = JObject.Parse(DeepSeekProvider.Instance.Encode(request))["messages"] as JArray;
            Assert.NotNull(messages);
            return messages!;
        }

        private static void AssertValidToolSequences(JArray messages)
        {
            for (var i = 0; i < messages.Count; i++)
            {
                var message = (JObject)messages[i];
                if (IsToolMessage(message))
                {
                    Assert.True(i > 0 && (IsToolMessage((JObject)messages[i - 1]) || HasToolCalls((JObject)messages[i - 1])));
                }

                if (!HasToolCalls(message))
                {
                    continue;
                }

                var toolCalls = message["tool_calls"] as JArray;
                Assert.NotNull(toolCalls);
                var expectedIds = toolCalls!
                    .OfType<JObject>()
                    .Select(toolCall => toolCall["id"]?.ToString() ?? string.Empty)
                    .ToHashSet(StringComparer.Ordinal);
                Assert.DoesNotContain(expectedIds, string.IsNullOrWhiteSpace);

                var actualIds = new List<string>();
                var resultIndex = i + 1;
                while (resultIndex < messages.Count && IsToolMessage((JObject)messages[resultIndex]))
                {
                    actualIds.Add(messages[resultIndex]["tool_call_id"]?.ToString() ?? string.Empty);
                    resultIndex++;
                }

                Assert.Equal(toolCalls.Count, expectedIds.Count);
                Assert.Equal(expectedIds.Count, actualIds.Count);
                Assert.Equal(actualIds.Count, actualIds.Distinct(StringComparer.Ordinal).Count());
                Assert.True(expectedIds.SetEquals(actualIds));
            }
        }

        private static bool HasToolCalls(JObject message)
        {
            return message["tool_calls"] is JArray toolCalls && toolCalls.Count > 0;
        }

        private static bool IsToolMessage(JObject message)
        {
            return string.Equals(message["role"]?.ToString(), "tool", StringComparison.OrdinalIgnoreCase);
        }
    }
}

#endif
