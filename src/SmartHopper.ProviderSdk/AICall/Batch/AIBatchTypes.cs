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
using System.Collections.ObjectModel;
using Newtonsoft.Json.Linq;
using SmartHopper.ProviderSdk.AICall.Core.Base;
using SmartHopper.ProviderSdk.AICall.Core.Requests;
using SmartHopper.ProviderSdk.Diagnostics;

namespace SmartHopper.ProviderSdk.AICall.Batch
{
    /// <summary>
    /// Possible states of a submitted batch job.
    /// </summary>
    public enum AIBatchState
    {
        /// <summary>Batch has been submitted and is queued for processing.</summary>
        Submitted,

        /// <summary>Batch is actively being processed by the provider.</summary>
        InProgress,

        /// <summary>Batch has completed successfully; results are available.</summary>
        Completed,

        /// <summary>Batch failed before producing any output.</summary>
        Failed,

        /// <summary>Batch was cancelled by the user or provider.</summary>
        Cancelled,

        /// <summary>Batch has expired (provider time limit exceeded).</summary>
        Expired,
    }

    /// <summary>
    /// Represents the submission handle returned after a batch job is accepted by the provider.
    /// </summary>
    public sealed class AIBatchSubmission
    {
        /// <summary>Gets the provider-assigned batch identifier.</summary>
        public string BatchId { get; }

        /// <summary>Gets the provider name that owns this batch.</summary>
        public string ProviderName { get; }

        /// <summary>Gets the UTC timestamp when the batch was submitted.</summary>
        public DateTimeOffset SubmittedAt { get; }

        /// <summary>Gets the serialized request body that was submitted (for persistence/diagnostics).</summary>
        public string SerializedRequest { get; }

        /// <summary>
        /// Gets all SmartHopper-generated custom IDs for the items in this batch.
        /// Format: <c>sh-{yyyyMMddHHmmss}-{endpoint}-{NN}-{random8}</c>.
        /// </summary>
        public IReadOnlyList<string> CustomIds { get; }

        /// <summary>
        /// Gets the first custom ID, for single-item backward compatibility and file persistence.
        /// Returns null if <see cref="CustomIds"/> is empty.
        /// </summary>
        public string CustomId => this.CustomIds?.Count > 0 ? this.CustomIds[0] : null;

        /// <summary>
        /// Initializes a new multi-item <see cref="AIBatchSubmission"/>.
        /// </summary>
        public AIBatchSubmission(string batchId, string providerName, string serializedRequest, IReadOnlyList<string> customIds)
        {
            this.BatchId = batchId ?? throw new ArgumentNullException(nameof(batchId));
            this.ProviderName = providerName ?? throw new ArgumentNullException(nameof(providerName));
            this.SubmittedAt = DateTimeOffset.UtcNow;
            this.SerializedRequest = serializedRequest;
            this.CustomIds = customIds ?? new List<string>().AsReadOnly();
        }

        /// <summary>
        /// Initializes a new single-item <see cref="AIBatchSubmission"/> (used during file reload).
        /// </summary>
        public AIBatchSubmission(string batchId, string providerName, string serializedRequest, string customId)
            : this(batchId, providerName, serializedRequest, string.IsNullOrEmpty(customId) ? (IReadOnlyList<string>)new List<string>().AsReadOnly() : new ReadOnlyCollection<string>(new[] { customId }))
        {
        }

        /// <summary>
        /// Generates a SmartHopper custom ID for a batch item.
        /// Format: <c>sh-{yyyyMMddHHmmss}-{endpoint}-{NN:00}-{random8}</c>.
        /// </summary>
        /// <param name="endpoint">Tool endpoint name (e.g. "text2text"). Defaults to "req" if null.</param>
        /// <param name="index">Zero-based index of this item within the batch.</param>
        /// <returns>A unique custom ID for batch request tracking.</returns>
        public static string GenerateCustomId(string endpoint = null, int index = 0)
        {
            var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss");
            var ep = string.IsNullOrWhiteSpace(endpoint) ? "req" : endpoint.Replace("_", "-").ToLowerInvariant();
            var idx = index.ToString("D2");
            var random = Guid.NewGuid().ToString("N").Substring(0, 8);
            return $"sh-{timestamp}-{ep}-{idx}-{random}";
        }
    }

    /// <summary>
    /// Represents the current status of a submitted batch job, including results if completed.
    /// </summary>
    public sealed class AIBatchStatus
    {
        /// <summary>Gets the current state of the batch.</summary>
        public AIBatchState State { get; }

        /// <summary>Gets the provider-assigned batch identifier.</summary>
        public string BatchId { get; }

        /// <summary>Gets an optional human-readable error message (set when State is Failed).</summary>
        public string ErrorMessage { get; }

        /// <summary>Gets the UTC timestamp of this status check.</summary>
        public DateTimeOffset CheckedAt { get; } = DateTimeOffset.UtcNow;

        /// <summary>
        /// Gets the number of items completed so far.
        /// Populated for <see cref="AIBatchState.InProgress"/> states when the provider reports it.
        /// Null when unknown.
        /// </summary>
        public int? CompletedCount { get; }

        /// <summary>
        /// Gets a dictionary mapping each <c>customId</c> to its decoded provider response body.
        /// Populated when <see cref="State"/> is <see cref="AIBatchState.Completed"/>.
        /// </summary>
        public IReadOnlyDictionary<string, JObject> Results { get; }

        /// <summary>
        /// Gets item-level diagnostic messages emitted by the provider during batch result parsing.
        /// Each message carries a <see cref="SHRuntimeMessageSeverity"/> (Error / Warning / Info),
        /// origin <see cref="SHRuntimeMessageOrigin.Provider"/>, and a human-readable text.
        /// Populated when <see cref="State"/> is <see cref="AIBatchState.Completed"/> and some items
        /// errored, were canceled, or expired.
        /// </summary>
        public IReadOnlyList<SHRuntimeMessage> Messages { get; }

        /// <summary>Initializes a non-completed status.</summary>
        /// <param name="batchId">Provider batch identifier.</param>
        /// <param name="state">Current batch state.</param>
        /// <param name="errorMessage">Optional error message for failed states.</param>
        /// <param name="completedCount">Number of items completed so far (for in-progress reporting).</param>
        public AIBatchStatus(string batchId, AIBatchState state, string errorMessage = null, int? completedCount = null)
        {
            this.BatchId = batchId;
            this.State = state;
            this.ErrorMessage = errorMessage;
            this.CompletedCount = completedCount;
        }

        /// <summary>Initializes a completed status with results and optional item-level messages.</summary>
        /// <param name="batchId">Provider batch identifier.</param>
        /// <param name="results">Successful result bodies keyed by <c>customId</c>.</param>
        /// <param name="messages">Optional item-level diagnostic messages (errors, warnings, info).</param>
        public AIBatchStatus(string batchId, IReadOnlyDictionary<string, JObject> results, IReadOnlyList<SHRuntimeMessage> messages = null)
        {
            this.BatchId = batchId;
            this.State = AIBatchState.Completed;
            this.Results = results;
            this.Messages = messages ?? Array.Empty<SHRuntimeMessage>();
        }
    }

    /// <summary>
    /// Internal merge helper used by batch providers to combine multiple parsed
    /// single-file <see cref="AIBatchStatus"/> values (e.g. OpenAI's output + error
    /// files, or multiple user-selected files in manual load) into accumulator
    /// collections with a first-wins policy on <c>custom_id</c>.
    /// </summary>
    public static class AIBatchStatusMerge
    {
        /// <summary>
        /// Merges one parsed single-file <see cref="AIBatchStatus"/> into accumulator collections.
        /// First-wins on <c>custom_id</c>; <see cref="AIBatchStatus.Messages"/> are always appended.
        /// </summary>
        /// <param name="source">The status to merge in. Null is a no-op.</param>
        /// <param name="destResults">Accumulator dictionary keyed by <c>custom_id</c>.</param>
        /// <param name="destMessages">Accumulator list for diagnostic messages.</param>
        public static void MergeInto(
            AIBatchStatus source,
            IDictionary<string, JObject> destResults,
            IList<SHRuntimeMessage> destMessages)
        {
            if (source == null)
            {
                return;
            }

            if (source.Results != null && destResults != null)
            {
                foreach (var kvp in source.Results)
                {
                    if (!destResults.ContainsKey(kvp.Key))
                    {
                        destResults[kvp.Key] = kvp.Value;
                    }
                }
            }

            if (source.Messages != null && destMessages != null)
            {
                foreach (var m in source.Messages)
                {
                    destMessages.Add(m);
                }
            }
        }
    }
}
