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
using System.Threading.Tasks;
using GhJSON.Core;
using GhJSON.Core.DiffOperations;
using GhJSON.Core.Serialization;
using Newtonsoft.Json.Linq;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AICall.Core.Returns;
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.Infrastructure.AITools;

namespace SmartHopper.Core.Grasshopper.AITools
{
    /// <summary>
    /// Tool provider for applying a `.ghpatch` patch document to a base GhJSON document.
    /// </summary>
    public class gh_patch_apply : IAIToolProvider
    {
        /// <summary>
        /// Name of the AI tool provided by this class.
        /// </summary>
        private readonly string toolName = "gh_patch_apply";

        /// <inheritdoc/>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: this.toolName,
                description: "Apply a `.ghpatch` patch document to a base GhJSON document. Components are matched by instanceGuid, then id, then structural fingerprint. New components and groups in `components.add` / `groups.add` must NOT include `instanceGuid` (it is generated on placement). By default, the patch's recorded base checksum is verified against the supplied base document — on mismatch, the apply is refused (no partial application). Conflicts (match not found, connection already present, dangling group members, ...) are recorded in the result.",
                category: "Components",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""base"": { ""type"": ""string"", ""description"": ""Base GhJSON document string to apply the patch to."" },
                        ""patch"": { ""type"": ""string"", ""description"": ""`.ghpatch` patch document string."" },
                        ""verifyBase"": { ""type"": ""boolean"", ""description"": ""If true, refuse to apply when the patch's recorded base checksum does not match the base document. Defaults to true."" },
                        ""continueOnConflict"": { ""type"": ""boolean"", ""description"": ""If true, collect all conflicts and keep applying remaining operations. If false, stop at the first conflict. Defaults to true."" },
                        ""renumberCollidingAddedIds"": { ""type"": ""boolean"", ""description"": ""If true, renumber colliding component ids on add (similar to gh_merge). Defaults to true."" }
                    },
                    ""required"": [""base"", ""patch""]
                }",
                execute: this.GhPatchApplyToolAsync,
                mutatesCanvas: false,
                tags: new[] { "canvas", "components", "patch", "read-only", "ghjson" },
                outputSchema: @"{ ""type"": ""object"", ""properties"": { ""ghjson"": { ""type"": ""string"", ""description"": ""Resulting GhJSON after applying the patch."" }, ""success"": { ""type"": ""boolean"" }, ""hasConflicts"": { ""type"": ""boolean"" }, ""conflicts"": { ""type"": ""array"" }, ""componentsAdded"": { ""type"": ""integer"" }, ""componentsRemoved"": { ""type"": ""integer"" }, ""componentsModified"": { ""type"": ""integer"" }, ""connectionsAdded"": { ""type"": ""integer"" }, ""connectionsRemoved"": { ""type"": ""integer"" }, ""groupsAdded"": { ""type"": ""integer"" }, ""groupsRemoved"": { ""type"": ""integer"" }, ""groupsModified"": { ""type"": ""integer"" } } }",
                annotations: new AIToolAnnotations(readOnlyHint: true));
        }

        private async Task<AIReturn> GhPatchApplyToolAsync(AIToolCall toolCall)
        {
            var output = new AIReturn()
            {
                Request = toolCall,
            };

            try
            {
                // Local tool: skip metrics validation (provider/model/finish_reason not required)
                toolCall.SkipMetricsValidation = true;

                AIInteractionToolCall toolInfo = toolCall.GetToolCall();
                var args = toolInfo.GetArgumentsOrEmpty();
                var baseJson = args["base"]?.ToString() ?? string.Empty;
                var patchJson = args["patch"]?.ToString() ?? string.Empty;

                var options = new ApplyPatchOptions
                {
                    VerifyBase = args["verifyBase"]?.ToObject<bool>() ?? true,
                    ContinueOnConflict = args["continueOnConflict"]?.ToObject<bool>() ?? true,
                    RenumberCollidingAddedIds = args["renumberCollidingAddedIds"]?.ToObject<bool>() ?? true,
                };

                if (!GhJson.IsValid(baseJson, out var baseAnalysis))
                {
                    output.CreateError($"Invalid base GhJSON: {baseAnalysis ?? "Invalid format"}");
                    return output;
                }

                var patchValidation = GhJson.ValidatePatch(patchJson);
                if (!patchValidation.IsValid)
                {
                    var errors = string.Join("; ", patchValidation.Errors.Select(e => e.ToString()));
                    output.CreateError($"Invalid patch: {errors}");
                    return output;
                }

                Debug.WriteLine("[gh_patch_apply] Applying patch...");
                var applyResult = GhJson.ApplyPatch(baseJson, patchJson, options);

                var resultJson = applyResult.Document != null
                    ? GhJson.ToJson(applyResult.Document, new WriteOptions { Indented = false })
                    : string.Empty;

                var conflicts = new JArray();
                foreach (var c in applyResult.Conflicts)
                {
                    conflicts.Add(new JObject
                    {
                        ["kind"] = c.Kind.ToString(),
                        ["message"] = c.Message,
                        ["path"] = c.Path,
                    });
                }

                Debug.WriteLine($"[gh_patch_apply] Apply complete: success={applyResult.Success}, conflicts={applyResult.Conflicts.Count}, +{applyResult.ComponentsAdded}/-{applyResult.ComponentsRemoved}/~{applyResult.ComponentsModified} components");

                var toolResult = new JObject
                {
                    ["ghjson"] = resultJson,
                    ["success"] = applyResult.Success,
                    ["hasConflicts"] = applyResult.HasConflicts,
                    ["conflicts"] = conflicts,
                    ["componentsAdded"] = applyResult.ComponentsAdded,
                    ["componentsRemoved"] = applyResult.ComponentsRemoved,
                    ["componentsModified"] = applyResult.ComponentsModified,
                    ["connectionsAdded"] = applyResult.ConnectionsAdded,
                    ["connectionsRemoved"] = applyResult.ConnectionsRemoved,
                    ["groupsAdded"] = applyResult.GroupsAdded,
                    ["groupsRemoved"] = applyResult.GroupsRemoved,
                    ["groupsModified"] = applyResult.GroupsModified,
                };

                var body = AIBodyBuilder.Create()
                    .AddToolResult(toolResult)
                    .Build();

                output.CreateSuccess(body, toolCall);
                return output;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[gh_patch_apply] Error: {ex.Message}");
                output.CreateError(ex.Message);
                return output;
            }
        }
    }
}
