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
        /// Tool passes and provider turns are bounded by <see cref="MaxAutonomousTime"/> and
        /// <see cref="MaxAutonomousTokens"/>.
        /// When false, the session also hides every tool from the provider for the duration of the run
        /// (tool filter <c>-*</c>), so no tool call can be emitted that would then be left without a result.
        /// </summary>
        public bool ProcessTools { get; set; } = true;

        /// <summary>
        /// Maximum wall-clock time an autonomous run may consume, measured from run start across all
        /// provider turns and tool passes. A value less than or equal to <see cref="TimeSpan.Zero"/>
        /// disables the time budget.
        /// </summary>
        public TimeSpan MaxAutonomousTime { get; set; } = TimeSpan.FromMinutes(10);

        /// <summary>
        /// Maximum total tokens (provider-reported input + output) an autonomous run may consume across
        /// all provider calls. A value less than or equal to zero disables the token budget.
        /// </summary>
        public long MaxAutonomousTokens { get; set; } = 300_000;

        public bool AllowParallelTools { get; set; }

        public CancellationToken CancellationToken { get; set; }
    }
}
