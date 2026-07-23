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
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SmartHopper.Core.Grasshopper.GhJsonSpec;
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.Infrastructure.AITools;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AICall.Core.Returns;

namespace SmartHopper.Core.Grasshopper.AITools
{
    /// <summary>
    /// Returns reference documentation for the GhJSON and GhPatch formats from the embedded
    /// specification snapshot (or the online ghjson-spec repository if enabled).
    /// </summary>
    public class smarthopper_ghjson_reference : IAIToolProvider
    {
        private const string ToolName = "smarthopper_ghjson_reference";

        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: ToolName,
                description: "Returns GhJSON and GhPatch format reference documentation. Pass `topic` to retrieve the full specification or a focused section. Use this whenever you need to generate, edit, or validate GhJSON/GhPatch documents instead of relying on internalized format knowledge.",
                category: "Instructions",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""topic"": {
                            ""type"": ""string"",
                            ""description"": ""Which reference topic to return."",
                            ""enum"": [""overview"", ""specification"", ""ghpatch"", ""document_structure"", ""components"", ""connections"", ""groups"", ""data_types"", ""component_specific_formats"", ""validation"", ""examples""]
                        }
                    },
                    ""required"": [""topic""]
                }",
                execute: this.ExecuteAsync,
                mutatesCanvas: false,
                tags: new[] { "instructions", "ghjson", "reference", "read-only" },
                outputSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""topic"": { ""type"": ""string"" },
                        ""instructions"": { ""type"": ""string"", ""description"": ""Markdown-formatted reference documentation for the requested topic."" }
                    },
                    ""required"": [""topic"", ""instructions""]
                }");
        }

        private async Task<AIReturn> ExecuteAsync(AIToolCall toolCall)
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
                var topic = args["topic"]?.ToString() ?? string.Empty;

                var instructions = await GhJsonSpecLoader.LoadTopicAsync(topic).ConfigureAwait(false);

                var toolResult = new JObject
                {
                    ["topic"] = topic,
                    ["instructions"] = instructions,
                };

                var toolBody = AIBodyBuilder.Create()
                    .AddToolResult(toolResult, id: toolInfo?.Id, name: ToolName)
                    .Build();

                output.CreateSuccess(toolBody);
                return output;
            }
            catch (Exception ex)
            {
                output.CreateError($"Error: {ex.Message}");
                return output;
            }
        }
    }
}