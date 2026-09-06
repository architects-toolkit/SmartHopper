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
using System.Collections.ObjectModel;
using System.Linq;

namespace SmartHopper.Infrastructure.Mcp
{
    /// <summary>
    /// Provides the canonical embedded knowledge used by in-process agents and MCP clients.
    /// </summary>
    public static class AgentKnowledgeCatalog
    {
        private static readonly IReadOnlyDictionary<string, AgentKnowledgeDocument> Documents =
            new ReadOnlyDictionary<string, AgentKnowledgeDocument>(
                new Dictionary<string, AgentKnowledgeDocument>(StringComparer.OrdinalIgnoreCase)
                {
                    ["assistant-core"] = new ("assistant-core", "Assistant Core", "Mandatory operating rules for SmartHopper Grasshopper agents.", "assistant-core.md", false),
                    ["grasshopper-foundations"] = new ("grasshopper-foundations", "Grasshopper Foundations", "Core Grasshopper concepts, terminology, dependency flow, and solution behavior.", "grasshopper-foundations.md"),
                    ["grasshopper-data-trees"] = new ("grasshopper-data-trees", "Grasshopper Data Trees", "Item, list, tree, path, access, and data-matching guidance.", "grasshopper-data-trees.md"),
                    ["grasshopper-definition-design"] = new ("grasshopper-definition-design", "Grasshopper Definition Design", "Workflow design, organization, robustness, and maintainability practices.", "grasshopper-definition-design.md"),
                    ["grasshopper-geometry"] = new ("grasshopper-geometry", "Grasshopper Geometry", "Units, tolerances, domains, directions, topology, and geometry-type guidance.", "grasshopper-geometry.md"),
                    ["grasshopper-debugging"] = new ("grasshopper-debugging", "Grasshopper Debugging", "A deterministic process for diagnosing component, data, geometry, and environment failures.", "grasshopper-debugging.md"),
                    ["grasshopper-performance"] = new ("grasshopper-performance", "Grasshopper Performance", "Profiling, data growth, preview, recomputation, and concurrency guidance.", "grasshopper-performance.md"),
                    ["grasshopper-scripting-csharp"] = new ("grasshopper-scripting-csharp", "Grasshopper C# Scripting", "Rhino 8 C# script-component contracts and coding practices.", "grasshopper-scripting-csharp.md"),
                    ["grasshopper-scripting-python"] = new ("grasshopper-scripting-python", "Grasshopper Python Scripting", "Rhino 8 Python and legacy IronPython script-component guidance.", "grasshopper-scripting-python.md"),
                    ["grasshopper-scripting-vb"] = new ("grasshopper-scripting-vb", "Grasshopper VB Scripting", "Grasshopper VB script-component compatibility and coding guidance.", "grasshopper-scripting-vb.md"),
                    ["smarthopper-tool-strategy"] = new ("smarthopper-tool-strategy", "SmartHopper Tool Strategy", "Tool selection, inspection, mutation, discovery, and verification guidance.", "smarthopper-tool-strategy.md"),
                    ["smarthopper-providers"] = new ("smarthopper-providers", "SmartHopper Providers", "Provider and model discovery and configuration guidance.", "smarthopper-providers.md"),
                    ["smarthopper-research"] = new ("smarthopper-research", "SmartHopper Research", "McNeel, Ladybug, Discourse, web, and file research workflows.", "smarthopper-research.md"),
                    ["smarthopper-ghjson"] = new ("smarthopper-ghjson", "SmartHopper GhJSON Guidance", "Routing guidance for GhJSON and GhPatch reference material.", "smarthopper-ghjson.md"),
                    ["source-policy"] = new ("source-policy", "Knowledge Source Policy", "Source priority and evidence rules for Grasshopper answers.", "source-policy.md"),
                    ["smarthopper-readme"] = new ("smarthopper-readme", "SmartHopper README", "Operational index for SmartHopper tools and reference topics.", "smarthopper-readme.md"),
                    ["smarthopper-workflows"] = new ("smarthopper-workflows", "SmartHopper Workflows", "Canonical tool chains for common tasks.", "smarthopper-workflows.md"),
                });

        private static readonly IReadOnlyDictionary<string, string> TopicAliases =
            new ReadOnlyDictionary<string, string>(
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["foundations"] = "grasshopper-foundations",
                    ["grasshopper"] = "grasshopper-foundations",
                    ["data-trees"] = "grasshopper-data-trees",
                    ["trees"] = "grasshopper-data-trees",
                    ["definition-design"] = "grasshopper-definition-design",
                    ["design"] = "grasshopper-definition-design",
                    ["geometry"] = "grasshopper-geometry",
                    ["debugging"] = "grasshopper-debugging",
                    ["performance"] = "grasshopper-performance",
                    ["canvas"] = "smarthopper-tool-strategy",
                    ["selected"] = "smarthopper-tool-strategy",
                    ["errors"] = "grasshopper-debugging",
                    ["locks"] = "smarthopper-tool-strategy",
                    ["visibility"] = "smarthopper-tool-strategy",
                    ["discovery"] = "smarthopper-tool-strategy",
                    ["scripting"] = "grasshopper-scripting-csharp",
                    ["csharp"] = "grasshopper-scripting-csharp",
                    ["python"] = "grasshopper-scripting-python",
                    ["vb"] = "grasshopper-scripting-vb",
                    ["providers"] = "smarthopper-providers",
                    ["knowledge"] = "smarthopper-research",
                    ["mcneel-forum"] = "smarthopper-research",
                    ["ladybug-forum"] = "smarthopper-research",
                    ["discourse-forum"] = "smarthopper-research",
                    ["research"] = "smarthopper-research",
                    ["web"] = "smarthopper-research",
                    ["ghjson"] = "smarthopper-ghjson",
                    ["sources"] = "source-policy",
                });

        private static readonly Lazy<IReadOnlyList<AgentWorkflow>> Workflows = new (ParseWorkflows);

        /// <summary>
        /// Lists embedded knowledge documents.
        /// </summary>
        /// <param name="includeInternal">Whether to include documents intended only for prompt composition.</param>
        /// <returns>The registered documents sorted by identifier.</returns>
        public static IReadOnlyList<AgentKnowledgeDocument> ListDocuments(bool includeInternal = false)
        {
            return Documents.Values
                .Where(document => includeInternal || document.ExposeAsResource)
                .OrderBy(document => document.Id, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// Reads a registered document by identifier.
        /// </summary>
        /// <param name="id">The stable document identifier.</param>
        /// <returns>The document contents, or <see langword="null"/> when the identifier is unknown.</returns>
        public static string? GetDocumentText(string id)
        {
            return TryGetDocument(id, out var document)
                ? EmbeddedMcpResourceLoader.ReadMarkdown(document.FileName)
                : null;
        }

        /// <summary>
        /// Resolves a registered document by identifier.
        /// </summary>
        /// <param name="id">The stable document identifier.</param>
        /// <param name="document">The resolved document.</param>
        /// <returns><see langword="true"/> when the document exists.</returns>
        public static bool TryGetDocument(string id, out AgentKnowledgeDocument document)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                document = null!;
                return false;
            }

            return Documents.TryGetValue(id.Trim(), out document!);
        }

        /// <summary>
        /// Reads focused instructions for an instruction-tool topic.
        /// </summary>
        /// <param name="topic">The requested topic or alias.</param>
        /// <returns>Markdown instructions, or a message listing valid topics.</returns>
        public static string GetInstructions(string topic)
        {
            var normalized = topic?.Trim() ?? string.Empty;
            if (TopicAliases.TryGetValue(normalized, out var documentId))
            {
                return GetDocumentText(documentId) ?? string.Empty;
            }

            return $"Unknown topic. Valid topics are: {string.Join(", ", TopicAliases.Keys.OrderBy(key => key, StringComparer.OrdinalIgnoreCase))}. For canonical step-by-step workflows, call `smarthopper_workflows`.";
        }

        /// <summary>
        /// Composes the mandatory SmartHopper agent core with caller-provided instructions.
        /// </summary>
        /// <param name="instructions">Profile-specific or user-provided instructions.</param>
        /// <returns>The composed system prompt.</returns>
        public static string ComposeGrasshopperSystemPrompt(string? instructions)
        {
            var core = GetDocumentText("assistant-core") ?? string.Empty;
            return string.IsNullOrWhiteSpace(instructions)
                ? core
                : $"{core.Trim()}\n\n---\n\n{instructions.Trim()}";
        }

        /// <summary>
        /// Reads and filters canonical workflows from the embedded workflow document.
        /// </summary>
        /// <param name="workflow">Optional workflow identifier.</param>
        /// <returns>The matching workflows, or all workflows when no filter is provided.</returns>
        public static IReadOnlyList<AgentWorkflow> GetWorkflows(string? workflow = null)
        {
            return Workflows.Value
                .Where(item => string.IsNullOrWhiteSpace(workflow) || string.Equals(item.Name, workflow, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        private static IReadOnlyList<AgentWorkflow> ParseWorkflows()
        {
            var markdown = GetDocumentText("smarthopper-workflows") ?? string.Empty;
            var lines = markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
            var workflows = new List<AgentWorkflow>();

            for (var index = 0; index < lines.Length; index++)
            {
                if (!lines[index].StartsWith("## ", StringComparison.Ordinal))
                {
                    continue;
                }

                var name = lines[index].Substring(3).Trim();
                var description = string.Empty;
                var steps = new List<string>();

                for (index++; index < lines.Length && !lines[index].StartsWith("## ", StringComparison.Ordinal); index++)
                {
                    var line = lines[index].Trim();
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    if (TryReadNumberedStep(line, out var step))
                    {
                        steps.Add(step);
                    }
                    else if (string.IsNullOrWhiteSpace(description))
                    {
                        description = line;
                    }
                }

                index--;
                workflows.Add(new AgentWorkflow(name, description, steps));
            }

            return workflows;
        }

        private static bool TryReadNumberedStep(string line, out string step)
        {
            step = string.Empty;
            var separatorIndex = line.IndexOf(". ", StringComparison.Ordinal);
            if (separatorIndex <= 0 || !int.TryParse(line.AsSpan(0, separatorIndex), out _))
            {
                return false;
            }

            step = line.Substring(separatorIndex + 2).Trim();
            return !string.IsNullOrWhiteSpace(step);
        }
    }
}
