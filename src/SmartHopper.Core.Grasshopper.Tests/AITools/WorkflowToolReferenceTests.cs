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

namespace SmartHopper.Core.Grasshopper.Tests.AITools
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text.RegularExpressions;
    using Newtonsoft.Json.Linq;
    using SmartHopper.Core.Grasshopper.AITools;
    using Xunit;

    /// <summary>
    /// Validates that every tool name referenced in the <c>smarthopper_workflows</c>
    /// workflow steps corresponds to a tool defined in the SmartHopper.Core.Grasshopper AITools source.
    /// </summary>
    public class WorkflowToolReferenceTests
    {
        /// <summary>
        /// Matches snake_case identifiers that are likely tool names in a workflow step.
        /// Single-word tool names (e.g., web2md) are handled separately.
        /// </summary>
        private static readonly Regex ToolCandidateRegex = new Regex(
            @"\b[a-z][a-z0-9_]*(?:_[a-z0-9_]+)+\b",
            RegexOptions.Compiled);

        /// <summary>
        /// Suffixes used by the Discourse forum tools to generate their names from a prefix.
        /// </summary>
        private static readonly string[] DiscourseToolSuffixes = new[]
        {
            "_forum_search",
            "_forum_post_get",
            "_forum_topic_get",
            "_forum_post_summarize",
            "_forum_topic_summarize",
        };

#if NET7_WINDOWS
        /// <summary>
        /// Every tool name referenced in smarthopper_workflows must be defined in the AITools source.
        /// </summary>
        [Fact(DisplayName = "Workflow steps reference registered tools [Windows]")]
#else
        /// <summary>
        /// Every tool name referenced in smarthopper_workflows must be defined in the AITools source.
        /// </summary>
        [Fact(DisplayName = "Workflow steps reference registered tools [Core]")]
#endif
        public void WorkflowSteps_OnlyReferenceRegisteredTools()
        {
            var knownToolNames = this.ParseToolNamesFromSource();
            var workflows = this.GetAllWorkflows();

            var missing = new List<string>();
            foreach (var workflow in workflows)
            {
                var workflowName = workflow["name"]?.ToString() ?? "unknown";
                var steps = workflow["steps"] as JArray ?? new JArray();

                foreach (var step in steps)
                {
                    var stepText = step?.ToString() ?? string.Empty;
                    foreach (Match match in ToolCandidateRegex.Matches(stepText))
                    {
                        var candidate = match.Value;
                        if (candidate.EndsWith("_", StringComparison.Ordinal))
                        {
                            // Ignore placeholders like "gh_get_".
                            continue;
                        }

                        if (!knownToolNames.Contains(candidate))
                        {
                            missing.Add($"{workflowName}: '{candidate}'");
                        }
                    }
                }
            }

            Assert.True(
                missing.Count == 0,
                "Workflow steps reference tool names that are not defined in the AITools source: " + string.Join(", ", missing));
        }

        private HashSet<string> ParseToolNamesFromSource()
        {
            var assemblyLocation = typeof(WorkflowToolReferenceTests).Assembly.Location;
            var projectDirectory = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(assemblyLocation), "..", "..", "..", ".."));
            var toolsDirectory = Path.Combine(projectDirectory, "SmartHopper.Core.Grasshopper", "AITools");

            Assert.True(Directory.Exists(toolsDirectory), $"AITools directory not found: {toolsDirectory}");

            var files = Directory.GetFiles(toolsDirectory, "*.cs", SearchOption.TopDirectoryOnly);
            var names = new HashSet<string>();

            foreach (var file in files)
            {
                var content = File.ReadAllText(file);

                // Direct name: "..." in AITool constructor calls.
                foreach (Match match in Regex.Matches(content, @"name:\s*""([^""]*)"""))
                {
                    names.Add(match.Groups[1].Value);
                }

                // Field/const initializations: toolName = "...", wrapperToolName = "...", ToolName = "...".
                foreach (Match match in Regex.Matches(
                    content,
                    @"(?:toolName|wrapperToolName|ToolName)\s*=\s*""([^""]*)"""))
                {
                    names.Add(match.Groups[1].Value);
                }

                // Discourse prefixes generate prefixed tool names.
                foreach (Match match in Regex.Matches(content, @"ToolPrefix\s*=>?\s*""([^""]*)"""))
                {
                    var prefix = match.Groups[1].Value;
                    foreach (var suffix in DiscourseToolSuffixes)
                    {
                        names.Add(prefix + suffix);
                    }
                }
            }

            return names;
        }

        private List<JObject> GetAllWorkflows()
        {
            var provider = new smarthopper_workflows();
            var method = typeof(smarthopper_workflows).GetMethod(
                "GetWorkflows",
                BindingFlags.NonPublic | BindingFlags.Instance);

            return (List<JObject>)method.Invoke(provider, new object[] { string.Empty });
        }
    }
}
