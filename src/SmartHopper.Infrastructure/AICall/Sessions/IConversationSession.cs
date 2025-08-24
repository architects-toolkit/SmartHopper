/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

namespace SmartHopper.Infrastructure.AICall.Sessions
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using SmartHopper.Infrastructure.AICall.Core.Requests;
    using SmartHopper.Infrastructure.AICall.Core.Returns;
    using SmartHopper.Infrastructure.Streaming;

    /// <summary>
    /// Contract for conversation session orchestrator. Minimal non-streaming API.
    /// </summary>
    public interface IConversationSession
    {
        /// <summary>
        /// Gets the underlying request used by this session.
        /// </summary>
        AIRequestCall Request { get; }

        /// <summary>
        /// Runs the session to a stable result (no further tool calls to execute).
        /// </summary>
        Task<AIReturn> RunToStableResult(SessionOptions options, CancellationToken cancellationToken = default);

        /// <summary>
        /// Streams incremental returns for live rendering. Implementations may use a provider-specific
        /// IStreamingAdapter when available; otherwise they should yield non-streaming fallbacks.
        /// </summary>
        /// <param name="options">Session options controlling turns and tool processing.</param>
        /// <param name="streamingOptions">Streaming behavior options (coalescing/backpressure).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Async sequence of AIReturn deltas.</returns>
        IAsyncEnumerable<AIReturn> Stream(SessionOptions options, StreamingOptions streamingOptions, CancellationToken cancellationToken = default);

        /// <summary>
        /// Attempts to cancel the session.
        /// </summary>
        void Cancel();
    }
}
