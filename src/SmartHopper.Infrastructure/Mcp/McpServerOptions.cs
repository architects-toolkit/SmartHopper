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

namespace SmartHopper.Infrastructure.Mcp
{
    /// <summary>
    /// Configuration for an <see cref="McpServer"/> instance.
    /// </summary>
    public sealed class McpServerOptions
    {
        /// <summary>
        /// Default TCP port. Matches Cordyceps' default to ease cross-tool documentation;
        /// can be overridden per-component.
        /// </summary>
        public const int DefaultPort = 26929;

        /// <summary>
        /// Gets or sets the loopback port the server should listen on. Range 1024..65535.
        /// </summary>
        public int Port { get; set; } = DefaultPort;

        /// <summary>
        /// Gets or sets the optional bearer token. When set, requests without
        /// <c>Authorization: Bearer &lt;token&gt;</c> are rejected with HTTP 401.
        /// </summary>
        public string? BearerToken { get; set; }

        /// <summary>
        /// Gets or sets an allow-list of tool names. When non-empty, only listed tools are
        /// exposed via <c>tools/list</c> / <c>tools/call</c>. When null or empty, every
        /// non-mutating tool is exposed and mutating tools are kept off by the per-tool
        /// <see cref="SmartHopper.Infrastructure.AITools.AITool.MutatesCanvas"/> flag.
        /// </summary>
        public IReadOnlyCollection<string>? EnabledTools { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether tools marked with
        /// <see cref="SmartHopper.Infrastructure.AITools.AITool.MutatesCanvas"/> are exposed.
        /// Defaults to <c>false</c> so the default surface is read-only.
        /// </summary>
        public bool ExposeMutatingTools { get; set; }

        /// <summary>
        /// Gets or sets the server identifier reported during MCP <c>initialize</c>.
        /// </summary>
        public string ServerName { get; set; } = "smarthopper";

        /// <summary>
        /// Gets or sets the server version reported during MCP <c>initialize</c>. When null,
        /// the dispatcher falls back to the assembly informational version.
        /// </summary>
        public string? ServerVersion { get; set; }

        /// <summary>
        /// Returns a defensive shallow copy.
        /// </summary>
        public McpServerOptions Clone()
        {
            return new McpServerOptions
            {
                Port = this.Port,
                BearerToken = this.BearerToken,
                EnabledTools = this.EnabledTools == null ? null : new List<string>(this.EnabledTools),
                ExposeMutatingTools = this.ExposeMutatingTools,
                ServerName = this.ServerName,
                ServerVersion = this.ServerVersion,
            };
        }
    }
}
