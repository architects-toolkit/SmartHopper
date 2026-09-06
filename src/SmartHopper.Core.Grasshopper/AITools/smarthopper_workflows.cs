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
using Newtonsoft.Json.Linq;
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.Infrastructure.AITools;
using SmartHopper.Infrastructure.Mcp;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AICall.Core.Returns;

namespace SmartHopper.Core.Grasshopper.AITools
{
    /// <summary>
    /// Returns canonical SmartHopper workflows from the shared embedded knowledge catalog.
    /// </summary>
    public class smarthopper_workflows : IAIToolProvider
    {
        private const string ToolName = "smarthopper_workflows";

        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: ToolName,
                description: "Returns canonical SmartHopper tool workflows. Pass `workflow` for detailed steps or omit it to list all workflows. Workflows include inspection, definition design, data-tree diagnosis, scripting, placement, performance review, validation, research, comparison, and patching.",
                category: "Instructions",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""workflow"": {
                            ""type"": ""string"",
                            ""description"": ""Optional workflow name to retrieve. Omit to list all workflows."",
                            ""enum"": [""inspect_canvas"", ""design_definition"", ""diagnose_data_tree"", ""edit_script"", ""create_script"", ""debug_script"", ""organize_canvas"", ""place_components"", ""review_performance"", ""validate_change"", ""search_knowledge"", ""compare_definitions"", ""apply_patch""]
                        }
                    }
                }",
                execute: this.ExecuteAsync,
                mutatesCanvas: false,
                tags: new[] { "instructions", "workflow", "read-only" },
                outputSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""workflows"": {
                            ""type"": ""array"",
                            ""items"": {
                                ""type"": ""object"",
                                ""properties"": {
                                    ""name"": { ""type"": ""string"" },
                                    ""description"": { ""type"": ""string"" },
                                    ""steps"": {
                                        ""type"": ""array"",
                                        ""items"": { ""type"": ""string"" }
                                    }
                                }
                            }
                        }
                    }
                }");
        }

        private Task<AIReturn> ExecuteAsync(AIToolCall toolCall)
        {
            var output = new AIReturn()
            {
                Request = toolCall,
            };

            try
            {
                toolCall.SkipMetricsValidation = true;

                var toolInfo = toolCall.GetToolCall();
                var args = toolInfo.GetArgumentsOrEmpty();
                var workflow = args["workflow"]?.ToString() ?? string.Empty;
                var toolResult = new JObject
                {
                    ["workflows"] = new JArray(this.GetWorkflows(workflow)),
                };

                var toolBody = AIBodyBuilder.Create()
                    .AddToolResult(toolResult, id: toolInfo?.Id, name: ToolName)
                    .Build();

                output.CreateSuccess(toolBody);
                return Task.FromResult(output);
            }
            catch (Exception ex)
            {
                output.CreateError($"Error: {ex.Message}");
                return Task.FromResult(output);
            }
        }

        private List<JObject> GetWorkflows(string filter)
        {
            return AgentKnowledgeCatalog.GetWorkflows(filter)
                .Select(workflow => new JObject
                {
                    ["name"] = workflow.Name,
                    ["description"] = workflow.Description,
                    ["steps"] = new JArray(workflow.Steps),
                })
                .ToList();
        }
    }
}
