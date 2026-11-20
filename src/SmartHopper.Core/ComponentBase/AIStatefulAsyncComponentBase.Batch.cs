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
using SmartHopper.Core.ComponentBase.State;
using SmartHopper.Infrastructure.AIProviders;
using SmartHopper.Infrastructure.Dialogs;
using SmartHopper.Infrastructure.Settings;
using SmartHopper.ProviderSdk.AICall.Batch;
using SmartHopper.ProviderSdk.AICall.Core.Base;
using SmartHopper.ProviderSdk.AICall.Core.Requests;
using SmartHopper.ProviderSdk.AICall.Core.Returns;
using SmartHopper.ProviderSdk.AICall.Metrics;
using SmartHopper.ProviderSdk.AIModels;
using SmartHopper.ProviderSdk.AIProviders;
using SmartHopper.ProviderSdk.Diagnostics;
using SmartHopper.ProviderSdk.Logging;
using SmartHopper.ProviderSdk.Settings;

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
                this._batchState.BatchSupported = false;
                return false;
            }

            // Provider/model support is validated once per run and the outcome —
            // including a negative result — is cached, so a rejected provider
            // stays rejected for the rest of the run instead of flipping to
            // supported on the next call.
            if (!this._batchState.UnsupportedChecked)
            {
                this._batchState.UnsupportedChecked = true;
                this._batchState.BatchSupported = this.CheckBatchSupport();
            }

            return this._batchState.BatchSupported;
        }

        /// <summary>
        /// Performs the one-time batch-capability validation for the current run:
        /// the selected provider must implement <see cref="IAIBatchProvider"/> and
        /// the selected model must not explicitly exclude batch support.
        /// </summary>
        /// <returns><c>true</c> when the provider and model support batch.</returns>
        private bool CheckBatchSupport()
        {
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

            // Also check if the selected model explicitly does not support batch.
            var model = this.GetModel();
            if (!string.IsNullOrWhiteSpace(model))
            {
                var capabilities = AIModelCapabilityRegistry.Instance.GetCapabilities(providerName, model);
                if (capabilities?.SupportsBatch == false)
                {
                    this.SetPersistentRuntimeMessage(
                        "batch_model_unsupported",
                        GH_RuntimeMessageLevel.Remark,
                        $"Model '{model}' does not support batch processing with provider '{providerName}'. Processing in regular mode.",
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
            this._batchState.SentinelTrees[paramName] = ToSentinelStringTree(tree);
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Captures the produced output trees so the parent-owned submission handoff
        /// can derive sentinel trees itself — workers queue deferred work through
        /// <c>RunProcessingAsync</c> and never have to forward their outputs for
        /// batch submission to happen.
        /// </remarks>
        protected override void OnProcessingOutputsReady<U>(IReadOnlyDictionary<string, GH_Structure<U>> outputs)
        {
            if (!this.IsBatchRequest() || outputs == null)
            {
                return;
            }

            var collected = this._batchState.CollectedOutputs ??= new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (var kvp in outputs)
            {
                collected[kvp.Key] = kvp.Value;
            }
        }

        /// <summary>
        /// Derives sentinel trees from the captured <see cref="Cores.BatchRunState.CollectedOutputs"/>:
        /// every output parameter whose tree contains at least one sentinel gets a
        /// normalized <see cref="GH_Structure{GH_String}"/> copy keyed by parameter name.
        /// </summary>
        /// <returns>Parameter name → sentinel tree; empty when nothing mappable was collected.</returns>
        private Dictionary<string, GH_Structure<GH_String>> DeriveSentinelTreesFromCollectedOutputs()
        {
            var derived = new Dictionary<string, GH_Structure<GH_String>>(StringComparer.Ordinal);
            var outputs = this._batchState.CollectedOutputs;
            if (outputs == null)
            {
                return derived;
            }

            foreach (var kvp in outputs)
            {
                if (kvp.Value is not IGH_Structure structure)
                {
                    continue;
                }

                var sentinelTree = ToSentinelStringTree(structure);
                if (BatchSentinel.TreeContainsSentinel(sentinelTree))
                {
                    derived[kvp.Key] = sentinelTree;
                }
            }

            return derived;
        }

        /// <summary>
        /// Parent-owned submission handoff: submits the collected queue and, on
        /// success, stores the derived sentinel trees. On failure the run is
        /// surfaced as an error and expired — unresolved queued work never
        /// reaches the Completed path. When the run is invalidated mid-flight
        /// (epoch moved) the late result is dropped silently — the destructive
        /// path already owns the transition.
        /// </summary>
        /// <param name="sentinelTrees">Derived sentinel trees to persist on success.</param>
        /// <param name="submitEpoch">Run epoch captured when the submission was marked
        /// pending on the UI thread; a mismatch on return means the run was
        /// invalidated or superseded — discard the late result silently.</param>
        private async Task SubmitCollectedBatchAsync(Dictionary<string, GH_Structure<GH_String>> sentinelTrees, int submitEpoch)
        {
            try
            {
                var submitted = await this.SubmitBatchQueueAsync(CancellationToken.None, submitEpoch).ConfigureAwait(false);
                if (this._batchState.Epoch != submitEpoch)
                {
                    // Stale submission: run was cleared/cancelled/superseded while
                    // the provider call was in flight. SubmitBatchQueueAsync already
                    // remote-cancelled the job — just stop here.
                    Logger.For<AIStatefulAsyncComponentBase>().Trace("[AIStatefulAsync] Deferred batch submission returned after epoch change; discarding stale result");
                    return;
                }

                if (submitted)
                {
                    // Store after submission: SubmitBatchQueueAsync clears sentinel
                    // state for the new batch before it prepares the queue.
                    foreach (var kvp in sentinelTrees)
                    {
                        this.StoreSentinelTree(kvp.Key, kvp.Value);
                    }

                    // Sentinels installed — complete the boundary: drain any
                    // staged import and begin polling the live submission.
                    this.CompleteSubmissionSetup();
                    return;
                }

                this.FailDeferredSubmission("Queued batch work could not be submitted; see earlier batch errors for details.");
            }
            catch (Exception ex)
            {
                if (this._batchState.Epoch != submitEpoch)
                {
                    Logger.For<AIStatefulAsyncComponentBase>().Trace($"[AIStatefulAsync] Stale deferred batch submission faulted; discarding: {ex.Message}");
                    return;
                }

                Logger.For<AIStatefulAsyncComponentBase>().Exception(ex, $"[AIStatefulAsync] Deferred batch submission error: {ex.Message}");
                this.FailDeferredSubmission($"Batch submission failed: {ex.Message}");
            }
            finally
            {
                // Release the in-flight marker only while it still belongs to
                // this submission — a newer run may already have set its own.
                if (this._batchState.PendingSubmissionEpoch == submitEpoch)
                {
                    this._batchState.PendingSubmissionEpoch = -1;
                }
            }
        }

        /// <summary>
        /// Terminal path for a queue that could not be submitted: surfaces the
        /// failure, transitions to <see cref="ComponentState.Error"/> and expires
        /// the solution. Marshaled to the UI thread; submission failures already
        /// carry their own persistent error messages, so this stays terse.
        /// </summary>
        private void FailDeferredSubmission(string detail)
        {
            Rhino.RhinoApp.InvokeOnUiThread(() =>
            {
                this.SetPersistentRuntimeMessage(
                    "batch_submit_unresolved",
                    GH_RuntimeMessageLevel.Error,
                    detail,
                    transitionToError: false);
                this.StateManager.RequestTransition(ComponentState.Error, TransitionReason.Error);
                this.ExpireSolution(true);
            });
        }

        /// <summary>
        /// Captures component-specific decode context for an in-flight batch so a
        /// save/reopen restores everything needed to interpret provider results —
        /// e.g. the source list items behind a <c>list_filter</c> index response,
        /// or the submitted Fallback value for Boolean components. Returned JSON
        /// is persisted under <see cref="Contracts.PersistenceKeys.BatchDecodeContext"/>.
        /// </summary>
        /// <returns>A JSON object to persist, or <c>null</c> when nothing is needed.</returns>
        protected virtual JObject CaptureBatchDecodeContext() => null;

        /// <summary>
        /// Restores the decode context captured by <see cref="CaptureBatchDecodeContext"/>.
        /// Invoked during <c>Read</c> before resumed polling can deliver results.
        /// </summary>
        /// <param name="context">The previously persisted JSON object.</param>
        protected virtual void RestoreBatchDecodeContext(JObject context)
        {
        }

        /// <summary>
        /// Normalizes a sentinel tree to <see cref="GH_Structure{GH_String}"/> items.
        /// Workers may produce sentinel trees typed as <see cref="GH_Structure{IGH_Goo}"/>;
        /// the generic type is invariant, so storing them unconverted makes the
        /// <see cref="GH_Structure{GH_String}"/> cast in <see cref="GetSentinelTree"/>
        /// return null and batch reconstruction never populates outputs.
        /// </summary>
        private static GH_Structure<GH_String> ToSentinelStringTree<T>(GH_Structure<T> tree)
            where T : IGH_Goo
        {
            if (tree is GH_Structure<GH_String> stringTree)
            {
                return stringTree;
            }

            return ToSentinelStringTree((IGH_Structure)tree);
        }

        /// <summary>
        /// Non-generic overload of the sentinel-tree normalization, used when the
        /// captured output trees are only known as <see cref="IGH_Structure"/>.
        /// </summary>
        private static GH_Structure<GH_String> ToSentinelStringTree(IGH_Structure tree)
        {
            var converted = new GH_Structure<GH_String>();
            if (tree == null)
            {
                return converted;
            }

            foreach (var path in tree.Paths)
            {
                foreach (var item in tree.get_Branch(path))
                {
                    converted.Append(item is GH_String str ? str : new GH_String(item?.ToString() ?? string.Empty), path);
                }
            }

            return converted;
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
            Logger.For<AIStatefulAsyncComponentBase>().Trace($"[AIStatefulAsync] StoreBatchSentinelIds: stored {this._batchState.SentinelIds.Count} sentinel IDs");
        }

        /// <summary>
        /// Clears sentinel trees and batch sentinel IDs when a genuinely NEW batch is being submitted.
        /// This ensures a fresh batch starts with clean state per the lifecycle contract.
        /// </summary>
        private void ClearSentinelState()
        {
            Logger.For<AIStatefulAsyncComponentBase>().Trace($"[AIStatefulAsync] ClearSentinelState: clearing _sentinelTrees (was {(this._batchState.SentinelTrees == null ? "null" : $"count={this._batchState.SentinelTrees.Count}")}) and _batchSentinelIds (was {(this._batchState.SentinelIds == null ? "null" : $"count={this._batchState.SentinelIds.Count}")})");
            this._batchState.SentinelTrees = null;
            this._batchState.SentinelIds = null;
            this._batchState.Queue = null;
            this._batchState.SubmittedProviderName = null;
            this._batchState.SubmittedModels = null;
            this._batchState.SubmittedRoles = null;
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
                Logger.For<AIStatefulAsyncComponentBase>().Trace($"[AIStatefulAsync] GetSentinelTree('{paramName}'): _sentinelTrees is null - tree was not persisted or restored");
                return null;
            }

            this._batchState.SentinelTrees.TryGetValue(paramName, out var tree);
            var result = tree switch
            {
                GH_Structure<GH_String> stringTree => stringTree,
                GH_Structure<IGH_Goo> gooTree => ToSentinelStringTree(gooTree),
                _ => null,
            };
            Logger.For<AIStatefulAsyncComponentBase>().Trace($"[AIStatefulAsync] GetSentinelTree('{paramName}'): {(result == null ? "NOT FOUND" : $"found, {result.PathCount} path(s), {result.DataCount} item(s)")}");
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

            if (submitted)
            {
                // Sentinels are installed — safe to drain a staged import and
                // begin polling. Ordering matters: a poll completing now can
                // only run against a fully prepared reconstruction map.
                this.CompleteSubmissionSetup(ct);
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
        /// The submission the running poll timer serves, or null when idle.
        /// <see cref="StopBatchPollTimer"/> uses it as an ownership check: a
        /// stale caller (late poll callback, remote-cancel finally) must not
        /// tear down the timer a newer run just started.
        /// </summary>
        private AIBatchSubmission _pollTimerSubmission;

        /// <summary>
        /// Serializes poll-timer ownership reads and writes so a timer install
        /// and a teardown can never interleave: without it, a stale poll tick
        /// that observed <c>Submission == null</c> could tear down a timer a
        /// newer run installed in the same nanosecond window.
        /// </summary>
        private readonly object _pollTimerGate = new object();

        /// <summary>
        /// Submits all queued batch requests as a single batch job via <see cref="IAIBatchProvider"/>
        /// and starts the poll timer. Call this from <c>DoWorkAsync</c> after
        /// <c>RunProcessingAsync</c> has finished collecting all items into the queue.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <param name="expectedEpoch">
        /// Run epoch the late-arrival gate compares against. The deferred
        /// handoff passes the epoch captured when the pending marker was set on
        /// the UI thread, so an invalidation landing anywhere in the
        /// preparation/submit window still reads as stale; <c>null</c> captures
        /// the epoch here (direct worker submissions).
        /// </param>
        /// <returns>True if submission succeeded; false if the provider doesn't support batch or the queue is empty.</returns>
        protected async Task<bool> SubmitBatchQueueAsync(CancellationToken cancellationToken = default, int? expectedEpoch = null)
        {
            // Prevent duplicate batch submissions
            if (this._batchState.Submission != null)
            {
                Logger.For<AIStatefulAsyncComponentBase>().Warning($"[AIStatefulAsync] Batch submission already active ({this._batchState.Submission.BatchId}), skipping duplicate submission");
                return false;
            }

            var queue = this._batchState.Queue;
            if (queue == null || queue.Count == 0) return false;

            // Collect-only mode: LoadResultsFromFile fallback.
            // We took the component's normal processing path to build fresh sentinel trees
            // (stored via TrySubmitBatchAsync → StoreSentinelTree after we return true),
            // but we do NOT submit to the provider. Instead, we re-key the pending file-loaded
            // results to the freshly collected custom IDs (in order) and stage them so the
            // caller finalizes through CompleteSubmissionSetup AFTER the sentinel trees are
            // installed — finalization can never observe an empty reconstruction map.
            if (this._batchCollectOnly)
            {
                Logger.For<AIStatefulAsyncComponentBase>().Trace($"[AIStatefulAsync] SubmitBatchQueueAsync: collect-only mode, queue has {queue.Count} items");

                var collectedCustomIds = queue.Select(q => q.CustomId).ToList();
                this._batchState.SentinelIds = new HashSet<string>(collectedCustomIds);
                this.CaptureSubmittedRequestInfo(queue, this.GetActualAIProviderName());
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

                Logger.For<AIStatefulAsyncComponentBase>().Trace($"[AIStatefulAsync] SubmitBatchQueueAsync: collect-only rekeyed {rekeyed.Count} result(s); collected={collectedCustomIds.Count}, loaded={loadedList.Count}");

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

                // Stage the import for CompleteSubmissionSetup — dispatched only after
                // the caller installs the sentinel trees. Expected ids are the freshly
                // collected ones, so missing/undecodable items count correctly.
                this._batchState.StagedImportResults = rekeyed;
                this._batchState.StagedImportMessages = messages;
                this._batchCollectOnly = false;
                this._pendingFileLoadedResults = null;
                this._pendingFileLoadMessages = null;

                // Return true so TrySubmitBatchAsync proceeds to call StoreSentinelTree,
                // populating _sentinelTrees for the staged-import reconstruction.
                return true;
            }

            // Single submission owner: the pending marker must cover the whole
            // prepare+submit window regardless of which path submitted. The
            // parent handoff marks it synchronously on the UI thread and passes
            // the epoch in; direct worker submissions claim it here so
            // BatchInFlight is already true while Submission is still null —
            // phantom workers, mid-batch prompts and cancellations all key off
            // that one predicate.
            var ownsPendingMarker = !expectedEpoch.HasValue;
            var submitEpoch = expectedEpoch ?? this._batchState.BeginPendingSubmission();
            try
            {
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

            // Model-bound preparation runs before submission, not during decode: each queued
            // request gets its outbound view (media conversion included) through the same
            // central preparation used by Exec. Blocking errors fail the whole submission
            // explicitly instead of submitting malformed payloads.
            var preparedQueue = new List<(string CustomId, AIRequestCall Request, string MetricsRole)>(queue.Count);
            var preparationErrors = new List<string>();
            foreach (var (customId, queuedRequest, metricsRole) in queue)
            {
                AIRequestPreparation preparation = null;
                try
                {
                    // Queued requests bypass Exec: request policies and approved-source
                    // materialization must run before preparation, exactly as in Exec.
                    await SmartHopper.ProviderSdk.Hosting.ProviderSdkHost.PolicyPipeline
                        .ApplyRequestPoliciesAsync(queuedRequest).ConfigureAwait(false);

                    preparation = await SmartHopper.ProviderSdk.Hosting.ProviderSdkHost.PolicyPipeline
                        .PrepareExecutionAsync(queuedRequest, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    preparationErrors.Add($"Batch item {customId}: {ex.Message}");
                    continue;
                }

                if (preparation?.Request != null)
                {
                    // Shared outbound gate (modality + structural + trust) — same as Exec.
                    preparation.Messages.AddRange(AIRequestPreparation.ValidateOutboundRequest(preparation.Request));
                }

                var errors = preparation?.Messages?
                    .Where(m => m != null && m.Severity == SHRuntimeMessageSeverity.Error)
                    .Select(m => m.Message)
                    .ToList() ?? new List<string> { "preparation returned no result" };

                if (preparation?.ExtraMetrics != null)
                {
                    // Charged helper usage is recorded even when the submission later aborts.
                    foreach (var extra in preparation.ExtraMetrics)
                    {
                        if (extra != null)
                        {
                            this.CombineIntoPersistedMetrics(extra, "fallback");
                        }
                    }
                }

                if (preparation == null || preparation.Request == null || errors.Count > 0)
                {
                    preparationErrors.Add($"Batch item {customId}: {string.Join("; ", errors)}");
                    continue;
                }

                preparedQueue.Add((customId, preparation.Request, metricsRole));
            }

            if (preparationErrors.Count > 0)
            {
                this.SetPersistentRuntimeMessage(
                    "batch_preparation_error",
                    GH_RuntimeMessageLevel.Error,
                    $"Batch submission aborted — media preparation failed:\n{string.Join("\n", preparationErrors)}",
                    false);
                return false;
            }

            // Late-arrival gate: the deferred handoff passes the epoch captured
            // when the pending marker was set on the UI thread; direct worker
            // submissions captured it above when claiming the marker — an
            // invalidation landing anywhere inside the preparation/submit
            // window then still reads as stale.
            try
            {
                var submission = await batchProvider.SubmitBatchAsync(
                    preparedQueue.Select(q => (q.CustomId, q.Request)).ToList(),
                    cancellationToken).ConfigureAwait(false);

                // Late-arrival gate: if the run was invalidated or superseded
                // while the provider call was in flight, the epoch moved. The
                // job exists upstream but belongs to a dead run — cancel it
                // remotely instead of installing a zombie Submission.
                if (this._batchState.Epoch != submitEpoch)
                {
                    Logger.For<AIStatefulAsyncComponentBase>().Warning($"[AIStatefulAsync] Batch submit returned after run invalidation; cancelling remote batch {submission?.BatchId}");
                    try
                    {
                        await batchProvider.CancelBatchAsync(submission, CancellationToken.None).ConfigureAwait(false);
                    }
                    catch (Exception cancelEx)
                    {
                        Logger.For<AIStatefulAsyncComponentBase>().Exception(cancelEx, $"[AIStatefulAsync] Failed to cancel stale batch {submission?.BatchId}: {cancelEx.Message}");
                    }

                    return false;
                }

                this._batchState.Submission = submission;

                // A new run owns completion now: any finalization callback queued
                // by the previous run (poll completion on the UI dispatcher, a
                // detached import) reads as stale from this point on.
                this._batchState.NoteSubmissionInstalled();
                this.CaptureSubmittedRequestInfo(preparedQueue, submission.ProviderName ?? providerName);

                // Store the sentinel IDs from the submission for later result mapping
                this.StoreBatchSentinelIds(submission.CustomIds);

                // Commit the input-hash and provider-selection baselines at submission.
                // The batch was collected from exactly the snapshot captured at gather
                // time, but the state machine stays in Processing until poll completion
                // and never reaches OnWorkerCompleted — without this commit the
                // post-completion solve sees every input as changed and resubmits the
                // batch in an endless loop. Committing the snapshot (rather than the
                // mutable pending values) keeps edits made while the batch was
                // collecting or submitting pending, so they still trigger the
                // mid-batch re-request prompt and post-completion recalculation.
                this.CommitRunInputSnapshot();
                
                this._batchState.ProgressCompleted = 0;
                this._batchState.StartTime = DateTime.UtcNow;
                this.Message = $"Batch 0/{submission.CustomIds?.Count ?? 0}...";

                // Polling is NOT started here: callers complete the submission
                // boundary via CompleteSubmissionSetup only after the sentinel
                // trees are installed, so no poll can finalize against an empty
                // reconstruction map.
                Logger.For<AIStatefulAsyncComponentBase>().Trace($"[AIStatefulAsync] Batch submitted: batchId={submission.BatchId}, itemCount={submission.CustomIds?.Count ?? 0}, startTime={this._batchState.StartTime:O}");
                return true;
            }
            catch (Exception ex)
            {
                Logger.For<AIStatefulAsyncComponentBase>().Exception(ex, $"[AIStatefulAsync] Batch submit failed: {ex.Message}");
                this.SetPersistentRuntimeMessage("batch_error", GH_RuntimeMessageLevel.Error, $"Batch submission failed: {ex.Message}", false);
                return false;
            }
            }
            finally
            {
                // Release the in-flight marker only while it still belongs to
                // this submission — and only when this call claimed it (direct
                // worker path). The parent handoff owns its own marker release.
                // On success Submission covers BatchInFlight from here on.
                if (ownsPendingMarker && this._batchState.PendingSubmissionEpoch == submitEpoch)
                {
                    this._batchState.PendingSubmissionEpoch = -1;
                }
            }
        }

        /// <summary>
        /// Completes the submission boundary after the sentinel trees have been
        /// installed by the caller: drains a staged file import (collect-only
        /// path) and starts the poll timer for a live remote submission. MUST
        /// run after <c>StoreSentinelTree</c> — never before — so neither
        /// completion nor polling can observe an empty reconstruction map.
        /// </summary>
        /// <param name="cancellationToken">Token to link with the polling CTS.</param>
        private void CompleteSubmissionSetup(CancellationToken cancellationToken = default)
        {
            this.FinalizeStagedImportIfAny();
            if (this._batchState.Submission != null)
            {
                this.StartBatchPollTimer(cancellationToken: cancellationToken);
            }
        }

        /// <summary>
        /// Drops an armed-but-never-consumed collect-only import. The arm is a
        /// component field (not <see cref="BatchRunState"/>) so it survives the
        /// forced run's own per-run reset — but that also means a run ending
        /// without reaching <c>SubmitBatchQueueAsync</c> leaves a stale arm
        /// that could hijack a later batch. Run-end hooks call this to bound
        /// the arm to a single collection cycle.
        /// </summary>
        private void ClearCollectOnlyArm()
        {
            this._batchCollectOnly = false;
            this._pendingFileLoadedResults = null;
            this._pendingFileLoadMessages = null;
        }

        /// <summary>
        /// Drains results staged by the collect-only branch of
        /// <c>SubmitBatchQueueAsync</c>: runs the same completion pipeline as a
        /// poll completion, with the freshly collected sentinel ids as the
        /// expectation so missing or undecodable items classify correctly.
        /// </summary>
        private void FinalizeStagedImportIfAny()
        {
            var staged = this._batchState.StagedImportResults;
            if (staged == null)
            {
                return;
            }

            var messages = this._batchState.StagedImportMessages ?? new List<SHRuntimeMessage>();
            this._batchState.StagedImportResults = null;
            this._batchState.StagedImportMessages = null;
            var expectedIds = this._batchState.SentinelIds;

            Rhino.RhinoApp.InvokeOnUiThread(() =>
            {
                try
                {
                    this.ClearPersistentRuntimeMessages();
                    this._batchState.CompletionTime ??= 0.0;
                    this.CompleteBatchAndTransition(staged, messages, expectedIds, forceState: true);
                    this.CommitRunInputSnapshot();
                }
                catch (Exception ex)
                {
                    Logger.For<AIStatefulAsyncComponentBase>().Exception(ex, $"[AIStatefulAsync] collect-only finalization failed: {ex.Message}");
                    this.SetPersistentRuntimeMessage("load_results_error", GH_RuntimeMessageLevel.Error, $"Error finalizing loaded results: {ex.Message}", false);
                    this.StateManager.ForceState(ComponentState.Error);
                    this.ExpireSolution(true);
                }
            });
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
            var cts = cancellationToken.CanBeCanceled
                ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
                : new CancellationTokenSource();

            int intervalMs = Math.Max(1, SmartHopperSettings.Instance.BatchPollIntervalSeconds) * 1000;
            int dueTimeMs = immediateFirstPoll ? 0 : intervalMs;
            var timer = new System.Threading.Timer(this.OnBatchPollTimerTick, null, dueTimeMs, intervalMs);

            // Install under the gate so an orphaned teardown or a stale stop
            // cannot slip between the ownership stamp and the field writes.
            lock (this._pollTimerGate)
            {
                this._pollTimerSubmission = this._batchState.Submission;
                this._batchPollCts = cts;
                this._batchPollTimer = timer;
            }

            Logger.For<AIStatefulAsyncComponentBase>().Trace($"[AIStatefulAsync] Batch poll timer started, dueTime={dueTimeMs}ms, interval={intervalMs}ms, batchId={this._batchState.Submission?.BatchId}");
        }

        /// <summary>
        /// Stops and disposes the poll timer and cancellation token source
        /// without cancelling the batch. When <paramref name="expectedSubmission"/>
        /// is given and the timer currently serves a different (newer)
        /// submission, this is a no-op — a stale callback must not tear down a
        /// newer run's polling.
        /// </summary>
        private void StopBatchPollTimer(AIBatchSubmission expectedSubmission = null)
        {
            System.Threading.Timer timer;
            CancellationTokenSource cts;
            lock (this._pollTimerGate)
            {
                if (expectedSubmission != null
                    && this._pollTimerSubmission != null
                    && !ReferenceEquals(this._pollTimerSubmission, expectedSubmission))
                {
                    Logger.For<AIStatefulAsyncComponentBase>().Trace($"[AIStatefulAsync] Ignoring stale timer stop for batch {expectedSubmission.BatchId} — timer now serves {this._pollTimerSubmission.BatchId}");
                    return;
                }

                this._pollTimerSubmission = null;
                timer = this._batchPollTimer;
                this._batchPollTimer = null;
                cts = this._batchPollCts;
                this._batchPollCts = null;
            }

            // Dispose outside the gate: Timer.Dispose may wait for callbacks.
            timer?.Dispose();
            cts?.Dispose();
        }

        /// <summary>
        /// Tears the poll timer down only when no submission claims it. Used by
        /// poll callbacks that found <c>Submission == null</c>: the timer may
        /// already serve a newer run's submission — leave that case alone.
        /// </summary>
        private void StopOrphanedPollTimer()
        {
            System.Threading.Timer timer;
            CancellationTokenSource cts;
            lock (this._pollTimerGate)
            {
                if (this._pollTimerSubmission != null)
                {
                    return;
                }

                timer = this._batchPollTimer;
                this._batchPollTimer = null;
                cts = this._batchPollCts;
                this._batchPollCts = null;
            }

            timer?.Dispose();
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
                Logger.For<AIStatefulAsyncComponentBase>().Exception(ex, $"[AIStatefulAsync] Poll tick error: {ex.Message}");
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
                // No live submission: the timer is either orphaned (cleared run)
                // or already serving a newer run — tear down only the first case.
                this.StopOrphanedPollTimer();
                Interlocked.Exchange(ref this._batchPollRunning, 0);
                return;
            }

            // Finalization generation: a destructive event (cache clear, cancel,
            // component removal) may run while the provider call or the UI
            // marshal is in flight. Everything below that mutates outputs or
            // state must re-check ownership before acting.
            var finalizeEpoch = this._batchState.FinalizeEpoch;

            // Ownership probe: this callback may only mutate shared state while
            // the submission it captured is still the installed one and no
            // destructive event bumped the finalize epoch. Checking BEFORE
            // every mutation — not only before final publication — prevents a
            // late callback from tearing down or corrupting a newer run.
            bool StillOwnsRun() =>
                ReferenceEquals(this._batchState.Submission, submission)
                && this._batchState.FinalizeEpoch == finalizeEpoch;

            // Check for timeout - stop polling after 24 hours
            // Note: We don't cancel the remote batch here - APIs handle their own timeouts
            if (this._batchState.StartTime.HasValue &&
                (DateTime.UtcNow - this._batchState.StartTime.Value) > MaxBatchPollingDuration)
            {
                Logger.For<AIStatefulAsyncComponentBase>().Error($"[AIStatefulAsync] Batch polling timeout exceeded for {submission.BatchId} - stopping local polling");

                if (!StillOwnsRun())
                {
                    Logger.For<AIStatefulAsyncComponentBase>().Trace($"[AIStatefulAsync] Discarding stale batch timeout for {submission.BatchId} — run was invalidated mid-poll");
                    return;
                }

                this.StopBatchPollTimer(submission);
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
                    this.StopBatchPollTimer(submission);
                    return;
                }

                var status = await batchProvider.GetBatchStatusAsync(submission, cancellationToken).ConfigureAwait(false);
                Logger.For<AIStatefulAsyncComponentBase>().Trace($"[AIStatefulAsync] Batch poll result: state={status.State}, batchId={submission.BatchId}");

                switch (status.State)
                {
                    case AIBatchState.InProgress:
                        if (status.CompletedCount.HasValue && StillOwnsRun())
                        {
                            this._batchState.ProgressReported = true;
                            this._batchState.ProgressCompleted = status.CompletedCount.Value;
                            var total = submission.CustomIds?.Count ?? 0;
                            var completed = status.CompletedCount.Value;
                            Rhino.RhinoApp.InvokeOnUiThread(() =>
                            {
                                if (this._batchState.FinalizeEpoch != finalizeEpoch)
                                {
                                    return;
                                }

                                this.Message = $"Batch {completed}/{total}...";
                                this.OnDisplayExpired(false);
                            });
                        }

                        break;

                    case AIBatchState.Completed:
                        if (!StillOwnsRun())
                        {
                            Logger.For<AIStatefulAsyncComponentBase>().Trace($"[AIStatefulAsync] Discarding stale batch completion for {submission.BatchId} — run was invalidated or superseded mid-poll");
                            break;
                        }

                        this.StopBatchPollTimer(submission);
                        this._batchState.Submission = null;

                        // Preserve the provider that executed the batch for result
                        // decoding and metrics stamping — covers batches restored from
                        // file whose submission never passed through SubmitBatchQueueAsync.
                        this._batchState.SubmittedProviderName = submission.ProviderName;

                        // Calculate batch completion time and store for FinishResults to consume
                        if (this._batchState.StartTime.HasValue)
                        {
                            this._batchState.CompletionTime = (DateTime.UtcNow - this._batchState.StartTime.Value).TotalSeconds;
                            Logger.For<AIStatefulAsyncComponentBase>().Trace($"[AIStatefulAsync] Batch completed: batchId={submission.BatchId}, completionTime={this._batchState.CompletionTime:F2}s");
                            this._batchState.StartTime = null;
                        }

                        // Finalization writes component outputs — marshal to the UI thread,
                        // same as the collect-only path, instead of mutating from the poll timer.
                        // The epoch re-check inside the lambda discards the callback when a
                        // destructive event ran while it was queued on the dispatcher.
                        Rhino.RhinoApp.InvokeOnUiThread(() =>
                        {
                            if (this._batchState.FinalizeEpoch != finalizeEpoch)
                            {
                                Logger.For<AIStatefulAsyncComponentBase>().Trace($"[AIStatefulAsync] Discarding stale batch completion for {submission.BatchId} — state was invalidated while marshalling");
                                return;
                            }

                            this.CompleteBatchAndTransition(status.Results, status.Messages, submission.CustomIds);
                        });
                        break;

                    case AIBatchState.Failed:
                    case AIBatchState.Cancelled:
                    case AIBatchState.Expired:
                        if (!StillOwnsRun())
                        {
                            Logger.For<AIStatefulAsyncComponentBase>().Trace($"[AIStatefulAsync] Discarding stale batch terminal status ({status.State}) for {submission.BatchId} — state was invalidated mid-poll");
                            break;
                        }

                        this.StopBatchPollTimer(submission);
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
                Logger.For<AIStatefulAsyncComponentBase>().Warning("[AIStatefulAsync] Batch polling cancelled - preserving remote batch");

                // Always preserve the remote batch on poll cancellation — only
                // tear down the timer while it still serves this submission.
                this.StopBatchPollTimer(submission);
            }
            catch (Exception ex)
            {
                Logger.For<AIStatefulAsyncComponentBase>().Exception(ex, $"[AIStatefulAsync] PollBatchStatus error: {ex.Message}");
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
        protected virtual void OnBatchCompleted(IReadOnlyDictionary<string, JObject>? results, IReadOnlyList<SHRuntimeMessage>? messages = null)
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
        private void OnBatchFinalized(AIBatchState state, IReadOnlyDictionary<string, JObject>? results, IReadOnlyList<SHRuntimeMessage>? messages, string? errorMessage)
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

            // Add item-level messages only for failure states.
            // For Completed batches, item-level messages (finish_reason, cancelled, expired)
            // are surfaced with branch-path context inside OnBatchCompleted →
            // ProcessBatchResults<T> / ProcessMappingsBatchResults. Surfacing them here as
            // well would cause duplicates (once with raw custom_id, once with branch path).
            if (state != AIBatchState.Completed && messages != null && messages.Count > 0)
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
            if (state == AIBatchState.Completed && (results != null || messages?.Count > 0))
            {
                try
                {
                    this.OnBatchCompleted(results, messages);
                }
                catch (Exception ex)
                {
                    // A crashed decode/finalization pipeline must not be mistaken for
                    // a successful batch: flag it so CompleteBatchAndTransition forces
                    // the Error transition and the failure is surfaced, not swallowed.
                    Logger.For<AIStatefulAsyncComponentBase>().Exception(ex, $"[AIStatefulAsync] OnBatchCompleted error: {ex.Message}");
                    this._batchState.CompletionFailed = true;
                    this._batchState.DecodeErrors ??= new List<SHRuntimeMessage>();
                    this._batchState.DecodeErrors.Add(new SHRuntimeMessage(
                        SHRuntimeMessageSeverity.Error,
                        SHRuntimeMessageOrigin.Worker,
                        SHMessageCode.WorkerException,
                        $"Batch completion pipeline failed: {ex.Message}",
                        true));
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
        /// <param name="expectedSentinelIds">
        /// The sentinel customIds submitted upstream (typically <c>submission.CustomIds</c>).
        /// Completion is judged per expected id — delivered bodies are not enough:
        /// an id counts as done only when <c>ResolveSentinel</c> wrote its decoded
        /// output into the unit fragment. Pass <c>null</c> when the expectation is
        /// unknown (manual file load / collect-only) to keep the legacy
        /// delivered-body semantics for that path.
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
            IReadOnlyCollection<string> expectedSentinelIds,
            bool forceState = false)
        {
            var actualCount = results?.Count ?? 0;
            var hasResults = actualCount > 0;
            var providerErrors = messages?.Any(m => m.Severity == SHRuntimeMessageSeverity.Error) ?? false;

            // Item-level provider failures ("Batch item {id}: ..." errors) mark
            // expected work with a terminal remote outcome: the unit stays
            // unresolved but must NOT force re-execution — the provider already
            // answered definitively and re-submitting would only re-charge.
            var providerFailedIds = new HashSet<string>(StringComparer.Ordinal);
            if (messages != null)
            {
                foreach (var m in messages)
                {
                    if (AIBatchResultParser.TryGetFailedItemCustomId(m, out var failedId) && failedId != null)
                    {
                        providerFailedIds.Add(failedId);
                    }
                }
            }

            // Severity-downgrade rule: item-level errors are fatal to the COMPONENT only when
            // the entire batch failed. When at least one item succeeded, demote per-item Error
            // messages to Warning so the component reaches Completed state with visible warnings
            // (matches the requirement: "error only if all items fail; otherwise warnings").
            IReadOnlyList<SHRuntimeMessage> outgoingMessages = messages;
            if (hasResults && providerErrors && messages != null)
            {
                outgoingMessages = messages
                    .Select(m => m.Severity == SHRuntimeMessageSeverity.Error
                        ? new SHRuntimeMessage(SHRuntimeMessageSeverity.Warning, m.Origin, m.Code, m.Message, m.Surfaceable)
                        : m)
                    .ToList();
            }

            // Centralized batch finalization for success case. Runs the component's
            // decode pipeline; locally recorded decode/finalization failures are
            // evaluated AFTER it, since only then are they known.
            this.OnBatchFinalized(AIBatchState.Completed, results, outgoingMessages, null);

            var decodeErrors = this._batchState.DecodeErrors;
            var decodeErrorCount = decodeErrors?.Count ?? 0;
            var hasErrors = providerErrors || decodeErrorCount > 0;

            // Per-outcome accounting is delegated to the pure assessor: resolved
            // = expected ids whose sentinel was actually replaced inside its
            // unit fragment; unresolved-but-retryable = ids with no result or a
            // local decode failure (they re-execute). Provider-failed ids are
            // terminal — surfaced as warnings, never auto-resubmitted. Falls
            // back to delivered-body counting when no expectation exists.
            var assessment = Cores.BatchCompletionAssessor.Assess(
                expectedSentinelIds,
                id => this.ResultStore.IsSentinelResolved(id),
                providerFailedIds,
                actualCount,
                providerErrors,
                decodeErrorCount,
                this._batchState.CompletionFailed);

            var allFailed = assessment.AllFailed;
            var hasUnresolvedSentinels = assessment.HasUnresolvedSentinels;
            var unresolvedRetryableCount = assessment.UnresolvedRetryableCount;
            var resolvedCount = assessment.ResolvedCount;

            // Surface decode/finalization failures — loud on total failure,
            // warnings on recoverable partial failure (mirrors the provider-side rule).
            if (decodeErrors != null && decodeErrors.Count > 0)
            {
                var severity = allFailed ? GH_RuntimeMessageLevel.Error : GH_RuntimeMessageLevel.Warning;
                int decodeIdx = 0;
                foreach (var decodeError in decodeErrors)
                {
                    this.SetPersistentRuntimeMessage(
                        $"batch_decode_error_{decodeIdx++}",
                        severity,
                        decodeError.Message,
                        false);
                }
            }

            ComponentState targetState;
            TransitionReason targetReason;
            switch (assessment.Target)
            {
                case Cores.BatchCompletionTarget.Error:
                    Logger.For<AIStatefulAsyncComponentBase>().Warning("[AIStatefulAsync] Batch produced no successful results; transitioning to Error");
                    targetState = ComponentState.Error;
                    targetReason = TransitionReason.Error;
                    break;

                case Cores.BatchCompletionTarget.NeedsRun:
                    // Missing or locally undecodable results → leave room for
                    // re-execution. Provider-failed items were already excluded.
                    Logger.For<AIStatefulAsyncComponentBase>().Trace($"[AIStatefulAsync] Batch completed but {unresolvedRetryableCount}/{expectedSentinelIds?.Count ?? 0} sentinel(s) unresolved, transitioning to NeedsRun");
                    targetState = ComponentState.NeedsRun;
                    targetReason = TransitionReason.ResultsIncomplete;
                    break;

                default:
                    if (hasErrors)
                    {
                        Logger.For<AIStatefulAsyncComponentBase>().Trace($"[AIStatefulAsync] Batch completed with partial failures ({resolvedCount} ok), transitioning to Completed with warnings");
                    }

                    targetState = ComponentState.Completed;
                    targetReason = TransitionReason.ProcessingComplete;
                    break;
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

        /// <summary>
        /// Captures the pending input hashes, branch counts and provider selection as the
        /// immutable baseline for the run about to start. Called when entering Processing
        /// so batch submission acknowledges exactly the inputs it was built with — edits
        /// made while the batch is collecting or submitting stay pending instead of being
        /// silently committed.
        /// </summary>
        private void CaptureRunInputSnapshot()
        {
            this._batchState.RunInputHashes = this.StateManager.GetPendingHashes();
            this._batchState.RunBranchCounts = this.StateManager.GetPendingBranchCounts();
            this._batchState.RunProviderSelection = this.SelectedProviderName;
        }

        /// <summary>
        /// Commits the run-input snapshot as the acknowledged baseline (hashes, branch
        /// counts and provider selection). Falls back to committing the current pending
        /// values when no snapshot exists (e.g. batch state restored from file).
        /// </summary>
        private void CommitRunInputSnapshot()
        {
            if (this._batchState.RunInputHashes != null)
            {
                this.StateManager.CommitHashes(this._batchState.RunInputHashes, this._batchState.RunBranchCounts);
            }
            else
            {
                this.StateManager.CommitHashes();
            }

            if (this._batchState.RunProviderSelection != null)
            {
                this.CommitProviderChange(this._batchState.RunProviderSelection);
            }
            else
            {
                this.CommitProviderChange();
            }
        }

        /// <summary>
        /// True when the pending inputs differ from the snapshot captured at gather
        /// time — i.e. the user edited an input (or switched provider) after the
        /// current run collected its data. Comparing against the snapshot instead of
        /// the committed baseline avoids treating the run's own gathered inputs as a
        /// "change" in the window between Processing entry and submission.
        /// </summary>
        private bool PendingInputsDifferFromRunSnapshot()
        {
            var snapshotHashes = this._batchState.RunInputHashes;
            if (snapshotHashes == null)
            {
                // No snapshot (e.g. batch restored from file): cannot tell whether the
                // pending values were gathered for this run, so do not flag them as edits.
                return false;
            }

            if (this.SelectedProviderName != this._batchState.RunProviderSelection)
            {
                return true;
            }

            var pendingHashes = this.StateManager.GetPendingHashes();
            var pendingBranchCounts = this.StateManager.GetPendingBranchCounts();

            return !DictionariesEqual(pendingHashes, snapshotHashes, WellKnownInputs.Run)
                || !DictionariesEqual(pendingBranchCounts, this._batchState.RunBranchCounts, WellKnownInputs.Run);
        }

        private static bool DictionariesEqual(Dictionary<string, int> a, Dictionary<string, int> b, string ignoredKey = null)
        {
            var left = a ?? new Dictionary<string, int>();
            var right = b ?? new Dictionary<string, int>();

            // The Run? input is control data, not work data: a button released
            // mid-batch (or a toggle flipped to stop future runs) must never be
            // treated as an edit to the submitted work. Explicit cancel owns abort.
            bool ignored(string key) => ignoredKey != null && string.Equals(key, ignoredKey, StringComparison.Ordinal);

            if (left.Keys.Count(k => !ignored(k)) != right.Keys.Count(k => !ignored(k)))
            {
                return false;
            }

            foreach (var pair in left)
            {
                if (ignored(pair.Key))
                {
                    continue;
                }

                if (!right.TryGetValue(pair.Key, out var value) || value != pair.Value)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Handles input changes made while a remote batch is in flight. Prompts the
        /// user: "Yes" cancels the in-flight batch and re-requests it with the new
        /// inputs; "No" keeps the current batch and acknowledges (omits) the edits so
        /// they neither re-prompt nor retrigger a run.
        /// Called from <c>SolveInstance</c> on the UI thread.
        /// </summary>
        private void PromptForMidBatchInputChange()
        {
            if (!this._batchState.BatchInFlight)
            {
                return;
            }

            if (this.StateManager.IsRestoringFromFile || this.StateManager.IsSuppressingInputChanges)
            {
                return;
            }

            if (!this.PendingInputsDifferFromRunSnapshot())
            {
                return;
            }

            var reRequest = StyledMessageDialog.ShowConfirmation(
                "Changing inputs while a batch is processing will cancel the current batch and restart it from the beginning. Every item already processed will be processed again.\n\n" +
                "Do you want to cancel the in-flight batch and re-request it with the new inputs?\n" +
                "(If you choose NO, the current batch will keep running and the new changes will be omitted.)",
                "Batch in progress",
                this.InstanceGuid);

            if (reRequest)
            {
                this.CancelActiveBatchForReRequest();
                return;
            }

            // "No": keep the current batch and omit the edits — acknowledge the current
            // pending values as both the committed baseline and the run snapshot so the
            // same edits neither resurface this prompt nor retrigger processing once the
            // batch completes.
            this.StateManager.CommitHashes();
            this.CommitProviderChange();
            this._batchState.RunInputHashes = this.StateManager.GetPendingHashes();
            this._batchState.RunBranchCounts = this.StateManager.GetPendingBranchCounts();
            this._batchState.RunProviderSelection = this.SelectedProviderName;
            this.SetPersistentRuntimeMessage(
                "batch_inputs_omitted",
                GH_RuntimeMessageLevel.Remark,
                "Input changes made while the batch was processing were omitted.",
                false);
        }

        /// <summary>
        /// Cancels the in-flight batch so the changed inputs can be re-requested. The
        /// pending hashes and provider selection are intentionally left uncommitted:
        /// their diff against the committed baseline drives the normal input-change
        /// path, which debounces into a fresh run that gathers and submits the new
        /// inputs.
        /// </summary>
        private void CancelActiveBatchForReRequest()
        {
            if (!this._batchState.BatchInFlight)
            {
                return;
            }

            // Invalidate before cancelling: a provider submit still in flight
            // must discard its response instead of installing a new Submission
            // for the cancelled run.
            this._batchState.InvalidateAsyncWork();

            var submission = this._batchState.Submission;
            var batchId = submission?.BatchId ?? "pending submission";
            Logger.For<AIStatefulAsyncComponentBase>().Trace($"[AIStatefulAsync] Cancelling batch {batchId} to re-request with updated inputs");

            this.SetPersistentRuntimeMessage(
                "batch_cancelling",
                GH_RuntimeMessageLevel.Warning,
                $"Cancelling batch {batchId} to re-request with the updated inputs...",
                false);

            if (submission != null)
            {
                _ = this.CancelRemoteBatchAsync(submission.BatchId);
            }

            this.StopBatchPollTimer();
            this._batchState.Submission = null;
            this._batchState.SubmittedProviderName = null;
            this._batchState.SubmittedModels = null;
            this._batchState.SubmittedRoles = null;

            this.StateManager.RequestTransition(ComponentState.Cancelled, TransitionReason.Cancelled);
            this.ExpireSolution(true);
        }

        /// <summary>
        /// Captures the provider and per-item model identity of a submitted batch so
        /// result decoding and metrics stamping reflect what actually ran — the
        /// submitted queue is discarded after <c>SubmitBatchAsync</c> returns.
        /// </summary>
        /// <param name="queue">The (CustomId, Request, MetricsRole) triples just submitted.</param>
        /// <param name="providerName">The provider that accepted the batch.</param>
        private void CaptureSubmittedRequestInfo(List<(string CustomId, AIRequestCall Request, string MetricsRole)> queue, string providerName)
        {
            this._batchState.SubmittedProviderName = providerName;
            this._batchState.SubmittedModels = queue?
                .Where(q => !string.IsNullOrWhiteSpace(q.Request?.Model))
                .GroupBy(q => q.CustomId)
                .ToDictionary(g => g.Key, g => g.First().Request.Model);
            this._batchState.SubmittedRoles = queue?
                .Where(q => !string.IsNullOrWhiteSpace(q.MetricsRole))
                .GroupBy(q => q.CustomId)
                .ToDictionary(g => g.Key, g => g.First().MetricsRole);
        }

        /// <summary>
        /// Resolves the provider that executed the in-flight or last submitted batch.
        /// Falls back to the live selection for paths that never submitted (e.g.
        /// results loaded from file without a prior submission in this session).
        /// </summary>
        protected string GetSubmittedProviderName()
            => this._batchState.SubmittedProviderName ?? this.GetActualAIProviderName();

        /// <summary>
        /// Stamps a batch-decoded metric with the submitted run's provider and model,
        /// mirroring the provider/model stamping <c>AIReturn.CreateSuccess</c> applies
        /// on the synchronous path, and defaults <see cref="AIMetrics.IterationsCount"/>
        /// to one — matching <c>AIBody.Metrics</c>' convention of one iteration per
        /// provider call. Raw provider <c>Decode</c> leaves these unset, so batch
        /// metrics would otherwise report null provider/model and zero iterations.
        /// The metrics <see cref="AIMetrics.Role"/> is resolved from the attribution
        /// recorded when the request was queued ("main" or "tool:{name}").
        /// </summary>
        /// <param name="metrics">The metric decoded from a batch result body.</param>
        /// <param name="customId">The sentinel custom ID the metric belongs to.</param>
        /// <param name="providerName">The provider that executed the batch.</param>
        protected AIMetrics StampBatchMetrics(AIMetrics metrics, string customId, string providerName)
        {
            var model = metrics.Model
                ?? (this._batchState.SubmittedModels != null
                    && this._batchState.SubmittedModels.TryGetValue(customId, out var submittedModel)
                    ? submittedModel
                    : null)
                ?? this.GetModel();

            return metrics with
            {
                Provider = providerName,
                Model = model,

                // Batch-decoded usage is always priced off batch_pricing —
                // never the regular tier — by AICostCalculator.
                IsBatch = true,
                IterationsCount = metrics.IterationsCount ?? 1,

                // One AI call = one processing unit — mirrors the sync path
                // (ProcessAIResult stamps DataCount = 1). Values >1 belong to
                // aggregated metrics only.
                DataCount = metrics.DataCount ?? 1,

                // Providers reporting neither choices[0].finish_reason nor a
                // Responses-API status leave the reason unset; mirror the sync
                // convention of normal-termination = "stop".
                FinishReason = string.IsNullOrEmpty(metrics.FinishReason) ? AIFinishReason.Stop : metrics.FinishReason,
                Role = metrics.Role ?? this.ResolveSubmittedRole(customId),

                // The customId is the stable execution identity for batch calls —
                // it survives save/reopen and deduplicates shared placements.
                ExecutionId = metrics.ExecutionId ?? customId,

                // Providers do not report per-item completion timestamps for
                // batches; stamp the locally observed completion time instead.
                CompletedAt = metrics.CompletedAt ?? DateTimeOffset.UtcNow,
            };
        }

        /// <summary>
        /// Records a per-item local decode failure so <see cref="CompleteBatchAndTransition"/>
        /// can count it toward the unresolved/all-failed decision instead of
        /// silently completing. Shared by the generic batch decode pipeline
        /// (<c>ProcessBatchResultsCore</c>) and the output-adapter variant.
        /// </summary>
        /// <param name="customId">The sentinel customId whose result could not be decoded.</param>
        /// <param name="ex">The decode exception.</param>
        protected void RecordBatchItemDecodeError(string customId, Exception ex)
        {
            this._batchState.DecodeErrors ??= new List<SHRuntimeMessage>();
            this._batchState.DecodeErrors.Add(new SHRuntimeMessage(
                SHRuntimeMessageSeverity.Error,
                SHRuntimeMessageOrigin.Worker,
                SHMessageCode.BatchItemError,
                $"Failed to decode batch result '{customId}': {ex.Message}",
                true));
        }

        /// <summary>
        /// Resolves the metrics role recorded for a submitted batch item.
        /// Falls back to "main" when no attribution was captured (e.g. batches
        /// submitted by a build that predates role persistence).
        /// </summary>
        private string ResolveSubmittedRole(string customId)
            => this._batchState.SubmittedRoles != null
                && this._batchState.SubmittedRoles.TryGetValue(customId, out var role)
                && !string.IsNullOrEmpty(role)
                ? role
                : "main";

        #endregion
    }
}
