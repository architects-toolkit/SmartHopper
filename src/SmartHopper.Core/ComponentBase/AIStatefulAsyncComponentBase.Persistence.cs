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
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Newtonsoft.Json.Linq;
using SmartHopper.Core.ComponentBase.Batch;
using SmartHopper.Core.ComponentBase.Contracts;
using SmartHopper.Core.ComponentBase.State;
using SmartHopper.Infrastructure.AIProviders;
using SmartHopper.ProviderSdk.AICall.Batch;
using SmartHopper.ProviderSdk.AICall.Core;
using SmartHopper.ProviderSdk.AICall.Core.Base;
using SmartHopper.ProviderSdk.AICall.Core.Requests;
using SmartHopper.ProviderSdk.AICall.Core.Returns;
using SmartHopper.ProviderSdk.AICall.Validation;
using SmartHopper.ProviderSdk.AIProviders;
using SmartHopper.ProviderSdk.Diagnostics;

using SmartHopper.ProviderSdk.Logging;
namespace SmartHopper.Core.ComponentBase
{
    /// <summary>
    /// Persistence and serialization logic for AIStatefulAsyncComponentBase.
    /// Handles file I/O, batch state restoration, and menu items.
    /// </summary>
    public abstract partial class AIStatefulAsyncComponentBase
    {
        /// <summary>
        /// Sets the AI request parameters from the Settings input.
        /// </summary>
        /// <param name="parameters">The AI request parameters to use.</param>
        protected void SetParameters(AIRequestParameters parameters)
        {
            this._requestParameters = parameters ?? AIRequestParameters.Empty;
        }

        /// <summary>
        /// Gets the AI request parameters effective for the current context: the
        /// run-frozen snapshot while processing (or while a batch is in flight),
        /// otherwise the most recently parsed Settings input.
        /// </summary>
        /// <returns>The effective <see cref="AIRequestParameters"/>, never null.</returns>
        protected AIRequestParameters GetParameters()
        {
            if (this._runParameters != null &&
                (this._batchState.BatchInFlight || this.StateManager.CurrentState == ComponentState.Processing))
            {
                return this._runParameters;
            }

            return this._requestParameters ?? AIRequestParameters.Empty;
        }

        /// <summary>
        /// Gets the resolved model name for AI processing.
        /// If the parameters specify a model, it is returned as-is.
        /// Otherwise the provider's capability-aware default is used.
        /// </summary>
        /// <returns>The model name to use, or empty string for provider default.</returns>
        protected string GetModel()
        {
            var modelFromParams = this._requestParameters?.Model;
            var provider = this.GetActualAIProvider();
            if (provider == null)
            {
                return modelFromParams ?? string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(modelFromParams))
            {
                return modelFromParams;
            }

            var selected = provider.GetDefaultModel(this.RequiredCapability, useSettings: true);
            return selected ?? string.Empty;
        }

        /// <summary>
        /// Surfaces structured runtime messages contained in an <see cref="IAIReturn"/> as persistent
        /// Grasshopper runtime messages. Maps <see cref="SHRuntimeMessageSeverity"/> to
        /// <see cref="GH_RuntimeMessageLevel"/> and prefixes the text with the message <see cref="SHRuntimeMessage.Origin"/>.
        /// </summary>
        /// <param name="aiReturn">The AI return object containing messages.</param>
        /// <param name="keyPrefix">A key prefix to namespace the persistent message keys.</param>
        protected void SurfaceMessagesFromReturn(IAIReturn aiReturn, string keyPrefix)
        {
            if (aiReturn?.Messages == null || aiReturn.Messages.Count == 0)
            {
                return;
            }

            int idx = 0;
            foreach (var item in aiReturn.Messages)
            {
                idx++;

                // Only surface messages intended for end users
                if (item == null || !item.Surfaceable)
                {
                    continue;
                }

                // Provider-integrity warnings are session-scoped: surface at most once
                // per provider across components (ProviderSdk-layer deduplication).
                if (!ProviderTrustWarningDeduplicator.ShouldSurface(item))
                {
                    continue;
                }

                // Map structured severity to GH level
                GH_RuntimeMessageLevel level = item.Severity switch
                {
                    SHRuntimeMessageSeverity.Warning => GH_RuntimeMessageLevel.Warning,
                    SHRuntimeMessageSeverity.Error => GH_RuntimeMessageLevel.Error,
                    _ => GH_RuntimeMessageLevel.Remark,
                };

                // Include origin for context, then the message text
                var originTag = $"[{item.Origin}] ";
                var msg = item.Message ?? string.Empty;

                this.SetPersistentRuntimeMessage($"{keyPrefix}_msg_{idx}", level, originTag + msg, false);
            }
        }

        /// <inheritdoc/>
        public override bool Write(GH_IO.Serialization.GH_IWriter writer)
        {
            if (!base.Write(writer)) return false;

            try
            {
                // Persist batch state so polling can resume after file close/reopen
                if (this._batchState.Submission != null)
                {
                    writer.SetString(PersistenceKeys.BatchId, this._batchState.Submission.BatchId);
                    writer.SetString(PersistenceKeys.BatchProvider, this._batchState.Submission.ProviderName);
                    writer.SetString(PersistenceKeys.BatchRequest, this._batchState.Submission.SerializedRequest ?? string.Empty);

                    // Persist the authoritative submission clock (StartTime when
                    // installed/restored), never the reconstructed submission's
                    // construction timestamp — writing SubmittedAt directly made
                    // every successive save/reopen lose the real elapsed time and
                    // re-extend the polling deadline from the previous reopen.
                    writer.SetString(PersistenceKeys.BatchSubmittedAt, this._batchState.SubmissionTimestamp?.ToString("O") ?? this._batchState.Submission.SubmittedAt.ToString("O"));

                    if (this._batchState.Submission.CustomIds != null && this._batchState.Submission.CustomIds.Count > 0)
                    {
                        writer.SetString(PersistenceKeys.BatchCustomIds, new JArray(this._batchState.Submission.CustomIds.ToArray()).ToString(Newtonsoft.Json.Formatting.None));
                    }

                    // Persist per-item metrics attribution so a saved-and-reopened
                    // in-flight batch stamps decoded metrics with the role recorded
                    // at queue time ("main" or "tool:{name}").
                    if (this._batchState.SubmittedRoles != null && this._batchState.SubmittedRoles.Count > 0)
                    {
                        writer.SetString(PersistenceKeys.BatchSubmittedRoles, JObject.FromObject(this._batchState.SubmittedRoles).ToString(Newtonsoft.Json.Formatting.None));
                    }

                    // Persist per-item model identity so a saved-and-reopened batch
                    // stamps decoded metrics with the model that actually ran, even
                    // when the live selection changed mid-flight.
                    if (this._batchState.SubmittedModels != null && this._batchState.SubmittedModels.Count > 0)
                    {
                        writer.SetString(PersistenceKeys.BatchSubmittedModels, JObject.FromObject(this._batchState.SubmittedModels).ToString(Newtonsoft.Json.Formatting.None));
                    }

                    // Persist component-specific decode context (e.g. the source
                    // items behind a list_filter index response, or the submitted
                    // Fallback value) so a saved-and-reopened in-flight batch
                    // decodes against the same data it was submitted with.
                    var decodeContext = this.CaptureBatchDecodeContext();
                    if (decodeContext != null)
                    {
                        writer.SetString(PersistenceKeys.BatchDecodeContext, decodeContext.ToString(Newtonsoft.Json.Formatting.None));
                    }

                    Logger.For<AIStatefulAsyncComponentBase>().Trace($"[AIStatefulAsync] Write: persisted batch state, batchId={this._batchState.Submission.BatchId}, items={this._batchState.Submission.CustomIds?.Count ?? 0}");
                }

                // Persist sentinel IDs so OnBatchCompleted can reconstruct trees after reload
                if (this._batchState.SentinelIds != null && this._batchState.SentinelIds.Count > 0)
                {
                    writer.SetString(PersistenceKeys.BatchSentinelIds, new JArray(this._batchState.SentinelIds.ToArray()).ToString(Newtonsoft.Json.Formatting.None));
                }

                // Persist sentinel trees (path layout + sentinel strings) so OnBatchCompleted
                // can reconstruct output trees correctly after file close/reopen.
                // Without this, GetSentinelTree() returns null on reload and no output is produced.
                if (this._batchState.SentinelTrees != null && this._batchState.SentinelTrees.Count > 0)
                {
                    var sentinelTreesJson = new JObject();
                    foreach (var kvp in this._batchState.SentinelTrees)
                    {
                        if (kvp.Value is GH_Structure<GH_String> tree)
                        {
                            var treeJson = new JArray();
                            foreach (var path in tree.Paths)
                            {
                                var pathIndices = new JArray(path.Indices.Cast<object>().ToArray());
                                var items = tree.get_Branch(path);
                                var itemsJson = new JArray();
                                foreach (GH_String item in items)
                                {
                                    itemsJson.Add(item?.Value ?? string.Empty);
                                }

                                treeJson.Add(new JObject
                                {
                                    ["path"] = pathIndices,
                                    ["items"] = itemsJson,
                                });
                            }

                            sentinelTreesJson[kvp.Key] = treeJson;
                        }
                    }

                    writer.SetString(PersistenceKeys.BatchSentinelTrees, sentinelTreesJson.ToString(Newtonsoft.Json.Formatting.None));
                    Logger.For<AIStatefulAsyncComponentBase>().Trace($"[AIStatefulAsync] Write: persisted {this._batchState.SentinelTrees.Count} sentinel tree(s)");
                }

                // Persist pending unit fragments (which still contain sentinels) and the
                // customId → unit-key map so decoded results can resolve cached fragments
                // after a save/reopen while the batch is in flight.
                if (this._batchState.Submission != null)
                {
                    var pendingJson = this.ResultStore.SerializePending();
                    if (pendingJson != null)
                    {
                        writer.SetString(PersistenceKeys.PendingResultCache, pendingJson.ToString(Newtonsoft.Json.Formatting.None));
                    }

                    var mapJson = this.ResultStore.SerializeCustomIdMap();
                    if (mapJson != null)
                    {
                        writer.SetString(PersistenceKeys.UnitCustomIdMap, mapJson.ToString(Newtonsoft.Json.Formatting.None));
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.For<AIStatefulAsyncComponentBase>().Exception(ex, $"[AIStatefulAsync] Write batch state error: {ex.Message}");
            }

            return true;
        }

        /// <inheritdoc/>
        public override bool Read(GH_IO.Serialization.GH_IReader reader)
        {
            if (!base.Read(reader)) return false;

            var restoredActiveSubmission = false;
            try
            {
                if (reader.ItemExists(PersistenceKeys.BatchId))
                {
                    var batchId = reader.GetString(PersistenceKeys.BatchId);
                    var providerName = reader.ItemExists(PersistenceKeys.BatchProvider) ? reader.GetString(PersistenceKeys.BatchProvider) : string.Empty;
                    var serializedReq = reader.ItemExists(PersistenceKeys.BatchRequest) ? reader.GetString(PersistenceKeys.BatchRequest) : string.Empty;

                    // Restore the original submission clock so a resumed poll keeps the
                    // real elapsed-time deadline and completion-time metric instead of
                    // restarting from the reopen moment.
                    DateTimeOffset? submittedAt = null;
                    if (reader.ItemExists(PersistenceKeys.BatchSubmittedAt))
                    {
                        var submittedAtJson = reader.GetString(PersistenceKeys.BatchSubmittedAt);
                        if (!string.IsNullOrEmpty(submittedAtJson)
                            && DateTimeOffset.TryParse(submittedAtJson, null, System.Globalization.DateTimeStyles.RoundtripKind, out var parsedSubmittedAt))
                        {
                            submittedAt = parsedSubmittedAt;
                            this._batchState.StartTime = parsedSubmittedAt.UtcDateTime;
                            Logger.For<AIStatefulAsyncComponentBase>().Trace($"[AIStatefulAsync] Read: restored batch submission clock, submittedAt={parsedSubmittedAt:O}");
                        }
                    }

                    IReadOnlyList<string> customIds = null;
                    if (reader.ItemExists(PersistenceKeys.BatchCustomIds))
                    {
                        var idsJson = reader.GetString(PersistenceKeys.BatchCustomIds);
                        if (!string.IsNullOrEmpty(idsJson))
                        {
                            try
                            {
                                customIds = JArray.Parse(idsJson).Values<string>().ToList().AsReadOnly();
                            }
                            catch (Exception parseEx)
                            {
                                Logger.For<AIStatefulAsyncComponentBase>().Error($"[AIStatefulAsync] Failed to parse batch custom IDs: {parseEx.Message}");
                            }
                        }
                    }
                    else if (reader.ItemExists(PersistenceKeys.LegacyBatchCustomId))
                    {
                        // Legacy single-ID format
                        var singleId = reader.GetString(PersistenceKeys.LegacyBatchCustomId);
                        if (!string.IsNullOrEmpty(singleId))
                            customIds = new List<string> { singleId }.AsReadOnly();
                    }

                    if (!string.IsNullOrEmpty(batchId) && !string.IsNullOrEmpty(providerName))
                    {
                        this._batchState.Submission = new SmartHopper.ProviderSdk.AICall.Batch.AIBatchSubmission(batchId, providerName, serializedReq, customIds ?? new List<string>().AsReadOnly(), submittedAt);
                        restoredActiveSubmission = true;
                        Logger.For<AIStatefulAsyncComponentBase>().Trace($"[AIStatefulAsync] Read: restored batch state, batchId={batchId}, items={customIds?.Count ?? 0}");

                        // Restore component to Processing state so sentinel values aren't output
                        this.StateManager.ForceState(ComponentState.Processing);
                        Logger.For<AIStatefulAsyncComponentBase>().Trace($"[AIStatefulAsync] Read: restored state to Processing for active batch");
                    }
                }

                if (reader.ItemExists(PersistenceKeys.BatchSentinelIds))
                {
                    var sentinelJson = reader.GetString(PersistenceKeys.BatchSentinelIds);
                    if (!string.IsNullOrEmpty(sentinelJson))
                    {
                        try
                        {
                            this._batchState.SentinelIds = new HashSet<string>(JArray.Parse(sentinelJson).Values<string>());
                            Logger.For<AIStatefulAsyncComponentBase>().Trace($"[AIStatefulAsync] Read: restored {this._batchState.SentinelIds.Count} sentinel IDs");
                        }
                        catch (Exception parseEx)
                        {
                            Logger.For<AIStatefulAsyncComponentBase>().Error($"[AIStatefulAsync] Failed to parse batch sentinel IDs: {parseEx.Message}");
                        }
                    }
                }

                if (reader.ItemExists(PersistenceKeys.BatchSubmittedRoles))
                {
                    var rolesJson = reader.GetString(PersistenceKeys.BatchSubmittedRoles);
                    if (!string.IsNullOrEmpty(rolesJson))
                    {
                        try
                        {
                            this._batchState.SubmittedRoles = JObject.Parse(rolesJson).ToObject<Dictionary<string, string>>();
                            Logger.For<AIStatefulAsyncComponentBase>().Trace($"[AIStatefulAsync] Read: restored {this._batchState.SubmittedRoles?.Count ?? 0} submitted metrics role(s)");
                        }
                        catch (Exception parseEx)
                        {
                            Logger.For<AIStatefulAsyncComponentBase>().Error($"[AIStatefulAsync] Failed to parse batch submitted roles: {parseEx.Message}");
                        }
                    }
                }

                if (reader.ItemExists(PersistenceKeys.BatchSubmittedModels))
                {
                    var modelsJson = reader.GetString(PersistenceKeys.BatchSubmittedModels);
                    if (!string.IsNullOrEmpty(modelsJson))
                    {
                        try
                        {
                            this._batchState.SubmittedModels = JObject.Parse(modelsJson).ToObject<Dictionary<string, string>>();
                            Logger.For<AIStatefulAsyncComponentBase>().Trace($"[AIStatefulAsync] Read: restored {this._batchState.SubmittedModels?.Count ?? 0} submitted model name(s)");
                        }
                        catch (Exception parseEx)
                        {
                            Logger.For<AIStatefulAsyncComponentBase>().Error($"[AIStatefulAsync] Failed to parse batch submitted models: {parseEx.Message}");
                        }
                    }
                }

                // Restore component-specific decode context (source items, fallback
                // values) before resumed polling can deliver results for decode.
                if (reader.ItemExists(PersistenceKeys.BatchDecodeContext))
                {
                    try
                    {
                        this.RestoreBatchDecodeContext(JObject.Parse(reader.GetString(PersistenceKeys.BatchDecodeContext)));
                    }
                    catch (Exception parseEx)
                    {
                        Logger.For<AIStatefulAsyncComponentBase>().Error($"[AIStatefulAsync] Failed to restore batch decode context: {parseEx.Message}");
                    }
                }

                // Restore pending unit fragments and the customId → unit-key map so a
                // resumed batch can resolve decoded results inside cached fragments.
                if (reader.ItemExists(PersistenceKeys.PendingResultCache))
                {
                    try
                    {
                        this.ResultStore.RestorePending(JObject.Parse(reader.GetString(PersistenceKeys.PendingResultCache)));
                    }
                    catch (Exception parseEx)
                    {
                        Logger.For<AIStatefulAsyncComponentBase>().Error($"[AIStatefulAsync] Failed to parse pending result cache: {parseEx.Message}");
                    }
                }

                if (reader.ItemExists(PersistenceKeys.UnitCustomIdMap))
                {
                    try
                    {
                        this.ResultStore.RestoreCustomIdMap(JObject.Parse(reader.GetString(PersistenceKeys.UnitCustomIdMap)));
                    }
                    catch (Exception parseEx)
                    {
                        Logger.For<AIStatefulAsyncComponentBase>().Error($"[AIStatefulAsync] Failed to parse unit customId map: {parseEx.Message}");
                    }
                }

                if (reader.ItemExists(PersistenceKeys.BatchSentinelTrees))
                {
                    var treesJson = reader.GetString(PersistenceKeys.BatchSentinelTrees);
                    if (!string.IsNullOrEmpty(treesJson))
                    {
                        try
                        {
                            var treesObj = JObject.Parse(treesJson);
                            this._batchState.SentinelTrees = new Dictionary<string, object>();
                            foreach (var prop in treesObj.Properties())
                            {
                                var tree = new GH_Structure<GH_String>();
                                foreach (var branchToken in prop.Value as JArray ?? new JArray())
                                {
                                    var pathIndices = (branchToken["path"] as JArray)?.Values<int>().ToArray() ?? Array.Empty<int>();
                                    var ghPath = new Grasshopper.Kernel.Data.GH_Path(pathIndices);
                                    var items = (branchToken["items"] as JArray) ?? new JArray();
                                    foreach (var itemToken in items)
                                    {
                                        tree.Append(new GH_String(itemToken.ToString()), ghPath);
                                    }
                                }

                                this._batchState.SentinelTrees[prop.Name] = tree;
                            }

                            Logger.For<AIStatefulAsyncComponentBase>().Trace($"[AIStatefulAsync] Read: restored {this._batchState.SentinelTrees.Count} sentinel tree(s)");
                        }
                        catch (Exception parseEx)
                        {
                            Logger.For<AIStatefulAsyncComponentBase>().Error($"[AIStatefulAsync] Failed to parse batch sentinel trees: {parseEx.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.For<AIStatefulAsyncComponentBase>().Exception(ex, $"[AIStatefulAsync] Read batch state error: {ex.Message}");
                this.SetPersistentRuntimeMessage("batch_read_error", GH_RuntimeMessageLevel.Error, $"Error restoring batch state: {ex.Message}", false);
            }

            this.MarkNeedsRunForOrphanedSentinels();

            // Restore-before-resume invariant: polling resumes only after ALL
            // reconstruction state above was installed (submission clock, decode
            // context, pending fragments, customId map, sentinel trees). A poll
            // completing early can now only see a fully restored run.
            if (restoredActiveSubmission)
            {
                Rhino.RhinoApp.InvokeOnUiThread(() =>
                {
                    if (this._batchState.Submission != null)
                    {
                        // Start timer with immediate first poll to check if batch already complete
                        this.StartBatchPollTimer(immediateFirstPoll: true);

                        // Expire solution to trigger recompute with Processing state
                        this.ExpireSolution(true);
                    }
                });
            }

            return true;
        }

        /// <summary>
        /// Forces <see cref="ComponentState.NeedsRun"/> when restored persistent
        /// outputs still carry <c>##SH_BATCH:</c> sentinels that can no longer be
        /// resolved: no batch submission survived to resume, and the committed
        /// snapshot has no fragment for the sentinel's unit — meaning the run
        /// that queued them never finished. The next solve re-executes only
        /// those units while committed-hit units still reuse their results.
        /// Sentinels whose units DO have committed fragments are republished by
        /// <c>SetPersistentOutput</c>'s resolve-or-strip pass instead.
        /// </summary>
        private void MarkNeedsRunForOrphanedSentinels()
        {
            if (this._batchState.Submission != null)
            {
                return;
            }

            try
            {
                foreach (var output in this.PersistentOutputs)
                {
                    if (output.Value is not IGH_Structure tree || !BatchSentinel.TreeContainsSentinel(tree))
                    {
                        continue;
                    }

                    foreach (GH_Path path in tree.Paths)
                    {
                        if (tree.get_Branch(path) is not System.Collections.IList branch)
                        {
                            continue;
                        }

                        foreach (var item in branch)
                        {
                            if (item is GH_String sentinel
                                && BatchSentinel.TryExtract(sentinel?.Value, out var customId)
                                && !this.ResultStore.TryGetCommittedForSentinel(customId, output.Key, out _))
                            {
                                Logger.For<AIStatefulAsyncComponentBase>().Trace($"[AIStatefulAsync] Read: output '{output.Key}' carries orphaned sentinel '{customId}' with no resumable batch or committed result — forcing NeedsRun");
                                this.StateManager.ForceState(ComponentState.NeedsRun);
                                return;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.For<AIStatefulAsyncComponentBase>().Warning($"[AIStatefulAsync] Orphaned-sentinel scan failed, leaving restored state unchanged: {ex.Message}");
            }
        }

        /// <inheritdoc/>
        public override void AppendAdditionalMenuItems(ToolStripDropDown menu)
        {
            base.AppendAdditionalMenuItems(menu);

            // Add batch-related menu items
            Menu_AppendSeparator(menu);

            // Always available: Load results from file
            Menu_AppendItem(menu, "Load results from file", (s, e) =>
            {
                this.LoadResultsFromFile();
            });

            // Only available when in processing batch state
            var checkBatchItem = Menu_AppendItem(menu, "Check batch status", (s, e) =>
            {
                this.CheckBatchStatus();
            });
            checkBatchItem.Enabled = this._batchState.Submission != null;
        }

        /// <summary>
        /// Loads batch results from one or more files and processes them through the
        /// same pipeline as API-completed batches. Supports multi-file selection; the
        /// selected provider parses each file and merges them internally with a
        /// first-wins policy on <c>custom_id</c>. This is the dialog/file-IO shell
        /// around <see cref="ImportBatchResultContents"/>, which owns the actual
        /// import so the <c>gh_batch_import_results</c> AI tool shares the exact
        /// same pipeline (no diverging logic paths).
        /// </summary>
        private void LoadResultsFromFile()
        {
            try
            {
                string[] fileNames;
                using (var dialog = new OpenFileDialog
                {
                    Filter = "Batch result files (*.json;*.jsonl)|*.json;*.jsonl|All files (*.*)|*.*",
                    Title = "Load Batch Results",
                    Multiselect = true,
                    CheckFileExists = true,
                })
                {
                    if (dialog.ShowDialog() != DialogResult.OK)
                    {
                        return;
                    }

                    fileNames = dialog.FileNames;
                }

                if (fileNames == null || fileNames.Length == 0)
                {
                    return;
                }

                // Read raw file contents in selection order. The provider handles
                // parsing and first-wins merging internally.
                var contents = new List<string>(fileNames.Length);
                var readErrors = new List<string>();
                foreach (var path in fileNames)
                {
                    try
                    {
                        contents.Add(File.ReadAllText(path));
                    }
                    catch (Exception ex)
                    {
                        readErrors.Add($"{Path.GetFileName(path)}: {ex.Message}");
                    }
                }

                this.ImportBatchResultContents(contents, readErrors);
            }
            catch (Exception ex)
            {
                this.SetPersistentRuntimeMessage(
                    "load_results_error",
                    GH_RuntimeMessageLevel.Error,
                    $"Error loading results files: {ex.Message}",
                    false);
            }
        }

        /// <summary>
        /// Shared batch-results import pipeline: parses raw provider result
        /// payloads, takes over completion ownership from any in-flight local
        /// poll, and either finalizes directly (expected sentinel ids matched)
        /// or triggers a collect-only re-collection run that rebuilds sentinel
        /// trees before the staged results finalize.
        /// </summary>
        /// <remarks>
        /// <b>Threading:</b> must be invoked on the Grasshopper UI thread —
        /// it performs state transitions and expiration directly. Callers off
        /// the UI thread must marshal via <c>RhinoApp.InvokeOnUiThread</c>
        /// (or <c>CanvasAccess.RunOnUiThread</c>).
        /// </remarks>
        /// <param name="contents">Raw result file contents, in user order.</param>
        /// <param name="readErrors">Optional file-read diagnostics surfaced as item messages.</param>
        /// <returns>A <see cref="BatchImportOutcome"/> describing what happened.</returns>
        public Cores.BatchImportOutcome ImportBatchResultContents(
            IReadOnlyList<string> contents,
            IReadOnlyList<string> readErrors = null)
        {
            var outcome = new Cores.BatchImportOutcome();
            try
            {
                var hasSentinels = this._batchState.SentinelTrees != null && this._batchState.SentinelTrees.Count > 0;

                if (contents == null || contents.Count == 0)
                {
                    outcome.Error = "No result contents were provided.";
                    return outcome;
                }

                // Resolve provider: the one attached to the active submission,
                // else the provider that last submitted/executed (covers a
                // restored or already-completed batch), else the live selection.
                var providerName = this._batchState.Submission?.ProviderName
                    ?? this._batchState.SubmittedProviderName
                    ?? this.GetActualAIProviderName();
                outcome.ProviderName = providerName;

                var provider = ProviderManager.Instance.GetProvider(providerName);
                if (provider is not IAIBatchProvider batchProvider)
                {
                    outcome.Error = $"Provider '{providerName}' does not support batch results.";
                    this.SetPersistentRuntimeMessage(
                        "load_results_error",
                        GH_RuntimeMessageLevel.Error,
                        outcome.Error,
                        false);
                    return outcome;
                }

                AIBatchStatus status;
                try
                {
                    status = batchProvider.ParseBatchResultsFiles(contents, this._batchState.Submission?.BatchId);
                }
                catch (Exception ex)
                {
                    outcome.Error = $"Provider '{providerName}' failed to parse loaded files: {ex.Message}";
                    this.SetPersistentRuntimeMessage(
                        "load_results_error",
                        GH_RuntimeMessageLevel.Error,
                        outcome.Error,
                        false);
                    return outcome;
                }

                // Append filesystem read errors as SHRuntimeMessages; the provider
                // has no visibility into disk I/O.
                var messages = status.Messages?.ToList() ?? new List<SHRuntimeMessage>();
                if (readErrors != null)
                {
                    foreach (var err in readErrors)
                    {
                        messages.Add(new SHRuntimeMessage(
                            SHRuntimeMessageSeverity.Error,
                            SHRuntimeMessageOrigin.Worker,
                            SHMessageCode.ConversionFailed,
                            $"Failed to read file: {err}"));
                    }
                }

                // Determine whether sentinels we have are usable for the loaded results.
                //
                // _batchSentinelIds is the set of custom_ids the current in-memory sentinel
                // tree expects (persisted to .ghx and restored on open). When it matches the
                // loaded file, we can finalize directly. Otherwise we trigger a collect-only
                // run that lets the component's normal processing rebuild sentinel trees
                // from scratch, then re-maps results in order.
                var expectedIds = this._batchState.SentinelIds;
                var loadedIds = status.Results?.Keys;
                var loadedTotal = status.Results?.Count ?? 0;
                var expectedTotal = expectedIds?.Count ?? 0;
                var matchedIds = hasSentinels && expectedIds != null && expectedIds.Count > 0 && loadedIds != null
                    ? loadedIds.Where(expectedIds.Contains).ToList()
                    : new List<string>();
                var matched = matchedIds.Count;
                outcome.LoadedCount = loadedTotal;
                outcome.ExpectedCount = expectedTotal;
                outcome.MatchedCount = matched;

                Logger.For<AIStatefulAsyncComponentBase>().Trace($"[AIStatefulAsync] ImportBatchResultContents: provider={providerName}, " +
                    $"results={loadedTotal}, messages={messages.Count}, hasSentinels={hasSentinels}, matched={matched}/{expectedTotal}");

                if (loadedTotal == 0)
                {
                    // Nothing parseable — an empty import takes no ownership:
                    // report the failure but leave the in-flight poll and the
                    // component state untouched so the user can retry.
                    var detail = messages.Count > 0
                        ? $" ({messages[0].Message})"
                        : string.Empty;
                    outcome.Error = $"No batch results could be parsed from the provided file(s).{detail}";
                    this.SetPersistentRuntimeMessage(
                        "load_results_error",
                        GH_RuntimeMessageLevel.Error,
                        outcome.Error,
                        false);
                    return outcome;
                }

                // Pre-flight the chosen mode BEFORE taking completion
                // ownership: the collect-only fallback needs an active batch
                // mode to rebuild the queue, and an import that cannot proceed
                // must leave the live poll untouched.
                //
                // A direct finalize additionally requires every matched id to be
                // resolvable — the customId → unitKey map and the unit's pending
                // fragment must both exist in the result store. They are only
                // persisted while a submission is in flight, so a matched id on
                // a restored completed component is stale bookkeeping:
                // finalizing it directly resolves nothing, the assessor counts
                // the id unresolved, and the resulting NeedsRun resubmits paid
                // work against already-published outputs. Unresolvable matches
                // fall back to collect-only, which rebuilds a live mapping.
                var canFinalizeDirectly = hasSentinels
                    && matched > 0
                    && matchedIds.All(id => this.ResultStore.CanResolveSentinel(id));
                if (!canFinalizeDirectly && !this.IsBatchRequest())
                {
                    outcome.Error = "Cannot rebuild the sentinel mapping: batch mode is not active on this component (Batch tier disabled, or the selected provider/model does not support batch). Re-enable batch mode or align the file's custom_ids with the expected sentinel ids and retry.";
                    this.SetPersistentRuntimeMessage(
                        "load_results_error",
                        GH_RuntimeMessageLevel.Error,
                        outcome.Error,
                        false);
                    return outcome;
                }

                // Single completion owner: the import supersedes local polling.
                // Detach the poll timer, drop the installed submission (keeping
                // its provider for decode/metrics) and invalidate in-flight
                // callbacks — including a still-pending provider submit — so
                // nothing can finalize the same run again. The remote batch is
                // left alone upstream.
                var priorSubmission = this._batchState.Submission;
                if (priorSubmission != null || this._batchState.PendingSubmissionEpoch >= 0)
                {
                    Logger.For<AIStatefulAsyncComponentBase>().Trace($"[AIStatefulAsync] Import detaching local poll for batch {priorSubmission?.BatchId ?? "(pending submission)"} — import takes over completion");
                    if (priorSubmission != null)
                    {
                        this._batchState.SubmittedProviderName = priorSubmission.ProviderName;
                        this._batchState.Submission = null;
                    }

                    this._batchState.InvalidateAsyncWork();
                    this.StopBatchPollTimer(priorSubmission);
                }

                // Direct path: sentinels are present and at least some custom_ids match.
                if (canFinalizeDirectly)
                {
                    if (matched < expectedTotal)
                    {
                        messages.Add(new SHRuntimeMessage(
                            SHRuntimeMessageSeverity.Warning,
                            SHRuntimeMessageOrigin.Worker,
                            SHMessageCode.ConversionFailed,
                            $"Only {matched}/{expectedTotal} expected sentinels matched the loaded file(s); " +
                            $"{expectedTotal - matched} output branch(es) will retain their sentinel placeholders."));
                    }

                    var effectiveResults = status.Results;
                    this.StateManager.SuppressInputChangesForNextSolve();
                    Rhino.RhinoApp.InvokeOnUiThread(() =>
                    {
                        try
                        {
                            this.ClearPersistentRuntimeMessages();
                            this._batchState.CompletionTime ??= 0.0;
                            this.CompleteBatchAndTransition(effectiveResults, messages, expectedIds, forceState: true);
                            this.CommitRunInputSnapshot();
                        }
                        catch (Exception ex)
                        {
                            this.SetPersistentRuntimeMessage(
                                "load_results_error",
                                GH_RuntimeMessageLevel.Error,
                                $"Error processing loaded results: {ex.Message}",
                                false);
                            this.StateManager.ForceState(ComponentState.Error);
                            this.ExpireSolution(true);
                        }
                    });
                    outcome.Success = true;
                    outcome.Mode = Cores.BatchImportOutcome.ImportMode.Direct;
                    return outcome;
                }

                // Fallback path: sentinels missing or custom_ids don't match.
                // Stage loaded results and trigger a collect-only run so the component's
                // normal processing rebuilds sentinel trees, then SubmitBatchQueueAsync
                // re-maps our staged results to fresh custom IDs (order-based) and
                // finalizes through the same OnBatchFinalized pipeline.
                Logger.For<AIStatefulAsyncComponentBase>().Trace($"[AIStatefulAsync] ImportBatchResultContents: Triggering collect-only fallback run (hasSentinels={hasSentinels}, matched={matched})");

                messages.Add(new SHRuntimeMessage(
                    SHRuntimeMessageSeverity.Warning,
                    SHRuntimeMessageOrigin.Worker,
                    SHMessageCode.ReturnInvalid,
                    $"Sentinel mapping unavailable; loaded {loadedTotal} result(s) will be mapped to input branches in order. " +
                    "This assumes the input structure hasn't changed since the batch was submitted."));

                this._pendingFileLoadedResults = status.Results;
                this._pendingFileLoadMessages = messages;
                this._batchCollectOnly = true;

                // Suppress input-change detection so the forced solve is not misclassified.
                this.StateManager.SuppressInputChangesForNextSolve();

                // Trigger the component's normal processing path on the UI thread.
                // ForceState(Processing) followed by ExpireSolution runs OnStateProcessing,
                // which spawns the worker → RunProcessingAsync → TrySubmitBatchAsync →
                // SubmitBatchQueueAsync (collect-only branch) → CompleteSubmissionSetup
                // drains the staged results after the sentinel trees are installed.
                Rhino.RhinoApp.InvokeOnUiThread(() =>
                {
                    try
                    {
                        this.ClearPersistentRuntimeMessages();
                        this.StateManager.ForceState(ComponentState.Processing);
                        this.ExpireSolution(true);
                    }
                    catch (Exception ex)
                    {
                        this._batchCollectOnly = false;
                        this._pendingFileLoadedResults = null;
                        this._pendingFileLoadMessages = null;
                        this.SetPersistentRuntimeMessage(
                            "load_results_error",
                            GH_RuntimeMessageLevel.Error,
                            $"Error triggering collect-only fallback: {ex.Message}",
                            false);
                        this.StateManager.ForceState(ComponentState.Error);
                        this.ExpireSolution(true);
                    }
                });
                outcome.Success = true;
                outcome.Mode = Cores.BatchImportOutcome.ImportMode.CollectOnly;
                return outcome;
            }
            catch (Exception ex)
            {
                outcome.Error = $"Error loading results files: {ex.Message}";
                this.SetPersistentRuntimeMessage(
                    "load_results_error",
                    GH_RuntimeMessageLevel.Error,
                    outcome.Error,
                    false);
                return outcome;
            }
        }

        /// <summary>
        /// Manually checks the current batch status and updates the component state.
        /// Available only when a batch is in processing state.
        /// </summary>
        private void CheckBatchStatus()
        {
            if (this._batchState.Submission == null)
            {
                this.SetPersistentRuntimeMessage(
                    "check_batch_status",
                    GH_RuntimeMessageLevel.Warning,
                    "No active batch submission found.",
                    false);
                return;
            }

            // Trigger an immediate poll
            _ = this.PollBatchStatusAsync();
        }

    }
}
