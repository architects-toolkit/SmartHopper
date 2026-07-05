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
using Newtonsoft.Json.Linq;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AICall.Core.Returns;
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.Infrastructure.AITools;

namespace SmartHopper.Core.Grasshopper.AITools
{
    /// <summary>
    /// Tool provider for structurally validating a `.ghpatch` document.
    /// </summary>
    public class gh_patch_validate : IAIToolProvider
    {
        /// <summary>
        /// Name of the AI tool provided by this class.
        /// </summary>
        private readonly string toolName = "gh_patch_validate";

        /// <inheritdoc/>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: this.toolName,
                description: "Structurally validate a `.ghpatch` document. Checks the patch kind, that components/groups in remove/modify ops carry at least one identity field, that new components/groups in add ops do NOT specify instanceGuid, and that connections have valid endpoints.",
                category: "Components",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""patch"": { ""type"": ""string"", ""description"": ""`.ghpatch` patch document string to validate."" }
                    },
                    ""required"": [""patch""]
                }",
                execute: this.GhPatchValidateToolAsync,
                mutatesCanvas: false,
                tags: new[] { "canvas", "components", "patch", "validation", "read-only", "ghjson" },
                outputSchema: @"{ ""type"": ""object"", ""properties"": { ""isValid"": { ""type"": ""boolean"" }, ""errors"": { ""type"": ""array"" }, ""warnings"": { ""type"": ""array"" } } }",
                annotations: new AIToolAnnotations(readOnlyHint: true));
        }

        private async Task<AIReturn> GhPatchValidateToolAsync(AIToolCall toolCall)
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
                var patchJson = args["patch"]?.ToString() ?? string.Empty;

                Debug.WriteLine("[gh_patch_validate] Validating patch...");
                var validationResult = GhJson.ValidatePatch(patchJson);

                var errors = new JArray();
                foreach (var error in validationResult.Errors)
                {
                    errors.Add(new JObject
                    {
                        ["path"] = error.Path,
                        ["message"] = error.Message,
                    });
                }

                var warnings = new JArray();
                foreach (var warning in validationResult.Warnings)
                {
                    warnings.Add(new JObject
                    {
                        ["path"] = warning.Path,
                        ["message"] = warning.Message,
                    });
                }

                var toolResult = new JObject
                {
                    ["isValid"] = validationResult.IsValid,
                    ["errors"] = errors,
                    ["warnings"] = warnings,
                };

                var body = AIBodyBuilder.Create()
                    .AddToolResult(toolResult)
                    .Build();

                output.CreateSuccess(body, toolCall);
                return output;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[gh_patch_validate] Error: {ex.Message}");
                output.CreateError(ex.Message);
                return output;
            }
        }
    }
}
