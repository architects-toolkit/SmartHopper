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

using Grasshopper.Kernel;

namespace SmartHopper.Core.Grasshopper.Contracts
{
    /// <summary>
    /// Implemented by Grasshopper components that should be shielded from AI-driven
    /// canvas mutations when they are in a state where changes would break the user
    /// experience (e.g. a live MCP server that would lose its client connection).
    /// </summary>
    /// <remarks>
    /// <see cref="SmartHopper.Components.Mcp.SmartHopperMcpServerComponent"/> implements
    /// this interface so that mutating AI tools skip it while the server is enabled.
    /// The contract is intentionally general: any canvas object can opt-in by implementing
    /// the interface and returning <c>true</c> from <see cref="IsProtected"/>.
    /// </remarks>
    public interface ICanvasProtectedComponent
    {
        /// <summary>
        /// Gets a value indicating whether this canvas object should currently be
        /// protected from AI-driven modifications.
        /// </summary>
        bool IsProtected { get; }
    }
}
