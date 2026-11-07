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
using SmartHopper.ProviderSdk.Hosting;

namespace SmartHopper.ProviderSdk.Logging
{
    /// <summary>
    /// Logger implementation bound to a specific type. All messages are forwarded to
    /// <see cref="ProviderSdkHost.Logger"/> using the full type name as the category.
    /// </summary>
    /// <typeparam name="T">The type that identifies the logger's category.</typeparam>
    public sealed class TypedLogger<T> : ILogger<T>, ILogger
    {
        /// <inheritdoc />
        public string Category { get; } = typeof(T).FullName ?? typeof(T).Name;

        /// <inheritdoc />
        public bool IsEnabled(ProviderLogLevel level)
        {
            return ProviderSdkHost.Logger?.IsEnabled(level) ?? false;
        }

        /// <inheritdoc />
        public IDisposable BeginScope(IReadOnlyDictionary<string, object> state)
        {
            return ProviderSdkHost.Logger?.BeginScope(state) ?? NullScope.Instance;
        }

        /// <inheritdoc />
        public void Log(ProviderLogLevel level, string message)
        {
            ((ILogger)this).Log(level, this.Category, message);
        }

        /// <inheritdoc />
        public void LogException(Exception exception, string? context = null)
        {
            ((ILogger)this).LogException(this.Category, exception, context);
        }

        /// <inheritdoc />
        public void Trace(string message)
        {
            this.Log(ProviderLogLevel.Trace, message);
        }

        /// <inheritdoc />
        public void Info(string message)
        {
            this.Log(ProviderLogLevel.Info, message);
        }

        /// <inheritdoc />
        public void Warning(string message)
        {
            this.Log(ProviderLogLevel.Warning, message);
        }

        /// <inheritdoc />
        public void Error(string message)
        {
            this.Log(ProviderLogLevel.Error, message);
        }

        /// <inheritdoc />
        public void Exception(Exception exception, string? context = null)
        {
            this.LogException(exception, context);
        }

        /// <inheritdoc />
        void ILogger.Log(ProviderLogLevel level, string category, string message)
        {
            ProviderSdkHost.Logger?.Log(level, category, message);
        }

        /// <inheritdoc />
        void ILogger.LogException(string category, Exception exception, string? context = null)
        {
            ProviderSdkHost.Logger?.LogException(category, exception, context);
        }
    }
}
