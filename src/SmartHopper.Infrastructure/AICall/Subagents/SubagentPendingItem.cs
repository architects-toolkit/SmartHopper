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
    /// One instruction waiting in a subagent's pending queue: enqueued by
    /// <c>send_to_subagent</c> while the worker is mid-run (or when an
    /// on-the-fly delivery was unsafe) and delivered as a new run when the
    /// current run reaches a terminal state. The chat host projects these into
    /// the shared outgoing queue panel keyed by <see cref="Id"/>.
    /// </summary>
    public sealed class SubagentPendingItem
    {
        /// <summary>Gets the pending-instruction identifier (queue-panel row key).</summary>
        public string Id { get; init; } = Guid.NewGuid().ToString("N");

        /// <summary>Gets the owning subagent identifier.</summary>
        public string SubagentId { get; init; } = string.Empty;

        /// <summary>Gets the owning subagent's human-readable label.</summary>
        public string Label { get; init; } = string.Empty;

        /// <summary>Gets or sets the instruction text delivered as the next run.</summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>Gets when the instruction was queued.</summary>
        public DateTime EnqueuedAtUtc { get; init; } = DateTime.UtcNow;
    }
}
