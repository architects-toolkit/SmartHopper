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

namespace SmartHopper.Infrastructure.AITools
{
    /// <summary>
    /// Lifecycle states of one tool execution that may own a canvas commit.
    /// The <see cref="ToolExecutionState.Queued"/> → <see cref="ToolExecutionState.Applying"/>
    /// transition is the single atomic claim on the commit: before it, cancellation
    /// ends the operation with zero side effects; after it, the operation owns its
    /// outcome and callers must wait for a terminal state.
    /// </summary>
    public enum ToolExecutionState
    {
        /// <summary>Inputs/arguments are being resolved; no UI admission yet.</summary>
        Preparing,

        /// <summary>A consent/review decision is pending.</summary>
        Reviewing,

        /// <summary>Queued for the document/UI commit path; not yet started.</summary>
        Queued,

        /// <summary>The commit was claimed and is being applied.</summary>
        Applying,

        /// <summary>The commit was applied.</summary>
        Applied,

        /// <summary>The commit failed and was fully compensated (no partial state).</summary>
        FailedCompensated,

        /// <summary>The commit failed partway; some effects may remain (honest partial).</summary>
        FailedPartial,

        /// <summary>Cancelled before the commit was claimed — no side effects.</summary>
        CancelledNotStarted,

        /// <summary>Rejected by consent before the commit was claimed — no side effects.</summary>
        Rejected,

        /// <summary>Validation failed before the commit was claimed — no side effects.</summary>
        Invalid,
    }

    /// <summary>
    /// Host-independent lifecycle for one tool execution. Carried on
    /// <see cref="Consent.MutationInvocationContext.Lifecycle"/> so the MCP adapter,
    /// the UI admission executor and the commit path share one ownership truth:
    /// whether the operation may still have side effects in flight.
    /// </summary>
    public sealed class ToolExecutionLifecycle
    {
        private int state = (int)ToolExecutionState.Preparing;

        /// <summary>Gets the unique operation identifier (used in MCP <c>operation_in_progress</c> payloads).</summary>
        public string OperationId { get; } = Guid.NewGuid().ToString("N");

        /// <summary>Gets the current state.</summary>
        public ToolExecutionState State => (ToolExecutionState)Interlocked.CompareExchange(ref this.state, 0, 0);

        /// <summary>
        /// Gets a value indicating whether the commit was claimed
        /// (<see cref="ToolExecutionState.Applying"/> was reached at least once).
        /// </summary>
        public bool CommitClaimed =>
            this.State == ToolExecutionState.Applying
            || this.State == ToolExecutionState.Applied
            || this.State == ToolExecutionState.FailedCompensated
            || this.State == ToolExecutionState.FailedPartial;

        /// <summary>Gets a value indicating whether the lifecycle reached a terminal state.</summary>
        public bool IsTerminal
        {
            get
            {
                switch (this.State)
                {
                    case ToolExecutionState.Applied:
                    case ToolExecutionState.FailedCompensated:
                    case ToolExecutionState.FailedPartial:
                    case ToolExecutionState.CancelledNotStarted:
                    case ToolExecutionState.Rejected:
                    case ToolExecutionState.Invalid:
                        return true;
                    default:
                        return false;
                }
            }
        }

        /// <summary>
        /// Gets whether the operation may still have side effects in flight: the
        /// commit was claimed and no terminal state was recorded yet.
        /// </summary>
        public bool MayBeApplying => this.State == ToolExecutionState.Applying || (this.CommitClaimed && !this.IsTerminal);

        /// <summary>
        /// Attempts a forward transition. Only <paramref name="expected"/> →
        /// <paramref name="next"/> succeeds, so out-of-order writes are ignored.
        /// </summary>
        /// <returns><c>true</c> when the transition was applied.</returns>
        public bool TryTransition(ToolExecutionState expected, ToolExecutionState next)
        {
            return Interlocked.CompareExchange(ref this.state, (int)next, (int)expected) == (int)expected;
        }

        /// <summary>
        /// Attempts the atomic <see cref="ToolExecutionState.Queued"/> →
        /// <see cref="ToolExecutionState.Applying"/> commit claim. Only one caller
        /// (the UI admission callback) may win; queued cancellation/deadline paths
        /// compete with this transition and lose once it lands.
        /// </summary>
        /// <returns><c>true</c> when the commit was claimed.</returns>
        public bool TryClaimCommit()
        {
            return this.TryTransition(ToolExecutionState.Queued, ToolExecutionState.Applying);
        }

        /// <summary>
        /// Cancels the operation while it has not claimed the commit. Transitions
        /// <see cref="ToolExecutionState.Preparing"/>, <see cref="ToolExecutionState.Reviewing"/>
        /// or <see cref="ToolExecutionState.Queued"/> to
        /// <see cref="ToolExecutionState.CancelledNotStarted"/>; after the commit was
        /// claimed the call is ignored — started work owns its outcome.
        /// </summary>
        /// <returns><c>true</c> when the operation was cancelled before starting.</returns>
        public bool TryCancelBeforeStart()
        {
            while (true)
            {
                var current = this.State;
                if (current != ToolExecutionState.Preparing
                    && current != ToolExecutionState.Reviewing
                    && current != ToolExecutionState.Queued)
                {
                    return false;
                }

                if (Interlocked.CompareExchange(ref this.state, (int)ToolExecutionState.CancelledNotStarted, (int)current) == (int)current)
                {
                    return true;
                }
            }
        }

        /// <summary>
        /// Records a terminal state from <see cref="ToolExecutionState.Applying"/>.
        /// Terminal writes are single-shot; a second terminal write is ignored.
        /// </summary>
        /// <returns><c>true</c> when the terminal state was recorded.</returns>
        public bool TryComplete(ToolExecutionState terminal)
        {
            if (terminal != ToolExecutionState.Applied
                && terminal != ToolExecutionState.FailedCompensated
                && terminal != ToolExecutionState.FailedPartial)
            {
                return false;
            }

            return this.TryTransition(ToolExecutionState.Applying, terminal);
        }

        /// <summary>
        /// Marks the operation as rejected before the commit (consent denial or
        /// argument invalidation). Ignored once the commit was claimed.
        /// </summary>
        /// <returns><c>true</c> when the terminal state was recorded.</returns>
        public bool TryReject(ToolExecutionState terminal)
        {
            if (terminal != ToolExecutionState.Rejected && terminal != ToolExecutionState.Invalid)
            {
                return false;
            }

            while (true)
            {
                var current = this.State;
                if (current == ToolExecutionState.Applying || this.CommitClaimed || this.IsTerminal)
                {
                    return false;
                }

                if (Interlocked.CompareExchange(ref this.state, (int)terminal, (int)current) == (int)current)
                {
                    return true;
                }
            }
        }
    }
}
