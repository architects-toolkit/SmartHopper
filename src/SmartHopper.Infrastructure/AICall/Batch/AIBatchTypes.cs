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
using Newtonsoft.Json.Linq;
using SmartHopper.Infrastructure.AICall.Core.Requests;

namespace SmartHopper.Infrastructure.AICall.Batch
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

        /// <summary>Gets the serialized request that was submitted.</summary>
        public string SerializedRequest { get; }

        /// <summary>
        /// Gets the SmartHopper-generated custom ID used to identify this request
        /// in provider batch outputs. Format: <c>sh-{timestamp:yyyyMMdd}-{random}</c>.
        /// </summary>
        public string CustomId { get; }

        /// <summary>
        /// Initializes a new <see cref="AIBatchSubmission"/>.
        /// </summary>
        public AIBatchSubmission(string batchId, string providerName, string serializedRequest, string customId = null)
        {
            this.BatchId = batchId ?? throw new ArgumentNullException(nameof(batchId));
            this.ProviderName = providerName ?? throw new ArgumentNullException(nameof(providerName));
            this.SubmittedAt = DateTimeOffset.UtcNow;
            this.SerializedRequest = serializedRequest;
            this.CustomId = customId ?? GenerateCustomId();
        }

        /// <summary>
        /// Generates a new SmartHopper custom ID.
        /// Format: <c>sh-{timestamp:yyyyMMdd}-{8-char-random}</c>.
        /// </summary>
        /// <returns>A unique custom ID for batch request tracking.</returns>
        public static string GenerateCustomId()
        {
            var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd");
            var random = Guid.NewGuid().ToString("N").Substring(0, 8);
            return $"sh-{timestamp}-{random}";
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

        /// <summary>Gets the raw provider response body (used to decode results).</summary>
        public JObject ResultBody { get; }

        /// <summary>Gets an optional human-readable error message (set when State is Failed).</summary>
        public string ErrorMessage { get; }

        /// <summary>Gets the UTC timestamp of this status check.</summary>
        public DateTimeOffset CheckedAt { get; } = DateTimeOffset.UtcNow;

        /// <summary>Initializes a non-completed status.</summary>
        public AIBatchStatus(string batchId, AIBatchState state, string errorMessage = null)
        {
            this.BatchId = batchId;
            this.State = state;
            this.ErrorMessage = errorMessage;
        }

        /// <summary>Initializes a completed status with result data.</summary>
        public AIBatchStatus(string batchId, JObject resultBody)
        {
            this.BatchId = batchId;
            this.State = AIBatchState.Completed;
            this.ResultBody = resultBody;
        }
    }
}
