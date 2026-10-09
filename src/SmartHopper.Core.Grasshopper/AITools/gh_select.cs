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
using System.Linq;
using System.Threading.Tasks;
using Grasshopper;
using Grasshopper.Kernel;
using Newtonsoft.Json.Linq;
using SmartHopper.Core.Grasshopper.Utils.Canvas;
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.Infrastructure.AITools;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AICall.Core.Returns;

namespace SmartHopper.Core.Grasshopper.AITools
{
    /// <summary>
    /// Mutating tool that updates the Grasshopper canvas selection. Selection state is
    /// user-interface state: Grasshopper does not include it in the document undo stack,
    /// so no undo event is recorded (consistent with manual selection changes). Proposed
    /// selection changes still go through the standard consent review, and protected
    /// components are never added to the selection.
    /// </summary>
    public class gh_select : IAIToolProvider
    {
        /// <summary>
        /// Name of the AI tool provided by this class.
        /// </summary>
        private const string ToolName = "gh_select";

        /// <summary>
        /// Returns AI tools for canvas selection control.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AIMutatingTool(
                name: ToolName,
                description: "Updates the Grasshopper canvas selection. Modes: 'set' replaces the selection with the given GUIDs; 'add' keeps the current selection and adds the given GUIDs; 'remove' deselects the given GUIDs; 'clear' deselects everything.",
                category: "Components",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""guids"": {
                            ""type"": ""array"",
                            ""items"": { ""type"": ""string"" },
                            ""description"": ""Instance GUIDs to select or deselect. Required for modes set, add and remove; ignored for clear.""
                        },
                        ""mode"": {
                            ""type"": ""string"",
                            ""enum"": [""set"", ""add"", ""remove"", ""clear""],
                            ""default"": ""set"",
                            ""description"": ""How the selection should be updated.""
                        }
                    }
                }",
                execute: this.GhSelectAsync,
                tags: new[] { "canvas", "selection", "mutating" },
                outputSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""mode"": { ""type"": ""string"" },
                        ""selected"": { ""type"": ""array"" },
                        ""changed"": { ""type"": ""array"" },
                        ""skippedProtected"": { ""type"": ""array"" },
                        ""missingGuids"": { ""type"": ""array"" }
                    }
                }",
                annotations: new AIToolAnnotations(destructiveHint: false));
        }

        private async Task<AIReturn> GhSelectAsync(AIToolCall toolCall)
        {
            var output = new AIReturn { Request = toolCall };
            try
            {
                var toolInfo = toolCall.GetToolCall();
                var args = toolInfo.GetArgumentsOrEmpty();
                var mode = args["mode"]?.ToString()?.ToLowerInvariant() ?? "set";
                if (mode != "set" && mode != "add" && mode != "remove" && mode != "clear")
                {
                    output.CreateError($"Invalid 'mode' value '{mode}'. Expected set, add, remove or clear.");
                    return output;
                }

                var requested = ParseGuids(args["guids"] as JArray);
                if (mode != "clear" && requested.Count == 0)
                {
                    output.CreateError($"Mode '{mode}' requires a non-empty 'guids' array.");
                    return output;
                }

                // The Grasshopper canvas is a WinForms control: every canvas, document
                // and attribute access in this tool must run on the Rhino UI thread or
                // it throws a cross-thread violation.
                var plan = CanvasAccess.RunOnUiThread(() => PrepareSelection(mode, requested));
                if (plan.Error != null)
                {
                    output.CreateError(plan.Error);
                    return output;
                }

                // The review proposes exactly the objects whose selection flag changes:
                // for 'set' that is the allowed targets plus every currently selected
                // object that is not requested (it would be deselected).
                var reviewSession = CanvasChangeReviewService.CreateComponentStateSession(ToolName, plan.ProposedGuids, "Update selection state");
                var applyReview = reviewSession.Items.Count > 0 &&
                    await CanvasChangeReviewService.ReviewAsync(reviewSession, toolCall.InvocationContext, toolCall.CancellationToken).ConfigureAwait(false);
                var accepted = applyReview
                    ? CanvasChangeReviewService.GetAcceptedComponentGuids(reviewSession)
                    : (IReadOnlySet<Guid>)new HashSet<Guid>();
                var (changed, selected) = CanvasAccess.RunOnUiThread(() => ApplySelection(mode, plan.AllowedGuids, accepted));

                var result = new JObject
                {
                    ["mode"] = mode,
                    ["selected"] = JArray.FromObject(selected.Select(g => g.ToString())),
                    ["changed"] = JArray.FromObject(changed.Select(g => g.ToString())),
                };
                if (plan.SkippedProtectedGuids.Count > 0)
                {
                    result["skippedProtected"] = JArray.FromObject(plan.SkippedProtectedGuids.Select(g => g.ToString()));
                }

                if (plan.MissingGuids.Count > 0)
                {
                    result["missingGuids"] = JArray.FromObject(plan.MissingGuids.Select(g => g.ToString()));
                }

                var builder = AIBodyBuilder.Create();
                builder.AddToolResult(result, toolInfo.Id, toolInfo.Name);
                output.CreateSuccess(builder.Build(), toolCall);
                return output;
            }
            catch (Exception ex)
            {
                output.CreateError($"Error: {ex.Message}");
                return output;
            }
        }

        private static List<Guid> ParseGuids(JArray? tokens)
        {
            var guids = new List<Guid>();
            if (tokens == null)
            {
                return guids;
            }

            foreach (var token in tokens)
            {
                if (Guid.TryParse(token?.ToString(), out var guid))
                {
                    guids.Add(guid);
                }
            }

            return guids;
        }

        /// <summary>
        /// Resolves the requested GUIDs, applies protection filtering and computes the
        /// proposed change set for review. Must run on the Rhino UI thread.
        /// </summary>
        private static SelectionPlan PrepareSelection(string mode, List<Guid> requested)
        {
            var plan = new SelectionPlan();
            var canvas = Instances.ActiveCanvas;
            var document = canvas?.Document;
            if (canvas == null || document == null)
            {
                plan.Error = "No active Grasshopper canvas is available.";
                return plan;
            }

            // Split requested GUIDs into objects that exist and unknown GUIDs.
            var targets = new List<IGH_DocumentObject>();
            foreach (var guid in requested)
            {
                var obj = document.FindObject(guid, true);
                if (obj == null)
                {
                    plan.MissingGuids.Add(guid);
                }
                else
                {
                    targets.Add(obj);
                }
            }

            // Protected components are never added to the selection; deselecting
            // them is still allowed because it only reduces AI control.
            var addsToSelection = mode == "set" || mode == "add";
            var allowed = new List<IGH_DocumentObject>();
            if (addsToSelection)
            {
                var (allowedGuids, protectedGuids) = CanvasProtection.FilterProtectedGuids(targets.Select(t => t.InstanceGuid));
                var allowedSet = new HashSet<Guid>(allowedGuids);
                allowed.AddRange(targets.Where(t => allowedSet.Contains(t.InstanceGuid)));
                plan.SkippedProtectedGuids.AddRange(protectedGuids);
            }
            else
            {
                allowed.AddRange(targets);
            }

            plan.AllowedGuids.AddRange(allowed.Select(o => o.InstanceGuid));
            plan.ProposedGuids.AddRange(ProposedObjects(document, mode, allowed).Select(o => o.InstanceGuid));
            return plan;
        }

        private static List<IGH_DocumentObject> ProposedObjects(GH_Document document, string mode, List<IGH_DocumentObject> allowed)
        {
            switch (mode)
            {
                case "clear":
                    return document.SelectedObjects()
                        .Where(o => o?.Attributes?.Selected == true)
                        .ToList();
                case "remove":
                    return allowed
                        .Where(o => o.Attributes?.Selected == true)
                        .ToList();
                case "set":
                {
                    var allowedIds = new HashSet<Guid>(allowed.Select(o => o.InstanceGuid));
                    return allowed
                        .Concat(document.SelectedObjects()
                            .Where(o => o?.Attributes != null && !allowedIds.Contains(o.InstanceGuid)))
                        .ToList();
                }

                case "add":
                default:
                    return allowed
                        .Where(o => o.Attributes?.Selected != true)
                        .ToList();
            }
        }

        /// <summary>
        /// Applies the accepted selection changes, refreshes the canvas and returns the
        /// changed GUIDs plus the resulting selection. Must run on the Rhino UI thread.
        /// </summary>
        private static (List<Guid> Changed, List<Guid> Selected) ApplySelection(
            string mode,
            List<Guid> allowedGuids,
            IReadOnlySet<Guid> accepted)
        {
            var changed = new List<Guid>();
            var canvas = Instances.ActiveCanvas;
            var document = canvas?.Document;
            if (canvas == null || document == null)
            {
                return (changed, new List<Guid>());
            }

            if (accepted.Count > 0)
            {
                var allowed = allowedGuids
                    .Select(guid => document.FindObject(guid, true))
                    .Where(o => o != null)
                    .Cast<IGH_DocumentObject>()
                    .ToList();

                switch (mode)
                {
                    case "set":
                        document.DeselectAll();
                        changed.AddRange(Select(allowed.Where(o => accepted.Contains(o.InstanceGuid))));
                        break;
                    case "add":
                        changed.AddRange(Select(allowed.Where(o => accepted.Contains(o.InstanceGuid))));
                        break;
                    case "remove":
                    case "clear":
                        changed.AddRange(Deselect(document.SelectedObjects()
                            .Where(o => accepted.Contains(o.InstanceGuid))));
                        break;
                }

                if (changed.Count > 0)
                {
                    canvas.Refresh();
                }
            }

            var selected = document.SelectedObjects()
                .Where(o => o?.Attributes?.Selected == true)
                .Select(o => o.InstanceGuid)
                .ToList();
            return (changed, selected);
        }

        private static IEnumerable<Guid> Select(IEnumerable<IGH_DocumentObject> objects)
        {
            foreach (var obj in objects)
            {
                if (obj?.Attributes == null || obj.Attributes.Selected)
                {
                    continue;
                }

                obj.Attributes.Selected = true;
                yield return obj.InstanceGuid;
            }
        }

        private static IEnumerable<Guid> Deselect(IEnumerable<IGH_DocumentObject> objects)
        {
            foreach (var obj in objects)
            {
                if (obj?.Attributes == null || !obj.Attributes.Selected)
                {
                    continue;
                }

                obj.Attributes.Selected = false;
                yield return obj.InstanceGuid;
            }
        }

        /// <summary>
        /// Carries the UI-thread preparation results across the asynchronous review step.
        /// </summary>
        private sealed class SelectionPlan
        {
            public string? Error { get; set; }

            public List<Guid> AllowedGuids { get; } = new List<Guid>();

            public List<Guid> MissingGuids { get; } = new List<Guid>();

            public List<Guid> SkippedProtectedGuids { get; } = new List<Guid>();

            public List<Guid> ProposedGuids { get; } = new List<Guid>();
        }
    }
}
