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

namespace SmartHopper.Infrastructure.AICall.Subagents
{
    /// <summary>
    /// Snapshot of a finished subagent run, delivered to the parent conversation's
    /// report sink (the web chat dialog) so the interaction can leave an evidence
    /// line and wake the conductor when it is idle.
    /// </summary>
    public sealed class SubagentRunReport : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SubagentRunReport"/> class.
        /// </summary>
        /// <param name="run">The run whose outcome this report carries.</param>
        /// <param name="subagentId">The subagent identifier.</param>
        /// <param name="label">The human-readable subagent label.</param>
        public SubagentRunReport(SubagentRun run, string subagentId, string label)
        {
            this.Run = run ?? throw new ArgumentNullException(nameof(run));
            this.SubagentId = subagentId;
            this.Label = label;
            this.Status = run.Status;
            this.Result = run.Result;
            this.Error = run.Error;
        }

        /// <summary>Gets the run whose outcome this report carries.</summary>
        public SubagentRun Run { get; }

        /// <summary>Gets the subagent identifier.</summary>
        public string SubagentId { get; }

        /// <summary>Gets the human-readable subagent label.</summary>
        public string Label { get; }

        /// <summary>Gets the terminal run status.</summary>
        public SubagentRunStatus Status { get; }

        /// <summary>Gets the status in the lowercase wire form used in transcript lines.</summary>
        public string StatusText => this.Status switch
        {
            SubagentRunStatus.Succeeded => "succeeded",
            SubagentRunStatus.Failed => "failed",
            _ => "running",
        };

        /// <summary>Gets the bounded result text (null when the run failed).</summary>
        public string? Result { get; }

        /// <summary>Gets the bounded error text (null when the run succeeded).</summary>
        public string? Error { get; }
    }
}
