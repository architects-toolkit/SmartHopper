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

namespace SmartHopper.Infrastructure.AICall.Subagents
{
    /// <summary>Terminal and in-flight states of a <see cref="SubagentRun"/>.</summary>
    public enum SubagentRunStatus
    {
        /// <summary>The run is executing in the background.</summary>
        Running,

        /// <summary>The run produced an assistant reply.</summary>
        Succeeded,

        /// <summary>The run ended without a usable reply (error or cancellation).</summary>
        Failed,
    }
}
