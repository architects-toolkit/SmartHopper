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

namespace SmartHopper.Infrastructure.AICall.Logging
{
    /// <summary>
    /// A destination for formatted log output. Sinks are registered with the
    /// <see cref="SmartHopperLogger"/> router.
    /// </summary>
    public interface ILogSink : IDisposable
    {
        /// <summary>
        /// Gets the sink name, used for diagnostics and settings mapping.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Writes the formatted entry to the sink.
        /// </summary>
        /// <param name="entry">The entry to write.</param>
        void Write(SmartHopperLogEntry entry);

        /// <summary>
        /// Flushes any buffered output.
        /// </summary>
        void Flush();
    }
}
