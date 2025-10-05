/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

namespace SmartHopper.Infrastructure.Tests
{
    using System;
    using Newtonsoft.Json.Linq;
    using SmartHopper.Infrastructure.AICall.Core.Base;
    using SmartHopper.Infrastructure.AICall.Core.Interactions;
    using Xunit;

    /// <summary>
    /// Unit tests for interaction key generation edge cases.
    /// Validates GetStreamKey() and GetDedupKey() implementations across all interaction types.
    /// </summary>
    public class InteractionKeyGenerationTests
    {
        #region AIInteractionText Tests

        /// <summary>
        /// Tests that AIInteractionText.GetStreamKey returns stable keys that ignore content changes.
        /// </summary>
#if NET7_WINDOWS
        [Fact(DisplayName = "AIInteractionText_GetStreamKey_ShouldReturnStableKey [Windows]")]
#else
        [Fact(DisplayName = "AIInteractionText_GetStreamKey_ShouldReturnStableKey [Core]")]
#endif
        public void AIInteractionText_GetStreamKey_WithTurnId_ReturnsStableKey()
        {
            // Arrange
            var text = new AIInteractionText
            {
                TurnId = "abc123",
                Agent = AIAgent.Assistant,
                Content = "Initial content"
            };

            // Act
            var key1 = text.GetStreamKey();
            text.Content = "Modified content"; // Change content
            var key2 = text.GetStreamKey();

            // Assert
            Assert.Equal(key1, key2); // Stream key ignores content
            Assert.Equal("turn:abc123:assistant", key1);
        }

        /// <summary>
        /// Tests that AIInteractionText.GetStreamKey falls back to agent-only format when TurnId is null.
        /// </summary>
#if NET7_WINDOWS
        [Fact(DisplayName = "AIInteractionText_GetStreamKey_ShouldUseAgentOnly [Windows]")]
#else
        [Fact(DisplayName = "AIInteractionText_GetStreamKey_ShouldUseAgentOnly [Core]")]
#endif
        public void AIInteractionText_GetStreamKey_WithoutTurnId_UsesAgentOnly()
        {
            // Arrange
            var text = new AIInteractionText
            {
                TurnId = null,
                Agent = AIAgent.User,
                Content = "Hello"
            };

            // Act
            var key = text.GetStreamKey();

            // Assert
            Assert.Equal("text:user", key);
        }

        /// <summary>
        /// Tests that AIInteractionText.GetDedupKey produces different keys for different content.
        /// </summary>
#if NET7_WINDOWS
        [Fact(DisplayName = "AIInteractionText_GetDedupKey_ShouldIncludeContentHash [Windows]")]
#else
        [Fact(DisplayName = "AIInteractionText_GetDedupKey_ShouldIncludeContentHash [Core]")]
#endif
        public void AIInteractionText_GetDedupKey_IncludesContentHash()
        {
            // Arrange
            var text1 = new AIInteractionText
            {
                TurnId = "abc123",
                Agent = AIAgent.Assistant,
                Content = "Hello"
            };

            var text2 = new AIInteractionText
            {
                TurnId = "abc123",
                Agent = AIAgent.Assistant,
                Content = "World"
            };

            // Act
            var key1 = text1.GetDedupKey();
            var key2 = text2.GetDedupKey();

            // Assert
            Assert.NotEqual(key1, key2); // Different content → different dedup key
            Assert.StartsWith("turn:abc123:assistant:", key1);
            Assert.StartsWith("turn:abc123:assistant:", key2);
        }

        /// <summary>
        /// Tests that AIInteractionText.GetDedupKey produces same keys for identical content.
        /// </summary>
#if NET7_WINDOWS
        [Fact(DisplayName = "AIInteractionText_GetDedupKey_ShouldBeDeterministic [Windows]")]
#else
        [Fact(DisplayName = "AIInteractionText_GetDedupKey_ShouldBeDeterministic [Core]")]
#endif
        public void AIInteractionText_GetDedupKey_SameContent_SameHash()
        {
            // Arrange
            var text1 = new AIInteractionText
            {
                TurnId = "abc123",
                Agent = AIAgent.Assistant,
                Content = "Hello World"
            };

            var text2 = new AIInteractionText
            {
                TurnId = "abc123",
                Agent = AIAgent.Assistant,
                Content = "Hello World"
            };

            // Act
            var key1 = text1.GetDedupKey();
            var key2 = text2.GetDedupKey();

            // Assert
            Assert.Equal(key1, key2); // Same content → same dedup key
        }

        /// <summary>
        /// Tests that AIInteractionText.GetStreamKey falls back to text prefix when TurnId is empty.
        /// </summary>
#if NET7_WINDOWS
        [Fact(DisplayName = "AIInteractionText_GetStreamKey_ShouldHandleEmptyTurnId [Windows]")]
#else
        [Fact(DisplayName = "AIInteractionText_GetStreamKey_ShouldHandleEmptyTurnId [Core]")]
#endif
        public void AIInteractionText_GetStreamKey_EmptyTurnId_FallsBackToTextPrefix()
        {
            // Arrange
            var text = new AIInteractionText
            {
                TurnId = "",
                Agent = AIAgent.Assistant,
                Content = "Test"
            };

            // Act
            var key = text.GetStreamKey();

            // Assert
            Assert.Equal("text:assistant", key);
        }

        #endregion

        #region AIInteractionToolCall Tests

        /// <summary>
        /// Tests that AIInteractionToolCall.GetStreamKey uses the tool call ID.
        /// </summary>
#if NET7_WINDOWS
        [Fact(DisplayName = "AIInteractionToolCall_GetStreamKey_ShouldUseToolCallId [Windows]")]
#else
        [Fact(DisplayName = "AIInteractionToolCall_GetStreamKey_ShouldUseToolCallId [Core]")]
#endif
        public void AIInteractionToolCall_GetStreamKey_UsesToolCallId()
        {
            // Arrange
            var toolCall = new AIInteractionToolCall
            {
                TurnId = "abc123",
                Id = "call_xyz",
                Name = "gh_get"
            };

            // Act
            var key = toolCall.GetStreamKey();

            // Assert
            Assert.Equal("turn:abc123:tool.call:call_xyz", key);
        }

        /// <summary>
        /// Tests that AIInteractionToolCall.GetStreamKey falls back to tool name when ID is null.
        /// </summary>
#if NET7_WINDOWS
        [Fact(DisplayName = "AIInteractionToolCall_GetStreamKey_ShouldFallbackToName [Windows]")]
#else
        [Fact(DisplayName = "AIInteractionToolCall_GetStreamKey_ShouldFallbackToName [Core]")]
#endif
        public void AIInteractionToolCall_GetStreamKey_FallsBackToName()
        {
            // Arrange
            var toolCall = new AIInteractionToolCall
            {
                TurnId = "abc123",
                Id = null,
                Name = "gh_get"
            };

            // Act
            var key = toolCall.GetStreamKey();

            // Assert
            Assert.Equal("turn:abc123:tool.call:gh_get", key);
        }

        /// <summary>
        /// Tests that AIInteractionToolCall.GetDedupKey hashes arguments to create stable keys.
        /// </summary>
#if NET7_WINDOWS
        [Fact(DisplayName = "AIInteractionToolCall_GetDedupKey_ShouldHashArguments [Windows]")]
#else
        [Fact(DisplayName = "AIInteractionToolCall_GetDedupKey_ShouldHashArguments [Core]")]
#endif
        public void AIInteractionToolCall_GetDedupKey_HashesArguments()
        {
            // Arrange
            var args = new JObject
            {
                ["filters"] = new JObject
                {
                    ["type"] = "Point",
                    ["name"] = "MyPoint"
                },
                ["includeWires"] = true
            };

            var toolCall = new AIInteractionToolCall
            {
                TurnId = "abc123",
                Id = "call_xyz",
                Name = "gh_get",
                Arguments = args
            };

            // Act
            var key = toolCall.GetDedupKey();

            // Assert
            Assert.StartsWith("turn:abc123:tool.call:call_xyz:", key);
            
            // Verify hash is 16 chars (short hash format)
            var parts = key.Split(':');
            Assert.Equal(5, parts.Length); // turn:abc123:tool.call:call_xyz:hash
            Assert.Equal(16, parts[4].Length); // Hash should be 16 hex chars
        }

        /// <summary>
        /// Tests that AIInteractionToolCall.GetDedupKey produces same keys for identical arguments.
        /// </summary>
#if NET7_WINDOWS
        [Fact(DisplayName = "AIInteractionToolCall_GetDedupKey_ShouldBeDeterministic [Windows]")]
#else
        [Fact(DisplayName = "AIInteractionToolCall_GetDedupKey_ShouldBeDeterministic [Core]")]
#endif
        public void AIInteractionToolCall_GetDedupKey_SameArguments_SameHash()
        {
            // Arrange
            var args1 = new JObject { ["test"] = "value" };
            var args2 = new JObject { ["test"] = "value" };

            var toolCall1 = new AIInteractionToolCall
            {
                TurnId = "abc123",
                Id = "call_1",
                Arguments = args1
            };

            var toolCall2 = new AIInteractionToolCall
            {
                TurnId = "abc123",
                Id = "call_1",
                Arguments = args2
            };

            // Act
            var key1 = toolCall1.GetDedupKey();
            var key2 = toolCall2.GetDedupKey();

            // Assert
            Assert.Equal(key1, key2); // Same arguments → same hash
        }

        /// <summary>
        /// Tests that AIInteractionToolCall.GetDedupKey produces different keys for different arguments.
        /// </summary>
#if NET7_WINDOWS
        [Fact(DisplayName = "AIInteractionToolCall_GetDedupKey_ShouldDifferentiateArguments [Windows]")]
#else
        [Fact(DisplayName = "AIInteractionToolCall_GetDedupKey_ShouldDifferentiateArguments [Core]")]
#endif
        public void AIInteractionToolCall_GetDedupKey_DifferentArguments_DifferentHash()
        {
            // Arrange
            var args1 = new JObject { ["param"] = "value1" };
            var args2 = new JObject { ["param"] = "value2" };

            var toolCall1 = new AIInteractionToolCall
            {
                TurnId = "abc123",
                Id = "call_1",
                Arguments = args1
            };

            var toolCall2 = new AIInteractionToolCall
            {
                TurnId = "abc123",
                Id = "call_1",
                Arguments = args2
            };

            // Act
            var key1 = toolCall1.GetDedupKey();
            var key2 = toolCall2.GetDedupKey();

            // Assert
            Assert.NotEqual(key1, key2); // Different arguments → different hash
        }

        /// <summary>
        /// Tests that AIInteractionToolCall.GetDedupKey uses 'none' for null arguments.
        /// </summary>
#if NET7_WINDOWS
        [Fact(DisplayName = "AIInteractionToolCall_GetDedupKey_ShouldHandleNullArguments [Windows]")]
#else
        [Fact(DisplayName = "AIInteractionToolCall_GetDedupKey_ShouldHandleNullArguments [Core]")]
#endif
        public void AIInteractionToolCall_GetDedupKey_NullArguments_UsesNone()
        {
            // Arrange
            var toolCall = new AIInteractionToolCall
            {
                TurnId = "abc123",
                Id = "call_1",
                Arguments = null
            };

            // Act
            var key = toolCall.GetDedupKey();

            // Assert
            Assert.EndsWith(":none", key); // Null arguments → "none"
        }

        /// <summary>
        /// Tests that AIInteractionToolCall.GetDedupKey produces fixed-length hashes regardless of argument size.
        /// </summary>
#if NET7_WINDOWS
        [Fact(DisplayName = "AIInteractionToolCall_GetDedupKey_ShouldProduceFixedLengthHash [Windows]")]
#else
        [Fact(DisplayName = "AIInteractionToolCall_GetDedupKey_ShouldProduceFixedLengthHash [Core]")]
#endif
        public void AIInteractionToolCall_GetDedupKey_LargeArguments_ProducesFixedLengthHash()
        {
            // Arrange - Create very large arguments object
            var largeArgs = new JObject();
            for (int i = 0; i < 100; i++)
            {
                largeArgs[$"property_{i}"] = $"very_long_value_string_that_repeats_{i}";
            }

            var toolCall = new AIInteractionToolCall
            {
                TurnId = "abc123",
                Id = "call_xyz",
                Arguments = largeArgs
            };

            // Act
            var key = toolCall.GetDedupKey();

            // Assert
            var parts = key.Split(':');
            Assert.Equal(16, parts[4].Length); // Hash length remains constant regardless of input size
            Assert.True(key.Length < 100); // Total key length bounded
        }

        #endregion

        #region AIInteractionToolResult Tests

        /// <summary>
        /// Tests that AIInteractionToolResult.GetStreamKey uses the result prefix.
        /// </summary>
#if NET7_WINDOWS
        [Fact(DisplayName = "AIInteractionToolResult_GetStreamKey_ShouldUseResultPrefix [Windows]")]
#else
        [Fact(DisplayName = "AIInteractionToolResult_GetStreamKey_ShouldUseResultPrefix [Core]")]
#endif
        public void AIInteractionToolResult_GetStreamKey_UsesResultPrefix()
        {
            // Arrange
            var toolResult = new AIInteractionToolResult
            {
                TurnId = "abc123",
                Id = "call_xyz",
                Name = "gh_get"
            };

            // Act
            var key = toolResult.GetStreamKey();

            // Assert
            Assert.Equal("turn:abc123:tool.result:call_xyz", key);
        }

        /// <summary>
        /// Tests that AIInteractionToolResult.GetDedupKey hashes the result data.
        /// </summary>
#if NET7_WINDOWS
        [Fact(DisplayName = "AIInteractionToolResult_GetDedupKey_ShouldHashResult [Windows]")]
#else
        [Fact(DisplayName = "AIInteractionToolResult_GetDedupKey_ShouldHashResult [Core]")]
#endif
        public void AIInteractionToolResult_GetDedupKey_HashesResult()
        {
            // Arrange
            var result = new JObject
            {
                ["data"] = new JArray { 1, 2, 3 },
                ["count"] = 3
            };

            var toolResult = new AIInteractionToolResult
            {
                TurnId = "abc123",
                Id = "call_xyz",
                Result = result
            };

            // Act
            var key = toolResult.GetDedupKey();

            // Assert
            Assert.StartsWith("turn:abc123:tool.result:call_xyz:", key);
            var parts = key.Split(':');
            Assert.Equal(5, parts.Length);
            Assert.Equal(16, parts[4].Length); // Hash is 16 chars
        }

        #endregion

        #region AIInteractionError Tests

        /// <summary>
        /// Tests that AIInteractionError.GetStreamKey hashes content for unique identification.
        /// </summary>
#if NET7_WINDOWS
        [Fact(DisplayName = "AIInteractionError_GetStreamKey_ShouldHashContent [Windows]")]
#else
        [Fact(DisplayName = "AIInteractionError_GetStreamKey_ShouldHashContent [Core]")]
#endif
        public void AIInteractionError_GetStreamKey_HashesContent()
        {
            // Arrange
            var error = new AIInteractionError
            {
                TurnId = "abc123",
                Content = "An error occurred"
            };

            // Act
            var key = error.GetStreamKey();

            // Assert
            Assert.StartsWith("turn:abc123:error:", key);
        }

        /// <summary>
        /// Tests that AIInteractionError.GetDedupKey equals GetStreamKey for errors.
        /// </summary>
#if NET7_WINDOWS
        [Fact(DisplayName = "AIInteractionError_GetDedupKey_ShouldEqualStreamKey [Windows]")]
#else
        [Fact(DisplayName = "AIInteractionError_GetDedupKey_ShouldEqualStreamKey [Core]")]
#endif
        public void AIInteractionError_GetDedupKey_EqualsStreamKey()
        {
            // Arrange
            var error = new AIInteractionError
            {
                TurnId = "abc123",
                Content = "Test error"
            };

            // Act
            var streamKey = error.GetStreamKey();
            var dedupKey = error.GetDedupKey();

            // Assert
            Assert.Equal(streamKey, dedupKey); // For errors, both keys are the same
        }

        /// <summary>
        /// Tests that AIInteractionError.GetStreamKey produces same keys for identical content.
        /// </summary>
#if NET7_WINDOWS
        [Fact(DisplayName = "AIInteractionError_GetStreamKey_ShouldBeDeterministic [Windows]")]
#else
        [Fact(DisplayName = "AIInteractionError_GetStreamKey_ShouldBeDeterministic [Core]")]
#endif
        public void AIInteractionError_GetStreamKey_SameContent_SameHash()
        {
            // Arrange
            var error1 = new AIInteractionError
            {
                TurnId = "abc123",
                Content = "Network timeout"
            };

            var error2 = new AIInteractionError
            {
                TurnId = "abc123",
                Content = "Network timeout"
            };

            // Act
            var key1 = error1.GetStreamKey();
            var key2 = error2.GetStreamKey();

            // Assert
            Assert.Equal(key1, key2); // Same content → same key
        }

        #endregion

        #region AIInteractionImage Tests

        /// <summary>
        /// Tests that AIInteractionImage.GetStreamKey uses the image URL.
        /// </summary>
#if NET7_WINDOWS
        [Fact(DisplayName = "AIInteractionImage_GetStreamKey_ShouldUseUrl [Windows]")]
#else
        [Fact(DisplayName = "AIInteractionImage_GetStreamKey_ShouldUseUrl [Core]")]
#endif
        public void AIInteractionImage_GetStreamKey_UsesUrl()
        {
            // Arrange
            var image = new AIInteractionImage
            {
                TurnId = "abc123"
            };
            image.SetResult(new System.Uri("https://example.com/image.png"));

            // Act
            var key = image.GetStreamKey();

            // Assert
            Assert.StartsWith("turn:abc123:image:", key);
            Assert.Contains("https://example.com/image.png", key);
        }

        /// <summary>
        /// Tests that AIInteractionImage.GetDedupKey includes image generation options.
        /// </summary>
#if NET7_WINDOWS
        [Fact(DisplayName = "AIInteractionImage_GetDedupKey_ShouldIncludeOptions [Windows]")]
#else
        [Fact(DisplayName = "AIInteractionImage_GetDedupKey_ShouldIncludeOptions [Core]")]
#endif
        public void AIInteractionImage_GetDedupKey_IncludesImageOptions()
        {
            // Arrange
            var image = new AIInteractionImage
            {
                TurnId = "abc123",
                ImageSize = "1024x1024",
                ImageQuality = "hd",
                ImageStyle = "vivid"
            };
            image.SetResult(new System.Uri("https://example.com/image.png"));

            // Act
            var key = image.GetDedupKey();

            // Assert
            Assert.Contains(":1024x1024:", key);
            Assert.Contains(":hd:", key);
            Assert.Contains(":vivid", key);
        }

        /// <summary>
        /// Tests that AIInteractionImage.GetDedupKey differentiates based on generation options.
        /// </summary>
#if NET7_WINDOWS
        [Fact(DisplayName = "AIInteractionImage_GetDedupKey_ShouldDifferentiateOptions [Windows]")]
#else
        [Fact(DisplayName = "AIInteractionImage_GetDedupKey_ShouldDifferentiateOptions [Core]")]
#endif
        public void AIInteractionImage_GetDedupKey_DifferentOptions_DifferentKeys()
        {
            // Arrange
            var image1 = new AIInteractionImage
            {
                TurnId = "abc123",
                ImageSize = "1024x1024",
                ImageQuality = "standard"
            };
            image1.SetResult(new System.Uri("https://example.com/same.png"));

            var image2 = new AIInteractionImage
            {
                TurnId = "abc123",
                ImageSize = "512x512",
                ImageQuality = "hd"
            };
            image2.SetResult(new System.Uri("https://example.com/same.png"));

            // Act
            var key1 = image1.GetDedupKey();
            var key2 = image2.GetDedupKey();

            // Assert
            Assert.NotEqual(key1, key2); // Different options → different dedup keys
        }

        #endregion

        #region Edge Cases

        /// <summary>
        /// Tests that all interaction types generate valid keys when TurnId is null.
        /// </summary>
#if NET7_WINDOWS
        [Fact(DisplayName = "AllInteractions_ShouldGenerateValidKeysWithoutTurnId [Windows]")]
#else
        [Fact(DisplayName = "AllInteractions_ShouldGenerateValidKeysWithoutTurnId [Core]")]
#endif
        public void AllInteractions_WithoutTurnId_GenerateValidKeys()
        {
            // Arrange
            var text = new AIInteractionText { Agent = AIAgent.User, Content = "Test" };
            var toolCall = new AIInteractionToolCall { Id = "call_1", Name = "test" };
            var toolResult = new AIInteractionToolResult { Id = "call_1" };
            var error = new AIInteractionError { Content = "Error" };

            // Act & Assert
            Assert.DoesNotContain("turn:", text.GetStreamKey());
            Assert.DoesNotContain("turn:", toolCall.GetStreamKey());
            Assert.DoesNotContain("turn:", toolResult.GetStreamKey());
            Assert.DoesNotContain("turn:", error.GetStreamKey());
        }

        /// <summary>
        /// Tests that all interaction types generate non-empty keys and reasonable lengths.
        /// </summary>
#if NET7_WINDOWS
        [Fact(DisplayName = "AllInteractions_ShouldGenerateNonEmptyKeys [Windows]")]
#else
        [Fact(DisplayName = "AllInteractions_ShouldGenerateNonEmptyKeys [Core]")]
#endif
        public void AllInteractions_GenerateNonEmptyKeys()
        {
            // Arrange
            var interactions = new IAIKeyedInteraction[]
            {
                new AIInteractionText { TurnId = "t1", Agent = AIAgent.Assistant, Content = "Test" },
                new AIInteractionToolCall { TurnId = "t1", Id = "c1", Name = "test" },
                new AIInteractionToolResult { TurnId = "t1", Id = "c1" },
                new AIInteractionError { TurnId = "t1", Content = "Error" },
                new AIInteractionImage { TurnId = "t1", OriginalPrompt = "Test" }
            };

            // Act & Assert
            foreach (var interaction in interactions)
            {
                var streamKey = interaction.GetStreamKey();
                var dedupKey = interaction.GetDedupKey();

                Assert.False(string.IsNullOrWhiteSpace(streamKey), $"{interaction.GetType().Name} produced empty stream key");
                Assert.False(string.IsNullOrWhiteSpace(dedupKey), $"{interaction.GetType().Name} produced empty dedup key");
                Assert.True(streamKey.Length < 500, $"{interaction.GetType().Name} stream key exceeds reasonable length");
                Assert.True(dedupKey.Length < 500, $"{interaction.GetType().Name} dedup key exceeds reasonable length");
            }
        }

        /// <summary>
        /// Tests that tool call key length remains bounded even with very large arguments.
        /// Validates the hash improvement that prevents excessively long keys.
        /// </summary>
#if NET7_WINDOWS
        [Fact(DisplayName = "ToolCall_KeyLength_ShouldBeBoundedDespiteArguments [Windows]")]
#else
        [Fact(DisplayName = "ToolCall_KeyLength_ShouldBeBoundedDespiteArguments [Core]")]
#endif
        public void ToolCall_KeyLengthBoundedDespiteArguments()
        {
            // This test validates the hash improvement: keys stay bounded even with large arguments
            var maxKeyLength = 0;

            for (int i = 1; i <= 10; i++)
            {
                var args = new JObject();
                for (int j = 0; j < i * 100; j++)
                {
                    args[$"key_{j}"] = $"value_{j}";
                }

                var toolCall = new AIInteractionToolCall
                {
                    TurnId = "abc123",
                    Id = $"call_{i}",
                    Name = "test_tool",
                    Arguments = args
                };

                var dedupKey = toolCall.GetDedupKey();
                maxKeyLength = Math.Max(maxKeyLength, dedupKey.Length);
            }

            // With hashing, key length should stay well under 100 chars regardless of argument size
            Assert.True(maxKeyLength < 100, $"Max key length {maxKeyLength} exceeds expected bound");
        }

        #endregion
    }
}
