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
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Grasshopper.Kernel;
using Newtonsoft.Json.Linq;
using SmartHopper.Components.Properties;
using SmartHopper.Core.ComponentBase;
using SmartHopper.ProviderSdk.AICall.Core.Base;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.ProviderSdk.Diagnostics;

namespace SmartHopper.Components.Grasshopper
{
    /// <summary>
    /// Grasshopper component for applying a `.ghpatch` to a base GhJSON document.
    /// </summary>
    public class GhPatchApplyComponents : StatefulComponentBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GhPatchApplyComponents"/> class.
        /// </summary>
        public GhPatchApplyComponents()
            : base(
                "Apply GhPatch",
                "GhPatchApply",
                "Apply a `.ghpatch` patch document to a base GhJSON document. By default, refuses to apply on base checksum mismatch.",
                "SmartHopper",
                "Grasshopper")
        {
            this.RunOnlyOnInputChanges = false;
        }

        /// <inheritdoc/>
        public override Guid ComponentGuid => new Guid("3964AC63-A845-4B7C-86F3-18E505634721");

        /// <inheritdoc/>
        public override GH_Exposure Exposure => GH_Exposure.secondary;

        /// <inheritdoc/>
        protected override Bitmap Icon => Resources.ghjsonpatch;

        /// <inheritdoc/>
        protected override void RegisterAdditionalInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Base", "B", "Base GhJSON document to apply the patch to.", GH_ParamAccess.item);
            pManager.AddTextParameter("Patch", "P", "GhPatch document.", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Verify Base", "V", "Refuse to apply on base checksum mismatch. Defaults to true.", GH_ParamAccess.item, true);
        }

        /// <inheritdoc/>
        protected override void RegisterAdditionalOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Result", "R", "Resulting GhJSON document with the patch applied", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Success", "S", "Whether the apply succeeded with no fatal conflicts", GH_ParamAccess.item);
            pManager.AddTextParameter("Conflicts", "C", "Summary of conflicts encountered during apply", GH_ParamAccess.item);
        }

        /// <inheritdoc/>
        protected override AsyncWorkerBase CreateWorker(Action<string> progressReporter)
        {
            return new GhPatchApplyWorker(this, this.AddRuntimeMessage, progressReporter);
        }

        /// <summary>
        /// Worker that applies a ghpatch to a base GhJSON document asynchronously.
        /// </summary>
        private class GhPatchApplyWorker : AsyncWorkerBase
        {
            private readonly GhPatchApplyComponents parent;
            private readonly Action<string> progressReporter;

            // Input data
            private string baseJson;
            private string patchJson;
            private bool verifyBase;

            // Output data
            private string resultJson = string.Empty;
            private bool success;
            private string conflictsSummary = string.Empty;
            private string error;

            public GhPatchApplyWorker(
                GhPatchApplyComponents parent,
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
                this.baseJson = null;
                this.patchJson = null;
                this.verifyBase = true;

                DA.GetData(0, ref this.baseJson);
                DA.GetData(1, ref this.patchJson);
                DA.GetData(2, ref this.verifyBase);

                dataCount = !string.IsNullOrWhiteSpace(this.baseJson) && !string.IsNullOrWhiteSpace(this.patchJson) ? 1 : 0;
            }

            /// <inheritdoc/>
            public override async Task DoWorkAsync(CancellationToken token)
            {
                this.resultJson = string.Empty;
                this.success = false;
                this.conflictsSummary = string.Empty;

                if (string.IsNullOrWhiteSpace(this.baseJson) || string.IsNullOrWhiteSpace(this.patchJson))
                {
                    this.error = "Base GhJSON and Patch document are required";
                    return;
                }

                this.progressReporter?.Invoke("Applying patch...");

                try
                {
                    token.ThrowIfCancellationRequested();

                    var parameters = new JObject
                    {
                        ["base"] = this.baseJson,
                        ["patch"] = this.patchJson,
                        ["verifyBase"] = this.verifyBase,
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

                        this.error = parts.Count > 0 ? string.Join(" \n", parts) : "gh_patch_apply execution failed";
                        return;
                    }

                    var toolResult = ToolCallResult.FromAIReturn(aiResult);

                    if (toolResult.Result == null)
                    {
                        this.error = "Tool 'gh_patch_apply' did not return a valid result";
                        return;
                    }

                    this.resultJson = toolResult["ghjson"]?.ToString() ?? string.Empty;
                    this.success = toolResult["success"]?.ToObject<bool>() ?? false;
                    var conflictsArray = toolResult["conflicts"] as JArray;

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
                }
                catch (OperationCanceledException)
                {
                    this.error = "Operation cancelled";
                }
                catch (Exception ex)
                {
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

                this.parent.SetPersistentOutput("Result", this.resultJson, DA);
                this.parent.SetPersistentOutput("Success", this.success, DA);
                this.parent.SetPersistentOutput("Conflicts", this.conflictsSummary, DA);

                message = this.success ? "Applied" : "Apply failed";
            }
        }
    }
}
