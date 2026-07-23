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
using System.Diagnostics;

namespace SmartHopper.ProviderSdk.Hosting
{
    /// <summary>
    /// Severity classification for log messages emitted from SDK code.
    /// </summary>
    public enum ProviderLogLevel
    {
        /// <summary>Verbose tracing useful only when debugging the provider.</summary>
        Trace,

        /// <summary>Routine informational messages.</summary>
        Info,

        /// <summary>Recoverable anomalies the user should know about.</summary>
        Warning,

        /// <summary>Unrecoverable failures within a single provider call.</summary>
        Error,
    }

    /// <summary>
    /// Surface that providers use for log output without depending on Rhino's
    /// <c>RhinoApp.WriteLine</c> directly. The host registers an implementation that
    /// routes to its preferred logging sink.
    /// </summary>
    public interface IProviderLogger
    {
        /// <summary>
        /// Log a message scoped to a provider id.
        /// </summary>
        void Log(ProviderLogLevel level, string providerName, string message);

        /// <summary>
        /// Log an exception scoped to a provider id.
        /// </summary>
        void LogException(string providerName, Exception exception, string context = null);
    }

    /// <summary>
    /// Default logger that writes to <see cref="Debug"/>. Suitable for tests and for
    /// stand-alone tooling that does not have a richer sink available.
    /// </summary>
    public sealed class DebugProviderLogger : IProviderLogger
    {
        /// <inheritdoc />
        public void Log(ProviderLogLevel level, string providerName, string message)
        {
            Debug.WriteLine($"[{level}][{providerName}] {message}");
        }

        /// <inheritdoc />
        public void LogException(string providerName, Exception exception, string context = null)
        {
            if (exception == null)
            {
                return;
            }

            var prefix = string.IsNullOrEmpty(context) ? string.Empty : context + ": ";
            Debug.WriteLine($"[Error][{providerName}] {prefix}{exception.GetType().Name}: {exception.Message}");
        }
    }
}
