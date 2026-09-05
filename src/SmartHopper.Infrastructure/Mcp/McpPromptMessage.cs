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
    /// A single message inside an MCP prompt template.
    /// </summary>
    public sealed class McpPromptMessage
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="McpPromptMessage"/> class.
        /// </summary>
        /// <param name="role">The message role, e.g. <c>user</c> or <c>assistant</c>.</param>
        /// <param name="contentType">The content type, e.g. <c>text</c>.</param>
        /// <param name="text">The message text.</param>
        public McpPromptMessage(string role, string contentType, string text)
        {
            this.Role = role;
            this.ContentType = contentType;
            this.Text = text;
        }

        /// <summary>
        /// Gets the message role.
        /// </summary>
        public string Role { get; }

        /// <summary>
        /// Gets the content type.
        /// </summary>
        public string ContentType { get; }

        /// <summary>
        /// Gets the message text.
        /// </summary>
        public string Text { get; }
    }
}
