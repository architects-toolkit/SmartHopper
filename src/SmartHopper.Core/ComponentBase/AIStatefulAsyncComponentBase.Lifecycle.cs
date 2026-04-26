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
using System.Threading;
using System.Threading.Tasks;
using Grasshopper.Kernel;
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
        /// <summary>Static flag indicating if a document is being removed (Rhino closing or document close).</summary>
        private static bool _isDocumentRemoving;

        /// <summary>Static constructor to subscribe to document removal event for shutdown detection.</summary>
        static AIStatefulAsyncComponentBase()
        {
            // GH_DocumentServer.DocumentRemoved fires early during Rhino shutdown or document close
            // This allows us to distinguish between manual component removal and shutdown scenarios
            Grasshopper.Instances.DocumentServer.DocumentRemoved += (sender, e) =>
            {
                _isDocumentRemoving = true;
                Debug.WriteLine("[AIStatefulAsync] Document removed event fired - shutdown detected");
            };
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
            if (DA.GetData("Settings", ref rawGoo) && rawGoo != null)
            {
                var scriptVar = rawGoo.ScriptVariable();
                if (scriptVar is AIRequestParameters p)
                {
                    Debug.WriteLine($"[AIStatefulAsyncComponentBase] Received AIRequestParameters: Model={p.Model}, MaxTokens={p.MaxTokens}, Temperature={p.Temperature}");
                    this.SetParameters(p);
                }
                else if (scriptVar is string s)
                {
                    this.SetParameters(AIRequestParameters.FromModel(s.Trim()));
                }
                else
                {
                    // Try string cast path (GH_String etc.)
                    string fallbackStr = rawGoo.ToString();
                    this.SetParameters(AIRequestParameters.FromModel(fallbackStr?.Trim()));
                }
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
            if (changed.Contains("AIProvider"))
            {
                this._batchUnsupportedChecked = false;
            }

            return changed;
        }

        protected override void BeforeSolveInstance()
        {
            base.BeforeSolveInstance();

            // NOTE: AIReturnSnapshot clearing is done in OnEnteringNeedsRunState (on input change)
            // and defensively in OnStateProcessing, not on every solve.
            // This prevents metrics loss when batch completes and component re-enters Processing.
        }

        /// <inheritdoc/>
        protected override void OnStateProcessing(IGH_DataAccess DA)
        {
            // When a batch submission is already active, skip spawning a new worker.
            // The poll timer (started in Read()) drives completion; no new work should begin.
            if (this._batchSubmission != null)
            {
                Debug.WriteLine("[AIStatefulAsyncComponentBase] OnStateProcessing: batch active, skipping worker spawn");
                return;
            }

            base.OnStateProcessing(DA);
        }

        /// <inheritdoc/>
        protected override void OnEnteringNeedsRunState()
        {
            Debug.WriteLine("[AIStatefulAsyncComponentBase] Entering NeedsRun state - clearing previous run batch state");
            Debug.WriteLine($"[AIStatefulAsyncComponentBase] OnEnteringNeedsRunState ENTRY: _sentinelTrees={(this._sentinelTrees == null ? "null" : $"count={this._sentinelTrees.Count}")}");

            // Clear previous response metrics and all previous-run batch state.
            // _batchSubmission and _batchPollTimer are intentionally NOT cleared here:
            // an in-flight remote batch must keep polling until it completes or fails.
            // _sentinelTrees is also NOT cleared here - it must survive until a new batch
            // starts so that loaded results can be processed.
            this.AIReturnSnapshot = null;
            this._persistedMetrics = null;
            this._batchQueue = null;
            this._batchSentinelIds = null;
            this._batchProgressCompleted = 0;
            this._batchUnsupportedChecked = false;
            this._batchStartTime = null;
            this._batchCompletionTime = null;
            this.ResetProgress();
            Debug.WriteLine($"[AIStatefulAsyncComponentBase] OnEnteringNeedsRunState EXIT: _sentinelTrees={(this._sentinelTrees == null ? "null" : $"count={this._sentinelTrees.Count}")}");
        }


        protected override void OnSolveInstancePostSolve(IGH_DataAccess DA)
        {
            // Skip output if batch is still active - outputs will be set after batch completes
            if (this._batchSubmission != null)
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
            if (this._batchSubmission != null)
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
            // Distinguish between manual component removal and document/Rhino close
            // _isDocumentRemoving is set by GH_DocumentServer.DocumentRemoved event which fires early
            if (this._batchSubmission != null)
            {
                if (_isDocumentRemoving)
                {
                    // Document/Rhino closing: preserve batch, just stop polling
                    Debug.WriteLine($"[AIStatefulAsync] RemovedFromDocument (shutdown): Preserving batch {this._batchSubmission.BatchId}");
                    this.StopBatchPollTimer();
                }
                else
                {
                    // Manual component removal: cancel the batch (no UI update needed as component is being destroyed)
                    var batchId = this._batchSubmission.BatchId;
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
            if (this._batchSubmission != null)
            {
                Debug.WriteLine($"[AIStatefulAsync] OnWorkerCompleted: Batch submission active ({this._batchSubmission.BatchId}), staying in Processing state");

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
            if (this._batchSubmission != null)
            {
                var batchId = this._batchSubmission.BatchId;
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
                var submission = this._batchSubmission;
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
                this._batchSubmission = null;

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
