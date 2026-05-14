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
using System.Threading;
using System.Threading.Tasks;
using SmartHopper.Infrastructure.AICall.Core.Requests;

namespace SmartHopper.Infrastructure.AICall.Batch
{
    /// <summary>
    /// Optional interface implemented by AI providers that support asynchronous batch processing.
    /// When a component has <c>BatchTier=true</c> in its request parameters, all per-item tool calls
    /// are collected into a queue and submitted as a single batch HTTP request via
    /// <see cref="SubmitBatchAsync"/>. Status is polled via <see cref="GetBatchStatusAsync"/>.
    /// </summary>
    public interface IAIBatchProvider
    {
        /// <summary>
        /// Submits multiple requests as a single batch job and returns a submission handle.
        /// The provider encodes each request using its normal <c>Encode</c> method and posts
        /// all of them in one HTTP call to the provider's batch endpoint.
        /// </summary>
        /// <param name="items">
        /// Ordered list of <c>(CustomId, Request)</c> pairs. Each <c>CustomId</c> is a unique
        /// SmartHopper-generated identifier (format: <c>sh-{timestamp}-{endpoint}-{NN}-{random8}</c>)
        /// used to correlate results from the provider's JSONL output.
        /// </param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>
        /// A <see cref="AIBatchSubmission"/> containing the provider-assigned batch ID and
        /// all submitted custom IDs.
        /// </returns>
        Task<AIBatchSubmission> SubmitBatchAsync(IReadOnlyList<(string CustomId, AIRequestCall Request)> items, CancellationToken cancellationToken = default);

        /// <summary>
        /// Polls the provider for the current status of a previously submitted batch.
        /// Returns a <see cref="AIBatchStatus"/> with <see cref="AIBatchState.Completed"/>
        /// and populated <see cref="AIBatchStatus.Results"/> when results are available.
        /// </summary>
        /// <param name="submission">The submission handle returned by <see cref="SubmitBatchAsync"/>.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The current <see cref="AIBatchStatus"/>.</returns>
        Task<AIBatchStatus> GetBatchStatusAsync(AIBatchSubmission submission, CancellationToken cancellationToken = default);

        /// <summary>
        /// Cancels a submitted batch job. Best-effort; providers may not support cancellation.
        /// </summary>
        /// <param name="submission">The submission handle to cancel.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task CancelBatchAsync(AIBatchSubmission submission, CancellationToken cancellationToken = default);
    }
}
