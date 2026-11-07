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

namespace SmartHopper.ProviderSdk.Logging
{
    /// <summary>
    /// Surface that SmartHopper code and providers use for log output without depending on Rhino's
    /// <c>RhinoApp.WriteLine</c> directly. The host registers an implementation that
    /// routes to its preferred logging sink.
    /// </summary>
    public interface ILogger
    {
        /// <summary>
        /// Gets a value indicating whether the given log level is enabled.
        /// </summary>
        bool IsEnabled(ProviderLogLevel level);

        /// <summary>
        /// Begins a logical operation scope with the specified state.
        /// </summary>
        /// <param name="state">Key/value state to associate with the scope.</param>
        /// <returns>A disposable that ends the scope when disposed.</returns>
        IDisposable BeginScope(IReadOnlyDictionary<string, object> state);

        /// <summary>
        /// Logs a message with the specified level and category.
        /// </summary>
        /// <param name="level">The severity of the message.</param>
        /// <param name="category">The logical source of the message (provider id, class name, or subsystem).</param>
        /// <param name="message">The message to log.</param>
        void Log(ProviderLogLevel level, string category, string message);

        /// <summary>
        /// Logs an exception with the specified category and optional context.
        /// </summary>
        /// <param name="category">The logical source of the exception.</param>
        /// <param name="exception">The exception to log.</param>
        /// <param name="context">Additional context about where the exception occurred.</param>
        void LogException(string category, Exception exception, string? context = null);
    }
}
