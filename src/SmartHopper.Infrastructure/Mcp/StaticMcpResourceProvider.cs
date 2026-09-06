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
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace SmartHopper.Infrastructure.Mcp
{
    /// <summary>
    /// Static MCP resource provider that serves documentation from Markdown files embedded in the
    /// <see cref="SmartHopper.Infrastructure"/> assembly. Stable resources are loaded directly from
    /// the assembly; per-tool help still delegates to the <c>smarthopper_tool_help</c> AITool.
    /// </summary>
    public sealed class StaticMcpResourceProvider : IMcpResourceProvider
    {
        private const string ToolHelpScheme = "docs";
        private static readonly Uri ToolHelpBaseUri = new ("docs:///tool-help", UriKind.Absolute);

        private readonly Func<string, JObject, CancellationToken, Task<JObject?>> toolExecutor;

        /// <summary>
        /// Initializes a new instance of the <see cref="StaticMcpResourceProvider"/> class
        /// using the default AITool execution path.
        /// </summary>
        public StaticMcpResourceProvider()
            : this((name, args, ct) => McpToolExecutor.ExecuteAsync(name, args, ct))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="StaticMcpResourceProvider"/> class
        /// with an injectable tool executor for testing.
        /// </summary>
        /// <param name="toolExecutor">Async function that executes a tool by name with arguments.</param>
        public StaticMcpResourceProvider(Func<string, JObject, CancellationToken, Task<JObject?>> toolExecutor)
        {
            this.toolExecutor = toolExecutor ?? throw new ArgumentNullException(nameof(toolExecutor));
        }

        /// <inheritdoc/>
        public Task<IReadOnlyList<McpResource>> ListResourcesAsync(CancellationToken cancellationToken = default)
        {
            var resources = AgentKnowledgeCatalog.ListDocuments()
                .Select(document => new McpResource(
                    new Uri($"docs:///{document.Id}", UriKind.Absolute),
                    document.Title,
                    "text/markdown",
                    document.Description,
                    _ => Task.FromResult(AgentKnowledgeCatalog.GetDocumentText(document.Id) ?? string.Empty)))
                .ToList();

            resources.Insert(0, new McpResource(
                new Uri("docs:///ghjson-schema", UriKind.Absolute),
                "GhJSON Schema",
                "text/markdown",
                "Authoritative GhJSON and GhPatch specification.",
                _ => Task.FromResult(EmbeddedMcpResourceLoader.ReadMarkdown("ghjson-schema.md"))));
            resources.Add(new McpResource(
                ToolHelpBaseUri,
                "Tool Help",
                "text/markdown",
                "Per-tool metadata and usage guidance. Request docs:///tool-help/{toolName}.",
                _ => Task.FromResult("Pass a tool name in the URI path (e.g. docs:///tool-help/gh_get).")));

            return Task.FromResult<IReadOnlyList<McpResource>>(resources);
        }

        /// <inheritdoc/>
        public async Task<McpResource?> GetResourceAsync(Uri uri, CancellationToken cancellationToken = default)
        {
            if (uri == null)
            {
                return null;
            }

            var absolute = uri.IsAbsoluteUri ? uri : new Uri($"docs://{uri}", UriKind.Absolute);

            if (IsToolHelpUri(absolute, out var toolName) && !string.IsNullOrWhiteSpace(toolName))
            {
                return new McpResource(
                    absolute,
                    $"Tool Help: {toolName}",
                    "text/markdown",
                    $"Metadata and usage guidance for the '{toolName}' tool.",
                    _ => this.GetToolHelpAsync(toolName, cancellationToken));
            }

            var known = await this.ListResourcesAsync(cancellationToken).ConfigureAwait(false);
            return known.FirstOrDefault(r =>
                string.Equals(r.Uri.ToString(), absolute.ToString(), StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsToolHelpUri(Uri uri, out string? toolName)
        {
            toolName = null;
            if (!uri.IsAbsoluteUri || !string.Equals(uri.Scheme, ToolHelpScheme, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var host = uri.Host;
            if (string.Equals(host, "tool-help", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(uri.AbsolutePath) && uri.AbsolutePath.Length > 1)
            {
                toolName = uri.AbsolutePath.TrimStart('/');
                return true;
            }

            if (uri.Segments.Length >= 2 &&
                string.Equals(uri.Segments[0], "/", StringComparison.Ordinal) &&
                string.Equals(uri.Segments[1].TrimEnd('/'), "tool-help", StringComparison.OrdinalIgnoreCase))
            {
                toolName = string.Concat(uri.Segments.Skip(2)).Trim('/');
                return !string.IsNullOrWhiteSpace(toolName);
            }

            return false;
        }

        private async Task<string> GetToolHelpAsync(string toolName, CancellationToken cancellationToken)
        {
            var result = await this.toolExecutor(
                "smarthopper_tool_help",
                new JObject { ["tool_name"] = toolName },
                cancellationToken).ConfigureAwait(false);

            if (result == null)
            {
                return $"No help available for '{toolName}'.";
            }

            var builder = new StringBuilder();
            builder.AppendLine($"# Tool Help: {toolName}");
            builder.AppendLine();

            var found = result["found"]?.Value<bool>() ?? false;
            if (!found)
            {
                builder.AppendLine(result["description"]?.ToString() ?? $"Tool '{toolName}' was not found.");

                var similar = result["similar_tools"] as JArray;
                if (similar != null && similar.Count > 0)
                {
                    builder.AppendLine();
                    builder.AppendLine("## Similar tools");
                    foreach (var tool in similar.OfType<JObject>())
                    {
                        builder.AppendLine($"- **{tool["name"]?.ToString()}**: {tool["description"]?.ToString()}");
                    }
                }

                return builder.ToString();
            }

            builder.AppendLine($"**Category:** {result["category"]?.ToString() ?? "unknown"}");
            builder.AppendLine();
            builder.AppendLine(result["description"]?.ToString() ?? string.Empty);
            builder.AppendLine();

            var tags = result["tags"] as JArray;
            if (tags != null && tags.Count > 0)
            {
                builder.AppendLine($"**Tags:** {string.Join(", ", tags.Select(t => t.ToString()))}");
                builder.AppendLine();
            }

            var annotations = result["annotations"] as JObject;
            if (annotations != null && annotations.HasValues)
            {
                builder.AppendLine("**Annotations:**");
                foreach (var property in annotations.Properties())
                {
                    builder.AppendLine($"- {property.Name}: {property.Value}");
                }

                builder.AppendLine();
            }

            builder.AppendLine("## Input schema");
            builder.AppendLine("```json");
            builder.AppendLine(result["input_schema"]?.ToString(Newtonsoft.Json.Formatting.Indented) ?? "{}");
            builder.AppendLine("```");
            builder.AppendLine();

            builder.AppendLine("## Output schema");
            builder.AppendLine("```json");
            builder.AppendLine(result["output_schema"]?.ToString(Newtonsoft.Json.Formatting.Indented) ?? "{}");
            builder.AppendLine("```");
            builder.AppendLine();

            var similarTools = result["similar_tools"] as JArray;
            if (similarTools != null && similarTools.Count > 0)
            {
                builder.AppendLine("## Similar tools");
                foreach (var tool in similarTools.OfType<JObject>())
                {
                    builder.AppendLine($"- **{tool["name"]?.ToString()}**: {tool["description"]?.ToString()}");
                }
            }

            return builder.ToString();
        }
    }
}
