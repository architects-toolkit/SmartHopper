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
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.Infrastructure.AITools;
using SmartHopper.Infrastructure.Mcp;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AICall.Core.Returns;

namespace SmartHopper.Core.Grasshopper.AITools
{
    /// <summary>
    /// Provides focused embedded instruction bundles so the system prompt can remain short.
    /// </summary>
    public class smarthopper_readme : IAIToolProvider
    {
        private const string ToolName = "smarthopper_readme";

        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: ToolName,
                description: "Returns focused Grasshopper and SmartHopper guidance. Pass `topic` with one of: foundations, data-trees, definition-design, geometry, debugging, performance, canvas, selected, errors, locks, visibility, discovery, scripting, python, csharp, vb, knowledge, providers, ghjson, sources, mcneel-forum, ladybug-forum, discourse-forum, research, web.",
                category: "Instructions",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""topic"": {
                            ""type"": ""string"",
                            ""description"": ""Which focused instruction bundle to return."",
                            ""enum"": [""foundations"", ""data-trees"", ""definition-design"", ""geometry"", ""debugging"", ""performance"", ""canvas"", ""selected"", ""errors"", ""locks"", ""visibility"", ""discovery"", ""scripting"", ""python"", ""csharp"", ""vb"", ""knowledge"", ""providers"", ""ghjson"", ""sources"", ""mcneel-forum"", ""ladybug-forum"", ""discourse-forum"", ""research"", ""web""]
                        }
                    },
                    ""required"": [""topic""]
                }",
                execute: this.ExecuteAsync,
                mutatesCanvas: false,
                tags: new[] { "instructions", "readme", "read-only" },
                outputSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""topic"": { ""type"": ""string"" },
                        ""instructions"": { ""type"": ""string"", ""description"": ""Markdown-formatted operational guidance for the agent."" }
                    },
                    ""required"": [""topic"", ""instructions""]
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
                var topic = args["topic"]?.ToString() ?? string.Empty;
                var toolResult = new JObject
                {
                    ["topic"] = topic,
                    ["instructions"] = AgentKnowledgeCatalog.GetInstructions(topic),
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
    }
}
