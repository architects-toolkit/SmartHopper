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
    /// Tool provider for diffing two GhJSON documents and producing a `.ghpatch` document.
    /// </summary>
    public class gh_diff : IAIToolProvider
    {
        /// <summary>
        /// Name of the AI tool provided by this class.
        /// </summary>
        private readonly string toolName = "gh_diff";

        /// <inheritdoc/>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: this.toolName,
                description: "Diff two GhJSON documents and produce a structured `.ghpatch` document describing the differences (added/removed/modified components, connections, groups, and metadata). Components are matched by instanceGuid, then id, then structural fingerprint (componentGuid + name + optional pivot). Connections are matched by their endpoints (paramName preferred, paramIndex fallback). By default, runtime messages, metadata counters and metadata timestamps are ignored.",
                category: "Components",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""left"": { ""type"": ""string"", ""description"": ""Left (base) GhJSON document string."" },
                        ""right"": { ""type"": ""string"", ""description"": ""Right (target) GhJSON document string."" },
                        ""ignoreRuntimeMessages"": { ""type"": ""boolean"", ""description"": ""Ignore runtime messages (errors/warnings/remarks) when computing the diff. Defaults to true."" },
                        ""ignoreMetadataCounters"": { ""type"": ""boolean"", ""description"": ""Ignore metadata.componentCount/connectionCount/groupCount. Defaults to true."" },
                        ""ignoreMetadataTimestamps"": { ""type"": ""boolean"", ""description"": ""Ignore metadata.created/modified timestamps. Defaults to true."" },
                        ""ignorePivots"": { ""type"": ""boolean"", ""description"": ""Ignore canvas positions when computing component diffs. Defaults to false."" },
                        ""includeBaseChecksum"": { ""type"": ""boolean"", ""description"": ""Emit a sha256 checksum of the left document in patch.base. Defaults to true."" }
                    },
                    ""required"": [""left"", ""right""]
                }",
                execute: this.GhDiffToolAsync,
                mutatesCanvas: false,
                tags: new[] { "canvas", "components", "diff", "patch", "read-only", "ghjson" },
                outputSchema: @"{ ""type"": ""object"", ""properties"": { ""ghpatch"": { ""type"": ""string"", ""description"": ""Serialized `.ghpatch` document describing differences."" }, ""hasChanges"": { ""type"": ""boolean"" }, ""componentOpCount"": { ""type"": ""integer"" }, ""connectionOpCount"": { ""type"": ""integer"" }, ""groupOpCount"": { ""type"": ""integer"" } } }",
                annotations: new AIToolAnnotations(readOnlyHint: true));
        }

        private async Task<AIReturn> GhDiffToolAsync(AIToolCall toolCall)
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
                var leftJson = args["left"]?.ToString() ?? string.Empty;
                var rightJson = args["right"]?.ToString() ?? string.Empty;

                var options = new DiffOptions
                {
                    IgnoreRuntimeMessages = args["ignoreRuntimeMessages"]?.ToObject<bool>() ?? true,
                    IgnoreMetadataCounters = args["ignoreMetadataCounters"]?.ToObject<bool>() ?? true,
                    IgnoreMetadataTimestamps = args["ignoreMetadataTimestamps"]?.ToObject<bool>() ?? true,
                    IgnorePivots = args["ignorePivots"]?.ToObject<bool>() ?? false,
                    IncludeBaseChecksum = args["includeBaseChecksum"]?.ToObject<bool>() ?? true,
                };

                if (!GhJson.IsValid(leftJson, out var leftAnalysis))
                {
                    output.CreateError($"Invalid left GhJSON: {leftAnalysis ?? "Invalid format"}");
                    return output;
                }

                if (!GhJson.IsValid(rightJson, out var rightAnalysis))
                {
                    output.CreateError($"Invalid right GhJSON: {rightAnalysis ?? "Invalid format"}");
                    return output;
                }

                var leftDoc = GhJson.FromJson(leftJson);
                var rightDoc = GhJson.FromJson(rightJson);

                Debug.WriteLine("[gh_diff] Diffing documents...");
                var diffResult = GhJson.Diff(leftDoc, rightDoc, options);

                var patchJson = GhJson.PatchToJson(diffResult.Patch, new WriteOptions { Indented = false });

                Debug.WriteLine($"[gh_diff] Diff complete: {diffResult.ComponentOpCount} component ops, {diffResult.ConnectionOpCount} connection ops, {diffResult.GroupOpCount} group ops");

                var toolResult = new JObject
                {
                    ["ghpatch"] = patchJson,
                    ["hasChanges"] = diffResult.HasChanges,
                    ["componentOpCount"] = diffResult.ComponentOpCount,
                    ["connectionOpCount"] = diffResult.ConnectionOpCount,
                    ["groupOpCount"] = diffResult.GroupOpCount,
                };

                var body = AIBodyBuilder.Create()
                    .AddToolResult(toolResult)
                    .Build();

                output.CreateSuccess(body, toolCall);
                return output;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[gh_diff] Error: {ex.Message}");
                output.CreateError(ex.Message);
                return output;
            }
        }
    }
}
