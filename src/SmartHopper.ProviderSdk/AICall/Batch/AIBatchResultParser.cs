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

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Newtonsoft.Json.Linq;
using SmartHopper.ProviderSdk.Diagnostics;

namespace SmartHopper.ProviderSdk.AICall.Batch
{
    /// <summary>
    /// Shared parser for the OpenAI-style batch result wire shape:
    /// <c>{ "custom_id": "…", "response": { "status_code": N, "body": {…} }, "error": {…} }</c>
    /// items inside a JSON array or an object carrying a <c>results</c> array.
    /// Providers that consume this shape share the extraction so item semantics —
    /// first-wins merge, per-item error messages and finish-reason warnings — cannot
    /// drift apart.
    /// </summary>
    public static class AIBatchResultParser
    {
        /// <summary>
        /// Parses and merges every content blob in <paramref name="fileContents"/> into a single
        /// completed <see cref="AIBatchStatus"/>. Duplicate <c>custom_id</c> entries keep the
        /// first occurrence (first-wins); messages always append.
        /// </summary>
        /// <param name="fileContents">Ordered list of raw result payloads.</param>
        /// <param name="batchId">Optional batch identifier stamped into the returned status.</param>
        /// <returns>The merged <see cref="AIBatchStatus"/>.</returns>
        public static AIBatchStatus ParseResultContents(IReadOnlyList<string> fileContents, string batchId)
        {
            var merged = new Dictionary<string, JObject>();
            var messages = new List<SHRuntimeMessage>();

            if (fileContents != null)
            {
                foreach (var content in fileContents)
                {
                    AIBatchStatusMerge.MergeInto(ParseResultContent(content, batchId), merged, messages);
                }
            }

            return new AIBatchStatus(
                batchId,
                new ReadOnlyDictionary<string, JObject>(merged),
                messages.AsReadOnly());
        }

        /// <summary>
        /// Parses a single result payload: a JSON array of result items, or a JSON
        /// object with a <c>results</c> array (e.g. a batch status wire object).
        /// </summary>
        /// <param name="content">Raw JSON content.</param>
        /// <param name="batchId">Optional batch identifier for diagnostics.</param>
        /// <returns>A parsed <see cref="AIBatchStatus"/> for the single payload.</returns>
        public static AIBatchStatus ParseResultContent(string content, string batchId)
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
                AddResultItem(item, results, messages);
            }

            return new AIBatchStatus(batchId, results, messages);
        }

        /// <summary>
        /// Extracts one <c>{custom_id, response|error}</c> result item into the
        /// accumulators: 2xx bodies land in <paramref name="results"/> keyed by
        /// <c>custom_id</c> (with finish-reason warnings), everything else becomes a
        /// provider-origin error message.
        /// </summary>
        /// <param name="item">The raw result item.</param>
        /// <param name="results">Accumulator keyed by <c>custom_id</c>.</param>
        /// <param name="messages">Accumulator for diagnostic messages.</param>
        public static void AddResultItem(JObject item, IDictionary<string, JObject> results, IList<SHRuntimeMessage> messages)
        {
            if (item == null)
            {
                return;
            }

            if (results == null)
            {
                throw new System.ArgumentNullException(nameof(results));
            }

            if (messages == null)
            {
                throw new System.ArgumentNullException(nameof(messages));
            }

            var customId = item["custom_id"]?.ToString();
            if (string.IsNullOrWhiteSpace(customId))
            {
                return;
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
                return;
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
    }
}
