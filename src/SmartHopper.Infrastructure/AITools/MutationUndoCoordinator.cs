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

using System.Threading;
using System.Threading.Tasks;
using SmartHopper.ProviderSdk.AICall.Core.Returns;

namespace SmartHopper.Infrastructure.AITools
{
    /// <summary>
    /// Coordinates host-specific undo records around one mutating tool invocation.
    /// </summary>
    public interface IMutationUndoCoordinator
    {
        /// <summary>Captures the undo state before a mutating tool executes.</summary>
        Task<IMutationUndoScope> BeginAsync(string toolName, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Completes host-specific undo handling after one mutating tool invocation.
    /// </summary>
    public interface IMutationUndoScope
    {
        /// <summary>Validates and coalesces undo records produced by the invocation.</summary>
        Task CompleteAsync(AIReturn result, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Stores the undo coordinator supplied by the Grasshopper host.
    /// </summary>
    public static class MutationUndoCoordinator
    {
        /// <summary>Gets or sets the active host coordinator.</summary>
        public static IMutationUndoCoordinator? Current { get; set; }
    }
}
