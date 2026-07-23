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
using SmartHopper.ProviderSdk.AICall.Core.Requests;

namespace SmartHopper.ProviderSdk.AICall.Batch
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

        /// <summary>
        /// Downloads all provider-side artifacts produced by a terminal-state batch
        /// (e.g. success output file and error file, where applicable).
        /// Used internally by <see cref="GetBatchStatusAsync"/>; returned contents are
        /// passed to <see cref="ParseBatchResultsFiles"/> in the same order for parsing
        /// and merging.
        /// </summary>
        /// <param name="submission">The submission handle to download results for.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>
        /// Ordered list of raw file contents. Canonical order is success file(s) first,
        /// error file(s) last. Empty or missing files are omitted.
        /// </returns>
        Task<IReadOnlyList<string>> DownloadBatchResultsAsync(AIBatchSubmission submission, CancellationToken cancellationToken = default);

        /// <summary>
        /// Parses one or more raw batch-result file contents (in the order given) into a single
        /// merged <see cref="AIBatchStatus"/>. Each provider knows its own file format(s) —
        /// OpenAI/Mistral accept an output file and an optional error file, Anthropic a single
        /// results file, Gemini a single operation JSON.
        /// <para/>
        /// Called by:
        /// <list type="bullet">
        ///   <item><see cref="GetBatchStatusAsync"/> (after <see cref="DownloadBatchResultsAsync"/>) — <paramref name="batchId"/> is provided.</item>
        ///   <item>The component base when the user manually loads file(s) from disk — <paramref name="batchId"/> may be null.</item>
        /// </list>
        /// <para/>
        /// Implementations MUST:
        /// <list type="bullet">
        ///   <item>Parse each file independently (success + error lines both populate Results/Messages).</item>
        ///   <item>Merge with first-wins policy on duplicate <c>custom_id</c>.</item>
        ///   <item>Return a Completed <see cref="AIBatchStatus"/> (zero-results but non-empty Messages is valid).</item>
        /// </list>
        /// </summary>
        /// <param name="fileContents">Ordered list of raw file contents to parse and merge.</param>
        /// <param name="batchId">Optional batch identifier to stamp into the returned status.</param>
        /// <returns>A merged <see cref="AIBatchStatus"/>.</returns>
        AIBatchStatus ParseBatchResultsFiles(IReadOnlyList<string> fileContents, string batchId = null);
    }
}
