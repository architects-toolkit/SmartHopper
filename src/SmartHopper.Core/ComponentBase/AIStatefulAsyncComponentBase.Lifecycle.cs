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
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Newtonsoft.Json.Linq;
using SmartHopper.Core.ComponentBase.Batch;
using SmartHopper.Core.ComponentBase.Contracts;
using SmartHopper.Core.ComponentBase.Cores;
using SmartHopper.Core.ComponentBase.Mixins;
using SmartHopper.Core.DataTree;
using SmartHopper.Infrastructure.AIProviders;
using SmartHopper.ProviderSdk.AICall.Batch;
using SmartHopper.ProviderSdk.AICall.Core;
using SmartHopper.ProviderSdk.AICall.Metrics;
using SmartHopper.ProviderSdk.AIProviders;

using SmartHopper.ProviderSdk.Logging;
namespace SmartHopper.Core.ComponentBase
{
    /// <summary>
    /// Lifecycle management for AIStatefulAsyncComponentBase.
    /// Handles state transitions, input changes, and persistence restoration.
    /// </summary>
    public abstract partial class AIStatefulAsyncComponentBase
    {
        /// <summary>
        /// Tracks which documents are currently being removed (closed / Rhino shutdown), keyed
        /// by <see cref="GH_Document.DocumentID"/>. Replaces the previous single static boolean,
        /// which incorrectly treated any open-document close as "the whole app is shutting down"
        /// when multiple documents were loaded — causing batches in doc B to be preserved when
        /// the user was only closing doc A.
        /// </summary>
        private static readonly HashSet<Guid> _removingDocumentIds = new HashSet<Guid>();
        private static readonly object _removingDocumentIdsLock = new object();

        /// <summary>Static constructor to subscribe to document removal event for shutdown detection.</summary>
        static AIStatefulAsyncComponentBase()
        {
            // GH_DocumentServer.DocumentRemoved fires early during Rhino shutdown or document close.
            // Recording the DocumentID lets each component compare against its own document in
            // RemovedFromDocument(), so only components whose document is actually closing take
            // the "preserve batch" path.
            Grasshopper.Instances.DocumentServer.DocumentRemoved += (sender, doc) =>
            {
                if (doc == null)
                {
                    return;
                }

                lock (_removingDocumentIdsLock)
                {
                    _removingDocumentIds.Add(doc.DocumentID);
                }

                Logger.For<AIStatefulAsyncComponentBase>().Trace($"[AIStatefulAsync] Document {doc.DocumentID} removed - shutdown detected for that document");
            };
        }

        /// <summary>
        /// Returns whether the given document is currently being closed/removed.
        /// </summary>
        private static bool IsDocumentBeingRemoved(GH_Document document)
        {
            if (document == null)
            {
                return false;
            }

            lock (_removingDocumentIdsLock)
            {
                return _removingDocumentIds.Contains(document.DocumentID);
            }
        }

        #region LIFECYCLE

        /// <summary>
        /// Main solving method for the component.
        /// Handles the execution flow and persistence of results.
        /// </summary>
        /// <param name="DA">The data access object.</param>
        /// <remarks>
        /// This method is sealed to ensure proper persistence and error handling.
        /// Override OnSolveInstance for custom solving logic.
        /// </remarks>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Grasshopper.Kernel.Types.IGH_Goo rawGoo = null;
            if (DA.GetData(WellKnownInputs.Settings, ref rawGoo))
            {
                if (!AIRequestParametersGooParser.TryFromGoo(rawGoo, out var parsed))
                {
                    this.SetPersistentRuntimeMessage(
                        "settings_parse_failed",
                        GH_RuntimeMessageLevel.Warning,
                        $"Could not parse '{WellKnownInputs.Settings}' input ({rawGoo?.GetType().Name ?? "null"}); falling back to defaults.",
                        false);
                }

                Logger.For<AIStatefulAsyncComponentBase>().Trace($"[AIStatefulAsyncComponentBase] Settings parsed: Model={parsed.Model}, MaxTokens={parsed.MaxTokens}, Temperature={parsed.Temperature}");
                this.SetParameters(parsed);
            }
            else
            {
                this.SetParameters(AIRequestParameters.Empty);
            }

            // Update badge cache using current inputs before executing the solution
            this.UpdateBadgeCache();

            base.SolveInstance(DA);

            // If the user edited inputs while a remote batch is in flight, ask whether
            // to cancel + re-request with the new inputs or keep the batch and omit
            // the changes (normal change detection is suspended during Processing).
            this.PromptForMidBatchInputChange();
        }

        /// <inheritdoc/>
        protected override List<string> InputsChanged()
        {
            var changed = base.InputsChanged();

            // Clear batch capability check when provider changes to ensure re-validation
            if (changed.Contains(WellKnownInputs.AIProvider))
            {
                this._batchState.UnsupportedChecked = false;
            }

            return changed;
        }

        protected override void BeforeSolveInstance()
        {
            base.BeforeSolveInstance();

            // NOTE: AIReturnSnapshot clearing is done in OnEnteringNeedsRun (on input change)
            // and defensively in OnStateProcessing, not on every solve.
            // This prevents metrics loss when batch completes and component re-enters Processing.
        }

        /// <inheritdoc/>
        protected override void OnStateProcessing(IGH_DataAccess DA)
        {
            // When a batch is active — submitted and polling, or merely on its
            // way to the provider — skip spawning a new worker. The submit
            // window is the dangerous one: the watchdog expires the solution
            // ~1s after Processing entry, and without the pending marker this
            // solve would respawn a worker that replays pending fragments as
            // "reused" units and completes the run before the job even exists.
            if (this._batchState.BatchInFlight)
            {
                Logger.For<AIStatefulAsyncComponentBase>().Warning("[AIStatefulAsyncComponentBase] OnStateProcessing: batch active, skipping worker spawn");
                return;
            }

            base.OnStateProcessing(DA);
        }

        /// <inheritdoc/>
        protected override void OnEnteringNeedsRun()
        {
            base.OnEnteringNeedsRun();

            // BatchRunState.ResetForNextRun() wipes the seven per-run scratch fields
            // (queue, sentinel ids, progress, unsupported flag, timings, persisted metrics)
            // while preserving the two cross-run survivors (Submission, SentinelTrees) so an
            // in-flight remote batch keeps polling and saved-batch reload remains correct.
            Logger.For<AIStatefulAsyncComponentBase>().Trace($"[AIStatefulAsyncComponentBase] OnEnteringNeedsRun: resetting per-run state, SentinelTrees count={this._batchState.SentinelTrees?.Count ?? 0}");
            this.AIReturnSnapshot = null;
            this._batchState.ResetForNextRun();
            this._currentProcessingPath = null;
            this._currentProcessingItemIndex = null;
            this._metricsTree = null;
            this.ResetProgress();
        }

        /// <inheritdoc/>
        protected override void OnEnteringProcessing()
        {
            base.OnEnteringProcessing();

            // With Run=true, every trigger (input change, Run pulse) transitions
            // directly to Processing and never enters NeedsRun, so the per-run
            // cleanup in OnEnteringNeedsRun would never fire: accumulated state
            // (metrics tree, AIReturn snapshot, batch scratch, and derived
            // components' fetch caches) leaks across runs — e.g. the Metrics
            // output growing one entry per run. Reuse the same reset here so
            // every new run starts clean regardless of the path that led to it.
            // Entering Processing while a remote batch is still in flight is a
            // continuation of the same run, not a new one — keep its state so
            // sentinel reconstruction still works. A submission still on its
            // way to the provider counts too: resetting here would bump the
            // epoch and orphan the queue's own submit.
            if (!this._batchState.BatchInFlight)
            {
                Logger.For<AIStatefulAsyncComponentBase>().Trace("[AIStatefulAsyncComponentBase] OnEnteringProcessing: no in-flight batch, resetting per-run state");
                this.OnEnteringNeedsRun();

                // Snapshot the inputs this run was gathered with, so batch submission
                // can acknowledge exactly those values instead of whatever the pending
                // baselines happen to be once the (async) submit call returns.
                this.CaptureRunInputSnapshot();

                // Freeze the request parameters alongside the input snapshot: a
                // Settings edit made mid-run stays pending and cannot leak into
                // the run's remaining units or its reuse/metrics context.
                this._runParameters = this._requestParameters;
            }
        }

        /// <inheritdoc/>
        protected override bool SupportsResultReuse => true;

        /// <inheritdoc/>
        protected override bool ShareIdenticalResults => this.GetParameters()?.ShareIdenticalResults ?? true;

        /// <inheritdoc/>
        /// <remarks>
        /// Unit identity embeds the component type, provider, model and the
        /// execution-relevant request parameters (model, temperature, seeds, extras…;
        /// transport/policy fields are excluded) so a settings change invalidates
        /// only genuinely different work.
        /// </remarks>
        protected override string GetResultReuseContext()
        {
            return SemanticFingerprint.ComputeShortHash(
                $"{this.GetType().FullName}|{this.GetActualAIProviderName()}|{this.GetModel()}|" +
                SemanticFingerprint.CanonicalParameters(this.GetParameters(), executionFieldsOnly: true));
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Entering Processing while a remote batch is still in flight continues the
        /// same run, so the pending (sentinel) fragments gathered at submission must
        /// survive — they are resolved when the batch completes.
        /// </remarks>
        protected override void ResetRunResults()
        {
            if (!this._batchState.BatchInFlight)
            {
                base.ResetRunResults();
            }
        }

        /// <inheritdoc/>
        protected override void OnClearResultCache()
        {
            // Invalidate every async side effect first: a submit or poll
            // completion already in flight must observe the moved epoch and
            // discard its result instead of reviving cleared work.
            this._batchState.InvalidateAsyncWork();

            base.OnClearResultCache();
            this.ClearCollectOnlyArm();
            this.StopBatchPollTimer();
            this._batchState.Submission = null;
            this._batchState.SubmittedProviderName = null;
            this._batchState.SubmittedModels = null;
            this._batchState.SubmittedRoles = null;
            this._batchState.SentinelTrees = null;
            this._batchState.Queue = null;
            this._batchState.SentinelIds = null;
        }

        /// <summary>
        /// Appends replayed provenance entries for a reused unit at the same
        /// output path fresh metrics would occupy (input branch, or grafted
        /// child path for ItemGraft topology).
        /// </summary>
        /// <param name="metrics">Stored provenance entries in call order.</param>
        private void AppendReusedMetrics(IReadOnlyList<AIMetrics> metrics)
        {
            if (metrics == null || metrics.Count == 0 || this._currentProcessingPath == null)
            {
                return;
            }

            var path = this._currentProcessingPath;
            if (this.ComponentProcessingOptions.Topology == ProcessingTopology.ItemGraft
                && this._currentProcessingItemIndex.HasValue)
            {
                path = path.AppendElement(this._currentProcessingItemIndex.Value);
            }

            this._metricsTree ??= new GH_Structure<GH_String>();
            foreach (var m in metrics)
            {
                var obj = this.SerializeMetricsEntry(
                    m with { DataCount = 0, IterationsCount = 0 },
                    reused: true);
                if (obj != null)
                {
                    this._metricsTree.Append(
                        new GH_String(obj.ToString(Newtonsoft.Json.Formatting.None)),
                        path);
                }
            }
        }

        /// <inheritdoc/>
        protected override void OnProcessingUnitStart(GH_Path path, int? itemIndex)
        {
            base.OnProcessingUnitStart(path, itemIndex);
            this._currentProcessingPath = path;
            this._currentProcessingItemIndex = itemIndex;
        }

        /// <inheritdoc/>
        protected override void OnProcessingUnitComplete(GH_Path inputPath, List<GH_Path> targetPaths)
        {
            base.OnProcessingUnitComplete(inputPath, targetPaths);

            // Cache-hit units replay their stored execution provenance so the
            // Metrics output describes the complete current output, not only the
            // calls made during this run. Replayed entries carry reused:true with
            // zeroed data/iterations counts — they represent previously incurred
            // work, not new processing.
            if (this.LastUnitWasReused
                && this.LastCompletedUnitKey != null
                && this.ResultStore.TryGetMetrics(this.LastCompletedUnitKey, out var replay))
            {
                this.AppendReusedMetrics(replay);
            }

            // When GroupIdenticalBranches is active, non-primary target paths reuse
            // results from the primary path. Replicate metrics to those reused paths
            // so downstream components can distinguish processed vs reused.
            if (this._metricsTree == null || inputPath == null || targetPaths == null)
            {
                return;
            }

            if (this.ComponentProcessingOptions.Topology == ProcessingTopology.ItemGraft)
            {
                // Metrics are already stored at grafted child paths (inputPath + item index).
                // Copy each child branch to the corresponding grafted path on every sibling target.
                var childPaths = this._metricsTree.Paths
                    .Where(p => p.Length == inputPath.Length + 1 && p.Indices.Take(inputPath.Length).SequenceEqual(inputPath.Indices))
                    .ToList();

                foreach (var targetPath in targetPaths)
                {
                    if (targetPath == null || targetPath.Equals(inputPath))
                    {
                        continue;
                    }

                    foreach (var childPath in childPaths)
                    {
                        int itemIndex = childPath.Indices[inputPath.Length];
                        var siblingPath = targetPath.AppendElement(itemIndex);
                        var childBranch = this._metricsTree.get_Branch(childPath);
                        if (childBranch == null)
                        {
                            continue;
                        }

                        foreach (GH_String metricStr in childBranch)
                        {
                            try
                            {
                                var obj = JObject.Parse(metricStr.Value);
                                obj["data_count"] = 0;
                                this._metricsTree.Append(new GH_String(obj.ToString(Newtonsoft.Json.Formatting.None)), siblingPath);
                            }
                            catch
                            {
                                // If parsing fails, skip the copy
                            }
                        }
                    }
                }

                return;
            }

            var inputBranch = this._metricsTree.get_Branch(inputPath);
            if (inputBranch == null || inputBranch.Count == 0)
            {
                return;
            }

            foreach (var targetPath in targetPaths)
            {
                if (targetPath == null || targetPath.Equals(inputPath))
                {
                    continue;
                }

                foreach (GH_String metricStr in inputBranch)
                {
                    try
                    {
                        var obj = JObject.Parse(metricStr.Value);
                        obj["data_count"] = 0;
                        this._metricsTree.Append(new GH_String(obj.ToString(Newtonsoft.Json.Formatting.None)), targetPath);
                    }
                    catch
                    {
                        // If parsing fails, skip the copy
                    }
                }
            }
        }

        protected override void OnSolveInstancePostSolve(IGH_DataAccess DA)
        {
            // Skip output if batch is still active - outputs will be set after batch completes
            if (this._batchState.BatchInFlight)
            {
                Logger.For<AIStatefulAsyncComponentBase>().Warning("[AIStatefulAsync] OnSolveInstancePostSolve: Batch still active, skipping output");
                return;
            }

            // Metrics are emitted by FinishResults.
            // Components that set metrics synchronously in their own SolveInstance (e.g. AIChatComponent)
            // call SetMetricsOutput(DA) directly and do not go through FinishResults.

            // Badge cache was already updated in SolveInstance (before base.SolveInstance)
            // No need to update again here - the configured model hasn't changed
        }

        /// <summary>
        /// Overrides persistent output restoration to prevent sentinel trees from being output
        /// while a batch is actively being processed. Sentinel trees are internal implementation
        /// details used during batch submission and should not be visible on the canvas.
        /// </summary>
        protected override void RestorePersistentOutputs(IGH_DataAccess DA)
        {
            // If a batch is actively being processed, skip restoration of sentinel trees
            if (this._batchState.BatchInFlight)
            {
                Logger.For<AIStatefulAsyncComponentBase>().Warning("[AIStatefulAsync] RestorePersistentOutputs: Batch submission active, skipping sentinel tree output");
                return;
            }

            // Otherwise, restore outputs normally
            base.RestorePersistentOutputs(DA);
        }

        /// <inheritdoc/>
        public override void RemovedFromDocument(GH_Document document)
        {
            // Distinguish between manual component removal and document/Rhino close.
            // IsDocumentBeingRemoved() checks the specific document id, so closing doc A does
            // not make components in doc B take the "preserve batch" path.
            var shuttingDown = IsDocumentBeingRemoved(document);
            if (this._batchState.BatchInFlight)
            {
                if (shuttingDown)
                {
                    // Document/Rhino closing: preserve batch, just stop polling.
                    // A still-pending submit cannot be persisted anyway — invalidate
                    // it so the late provider response is dropped, not installed.
                    Logger.For<AIStatefulAsyncComponentBase>().Trace($"[AIStatefulAsync] RemovedFromDocument (shutdown): Preserving batch {this._batchState.Submission?.BatchId ?? "(pending)"}");
                    this.StopBatchPollTimer();
                    this._batchState.PendingSubmissionEpoch = -1;
                }
                else
                {
                    // Manual component removal: cancel the batch (no UI update needed as component is being destroyed)
                    this._batchState.InvalidateAsyncWork();
                    var batchId = this._batchState.Submission?.BatchId;
                    if (batchId != null)
                    {
                        Logger.For<AIStatefulAsyncComponentBase>().Trace($"[AIStatefulAsync] RemovedFromDocument (manual removal): Cancelling batch {batchId}");
                        _ = this.CancelRemoteBatchAsync(batchId);
                    }
                }
            }
            else if (this._batchPollTimer != null)
            {
                Logger.For<AIStatefulAsyncComponentBase>().Trace($"[AIStatefulAsync] RemovedFromDocument: Stopping batch poll timer");
                this.StopBatchPollTimer();
            }

            base.RemovedFromDocument(document);
        }

        /// <inheritdoc/>
        protected override void OnWorkerCompleted()
        {
            // If a batch is active — submitted/polling OR still on its way to
            // the provider — don't transition to Completed yet. The pending
            // case is what kept the submission window unguarded: Submission is
            // only assigned after the provider call returns, so a second worker
            // completing inside that window would have fallen through to
            // base.OnWorkerCompleted and finished the run mid-submit.
            if (this._batchState.BatchInFlight)
            {
                Logger.For<AIStatefulAsyncComponentBase>().Trace($"[AIStatefulAsync] OnWorkerCompleted: Batch in flight ({this._batchState.Submission?.BatchId ?? "pending submission"}), staying in Processing state");

                // Stay in Processing state - batch polling will transition to Completed
                // Still commit hashes so we don't re-trigger processing — commit the
                // gather-time snapshot, not the live pending values, so mid-batch edits
                // stay pending and surface via the re-request prompt.
                this.CommitRunInputSnapshot();
                this.StateManager.CancelDebounce();

                // Don't call base.OnWorkerCompleted() which would transition to Completed
                // Don't expire solution - the batch poll will do it when complete
                return;
            }

            // Parent-owned deferred handoff: any queue collected during the run is
            // submitted by the base itself — workers no longer need to remember the
            // submission call, and a component can never complete with deferred work
            // it merely queued but never sent (the AIListFilter D46 failure mode).
            if (this._batchState.Queue?.Count > 0)
            {
                var sentinelTrees = this.DeriveSentinelTreesFromCollectedOutputs();
                if (sentinelTrees.Count == 0)
                {
                    Logger.For<AIStatefulAsyncComponentBase>().Error("[AIStatefulAsync] OnWorkerCompleted: batch queue is populated but no sentinel-bearing output was collected — run cannot be submitted");
                    this.SetPersistentRuntimeMessage(
                        "batch_orphaned_queue",
                        GH_RuntimeMessageLevel.Error,
                        "Deferred batch work was collected but could not be mapped to any output; the run cannot be submitted.",
                        false);
                }
                else
                {
                    Logger.For<AIStatefulAsyncComponentBase>().Trace($"[AIStatefulAsync] OnWorkerCompleted: submitting {this._batchState.Queue.Count} queued request(s) via parent-owned handoff ({sentinelTrees.Count} sentinel tree(s))");

                    // Mark the submission in flight SYNCHRONOUSLY, before the
                    // provider HTTP call begins: every lifecycle guard keys off
                    // BatchInFlight, so the window between queue consumption and
                    // Submission assignment can no longer spawn phantom workers
                    // or race the run to Completed. The epoch is captured here on
                    // the UI thread — inside the pooled task it could already be
                    // stale.
                    var submitEpoch = this._batchState.BeginPendingSubmission();
                    _ = Task.Run(() => this.SubmitCollectedBatchAsync(sentinelTrees, submitEpoch));
                }

                // Either way do NOT fall through to normal completion: a failed
                // submission transitions to Error via FailDeferredSubmission /
                // the error message above; a successful one stays in Processing
                // under the poll timer.
                if (this.RuntimeMessageLevel != GH_RuntimeMessageLevel.Error)
                {
                    return;
                }
            }

            // No active batch - proceed with normal completion
            base.OnWorkerCompleted();
        }

        /// <summary>
        /// Called when tasks are canceled (e.g., user clicked "Cancel Current Process").
        /// For batch: immediately transitions to Cancelled state and cancels the remote batch on the provider,
        /// surfacing confirmation when the API responds.
        /// </summary>
        protected override void OnTasksCancelDetected()
        {
            // If a batch is active — submitted or still on its way to the
            // provider — cancel it and update UI with confirmation. A pending
            // submit is invalidated synchronously so its late provider response
            // is discarded (and remotely cancelled), never installed.
            if (this._batchState.BatchInFlight)
            {
                this._batchState.InvalidateAsyncWork();
                var batchId = this._batchState.Submission?.BatchId;
                Logger.For<AIStatefulAsyncComponentBase>().Trace($"[AIStatefulAsync] OnTasksCancelDetected: Cancelling remote batch {batchId ?? "(pending submission)"}");

                // Add immediate feedback that cancellation is in progress
                this.SetPersistentRuntimeMessage(
                    "batch_cancelling",
                    GH_RuntimeMessageLevel.Warning,
                    $"Cancelling batch {batchId ?? "submission"}...",
                    false);

                // Start cancellation and update with confirmation when complete
                if (batchId != null)
                {
                    _ = this.CancelRemoteBatchAsync(batchId);
                }
            }

            // Transition to Cancelled immediately for UI responsiveness
            base.OnTasksCancelDetected();
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Runs ending without reaching <c>SubmitBatchQueueAsync</c> must drop
        /// an armed collect-only import — see <see cref="ClearCollectOnlyArm"/>.
        /// </remarks>
        protected override void OnEnteringCompleted()
        {
            base.OnEnteringCompleted();
            this.ClearCollectOnlyArm();
        }

        /// <inheritdoc/>
        protected override void OnEnteringError()
        {
            base.OnEnteringError();
            this.ClearCollectOnlyArm();
        }

        /// <inheritdoc/>
        /// <remarks>
        /// During the batch poll phase no worker tasks exist, so
        /// <see cref="OnTasksCancelDetected"/> never runs — the Cancelled
        /// transition itself must tear down in-flight batch work. Without this
        /// hook the poll timer kept polling and a late completion could
        /// resurrect a cancelled component (and keep billing the remote job).
        /// </remarks>
        protected override void OnEnteringCancelled()
        {
            // A collect-only arm never survives a run end — the run it was
            // armed for died without consuming it, and a stale arm could
            // otherwise re-key its loaded results into an unrelated batch.
            this.ClearCollectOnlyArm();
            if (!this._batchState.BatchInFlight)
            {
                return;
            }

            this._batchState.InvalidateAsyncWork();
            var batchId = this._batchState.Submission?.BatchId;
            Logger.For<AIStatefulAsyncComponentBase>().Trace($"[AIStatefulAsync] OnEnteringCancelled: cancelling in-flight batch {batchId ?? "(pending submission)"}");
            if (batchId != null)
            {
                _ = this.CancelRemoteBatchAsync(batchId);
            }
            else
            {
                this.StopBatchPollTimer();
            }
        }

        /// <summary>
        /// Cancels the remote batch job on the provider and updates the component
        /// with confirmation status. Fire-and-forget pattern with UI update on completion.
        /// </summary>
        /// <param name="batchId">The batch ID being cancelled (for logging and messages).</param>
        private async Task CancelRemoteBatchAsync(string batchId)
        {
            // Claim the submission synchronously: multiple cancel paths can fire
            // for one user gesture (task-cancel continuation AND the Cancelled
            // state entry). Only the first caller issues the remote cancel —
            // the rest observe a null Submission and return.
            var submission = this._batchState.Submission;
            if (submission == null)
            {
                return;
            }

            this._batchState.Submission = null;

            // Invalidate first, before any await: in-flight poll finalizations
            // and pending submits must observe the moved epoch and discard
            // their result rather than reviving a cancelled batch.
            this._batchState.InvalidateAsyncWork();

            bool cancelledSuccessfully = false;
            string errorMessage = null;

            try
            {
                var provider = ProviderManager.Instance.GetProvider(submission.ProviderName);
                if (provider is IAIBatchProvider batchProvider)
                {
                    await batchProvider.CancelBatchAsync(submission, CancellationToken.None).ConfigureAwait(false);
                    cancelledSuccessfully = true;
                    Logger.For<AIStatefulAsyncComponentBase>().Trace($"[AIStatefulAsync] Remote batch cancelled successfully: {batchId}");
                }
                else
                {
                    errorMessage = "Provider does not support batch cancellation";
                    Logger.For<AIStatefulAsyncComponentBase>().Trace($"[AIStatefulAsync] Provider does not support batch cancellation: {submission.ProviderName}");
                }
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                Logger.For<AIStatefulAsyncComponentBase>().Exception(ex, $"[AIStatefulAsync] Error cancelling remote batch {batchId}: {ex.Message}");
            }
            finally
            {
                // Only tear down what still belongs to the claimed submission:
                // a newer run may have installed its own submission and timer
                // while the remote cancel awaited the provider.
                this.StopBatchPollTimer(submission);
                if (this._batchState.Submission == null || ReferenceEquals(this._batchState.Submission, submission))
                {
                    this._batchState.Submission = null;
                }

                // Update the UI with cancellation confirmation on the main thread
                Rhino.RhinoApp.InvokeOnUiThread(() =>
                {
                    if (cancelledSuccessfully)
                    {
                        this.SetPersistentRuntimeMessage(
                            "batch_cancelled",
                            GH_RuntimeMessageLevel.Remark,
                            $"Batch {batchId} cancelled successfully",
                            false);
                        this.Message = "Batch cancelled";
                    }
                    else if (!string.IsNullOrEmpty(errorMessage))
                    {
                        this.SetPersistentRuntimeMessage(
                            "batch_cancel_failed",
                            GH_RuntimeMessageLevel.Error,
                            $"Failed to cancel batch {batchId}: {errorMessage}",
                            false);
                        this.Message = "Cancel failed";
                    }

                    this.OnDisplayExpired(true);
                });
            }
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Last-resort publication boundary: a <c>##SH_BATCH:</c> sentinel is
        /// transport metadata inside the pending unit fragment — never component
        /// output. Any publish path that bypasses <c>FinishResults</c>' own
        /// strip (e.g. a worker forwarding its raw <c>RunProcessingAsync</c>
        /// result through <c>SetPersistentOutput</c>) emits a sentinel-free
        /// copy here: branch layout is preserved, sentinel items are removed,
        /// and the real values arrive later via batch-completion reconstruction.
        /// Before stripping, each sentinel is resolved against the committed
        /// snapshot through its restored customId → unit key association —
        /// after a mid-flight save whose run can no longer resume, unchanged
        /// inputs still republish the last committed results instead of an
        /// empty branch. Only sentinels with no committed counterpart are
        /// dropped. The stored fragment keeps its placeholders — the rebuild
        /// produces a new tree and never mutates the source.
        /// </remarks>
        protected override void SetPersistentOutput(string paramName, object value, IGH_DataAccess DA)
        {
            if (value is IGH_Structure structure && BatchSentinel.TreeContainsSentinel(structure))
            {
                var resolved = 0;
                var stripped = 0;
                var rebuilt = new GH_Structure<IGH_Goo>();
                foreach (GH_Path path in structure.Paths)
                {
                    rebuilt.EnsurePath(path);
                    if (structure.get_Branch(path) is not System.Collections.IList branch)
                    {
                        continue;
                    }

                    foreach (var item in branch)
                    {
                        if (item is GH_String sentinel
                            && BatchSentinel.TryExtract(sentinel?.Value, out var customId))
                        {
                            if (this.ResultStore.TryGetCommittedForSentinel(customId, paramName, out var committedItems))
                            {
                                foreach (var committedItem in committedItems)
                                {
                                    rebuilt.Append(committedItem, path);
                                }

                                resolved++;
                            }
                            else
                            {
                                stripped++;
                            }

                            continue;
                        }

                        rebuilt.Append(item as IGH_Goo ?? new GH_String(item?.ToString() ?? string.Empty), path);
                    }
                }

                Logger.For<AIStatefulAsyncComponentBase>().Trace($"[AIStatefulAsync] SetPersistentOutput('{paramName}'): resolved {resolved} sentinel placeholder(s) from committed results, stripped {stripped}");
                value = rebuilt;
            }

            base.SetPersistentOutput(paramName, value, DA);
        }

        #endregion
    }
}
