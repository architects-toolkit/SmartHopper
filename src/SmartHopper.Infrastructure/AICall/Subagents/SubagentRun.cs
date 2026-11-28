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
using System.Threading.Tasks;

namespace SmartHopper.Infrastructure.AICall.Subagents
{
    /// <summary>
    /// One execution of a subagent — the equivalent of cloud's SubagentRun row,
    /// kept in memory because plugin sessions are ephemeral.
    /// </summary>
    public sealed class SubagentRun
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SubagentRun"/> class.
        /// </summary>
        /// <param name="instruction">The instruction that produced this run.</param>
        public SubagentRun(string instruction)
        {
            this.Instruction = instruction;
        }

        /// <summary>Gets the run identifier.</summary>
        public string Id { get; } = Guid.NewGuid().ToString("N");

        /// <summary>Gets the instruction that produced this run (bounded).</summary>
        public string Instruction { get; }

        /// <summary>Gets or sets the run status.</summary>
        public SubagentRunStatus Status { get; set; } = SubagentRunStatus.Running;

        /// <summary>Gets or sets the bounded result text.</summary>
        public string? Result { get; set; }

        /// <summary>Gets or sets the bounded error text.</summary>
        public string? Error { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the run's outcome reached the
        /// conductor — via <c>wait_subagents</c> collecting it inline or via the
        /// wake turn consuming it. Undelivered terminal runs are what the wake
        /// queue drains; matches cloud's <c>SubagentRun.deliveredAt</c>.
        /// </summary>
        public bool Delivered { get; set; }

        /// <summary>Gets when the run was created.</summary>
        public DateTime CreatedAt { get; } = DateTime.UtcNow;

        /// <summary>Gets or sets when the run reached a terminal state.</summary>
        public DateTime? FinishedAt { get; set; }

        /// <summary>Gets whether the run reached a terminal state.</summary>
        public bool IsTerminal => this.Status != SubagentRunStatus.Running;

        /// <summary>Gets the background task executing the run.</summary>
        internal Task? Work { get; set; }
    }
}
