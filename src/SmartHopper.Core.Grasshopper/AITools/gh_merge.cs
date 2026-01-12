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
 * along with this library; if not, write to the Free Software Foundation,
 * Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301, USA.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using GhJSON.Core;
using Newtonsoft.Json.Linq;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AICall.Core.Returns;
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.Infrastructure.AITools;

namespace SmartHopper.Core.Grasshopper.AITools
{
    /// <summary>
    /// Tool provider for merging two GhJSON documents into one.
    /// </summary>
    public class gh_merge : IAIToolProvider
    {
        /// <summary>
        /// Name of the AI tool provided by this class.
        /// </summary>
        private readonly string toolName = "gh_merge";

        /// <inheritdoc/>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: this.toolName,
                description: "Merge two GhJSON documents into one. The target document takes priority on conflicts (duplicate components by GUID are skipped from source). Connections and groups from both documents are combined with proper ID remapping and deduplication.",
                category: "Components",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""target"": { ""type"": ""string"", ""description"": ""Target GhJSON document string. This document takes priority on conflicts."" },
                        ""source"": { ""type"": ""string"", ""description"": ""Source GhJSON document string to merge into the target."" }
                    },
                    ""required"": [""target"", ""source""]
                }",
                execute: this.GhMergeToolAsync);
        }

        private async Task<AIReturn> GhMergeToolAsync(AIToolCall toolCall)
        {
            var output = new AIReturn()
            {
                Request = toolCall,
            };

            try
            {
                // Local tool: skip metrics validation (provider/model/finish_reason not required)
                toolCall.SkipMetricsValidation = true;

                // Extract parameters
                AIInteractionToolCall toolInfo = toolCall.GetToolCall();
                var args = toolInfo.Arguments ?? new JObject();
                var targetJson = args["target"]?.ToString() ?? string.Empty;
                var sourceJson = args["source"]?.ToString() ?? string.Empty;

                // Validate and parse target using GhJson facade
                if (!GhJson.IsValid(targetJson, out var targetAnalysis))
                {
                    output.CreateError($"Invalid target GhJSON: {targetAnalysis ?? "Invalid format"}");
                    return output;
                }

                var targetDoc = GhJson.Parse(targetJson);
                targetDoc = GhJson.Fix(targetDoc).Document;

                // Validate and parse source using GhJson facade
                if (!GhJson.IsValid(sourceJson, out var sourceAnalysis))
                {
                    output.CreateError($"Invalid source GhJSON: {sourceAnalysis ?? "Invalid format"}");
                    return output;
                }

                var sourceDoc = GhJson.Parse(sourceJson);
                sourceDoc = GhJson.Fix(sourceDoc).Document;

                // Merge documents using GhJson façade
                Debug.WriteLine("[gh_merge] Merging documents...");
                var mergeResult = GhJson.Merge(targetDoc, sourceDoc);

                // Serialize merged document back to JSON using GhJson facade
                var mergedJson = GhJson.Serialize(mergeResult.Document, new WriteOptions { Indented = false });

                Debug.WriteLine($"[gh_merge] Merge complete: +{mergeResult.ComponentsAdded} components, +{mergeResult.ConnectionsAdded} connections, +{mergeResult.GroupsAdded} groups");

                var toolResult = new JObject
                {
                    ["ghjson"] = mergedJson,
                    ["componentsAdded"] = mergeResult.ComponentsAdded,
                    ["componentsDuplicated"] = mergeResult.ComponentsDuplicated,
                    ["connectionsAdded"] = mergeResult.ConnectionsAdded,
                    ["connectionsDuplicated"] = mergeResult.ConnectionsDuplicated,
                    ["groupsAdded"] = mergeResult.GroupsAdded,
                    ["totalComponents"] = mergeResult.Document?.Components?.Count ?? 0,
                    ["totalConnections"] = mergeResult.Document?.Connections?.Count ?? 0,
                    ["totalGroups"] = mergeResult.Document?.Groups?.Count ?? 0,
                };

                var body = AIBodyBuilder.Create()
                    .AddToolResult(toolResult)
                    .Build();

                output.CreateSuccess(body, toolCall);
                return output;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[gh_merge] Error: {ex.Message}");
                output.CreateError(ex.Message);
                return output;
            }
        }
    }
}
