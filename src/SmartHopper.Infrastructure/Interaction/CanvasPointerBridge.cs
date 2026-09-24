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

namespace SmartHopper.Infrastructure.Interaction
{
    /// <summary>
    /// Process-wide bridge that lets chat surfaces replay a stored <see cref="CanvasPointerRequest"/>
    /// (pan, zoom and highlight) without holding a reference to the Grasshopper canvas layer.
    /// The Grasshopper layer assigns <see cref="ReplayHandler"/> when the canvas pointer service
    /// initializes; callers resolve requests by pointer id only.
    /// </summary>
    public static class CanvasPointerBridge
    {
        /// <summary>
        /// Gets or sets the handler that replays a pointer request by id.
        /// Signature: (pointerId, cancellationToken) => whether the replay was executed.
        /// Set once by the Grasshopper layer; null when no canvas pointer service is active.
        /// </summary>
        public static Func<string, CancellationToken, Task<bool>>? ReplayHandler { get; set; }
    }
}
