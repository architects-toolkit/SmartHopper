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

namespace SmartHopper.Infrastructure.AICall.Sessions
{
    /// <summary>
    /// Immutable snapshot of an autonomous run's budget consumption: elapsed wall-clock time and
    /// provider-reported tokens versus the configured limits. A limit of zero or less means unlimited.
    /// </summary>
    public sealed class AutonomyUsage
    {
        /// <summary>
        /// Gets the elapsed wall-clock time of the current or last run, in seconds.
        /// </summary>
        public double ElapsedSeconds { get; init; }

        /// <summary>
        /// Gets the configured maximum autonomous time, in seconds. Zero or less means unlimited.
        /// </summary>
        public double MaxSeconds { get; init; }

        /// <summary>
        /// Gets the provider-reported tokens (input + output) consumed by the current or last run.
        /// </summary>
        public long Tokens { get; init; }

        /// <summary>
        /// Gets the configured maximum autonomous tokens. Zero or less means unlimited.
        /// </summary>
        public long MaxTokens { get; init; }

        /// <summary>
        /// Gets a value indicating whether <see cref="Tokens"/> includes heuristic estimates for calls
        /// where the provider did not report usage. The UI should display it as approximate.
        /// </summary>
        public bool TokensEstimated { get; init; }

        /// <summary>
        /// Gets a value indicating whether a run is currently in progress.
        /// </summary>
        public bool IsRunning { get; init; }

        /// <summary>
        /// Gets a value indicating whether the run stopped because a budget was exhausted.
        /// </summary>
        public bool IsExhausted { get; init; }
    }
}
