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
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AICall.Core.Returns;
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.Infrastructure.AITools;

namespace SmartHopper.Core.Grasshopper.AITools
{
    /// <summary>
    /// Returns detailed metadata for a SmartHopper tool, including its schema, tags, annotations,
    /// and relationship hints. Helps MCP clients understand how to use a tool without reading code.
    /// </summary>
    public class smarthopper_tool_help : IAIToolProvider
    {
        private const string ToolName = "smarthopper_tool_help";

        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: ToolName,
                description: "Returns metadata, usage guidance, and relationship hints for a SmartHopper tool. Pass `tool_name` to look up a specific tool. Use this when you need to understand a tool's inputs, outputs, or how it chains with other tools.",
                category: "Instructions",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""tool_name"": {
                            ""type"": ""string"",
                            ""description"": ""Name of the SmartHopper tool to look up (e.g., gh_get, script_edit, text2json).""
                        }
                    },
                    ""required"": [""tool_name""]
                }",
                execute: this.ExecuteAsync,
                mutatesCanvas: false,
                tags: new[] { "instructions", "meta", "read-only" },
                outputSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""tool_name"": { ""type"": ""string"" },
                        ""found"": { ""type"": ""boolean"" },
                        ""description"": { ""type"": ""string"" },
                        ""category"": { ""type"": ""string"" },
                        ""tags"": { ""type"": ""array"", ""items"": { ""type"": ""string"" } },
                        ""mutates_canvas"": { ""type"": ""boolean"" },
                        ""annotations"": {
                            ""type"": ""object"",
                            ""properties"": {
                                ""readOnlyHint"": { ""type"": ""boolean"" },
                                ""destructiveHint"": { ""type"": ""boolean"" },
                                ""idempotentHint"": { ""type"": ""boolean"" },
                                ""openWorldHint"": { ""type"": ""boolean"" },
                                ""title"": { ""type"": ""string"" }
                            }
                        },
                        ""input_schema"": { ""type"": ""object"" },
                        ""output_schema"": { ""type"": ""object"" },
                        ""similar_tools"": {
                            ""type"": ""array"",
                            ""description"": ""Tools related to the requested tool. When the tool is found, this contains all tools in the same category; otherwise it contains the full catalog for discovery."",
                            ""items"": {
                                ""type"": ""object"",
                                ""properties"": {
                                    ""name"": { ""type"": ""string"" },
                                    ""description"": { ""type"": ""string"" },
                                    ""category"": { ""type"": ""string"" },
                                    ""tags"": { ""type"": ""array"", ""items"": { ""type"": ""string"" } }
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
                var requestedName = args["tool_name"]?.ToString() ?? string.Empty;

                var tools = AIToolManager.GetTools();
                var found = tools.TryGetValue(requestedName, out var targetTool);

                var toolCatalog = tools.Values
                    .OrderBy(t => t.Name)
                    .Select(t => new JObject
                    {
                        ["name"] = t.Name,
                        ["description"] = t.RichDescription,
                        ["category"] = t.Category,
                        ["tags"] = new JArray(t.Tags),
                    })
                    .ToList();

                var similarTools = (found && targetTool != null)
                    ? toolCatalog.Where(t => t["category"]?.ToString() == targetTool.Category)
                    : toolCatalog;

                JObject result;
                if (found && targetTool != null)
                {
                    result = new JObject
                    {
                        ["tool_name"] = requestedName,
                        ["found"] = true,
                        ["description"] = targetTool.RichDescription,
                        ["category"] = targetTool.Category,
                        ["tags"] = new JArray(targetTool.Tags),
                        ["mutates_canvas"] = targetTool.MutatesCanvas,
                        ["annotations"] = new JObject
                        {
                            ["readOnlyHint"] = targetTool.Annotations.ReadOnlyHint,
                            ["destructiveHint"] = targetTool.Annotations.DestructiveHint,
                            ["idempotentHint"] = targetTool.Annotations.IdempotentHint,
                            ["openWorldHint"] = targetTool.Annotations.OpenWorldHint,
                            ["title"] = targetTool.Annotations.Title,
                        },
                        ["input_schema"] = JToken.Parse(targetTool.ParametersSchema),
                        ["output_schema"] = JToken.Parse(targetTool.OutputSchema),
                        ["similar_tools"] = new JArray(similarTools),
                    };
                }
                else
                {
                    result = new JObject
                    {
                        ["tool_name"] = requestedName,
                        ["found"] = false,
                        ["description"] = $"Tool '{requestedName}' is not registered.",
                        ["similar_tools"] = new JArray(similarTools),
                    };
                }

                var toolBody = AIBodyBuilder.Create()
                    .AddToolResult(result, id: toolInfo?.Id, name: ToolName)
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
    }
}
