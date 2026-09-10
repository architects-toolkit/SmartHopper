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
using GhJSON.Grasshopper;
using GhJSON.Grasshopper.DeleteOperations;
using Grasshopper;
using Grasshopper.Kernel;
using Newtonsoft.Json.Linq;
using SmartHopper.Core.Grasshopper.Utils.Canvas;
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.Infrastructure.AITools;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AICall.Core.Returns;
using SmartHopper.ProviderSdk.Diagnostics;

namespace SmartHopper.Core.Grasshopper.AITools
{
    /// <summary>
    /// Tool provider for clearing all components from the Grasshopper canvas.
    /// Supports keeping locked components and always respects <see cref="CanvasProtection"/>.
    /// </summary>
    public class gh_clear : IAIToolProvider
    {
        /// <summary>
        /// Name of the AI tool provided by this class.
        /// </summary>
        private readonly string toolName = "gh_clear";

        /// <inheritdoc/>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AIMutatingTool(
                name: this.toolName,
                description: "Stage removal of all components from the Grasshopper canvas and let the user visually review and select removals before applying them. Optionally keep locked components. Protected components (and their direct neighbors) are always preserved. Supports undo (Ctrl+Z).",
                category: "Components",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""keepLocked"": {
                            ""type"": ""boolean"",
                            ""description"": ""If true, locked (disabled) components will not be deleted. Default is false (delete everything except protected components)."",
                            ""default"": false
                        }
                    }
                }",
                execute: this.ClearCanvasAsync,
                tags: new[] { "canvas", "components", "mutating", "delete", "destructive" },
                outputSchema: @"{ ""type"": ""object"", ""properties"": { ""deletedCount"": { ""type"": ""integer"" }, ""deleted"": { ""type"": ""array"", ""items"": { ""type"": ""string"" } }, ""rejectedCount"": { ""type"": ""integer"" }, ""skippedLockedCount"": { ""type"": ""integer"" }, ""protectedGuids"": { ""type"": ""array"", ""items"": { ""type"": ""string"" } }, ""message"": { ""type"": ""string"" } } }",
                annotations: new AIToolAnnotations(destructiveHint: true));
        }

        private async Task<AIReturn> ClearCanvasAsync(AIToolCall toolCall)
        {
            var output = new AIReturn { Request = toolCall };

            try
            {
                toolCall.SkipMetricsValidation = true;

                AIInteractionToolCall toolInfo = toolCall.GetToolCall();
                var args = toolInfo.GetArgumentsOrEmpty();
                var keepLocked = args["keepLocked"]?.ToObject<bool>() ?? false;

                var doc = CanvasAccess.GetCurrentCanvas();
                if (doc == null)
                {
                    output.CreateError("No active Grasshopper document found.");
                    return output;
                }

                // Collect all canvas object GUIDs
                var allObjects = doc.Objects.ToList();
                var allGuids = allObjects.Select(o => o.InstanceGuid).ToList();

                // Filter out locked objects if keepLocked is true
                List<Guid> candidateGuids;
                int skippedLockedCount = 0;

                if (keepLocked)
                {
                    var lockedGuids = new HashSet<Guid>(
                        allObjects.Where(o => o is IGH_Component c && c.Locked)
                            .Select(o => o.InstanceGuid));
                    skippedLockedCount = lockedGuids.Count;
                    candidateGuids = allGuids.Where(g => !lockedGuids.Contains(g)).ToList();
                }
                else
                {
                    candidateGuids = allGuids;
                }

                // Filter out CanvasProtection protected GUIDs
                var (allowedGuids, protectedGuids) = CanvasProtection.FilterProtectedGuids(candidateGuids);

                if (protectedGuids.Count > 0)
                {
                    output.AddRuntimeMessage(
                        SHRuntimeMessageSeverity.Warning,
                        SHRuntimeMessageOrigin.Tool,
                        CanvasProtection.FormatProtectionMessage(protectedGuids));
                }

                if (allowedGuids.Count == 0)
                {
                    var toolResult = new JObject
                    {
                        ["deletedCount"] = 0,
                        ["deleted"] = new JArray(),
                        ["skippedLockedCount"] = skippedLockedCount,
                        ["protectedGuids"] = JArray.FromObject(protectedGuids.Select(g => g.ToString())),
                        ["message"] = "Canvas is already empty or all components are locked or protected.",
                    };

                    var body = AIBodyBuilder.Create()
                        .AddToolResult(toolResult)
                        .Build();

                    output.CreateSuccess(body, toolCall);
                    return output;
                }

                var reviewSession = CanvasChangeReviewService.CreateRemovalSession(this.toolName, allowedGuids);
                var applyReview = reviewSession.Items.Count > 0 &&
                    await CanvasChangeReviewService.ReviewAsync(reviewSession, toolCall.InvocationContext, toolCall.CancellationToken).ConfigureAwait(false);
                IReadOnlyList<Guid> acceptedGuids = applyReview
                    ? CanvasChangeReviewService.GetAcceptedRemovalGuids(reviewSession)
                    : Array.Empty<Guid>();
                var rejectedCount = reviewSession.Items.Count - acceptedGuids.Count;

                // Delete via GhJsonGrasshopper (handles UI thread + undo)
                var deleteOptions = new DeleteOptions { Redraw = true };
                var deleteResult = GhJsonGrasshopper.Delete(acceptedGuids, deleteOptions);

                var result = new JObject
                {
                    ["deletedCount"] = deleteResult.DeletedCount,
                    ["deleted"] = JArray.FromObject(deleteResult.Deleted.Select(g => g.ToString())),
                    ["rejectedCount"] = rejectedCount,
                    ["skippedLockedCount"] = skippedLockedCount,
                    ["protectedGuids"] = JArray.FromObject(protectedGuids.Select(g => g.ToString())),
                };

                if (deleteResult.Failed.Count > 0)
                {
                    result["failedCount"] = deleteResult.FailedCount;
                    result["failed"] = JArray.FromObject(deleteResult.Failed.Select(f => new JObject
                    {
                        ["guid"] = f.Guid.ToString(),
                        ["error"] = f.Error,
                    }));
                }

                result["message"] = deleteResult.DeletedCount > 0
                    ? $"Deleted {deleteResult.DeletedCount} component(s); the user rejected {rejectedCount} staged removal(s)."
                    : rejectedCount > 0
                        ? "The user rejected all staged removals."
                        : "No components were deleted.";

                var outBody = AIBodyBuilder.Create()
                    .AddToolResult(result)
                    .Build();

                output.CreateSuccess(outBody, toolCall);
                return output;
            }
            catch (Exception ex)
            {
                output.CreateError($"Error clearing canvas: {ex.Message}");
                return output;
            }
        }
    }
}
