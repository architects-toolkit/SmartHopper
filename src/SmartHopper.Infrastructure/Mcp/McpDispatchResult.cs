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
    /// Transport-facing outcome of one JSON-RPC dispatch: the serialized response
    /// body (null for notifications → HTTP 204), an optional session id to emit
    /// as <c>Mcp-Session-Id</c>, and an HTTP status hint for protocol-level
    /// rejections (e.g. unknown session → 404).
    /// </summary>
    public sealed class McpDispatchResult
    {
        /// <summary>Gets or sets the serialized JSON-RPC response body, or null for notifications.</summary>
        public string? Body { get; set; }

        /// <summary>Gets or sets the session id to return in the <c>Mcp-Session-Id</c> response header.</summary>
        public string? SessionId { get; set; }

        /// <summary>Gets or sets the HTTP status hint; 200 for normal responses, 204 for notifications, 404 for unknown sessions.</summary>
        public int StatusHint { get; set; } = 200;
    }
}
