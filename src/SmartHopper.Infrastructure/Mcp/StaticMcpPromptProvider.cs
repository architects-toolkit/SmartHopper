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
using System.Threading;
using System.Threading.Tasks;

namespace SmartHopper.Infrastructure.Mcp
{
    /// <summary>
    /// Static MCP prompt provider that serves reusable, pure prompt templates from embedded Markdown files.
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
                    _ => this.GetPromptMessagesAsync("grasshopper-expert.md")),
                new McpPrompt(
                    new Uri("prompts:///script-writer", UriKind.Absolute),
                    "script-writer",
                    "Helps create, review, and edit Grasshopper script components.",
                    _ => this.GetPromptMessagesAsync("script-writer.md")),
                new McpPrompt(
                    new Uri("prompts:///canvas-debugger", UriKind.Absolute),
                    "canvas-debugger",
                    "Diagnoses canvas issues and suggests debugging steps.",
                    _ => this.GetPromptMessagesAsync("canvas-debugger.md")),
                new McpPrompt(
                    new Uri("prompts:///definition-builder", UriKind.Absolute),
                    "definition-builder",
                    "Designs and builds robust Grasshopper component workflows.",
                    _ => this.GetPromptMessagesAsync("definition-builder.md")),
                new McpPrompt(
                    new Uri("prompts:///performance-reviewer", UriKind.Absolute),
                    "performance-reviewer",
                    "Reviews Grasshopper definitions for measurable performance problems.",
                    _ => this.GetPromptMessagesAsync("performance-reviewer.md")),
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

        private Task<IReadOnlyList<McpPromptMessage>> GetPromptMessagesAsync(string fileName)
        {
            var text = EmbeddedMcpResourceLoader.ReadMarkdown(fileName);

            return Task.FromResult<IReadOnlyList<McpPromptMessage>>(new List<McpPromptMessage>
            {
                new McpPromptMessage("user", "text", text),
            });
        }
    }
}
