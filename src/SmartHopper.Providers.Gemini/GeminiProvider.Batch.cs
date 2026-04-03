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
using SmartHopper.Infrastructure.AICall.Batch;
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AICall.Core.Requests;
using SmartHopper.Infrastructure.AICall.Core.Returns;

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
            var requestsArray = new JArray();

            foreach (var (customId, request) in items)
            {
                customIds.Add(customId);

                var batchRequest = new JObject
                {
                    { "custom_id", customId },
                    { "method", "POST" },
                    { "url", $"/models/{request.Model}:generateContent" },
                    { "body", JObject.Parse(request.EncodedRequestBody ?? "{}") },
                };

                if (request.Parameters?.Extras != null && request.Parameters.Extras.TryGetValue("batch_priority", out var priority) && priority != null && int.TryParse(priority.ToString(), out var priorityValue) && priorityValue > 0)
                {
                    batchRequest["priority"] = priorityValue;
                }

                requestsArray.Add(batchRequest);
            }

            var batchBody = new JObject
            {
                { "requests", requestsArray },
            };

            var serializedRequest = batchBody.ToString();

            try
            {
                var firstRequest = items[0].Request;
                var responseObj = await this.PostBatchRequestAsync($"/models/{firstRequest.Model}:batchGenerateContent", serializedRequest, cancellationToken).ConfigureAwait(false);
                var operationName = responseObj["name"]?.ToString();

                if (string.IsNullOrWhiteSpace(operationName))
                {
                    throw new InvalidOperationException("No operation name in batch submission response");
                }

                Debug.WriteLine($"[{this.Name}] Batch submitted: {operationName}");

                return new AIBatchSubmission(operationName, this.Name, serializedRequest, customIds);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[{this.Name}] Batch submission error: {ex.Message}");
                throw;
            }
        }

        private async Task<JObject> PostBatchRequestAsync(string endpoint, string requestBody, CancellationToken cancellationToken)
        {
            using var client = new HttpClient();
            var apiKey = this.GetSetting<string>("ApiKey");
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

        /// <inheritdoc/>
        public async Task<AIBatchStatus> GetBatchStatusAsync(AIBatchSubmission submission, CancellationToken cancellationToken = default)
        {
            if (submission == null || string.IsNullOrWhiteSpace(submission.BatchId))
            {
                throw new ArgumentException("Submission with BatchId cannot be null or empty", nameof(submission));
            }

            try
            {
                var request = new AIRequestCall
                {
                    Endpoint = $"/{submission.BatchId}",
                    HttpMethod = "GET",
                    Authentication = "x-goog-api-key",
                };

                var response = await this.Call(request);

                var air = response as AIReturn;
                if (air == null)
                {
                    return new AIBatchStatus(submission.BatchId, AIBatchState.Failed, "Failed to get batch status");
                }

                if (!air.Success)
                {
                    return new AIBatchStatus(submission.BatchId, AIBatchState.Failed, string.Join(" | ", air.Messages.Select(m => m.Message)));
                }

                var responseBody = (air.Body?.Interactions?.FirstOrDefault(i => i is AIInteractionText) as AIInteractionText)?.Content ?? string.Empty;
                var responseObj = JObject.Parse(responseBody);
                var done = responseObj["done"]?.Value<bool>() ?? false;

                if (done)
                {
                    var result = responseObj["result"] as JObject;
                    if (result != null && result.ContainsKey("responses"))
                    {
                        var responses = result["responses"] as JArray;

                        if (responses != null)
                        {
                            var resultsDict = new Dictionary<string, JObject>();
                            var messages = new List<AIRuntimeMessage>();
                            int completedCount = 0;

                            foreach (var resp in responses.OfType<JObject>())
                            {
                                var customId = resp["custom_id"]?.ToString();
                                if (string.IsNullOrWhiteSpace(customId))
                                {
                                    continue;
                                }

                                completedCount++;

                                var body = resp["result"]?.ToString();
                                if (!string.IsNullOrWhiteSpace(body))
                                {
                                    try
                                    {
                                        var bodyObj = JObject.Parse(body);
                                        resultsDict[customId] = bodyObj;
                                    }
                                    catch (Exception ex)
                                    {
                                        messages.Add(new AIRuntimeMessage(
                                            AIRuntimeMessageSeverity.Error,
                                            AIRuntimeMessageOrigin.Provider,
                                            AIMessageCode.BodyInvalid,
                                            $"Failed to decode response for {customId}: {ex.Message}"));
                                    }
                                }
                            }

                            return new AIBatchStatus(submission.BatchId, resultsDict, messages);
                        }
                    }
                    else if (result != null && result.ContainsKey("error"))
                    {
                        return new AIBatchStatus(submission.BatchId, AIBatchState.Failed, result["error"]?.ToString());
                    }
                }

                return new AIBatchStatus(submission.BatchId, AIBatchState.InProgress);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[{this.Name}] Batch status error: {ex.Message}");
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
                var request = new AIRequestCall
                {
                    Endpoint = $"/{submission.BatchId}:cancel",
                    HttpMethod = "POST",
                    Authentication = "x-goog-api-key",
                };

                var response = await this.Call(request);

                var air = response as AIReturn;
                var cancelMessage = air == null || air.Success
                    ? "success"
                    : string.Join(" | ", air.Messages.Select(m => m.Message));
                Debug.WriteLine($"[{this.Name}] Batch cancel response: {cancelMessage}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[{this.Name}] Batch cancel error: {ex.Message}");
            }
        }
    }
}
