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
using System.Threading;

namespace SmartHopper.ProviderSdk.Logging
{
    /// <summary>
    /// A logical logging scope that carries a set of key/value state.
    /// </summary>
    public sealed class LogScope : IDisposable
    {
        private static readonly AsyncLocal<LogScope?> _current = new AsyncLocal<LogScope?>();

        /// <summary>
        /// Gets the current active scope for the execution context.
        /// </summary>
        public static LogScope? Current => _current.Value;

        /// <summary>
        /// The state attached to this scope.
        /// </summary>
        public IReadOnlyDictionary<string, object> State { get; }

        /// <summary>
        /// The parent scope, if any.
        /// </summary>
        public LogScope? Parent { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="LogScope"/> class.
        /// </summary>
        public LogScope(IReadOnlyDictionary<string, object> state, LogScope? parent)
        {
            this.State = state ?? new Dictionary<string, object>();
            this.Parent = parent;
        }

        /// <summary>
        /// Begins a new scope as a child of the current scope and sets it as current.
        /// </summary>
        public static IDisposable BeginScope(IReadOnlyDictionary<string, object> state)
        {
            var parent = _current.Value;
            var scope = new LogScope(state, parent);
            _current.Value = scope;
            return new ScopeDisposer(scope);
        }

        /// <summary>
        /// Flattens this scope and all parent scopes into a single dictionary.
        /// Child values override parent values.
        /// </summary>
        public Dictionary<string, object> Flatten()
        {
            var result = new Dictionary<string, object>();
            var current = this;
            var stack = new Stack<LogScope>();

            while (current != null)
            {
                stack.Push(current);
                current = current.Parent;
            }

            foreach (var scope in stack)
            {
                foreach (var kvp in scope.State)
                {
                    result[kvp.Key] = kvp.Value;
                }
            }

            return result;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            // Explicit dispose is not used; the ScopeDisposer handles restoration.
        }

        private sealed class ScopeDisposer : IDisposable
        {
            private readonly LogScope _scope;
            private bool _disposed;

            public ScopeDisposer(LogScope scope)
            {
                this._scope = scope;
            }

            public void Dispose()
            {
                if (this._disposed)
                {
                    return;
                }

                this._disposed = true;

                // Restore the parent scope, but only if the current scope is still this one.
                if (_current.Value == this._scope)
                {
                    _current.Value = this._scope.Parent;
                }
            }
        }
    }
}
