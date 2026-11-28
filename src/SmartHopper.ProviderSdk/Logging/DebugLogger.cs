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
using System.Diagnostics;

using SmartHopper.ProviderSdk.Logging;
namespace SmartHopper.ProviderSdk.Logging
{
    /// <summary>
    /// Default logger that writes to <see cref="Debug"/> and ignores scopes. Suitable for tests and for
    /// stand-alone tooling that does not have a richer sink available.
    /// </summary>
    public sealed class DebugLogger : ILogger
    {
        /// <inheritdoc />
        public bool IsEnabled(ProviderLogLevel level) => true;

        /// <inheritdoc />
        public IDisposable BeginScope(IReadOnlyDictionary<string, object> state) => NullScope.Instance;

        /// <inheritdoc />
        public void Log(ProviderLogLevel level, string category, string message)
        {
            var scrubbed = SmartHopperLogScrubber.Scrub(message);

            // Write directly to Debug output — this logger sits below the
            // Logger.For facade (it is the default ProviderSdkHost.Logger), so
            // logging through the facade would recurse into itself forever.
            Debug.WriteLine($"[{level}][{category}] {scrubbed}");
        }

        /// <inheritdoc />
        public void LogException(string category, Exception exception, string? context = null)
        {
            if (exception == null)
            {
                return;
            }

            var message = SmartHopperLogExceptionFormatter.FormatException(exception, context);
            this.Log(ProviderLogLevel.Error, category, message);
        }
    }
}
