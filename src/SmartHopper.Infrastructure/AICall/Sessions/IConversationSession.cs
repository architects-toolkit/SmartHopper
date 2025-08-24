/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using System.Threading;
using System.Threading.Tasks;

namespace SmartHopper.Infrastructure.AICall.Sessions
{
    using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AICall.Core.Requests;
using SmartHopper.Infrastructure.AICall.Core.Returns;
using SmartHopper.Infrastructure.AICall.Tools;

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
        /// Attempts to cancel the session.
        /// </summary>
        void Cancel();
    }
}

