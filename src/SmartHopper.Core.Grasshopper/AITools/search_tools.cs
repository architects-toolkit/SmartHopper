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
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AICall.Core.Returns;
using SmartHopper.ProviderSdk.AIModels;
using SmartHopper.ProviderSdk.Utils;

namespace SmartHopper.Core.Grasshopper.AITools
{
    /// <summary>
    /// Progressive tool discovery: when the eligible tool set outgrows the
    /// inline threshold, providers send only the core tools plus this one —
    /// the model searches the rest by intent and calls them by name, since
    /// execution is name-dispatched and does not require the schema to have
    /// been inline. Results stay inside the request's eligible scope: the
    /// originating ToolFilter rides on the invocation context so narrowed
    /// requests (component-scoped includes, explicit excludes) are honored.
    /// </summary>
    public class search_tools : IAIToolProvider
    {
        /// <inheritdoc/>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: ProgressiveToolLoading.SearchToolName,
                description: "Discover additional tools not already in your toolset: component control, script parameters, data tools, document management, vision, provider info and more. Describe what you want to do (e.g. \"lock selected components\", \"list data\", \"save document\") — matching tool definitions are returned, then call them by name.",
                category: ProgressiveToolLoading.SearchToolCategory,
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""query"": { ""type"": ""string"", ""description"": ""What you want to do, in a few words"" }
                    },
                    ""required"": [""query""]
                }",
                execute: this.SearchAsync,
                requiredCapabilities: AICapability.None,
                mutatesCanvas: false,
                enabled: true,
                tags: new[] { "meta", "discovery", "read-only" },
                outputSchema: @"{ ""type"": ""object"", ""properties"": { ""tools"": { ""type"": ""array"" } } }",
                annotations: new AIToolAnnotations(readOnlyHint: true, destructiveHint: false),
                activityLabel: "searching available tools",
                resultLabel: "searched available tools");
        }

        /// <summary>
        /// Scores the request's eligible tool catalogue against the query and
        /// returns the top matches with their full schemas.
        /// </summary>
        private Task<AIReturn> SearchAsync(AIToolCall toolCall)
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
                var query = args["query"]?.ToString()?.Trim().ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(query))
                {
                    output.CreateError("The 'query' parameter is required.");
                    return Task.FromResult(output);
                }

                // Rebuild the catalogue the request was offered: the original
                // ToolFilter rides on the invocation context, so the same
                // include/exclude scope the provider resolved applies here.
                var scopeFilter = toolCall.InvocationContext?.ToolScopeFilter;
                var scope = Filtering.Parse(scopeFilter);
                var surface = toolCall.ToolSurface;
                var words = query.Split(new[] { ' ', '_', '-', '.', ',', ';', ':' }, StringSplitOptions.RemoveEmptyEntries)
                    .Where(w => w.Length > 1)
                    .ToList();

                var scored = AIToolManager.GetTools().Values
                    .Where(t => t.Enabled
                        && (t.Surfaces & surface) != 0
                        && !string.Equals(t.Name, ProgressiveToolLoading.SearchToolName, StringComparison.Ordinal)
                        && scope.ShouldIncludePath(t.Category))
                    .Select(t =>
                    {
                        var name = t.Name.ToLowerInvariant();
                        var description = (t.Description ?? string.Empty).ToLowerInvariant();
                        var score = words.Sum(w => name.Contains(w) ? 3 : description.Contains(w) ? 1 : 0);
                        return new { Tool = t, Score = score };
                    })
                    .Where(s => s.Score > 0)
                    .OrderByDescending(s => s.Score)
                    .Take(ProgressiveToolLoading.ResultLimit)
                    .Select(s => (object)new JObject
                    {
                        ["name"] = s.Tool.Name,
                        ["description"] = s.Tool.RichDescription,
                        ["parameters"] = SafeParseSchema(s.Tool.ParametersSchema),
                    })
                    .ToList();

                var result = new JObject
                {
                    ["tools"] = JArray.FromObject(scored),
                };

                var body = AIBodyBuilder.Create()
                    .AddToolResult(result, toolInfo.Id, toolInfo.Name ?? ProgressiveToolLoading.SearchToolName)
                    .Build();

                output.CreateSuccess(body, toolCall);
                return Task.FromResult(output);
            }
            catch (Exception ex)
            {
                output.CreateError($"Error executing {ProgressiveToolLoading.SearchToolName}: {ex.Message}");
                return Task.FromResult(output);
            }
        }

        /// <summary>Parses a tool's parameter schema, falling back to an empty object.</summary>
        private static JObject SafeParseSchema(string schema)
        {
            try
            {
                return JObject.Parse(schema);
            }
            catch
            {
                return new JObject { ["type"] = "object" };
            }
        }
    }
}
