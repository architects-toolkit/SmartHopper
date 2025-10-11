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

namespace SmartHopper.Infrastructure.AICall.Sessions
{
    /// <summary>
    /// Options controlling conversation session execution.
    /// </summary>
    public sealed class SessionOptions
    {
        /// <summary>
        /// When true, the session will execute pending tool calls and allow providers to call tools.
        /// Tool passes are bounded by <see cref="MaxToolPasses"/> and do not consume <see cref="MaxTurns"/>.
        /// </summary>
        public bool ProcessTools { get; set; } = true;

        /// <summary>
        /// Maximum number of provider turns. Only provider calls increment this counter; tool passes do not.
        /// </summary>
        public int MaxTurns { get; set; } = 8;

        /// <summary>
        /// Maximum number of tool-processing passes per turn. Tool passes are consumed by tool execution
        /// (e.g., <c>ProcessPendingToolsAsync</c>) and do not affect <see cref="MaxTurns"/>.
        /// </summary>
        public int MaxToolPasses { get; set; } = 4;

        public bool AllowParallelTools { get; set; }

        public CancellationToken CancellationToken { get; set; }
    }
}
