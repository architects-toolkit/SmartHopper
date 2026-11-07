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

namespace SmartHopper.ProviderSdk.Tests.Logging
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using SmartHopper.ProviderSdk.Hosting;
    using SmartHopper.ProviderSdk.Logging;
    using Xunit;

    /// <summary>
    /// Tests for <see cref="LogScope"/> and <see cref="LogScopeContext"/>.
    /// </summary>
    [Collection("ProviderSdk")]
    public sealed class LogScopeTests : IDisposable
    {
        private readonly ILogger previousLogger;

        /// <summary>
        /// Initializes a new instance of the <see cref="LogScopeTests"/> class.
        /// </summary>
        public LogScopeTests()
        {
            this.previousLogger = ProviderSdkHost.Logger;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            ProviderSdkHost.Logger = this.previousLogger;
        }

        /// <summary>
        /// Beginning a scope should set <see cref="LogScope.Current"/> and expose its state.
        /// </summary>
        [Fact]
        public void BeginScope_SetsCurrentAndState()
        {
            var state = new Dictionary<string, object> { { "RequestId", "42" } };

            using (LogScope.BeginScope(state))
            {
                Assert.NotNull(LogScope.Current);
                Assert.Equal("42", LogScope.Current!.State["RequestId"]);
            }

            Assert.Null(LogScope.Current);
        }

        /// <summary>
        /// Disposing a child scope should restore the parent scope.
        /// </summary>
        [Fact]
        public void BeginScope_DisposeChild_RestoresParent()
        {
            var parentState = new Dictionary<string, object> { { "TraceId", "abc" } };
            var childState = new Dictionary<string, object> { { "SpanId", "def" } };

            using (LogScope.BeginScope(parentState))
            {
                using (LogScope.BeginScope(childState))
                {
                    Assert.Equal("def", LogScope.Current!.State["SpanId"]);
                }

                Assert.NotNull(LogScope.Current);
                Assert.Equal("abc", LogScope.Current!.State["TraceId"]);
            }

            Assert.Null(LogScope.Current);
        }

        /// <summary>
        /// Flatten should merge all parent and child state, with child values winning.
        /// </summary>
        [Fact]
        public void Flatten_MergesNestedScopes_ChildOverridesParent()
        {
            var parentState = new Dictionary<string, object>
            {
                { "Key", "parent" },
                { "ParentOnly", "keep" },
            };

            var childState = new Dictionary<string, object>
            {
                { "Key", "child" },
                { "ChildOnly", "also-keep" },
            };

            using (LogScope.BeginScope(parentState))
            {
                using (LogScope.BeginScope(childState))
                {
                    var flattened = LogScope.Current!.Flatten();

                    Assert.Equal(3, flattened.Count);
                    Assert.Equal("child", flattened["Key"]);
                    Assert.Equal("keep", flattened["ParentOnly"]);
                    Assert.Equal("also-keep", flattened["ChildOnly"]);
                }
            }
        }

        /// <summary>
        /// The scope should flow across async points via <see cref="AsyncLocal{T}"/>.
        /// </summary>
        [Fact]
        public async Task Scope_PersistsAcrossAsyncYield()
        {
            var state = new Dictionary<string, object> { { "CorrelationId", "xyz" } };

            using (LogScope.BeginScope(state))
            {
                await Task.Yield();

                Assert.NotNull(LogScope.Current);
                Assert.Equal("xyz", LogScope.Current!.State["CorrelationId"]);
            }

            Assert.Null(LogScope.Current);
        }

        /// <summary>
        /// <see cref="LogScopeContext"/> should mirror the current <see cref="LogScope"/>.
        /// </summary>
        [Fact]
        public void LogScopeContext_Current_MatchesLogScope()
        {
            var state = new Dictionary<string, object> { { "Context", "value" } };

            using (LogScopeContext.BeginScope(state))
            {
                Assert.NotNull(LogScopeContext.Current);
                Assert.Same(LogScope.Current, LogScopeContext.Current);
                Assert.Equal("value", LogScopeContext.Current!.State["Context"]);
            }
        }

        /// <summary>
        /// The static <see cref="Logger"/> facade should forward <see cref="BeginScope"/> to the host logger
        /// and produce an active scope when the host uses <see cref="LogScopeContext"/>.
        /// </summary>
        [Fact]
        public void Logger_BeginScope_ForwardedToHostLoggerAndSetsScope()
        {
            ProviderSdkHost.Logger = new ScopeForwardingLogger();

            var state = new Dictionary<string, object> { { "HostScope", "1" } };

            using (Logger.BeginScope(state))
            {
                Assert.NotNull(LogScope.Current);
                Assert.Equal("1", LogScope.Current!.State["HostScope"]);
            }

            Assert.Null(LogScope.Current);
        }

        private sealed class ScopeForwardingLogger : ILogger
        {
            public bool IsEnabled(ProviderLogLevel level) => true;

            public IDisposable BeginScope(IReadOnlyDictionary<string, object> state)
                => LogScopeContext.BeginScope(state);

            public void Log(ProviderLogLevel level, string category, string message)
            {
            }

            public void LogException(string category, Exception exception, string? context = null)
            {
            }
        }
    }
}
