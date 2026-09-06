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
using System.Threading;
using System.Threading.Tasks;

namespace SmartHopper.Infrastructure.Mcp
{
    /// <summary>
    /// A static MCP resource exposed through <c>resources/list</c> and <c>resources/read</c>.
    /// </summary>
    public sealed class McpResource
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="McpResource"/> class.
        /// </summary>
        /// <param name="uri">The resource URI.</param>
        /// <param name="name">The human-readable resource name.</param>
        /// <param name="mimeType">The MIME type of the resource content.</param>
        /// <param name="description">An optional short description for the resource.</param>
        /// <param name="textFactory">Async factory that returns the resource text.</param>
        public McpResource(
            Uri uri,
            string name,
            string mimeType,
            string? description,
            Func<CancellationToken, Task<string>> textFactory)
        {
            this.Uri = uri ?? throw new ArgumentNullException(nameof(uri));
            this.Name = name ?? throw new ArgumentNullException(nameof(name));
            this.MimeType = mimeType ?? throw new ArgumentNullException(nameof(mimeType));
            this.Description = description;
            this.TextFactory = textFactory ?? throw new ArgumentNullException(nameof(textFactory));
        }

        /// <summary>
        /// Gets the resource URI.
        /// </summary>
        public Uri Uri { get; }

        /// <summary>
        /// Gets the human-readable resource name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the MIME type of the resource content.
        /// </summary>
        public string MimeType { get; }

        /// <summary>
        /// Gets an optional short description for the resource.
        /// </summary>
        public string? Description { get; }

        /// <summary>
        /// Gets the async factory that returns the resource text.
        /// </summary>
        private Func<CancellationToken, Task<string>> TextFactory { get; }

        /// <summary>
        /// Reads the resource text.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The resource text.</returns>
        public Task<string> GetTextAsync(CancellationToken cancellationToken = default)
        {
            return this.TextFactory(cancellationToken);
        }
    }
}
