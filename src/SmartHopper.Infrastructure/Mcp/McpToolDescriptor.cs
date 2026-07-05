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

using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using SmartHopper.Infrastructure.AITools;

namespace SmartHopper.Infrastructure.Mcp
{
    /// <summary>
    /// MCP-shaped view of a SmartHopper <see cref="SmartHopper.Infrastructure.AITools.AITool"/>.
    /// </summary>
    public sealed class McpToolDescriptor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="McpToolDescriptor"/> class.
        /// </summary>
        /// <param name="name">Tool name. Used as the MCP <c>tools/call</c> target.</param>
        /// <param name="description">Human-readable description.</param>
        /// <param name="inputSchema">Parsed JSON schema for tool arguments.</param>
        /// <param name="tags">Category tags for the tool.</param>
        /// <param name="outputSchema">Parsed JSON schema for the tool result payload.</param>
        /// <param name="annotations">MCP-style annotations for the tool.</param>
        public McpToolDescriptor(
            string name,
            string description,
            JObject inputSchema,
            IReadOnlyList<string> tags,
            JObject outputSchema,
            AIToolAnnotations annotations)
        {
            this.Name = name;
            this.Description = description;
            this.InputSchema = inputSchema;
            this.Tags = tags;
            this.OutputSchema = outputSchema;
            this.Annotations = annotations;
        }

        /// <summary>Gets the tool name.</summary>
        public string Name { get; }

        /// <summary>Gets the tool description.</summary>
        public string Description { get; }

        /// <summary>Gets the JSON schema describing tool arguments.</summary>
        public JObject InputSchema { get; }

        /// <summary>Gets the category tags for the tool.</summary>
        public IReadOnlyList<string> Tags { get; }

        /// <summary>Gets the JSON schema describing the tool result payload.</summary>
        public JObject OutputSchema { get; }

        /// <summary>Gets the MCP-style annotations for the tool.</summary>
        public AIToolAnnotations Annotations { get; }

        /// <summary>
        /// Renders this descriptor as the JSON object returned in MCP <c>tools/list</c>.
        /// </summary>
        public JObject ToMcpJson()
        {
            var json = new JObject
            {
                ["name"] = this.Name,
                ["description"] = this.Description,
                ["inputSchema"] = this.InputSchema,
                ["outputSchema"] = this.OutputSchema,
                ["tags"] = new JArray(this.Tags.Select(t => (JToken)t)),
                ["annotations"] = this.BuildAnnotationsJson(),
            };

            return json;
        }

        private JObject BuildAnnotationsJson()
        {
            var annotations = new JObject();

            if (this.Annotations.ReadOnlyHint.HasValue)
            {
                annotations["readOnlyHint"] = this.Annotations.ReadOnlyHint.Value;
            }

            if (this.Annotations.DestructiveHint.HasValue)
            {
                annotations["destructiveHint"] = this.Annotations.DestructiveHint.Value;
            }

            if (this.Annotations.IdempotentHint.HasValue)
            {
                annotations["idempotentHint"] = this.Annotations.IdempotentHint.Value;
            }

            if (this.Annotations.OpenWorldHint.HasValue)
            {
                annotations["openWorldHint"] = this.Annotations.OpenWorldHint.Value;
            }

            if (!string.IsNullOrWhiteSpace(this.Annotations.Title))
            {
                annotations["title"] = this.Annotations.Title;
            }

            return annotations;
        }
    }
}
