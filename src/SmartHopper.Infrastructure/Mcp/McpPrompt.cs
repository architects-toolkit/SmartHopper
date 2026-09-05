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
using System.Threading;
using System.Threading.Tasks;

namespace SmartHopper.Infrastructure.Mcp
{
    /// <summary>
    /// A static MCP prompt exposed through <c>prompts/list</c> and <c>prompts/get</c>.
    /// </summary>
    public sealed class McpPrompt
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="McpPrompt"/> class.
        /// </summary>
        /// <param name="uri">The prompt URI.</param>
        /// <param name="name">The human-readable prompt name.</param>
        /// <param name="description">A short description of the prompt.</param>
        /// <param name="messagesFactory">Async factory that returns the prompt messages.</param>
        public McpPrompt(
            Uri uri,
            string name,
            string description,
            Func<CancellationToken, Task<IReadOnlyList<McpPromptMessage>>> messagesFactory)
        {
            this.Uri = uri ?? throw new ArgumentNullException(nameof(uri));
            this.Name = name ?? throw new ArgumentNullException(nameof(name));
            this.Description = description ?? throw new ArgumentNullException(nameof(description));
            this.MessagesFactory = messagesFactory ?? throw new ArgumentNullException(nameof(messagesFactory));
        }

        /// <summary>
        /// Gets the prompt URI.
        /// </summary>
        public Uri Uri { get; }

        /// <summary>
        /// Gets the human-readable prompt name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets a short description of the prompt.
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// Gets the async factory that returns the prompt messages.
        /// </summary>
        private Func<CancellationToken, Task<IReadOnlyList<McpPromptMessage>>> MessagesFactory { get; }

        /// <summary>
        /// Gets the prompt messages.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The prompt messages.</returns>
        public Task<IReadOnlyList<McpPromptMessage>> GetMessagesAsync(CancellationToken cancellationToken = default)
        {
            return this.MessagesFactory(cancellationToken);
        }
    }
}
