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

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SmartHopper.Infrastructure.AICall.Subagents
{
    /// <summary>
    /// Per-session registry and lifecycle owner for conductor subagents. Sessions
    /// expose their host to conductor tools through
    /// <see cref="SmartHopper.Infrastructure.Consent.MutationInvocationContext.SubagentHost"/>;
    /// child sessions receive no host, which (together with the tool filter)
    /// enforces the depth cap of one level.
    /// </summary>
    public interface ISubagentHost
    {
        /// <summary>
        /// Fired when a run reaches a terminal state. The subscriber (the chat host)
        /// marks the transcript and wakes the conductor when no turn is active.
        /// </summary>
        event System.EventHandler<SubagentRunReport>? RunFinished;

        /// <summary>
        /// Fired whenever any worker's pending-instruction queue changes
        /// (enqueue, dequeue, edit, remove, promote) so the chat host can
        /// re-render the shared outgoing queue panel.
        /// </summary>
        event System.EventHandler? PendingChanged;

        /// <summary>Gets the number of runs still in flight.</summary>
        int RunningCount { get; }

        /// <summary>
        /// Gets every worker's pending instructions, flattened and ordered by
        /// enqueue time — the agent-injected entries of the outgoing queue panel.
        /// </summary>
        IReadOnlyList<SubagentPendingItem> PendingItems { get; }

        /// <summary>
        /// Spawns a new background subagent and starts its first run.
        /// </summary>
        Task<SubagentItem> SpawnAsync(string label, string brief, CancellationToken cancellationToken);

        /// <summary>
        /// Sends a follow-up instruction to an existing subagent. When the worker
        /// is mid-run, <paramref name="mode"/> selects between queueing the
        /// instruction for delivery when the run finishes and injecting it into
        /// the live session on the fly.
        /// </summary>
        Task<SubagentItem> SendAsync(string subagentId, string message, SubagentSendMode mode, CancellationToken cancellationToken);

        /// <summary>
        /// Applies a queue-panel action to a pending instruction identified by
        /// <paramref name="itemId"/>: <see cref="SubagentPendingAction.Remove"/>
        /// drops it, <see cref="SubagentPendingAction.Edit"/> replaces its text
        /// (<paramref name="text"/>), and <see cref="SubagentPendingAction.Promote"/>
        /// moves it to the head of its worker's queue.
        /// </summary>
        /// <returns>True when a pending instruction was found and mutated.</returns>
        bool TryApplyPendingAction(SubagentPendingAction action, string itemId, string? text);

        /// <summary>
        /// Blocks until the selected runs reach a terminal state or the timeout
        /// elapses. Runs whose outcome is returned here count as delivered and
        /// never trigger a wake turn.
        /// </summary>
        Task<IReadOnlyList<SubagentItem>> WaitAsync(IReadOnlyList<string>? subagentIds, int timeoutSeconds, CancellationToken cancellationToken);

        /// <summary>Lists the session's subagents with their latest run state.</summary>
        IReadOnlyList<SubagentItem> List();

        /// <summary>Cancels every in-flight run (dialog closing, session teardown).</summary>
        void CancelAll();
    }
}
