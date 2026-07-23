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
using SmartHopper.ProviderSdk.AICall.Batch;
using SmartHopper.ProviderSdk.AICall.Core.Requests;
using SmartHopper.ProviderSdk.AICall.Metrics;

namespace SmartHopper.Core.ComponentBase.Cores
{
    /// <summary>
    /// Mutable per-run state for the batch / metrics machinery on
    /// <see cref="AIStatefulAsyncComponentBase"/>. Bundles the nine fields that
    /// previously lived directly on the base class so their lifecycle is governed
    /// by two explicit methods (<see cref="ResetForNextRun"/> and
    /// <see cref="ResetForRestoration"/>) instead of an open-coded 17-line block
    /// in <c>OnEnteringNeedsRun</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Field categories</b> (drives reset semantics):
    /// </para>
    /// <list type="bullet">
    ///   <item>
    ///     <b>Cross-run survivors</b>: <see cref="Submission"/> and
    ///     <see cref="SentinelTrees"/>. An in-flight remote batch must keep
    ///     polling and reconstructing its tree even when the user toggles the Run
    ///     input or edits an upstream value, so neither is touched by
    ///     <see cref="ResetForNextRun"/>.
    ///   </item>
    ///   <item>
    ///     <b>Per-run scratch</b>: <see cref="Queue"/>,
    ///     <see cref="SentinelIds"/>, <see cref="ProgressCompleted"/>,
    ///     <see cref="UnsupportedChecked"/>, <see cref="StartTime"/>,
    ///     <see cref="CompletionTime"/>, <see cref="PersistedMetrics"/>. All seven
    ///     are wiped at the entry of <c>NeedsRun</c> so each new run begins from a
    ///     clean baseline.
    ///   </item>
    /// </list>
    /// <para>
    /// <see cref="ResetForRestoration"/> wipes everything; it is intended for
    /// initialisation paths and tests that need a pristine state object.
    /// </para>
    /// </remarks>
    internal sealed class BatchRunState
    {
        /// <summary>
        /// Active batch submission tracked by the poll timer, or <c>null</c>
        /// outside of batch mode and after completion. Survives
        /// <see cref="ResetForNextRun"/>.
        /// </summary>
        public AIBatchSubmission Submission { get; set; }

        /// <summary>
        /// Sentinel trees keyed by output parameter name, populated during batch
        /// collection or by <c>Read</c> when restoring from a saved file.
        /// Survives <see cref="ResetForNextRun"/> so a saved-and-reopened
        /// component can still reconstruct outputs from completed batch results.
        /// </summary>
        public Dictionary<string, object> SentinelTrees { get; set; }

        /// <summary>
        /// Queue of (CustomId, Request) pairs collected during a batch-mode run,
        /// consumed by <c>SubmitBatchQueueAsync</c>. Per-run scratch.
        /// </summary>
        public List<(string CustomId, AIRequestCall Request)> Queue { get; set; }

        /// <summary>
        /// Sentinel custom IDs generated for the current batch, used to validate
        /// loaded result files and reconstruct branch order. Per-run scratch
        /// (cleared at next NeedsRun).
        /// </summary>
        public HashSet<string> SentinelIds { get; set; }

        /// <summary>
        /// Number of items completed so far in the active batch, surfaced in the
        /// component message. Per-run scratch.
        /// </summary>
        public int ProgressCompleted { get; set; }

        /// <summary>
        /// True once <c>IsBatchRequest()</c> has confirmed (or rejected) batch
        /// support for the current provider during this run. Per-run scratch so
        /// the check repeats when batch tier toggles.
        /// </summary>
        public bool UnsupportedChecked { get; set; }

        /// <summary>
        /// Wall-clock timestamp when the batch was submitted to the provider, or
        /// <c>null</c> until submission. Per-run scratch.
        /// </summary>
        public DateTime? StartTime { get; set; }

        /// <summary>
        /// Wall-clock seconds elapsed from submission to completion, consumed by
        /// <c>FinishResults</c> to stamp <c>AIMetrics.CompletionTime</c>.
        /// Per-run scratch.
        /// </summary>
        public double? CompletionTime { get; set; }

        /// <summary>
        /// Single authoritative metrics instance for the run. Replaces the
        /// computed-on-demand <c>AIReturn.Metrics</c> property as the source of
        /// truth for <c>SetMetricsOutput</c>. Per-run scratch.
        /// </summary>
        public AIMetrics PersistedMetrics { get; set; }

        /// <summary>
        /// Wipes per-run scratch fields without touching the cross-run survivors
        /// (<see cref="Submission"/>, <see cref="SentinelTrees"/>). Call from
        /// <c>OnEnteringNeedsRun</c>.
        /// </summary>
        public void ResetForNextRun()
        {
            this.Queue = null;
            this.SentinelIds = null;
            this.ProgressCompleted = 0;
            this.UnsupportedChecked = false;
            this.StartTime = null;
            this.CompletionTime = null;
            this.PersistedMetrics = null;
        }

        /// <summary>
        /// Wipes every field, including the cross-run survivors. Used by
        /// initialisation/test paths that need a pristine state object.
        /// </summary>
        public void ResetForRestoration()
        {
            this.Submission = null;
            this.SentinelTrees = null;
            this.ResetForNextRun();
        }
    }
}
