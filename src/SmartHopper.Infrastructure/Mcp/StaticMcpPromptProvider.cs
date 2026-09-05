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

namespace SmartHopper.Infrastructure.Mcp
{
    /// <summary>
    /// Static MCP prompt provider that serves reusable, pure prompt templates.
    /// Prompts do not execute tools; they reference resources and tools by name/URI so the client
    /// can fetch live data through <c>resources/read</c> or <c>tools/call</c> when needed.
    /// </summary>
    public sealed class StaticMcpPromptProvider : IMcpPromptProvider
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="StaticMcpPromptProvider"/> class.
        /// </summary>
        public StaticMcpPromptProvider()
        {
        }

        /// <inheritdoc/>
        public Task<IReadOnlyList<McpPrompt>> ListPromptsAsync(CancellationToken cancellationToken = default)
        {
            var prompts = new List<McpPrompt>
            {
                new McpPrompt(
                    new Uri("prompts:///grasshopper-expert", UriKind.Absolute),
                    "grasshopper-expert",
                    "A general-purpose Grasshopper and SmartHopper expert.",
                    _ => this.GetGrasshopperExpertPromptAsync()),
                new McpPrompt(
                    new Uri("prompts:///script-writer", UriKind.Absolute),
                    "script-writer",
                    "Helps create, review, and edit Grasshopper script components.",
                    _ => this.GetScriptWriterPromptAsync()),
                new McpPrompt(
                    new Uri("prompts:///canvas-debugger", UriKind.Absolute),
                    "canvas-debugger",
                    "Diagnoses canvas issues and suggests debugging steps.",
                    _ => this.GetCanvasDebuggerPromptAsync()),
            };

            return Task.FromResult<IReadOnlyList<McpPrompt>>(prompts);
        }

        /// <inheritdoc/>
        public async Task<McpPrompt?> GetPromptAsync(Uri uri, CancellationToken cancellationToken = default)
        {
            if (uri == null)
            {
                return null;
            }

            var absolute = uri.IsAbsoluteUri ? uri : new Uri($"prompts://{uri}", UriKind.Absolute);

            var known = await this.ListPromptsAsync(cancellationToken).ConfigureAwait(false);
            var match = known.FirstOrDefault(p =>
                string.Equals(p.Uri.ToString(), absolute.ToString(), StringComparison.OrdinalIgnoreCase));

            return match;
        }

        private Task<IReadOnlyList<McpPromptMessage>> GetGrasshopperExpertPromptAsync()
        {
            var builder = new StringBuilder();
            builder.AppendLine("You are an expert in Grasshopper, Rhino 3D, and SmartHopper.");
            builder.AppendLine();
            builder.AppendLine("When the user asks a question, prefer calling SmartHopper tools instead of inventing answers.");
            builder.AppendLine("Use GhJSON/GhPatch when generating or editing canvas definitions.");
            builder.AppendLine("For detailed operational guidance, read the `docs:///smarthopper-readme` resource.");
            builder.AppendLine("For per-tool documentation, call `smarthopper_tool_help` or read `docs:///tool-help/{toolName}`.");
            builder.AppendLine("Ask for clarification when the request is ambiguous.");

            return Task.FromResult<IReadOnlyList<McpPromptMessage>>(new List<McpPromptMessage>
            {
                new McpPromptMessage("user", "text", builder.ToString()),
            });
        }

        private Task<IReadOnlyList<McpPromptMessage>> GetScriptWriterPromptAsync()
        {
            var builder = new StringBuilder();
            builder.AppendLine("You are a Grasshopper script-writing assistant.");
            builder.AppendLine();
            builder.AppendLine("You help create, review, and edit C# and Python script components in Grasshopper.");
            builder.AppendLine("For SmartHopper scripting conventions, read the `docs:///smarthopper-readme` resource with topic `scripting`.");
            builder.AppendLine();
            builder.AppendLine("For script generation, use the `script_generate` or `script_generate_and_place_on_canvas` tool.");
            builder.AppendLine("For reviewing existing scripts, use `script_review` with the component GUID.");
            builder.AppendLine("For editing, use `script_edit_and_replace_on_canvas`.");

            return Task.FromResult<IReadOnlyList<McpPromptMessage>>(new List<McpPromptMessage>
            {
                new McpPromptMessage("user", "text", builder.ToString()),
            });
        }

        private Task<IReadOnlyList<McpPromptMessage>> GetCanvasDebuggerPromptAsync()
        {
            var builder = new StringBuilder();
            builder.AppendLine("You are a Grasshopper canvas debugger.");
            builder.AppendLine();
            builder.AppendLine("Follow this workflow when diagnosing a broken or unexpected Grasshopper definition:");
            builder.AppendLine();
            builder.AppendLine("1. Call `gh_report` for a comprehensive canvas status report.");
            builder.AppendLine("2. Call `gh_get_errors` to locate components with errors or warnings.");
            builder.AppendLine("3. Inspect suspicious components with `gh_get` or `gh_get_by_guid`.");
            builder.AppendLine("4. For script components, use `script_review` to read the code.");
            builder.AppendLine("5. Suggest concrete fixes using the available tools, or ask for user confirmation before mutating the canvas.");
            builder.AppendLine();
            builder.AppendLine("For canonical debugging workflows, read the `docs:///smarthopper-workflows` resource.");

            return Task.FromResult<IReadOnlyList<McpPromptMessage>>(new List<McpPromptMessage>
            {
                new McpPromptMessage("user", "text", builder.ToString()),
            });
        }
    }
}
