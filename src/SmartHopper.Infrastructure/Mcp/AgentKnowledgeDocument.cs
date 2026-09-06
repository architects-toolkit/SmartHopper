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

namespace SmartHopper.Infrastructure.Mcp
{
    /// <summary>
    /// Describes one embedded agent-knowledge document.
    /// </summary>
    public sealed class AgentKnowledgeDocument
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AgentKnowledgeDocument"/> class.
        /// </summary>
        /// <param name="id">The stable document identifier.</param>
        /// <param name="title">The display title.</param>
        /// <param name="description">The concise document description.</param>
        /// <param name="fileName">The embedded Markdown file name.</param>
        /// <param name="exposeAsResource">Whether MCP should expose the document.</param>
        public AgentKnowledgeDocument(string id, string title, string description, string fileName, bool exposeAsResource = true)
        {
            this.Id = id;
            this.Title = title;
            this.Description = description;
            this.FileName = fileName;
            this.ExposeAsResource = exposeAsResource;
        }

        /// <summary>
        /// Gets the stable document identifier.
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Gets the display title.
        /// </summary>
        public string Title { get; }

        /// <summary>
        /// Gets the concise document description.
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// Gets the embedded Markdown file name.
        /// </summary>
        public string FileName { get; }

        /// <summary>
        /// Gets a value indicating whether MCP should expose this document.
        /// </summary>
        public bool ExposeAsResource { get; }
    }
}
