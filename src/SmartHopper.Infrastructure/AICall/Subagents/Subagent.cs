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
using System.Collections.Generic;
using SmartHopper.Infrastructure.AICall.Sessions;

namespace SmartHopper.Infrastructure.AICall.Subagents
{
    /// <summary>
    /// A persistent child worker of a conductor session: its own
    /// <see cref="ConversationSession"/> plus the list of runs it executed.
    /// </summary>
    public sealed class Subagent
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Subagent"/> class.
        /// </summary>
        /// <param name="label">The human-readable label.</param>
        /// <param name="session">The child conversation session.</param>
        public Subagent(string label, ConversationSession session)
        {
            this.Label = label;
            this.Session = session;
        }

        /// <summary>Gets the subagent identifier.</summary>
        public string Id { get; } = Guid.NewGuid().ToString("N");

        /// <summary>Gets the human-readable label.</summary>
        public string Label { get; }

        /// <summary>Gets the child conversation session (its own short history).</summary>
        public ConversationSession Session { get; }

        /// <summary>Gets the latest run, or null before the first launch.</summary>
        public SubagentRun? LatestRun => this.Runs.Count > 0 ? this.Runs[this.Runs.Count - 1] : null;

        /// <summary>Gets the runs executed so far, oldest first.</summary>
        internal List<SubagentRun> Runs { get; } = new List<SubagentRun>();

        /// <summary>
        /// Gets the queued instructions waiting for the current run to finish,
        /// oldest first. Enqueued by <c>send_to_subagent</c> while a run is in
        /// flight; the host dequeues the head into a new run at turn settle.
        /// Mutated only under the host gate.
        /// </summary>
        internal List<SubagentPendingItem> PendingInstructions { get; } = new List<SubagentPendingItem>();
    }
}
