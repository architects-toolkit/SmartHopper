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
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SmartHopper.Core.ComponentBase;
using SmartHopper.Core.ComponentBase.Cores;
using SmartHopper.Core.Grasshopper.Utils.Canvas;
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.Infrastructure.AITools;
using SmartHopper.Infrastructure.FileSystem;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AICall.Core.Returns;
using SmartHopper.ProviderSdk.Diagnostics;

using SmartHopper.ProviderSdk.Logging;

namespace SmartHopper.Core.Grasshopper.AITools
{
    /// <summary>
    /// Tool provider for importing locally saved provider batch-result files into
    /// a stateful SmartHopper AI component. Shares the exact pipeline of the
    /// component's "Load results from file" menu entry
    /// (<see cref="AIStatefulAsyncComponentBase.ImportBatchResultContents"/>) so
    /// both entry points obey one set of lifecycle guarantees.
    /// </summary>
    /// <remarks>
    /// Guardrails:
    /// <list type="bullet">
    ///   <item>Mutating tool — only exposed when the host allows canvas editing.</item>
    ///   <item>Reads files on the Rhino host — annotated <c>openWorldHint</c>.</item>
    ///   <item>Never guesses the target: an explicit component instance GUID is required.</item>
    ///   <item>Identity-based import: payloads are matched by <c>custom_id</c>; the
    ///   component decides direct vs collect-only fallback — the tool never
    ///   performs positional mapping itself.</item>
    ///   <item>No implicit remote side effects: importing detaches the local poll
    ///   but never cancels or resubmits the remote job.</item>
    /// </list>
    /// </remarks>
    public class gh_batch_import_results : IAIToolProvider
    {
        /// <summary>Maximum accepted size per result file (defense against multi-GB payloads).</summary>
        private const long MaxResultFileBytes = 64L * 1024 * 1024;

        /// <inheritdoc/>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AIMutatingTool(
                name: "gh_batch_import_results",
                description: "Import locally saved provider batch-result files (*.jsonl/*.json) into a stateful SmartHopper AI component that ran a batch (Batch tier). Results are matched by custom_id: when the component's expected sentinel ids match the file contents the run finalizes immediately; otherwise a collect-only recollection run rebuilds the mapping. Returns per-import statistics; result bodies are delivered as component outputs, never in the tool response. Requires the component instance GUID (from gh_get) and absolute file paths on the Rhino host.",
                category: "Canvas.ComponentControl",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""componentGuid"": {
                            ""type"": ""string"",
                            ""description"": ""Instance GUID of the target SmartHopper AI component (from gh_get).""
                        },
                        ""filePaths"": {
                            ""type"": ""array"",
                            ""items"": { ""type"": ""string"" },
                            ""description"": ""Absolute paths on the Rhino host to downloaded provider batch-result files (*.jsonl/*.json).""
                        }
                    },
                    ""required"": [ ""componentGuid"", ""filePaths"" ]
                }",
                execute: this.ImportBatchResultsAsync,
                tags: new[] { "canvas", "components", "batch", "mutating" },
                outputSchema: @"{ ""type"": ""object"", ""properties"": { ""success"": { ""type"": ""boolean"" }, ""mode"": { ""type"": ""string"" }, ""provider"": { ""type"": ""string"" }, ""loaded"": { ""type"": ""integer"" }, ""matched"": { ""type"": ""integer"" }, ""expected"": { ""type"": ""integer"" }, ""error"": { ""type"": ""string"" } } }",
                annotations: new AIToolAnnotations(destructiveHint: false, openWorldHint: true),
                activityLabel: "importing batch results",
                resultLabel: "imported batch results");
        }

        /// <summary>
        /// Executes the import: validates arguments, reads the files under a size
        /// cap, resolves the target component, and runs the shared import
        /// pipeline on the UI thread.
        /// </summary>
        private async Task<AIReturn> ImportBatchResultsAsync(AIToolCall toolCall)
        {
            var output = new AIReturn() { Request = toolCall };

            try
            {
                // Local tool: do not enforce provider/model/finish_reason metrics
                toolCall.SkipMetricsValidation = true;

                AIInteractionToolCall toolInfo = toolCall.GetToolCall();
                var args = toolInfo.GetArgumentsOrEmpty();

                var guidText = args["componentGuid"]?.ToString();
                if (!Guid.TryParse(guidText, out var componentGuid))
                {
                    output.CreateError("Invalid or missing 'componentGuid' — provide the component's instance GUID (from gh_get).");
                    return output;
                }

                var filePaths = args["filePaths"]?.ToObject<List<string>>() ?? new List<string>();
                if (filePaths.Count == 0)
                {
                    output.CreateError("At least one file path must be provided in 'filePaths'.");
                    return output;
                }

                // Resolve and validate the target component before any review or I/O.
                var target = CanvasAccess.FindInstance(componentGuid);
                if (target is not AIStatefulAsyncComponentBase component)
                {
                    output.CreateError(
                        target == null
                            ? $"No document object found for GUID '{componentGuid}'."
                            : $"Object '{componentGuid}' is not a SmartHopper AI component (got {target.GetType().Name}).");
                    return output;
                }

                // Mutation approval — same per-component review flow other
                // canvas-state tools use; skipped automatically when the host
                // auto-approves mutations.
                var review = CanvasChangeReviewService.CreateComponentStateSession(
                    toolInfo.Name ?? "gh_batch_import_results",
                    new[] { componentGuid },
                    $"Import {filePaths.Count} batch result file(s)");
                var approved = await CanvasChangeReviewService.ReviewAsync(
                    review,
                    toolCall.InvocationContext,
                    toolCall.CancellationToken).ConfigureAwait(false);
                if (!approved || !CanvasChangeReviewService.GetAcceptedComponentGuids(review).Contains(componentGuid))
                {
                    output.CreateError("Batch results import was rejected by the user.");
                    return output;
                }

                // Read file contents on the Rhino host with a per-file size cap.
                var contents = new List<string>(filePaths.Count);
                var readErrors = new List<string>();
                var unreadable = 0;
                foreach (var path in filePaths)
                {
                    try
                    {
                        await LocalFileAccess.RequireAsync(
                            path, LocalFileCapability.Read, toolInfo.Name ?? "gh_batch_import_results",
                            toolCall.CancellationToken).ConfigureAwait(false);

                        if (!File.Exists(path))
                        {
                            readErrors.Add($"{Path.GetFileName(path)}: file does not exist");
                            unreadable++;
                            continue;
                        }

                        var length = new FileInfo(path).Length;
                        if (length > MaxResultFileBytes)
                        {
                            readErrors.Add($"{Path.GetFileName(path)}: file exceeds the {MaxResultFileBytes / (1024 * 1024)} MiB limit");
                            unreadable++;
                            continue;
                        }

                        contents.Add(File.ReadAllText(path));
                    }
                    catch (Exception ex)
                    {
                        readErrors.Add($"{Path.GetFileName(path)}: {ex.Message}");
                        unreadable++;
                    }
                }

                if (contents.Count == 0)
                {
                    output.CreateError($"None of the {filePaths.Count} provided file(s) could be read: {string.Join("; ", readErrors)}");
                    return output;
                }

                // The import performs state transitions and expiration — marshal
                // the whole operation onto the UI thread.
                var outcome = CanvasAccess.RunOnUiThread(() =>
                    component.ImportBatchResultContents(contents, readErrors));

                Logger.For<gh_batch_import_results>().Trace(
                    $"[GhBatchImport] component={componentGuid}, mode={outcome.Mode}, loaded={outcome.LoadedCount}, matched={outcome.MatchedCount}/{outcome.ExpectedCount}, success={outcome.Success}");

                var toolResult = new JObject
                {
                    ["success"] = outcome.Success,
                    ["mode"] = outcome.Mode.ToString(),
                    ["provider"] = outcome.ProviderName,
                    ["loaded"] = outcome.LoadedCount,
                    ["matched"] = outcome.MatchedCount,
                    ["expected"] = outcome.ExpectedCount,
                    ["unreadableFiles"] = unreadable,
                };
                if (!string.IsNullOrEmpty(outcome.Error))
                {
                    toolResult["error"] = outcome.Error;
                }

                var immutableBody = AIBodyBuilder.Create()
                    .AddToolResult(toolResult, id: toolInfo.Id, name: toolInfo.Name ?? "gh_batch_import_results")
                    .Build();

                output.CreateSuccess(immutableBody, toolCall);
                return output;
            }
            catch (Exception ex)
            {
                output.CreateError($"Error: {ex.Message}");
                return output;
            }
        }
    }
}
