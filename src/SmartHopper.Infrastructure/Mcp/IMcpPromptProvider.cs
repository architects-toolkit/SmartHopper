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
    /// Provides static MCP prompts exposed through <c>prompts/list</c> and <c>prompts/get</c>.
    /// </summary>
    public interface IMcpPromptProvider
    {
        /// <summary>
        /// Lists the available prompts.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The list of available prompts.</returns>
        Task<IReadOnlyList<McpPrompt>> ListPromptsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the prompt with the specified URI, or <c>null</c> if it is not available.
        /// </summary>
        /// <param name="uri">The prompt URI.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The prompt, or <c>null</c>.</returns>
        Task<McpPrompt?> GetPromptAsync(Uri uri, CancellationToken cancellationToken = default);
    }
}
