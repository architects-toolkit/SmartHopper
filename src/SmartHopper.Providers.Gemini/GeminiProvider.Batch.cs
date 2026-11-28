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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SmartHopper.ProviderSdk.AICall.Batch;
using SmartHopper.ProviderSdk.AICall.Core.Requests;
using SmartHopper.ProviderSdk.Diagnostics;

using SmartHopper.ProviderSdk.Logging;
namespace SmartHopper.Providers.Gemini
{
    public sealed partial class GeminiProvider : IAIBatchProvider
    {
        /// <inheritdoc/>
        public async Task<AIBatchSubmission> SubmitBatchAsync(IReadOnlyList<(string CustomId, AIRequestCall Request)> items, CancellationToken cancellationToken = default)
        {
            if (items == null || items.Count == 0)
            {
                throw new ArgumentException("Items list cannot be null or empty", nameof(items));
            }

            var customIds = new List<string>();
            var inlinedRequests = new JArray();
            AIRequestCall firstPreparedRequest = null;

            foreach (var (customId, request) in items)
            {
                customIds.Add(customId);

                // Shared outbound pipeline: PreCall + outbound gate + Encode.
                // Items that bypassed Initialize()/preparation (e.g. direct
                // SubmitBatchAsync callers) fail with a clear validation error
                // instead of crashing on an empty EncodedRequestBody (D19).
                var (preparedRequest, requestBody) = await this.EncodeBatchItemAsync(request, cancellationToken).ConfigureAwait(false);
                firstPreparedRequest ??= preparedRequest;

                // InlinedRequest metadata echoes back as metadata.key on each result —
                // the API has no custom_id field on this surface.
                var inlinedRequest = new JObject
                {
                    { "request", requestBody },
                    { "metadata", new JObject { { "key", customId } } },
                };

                inlinedRequests.Add(inlinedRequest);
            }

            // Extract batch priority from first request if available
            var batchPriority = 0;
            if (items[0].Request.Parameters?.Extras != null && items[0].Request.Parameters.Extras.TryGetValue("batch_priority", out var priority) && priority != null && int.TryParse(priority.ToString(), out var priorityValue))
            {
                batchPriority = priorityValue;
            }

            // Build the batch request according to the Gemini Batch API contract:
            // POST {model}:batchGenerateContent takes {"batch": {displayName, inputConfig,
            // priority}} — the Batch resource wrapped in a top-level "batch" field.
            var batchBody = new JObject
            {
                {
                    "batch", new JObject
                    {
                        { "displayName", $"batch-{DateTime.UtcNow:yyyyMMddHHmmss}" },
                        {
                            "inputConfig", new JObject
                            {
                                {
                                    "requests", new JObject
                                    {
                                        { "requests", inlinedRequests },
                                    }
                                },
                            }
                        },
                    }
                },
            };

            var batchResource = batchBody["batch"] as JObject;

            if (batchPriority != 0)
            {
                batchResource["priority"] = batchPriority.ToString();
            }

            var serializedRequest = batchBody.ToString();

            try
            {
                var jobModel = firstPreparedRequest?.Model ?? items[0].Request.Model;
                var responseObj = await this.PostBatchRequestAsync($"/models/{jobModel}:batchGenerateContent", serializedRequest, cancellationToken).ConfigureAwait(false);
                var operationName = responseObj["name"]?.ToString();

                if (string.IsNullOrWhiteSpace(operationName))
                {
                    throw new InvalidOperationException("No operation name in batch submission response");
                }

                Logger.For<GeminiProvider>().Trace($"[{this.Name}] Batch submitted: {operationName}");

                return new AIBatchSubmission(operationName, this.Name, serializedRequest, customIds);
            }
            catch (Exception ex)
            {
                Logger.For<GeminiProvider>().Exception(ex, $"[{this.Name}] Batch submission error: {ex.Message}");
                throw;
            }
        }

        private async Task<JObject> PostBatchRequestAsync(string endpoint, string requestBody, CancellationToken cancellationToken)
        {
            using var client = this.CreateBatchHttpClient();
            var apiKey = this.GetApiKey();
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException($"{this.Name} API key is not configured or is invalid.");
            }

            client.DefaultRequestHeaders.Remove("x-goog-api-key");
            client.DefaultRequestHeaders.TryAddWithoutValidation("x-goog-api-key", apiKey);

            var uri = this.BuildFullUrl(endpoint);
            using var content = new StringContent(requestBody ?? "{}", System.Text.Encoding.UTF8, "application/json");
            using var response = await client.PostAsync(uri, content, cancellationToken).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"HTTP {(int)response.StatusCode}: {body}");
            }

            return JObject.Parse(body);
        }

        private async Task<JObject> SendBatchRequestAsync(HttpMethod method, string endpoint, CancellationToken cancellationToken)
        {
            using var client = this.CreateBatchHttpClient();
            var apiKey = this.GetApiKey();
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException($"{this.Name} API key is not configured or is invalid.");
            }

            client.DefaultRequestHeaders.TryAddWithoutValidation("x-goog-api-key", apiKey);

            var uri = this.BuildFullUrl(endpoint);
            using var req = new HttpRequestMessage(method, uri);
            using var response = await client.SendAsync(req, cancellationToken).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"HTTP {(int)response.StatusCode}: {body}");
            }

            return string.IsNullOrWhiteSpace(body) ? new JObject() : JObject.Parse(body);
        }

        /// <inheritdoc/>
        public async Task<AIBatchStatus> GetBatchStatusAsync(AIBatchSubmission submission, CancellationToken cancellationToken = default)
        {
            if (submission == null || string.IsNullOrWhiteSpace(submission.BatchId))
            {
                throw new ArgumentException("Submission with BatchId cannot be null or empty", nameof(submission));
            }

            try
            {
                // Use a direct HTTP GET instead of routing through the Call()/Decode() pipeline,
                // because batch status responses are Operation objects (name/done/result), not
                // generateContent responses with a candidates array.
                var operationObj = await this.SendBatchRequestAsync(HttpMethod.Get, $"/{submission.BatchId}", cancellationToken).ConfigureAwait(false);

                // Operation wire shape (generativelanguage v1beta): top-level name/done/
                // error/response; lifecycle state under metadata.state as BATCH_STATE_*.
                var done = operationObj["done"]?.Value<bool>() ?? false;
                if (!done)
                {
                    return new AIBatchStatus(submission.BatchId, AIBatchState.InProgress);
                }

                if (operationObj["error"] is JObject operationError)
                {
                    var state = operationObj["metadata"]?["state"]?.ToString();
                    var cancelled = string.Equals(state, "BATCH_STATE_CANCELLED", StringComparison.OrdinalIgnoreCase);
                    return new AIBatchStatus(
                        submission.BatchId,
                        cancelled ? AIBatchState.Cancelled : AIBatchState.Failed,
                        operationError["message"]?.ToString() ?? operationError.ToString());
                }

                var batchState = operationObj["metadata"]?["state"]?.ToString();
                if (string.Equals(batchState, "BATCH_STATE_FAILED", StringComparison.OrdinalIgnoreCase))
                {
                    return new AIBatchStatus(submission.BatchId, AIBatchState.Failed, "Batch job failed");
                }

                if (string.Equals(batchState, "BATCH_STATE_CANCELLED", StringComparison.OrdinalIgnoreCase))
                {
                    return new AIBatchStatus(submission.BatchId, AIBatchState.Cancelled);
                }

                if (string.Equals(batchState, "BATCH_STATE_EXPIRED", StringComparison.OrdinalIgnoreCase))
                {
                    return new AIBatchStatus(submission.BatchId, AIBatchState.Expired);
                }

                // Delegate download + parse to the interface methods.
                IReadOnlyList<string> files;
                try
                {
                    files = await this.DownloadBatchResultsAsync(submission, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    return new AIBatchStatus(submission.BatchId, AIBatchState.Failed, $"Failed to download batch results: {ex.Message}");
                }

                return this.ParseBatchResultsFiles(files, submission.BatchId);
            }
            catch (Exception ex)
            {
                Logger.For<GeminiProvider>().Exception(ex, $"[{this.Name}] Batch status error: {ex.Message}");
                return new AIBatchStatus(submission.BatchId, AIBatchState.Failed, ex.Message);
            }
        }

        /// <inheritdoc/>
        public async Task CancelBatchAsync(AIBatchSubmission submission, CancellationToken cancellationToken = default)
        {
            if (submission == null || string.IsNullOrWhiteSpace(submission.BatchId))
            {
                throw new ArgumentException("Submission with BatchId cannot be null or empty", nameof(submission));
            }

            try
            {
                await this.SendBatchRequestAsync(HttpMethod.Post, $"/{submission.BatchId}:cancel", cancellationToken).ConfigureAwait(false);
                Logger.For<GeminiProvider>().Trace($"[{this.Name}] Batch cancel response: success");
            }
            catch (Exception ex)
            {
                Logger.For<GeminiProvider>().Exception(ex, $"[{this.Name}] Batch cancel error: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<string>> DownloadBatchResultsAsync(AIBatchSubmission submission, CancellationToken cancellationToken = default)
        {
            if (submission == null || string.IsNullOrWhiteSpace(submission.BatchId))
            {
                throw new ArgumentException("Submission with BatchId cannot be null or empty", nameof(submission));
            }

            // Gemini's batch-mode "file" is the GenerateContentBatchOutput embedded in the
            // Operation JSON — fetch it directly instead of routing through Call(): the
            // operation status endpoint is a backoffice GET, not a generation request, so
            // it must not run the generation outbound gate (no body/model).
            var operationObj = await this.SendBatchRequestAsync(HttpMethod.Get, $"/{submission.BatchId}", cancellationToken).ConfigureAwait(false);
            var body = operationObj?.ToString();
            return string.IsNullOrWhiteSpace(body)
                ? Array.Empty<string>()
                : new[] { body };
        }

        /// <inheritdoc/>
        public AIBatchStatus ParseBatchResultsFiles(IReadOnlyList<string> fileContents, string batchId = null)
        {
            if (fileContents == null || fileContents.Count == 0)
            {
                return new AIBatchStatus(batchId, AIBatchState.Failed, "No file contents provided");
            }

            var merged = new Dictionary<string, JObject>();
            var messages = new List<SHRuntimeMessage>();

            foreach (var content in fileContents)
            {
                AIBatchStatusMerge.MergeInto(this.ParseSingleBatchResultFile(content, batchId), merged, messages);
            }

            return new AIBatchStatus(
                batchId,
                new System.Collections.ObjectModel.ReadOnlyDictionary<string, JObject>(merged),
                messages.Count > 0 ? messages.AsReadOnly() : null);
        }

        /// <summary>
        /// Parses a single Gemini batch operation JSON. REST shape:
        /// <c>{"done":true, "response":{"inlinedResponses":{"inlinedResponses":[{"metadata":{"key":"..."}, "response":{...}}]}}}</c>;
        /// the same array is mirrored at <c>metadata.output.inlinedResponses.inlinedResponses</c>.
        /// Item errors surface as <c>error</c> siblings of <c>response</c>.
        /// Finish reasons (e.g., "MAX_TOKENS") are extracted from successful responses and surfaced as warnings.
        /// </summary>
        private AIBatchStatus ParseSingleBatchResultFile(string content, string batchId)
        {
            var results = new Dictionary<string, JObject>();
            var messages = new List<SHRuntimeMessage>();

            if (string.IsNullOrWhiteSpace(content))
            {
                return new AIBatchStatus(batchId, results, messages);
            }

            JObject operationObj;
            try
            {
                operationObj = JObject.Parse(content);
            }
            catch (Exception ex)
            {
                messages.Add(new SHRuntimeMessage(
                    SHRuntimeMessageSeverity.Error,
                    SHRuntimeMessageOrigin.Provider,
                    SHMessageCode.BodyInvalid,
                    $"Failed to parse batch operation JSON: {ex.Message}"));
                return new AIBatchStatus(batchId, results, messages);
            }

            var responses =
                ((operationObj["response"] as JObject)?["inlinedResponses"] as JObject)?["inlinedResponses"] as JArray
                ?? (((operationObj["metadata"] as JObject)?["output"] as JObject)?["inlinedResponses"] as JObject)?["inlinedResponses"] as JArray;
            if (responses == null)
            {
                return new AIBatchStatus(batchId, results, messages);
            }

            foreach (var resp in responses.OfType<JObject>())
            {
                var metadata = resp["metadata"] as JObject;
                var customId = metadata?["key"]?.ToString();
                if (string.IsNullOrWhiteSpace(customId))
                {
                    continue;
                }

                // Inline error surface: per-item response.error — shared
                // extraction also catches scalar error payloads the previous
                // is-JObject guard silently dropped.
                var inlineErrorMsg = AIBatchResultParser.ExtractItemErrorMessage(resp["error"]);
                if (inlineErrorMsg != null)
                {
                    messages.Add(new SHRuntimeMessage(
                        SHRuntimeMessageSeverity.Error,
                        SHRuntimeMessageOrigin.Provider,
                        SHMessageCode.BatchItemError,
                        $"{AIBatchResultParser.ItemMessagePrefix}{customId}: {inlineErrorMsg}"));
                    continue;
                }

                var resultContent = resp["response"];
                if (resultContent == null)
                {
                    continue;
                }

                try
                {
                    var resultObj = resultContent is JObject jObj
                        ? jObj
                        : JObject.Parse(resultContent.ToString());
                    results[customId] = resultObj;

                    // Extract finishReason and surface as warning for non-STOP reasons
                    // Gemini uses camelCase finishReason in candidates array
                    var candidates = resultObj["candidates"] as JArray;
                    var firstCandidate = candidates?.FirstOrDefault() as JObject;
                    var finishReason = firstCandidate?["finishReason"]?.ToString();

                    if (!string.IsNullOrEmpty(finishReason) && finishReason != "STOP")
                    {
                        messages.Add(new SHRuntimeMessage(
                            SHRuntimeMessageSeverity.Warning,
                            SHRuntimeMessageOrigin.Provider,
                            SHMessageCode.BatchItemFinishReason,
                            $"Batch item {customId}: completed with finishReason='{finishReason}'"));
                    }
                }
                catch (Exception ex)
                {
                    messages.Add(new SHRuntimeMessage(
                        SHRuntimeMessageSeverity.Error,
                        SHRuntimeMessageOrigin.Provider,
                        SHMessageCode.BodyInvalid,
                        $"Failed to decode response for {customId}: {ex.Message}"));
                }
            }

            return new AIBatchStatus(batchId, results, messages);
        }
    }
}
