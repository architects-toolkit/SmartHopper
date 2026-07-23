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

#if NET7_WINDOWS

namespace SmartHopper.Infrastructure.Tests.AICall.Providers
{
    using System.Collections.Generic;
    using System.Linq;
    using SmartHopper.ProviderSdk.AICall.Batch;
    using SmartHopper.ProviderSdk.Diagnostics;
    using SmartHopper.Providers.Anthropic;
    using SmartHopper.Providers.Gemini;
    using SmartHopper.Providers.MistralAI;
    using SmartHopper.Providers.OpenAI;
    using Xunit;

    /// <summary>
    /// Unit tests verifying that <see cref="IAIBatchProvider.ParseBatchResultsFiles"/> correctly
    /// identifies unexpected item endings (finish_reason=length, cancelled, expired, etc.) and
    /// surfaces them as <see cref="SHRuntimeMessage"/> entries so the component can display them
    /// as runtime messages.
    /// </summary>
    public class ProviderBatchParsingTests
    {
        // ────────────────────────────────────────────────────
        // OpenAI – Chat Completions (JSONL)
        // ────────────────────────────────────────────────────

        [Fact(DisplayName = "OpenAI_ChatComp_FinishReason_Stop_NoWarning")]
        public void OpenAI_ChatComp_FinishReason_Stop_NoWarning()
        {
            var jsonl = MakeOpenAILine("sh-id-001", 200, MakeOpenAIChatBody("hello", "stop"));
            var status = OpenAIProvider.Instance.ParseBatchResultsFiles(new[] { jsonl });

            Assert.Equal(AIBatchState.Completed, status.State);
            Assert.True(status.Results?.ContainsKey("sh-id-001"));
            var finishWarnings = (status.Messages ?? new List<SHRuntimeMessage>())
                .Where(m => m.Code == SHMessageCode.BatchItemFinishReason)
                .ToList();
            Assert.Empty(finishWarnings);
        }

        [Fact(DisplayName = "OpenAI_ChatComp_FinishReason_Length_EmitsWarning")]
        public void OpenAI_ChatComp_FinishReason_Length_EmitsWarning()
        {
            var jsonl = MakeOpenAILine("sh-id-002", 200, MakeOpenAIChatBody("truncated...", "length"));
            var status = OpenAIProvider.Instance.ParseBatchResultsFiles(new[] { jsonl });

            Assert.Equal(AIBatchState.Completed, status.State);
            Assert.True(status.Results?.ContainsKey("sh-id-002"));

            var warning = status.Messages?.SingleOrDefault(m => m.Code == SHMessageCode.BatchItemFinishReason);
            Assert.NotNull(warning);
            Assert.Equal(SHRuntimeMessageSeverity.Warning, warning.Severity);
            Assert.Equal(SHRuntimeMessageOrigin.Provider, warning.Origin);
            Assert.Contains("sh-id-002", warning.Message);
            Assert.Contains("length", warning.Message);
        }

        [Fact(DisplayName = "OpenAI_ChatComp_FinishReason_ContentFilter_EmitsWarning")]
        public void OpenAI_ChatComp_FinishReason_ContentFilter_EmitsWarning()
        {
            var jsonl = MakeOpenAILine("sh-id-003", 200, MakeOpenAIChatBody("filtered", "content_filter"));
            var status = OpenAIProvider.Instance.ParseBatchResultsFiles(new[] { jsonl });

            var warning = status.Messages?.SingleOrDefault(m => m.Code == SHMessageCode.BatchItemFinishReason);
            Assert.NotNull(warning);
            Assert.Equal(SHRuntimeMessageSeverity.Warning, warning.Severity);
            Assert.Contains("content_filter", warning.Message);
        }

        [Fact(DisplayName = "OpenAI_ChatComp_HttpError_EmitsError")]
        public void OpenAI_ChatComp_HttpError_EmitsError()
        {
            var errorBody = @"{""error"":{""message"":""Rate limit exceeded"",""type"":""rate_limit_error""}}";
            var jsonl = MakeOpenAILine("sh-id-004", 429, errorBody);
            var status = OpenAIProvider.Instance.ParseBatchResultsFiles(new[] { jsonl });

            Assert.False(status.Results?.ContainsKey("sh-id-004") ?? false);

            var error = status.Messages?.SingleOrDefault(m => m.Code == SHMessageCode.BatchItemError);
            Assert.NotNull(error);
            Assert.Equal(SHRuntimeMessageSeverity.Error, error.Severity);
            Assert.Contains("sh-id-004", error.Message);
        }

        [Fact(DisplayName = "OpenAI_MixedBatch_OneOk_OneLengthTruncated")]
        public void OpenAI_MixedBatch_OneOk_OneLengthTruncated()
        {
            var okLine = MakeOpenAILine("sh-ok", 200, MakeOpenAIChatBody("ok", "stop"));
            var lenLine = MakeOpenAILine("sh-trunc", 200, MakeOpenAIChatBody("truncated", "length"));
            var status = OpenAIProvider.Instance.ParseBatchResultsFiles(new[] { okLine + "\n" + lenLine });

            Assert.Equal(AIBatchState.Completed, status.State);
            Assert.True(status.Results?.ContainsKey("sh-ok"));
            Assert.True(status.Results?.ContainsKey("sh-trunc"));

            var finishWarnings = (status.Messages ?? new List<SHRuntimeMessage>())
                .Where(m => m.Code == SHMessageCode.BatchItemFinishReason)
                .ToList();
            Assert.Single(finishWarnings);
            Assert.Contains("sh-trunc", finishWarnings[0].Message);
        }

        // ────────────────────────────────────────────────────
        // OpenAI – Responses API (JSONL)
        // ────────────────────────────────────────────────────

        [Fact(DisplayName = "OpenAI_Responses_StatusIncomplete_EmitsWarning")]
        public void OpenAI_Responses_StatusIncomplete_EmitsWarning()
        {
            var body = @"{""id"":""resp-abc"",""object"":""response"",""status"":""incomplete"",""output"":[]}";
            var jsonl = MakeOpenAILine("sh-resp-001", 200, body);
            var status = OpenAIProvider.Instance.ParseBatchResultsFiles(new[] { jsonl });

            Assert.True(status.Results?.ContainsKey("sh-resp-001"));
            var warning = status.Messages?.SingleOrDefault(m => m.Code == SHMessageCode.BatchItemFinishReason);
            Assert.NotNull(warning);
            Assert.Contains("incomplete", warning.Message);
        }

        [Fact(DisplayName = "OpenAI_Responses_StatusCompleted_NoWarning")]
        public void OpenAI_Responses_StatusCompleted_NoWarning()
        {
            var body = @"{""id"":""resp-abc"",""object"":""response"",""status"":""completed"",""output"":[]}";
            var jsonl = MakeOpenAILine("sh-resp-002", 200, body);
            var status = OpenAIProvider.Instance.ParseBatchResultsFiles(new[] { jsonl });

            Assert.True(status.Results?.ContainsKey("sh-resp-002"));
            var finishWarnings = (status.Messages ?? new List<SHRuntimeMessage>())
                .Where(m => m.Code == SHMessageCode.BatchItemFinishReason)
                .ToList();
            Assert.Empty(finishWarnings);
        }

        // ────────────────────────────────────────────────────
        // Anthropic – JSONL
        // ────────────────────────────────────────────────────

        [Fact(DisplayName = "Anthropic_Succeeded_EndTurn_NoWarning")]
        public void Anthropic_Succeeded_EndTurn_NoWarning()
        {
            var jsonl = MakeAnthropicLine("sh-ant-001", "succeeded", stopReason: "end_turn");
            var status = AnthropicProvider.Instance.ParseBatchResultsFiles(new[] { jsonl });

            Assert.Equal(AIBatchState.Completed, status.State);
            Assert.True(status.Results?.ContainsKey("sh-ant-001"));
            Assert.Empty(status.Messages ?? new List<SHRuntimeMessage>());
        }

        [Fact(DisplayName = "Anthropic_Succeeded_MaxTokens_EmitsWarning")]
        public void Anthropic_Succeeded_MaxTokens_EmitsWarning()
        {
            var jsonl = MakeAnthropicLine("sh-ant-002", "succeeded", stopReason: "max_tokens");
            var status = AnthropicProvider.Instance.ParseBatchResultsFiles(new[] { jsonl });

            Assert.True(status.Results?.ContainsKey("sh-ant-002"));
            var warning = status.Messages?.SingleOrDefault(m => m.Code == SHMessageCode.BatchItemFinishReason);
            Assert.NotNull(warning);
            Assert.Equal(SHRuntimeMessageSeverity.Warning, warning.Severity);
            Assert.Contains("sh-ant-002", warning.Message);
            Assert.Contains("max_tokens", warning.Message);
        }

        [Fact(DisplayName = "Anthropic_Errored_EmitsError")]
        public void Anthropic_Errored_EmitsError()
        {
            var jsonl = MakeAnthropicLine("sh-ant-003", "errored", errorMsg: "Internal server error");
            var status = AnthropicProvider.Instance.ParseBatchResultsFiles(new[] { jsonl });

            Assert.False(status.Results?.ContainsKey("sh-ant-003") ?? false);
            var error = status.Messages?.SingleOrDefault(m => m.Code == SHMessageCode.BatchItemError);
            Assert.NotNull(error);
            Assert.Equal(SHRuntimeMessageSeverity.Error, error.Severity);
            Assert.Contains("sh-ant-003", error.Message);
        }

        [Fact(DisplayName = "Anthropic_Canceled_EmitsError")]
        public void Anthropic_Canceled_EmitsError()
        {
            var jsonl = MakeAnthropicLine("sh-ant-004", "canceled");
            var status = AnthropicProvider.Instance.ParseBatchResultsFiles(new[] { jsonl });

            Assert.False(status.Results?.ContainsKey("sh-ant-004") ?? false);
            var error = status.Messages?.SingleOrDefault(m => m.Code == SHMessageCode.BatchItemCanceled);
            Assert.NotNull(error);
            Assert.Equal(SHRuntimeMessageSeverity.Error, error.Severity);
            Assert.Contains("sh-ant-004", error.Message);
            Assert.Contains("canceled", error.Message, System.StringComparison.OrdinalIgnoreCase);
        }

        [Fact(DisplayName = "Anthropic_Expired_EmitsWarning")]
        public void Anthropic_Expired_EmitsWarning()
        {
            var jsonl = MakeAnthropicLine("sh-ant-005", "expired");
            var status = AnthropicProvider.Instance.ParseBatchResultsFiles(new[] { jsonl });

            Assert.False(status.Results?.ContainsKey("sh-ant-005") ?? false);
            var warning = status.Messages?.SingleOrDefault(m => m.Code == SHMessageCode.BatchItemExpired);
            Assert.NotNull(warning);
            Assert.Equal(SHRuntimeMessageSeverity.Warning, warning.Severity);
            Assert.Contains("sh-ant-005", warning.Message);
        }

        // ────────────────────────────────────────────────────
        // MistralAI – JSONL (same wire format as OpenAI Chat)
        // ────────────────────────────────────────────────────

        [Fact(DisplayName = "MistralAI_FinishReason_Stop_NoWarning")]
        public void MistralAI_FinishReason_Stop_NoWarning()
        {
            var jsonl = MakeOpenAILine("sh-mis-001", 200, MakeOpenAIChatBody("hello", "stop"));
            var status = MistralAIProvider.Instance.ParseBatchResultsFiles(new[] { jsonl });

            Assert.True(status.Results?.ContainsKey("sh-mis-001"));
            var finishWarnings = (status.Messages ?? new List<SHRuntimeMessage>())
                .Where(m => m.Code == SHMessageCode.BatchItemFinishReason)
                .ToList();
            Assert.Empty(finishWarnings);
        }

        [Fact(DisplayName = "MistralAI_FinishReason_Length_EmitsWarning")]
        public void MistralAI_FinishReason_Length_EmitsWarning()
        {
            var jsonl = MakeOpenAILine("sh-mis-002", 200, MakeOpenAIChatBody("truncated", "length"));
            var status = MistralAIProvider.Instance.ParseBatchResultsFiles(new[] { jsonl });

            Assert.True(status.Results?.ContainsKey("sh-mis-002"));
            var warning = status.Messages?.SingleOrDefault(m => m.Code == SHMessageCode.BatchItemFinishReason);
            Assert.NotNull(warning);
            Assert.Equal(SHRuntimeMessageSeverity.Warning, warning.Severity);
            Assert.Contains("sh-mis-002", warning.Message);
            Assert.Contains("length", warning.Message);
        }

        [Fact(DisplayName = "MistralAI_HttpError_EmitsError")]
        public void MistralAI_HttpError_EmitsError()
        {
            var errorBody = @"{""error"":{""message"":""Invalid request"",""type"":""invalid_request_error""}}";
            var jsonl = MakeOpenAILine("sh-mis-003", 400, errorBody);
            var status = MistralAIProvider.Instance.ParseBatchResultsFiles(new[] { jsonl });

            Assert.False(status.Results?.ContainsKey("sh-mis-003") ?? false);
            var error = status.Messages?.SingleOrDefault(m => m.Code == SHMessageCode.BatchItemError);
            Assert.NotNull(error);
            Assert.Equal(SHRuntimeMessageSeverity.Error, error.Severity);
        }

        // ────────────────────────────────────────────────────
        // Gemini – Operation JSON
        // ────────────────────────────────────────────────────

        [Fact(DisplayName = "Gemini_FinishReason_Stop_NoWarning")]
        public void Gemini_FinishReason_Stop_NoWarning()
        {
            var operationJson = MakeGeminiOperation(new[] { ("sh-gem-001", "STOP") });
            var status = GeminiProvider.Instance.ParseBatchResultsFiles(new[] { operationJson });

            Assert.True(status.Results?.ContainsKey("sh-gem-001"));
            var finishWarnings = (status.Messages ?? new List<SHRuntimeMessage>())
                .Where(m => m.Code == SHMessageCode.BatchItemFinishReason)
                .ToList();
            Assert.Empty(finishWarnings);
        }

        [Fact(DisplayName = "Gemini_FinishReason_MaxTokens_EmitsWarning")]
        public void Gemini_FinishReason_MaxTokens_EmitsWarning()
        {
            var operationJson = MakeGeminiOperation(new[] { ("sh-gem-002", "MAX_TOKENS") });
            var status = GeminiProvider.Instance.ParseBatchResultsFiles(new[] { operationJson });

            Assert.True(status.Results?.ContainsKey("sh-gem-002"));
            var warning = status.Messages?.SingleOrDefault(m => m.Code == SHMessageCode.BatchItemFinishReason);
            Assert.NotNull(warning);
            Assert.Equal(SHRuntimeMessageSeverity.Warning, warning.Severity);
            Assert.Contains("sh-gem-002", warning.Message);
            Assert.Contains("MAX_TOKENS", warning.Message);
        }

        [Fact(DisplayName = "Gemini_FinishReason_Safety_EmitsWarning")]
        public void Gemini_FinishReason_Safety_EmitsWarning()
        {
            var operationJson = MakeGeminiOperation(new[] { ("sh-gem-003", "SAFETY") });
            var status = GeminiProvider.Instance.ParseBatchResultsFiles(new[] { operationJson });

            var warning = status.Messages?.SingleOrDefault(m => m.Code == SHMessageCode.BatchItemFinishReason);
            Assert.NotNull(warning);
            Assert.Contains("SAFETY", warning.Message);
        }

        [Fact(DisplayName = "Gemini_ItemError_EmitsError")]
        public void Gemini_ItemError_EmitsError()
        {
            var operationJson = MakeGeminiOperationWithError("sh-gem-004", "Permission denied");
            var status = GeminiProvider.Instance.ParseBatchResultsFiles(new[] { operationJson });

            Assert.False(status.Results?.ContainsKey("sh-gem-004") ?? false);
            var error = status.Messages?.SingleOrDefault(m => m.Code == SHMessageCode.BatchItemError);
            Assert.NotNull(error);
            Assert.Equal(SHRuntimeMessageSeverity.Error, error.Severity);
            Assert.Contains("sh-gem-004", error.Message);
        }

        [Fact(DisplayName = "Gemini_MixedBatch_OneStop_OneSafety")]
        public void Gemini_MixedBatch_OneStop_OneSafety()
        {
            var operationJson = MakeGeminiOperation(new[]
            {
                ("sh-gem-ok", "STOP"),
                ("sh-gem-safe", "SAFETY"),
            });
            var status = GeminiProvider.Instance.ParseBatchResultsFiles(new[] { operationJson });

            Assert.True(status.Results?.ContainsKey("sh-gem-ok"));
            Assert.True(status.Results?.ContainsKey("sh-gem-safe"));

            var finishWarnings = (status.Messages ?? new List<SHRuntimeMessage>())
                .Where(m => m.Code == SHMessageCode.BatchItemFinishReason)
                .ToList();
            Assert.Single(finishWarnings);
            Assert.Contains("sh-gem-safe", finishWarnings[0].Message);
        }

        // ────────────────────────────────────────────────────
        // Cross-provider: empty input safety
        // ────────────────────────────────────────────────────

        [Fact(DisplayName = "OpenAI_EmptyFile_ReturnsFailed")]
        public void OpenAI_EmptyFile_ReturnsFailed()
        {
            var status = OpenAIProvider.Instance.ParseBatchResultsFiles(new string[0]);
            Assert.Equal(AIBatchState.Failed, status.State);
        }

        [Fact(DisplayName = "Anthropic_EmptyFile_ReturnsFailed")]
        public void Anthropic_EmptyFile_ReturnsFailed()
        {
            var status = AnthropicProvider.Instance.ParseBatchResultsFiles(new string[0]);
            Assert.Equal(AIBatchState.Failed, status.State);
        }

        [Fact(DisplayName = "MistralAI_EmptyFile_ReturnsFailed")]
        public void MistralAI_EmptyFile_ReturnsFailed()
        {
            var status = MistralAIProvider.Instance.ParseBatchResultsFiles(new string[0]);
            Assert.Equal(AIBatchState.Failed, status.State);
        }

        [Fact(DisplayName = "Gemini_EmptyFile_ReturnsFailed")]
        public void Gemini_EmptyFile_ReturnsFailed()
        {
            var status = GeminiProvider.Instance.ParseBatchResultsFiles(new string[0]);
            Assert.Equal(AIBatchState.Failed, status.State);
        }

        // ────────────────────────────────────────────────────
        // Helpers
        // ────────────────────────────────────────────────────

        /// <summary>Builds a single OpenAI/MistralAI JSONL line.</summary>
        private static string MakeOpenAILine(string customId, int statusCode, string bodyJson)
        {
            return $@"{{""custom_id"":""{customId}"",""response"":{{""status_code"":{statusCode},""body"":{bodyJson}}},""error"":null}}";
        }

        /// <summary>Builds an OpenAI Chat Completions response body with the given finish_reason.</summary>
        private static string MakeOpenAIChatBody(string content, string finishReason)
        {
            return $@"{{""id"":""chatcmpl-abc"",""object"":""chat.completion"",""choices"":[{{""index"":0,""message"":{{""role"":""assistant"",""content"":""{content}""}},""finish_reason"":""{finishReason}""}}],""usage"":{{""prompt_tokens"":10,""completion_tokens"":5,""total_tokens"":15}}}}";
        }

        /// <summary>Builds a single Anthropic JSONL line for the given result type.</summary>
        private static string MakeAnthropicLine(string customId, string resultType, string stopReason = null, string errorMsg = null)
        {
            if (resultType == "succeeded")
            {
                var sr = stopReason ?? "end_turn";
                return $@"{{""custom_id"":""{customId}"",""result"":{{""type"":""succeeded"",""message"":{{""id"":""msg_abc"",""type"":""message"",""role"":""assistant"",""stop_reason"":""{sr}"",""content"":[{{""type"":""text"",""text"":""hello""}}]}}}}}}";
            }

            if (resultType == "errored")
            {
                var msg = errorMsg ?? "Unknown error";
                return $@"{{""custom_id"":""{customId}"",""result"":{{""type"":""errored"",""error"":{{""type"":""server_error"",""message"":""{msg}""}}}}}}";
            }

            // canceled or expired — no extra payload
            return $@"{{""custom_id"":""{customId}"",""result"":{{""type"":""{resultType}""}}}}";
        }

        /// <summary>Builds a Gemini Operation JSON with one or more successful items, each with a finishReason.</summary>
        private static string MakeGeminiOperation((string customId, string finishReason)[] items)
        {
            var responses = items.Select(item =>
            {
                var resultJson = $@"{{""candidates"":[{{""content"":{{""role"":""model"",""parts"":[{{""text"":""hello""}}]}},""finishReason"":""{item.finishReason}""}}]}}";
                return $@"{{""metadata"":{{""custom_id"":""{item.customId}""}},""result"":{resultJson}}}";
            });
            var responsesArray = string.Join(",", responses);
            return $@"{{""name"":""operations/batch-abc"",""done"":true,""result"":{{""output"":{{""responses"":[{responsesArray}]}}}}}}";
        }

        /// <summary>Builds a Gemini Operation JSON with a single item-level error.</summary>
        private static string MakeGeminiOperationWithError(string customId, string errorMessage)
        {
            return $@"{{""name"":""operations/batch-abc"",""done"":true,""result"":{{""output"":{{""responses"":[{{""metadata"":{{""custom_id"":""{customId}""}},""error"":{{""message"":""{errorMessage}"",""code"":403}}}}]}}}}}}";
        }
    }
}
#endif
