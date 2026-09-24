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

                var canvas = Instances.ActiveCanvas;
                var document = canvas?.Document;
                if (canvas == null || document == null)
                {
                    output.CreateError("No active Grasshopper canvas is available.");
                    return output;
                }

                // Split requested GUIDs into objects that exist and unknown GUIDs.
                var targets = new List<IGH_DocumentObject>();
                var missing = new List<Guid>();
                foreach (var guid in requested)
                {
                    var obj = document.FindObject(guid, true);
                    if (obj == null)
                    {
                        missing.Add(guid);
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
                var skippedProtected = new List<Guid>();
                if (addsToSelection)
                {
                    var (allowedGuids, protectedGuids) = CanvasProtection.FilterProtectedGuids(targets.Select(t => t.InstanceGuid));
                    var allowedSet = new HashSet<Guid>(allowedGuids);
                    allowed.AddRange(targets.Where(t => allowedSet.Contains(t.InstanceGuid)));
                    skippedProtected.AddRange(protectedGuids);
                }
                else
                {
                    allowed.AddRange(targets);
                }

                // The review proposes exactly the objects whose selection flag changes:
                // for 'set' that is the allowed targets plus every currently selected
                // object that is not requested (it would be deselected).
                var proposed = ProposedObjects(document, mode, allowed);
                var reviewSession = CanvasChangeReviewService.CreateComponentStateSession(ToolName, proposed.Select(o => o.InstanceGuid), "Update selection state");
                var applyReview = reviewSession.Items.Count > 0 &&
                    await CanvasChangeReviewService.ReviewAsync(reviewSession, toolCall.InvocationContext, toolCall.CancellationToken).ConfigureAwait(false);
                var changed = applyReview
                    ? ApplySelection(document, mode, allowed, CanvasChangeReviewService.GetAcceptedComponentGuids(reviewSession))
                    : new List<Guid>();
                if (changed.Count > 0)
                {
                    canvas.Refresh();
                }

                var result = new JObject
                {
                    ["mode"] = mode,
                    ["selected"] = JArray.FromObject(document.SelectedObjects()
                        .Where(o => o?.Attributes?.Selected == true)
                        .Select(o => o.InstanceGuid.ToString())),
                    ["changed"] = JArray.FromObject(changed.Select(g => g.ToString())),
                };
                if (skippedProtected.Count > 0)
                {
                    result["skippedProtected"] = JArray.FromObject(skippedProtected.Select(g => g.ToString()));
                }

                if (missing.Count > 0)
                {
                    result["missingGuids"] = JArray.FromObject(missing.Select(g => g.ToString()));
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

        private static List<Guid> ApplySelection(
            GH_Document document,
            string mode,
            List<IGH_DocumentObject> allowed,
            IReadOnlySet<Guid> accepted)
        {
            var changed = new List<Guid>();
            if (accepted.Count == 0)
            {
                return changed;
            }

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

            return changed;
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
    }
}
