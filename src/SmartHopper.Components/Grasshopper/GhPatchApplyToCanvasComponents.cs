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
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GhJSON.Core;
using GhJSON.Grasshopper;
using Grasshopper.Kernel;
using Newtonsoft.Json.Linq;
using SmartHopper.Core.ComponentBase;
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.ProviderSdk.AICall.Core.Base;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.Diagnostics;

namespace SmartHopper.Components.Grasshopper
{
    /// <summary>
    /// Grasshopper component that applies a <c>.ghpatch</c> directly to the current canvas.
    /// Gets the current canvas as base, applies the patch, deletes removed components,
    /// and calls <c>gh_put</c> (edit mode) so only changed components are updated.
    /// </summary>
    public class GhPatchApplyToCanvasComponents : StatefulComponentBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GhPatchApplyToCanvasComponents"/> class.
        /// </summary>
        public GhPatchApplyToCanvasComponents()
            : base(
                "Apply GhPatch to Canvas",
                "GhPatchApplyCanvas",
                "Apply a `.ghpatch` directly to the current Grasshopper canvas. Only actually changed components are replaced.",
                "SmartHopper",
                "Grasshopper")
        {
            this.RunOnlyOnInputChanges = false;
        }

        /// <inheritdoc/>
        public override Guid ComponentGuid => new Guid("5E7C18F0-284C-41B0-B50D-BAB0AF23D690");

        /// <inheritdoc/>
        protected override Bitmap Icon => null;

        /// <inheritdoc/>
        protected override void RegisterAdditionalInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Patch", "P", "GhPatch document string.", GH_ParamAccess.item);
        }

        /// <inheritdoc/>
        protected override void RegisterAdditionalOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddBooleanParameter("Success", "S", "Whether the patch applied with no fatal conflicts.", GH_ParamAccess.item);
            pManager.AddTextParameter("Conflicts", "C", "Summary of conflicts encountered during apply.", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Components+", "C+", "Components added.", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Components-", "C-", "Components removed.", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Components~", "C~", "Components modified.", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Connections+", "W+", "Connections added.", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Connections-", "W-", "Connections removed.", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Groups+", "G+", "Groups added.", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Groups-", "G-", "Groups removed.", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Groups~", "G~", "Groups modified.", GH_ParamAccess.item);
        }

        /// <inheritdoc/>
        protected override AsyncWorkerBase CreateWorker(Action<string> progressReporter)
        {
            return new GhPatchApplyToCanvasWorker(this, this.AddRuntimeMessage, progressReporter);
        }

        /// <summary>
        /// Worker that applies a ghpatch to the canvas asynchronously.
        /// </summary>
        private class GhPatchApplyToCanvasWorker : AsyncWorkerBase
        {
            private readonly Action<string> progressReporter;
            private readonly GhPatchApplyToCanvasComponents parent;

            // Input data
            private string patchJson;

            // Output data
            private bool success;
            private string conflictsSummary = string.Empty;
            private int componentsAdded;
            private int componentsRemoved;
            private int componentsModified;
            private int connectionsAdded;
            private int connectionsRemoved;
            private int groupsAdded;
            private int groupsRemoved;
            private int groupsModified;
            private string error;

            public GhPatchApplyToCanvasWorker(
                GhPatchApplyToCanvasComponents parent,
                Action<GH_RuntimeMessageLevel, string> addRuntimeMessage,
                Action<string> progressReporter)
                : base(parent, addRuntimeMessage)
            {
                this.parent = parent;
                this.progressReporter = progressReporter;
            }

            /// <inheritdoc/>
            public override void GatherInput(IGH_DataAccess DA, out int dataCount)
            {
                dataCount = 1;

                this.patchJson = null;
                DA.GetData(0, ref this.patchJson);
            }

            /// <inheritdoc/>
            public override async Task DoWorkAsync(CancellationToken token)
            {
                if (string.IsNullOrEmpty(this.patchJson))
                {
                    this.error = "Patch document is required";
                    return;
                }

                this.progressReporter?.Invoke("Applying patch...");

                try
                {
                    token.ThrowIfCancellationRequested();

                    // 1. Get current canvas as base
                    var baseDoc = GhJsonGrasshopper.Get();
                    if (baseDoc == null)
                    {
                        this.error = "Failed to retrieve current canvas document";
                        return;
                    }

                    var baseJson = GhJson.ToJson(baseDoc, new GhJSON.Core.Serialization.WriteOptions { Indented = false });

                    // 2. Apply patch via AI tool
                    var parameters = new JObject
                    {
                        ["base"] = baseJson,
                        ["patch"] = this.patchJson,
                        ["verifyBase"] = true,
                        ["continueOnConflict"] = true,
                        ["renumberCollidingAddedIds"] = true,
                    };

                    var toolCallInteraction = new AIInteractionToolCall
                    {
                        Name = "gh_patch_apply",
                        Arguments = parameters,
                        Agent = AIAgent.Assistant,
                    };

                    var toolCall = new AIToolCall();
                    toolCall.Endpoint = "gh_patch_apply";
                    toolCall.FromToolCallInteraction(toolCallInteraction);
                    toolCall.SkipMetricsValidation = true;

                    var aiResult = await toolCall.Exec().ConfigureAwait(false);

                    token.ThrowIfCancellationRequested();

                    if (!aiResult.Success)
                    {
                        var parts = new List<string>();
                        if (aiResult.Messages != null)
                        {
                            parts.AddRange(
                                aiResult.Messages
                                    .Where(msg => !string.IsNullOrWhiteSpace(msg?.Message))
                                    .Select(msg => $"{msg.Severity}: {msg.Message}"));
                        }

                        var errorInteraction = aiResult.Body?.GetLastInteraction(AIAgent.Error) as AIInteractionRuntimeMessage;
                        var errorPayload = errorInteraction?.Content;
                        if (!string.IsNullOrWhiteSpace(errorPayload))
                        {
                            parts.Add(errorPayload);
                        }

                        var combined = parts.Count > 0 ? string.Join(" \n", parts) : "gh_patch_apply execution failed";
                        Debug.WriteLine($"[GhPatchApplyToCanvas] gh_patch_apply Exec failed: {combined}");
                        this.error = combined;
                        return;
                    }

                    var toolResult = ToolCallResult.FromAIReturn(aiResult);

                    if (toolResult.Result == null)
                    {
                        this.error = "Tool 'gh_patch_apply' did not return a valid result";
                        return;
                    }

                    this.success = toolResult["success"]?.ToObject<bool>() ?? false;
                    var resultJson = toolResult["ghjson"]?.ToString() ?? string.Empty;
                    var conflictsArray = toolResult["conflicts"] as JArray;

                    this.componentsAdded = toolResult["componentsAdded"]?.ToObject<int>() ?? 0;
                    this.componentsRemoved = toolResult["componentsRemoved"]?.ToObject<int>() ?? 0;
                    this.componentsModified = toolResult["componentsModified"]?.ToObject<int>() ?? 0;
                    this.connectionsAdded = toolResult["connectionsAdded"]?.ToObject<int>() ?? 0;
                    this.connectionsRemoved = toolResult["connectionsRemoved"]?.ToObject<int>() ?? 0;
                    this.groupsAdded = toolResult["groupsAdded"]?.ToObject<int>() ?? 0;
                    this.groupsRemoved = toolResult["groupsRemoved"]?.ToObject<int>() ?? 0;
                    this.groupsModified = toolResult["groupsModified"]?.ToObject<int>() ?? 0;

                    if (conflictsArray != null && conflictsArray.Count > 0)
                    {
                        var lines = new List<string>();
                        foreach (var c in conflictsArray)
                        {
                            var kind = c["kind"]?.ToString() ?? "?";
                            var message = c["message"]?.ToString() ?? string.Empty;
                            var path = c["path"]?.ToString() ?? string.Empty;
                            lines.Add(string.IsNullOrEmpty(path) ? $"[{kind}] {message}" : $"[{kind}] {message} ({path})");
                        }

                        this.conflictsSummary = string.Join("\n", lines);
                    }

                    if (!this.success)
                    {
                        this.CollectMessage(SHRuntimeMessageSeverity.Warning, $"Patch apply reported failure ({conflictsArray?.Count ?? 0} conflict(s))");
                    }
                    else if (conflictsArray != null && conflictsArray.Count > 0)
                    {
                        this.CollectMessage(SHRuntimeMessageSeverity.Info, $"{conflictsArray.Count} non-fatal conflict(s)");
                    }

                    // 3. Delete removed components from canvas
                    if (this.componentsRemoved > 0)
                    {
                        var removedGuids = ExtractRemovedGuids(baseDoc, resultJson);
                        if (removedGuids.Count > 0)
                        {
                            var deleteResult = GhJsonGrasshopper.Delete(removedGuids);
                            if (!deleteResult.Success)
                            {
                                this.CollectMessage(SHRuntimeMessageSeverity.Warning, $"Failed to delete some removed components: {string.Join(", ", deleteResult.Failed)}");
                            }
                        }
                    }

                    // 4. Call gh_put to place modified/added components
                    if (!string.IsNullOrEmpty(resultJson))
                    {
                        this.progressReporter?.Invoke("Updating canvas...");

                        var putParameters = new JObject
                        {
                            ["ghjson"] = resultJson,
                            ["editMode"] = true,
                        };

                        var putToolCallInteraction = new AIInteractionToolCall
                        {
                            Name = "gh_put",
                            Arguments = putParameters,
                            Agent = AIAgent.Assistant,
                        };

                        var putToolCall = new AIToolCall();
                        putToolCall.Endpoint = "gh_put";
                        putToolCall.FromToolCallInteraction(putToolCallInteraction);
                        putToolCall.SkipMetricsValidation = true;

                        var putAiResult = await putToolCall.Exec().ConfigureAwait(false);

                        if (!putAiResult.Success)
                        {
                            this.CollectMessage(SHRuntimeMessageSeverity.Warning, "gh_put did not complete successfully after patch apply");
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    this.error = "Operation cancelled";
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[GhPatchApplyToCanvas] Error: {ex.Message}");
                    this.error = ex.Message;
                }
            }

            /// <inheritdoc/>
            public override void SetOutput(IGH_DataAccess DA, out string message)
            {
                message = null;

                if (!string.IsNullOrEmpty(this.error))
                {
                    this.CollectMessage(SHRuntimeMessageSeverity.Error, this.error);
                    return;
                }

                this.parent.SetPersistentOutput("Success", this.success, DA);
                this.parent.SetPersistentOutput("Conflicts", this.conflictsSummary, DA);
                this.parent.SetPersistentOutput("Components+", this.componentsAdded, DA);
                this.parent.SetPersistentOutput("Components-", this.componentsRemoved, DA);
                this.parent.SetPersistentOutput("Components~", this.componentsModified, DA);
                this.parent.SetPersistentOutput("Connections+", this.connectionsAdded, DA);
                this.parent.SetPersistentOutput("Connections-", this.connectionsRemoved, DA);
                this.parent.SetPersistentOutput("Groups+", this.groupsAdded, DA);
                this.parent.SetPersistentOutput("Groups-", this.groupsRemoved, DA);
                this.parent.SetPersistentOutput("Groups~", this.groupsModified, DA);
            }

            /// <summary>
            /// Extracts the instance GUIDs of components that exist in the base document
            /// but are missing from the patched document.
            /// </summary>
            private static List<Guid> ExtractRemovedGuids(GhJSON.Core.SchemaModels.GhJsonDocument baseDoc, string patchedJson)
            {
                var removed = new List<Guid>();

                if (baseDoc?.Components == null || string.IsNullOrEmpty(patchedJson))
                {
                    return removed;
                }

                var patchedDoc = GhJson.FromJson(patchedJson);
                var patchedGuids = new HashSet<Guid>(
                    patchedDoc.Components?
                        .Where(c => c.InstanceGuid.HasValue && c.InstanceGuid != Guid.Empty)
                        .Select(c => c.InstanceGuid.Value) ?? Enumerable.Empty<Guid>());

                foreach (var comp in baseDoc.Components)
                {
                    if (comp.InstanceGuid.HasValue && comp.InstanceGuid != Guid.Empty && !patchedGuids.Contains(comp.InstanceGuid.Value))
                    {
                        removed.Add(comp.InstanceGuid.Value);
                    }
                }

                return removed;
            }
        }
    }
}