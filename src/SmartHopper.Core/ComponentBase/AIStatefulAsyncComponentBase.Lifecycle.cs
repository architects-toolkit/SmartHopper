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
using SmartHopper.Core.ComponentBase.Contracts;
using SmartHopper.Core.ComponentBase.Cores;
using SmartHopper.Core.ComponentBase.Mixins;
using SmartHopper.Core.DataTree;
using SmartHopper.Infrastructure.AICall.Batch;
using SmartHopper.Infrastructure.AICall.Core;
using SmartHopper.Infrastructure.AIProviders;

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

                Debug.WriteLine($"[AIStatefulAsync] Document {doc.DocumentID} removed - shutdown detected for that document");
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

                Debug.WriteLine($"[AIStatefulAsyncComponentBase] Settings parsed: Model={parsed.Model}, MaxTokens={parsed.MaxTokens}, Temperature={parsed.Temperature}");
                this.SetParameters(parsed);
            }
            else
            {
                this.SetParameters(AIRequestParameters.Empty);
            }

            // Update badge cache using current inputs before executing the solution
            this.UpdateBadgeCache();

            base.SolveInstance(DA);
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
            // When a batch submission is already active, skip spawning a new worker.
            // The poll timer (started in Read()) drives completion; no new work should begin.
            if (this._batchState.Submission != null)
            {
                Debug.WriteLine("[AIStatefulAsyncComponentBase] OnStateProcessing: batch active, skipping worker spawn");
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
            Debug.WriteLine($"[AIStatefulAsyncComponentBase] OnEnteringNeedsRun: resetting per-run state, SentinelTrees count={this._batchState.SentinelTrees?.Count ?? 0}");
            this.AIReturnSnapshot = null;
            this._batchState.ResetForNextRun();
            this._currentProcessingPath = null;
            this._currentProcessingItemIndex = null;
            this._metricsTree = null;
            this.ResetProgress();
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
            if (this._batchState.Submission != null)
            {
                Debug.WriteLine("[AIStatefulAsync] OnSolveInstancePostSolve: Batch still active, skipping output");
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
            if (this._batchState.Submission != null)
            {
                Debug.WriteLine("[AIStatefulAsync] RestorePersistentOutputs: Batch submission active, skipping sentinel tree output");
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
            if (this._batchState.Submission != null)
            {
                if (shuttingDown)
                {
                    // Document/Rhino closing: preserve batch, just stop polling
                    Debug.WriteLine($"[AIStatefulAsync] RemovedFromDocument (shutdown): Preserving batch {this._batchState.Submission.BatchId}");
                    this.StopBatchPollTimer();
                }
                else
                {
                    // Manual component removal: cancel the batch (no UI update needed as component is being destroyed)
                    var batchId = this._batchState.Submission.BatchId;
                    Debug.WriteLine($"[AIStatefulAsync] RemovedFromDocument (manual removal): Cancelling batch {batchId}");
                    _ = this.CancelRemoteBatchAsync(batchId);
                }
            }
            else if (this._batchPollTimer != null)
            {
                Debug.WriteLine($"[AIStatefulAsync] RemovedFromDocument: Stopping batch poll timer");
                this.StopBatchPollTimer();
            }

            base.RemovedFromDocument(document);
        }

        /// <inheritdoc/>
        protected override void OnWorkerCompleted()
        {
            // If a batch is active, don't transition to Completed yet.
            // The batch polling will handle the transition when results are ready.
            if (this._batchState.Submission != null)
            {
                Debug.WriteLine($"[AIStatefulAsync] OnWorkerCompleted: Batch submission active ({this._batchState.Submission.BatchId}), staying in Processing state");

                // Stay in Processing state - batch polling will transition to Completed
                // Still commit hashes so we don't re-trigger processing
                this.StateManager.CommitHashes();
                this.StateManager.CancelDebounce();

                // Don't call base.OnWorkerCompleted() which would transition to Completed
                // Don't expire solution - the batch poll will do it when complete
                return;
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
            // If a batch is active, cancel it on the provider and update UI with confirmation
            if (this._batchState.Submission != null)
            {
                var batchId = this._batchState.Submission.BatchId;
                Debug.WriteLine($"[AIStatefulAsync] OnTasksCancelDetected: Cancelling remote batch {batchId}");

                // Add immediate feedback that cancellation is in progress
                this.SetPersistentRuntimeMessage(
                    "batch_cancelling",
                    GH_RuntimeMessageLevel.Warning,
                    $"Cancelling batch {batchId}...",
                    false);

                // Start cancellation and update with confirmation when complete
                _ = this.CancelRemoteBatchAsync(batchId);
            }

            // Transition to Cancelled immediately for UI responsiveness
            base.OnTasksCancelDetected();
        }

        /// <summary>
        /// Cancels the remote batch job on the provider and updates the component
        /// with confirmation status. Fire-and-forget pattern with UI update on completion.
        /// </summary>
        /// <param name="batchId">The batch ID being cancelled (for logging and messages).</param>
        private async Task CancelRemoteBatchAsync(string batchId)
        {
            bool cancelledSuccessfully = false;
            string errorMessage = null;

            try
            {
                var submission = this._batchState.Submission;
                if (submission != null)
                {
                    var provider = ProviderManager.Instance.GetProvider(submission.ProviderName);
                    if (provider is IAIBatchProvider batchProvider)
                    {
                        await batchProvider.CancelBatchAsync(submission, CancellationToken.None).ConfigureAwait(false);
                        cancelledSuccessfully = true;
                        Debug.WriteLine($"[AIStatefulAsync] Remote batch cancelled successfully: {batchId}");
                    }
                    else
                    {
                        errorMessage = "Provider does not support batch cancellation";
                        Debug.WriteLine($"[AIStatefulAsync] Provider does not support batch cancellation: {submission.ProviderName}");
                    }
                }
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                Debug.WriteLine($"[AIStatefulAsync] Error cancelling remote batch {batchId}: {ex.Message}");
            }
            finally
            {
                this.StopBatchPollTimer();
                this._batchState.Submission = null;

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

        #endregion
    }
}
