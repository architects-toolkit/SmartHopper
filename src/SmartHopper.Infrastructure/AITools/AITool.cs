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
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.ProviderSdk.AICall.Core.Requests;
using SmartHopper.ProviderSdk.AICall.Core.Returns;
using SmartHopper.ProviderSdk.AIModels;

namespace SmartHopper.Infrastructure.AITools
{
    /// <summary>
    /// MCP-style hints for an AI tool. Exposed to clients that understand the
    /// Model Context Protocol tool annotations.
    /// </summary>
    public sealed class AIToolAnnotations
    {
        /// <summary>
        /// Gets a hint that the tool does not modify any state. Defaults to the opposite of
        /// <see cref="AITool.MutatesCanvas"/> when not explicitly set.
        /// </summary>
        public bool? ReadOnlyHint { get; }

        /// <summary>
        /// Gets a hint that the tool may perform destructive updates to existing entities.
        /// </summary>
        public bool? DestructiveHint { get; }

        /// <summary>
        /// Gets a hint that the tool may perform the same operation repeatedly with the same effect.
        /// </summary>
        public bool? IdempotentHint { get; }

        /// <summary>
        /// Gets a hint that the tool interacts with external entities. Useful for tools that
        /// call the web or external services.
        /// </summary>
        public bool? OpenWorldHint { get; }

        /// <summary>
        /// Gets a human-readable title for the tool.
        /// </summary>
        public string? Title { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="AIToolAnnotations"/> class.
        /// </summary>
        /// <param name="readOnlyHint">Whether the tool is read-only.</param>
        /// <param name="destructiveHint">Whether the tool performs destructive updates.</param>
        /// <param name="idempotentHint">Whether the tool is idempotent.</param>
        /// <param name="openWorldHint">Whether the tool interacts with external entities.</param>
        /// <param name="title">Human-readable title for the tool.</param>
        public AIToolAnnotations(bool? readOnlyHint = null, bool? destructiveHint = null, bool? idempotentHint = null, bool? openWorldHint = null, string? title = null)
        {
            this.ReadOnlyHint = readOnlyHint;
            this.DestructiveHint = destructiveHint;
            this.IdempotentHint = idempotentHint;
            this.OpenWorldHint = openWorldHint;
            this.Title = title;
        }
    }

    /// <summary>
    /// Represents an AI-callable tool with metadata and execution function.
    /// </summary>
    public class AITool
    {
        /// <summary>
        /// Gets a value indicating whether the tool is enabled and should be exposed to AI models.
        /// Defaults to <c>true</c>; set to <c>false</c> to hide experimental or unsupported tools.
        /// </summary>
        public bool Enabled { get; }

        /// <summary>
        /// Gets name of the tool (used for tool calls).
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets description of what the tool does.
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// Gets category of the tool.
        /// </summary>
        public string Category { get; } = "General";

        /// <summary>
        /// Gets the JSON schema describing the tool's parameters.
        /// </summary>
        public string ParametersSchema { get; }

        /// <summary>
        /// Gets the JSON schema describing the tool's result payload.
        /// </summary>
        public string OutputSchema { get; }

        /// <summary>
        /// Gets the rich, LLM-facing description for this tool, including mutability prefix and tags.
        /// This is the description exposed to LLMs both through MCP and inside-Smarthopper tool calls.
        /// </summary>
        public string RichDescription => this.GetRichDescription();

        /// <summary>
        /// Gets the function to execute the tool with given parameters.
        /// </summary>
        public Func<AIToolCall, Task<AIReturn>> Execute { get; }

        /// <summary>
        /// Gets required capabilities for this tool to function properly.
        /// </summary>
        public AICapability RequiredCapabilities { get; }

        /// <summary>
        /// Gets the optional function that builds a provider-level <see cref="AIRequestCall"/> from
        /// tool parameters without executing it. Used during batch collection to build requests
        /// for aggregated submission. <c>null</c> means the tool does not support batch mode
        /// and will always be executed synchronously.
        /// </summary>
        public Func<AIToolCall, AIRequestCall>? BuildRequest { get; }

        /// <summary>
        /// Gets a value indicating whether invoking this tool alters the Grasshopper canvas or
        /// document state. Defaults to <c>true</c>; read-only or query-style tools should set this
        /// to <c>false</c>.
        /// </summary>
        public bool MutatesCanvas { get; }

        /// <summary>
        /// Gets the category tags associated with the tool (e.g., <c>canvas</c>, <c>read-only</c>).
        /// </summary>
        public IReadOnlyList<string> Tags { get; }

        /// <summary>
        /// Gets the MCP-style annotations for the tool, including mutability and risk hints.
        /// </summary>
        public AIToolAnnotations Annotations { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="AITool"/> class.
        /// Creates a new AI tool.
        /// </summary>
        /// <param name="name">Name of the tool (used for tool calls).</param>
        /// <param name="description">Description of what the tool does.</param>
        /// <param name="category">Category of the tool.</param>
        /// <param name="parametersSchema">JSON schema describing the tool's parameters.</param>
        /// <param name="execute">Function to execute the tool with given parameters.</param>
        /// <param name="requiredCapabilities">Array of capabilities required by this tool (optional, defaults to no requirements).</param>
        /// <param name="buildRequest">Optional function to build an <see cref="AIRequestCall"/> without executing it, for batch mode support. Null means sync-only.</param>
        /// <param name="mutatesCanvas">Whether invoking the tool mutates the Grasshopper canvas or document state. Defaults to <c>true</c>.</param>
        /// <param name="enabled">Whether the tool is enabled and exposed to AI models. Defaults to <c>true</c>.</param>
        /// <param name="tags">Category tags for the tool.</param>
        /// <param name="outputSchema">JSON schema describing the tool's result payload.</param>
        /// <param name="annotations">MCP-style annotations for the tool.</param>
        public AITool(
            string name,
            string description,
            string category,
            string parametersSchema,
            Func<AIToolCall, Task<AIReturn>> execute,
            AICapability requiredCapabilities = AICapability.None,
            Func<AIToolCall, AIRequestCall>? buildRequest = null,
            bool mutatesCanvas = true,
            bool enabled = true,
            IReadOnlyList<string>? tags = null,
            string? outputSchema = null,
            AIToolAnnotations? annotations = null)
        {
            this.Name = name ?? throw new ArgumentNullException(nameof(name));
            this.Description = description ?? throw new ArgumentNullException(nameof(description));
            this.Category = category ?? throw new ArgumentNullException(nameof(category));
            this.ParametersSchema = parametersSchema ?? throw new ArgumentNullException(nameof(parametersSchema));
            this.Execute = execute ?? throw new ArgumentNullException(nameof(execute));
            this.RequiredCapabilities = requiredCapabilities;
            this.BuildRequest = buildRequest;
            this.MutatesCanvas = mutatesCanvas;
            this.Enabled = enabled;
            this.Tags = tags ?? BuildDefaultTags(category, mutatesCanvas);
            this.OutputSchema = outputSchema ?? "{ \"type\": \"object\" }";
            this.Annotations = annotations ?? new AIToolAnnotations(
                readOnlyHint: !mutatesCanvas,
                destructiveHint: mutatesCanvas);
        }

        /// <summary>
        /// Builds the rich, LLM-facing description for this tool, including mutability prefix and tags.
        /// This description is exposed to LLMs both through MCP and inside-Smarthopper tool calls.
        /// </summary>
        /// <returns>The formatted description.</returns>
        public string GetRichDescription()
        {
            var prefix = this.MutatesCanvas ? "[Mutates canvas]" : "[Read-only]";
            var tags = this.Tags.Count > 0 ? $" Tags: {string.Join(", ", this.Tags)}." : string.Empty;
            return $"{prefix}{tags} {this.Description}";
        }

        private static IReadOnlyList<string> BuildDefaultTags(string category, bool mutatesCanvas)
        {
            var tags = new List<string>
            {
                category.ToLowerInvariant(),
                mutatesCanvas ? "mutating" : "read-only",
            };

            return tags;
        }
    }
}
