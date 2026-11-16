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

namespace SmartHopper.Core.Grasshopper.Contracts
{
    /// <summary>
    /// Stable <see cref="Grasshopper.Kernel.GH_Component.ComponentGuid"/> values shared
    /// across assemblies that cannot reference the component projects themselves.
    /// </summary>
    public static class WellKnownComponentGuids
    {
        /// <summary>
        /// Component type GUID of the SmartHopper MCP Server component. Any document
        /// containing an instance of it can host the loopback MCP server.
        /// </summary>
        public static readonly Guid McpServerComponent = new Guid("a3c4f1d0-7e2b-4c5a-9d1b-7f5e8c0a2b4d");
    }
}
