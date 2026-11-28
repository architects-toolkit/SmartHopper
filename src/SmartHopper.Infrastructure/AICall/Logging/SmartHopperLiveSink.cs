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
using SmartHopper.ProviderSdk.Logging;



namespace SmartHopper.Infrastructure.AICall.Logging
{
    /// <summary>
    /// Writes log output to Rhino's command line and <see cref="Debug"/> output.
    /// </summary>
    public sealed class SmartHopperLiveSink : ILogSink
    {
        private readonly ILogFormatter _formatter;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="SmartHopperLiveSink"/> class.
        /// </summary>
        /// <param name="formatter">The formatter to use for output.</param>
        public SmartHopperLiveSink(ILogFormatter formatter)
        {
            this._formatter = formatter ?? throw new ArgumentNullException(nameof(formatter));
        }

        /// <inheritdoc />
        public string Name => "Live";

        /// <inheritdoc />
        public void Write(SmartHopperLogEntry entry)
        {
            if (this._disposed)
            {
                return;
            }

            var line = this._formatter.Format(entry);

            try
            {
                Rhino.RhinoApp.WriteLine(line);
            }
            catch
            {
                // RhinoApp may not be initialized in headless/test runs.
            }

            // Mirror to Debug output only — routing this line back through
            // Logger.For would re-enter this sink and loop forever.
            Debug.WriteLine(line);
        }

        /// <inheritdoc />
        public void Flush()
        {
            // Live output is unbuffered.
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this._disposed = true;
        }
    }
}
