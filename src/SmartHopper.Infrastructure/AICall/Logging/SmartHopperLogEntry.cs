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
using SmartHopper.ProviderSdk.Logging;



namespace SmartHopper.Infrastructure.AICall.Logging
{
    /// <summary>
    /// A single log entry routed from the SDK logger to the configured sinks.
    /// </summary>
    public sealed class SmartHopperLogEntry
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SmartHopperLogEntry"/> class.
        /// </summary>
        /// <param name="timestamp">The time the entry was created.</param>
        /// <param name="level">The log severity.</param>
        /// <param name="category">The log category.</param>
        /// <param name="message">The log message.</param>
        /// <param name="scope">The optional scope values.</param>
        public SmartHopperLogEntry(
            DateTimeOffset timestamp,
            ProviderLogLevel level,
            string category,
            string message,
            IReadOnlyDictionary<string, object>? scope)
        {
            this.Timestamp = timestamp;
            this.Level = level;
            this.Category = category ?? string.Empty;
            this.Message = message ?? string.Empty;
            this.Scope = scope;
        }

        /// <summary>
        /// Gets the time the entry was created.
        /// </summary>
        public DateTimeOffset Timestamp { get; }

        /// <summary>
        /// Gets the log severity.
        /// </summary>
        public ProviderLogLevel Level { get; }

        /// <summary>
        /// Gets the log category.
        /// </summary>
        public string Category { get; }

        /// <summary>
        /// Gets the log message.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Gets the optional scope values.
        /// </summary>
        public IReadOnlyDictionary<string, object>? Scope { get; }
    }
}
