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
using SmartHopper.Core.ComponentBase.Cores;
using SmartHopper.Core.ComponentBase.State;
using SmartHopper.Infrastructure.AICall.Batch;
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Returns;
using SmartHopper.Infrastructure.AIProviders;
using SmartHopper.Infrastructure.Diagnostics;
using SmartHopper.Infrastructure.Settings;

namespace SmartHopper.Core.ComponentBase
{
    /// <summary>
    /// Batch processing logic for AIStatefulAsyncComponentBase.
    /// Handles batch submission, polling, result processing, and persistence.
    /// </summary>
    public abstract partial class AIStatefulAsyncComponentBase
    {
        #region BATCH

        /// <summary>
        /// Determines whether the current request parameters specify batch mode
        /// by checking the <see cref="AIRequestParameters.BatchTier"/> flag and
        /// verifying the selected provider supports batch processing.
        /// </summary>
        /// <remarks>
        /// This method centralizes batch capability checking to prevent components
        /// from entering batch mode when the provider does not support it. If batch
        /// mode is requested but unsupported, a remark is surfaced once per run.
        /// </remarks>
        protected bool IsBatchRequest()
        {
            // Fast path: batch tier not enabled
            if (this._requestParameters?.BatchTier != true)
            {
                this._batchState.UnsupportedChecked = false;
                return false;
            }

            // Check if provider supports batch (cached per run)
            if (!this._batchState.UnsupportedChecked)
            {
                this._batchState.UnsupportedChecked = true;

                var providerName = this.GetActualAIProviderName();
                var provider = ProviderManager.Instance.GetProvider(providerName);

                if (provider is not IAIBatchProvider)
                {
                    this.SetPersistentRuntimeMessage(
                        "batch_unsupported",
                        GH_RuntimeMessageLevel.Remark,
                        $"The selected provider '{providerName}' does not support batch processing. Processing in regular mode.",
                        false);

                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Stores the sentinel tree for a named output parameter so that
        /// <see cref="OnBatchCompleted"/> can reconstruct the output tree later.
        /// </summary>
        /// <typeparam name="T">The output goo type (typically <see cref="GH_String"/> for sentinel trees).</typeparam>
        /// <param name="paramName">Output parameter name (e.g., "Result").</param>
        /// <param name="tree">The sentinel tree produced during batch collection.</param>
        protected void StoreSentinelTree<T>(string paramName, GH_Structure<T> tree)
            where T : IGH_Goo
        {
            this._batchState.SentinelTrees ??= new Dictionary<string, object>();
            this._batchState.SentinelTrees[paramName] = tree;
        }

        /// <summary>
        /// Stores the set of sentinel custom IDs for the current batch submission.
        /// Called during <see cref="SubmitBatchQueueAsync"/> to record the mapping between
        /// custom_ids and branch paths for later result reconstruction.
        /// </summary>
        /// <param name="customIds">Custom IDs from the batch submission.</param>
        protected void StoreBatchSentinelIds(IEnumerable<string> customIds)
        {
            this._batchState.SentinelIds = new HashSet<string>(customIds ?? Enumerable.Empty<string>());
            Debug.WriteLine($"[AIStatefulAsync] StoreBatchSentinelIds: stored {this._batchState.SentinelIds.Count} sentinel IDs");
        }

        /// <summary>
        /// Clears sentinel trees and batch sentinel IDs when a genuinely NEW batch is being submitted.
        /// This ensures a fresh batch starts with clean state per the lifecycle contract.
        /// </summary>
        private void ClearSentinelState()
        {
            Debug.WriteLine($"[AIStatefulAsync] ClearSentinelState: clearing _sentinelTrees (was {(this._batchState.SentinelTrees == null ? "null" : $"count={this._batchState.SentinelTrees.Count}")}) and _batchSentinelIds (was {(this._batchState.SentinelIds == null ? "null" : $"count={this._batchState.SentinelIds.Count}")})");
            this._batchState.SentinelTrees = null;
            this._batchState.SentinelIds = null;
            this._batchState.Queue = null;
        }

        /// <summary>
        /// Retrieves a previously stored sentinel tree for the given output parameter name.
        /// Returns <c>null</c> if no sentinel tree has been stored.
        /// </summary>
        /// <param name="paramName">Output parameter name (e.g., "Result").</param>
        /// <returns>The sentinel <see cref="GH_Structure{GH_String}"/>, or <c>null</c>.</returns>
        protected GH_Structure<GH_String> GetSentinelTree(string paramName)
        {
            if (this._batchState.SentinelTrees == null)
            {
                Debug.WriteLine($"[AIStatefulAsync] GetSentinelTree('{paramName}'): _sentinelTrees is null - tree was not persisted or restored");
                return null;
            }

            this._batchState.SentinelTrees.TryGetValue(paramName, out var tree);
            var result = tree as GH_Structure<GH_String>;
            Debug.WriteLine($"[AIStatefulAsync] GetSentinelTree('{paramName}'): {(result == null ? "NOT FOUND" : $"found, {result.PathCount} path(s), {result.DataCount} item(s)")}");
            return result;
        }

        /// <summary>
        /// Convenience helper for workers: after <c>RunProcessingAsync</c> finishes in batch mode,
        /// stores the sentinel tree for the given output parameter name and submits the batch queue.
        /// </summary>
        /// <typeparam name="T">The output goo type of the result dictionary.</typeparam>
        /// <param name="outputParamName">Output parameter name (e.g., "Result").</param>
        /// <param name="result">The result dictionary produced by <c>RunProcessingAsync</c>.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// <c>true</c> if batch was successfully submitted; <c>false</c> if not in batch mode
        /// or the provider does not support batch processing.
        /// </returns>
        protected async Task<bool> TrySubmitBatchAsync<T>(
            string outputParamName,
            Dictionary<string, GH_Structure<T>> result,
            CancellationToken ct)
            where T : IGH_Goo
        {
            if (!this.IsCurrentlyBatchMode()) return false;
            var submitted = await this.SubmitBatchQueueAsync(ct).ConfigureAwait(false);
            if (submitted && result != null && result.TryGetValue(outputParamName, out var sentinelTree))
            {
                this.StoreSentinelTree(outputParamName, sentinelTree);
            }

            return submitted;
        }

        /// <summary>Returns true when batch mode is active and items have been collected.</summary>
        private bool IsCurrentlyBatchMode() => this.IsBatchRequest() && this.HasBatchQueue;

        /// <summary>
        /// Gets a value indicating whether a batch job has been submitted and is currently being polled.
        /// Returns <c>false</c> during a fresh run (before submission) and after batch completion.
        /// Derived classes can use this to distinguish poll cycles from genuinely new runs.
        /// </summary>
        protected bool HasActiveBatchSubmission => this._batchState.Submission != null;

        /// <summary>
        /// Gets a value indicating whether there are queued batch requests waiting to be submitted.
        /// </summary>
        protected bool HasBatchQueue => this._batchState.Queue?.Count > 0;

        /// <summary>Cancellation token source for batch polling. Created when polling starts, disposed when stopped.</summary>
        private CancellationTokenSource _batchPollCts;

        /// <summary>
        /// Submits all queued batch requests as a single batch job via <see cref="IAIBatchProvider"/>
        /// and starts the poll timer. Call this from <c>DoWorkAsync</c> after
        /// <c>RunProcessingAsync</c> has finished collecting all items into the queue.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if submission succeeded; false if the provider doesn't support batch or the queue is empty.</returns>
        protected async Task<bool> SubmitBatchQueueAsync(CancellationToken cancellationToken = default)
        {
            // Prevent duplicate batch submissions
            if (this._batchState.Submission != null)
            {
                Debug.WriteLine($"[AIStatefulAsync] Batch submission already active ({this._batchState.Submission.BatchId}), skipping duplicate submission");
                return false;
            }

            var queue = this._batchState.Queue;
            if (queue == null || queue.Count == 0) return false;

            // Collect-only mode: LoadResultsFromFile fallback.
            // We took the component's normal processing path to build fresh sentinel trees
            // (stored via TrySubmitBatchAsync → StoreSentinelTree after we return true),
            // but we do NOT submit to the provider. Instead, we re-key the pending file-loaded
            // results to the freshly collected custom IDs (in order) and schedule finalization
            // through the normal OnBatchFinalized pipeline.
            if (this._batchCollectOnly)
            {
                Debug.WriteLine($"[AIStatefulAsync] SubmitBatchQueueAsync: collect-only mode, queue has {queue.Count} items");

                var collectedCustomIds = queue.Select(q => q.CustomId).ToList();
                this._batchState.SentinelIds = new HashSet<string>(collectedCustomIds);
                this._batchState.Queue = null;

                var pending = this._pendingFileLoadedResults;
                var messages = this._pendingFileLoadMessages ?? new List<SHRuntimeMessage>();

                // Re-key loaded results to collected custom IDs (order-based fallback).
                var rekeyed = new Dictionary<string, JObject>();
                var loadedList = pending?.Values.ToList() ?? new List<JObject>();
                int pairCount = Math.Min(collectedCustomIds.Count, loadedList.Count);
                for (int i = 0; i < pairCount; i++)
                {
                    rekeyed[collectedCustomIds[i]] = loadedList[i];
                }

                Debug.WriteLine($"[AIStatefulAsync] SubmitBatchQueueAsync: collect-only rekeyed {rekeyed.Count} result(s); collected={collectedCustomIds.Count}, loaded={loadedList.Count}");

                if (loadedList.Count < collectedCustomIds.Count)
                {
                    messages.Add(new SHRuntimeMessage(
                        SHRuntimeMessageSeverity.Warning,
                        SHRuntimeMessageOrigin.Worker,
                        SHMessageCode.ConversionFailed,
                        $"Only {loadedList.Count} of {collectedCustomIds.Count} input branch(es) have loaded results; the remaining branch(es) will stay empty."));
                }
                else if (loadedList.Count > collectedCustomIds.Count)
                {
                    messages.Add(new SHRuntimeMessage(
                        SHRuntimeMessageSeverity.Warning,
                        SHRuntimeMessageOrigin.Worker,
                        SHMessageCode.ConversionFailed,
                        $"Loaded {loadedList.Count} result(s) but only {collectedCustomIds.Count} branch(es) were collected; {loadedList.Count - collectedCustomIds.Count} result(s) were ignored."));
                }

                // Clear collect-only staging state. Finalization runs on UI thread after
                // the worker completes storing sentinel trees (which happens after this returns).
                this._batchCollectOnly = false;
                this._pendingFileLoadedResults = null;
                this._pendingFileLoadMessages = null;

                IReadOnlyDictionary<string, JObject> finalResults = rekeyed;
                Rhino.RhinoApp.InvokeOnUiThread(() =>
                {
                    try
                    {
                        this.ClearPersistentRuntimeMessages();
                        this._batchState.CompletionTime ??= 0.0;
                        this.CompleteBatchAndTransition(finalResults, messages, expectedResultCount: 0, forceState: true);
                        this.StateManager.CommitHashes();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[AIStatefulAsync] collect-only finalization failed: {ex.Message}");
                        this.SetPersistentRuntimeMessage("load_results_error", GH_RuntimeMessageLevel.Error, $"Error finalizing loaded results: {ex.Message}", false);
                        this.StateManager.ForceState(ComponentState.Error);
                        this.ExpireSolution(true);
                    }
                });

                // Return true so TrySubmitBatchAsync proceeds to call StoreSentinelTree,
                // populating _sentinelTrees for the UI-thread OnBatchCompleted reconstruction.
                return true;
            }

            // Clear sentinel state for the NEW batch per lifecycle contract.
            // This ensures a fresh batch starts with clean state.
            this.ClearSentinelState();

            var providerName = this.GetActualAIProviderName();
            var provider = ProviderManager.Instance.GetProvider(providerName);

            if (provider is not IAIBatchProvider batchProvider)
            {
                this.SetPersistentRuntimeMessage("batch_unsupported", GH_RuntimeMessageLevel.Warning, $"Provider '{providerName}' does not support batch processing. Falling back to synchronous mode.", false);
                return false;
            }

            try
            {
                var submission = await batchProvider.SubmitBatchAsync(queue, cancellationToken).ConfigureAwait(false);
                this._batchState.Submission = submission;
                
                // Store the sentinel IDs from the submission for later result mapping
                this.StoreBatchSentinelIds(submission.CustomIds);
                
                this._batchState.ProgressCompleted = 0;
                this._batchState.StartTime = DateTime.UtcNow;
                this.Message = $"Processing batch (0/{submission.CustomIds?.Count ?? 0})...";
                this.StartBatchPollTimer(cancellationToken: cancellationToken);
                Debug.WriteLine($"[AIStatefulAsync] Batch submitted: batchId={submission.BatchId}, itemCount={submission.CustomIds?.Count ?? 0}, startTime={this._batchState.StartTime:O}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AIStatefulAsync] Batch submit failed: {ex.Message}");
                this.SetPersistentRuntimeMessage("batch_error", GH_RuntimeMessageLevel.Error, $"Batch submission failed: {ex.Message}", false);
                return false;
            }
        }

        /// <summary>
        /// Starts the batch status poll timer with specified initial delay.
        /// </summary>
        /// <param name="immediateFirstPoll">If true, the first poll happens immediately (dueTime=0).
        /// Use true when restoring batch state to check if already complete.</param>
        /// <param name="cancellationToken">Optional cancellation token to link with the polling CTS.</param>
        private void StartBatchPollTimer(bool immediateFirstPoll = false, CancellationToken cancellationToken = default)
        {
            this.StopBatchPollTimer();

            // Create a new CTS, optionally linked to the provided token
            this._batchPollCts = cancellationToken.CanBeCanceled
                ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
                : new CancellationTokenSource();

            int intervalMs = Math.Max(1, SmartHopperSettings.Instance.BatchPollIntervalSeconds) * 1000;
            int dueTimeMs = immediateFirstPoll ? 0 : intervalMs;
            this._batchPollTimer = new System.Threading.Timer(this.OnBatchPollTimerTick, null, dueTimeMs, intervalMs);
            Debug.WriteLine($"[AIStatefulAsync] Batch poll timer started, dueTime={dueTimeMs}ms, interval={intervalMs}ms, batchId={this._batchState.Submission?.BatchId}");
        }

        /// <summary>Stops and disposes the poll timer and cancellation token source without cancelling the batch.</summary>
        private void StopBatchPollTimer()
        {
            var t = Interlocked.Exchange(ref this._batchPollTimer, null);
            t?.Dispose();

            var cts = Interlocked.Exchange(ref this._batchPollCts, null);
            cts?.Dispose();
        }

        /// <summary>Timer callback — fires on a thread-pool thread.</summary>
        private void OnBatchPollTimerTick(object state)
        {
            // Prevent overlapping polls
            if (Interlocked.CompareExchange(ref this._batchPollRunning, 1, 0) != 0) return;

            // Check if cancellation was requested
            if (this._batchPollCts?.IsCancellationRequested == true)
            {
                this.StopBatchPollTimer();
                Interlocked.Exchange(ref this._batchPollRunning, 0);
                return;
            }

            try
            {
                _ = this.PollBatchStatusAsync(this._batchPollCts?.Token ?? CancellationToken.None);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AIStatefulAsync] Poll tick error: {ex.Message}");
                Interlocked.Exchange(ref this._batchPollRunning, 0);
            }
        }

        /// <summary>
        /// Asynchronously polls the provider for batch status and processes results when ready.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token to stop polling.</param>
        private async Task PollBatchStatusAsync(CancellationToken cancellationToken = default)
        {
            var submission = this._batchState.Submission;
            if (submission == null)
            {
                this.StopBatchPollTimer();
                Interlocked.Exchange(ref this._batchPollRunning, 0);
                return;
            }

            // Check for timeout - stop polling after 24 hours
            // Note: We don't cancel the remote batch here - APIs handle their own timeouts
            if (this._batchState.StartTime.HasValue &&
                (DateTime.UtcNow - this._batchState.StartTime.Value) > MaxBatchPollingDuration)
            {
                Debug.WriteLine($"[AIStatefulAsync] Batch polling timeout exceeded for {submission.BatchId} - stopping local polling");

                this.StopBatchPollTimer();
                this._batchState.Submission = null;

                this.OnBatchFinalized(AIBatchState.Expired, null, null, "Batch polling timeout exceeded (24 hours)");
                this.StateManager.RequestTransition(ComponentState.Error, TransitionReason.Error);
                Rhino.RhinoApp.InvokeOnUiThread(() => this.ExpireSolution(true));
                return;
            }

            try
            {
                var providerName = submission.ProviderName;
                var provider = ProviderManager.Instance.GetProvider(providerName);

                if (provider is not IAIBatchProvider batchProvider)
                {
                    this.StopBatchPollTimer();
                    return;
                }

                var status = await batchProvider.GetBatchStatusAsync(submission, cancellationToken).ConfigureAwait(false);
                Debug.WriteLine($"[AIStatefulAsync] Batch poll result: state={status.State}, batchId={submission.BatchId}");

                switch (status.State)
                {
                    case AIBatchState.InProgress:
                        if (status.CompletedCount.HasValue)
                        {
                            this._batchState.ProgressCompleted = status.CompletedCount.Value;
                            var total = this._batchState.Submission?.CustomIds?.Count ?? 0;
                            Rhino.RhinoApp.InvokeOnUiThread(() =>
                            {
                                this.Message = $"Processing batch ({this._batchState.ProgressCompleted}/{total})...";
                                this.OnDisplayExpired(false);
                            });
                        }

                        break;

                    case AIBatchState.Completed:
                        this.StopBatchPollTimer();
                        this._batchState.Submission = null;

                        // Calculate batch completion time and store for FinishResults to consume
                        if (this._batchState.StartTime.HasValue)
                        {
                            this._batchState.CompletionTime = (DateTime.UtcNow - this._batchState.StartTime.Value).TotalSeconds;
                            Debug.WriteLine($"[AIStatefulAsync] Batch completed: batchId={submission.BatchId}, completionTime={this._batchState.CompletionTime:F2}s");
                            this._batchState.StartTime = null;
                        }

                        this.CompleteBatchAndTransition(status.Results, status.Messages, submission.CustomIds?.Count ?? 0);
                        break;

                    case AIBatchState.Failed:
                    case AIBatchState.Cancelled:
                    case AIBatchState.Expired:
                        this.StopBatchPollTimer();
                        this._batchState.Submission = null;

                        // Centralized batch finalization for terminal failure cases
                        this.OnBatchFinalized(status.State, null, status.Messages, status.ErrorMessage);

                        // Transition to Error state for terminal failures
                        this.StateManager.RequestTransition(ComponentState.Error, TransitionReason.Error);
                        Rhino.RhinoApp.InvokeOnUiThread(() => this.ExpireSolution(true));
                        break;
                }
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("[AIStatefulAsync] Batch polling cancelled - preserving remote batch");

                // Always preserve the remote batch on poll cancellation
                // The batch continues on the provider and can be resumed
                this.StopBatchPollTimer();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AIStatefulAsync] PollBatchStatus error: {ex.Message}");
            }
            finally
            {
                Interlocked.Exchange(ref this._batchPollRunning, 0);
            }
        }

        /// <summary>
        /// Called when a batch job completes. Override in derived classes to decode results,
        /// reconstruct output trees, and set persistent outputs.
        /// </summary>
        /// <param name="results">
        /// Dictionary mapping each <c>customId</c> (sentinel key) to the provider-specific
        /// raw response body. Use the provider's <c>Decode</c> method on each value to extract
        /// the AI response.
        /// </param>
        /// <param name="messages">
        /// Item-level diagnostic messages from the provider (errors, warnings, info).
        /// May be null or empty if all items succeeded.
        /// </param>
        protected virtual void OnBatchCompleted(IReadOnlyDictionary<string, JObject> results, IReadOnlyList<SHRuntimeMessage> messages = null)
        {
        }

        /// <summary>
        /// Centralized batch finalization handler called for both successful completion
        /// and terminal failure states (Failed, Cancelled, Expired). Surfaces all messages
        /// via <see cref="SurfaceMessagesFromReturn"/> and delegates to <see cref="OnBatchCompleted"/>
        /// for success cases.
        /// </summary>
        /// <param name="state">The final batch state.</param>
        /// <param name="results">Result dictionary for success cases; null for failures.</param>
        /// <param name="messages">Item-level diagnostic messages from the provider.</param>
        /// <param name="errorMessage">Error description for failure states.</param>
        private void OnBatchFinalized(AIBatchState state, IReadOnlyDictionary<string, JObject> results, IReadOnlyList<SHRuntimeMessage> messages, string errorMessage)
        {
            // Build a unified AIReturn to collect all messages for surfacing.
            // Skip Request/Metrics validation: this AIReturn exists solely to relay
            // already-captured messages, not to represent a real call.
            var unifiedReturn = new AIReturn
            {
                SkipRequestValidation = true,
                SkipMetricsValidation = true,
            };
            var hasMessages = false;

            // Add terminal state message for failures
            if (state != AIBatchState.Completed)
            {
                unifiedReturn.AddRuntimeMessage(
                    SHRuntimeMessageSeverity.Error,
                    SHRuntimeMessageOrigin.Provider,
                    $"Batch {state.ToString().ToLowerInvariant()}: {errorMessage ?? "no details"}");
                hasMessages = true;
            }

            // Add all item-level messages (works for both success and failure)
            if (messages != null && messages.Count > 0)
            {
                foreach (var msg in messages)
                {
                    unifiedReturn.AddRuntimeMessage(msg.Severity, msg.Origin, msg.Message);
                }

                hasMessages = true;
            }

            // Surface all collected messages in one call
            if (hasMessages)
            {
                this.SurfaceMessagesFromReturn(unifiedReturn, "batch_item");
            }

            // For success states, delegate to the virtual OnBatchCompleted hook
            if (state == AIBatchState.Completed && (results != null || hasMessages))
            {
                try
                {
                    this.OnBatchCompleted(results, messages);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[AIStatefulAsync] OnBatchCompleted error: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Shared batch-completion flow used by both live polling (<see cref="PollBatchStatusAsync"/>)
        /// and manual file load (<c>LoadResultsFromFile</c>): invokes <see cref="OnBatchFinalized"/>
        /// in the <see cref="AIBatchState.Completed"/> branch, then requests the appropriate state
        /// transition (Error / NeedsRun / Completed) based on message severity and sentinel coverage,
        /// and finally expires the solution on the UI thread.
        /// </summary>
        /// <param name="results">Decoded result bodies keyed by <c>customId</c>.</param>
        /// <param name="messages">Item-level diagnostic messages from the provider.</param>
        /// <param name="expectedResultCount">
        /// Number of sentinels expected to resolve (typically <c>submission.CustomIds.Count</c>).
        /// Pass <c>0</c> when the expected count is unknown (e.g. manual file load) to disable
        /// the unresolved-sentinels check.
        /// </param>
        /// <param name="forceState">
        /// When <c>true</c>, uses <see cref="ComponentStateManager.ForceState"/> instead of
        /// <see cref="ComponentStateManager.RequestTransition"/> for the final transition.
        /// Needed by the manual file-load path, where the source state may be <c>Error</c>,
        /// <c>NeedsRun</c>, <c>Waiting</c>, or already <c>Completed</c> — none of which have a
        /// valid direct transition to the target state per <c>IsValidTransition</c>. Live
        /// polling always arrives from <c>Processing</c> and must pass <c>false</c> to keep
        /// the state machine honest.
        /// </param>
        private void CompleteBatchAndTransition(
            IReadOnlyDictionary<string, JObject> results,
            IReadOnlyList<SHRuntimeMessage> messages,
            int expectedResultCount,
            bool forceState = false)
        {
            var actualCount = results?.Count ?? 0;
            var hasResults = actualCount > 0;
            var hasErrors = messages?.Any(m => m.Severity == SHRuntimeMessageSeverity.Error) ?? false;
            var allFailed = !hasResults && hasErrors;
            var hasUnresolvedSentinels = expectedResultCount > 0 && actualCount < expectedResultCount;

            // Severity-downgrade rule: item-level errors are fatal to the COMPONENT only when
            // the entire batch failed. When at least one item succeeded, demote per-item Error
            // messages to Warning so the component reaches Completed state with visible warnings
            // (matches the requirement: "error only if all items fail; otherwise warnings").
            IReadOnlyList<SHRuntimeMessage> outgoingMessages = messages;
            if (hasResults && hasErrors && messages != null)
            {
                outgoingMessages = messages
                    .Select(m => m.Severity == SHRuntimeMessageSeverity.Error
                        ? new SHRuntimeMessage(SHRuntimeMessageSeverity.Warning, m.Origin, m.Code, m.Message, m.Surfaceable)
                        : m)
                    .ToList();
            }

            // Centralized batch finalization for success case
            this.OnBatchFinalized(AIBatchState.Completed, results, outgoingMessages, null);

            ComponentState targetState;
            TransitionReason targetReason;
            if (allFailed)
            {
                Debug.WriteLine("[AIStatefulAsync] Batch produced no successful results; transitioning to Error");
                targetState = ComponentState.Error;
                targetReason = TransitionReason.Error;
            }
            else if (hasUnresolvedSentinels && !hasErrors)
            {
                // Missing results without an explicit error (rare; only from live polling) →
                // leave room for re-execution.
                Debug.WriteLine($"[AIStatefulAsync] Batch completed but {expectedResultCount - actualCount}/{expectedResultCount} sentinels unresolved, transitioning to NeedsRun");
                targetState = ComponentState.NeedsRun;
                targetReason = TransitionReason.InputChanged;
            }
            else
            {
                if (hasErrors)
                {
                    Debug.WriteLine($"[AIStatefulAsync] Batch completed with partial failures ({actualCount} ok), transitioning to Completed with warnings");
                }

                targetState = ComponentState.Completed;
                targetReason = TransitionReason.ProcessingComplete;
            }

            if (forceState)
            {
                // Bypasses IsValidTransition — required when the caller may already be in
                // a terminal state (e.g. Error after a failed poll) and a normal
                // RequestTransition would be rejected. Going through an intermediate
                // Processing state is NOT an option: OnEnteringProcessing clears
                // _sentinelTrees when _batchSubmission is null (new batch starting), which
                // would destroy the customId → branch-path map needed to populate outputs
                // from the freshly loaded results.
                this.StateManager.ForceState(targetState);
            }
            else
            {
                this.StateManager.RequestTransition(targetState, targetReason);
            }

            Rhino.RhinoApp.InvokeOnUiThread(() => this.ExpireSolution(true));
        }

        #endregion
    }
}
