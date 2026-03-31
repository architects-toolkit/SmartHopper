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
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SmartHopper.Infrastructure.AICall.Batch;
using SmartHopper.Infrastructure.AICall.Core.Requests;
using SmartHopper.Infrastructure.AICall.Core.Returns;

namespace SmartHopper.Providers.Google
{
    public partial class GoogleProvider : IAIBatchProvider
    {
        /// <inheritdoc/>
        public async Task<AIBatchSubmission> SubmitBatchAsync(List<AIRequestCall> requests)
        {
            if (requests == null || requests.Count == 0)
            {
                throw new ArgumentException("Requests list cannot be null or empty", nameof(requests));
            }

            var submission = new AIBatchSubmission
            {
                ProviderName = this.Name,
                SubmittedAt = DateTime.UtcNow,
            };

            var requestsArray = new JArray();

            foreach (var request in requests)
            {
                var customId = AIBatchSubmission.GenerateCustomId();
                submission.CustomIds.Add(customId);

                var batchRequest = new JObject
                {
                    { "custom_id", customId },
                    { "method", "POST" },
                    { "url", $"/models/{request.Model}:generateContent" },
                    { "body", JObject.Parse(request.EncodedRequestBody ?? "{}") },
                };

                if (request.Extras != null && request.Extras.TryGetValue("batch_priority", out var priority) && priority != null && int.TryParse(priority.ToString(), out var priorityValue) && priorityValue > 0)
                {
                    batchRequest["priority"] = priorityValue;
                }

                requestsArray.Add(batchRequest);
            }

            var batchBody = new JObject
            {
                { "requests", requestsArray },
            };

            submission.SerializedRequest = batchBody.ToString();

            try
            {
                var batchRequest = new AIRequestCall
                {
                    Endpoint = $"/models/{requests[0].Model}:batchGenerateContent",
                    HttpMethod = "POST",
                    EncodedRequestBody = batchBody.ToString(),
                    ContentType = "application/json",
                    Authentication = "x-goog-api-key",
                };

                var response = await this.CallApiAsync(batchRequest);

                if (!response.IsSuccess)
                {
                    throw new InvalidOperationException($"Failed to submit batch: {response.Body}");
                }

                var responseObj = JObject.Parse(response.Body);
                var operationName = responseObj["name"]?.ToString();

                if (string.IsNullOrWhiteSpace(operationName))
                {
                    throw new InvalidOperationException("No operation name in batch submission response");
                }

                submission.BatchId = operationName;

                Debug.WriteLine($"[{this.Name}] Batch submitted: {submission.BatchId}");

                return submission;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[{this.Name}] Batch submission error: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<AIBatchStatus> GetBatchStatusAsync(string batchId)
        {
            if (string.IsNullOrWhiteSpace(batchId))
            {
                throw new ArgumentException("Batch ID cannot be null or empty", nameof(batchId));
            }

            var status = new AIBatchStatus
            {
                BatchId = batchId,
            };

            try
            {
                var request = new AIRequestCall
                {
                    Endpoint = $"/{batchId}",
                    HttpMethod = "GET",
                    Authentication = "x-goog-api-key",
                };

                var response = await this.CallApiAsync(request);

                if (!response.IsSuccess)
                {
                    status.State = AIBatchState.Failed;
                    status.ErrorMessage = response.Body;
                    return status;
                }

                var responseObj = JObject.Parse(response.Body);
                var done = responseObj["done"]?.Value<bool>() ?? false;

                if (done)
                {
                    var result = responseObj["result"] as JObject;
                    if (result != null && result.ContainsKey("responses"))
                    {
                        status.State = AIBatchState.Completed;
                        var responses = result["responses"] as JArray;

                        if (responses != null)
                        {
                            foreach (var resp in responses.OfType<JObject>())
                            {
                                var customId = resp["custom_id"]?.ToString();
                                if (string.IsNullOrWhiteSpace(customId))
                                {
                                    continue;
                                }

                                status.CompletedCount++;

                                var body = resp["result"]?.ToString();
                                if (!string.IsNullOrWhiteSpace(body))
                                {
                                    try
                                    {
                                        var bodyObj = JObject.Parse(body);
                                        var decoded = this.Decode(bodyObj);
                                        status.Results[customId] = decoded;
                                    }
                                    catch (Exception ex)
                                    {
                                        status.Messages.Add(new AIBatchMessage
                                        {
                                            CustomId = customId,
                                            Level = "error",
                                            Content = $"Failed to decode response: {ex.Message}",
                                        });
                                    }
                                }
                            }
                        }
                    }
                    else if (result != null && result.ContainsKey("error"))
                    {
                        status.State = AIBatchState.Failed;
                        status.ErrorMessage = result["error"]?.ToString();
                    }
                }
                else
                {
                    status.State = AIBatchState.Processing;
                }

                return status;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[{this.Name}] Batch status error: {ex.Message}");
                status.State = AIBatchState.Failed;
                status.ErrorMessage = ex.Message;
                return status;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> CancelBatchAsync(string batchId)
        {
            if (string.IsNullOrWhiteSpace(batchId))
            {
                throw new ArgumentException("Batch ID cannot be null or empty", nameof(batchId));
            }

            try
            {
                var request = new AIRequestCall
                {
                    Endpoint = $"/{batchId}:cancel",
                    HttpMethod = "POST",
                    Authentication = "x-goog-api-key",
                };

                var response = await this.CallApiAsync(request);

                Debug.WriteLine($"[{this.Name}] Batch cancel response: {response.IsSuccess}");

                return response.IsSuccess;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[{this.Name}] Batch cancel error: {ex.Message}");
                return false;
            }
        }
    }
}
