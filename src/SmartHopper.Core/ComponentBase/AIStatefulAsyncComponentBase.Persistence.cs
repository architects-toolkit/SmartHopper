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
using SmartHopper.Infrastructure.AICall.Batch;
using SmartHopper.Infrastructure.AICall.Core;
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Requests;
using SmartHopper.Infrastructure.AICall.Core.Returns;
using SmartHopper.Infrastructure.AIProviders;
using SmartHopper.Infrastructure.Diagnostics;

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
        /// Gets the current AI request parameters.
        /// </summary>
        /// <returns>The current <see cref="AIRequestParameters"/>, never null.</returns>
        protected AIRequestParameters GetParameters() => this._requestParameters ?? AIRequestParameters.Empty;

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
                    writer.SetString(PersistenceKeys.BatchSubmittedAt, this._batchState.Submission.SubmittedAt.ToString("O"));

                    if (this._batchState.Submission.CustomIds != null && this._batchState.Submission.CustomIds.Count > 0)
                    {
                        writer.SetString(PersistenceKeys.BatchCustomIds, new JArray(this._batchState.Submission.CustomIds.ToArray()).ToString(Newtonsoft.Json.Formatting.None));
                    }

                    Debug.WriteLine($"[AIStatefulAsync] Write: persisted batch state, batchId={this._batchState.Submission.BatchId}, items={this._batchState.Submission.CustomIds?.Count ?? 0}");
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
                    Debug.WriteLine($"[AIStatefulAsync] Write: persisted {this._batchState.SentinelTrees.Count} sentinel tree(s)");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AIStatefulAsync] Write batch state error: {ex.Message}");
            }

            return true;
        }

        /// <inheritdoc/>
        public override bool Read(GH_IO.Serialization.GH_IReader reader)
        {
            if (!base.Read(reader)) return false;

            try
            {
                if (reader.ItemExists(PersistenceKeys.BatchId))
                {
                    var batchId = reader.GetString(PersistenceKeys.BatchId);
                    var providerName = reader.ItemExists(PersistenceKeys.BatchProvider) ? reader.GetString(PersistenceKeys.BatchProvider) : string.Empty;
                    var serializedReq = reader.ItemExists(PersistenceKeys.BatchRequest) ? reader.GetString(PersistenceKeys.BatchRequest) : string.Empty;

                    IReadOnlyList<string> customIds = null;
                    if (reader.ItemExists(PersistenceKeys.BatchCustomIds))
                    {
                        var idsJson = reader.GetString(PersistenceKeys.BatchCustomIds);
                        if (!string.IsNullOrEmpty(idsJson))
                        {
                            customIds = JArray.Parse(idsJson).Values<string>().ToList().AsReadOnly();
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
                        this._batchState.Submission = new SmartHopper.Infrastructure.AICall.Batch.AIBatchSubmission(batchId, providerName, serializedReq, customIds ?? new List<string>().AsReadOnly());
                        Debug.WriteLine($"[AIStatefulAsync] Read: restored batch state, batchId={batchId}, items={customIds?.Count ?? 0}");

                        // Restore component to Processing state so sentinel values aren't output
                        this.StateManager.ForceState(ComponentState.Processing);
                        Debug.WriteLine($"[AIStatefulAsync] Read: restored state to Processing for active batch");

                        // Resume polling — defer until after component is fully loaded
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
                }

                if (reader.ItemExists(PersistenceKeys.BatchSentinelIds))
                {
                    var sentinelJson = reader.GetString(PersistenceKeys.BatchSentinelIds);
                    if (!string.IsNullOrEmpty(sentinelJson))
                    {
                        this._batchState.SentinelIds = new HashSet<string>(JArray.Parse(sentinelJson).Values<string>());
                        Debug.WriteLine($"[AIStatefulAsync] Read: restored {this._batchState.SentinelIds.Count} sentinel IDs");
                    }
                }

                if (reader.ItemExists(PersistenceKeys.BatchSentinelTrees))
                {
                    var treesJson = reader.GetString(PersistenceKeys.BatchSentinelTrees);
                    if (!string.IsNullOrEmpty(treesJson))
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

                        Debug.WriteLine($"[AIStatefulAsync] Read: restored {this._batchState.SentinelTrees.Count} sentinel tree(s)");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AIStatefulAsync] Read batch state error: {ex.Message}");
            }

            return true;
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
        /// first-wins policy on <c>custom_id</c>. The component base is fully
        /// provider-agnostic: it only reads raw file content as strings and delegates
        /// parsing + merging to <see cref="IAIBatchProvider.ParseBatchResultsFiles"/>.
        /// </summary>
        private void LoadResultsFromFile()
        {
            try
            {
                Debug.WriteLine($"[AIStatefulAsync] LoadResultsFromFile: Early check - _sentinelTrees is {(this._batchState.SentinelTrees == null ? "null" : $"count={this._batchState.SentinelTrees.Count}")}");

                var hasSentinels = this._batchState.SentinelTrees != null && this._batchState.SentinelTrees.Count > 0;
                
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

                Debug.WriteLine($"[AIStatefulAsync] LoadResultsFromFile: After dialog - _sentinelTrees is {(this._batchState.SentinelTrees == null ? "null" : $"count={this._batchState.SentinelTrees.Count}")}, hasSentinels={hasSentinels}");

                // Resolve provider: prefer the one attached to the active batch submission
                // (if any), else fall back to the component's configured provider.
                var providerName = this._batchState.Submission?.ProviderName ?? this.GetActualAIProviderName();
                var provider = ProviderManager.Instance.GetProvider(providerName);
                if (provider is not IAIBatchProvider batchProvider)
                {
                    this.SetPersistentRuntimeMessage(
                        "load_results_error",
                        GH_RuntimeMessageLevel.Error,
                        $"Provider '{providerName}' does not support batch results.",
                        false);
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

                AIBatchStatus status;
                try
                {
                    status = batchProvider.ParseBatchResultsFiles(contents, this._batchState.Submission?.BatchId);
                }
                catch (Exception ex)
                {
                    this.SetPersistentRuntimeMessage(
                        "load_results_error",
                        GH_RuntimeMessageLevel.Error,
                        $"Provider '{providerName}' failed to parse loaded files: {ex.Message}",
                        false);
                    return;
                }

                // Append filesystem read errors as SHRuntimeMessages; the provider
                // has no visibility into disk I/O.
                var messages = status.Messages?.ToList() ?? new List<SHRuntimeMessage>();
                foreach (var err in readErrors)
                {
                    messages.Add(new SHRuntimeMessage(
                        SHRuntimeMessageSeverity.Error,
                        SHRuntimeMessageOrigin.Worker,
                        SHMessageCode.ConversionFailed,
                        $"Failed to read file: {err}"));
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
                var matched = 0;

                if (hasSentinels && expectedIds != null && expectedIds.Count > 0 && loadedIds != null)
                {
                    matched = loadedIds.Count(id => expectedIds.Contains(id));
                    Debug.WriteLine(
                        $"[AIStatefulAsync] LoadResultsFromFile: id-match stats — " +
                        $"loaded={loadedTotal}, expected={expectedTotal}, matched={matched}");
                }

                Debug.WriteLine(
                    $"[AIStatefulAsync] LoadResultsFromFile: provider={providerName}, files={fileNames.Length}, " +
                    $"results={loadedTotal}, messages={messages.Count}, hasSentinels={hasSentinels}, matched={matched}");

                if (loadedTotal == 0)
                {
                    // Nothing parseable — surface error.
                    messages.Add(new SHRuntimeMessage(
                        SHRuntimeMessageSeverity.Error,
                        SHRuntimeMessageOrigin.Worker,
                        SHMessageCode.ReturnInvalid,
                        "No batch results could be parsed from the selected file(s)."));

                    this.StateManager.SuppressInputChangesForNextSolve();
                    Rhino.RhinoApp.InvokeOnUiThread(() =>
                    {
                        this.ClearPersistentRuntimeMessages();
                        this.CompleteBatchAndTransition(null, messages, expectedResultCount: 0, forceState: true);
                        this.StateManager.CommitHashes();
                    });
                    return;
                }

                // Direct path: sentinels are present and at least some custom_ids match.
                if (hasSentinels && matched > 0)
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
                            this.CompleteBatchAndTransition(effectiveResults, messages, expectedResultCount: 0, forceState: true);
                            this.StateManager.CommitHashes();
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
                    return;
                }

                // Fallback path: sentinels missing or custom_ids don't match.
                // Stage loaded results and trigger a collect-only run so the component's
                // normal processing rebuilds sentinel trees, then SubmitBatchQueueAsync
                // re-maps our staged results to fresh custom IDs (order-based) and
                // finalizes through the same OnBatchFinalized pipeline.
                Debug.WriteLine($"[AIStatefulAsync] LoadResultsFromFile: Triggering collect-only fallback run (hasSentinels={hasSentinels}, matched={matched})");

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
                // SubmitBatchQueueAsync (collect-only branch) → schedules finalization.
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
