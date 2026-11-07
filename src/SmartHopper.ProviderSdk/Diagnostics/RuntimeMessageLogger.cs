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

using System.Collections.Generic;
using SmartHopper.ProviderSdk.Logging;

namespace SmartHopper.ProviderSdk.Diagnostics
{
    /// <summary>
    /// Bridges <see cref="SHRuntimeMessage"/> instances to the provider SDK logger.
    /// Maps the message severity to the matching <see cref="ProviderLogLevel"/> and
    /// includes the origin and code in the logged text.
    /// </summary>
    public static class RuntimeMessageLogger
    {
        /// <summary>
        /// Logs a single runtime message to the configured logger.
        /// </summary>
        /// <param name="message">The message to log.</param>
        /// <param name="category">Optional category; defaults to "SmartHopper".</param>
        public static void Log(SHRuntimeMessage message, string category = null)
        {
            if (message == null)
            {
                return;
            }

            var cat = category ?? "SmartHopper";
            var text = $"[{message.Origin}:{message.Code}] {message.Message}";

            switch (message.Severity)
            {
                case SHRuntimeMessageSeverity.Debug:
                    Logger.Trace(cat, text);
                    break;
                case SHRuntimeMessageSeverity.Info:
                    Logger.Info(cat, text);
                    break;
                case SHRuntimeMessageSeverity.Warning:
                    Logger.Warning(cat, text);
                    break;
                case SHRuntimeMessageSeverity.Error:
                    Logger.Error(cat, text);
                    break;
            }
        }

        /// <summary>
        /// Logs a collection of runtime messages to the configured logger.
        /// </summary>
        /// <param name="messages">The messages to log.</param>
        /// <param name="category">Optional category; defaults to "SmartHopper".</param>
        public static void Log(IEnumerable<SHRuntimeMessage> messages, string category = null)
        {
            if (messages == null)
            {
                return;
            }

            foreach (var message in messages)
            {
                Log(message, category);
            }
        }
    }
}
