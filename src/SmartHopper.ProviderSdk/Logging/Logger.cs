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
using System.Collections.Concurrent;
using System.Collections.Generic;
using SmartHopper.ProviderSdk.Hosting;

namespace SmartHopper.ProviderSdk.Logging
{
    /// <summary>
    /// Static facade for the provider SDK logger. Routes calls to <see cref="ProviderSdkHost.Logger"/>.
    /// This is the preferred call site for ad-hoc logging from production source.
    /// </summary>
    public static class Logger
    {
        private static readonly ConcurrentDictionary<Type, ILogger> TypedLoggers = new ConcurrentDictionary<Type, ILogger>();

        /// <summary>
        /// Gets a logger bound to the specified type.
        /// </summary>
        /// <typeparam name="T">The type that identifies the logger category.</typeparam>
        /// <returns>A typed logger.</returns>
        public static ILogger<T> For<T>()
        {
            return (ILogger<T>)TypedLoggers.GetOrAdd(typeof(T), _ => new TypedLogger<T>());
        }

        /// <summary>
        /// Gets a logger bound to the specified type.
        /// </summary>
        /// <param name="type">The type that identifies the logger category.</param>
        /// <returns>A logger using the full type name as the category.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="type"/> is null.</exception>
        public static ILogger For(Type type)
        {
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            return TypedLoggers.GetOrAdd(
                type,
                t => (ILogger)Activator.CreateInstance(typeof(TypedLogger<>).MakeGenericType(t)));
        }

        /// <summary>
        /// Gets a value indicating whether the given log level is enabled by the current logger.
        /// </summary>
        public static bool IsEnabled(ProviderLogLevel level)
        {
            var logger = ProviderSdkHost.Logger;
            return logger?.IsEnabled(level) ?? false;
        }

        /// <summary>
        /// Begins a logical operation scope with the specified state.
        /// </summary>
        public static IDisposable BeginScope(IReadOnlyDictionary<string, object> state)
        {
            var logger = ProviderSdkHost.Logger;
            return logger?.BeginScope(state) ?? NullScope.Instance;
        }

        /// <summary>
        /// Logs a trace message.
        /// </summary>
        public static void Trace(string category, string message) => Log(ProviderLogLevel.Trace, category, message);

        /// <summary>
        /// Logs an informational message.
        /// </summary>
        public static void Info(string category, string message) => Log(ProviderLogLevel.Info, category, message);

        /// <summary>
        /// Logs a warning message.
        /// </summary>
        public static void Warning(string category, string message) => Log(ProviderLogLevel.Warning, category, message);

        /// <summary>
        /// Logs an error message.
        /// </summary>
        public static void Error(string category, string message) => Log(ProviderLogLevel.Error, category, message);

        /// <summary>
        /// Logs an exception.
        /// </summary>
        public static void Exception(string category, Exception ex, string? context = null)
        {
            var logger = ProviderSdkHost.Logger;
            if (logger != null)
            {
                logger.LogException(category, ex, context);
            }
        }

        private static void Log(ProviderLogLevel level, string category, string message)
        {
            var logger = ProviderSdkHost.Logger;
            if (logger == null || !logger.IsEnabled(level))
            {
                return;
            }

            logger.Log(level, category, message);
        }
    }
}
