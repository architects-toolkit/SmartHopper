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

using System.Threading;
using System.Threading.Tasks;
using SmartHopper.Infrastructure.AICall.Core.Requests;

namespace SmartHopper.Infrastructure.AICall.Batch
{
    /// <summary>
    /// Optional interface implemented by AI providers that support asynchronous batch processing.
    /// When a component detects <c>service_tier=batch</c> (or equivalent) in the request parameters,
    /// it calls <see cref="SubmitBatchAsync"/> and then polls <see cref="GetBatchStatusAsync"/>
    /// at configurable intervals until the batch completes.
    /// </summary>
    public interface IAIBatchProvider
    {
        /// <summary>
        /// Submits a request as a batch job and returns a submission handle.
        /// The provider encodes the request body using its normal <c>Encode</c> method,
        /// then posts it to the provider's batch submission endpoint.
        /// </summary>
        /// <param name="request">The fully-specified AI request to submit as a batch.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A <see cref="AIBatchSubmission"/> containing the provider-assigned batch ID.</returns>
        Task<AIBatchSubmission> SubmitBatchAsync(AIRequestCall request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Polls the provider for the current status of a previously submitted batch.
        /// Returns a <see cref="AIBatchStatus"/> with <see cref="AIBatchState.Completed"/>
        /// and populated <see cref="AIBatchStatus.ResultBody"/> when results are available.
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
