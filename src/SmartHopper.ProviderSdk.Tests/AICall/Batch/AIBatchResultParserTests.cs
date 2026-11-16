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

namespace SmartHopper.ProviderSdk.Tests.AICall.Batch
{
    using System.Collections.Generic;
    using System.Linq;
    using Newtonsoft.Json.Linq;
    using SmartHopper.ProviderSdk.AICall.Batch;
    using SmartHopper.ProviderSdk.Diagnostics;
    using Xunit;

    /// <summary>
    /// Tests for <see cref="AIBatchResultParser"/>: per-item error extraction across
    /// the wire shapes providers actually emit (object, scalar, null) and the
    /// per-customId failure lookup used by completion accounting.
    /// </summary>
    [Collection("ProviderSdk")]
    public class AIBatchResultParserTests
    {
#if NET7_WINDOWS
        [Fact(DisplayName = "ExtractItemErrorMessage returns null for null/undefined [Windows]")]
#else
        [Fact(DisplayName = "ExtractItemErrorMessage returns null for null/undefined [Core]")]
#endif
        public void ExtractItemErrorMessage_Nullish_ReturnsNull()
        {
            Assert.Null(AIBatchResultParser.ExtractItemErrorMessage(null));
            Assert.Null(AIBatchResultParser.ExtractItemErrorMessage(JValue.CreateNull()));
            Assert.Null(AIBatchResultParser.ExtractItemErrorMessage(new JValue((object)null)));
            Assert.Null(AIBatchResultParser.ExtractItemErrorMessage(JToken.Parse("\"\"")));
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "ExtractItemErrorMessage reads object message [Windows]")]
#else
        [Fact(DisplayName = "ExtractItemErrorMessage reads object message [Core]")]
#endif
        public void ExtractItemErrorMessage_Object_ReadsMessage()
        {
            var error = JObject.Parse("{\"message\": \"rate limited\"}");
            Assert.Equal("rate limited", AIBatchResultParser.ExtractItemErrorMessage(error));
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "ExtractItemErrorMessage reads nested error.message [Windows]")]
#else
        [Fact(DisplayName = "ExtractItemErrorMessage reads nested error.message [Core]")]
#endif
        public void ExtractItemErrorMessage_Nested_ReadsInnerMessage()
        {
            var error = JObject.Parse("{\"error\": {\"message\": \"inner failure\"}}");
            Assert.Equal("inner failure", AIBatchResultParser.ExtractItemErrorMessage(error));
        }

#if NET7_WINDOWS
        [Theory(DisplayName = "ExtractItemErrorMessage reads scalar payloads [Windows]")]
#else
        [Theory(DisplayName = "ExtractItemErrorMessage reads scalar payloads [Core]")]
#endif
        [InlineData("\"content_filter\"", "content_filter")]
        [InlineData("429", "429")]
        [InlineData("true", "True")]
        public void ExtractItemErrorMessage_Scalar_ReturnsText(string json, string expected)
        {
            var token = JToken.Parse(json);
            Assert.Equal(expected, AIBatchResultParser.ExtractItemErrorMessage(token));
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "ExtractItemErrorMessage falls back to JSON for object without message [Windows]")]
#else
        [Fact(DisplayName = "ExtractItemErrorMessage falls back to JSON for object without message [Core]")]
#endif
        public void ExtractItemErrorMessage_ObjectNoMessage_ReturnsJson()
        {
            var error = JObject.Parse("{\"code\": 5}");
            var text = AIBatchResultParser.ExtractItemErrorMessage(error);
            Assert.NotNull(text);
            Assert.Contains("5", text);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "AddResultItem tolerates null error field (OpenRouter shape) [Windows]")]
#else
        [Fact(DisplayName = "AddResultItem tolerates null error field (OpenRouter shape) [Core]")]
#endif
        public void AddResultItem_NullError_CapturesResult()
        {
            // Live failure regression: OpenRouter emits "error": null on success
            // lines — indexing error["message"] on the JValue threw
            // InvalidOperationException and the poll loop retried the completed
            // batch forever.
            var item = JObject.Parse(
                "{\"custom_id\": \"req-1\", \"error\": null, \"response\": {\"status_code\": 200, \"body\": {\"choices\": []}}}");
            var results = new Dictionary<string, JObject>();
            var messages = new List<SHRuntimeMessage>();

            AIBatchResultParser.AddResultItem(item, results, messages);

            Assert.Single(results);
            Assert.True(results.ContainsKey("req-1"));
            Assert.Empty(messages.Where(m => m.Severity == SHRuntimeMessageSeverity.Error));
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "AddResultItem tolerates scalar error field [Windows]")]
#else
        [Fact(DisplayName = "AddResultItem tolerates scalar error field [Core]")]
#endif
        public void AddResultItem_ScalarError_ProducesMessage()
        {
            var item = JObject.Parse(
                "{\"custom_id\": \"req-2\", \"error\": \"content_filter\", \"response\": {\"status_code\": 200, \"body\": {}}}");
            var results = new Dictionary<string, JObject>();
            var messages = new List<SHRuntimeMessage>();

            AIBatchResultParser.AddResultItem(item, results, messages);

            Assert.Empty(results);
            var error = Assert.Single(messages.Where(m => m.Severity == SHRuntimeMessageSeverity.Error));
            Assert.Equal(SHMessageCode.BatchItemError, error.Code);
            Assert.Contains("req-2", error.Message);
            Assert.Contains("content_filter", error.Message);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "AddResultItem reports non-2xx responses [Windows]")]
#else
        [Fact(DisplayName = "AddResultItem reports non-2xx responses [Core]")]
#endif
        public void AddResultItem_NonSuccessStatus_ProducesError()
        {
            var item = JObject.Parse(
                "{\"custom_id\": \"req-3\", \"response\": {\"status_code\": 500, \"body\": {}}}");
            var results = new Dictionary<string, JObject>();
            var messages = new List<SHRuntimeMessage>();

            AIBatchResultParser.AddResultItem(item, results, messages);

            Assert.Empty(results);
            var error = Assert.Single(messages.Where(m => m.Severity == SHRuntimeMessageSeverity.Error));
            Assert.Contains("500", error.Message);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "AddResultItem skips items without custom_id [Windows]")]
#else
        [Fact(DisplayName = "AddResultItem skips items without custom_id [Core]")]
#endif
        public void AddResultItem_MissingCustomId_ProducesError()
        {
            var item = JObject.Parse(
                "{\"response\": {\"status_code\": 200, \"body\": {}}}");
            var results = new Dictionary<string, JObject>();
            var messages = new List<SHRuntimeMessage>();

            AIBatchResultParser.AddResultItem(item, results, messages);

            Assert.Empty(results);
            Assert.Single(messages.Where(m => m.Severity == SHRuntimeMessageSeverity.Error));
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "ParseResultContent parses full payload with null error [Windows]")]
#else
        [Fact(DisplayName = "ParseResultContent parses full payload with null error [Core]")]
#endif
        public void ParseResultContent_NullError_ParsesCleanly()
        {
            var content =
                "[{\"custom_id\": \"a\", \"error\": null, \"response\": {\"status_code\": 200, \"body\": {\"x\": 1}}}," +
                "{\"custom_id\": \"b\", \"error\": {\"message\": \"denied\"}, \"response\": {\"status_code\": 200, \"body\": {}}}]";

            var status = AIBatchResultParser.ParseResultContent(content, "batch-1");

            Assert.Single(status.Results);
            Assert.True(status.Results.ContainsKey("a"));
            var error = Assert.Single(status.Messages.Where(m => m.Severity == SHRuntimeMessageSeverity.Error));
            Assert.Contains("b", error.Message);
            Assert.Contains("denied", error.Message);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "TryGetFailedItemCustomId extracts the id [Windows]")]
#else
        [Fact(DisplayName = "TryGetFailedItemCustomId extracts the id [Core]")]
#endif
        public void TryGetFailedItemCustomId_MatchingMessage_ReturnsId()
        {
            var message = new SHRuntimeMessage(
                SHRuntimeMessageSeverity.Error,
                SHRuntimeMessageOrigin.Provider,
                SHMessageCode.BatchItemError,
                "Batch item req-9: rate limited");

            Assert.True(AIBatchResultParser.TryGetFailedItemCustomId(message, out var id));
            Assert.Equal("req-9", id);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "TryGetFailedItemCustomId rejects non-matching messages [Windows]")]
#else
        [Fact(DisplayName = "TryGetFailedItemCustomId rejects non-matching messages [Core]")]
#endif
        public void TryGetFailedItemCustomId_NonMatching_ReturnsFalse()
        {
            // Wrong severity (warnings are not terminal provider failures)
            Assert.False(AIBatchResultParser.TryGetFailedItemCustomId(
                new SHRuntimeMessage(
                    SHRuntimeMessageSeverity.Warning,
                    SHRuntimeMessageOrigin.Provider,
                    SHMessageCode.BatchItemError,
                    "Batch item req-1: expired"),
                out _));

            // Wrong code (cancellation/expiry are retryable, not provider-failed)
            Assert.False(AIBatchResultParser.TryGetFailedItemCustomId(
                new SHRuntimeMessage(
                    SHRuntimeMessageSeverity.Error,
                    SHRuntimeMessageOrigin.Provider,
                    SHMessageCode.BatchItemCanceled,
                    "Batch item req-1: canceled"),
                out _));

            // Wrong prefix (local decode failures stay retryable)
            Assert.False(AIBatchResultParser.TryGetFailedItemCustomId(
                new SHRuntimeMessage(
                    SHRuntimeMessageSeverity.Error,
                    SHRuntimeMessageOrigin.Worker,
                    SHMessageCode.BatchItemError,
                    "Failed to decode batch result 'req-1': boom"),
                out _));

            Assert.False(AIBatchResultParser.TryGetFailedItemCustomId(null, out _));
        }
    }
}
