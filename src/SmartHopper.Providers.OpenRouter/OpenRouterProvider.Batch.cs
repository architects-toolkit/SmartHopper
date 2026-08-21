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
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SmartHopper.ProviderSdk.AICall.Batch;
using SmartHopper.ProviderSdk.AICall.Core.Requests;
using SmartHopper.ProviderSdk.Diagnostics;

namespace SmartHopper.Providers.OpenRouter
{
    public sealed partial class OpenRouterProvider : IAIBatchProvider
    {
        /// <summary>
        /// Builds an absolute URL for the OpenRouter beta batch endpoint.
        /// The batch API lives under <c>/api/beta/batches</c>, which is not under
        /// the provider's default <c>/api/v1</c> base path.
        /// </summary>
        /// <param name="relativePath">Path relative to <c>https://openrouter.ai/</c>, without a leading slash.</param>
        /// <returns>The absolute batch URL.</returns>
        private Uri BuildBatchUrl(string relativePath)
        {
            var authority = this.DefaultServerUrl.GetLeftPart(UriPartial.Authority);
            var baseUri = new Uri(authority + "/", UriKind.Absolute);
            return new Uri(baseUri, relativePath);
        }

        /// <summary>
        /// Strips the OpenRouter <c>:batch</c> suffix from a model slug.
        /// This keeps old definitions and user inputs that selected the
        /// <c>:batch</c> alias working with both chat and batch endpoints.
        /// </summary>
        /// <param name="model">The model slug, possibly ending with <c>:batch</c>.</param>
        /// <returns>The base model slug without a <c>:batch</c> suffix.</returns>
        private static string StripBatchSuffix(string model)
        {
            if (string.IsNullOrWhiteSpace(model))
            {
                return model;
            }

            if (model.EndsWith(":batch", StringComparison.OrdinalIgnoreCase))
            {
                return model.Substring(0, model.Length - 6);
            }

            return model;
        }

        /// <summary>
        /// Maps an internal request endpoint to the shape expected by the
        /// OpenRouter batch API. The batch endpoint must start with <c>/v1/</c>.
        /// </summary>
        /// <param name="endpoint">The endpoint from <see cref="PreCall(AIRequestCall)"/>.</param>
        /// <returns>The batch-level endpoint string.</returns>
        private static string ToBatchEndpoint(string endpoint)
        {
            if (string.IsNullOrWhiteSpace(endpoint))
            {
                return "/v1/chat/completions";
            }

            var normalized = endpoint.TrimStart('/');

            if (endpoint.StartsWith("/v1/", StringComparison.OrdinalIgnoreCase))
            {
                return endpoint;
            }

            switch (normalized.ToLowerInvariant())
            {
                case "chat/completions":
                    return "/v1/chat/completions";
                case "responses":
                    return "/v1/responses";
                case "messages":
                    return "/v1/messages";
                case "embeddings":
                    return "/v1/embeddings";
                default:
                    return "/v1/" + normalized;
            }
        }

        /// <summary>
        /// Validates that a batch request body only contains text content.
        /// OpenRouter's batch API currently rejects image, audio, video and file
        /// parts, as well as non-text output requests.
        /// </summary>
        /// <param name="body">The encoded request body.</param>
        /// <exception cref="InvalidOperationException">Thrown when the request is not text-only.</exception>
        private static void ValidateBatchTextOnly(JObject body)
        {
            if (body == null)
            {
                return;
            }

            var messages = body["messages"] as JArray;
            if (messages != null)
            {
                foreach (var message in messages.OfType<JObject>())
                {
                    var content = message["content"];
                    if (content is JArray parts)
                    {
                        foreach (var part in parts.OfType<JObject>())
                        {
                            var type = part["type"]?.ToString() ?? string.Empty;
                            if (string.Equals(type, "image_url", StringComparison.OrdinalIgnoreCase)
                                || string.Equals(type, "audio_url", StringComparison.OrdinalIgnoreCase)
                                || string.Equals(type, "video_url", StringComparison.OrdinalIgnoreCase)
                                || string.Equals(type, "file", StringComparison.OrdinalIgnoreCase))
                            {
                                throw new InvalidOperationException($"[OpenRouter] Batch requests do not support '{type}' content. Use the synchronous API for multimodal requests.");
                            }

                            if (part["image_url"] != null
                                || part["audio_url"] != null
                                || part["video_url"] != null
                                || part["file"] != null)
                            {
                                throw new InvalidOperationException("[OpenRouter] Batch requests do not support multimodal content. Use the synchronous API for multimodal requests.");
                            }
                        }
                    }
                }
            }

            if (body["modalities"] != null
                || body["audio"] != null
                || body["image_config"] != null)
            {
                throw new InvalidOperationException("[OpenRouter] Batch requests do not support non-text output configurations (modalities, audio, image_config).");
            }
        }

        /// <inheritdoc/>
        public async Task<AIBatchSubmission> SubmitBatchAsync(IReadOnlyList<(string CustomId, AIRequestCall Request)> items, CancellationToken cancellationToken = default)
        {
            if (items == null || items.Count == 0)
            {
                throw new ArgumentException("At least one batch item is required.", nameof(items));
            }

            string? batchEndpoint = null;
            string? batchModel = null;
            var requestArray = new JArray();
            var customIds = new List<string>();
            string? firstEncodedBody = null;

            foreach (var (customId, request) in items)
            {
                // Normalize :batch alias to the base model slug before encoding.
                request.Model = StripBatchSuffix(request.Model);

                var preparedRequest = this.PreCall(request);
                var encodedBody = this.Encode(preparedRequest);
                if (firstEncodedBody == null)
                {
                    firstEncodedBody = encodedBody;
                }

                var bodyObj = JObject.Parse(encodedBody);

                // OpenRouter batch is text-only and uses one endpoint/model for the whole batch.
                ValidateBatchTextOnly(bodyObj);

                var endpoint = ToBatchEndpoint(preparedRequest.Endpoint);
                if (batchEndpoint == null)
                {
                    batchEndpoint = endpoint;
                }
                else if (!string.Equals(batchEndpoint, endpoint, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"[OpenRouter] All batch items must use the same endpoint. Expected '{batchEndpoint}', found '{endpoint}'.");
                }

                if (batchModel == null)
                {
                    batchModel = preparedRequest.Model;
                }
                else if (!string.Equals(batchModel, preparedRequest.Model, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"[OpenRouter] All batch items must use the same model. Expected '{batchModel}', found '{preparedRequest.Model}'.");
                }

                var requestItem = new JObject
                {
                    ["custom_id"] = customId,
                    ["body"] = bodyObj,
                };
                requestArray.Add(requestItem);
                customIds.Add(customId);
            }

            // OpenRouter streams the input and requires endpoint/model to appear
            // before the (potentially very large) requests array.
            var batchBody = new JObject
            {
                ["endpoint"] = batchEndpoint,
                ["model"] = batchModel,
                ["completion_window"] = "24h",
                ["requests"] = requestArray,
            };

            var apiKey = this.GetApiKey();
            using var client = this.CreateBatchHttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            client.DefaultRequestHeaders.Add("X-Title", "SmartHopper");
            client.DefaultRequestHeaders.Add("Referer", "https://smarthopper.xyz");

            var batchUrl = this.BuildBatchUrl("api/beta/batches");
            var content = new StringContent(batchBody.ToString(Formatting.None), Encoding.UTF8, "application/json");
            var response = await client.PostAsync(batchUrl, content, cancellationToken).ConfigureAwait(false);
            var responseString = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!response.IsSuccessStatusCode && (int)response.StatusCode != 202)
            {
                throw new InvalidOperationException($"[OpenRouter] Batch submission failed ({(int)response.StatusCode}): {responseString}");
            }

            var result = JObject.Parse(responseString);
            var batchId = result["id"]?.ToString()
                ?? throw new InvalidOperationException("[OpenRouter] Batch submission response missing 'id'.");

            Debug.WriteLine($"[OpenRouter] Batch submitted: batchId={batchId}, count={items.Count}");

            return new AIBatchSubmission(batchId, this.Name, firstEncodedBody ?? string.Empty, (IReadOnlyList<string>)customIds.AsReadOnly());
        }

        /// <inheritdoc/>
        public async Task<AIBatchStatus> GetBatchStatusAsync(AIBatchSubmission submission, CancellationToken cancellationToken = default)
        {
            if (submission == null)
            {
                throw new ArgumentNullException(nameof(submission));
            }

            var json = await this.GetBatchJsonAsync(submission.BatchId, cancellationToken).ConfigureAwait(false);
            if (json == null)
            {
                return new AIBatchStatus(submission.BatchId, AIBatchState.Failed, "OpenRouter batch status response was empty.");
            }

            var status = json["status"]?.ToString() ?? string.Empty;
            Debug.WriteLine($"[OpenRouter] Batch status check: id={submission.BatchId}, status={status}");

            switch (status.ToLowerInvariant())
            {
                case "validating":
                case "in_progress":
                case "finalizing":
                {
                    var requestCounts = json["request_counts"] as JObject;
                    int? completedCount = requestCounts?["completed"]?.Value<int?>();
                    return new AIBatchStatus(submission.BatchId, AIBatchState.InProgress, completedCount: completedCount);
                }

                case "completed":
                {
                    var results = json["results"];
                    if (results == null || !results.HasValues)
                    {
                        return new AIBatchStatus(submission.BatchId, AIBatchState.Failed, "Batch completed but no results were returned.");
                    }

                    var parsed = this.ParseBatchResultsFiles(new[] { results.ToString(Formatting.None) }, submission.BatchId);
                    if ((parsed.Results?.Count ?? 0) == 0 && (parsed.Messages?.Count ?? 0) == 0)
                    {
                        return new AIBatchStatus(submission.BatchId, AIBatchState.Failed, "No results found in completed batch.");
                    }

                    return parsed;
                }

                case "failed":
                    return new AIBatchStatus(submission.BatchId, AIBatchState.Failed, json["error"]?.ToString() ?? string.Empty);

                case "expired":
                    return new AIBatchStatus(submission.BatchId, AIBatchState.Expired);

                case "cancelling":
                case "cancelled":
                    return new AIBatchStatus(submission.BatchId, AIBatchState.Cancelled);

                default:
                    return new AIBatchStatus(submission.BatchId, AIBatchState.Submitted);
            }
        }

        /// <inheritdoc/>
        public async Task CancelBatchAsync(AIBatchSubmission submission, CancellationToken cancellationToken = default)
        {
            if (submission == null)
            {
                throw new ArgumentNullException(nameof(submission));
            }

            var apiKey = this.GetApiKey();
            using var client = this.CreateBatchHttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            client.DefaultRequestHeaders.Add("X-Title", "SmartHopper");
            client.DefaultRequestHeaders.Add("Referer", "https://smarthopper.xyz");

            var cancelUrl = this.BuildBatchUrl($"api/beta/batches/{submission.BatchId}/cancel");
            var response = await client.PostAsync(cancelUrl, null, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                Debug.WriteLine($"[OpenRouter] Batch cancel failed ({(int)response.StatusCode}): {content}");
            }
            else
            {
                Debug.WriteLine($"[OpenRouter] Batch {submission.BatchId} cancel accepted.");
            }
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<string>> DownloadBatchResultsAsync(AIBatchSubmission submission, CancellationToken cancellationToken = default)
        {
            if (submission == null)
            {
                throw new ArgumentNullException(nameof(submission));
            }

            var json = await this.GetBatchJsonAsync(submission.BatchId, cancellationToken).ConfigureAwait(false);
            if (json == null)
            {
                return Array.Empty<string>();
            }

            var results = json["results"];
            if (results != null && results.HasValues)
            {
                return new[] { results.ToString(Formatting.None) };
            }

            return Array.Empty<string>();
        }

        /// <inheritdoc/>
        public AIBatchStatus ParseBatchResultsFiles(IReadOnlyList<string> fileContents, string batchId = null)
        {
            if (fileContents == null || fileContents.Count == 0)
            {
                return new AIBatchStatus(batchId, AIBatchState.Failed, "No batch result contents provided.");
            }

            var merged = new Dictionary<string, JObject>();
            var messages = new List<SHRuntimeMessage>();

            foreach (var content in fileContents)
            {
                AIBatchStatusMerge.MergeInto(ParseSingleBatchResultFile(content, batchId), merged, messages);
            }

            return new AIBatchStatus(
                batchId,
                new System.Collections.ObjectModel.ReadOnlyDictionary<string, JObject>(merged),
                messages.AsReadOnly());
        }

        /// <summary>
        /// Fetches the raw JSON for a batch from the OpenRouter status endpoint.
        /// </summary>
        /// <param name="batchId">The OpenRouter batch id.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The parsed batch object, or null if the request failed.</returns>
        private async Task<JObject?> GetBatchJsonAsync(string batchId, CancellationToken cancellationToken)
        {
            var apiKey = this.GetApiKey();
            using var client = this.CreateBatchHttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            client.DefaultRequestHeaders.Add("X-Title", "SmartHopper");
            client.DefaultRequestHeaders.Add("Referer", "https://smarthopper.xyz");

            var statusUrl = this.BuildBatchUrl($"api/beta/batches/{batchId}");
            var response = await client.GetAsync(statusUrl, cancellationToken).ConfigureAwait(false);
            var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                Debug.WriteLine($"[OpenRouter] Batch status fetch failed ({(int)response.StatusCode}): {content}");
                return null;
            }

            return JObject.Parse(content);
        }

        /// <summary>
        /// Parses a single OpenRouter batch result payload. The payload can be the
        /// inline <c>results</c> array returned by the status endpoint, or a
        /// previously saved JSON array of result items.
        /// </summary>
        /// <param name="content">JSON content containing the results array (or a batch object with a <c>results</c> property).</param>
        /// <param name="batchId">Optional batch id for diagnostics.</param>
        /// <returns>A parsed <see cref="AIBatchStatus"/> for the single file.</returns>
        private static AIBatchStatus ParseSingleBatchResultFile(string content, string batchId)
        {
            var results = new Dictionary<string, JObject>();
            var messages = new List<SHRuntimeMessage>();

            if (string.IsNullOrWhiteSpace(content))
            {
                return new AIBatchStatus(batchId, results, messages);
            }

            JArray resultArray;
            var token = JToken.Parse(content);
            if (token is JArray array)
            {
                resultArray = array;
            }
            else if (token is JObject obj && obj["results"] is JArray nested)
            {
                resultArray = nested;
            }
            else
            {
                messages.Add(new SHRuntimeMessage(
                    SHRuntimeMessageSeverity.Error,
                    SHRuntimeMessageOrigin.Provider,
                    SHMessageCode.BatchItemError,
                    "Batch result payload is not a results array or a batch object with a results array."));
                return new AIBatchStatus(batchId, results, messages);
            }

            foreach (var item in resultArray.OfType<JObject>())
            {
                var customId = item["custom_id"]?.ToString();
                if (string.IsNullOrWhiteSpace(customId))
                {
                    continue;
                }

                var error = item["error"];
                if (error != null)
                {
                    var errorMsg = error["message"]?.ToString() ?? error.ToString();
                    messages.Add(new SHRuntimeMessage(
                        SHRuntimeMessageSeverity.Error,
                        SHRuntimeMessageOrigin.Provider,
                        SHMessageCode.BatchItemError,
                        $"Batch item {customId}: {errorMsg}"));
                    continue;
                }

                var responseObj = item["response"] as JObject;
                var statusCode = responseObj?["status_code"]?.Value<int>() ?? 0;
                var responseBody = responseObj?["body"] as JObject;

                if (statusCode >= 200 && statusCode < 300 && responseBody != null)
                {
                    results[customId] = responseBody;

                    // Surface non-standard finish reasons as warnings.
                    var choices = responseBody["choices"] as JArray;
                    var firstChoice = choices?.FirstOrDefault() as JObject;
                    var finishReason = firstChoice?["finish_reason"]?.ToString()
                        ?? responseBody["status"]?.ToString();

                    if (!string.IsNullOrEmpty(finishReason) && finishReason != "stop")
                    {
                        messages.Add(new SHRuntimeMessage(
                            SHRuntimeMessageSeverity.Warning,
                            SHRuntimeMessageOrigin.Provider,
                            SHMessageCode.BatchItemFinishReason,
                            $"Batch item {customId}: completed with finish_reason='{finishReason}'"));
                    }
                }
                else
                {
                    var errorText = responseBody?["error"]?.ToString()
                        ?? (statusCode > 0 ? $"HTTP {statusCode}" : "Unknown batch item error");
                    messages.Add(new SHRuntimeMessage(
                        SHRuntimeMessageSeverity.Error,
                        SHRuntimeMessageOrigin.Provider,
                        SHMessageCode.BatchItemError,
                        $"Batch item {customId}: {errorText}"));
                }
            }

            return new AIBatchStatus(batchId, results, messages);
        }
    }
}
