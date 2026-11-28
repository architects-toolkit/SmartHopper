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
    /// Public state of one subagent as returned by <c>list_subagents</c> and
    /// <c>wait_subagents</c> to the model.
    /// </summary>
    public sealed class SubagentItem
    {
        /// <summary>Gets the subagent identifier.</summary>
        public string SubagentId { get; init; } = string.Empty;

        /// <summary>Gets the human-readable label.</summary>
        public string Label { get; init; } = string.Empty;

        /// <summary>Gets the latest run identifier.</summary>
        public string? RunId { get; init; }

        /// <summary>Gets the latest run status (running, succeeded, failed).</summary>
        public string Status { get; init; } = string.Empty;

        /// <summary>Gets the latest run's result text, when succeeded.</summary>
        public string? Result { get; init; }

        /// <summary>Gets the latest run's error text, when failed.</summary>
        public string? Error { get; init; }

        /// <summary>Gets the instruction that produced the latest run (bounded).</summary>
        public string LastInstruction { get; init; } = string.Empty;

        /// <summary>Gets when the latest run finished, or null while running.</summary>
        public DateTime? FinishedAt { get; init; }

        /// <summary>
        /// Gets the number of instructions still queued for this subagent — they
        /// deliver as new runs, oldest first, once the current run finishes.
        /// </summary>
        public int PendingCount { get; init; }

        /// <summary>
        /// Gets how the instruction that produced this item was delivered:
        /// <c>"started"</c> (a new run launched immediately), <c>"queued"</c>
        /// (held in the pending queue until the current run finishes), or
        /// <c>"steered"</c> (injected into the live run on the fly). Null for
        /// items not produced by <c>send_to_subagent</c> (list/wait calls).
        /// </summary>
        public string? Delivery { get; init; }
    }
}
