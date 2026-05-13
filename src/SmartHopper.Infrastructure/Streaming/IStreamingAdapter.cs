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

namespace SmartHopper.Infrastructure.Streaming
{
    using SmartHopper.Infrastructure.AICall.Core.Requests;
    using SmartHopper.Infrastructure.AICall.Core.Returns;

    /// <summary>
    /// Defines the contract for provider-specific streaming adapters that yield incremental AIReturn deltas.
    /// </summary>
    public interface IStreamingAdapter
    {
        /// <summary>
        /// Streams incremental AIReturn deltas for the given request.
        /// Implementations must honor cancellation and avoid unbounded buffering.
        /// </summary>
        /// <param name="request">The structured SmartHopper request.</param>
        /// <param name="options">Streaming options controlling coalescing/backpressure.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>An async sequence of AIReturn deltas suitable for incremental UI rendering.</returns>
        IAsyncEnumerable<AIReturn> StreamAsync(
            AIRequestCall request,
            StreamingOptions options,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Normalizes a provider-specific delta to a common format.
        /// Default implementation returns the delta unchanged.
        /// Override to handle provider-specific quirks (e.g., reasoning content, tool call formats).
        /// </summary>
        /// <param name="delta">The raw delta from the provider.</param>
        /// <returns>The normalized delta in a consistent format.</returns>
        AIReturn NormalizeDelta(AIReturn delta) => delta;
    }

    /// <summary>
    /// Options controlling streaming behavior such as token coalescing and buffering.
    /// </summary>
    public sealed class StreamingOptions
    {
        /// <summary>
        /// When true, tokens are coalesced into larger chunks using CoalesceDelayMs.
        /// </summary>
        public bool CoalesceTokens { get; set; } = true;

        /// <summary>
        /// Delay window in milliseconds to accumulate tokens before emitting a delta.
        /// </summary>
        public int CoalesceDelayMs { get; set; } = 40;

        /// <summary>
        /// Preferred chunk size in characters when coalescing tokens.
        /// </summary>
        public int PreferredChunkSize { get; set; } = 64;
    }
}
