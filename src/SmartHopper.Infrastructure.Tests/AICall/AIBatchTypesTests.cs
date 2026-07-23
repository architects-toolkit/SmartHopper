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
    using SmartHopper.ProviderSdk.AICall.Batch;
    using SmartHopper.ProviderSdk.Diagnostics;
    using Xunit;

    public class AIBatchSubmissionTests
    {
        [Fact(DisplayName = "Constructor_MultiItem_SetsAllProperties")]
        public void Constructor_MultiItem_SetsAllProperties()
        {
            var customIds = new List<string> { "id1", "id2", "id3" };
            var submission = new AIBatchSubmission("batch-123", "openai", "serialized", customIds);
            Assert.Equal("batch-123", submission.BatchId);
            Assert.Equal("openai", submission.ProviderName);
            Assert.Equal(customIds, submission.CustomIds);
            Assert.Equal("id1", submission.CustomId);
        }

        [Fact(DisplayName = "Constructor_SingleItem_WrapsToReadOnlyList")]
        public void Constructor_SingleItem_WrapsToReadOnlyList()
        {
            var submission = new AIBatchSubmission("batch-123", "openai", "serialized", "single-id");
            Assert.Single(submission.CustomIds);
            Assert.Equal("single-id", submission.CustomId);
        }

        [Fact(DisplayName = "Constructor_NullBatchId_ThrowsArgumentNull")]
        public void Constructor_NullBatchId_ThrowsArgumentNull()
        {
            var customIds = new List<string> { "id1" };
            Assert.Throws<ArgumentNullException>(() =>
                new AIBatchSubmission(null, "openai", "serialized", customIds));
        }

        [Fact(DisplayName = "Constructor_NullProviderName_ThrowsArgumentNull")]
        public void Constructor_NullProviderName_ThrowsArgumentNull()
        {
            var customIds = new List<string> { "id1" };
            Assert.Throws<ArgumentNullException>(() =>
                new AIBatchSubmission("batch-123", null, "serialized", customIds));
        }

        [Fact(DisplayName = "Constructor_NullCustomId_EmptyCustomIds")]
        public void Constructor_NullCustomId_EmptyCustomIds()
        {
            var submission = new AIBatchSubmission("batch-123", "openai", "serialized", (string)null);
            Assert.Empty(submission.CustomIds);
            Assert.Null(submission.CustomId);
        }

        [Fact(DisplayName = "CustomId_ReturnsFirstFromList")]
        public void CustomId_ReturnsFirstFromList()
        {
            var customIds = new List<string> { "first", "second", "third" };
            var submission = new AIBatchSubmission("batch-123", "openai", "serialized", customIds);
            Assert.Equal("first", submission.CustomId);
        }

        [Fact(DisplayName = "CustomId_WhenEmpty_ReturnsNull")]
        public void CustomId_WhenEmpty_ReturnsNull()
        {
            var submission = new AIBatchSubmission("batch-123", "openai", "serialized", new List<string>());
            Assert.Null(submission.CustomId);
        }

        [Fact(DisplayName = "GenerateCustomId_DefaultParams_MatchesFormat")]
        public void GenerateCustomId_DefaultParams_MatchesFormat()
        {
            var customId = AIBatchSubmission.GenerateCustomId();
            Assert.NotNull(customId);
            Assert.StartsWith("sh-", customId);
            Assert.Contains("-req-", customId);
            Assert.Contains("-00-", customId);
            var parts = customId.Split('-');
            Assert.True(parts.Length >= 5);
        }

        [Fact(DisplayName = "GenerateCustomId_CustomEndpoint_SanitizesUnderscores")]
        public void GenerateCustomId_CustomEndpoint_SanitizesUnderscores()
        {
            var customId = AIBatchSubmission.GenerateCustomId("text_2_text", 0);
            Assert.Contains("-text-2-text-", customId);
        }

        [Fact(DisplayName = "GenerateCustomId_CustomIndex_FormatsTwoDigits")]
        public void GenerateCustomId_CustomIndex_FormatsTwoDigits()
        {
            var customId1 = AIBatchSubmission.GenerateCustomId("test", 0);
            var customId2 = AIBatchSubmission.GenerateCustomId("test", 9);
            var customId3 = AIBatchSubmission.GenerateCustomId("test", 42);
            Assert.Contains("-00-", customId1);
            Assert.Contains("-09-", customId2);
            Assert.Contains("-42-", customId3);
        }

        [Fact(DisplayName = "GenerateCustomId_TwoCalls_ProduceDifferentRandomParts")]
        public void GenerateCustomId_TwoCalls_ProduceDifferentRandomParts()
        {
            var customId1 = AIBatchSubmission.GenerateCustomId("test", 0);
            var customId2 = AIBatchSubmission.GenerateCustomId("test", 0);
            Assert.NotEqual(customId1, customId2);
        }

        [Fact(DisplayName = "SubmittedAt_IsUtcNow")]
        public void SubmittedAt_IsUtcNow()
        {
            var before = DateTimeOffset.UtcNow;
            var submission = new AIBatchSubmission("batch-123", "openai", "serialized", "id1");
            var after = DateTimeOffset.UtcNow;
            Assert.InRange(submission.SubmittedAt, before, after);
        }
    }

    public class AIBatchStatusTests
    {
        [Fact(DisplayName = "Constructor_NonCompleted_SetsStateAndFields")]
        public void Constructor_NonCompleted_SetsStateAndFields()
        {
            var status = new AIBatchStatus("batch-123", AIBatchState.InProgress, null, 5);
            Assert.Equal("batch-123", status.BatchId);
            Assert.Equal(AIBatchState.InProgress, status.State);
            Assert.Null(status.ErrorMessage);
            Assert.Equal(5, status.CompletedCount);
        }

        [Fact(DisplayName = "Constructor_Completed_SetsStateAndResults")]
        public void Constructor_Completed_SetsStateAndResults()
        {
            var results = new Dictionary<string, Newtonsoft.Json.Linq.JObject>
            {
                { "id1", new Newtonsoft.Json.Linq.JObject() }
            };
            var status = new AIBatchStatus("batch-123", results);
            Assert.Equal("batch-123", status.BatchId);
            Assert.Equal(AIBatchState.Completed, status.State);
            Assert.Single(status.Results);
        }

        [Fact(DisplayName = "Constructor_WithMessages_SetsMessages")]
        public void Constructor_WithMessages_SetsMessages()
        {
            var results = new Dictionary<string, Newtonsoft.Json.Linq.JObject>();
            var messages = new List<SHRuntimeMessage>
            {
                new SHRuntimeMessage(
                    SHRuntimeMessageSeverity.Error,
                    SHRuntimeMessageOrigin.Provider,
                    SHMessageCode.Unknown,
                    "Error"),
            };
            var status = new AIBatchStatus("batch-123", results, messages);
            Assert.Single(status.Messages);
        }

        [Fact(DisplayName = "CompletedCount_Null_WhenNotProvided")]
        public void CompletedCount_Null_WhenNotProvided()
        {
            var status = new AIBatchStatus("batch-123", AIBatchState.Submitted);
            Assert.Null(status.CompletedCount);
        }

        [Fact(DisplayName = "CompletedCount_Set_WhenProvided")]
        public void CompletedCount_Set_WhenProvided()
        {
            var status = new AIBatchStatus("batch-123", AIBatchState.InProgress, null, 10);
            Assert.Equal(10, status.CompletedCount);
        }

        [Fact(DisplayName = "CheckedAt_IsUtcNow")]
        public void CheckedAt_IsUtcNow()
        {
            var before = DateTimeOffset.UtcNow;
            var status = new AIBatchStatus("batch-123", AIBatchState.Submitted);
            var after = DateTimeOffset.UtcNow;
            Assert.InRange(status.CheckedAt, before, after);
        }

        [Fact(DisplayName = "State_Submitted_IsValid")]
        public void State_Submitted_IsValid()
        {
            var status = new AIBatchStatus("batch-123", AIBatchState.Submitted);
            Assert.Equal(AIBatchState.Submitted, status.State);
        }

        [Fact(DisplayName = "State_Failed_WithErrorMessage")]
        public void State_Failed_WithErrorMessage()
        {
            var status = new AIBatchStatus("batch-123", AIBatchState.Failed, "Something went wrong");
            Assert.Equal(AIBatchState.Failed, status.State);
            Assert.Equal("Something went wrong", status.ErrorMessage);
        }
    }
}
