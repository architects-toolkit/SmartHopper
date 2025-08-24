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
        public bool ProcessTools { get; set; } = true;

        public int MaxTurns { get; set; } = 8;

        public int MaxToolPasses { get; set; } = 4;

        public bool AllowParallelTools { get; set; } = false;

        public CancellationToken CancellationToken { get; set; } = default;
    }
}
