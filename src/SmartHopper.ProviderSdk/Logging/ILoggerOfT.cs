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
    /// Generic logger bound to a specific type. The logger's category is derived from <typeparamref name="T"/>,
    /// so callers can emit messages without passing an explicit category string.
    /// </summary>
    /// <typeparam name="T">The type that identifies the logger's category.</typeparam>
    public interface ILogger<T>
    {
        /// <summary>
        /// Gets the category used for log messages emitted through this logger.
        /// </summary>
        string Category { get; }

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
        /// Logs a message with the specified level.
        /// </summary>
        /// <param name="level">The severity of the message.</param>
        /// <param name="message">The message to log.</param>
        void Log(ProviderLogLevel level, string message);

        /// <summary>
        /// Logs an exception.
        /// </summary>
        /// <param name="exception">The exception to log.</param>
        /// <param name="context">Additional context about where the exception occurred.</param>
        void LogException(Exception exception, string? context = null);

        /// <summary>
        /// Logs a trace message.
        /// </summary>
        /// <param name="message">The message to log.</param>
        void Trace(string message);

        /// <summary>
        /// Logs an informational message.
        /// </summary>
        /// <param name="message">The message to log.</param>
        void Info(string message);

        /// <summary>
        /// Logs a warning message.
        /// </summary>
        /// <param name="message">The message to log.</param>
        void Warning(string message);

        /// <summary>
        /// Logs an error message.
        /// </summary>
        /// <param name="message">The message to log.</param>
        void Error(string message);

        /// <summary>
        /// Logs an exception.
        /// </summary>
        /// <param name="exception">The exception to log.</param>
        /// <param name="context">Additional context about where the exception occurred.</param>
        void Exception(Exception exception, string? context = null);
    }
}
