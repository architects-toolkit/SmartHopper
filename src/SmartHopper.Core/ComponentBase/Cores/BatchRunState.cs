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
using Newtonsoft.Json.Linq;
using SmartHopper.ProviderSdk.AICall.Batch;
using SmartHopper.ProviderSdk.AICall.Core.Requests;
using SmartHopper.ProviderSdk.AICall.Metrics;
using SmartHopper.ProviderSdk.Diagnostics;

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
    ///     <b>Cross-run survivors</b>: <see cref="Submission"/>,
    ///     <see cref="SubmittedProviderName"/>, <see cref="SubmittedModels"/>,
    ///     <see cref="SubmittedRoles"/> and <see cref="SentinelTrees"/>. An in-flight remote batch must keep
    ///     polling and reconstructing its tree even when the user toggles the Run
    ///     input or edits an upstream value, so neither is touched by
    ///     <see cref="ResetForNextRun"/>.
    ///   </item>
    ///   <item>
    ///     <b>Per-run scratch</b>: <see cref="Queue"/>,
    ///     <see cref="SentinelIds"/>, <see cref="ProgressCompleted"/>,
    ///     <see cref="UnsupportedChecked"/>, <see cref="BatchSupported"/>,
    ///     <see cref="RunInputHashes"/>, <see cref="RunBranchCounts"/>,
    ///     <see cref="RunProviderSelection"/>, <see cref="StartTime"/>,
    ///     <see cref="CompletionTime"/>, <see cref="PersistedMetricsList"/>. All
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
        /// Name of the provider that actually executed the submitted batch, captured
        /// at submission time (or from <see cref="AIBatchSubmission.ProviderName"/>
        /// when a batch restored from file finishes polling). Used to decode results
        /// and stamp metrics with the provider that ran the batch rather than the
        /// provider currently selected — the selection may have changed mid-flight.
        /// Survives <see cref="ResetForNextRun"/> alongside <see cref="Submission"/>.
        /// </summary>
        public string SubmittedProviderName { get; set; }

        /// <summary>
        /// Model name requested per sentinel custom ID, captured from the submitted
        /// queue before it is discarded. Used to stamp batch-decoded metrics whose
        /// raw provider decode leaves <see cref="AIMetrics.Model"/> unset.
        /// Survives <see cref="ResetForNextRun"/> alongside <see cref="Submission"/>.
        /// </summary>
        public Dictionary<string, string> SubmittedModels { get; set; }

        /// <summary>
        /// Metrics role label requested per sentinel custom ID, recorded at queue
        /// time ("main" for component requests, "tool:{name}" for direct tool
        /// calls). Used to stamp batch-decoded metrics with the attribution of the
        /// call that produced them. Persisted to the archive so a
        /// saved-and-reopened in-flight batch keeps its attribution.
        /// Survives <see cref="ResetForNextRun"/> alongside <see cref="Submission"/>.
        /// </summary>
        public Dictionary<string, string> SubmittedRoles { get; set; }

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
        public List<(string CustomId, AIRequestCall Request, string MetricsRole)> Queue { get; set; }

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
        /// Run generation counter, bumped by <see cref="ResetForNextRun"/> and
        /// <see cref="InvalidateAsyncWork"/>. A submission in flight captures the
        /// current epoch before its provider call; if the epoch moves before the
        /// call returns, the result is stale (the run was invalidated or
        /// superseded) and must be discarded — never installed as
        /// <see cref="Submission"/>.
        /// </summary>
        public int Epoch { get; private set; }

        /// <summary>
        /// Finalization generation counter, bumped only by
        /// <see cref="InvalidateAsyncWork"/> (cache clear, cancel, component
        /// removal). A late poll completion compares the epoch captured when the
        /// remote status arrived against the current value before mutating
        /// component outputs or state — a mismatch means a destructive event
        /// happened while the callback was in flight.
        /// </summary>
        public int FinalizeEpoch { get; private set; }

        /// <summary>
        /// Epoch of the run that owns the in-flight provider submission, or
        /// <c>-1</c> when no provider call is being submitted. Set synchronously
        /// by the parent handoff before the awaited submit begins so the whole
        /// HTTP window is covered by <see cref="BatchInFlight"/> — previously
        /// <see cref="Submission"/> only materialized after the provider
        /// answered, leaving the window unguarded (phantom workers could spawn,
        /// replay pending fragments and transition to Completed mid-submit).
        /// </summary>
        public int PendingSubmissionEpoch { get; set; } = -1;

        /// <summary>
        /// True while a batch exists upstream <b>or</b> a submission is in
        /// flight toward the provider. All lifecycle guards (worker spawn,
        /// per-run reset, output publishing, mid-batch prompts, cancellation)
        /// must use this single predicate instead of only testing
        /// <see cref="Submission"/>.
        /// </summary>
        public bool BatchInFlight => this.Submission != null || this.PendingSubmissionEpoch >= 0;

        /// <summary>
        /// Marks the submission of the collected queue as in flight for the
        /// current run. Called synchronously from <c>OnWorkerCompleted</c>
        /// immediately before the fire-and-forget submit starts.
        /// </summary>
        /// <returns>The epoch the pending submission belongs to.</returns>
        public int BeginPendingSubmission()
        {
            this.PendingSubmissionEpoch = this.Epoch;
            return this.Epoch;
        }

        /// <summary>
        /// Invalidates every async side effect owned by this state: bumps
        /// <see cref="Epoch"/> and <see cref="FinalizeEpoch"/> and clears the
        /// pending-submission marker. Any provider submit or poll callback
        /// already in flight detects the moved epoch and discards its result
        /// instead of reviving cancelled/cleared work.
        /// </summary>
        public void InvalidateAsyncWork()
        {
            this.Epoch++;
            this.FinalizeEpoch++;
            this.PendingSubmissionEpoch = -1;
        }

        /// <summary>
        /// Called when a new remote submission is installed as
        /// <see cref="Submission"/>. Bumps <see cref="FinalizeEpoch"/> so any
        /// finalization callback already queued by the previous run — e.g. a
        /// poll completion sitting on the UI dispatcher — is recognised as
        /// stale and discarded instead of finalizing the new run with the old
        /// run's results.
        /// </summary>
        public void NoteSubmissionInstalled()
        {
            this.FinalizeEpoch++;
        }

        /// <summary>
        /// Whether the provider has ever reported a <c>CompletedCount</c> for the
        /// active batch. Distinguishes a confirmed zero processed count from
        /// unknown progress, which callers must treat as &gt;0. Per-run scratch.
        /// </summary>
        public bool ProgressReported { get; set; }

        /// <summary>
        /// True once <c>IsBatchRequest()</c> has confirmed (or rejected) batch
        /// support for the current provider during this run. Per-run scratch so
        /// the check repeats when batch tier toggles.
        /// </summary>
        public bool UnsupportedChecked { get; set; }

        /// <summary>
        /// Cached outcome of the batch-capability check performed by
        /// <c>IsBatchRequest()</c>: <c>true</c> only when the provider and model
        /// actually support batch. Negative results are cached too — a rejected
        /// provider must stay rejected for the rest of the run instead of
        /// flipping to supported on the next call.
        /// </summary>
        public bool BatchSupported { get; set; }

        /// <summary>
        /// Immutable snapshot of the pending input hashes captured when the run
        /// gathered its inputs. Batch submission commits this snapshot instead of
        /// the mutable pending baselines, so input edits made while the batch is
        /// collecting or submitting remain pending and trigger recalculation
        /// after completion. Per-run scratch.
        /// </summary>
        public Dictionary<string, int> RunInputHashes { get; set; }

        /// <summary>
        /// Branch-count companion to <see cref="RunInputHashes"/>. Per-run scratch.
        /// </summary>
        public Dictionary<string, int> RunBranchCounts { get; set; }

        /// <summary>
        /// Provider selection captured with <see cref="RunInputHashes"/>, so batch
        /// submission acknowledges exactly the selection the batch was built with.
        /// Per-run scratch.
        /// </summary>
        public string RunProviderSelection { get; set; }

        /// <summary>
        /// Wall-clock timestamp (UTC) when the batch was submitted to the provider,
        /// or <c>null</c> until submission. Doubles as the polling deadline clock
        /// and, across save/reopen, carries the <b>original</b> submission time —
        /// it must never be replaced by a reconstructed submission's timestamp.
        /// Per-run scratch.
        /// </summary>
        public DateTime? StartTime { get; set; }

        /// <summary>
        /// The authoritative original submission instant for persistence. Prefers
        /// <see cref="StartTime"/> — which is restored from the archive and
        /// survives reopen cycles — over <see cref="AIBatchSubmission.SubmittedAt"/>,
        /// which is stamped at construction time and would drift forward on every
        /// successive save/reopen (each restore loses the real elapsed duration and
        /// re-extends the polling deadline). Falls back to the live submission
        /// timestamp when no clock was installed or restored yet.
        /// </summary>
        public DateTimeOffset? SubmissionTimestamp
            => this.StartTime.HasValue
                ? new DateTimeOffset(DateTime.SpecifyKind(this.StartTime.Value, DateTimeKind.Utc))
                : this.Submission?.SubmittedAt;

        /// <summary>
        /// Results of a manual file import staged for the collect-only fallback:
        /// the payload bodies re-keyed to the freshly collected customIds. Held
        /// (not dispatched) until the caller has installed the sentinel trees, so
        /// completion can never observe an empty reconstruction map.
        /// </summary>
        public IReadOnlyDictionary<string, JObject> StagedImportResults { get; set; }

        /// <summary>Diagnostic messages staged alongside <see cref="StagedImportResults"/>.</summary>
        public List<SHRuntimeMessage> StagedImportMessages { get; set; }

        /// <summary>
        /// Wall-clock seconds elapsed from submission to completion, consumed by
        /// <c>FinishResults</c> to stamp <c>AIMetrics.CompletionTime</c>.
        /// Per-run scratch.
        /// </summary>
        public double? CompletionTime { get; set; }

        /// <summary>
        /// Ordered list of per-call metrics for the run. Replaces the former scalar
        /// <c>PersistedMetrics</c> as the source of truth for <c>SetMetricsOutput</c>.
        /// Per-run scratch.
        /// </summary>
        public AIMetricsList PersistedMetricsList { get; set; }

        /// <summary>
        /// Output trees captured from the most recent <c>RunProcessingAsync</c> call,
        /// keyed by output parameter name. The parent-owned submission handoff scans
        /// these for batch sentinels so workers cannot queue requests without the
        /// base being able to submit them. Per-run scratch. Values are
        /// <c>IGH_Structure</c> trees — typed as <see cref="object"/> like
        /// <see cref="SentinelTrees"/> so this state object stays Grasshopper-free
        /// (the test assembly has no Grasshopper reference).
        /// </summary>
        public Dictionary<string, object> CollectedOutputs { get; set; }

        /// <summary>
        /// Per-item errors recorded while decoding batch results (decode exceptions,
        /// missing decode context). Consulted by <c>CompleteBatchAndTransition</c> so
        /// locally failed items count toward the error/all-failed decision instead of
        /// silently completing. Per-run scratch.
        /// </summary>
        public List<SHRuntimeMessage> DecodeErrors { get; set; } = new List<SHRuntimeMessage>();

        /// <summary>
        /// True when the batch-completion pipeline itself failed (an exception inside
        /// <c>OnBatchCompleted</c>). Forces an <see cref="State.ComponentState.Error"/>
        /// transition — a crashed decode pipeline must never finish as
        /// <see cref="State.ComponentState.Completed"/>. Per-run scratch.
        /// </summary>
        public bool CompletionFailed { get; set; }

        /// <summary>
        /// Wipes per-run scratch fields without touching the cross-run survivors
        /// (<see cref="Submission"/>, <see cref="SentinelTrees"/>). Called from
        /// <c>OnEnteringNeedsRun</c>, which is invoked both on the NeedsRun
        /// transition and on Processing entry (via <c>OnEnteringProcessing</c>)
        /// so runs triggered directly with Run=true reset too.
        /// </summary>
        public void ResetForNextRun()
        {
            this.Queue = null;
            this.SentinelIds = null;
            this.ProgressCompleted = 0;
            this.ProgressReported = false;
            this.UnsupportedChecked = false;
            this.BatchSupported = false;
            this.RunInputHashes = null;
            this.RunBranchCounts = null;
            this.RunProviderSelection = null;
            this.StartTime = null;
            this.CompletionTime = null;
            this.PersistedMetricsList = null;
            this.CollectedOutputs = null;
            this.DecodeErrors = null;
            this.CompletionFailed = false;
            this.StagedImportResults = null;
            this.StagedImportMessages = null;

            // A new run invalidates a still-pending submission belonging to the
            // previous one: its captured epoch no longer matches, so the late
            // provider response is discarded (and remotely cancelled) instead
            // of being installed as this run's submission.
            this.Epoch++;
            this.PendingSubmissionEpoch = -1;
        }

        /// <summary>
        /// Wipes every field, including the cross-run survivors. Used by
        /// initialisation/test paths that need a pristine state object.
        /// </summary>
        public void ResetForRestoration()
        {
            this.Submission = null;
            this.SubmittedProviderName = null;
            this.SubmittedModels = null;
            this.SubmittedRoles = null;
            this.SentinelTrees = null;
            this.FinalizeEpoch = 0;
            this.ResetForNextRun();
        }
    }
}
