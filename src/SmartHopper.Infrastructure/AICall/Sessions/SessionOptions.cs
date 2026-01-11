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
 * along with this library; if not, write to the Free Software Foundation,
 * Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301, USA.
 */

using System.Threading;

namespace SmartHopper.Infrastructure.AICall.Sessions
{
    /// <summary>
    /// Options controlling conversation session execution.
    /// </summary>
    public sealed class SessionOptions
    {
        /// <summary>
        /// When true, the session will execute pending tool calls and allow providers to call tools.
        /// Tool passes are bounded by <see cref="MaxToolPasses"/> and do not consume <see cref="MaxTurns"/>.
        /// </summary>
        public bool ProcessTools { get; set; } = true;

        /// <summary>
        /// Maximum number of provider turns. Only provider calls increment this counter; tool passes do not.
        /// </summary>
        public int MaxTurns { get; set; } = 8;

        /// <summary>
        /// Maximum number of tool-processing passes per turn. Tool passes are consumed by tool execution
        /// (e.g., <c>ProcessPendingToolsAsync</c>) and do not affect <see cref="MaxTurns"/>.
        /// </summary>
        public int MaxToolPasses { get; set; } = 4;

        public bool AllowParallelTools { get; set; }

        public CancellationToken CancellationToken { get; set; }
    }
}
