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
    using System.Linq;
    using SmartHopper.ProviderSdk.Hosting;
    using SmartHopper.ProviderSdk.Logging;
    using SmartHopper.ProviderSdk.Tests.TestHelpers;
    using Xunit;

    /// <summary>
    /// Tests for the typed logger factory and delegation.
    /// </summary>
    [Collection("ProviderSdk")]
    public sealed class LoggerTests : IDisposable
    {
        private readonly ILogger _previousLogger;

        /// <summary>
        /// Initializes a new instance of the <see cref="LoggerTests"/> class.
        /// </summary>
        public LoggerTests()
        {
            this._previousLogger = ProviderSdkHost.Logger;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            ProviderSdkHost.Logger = this._previousLogger;
        }

        /// <summary>
        /// <see cref="Logger.For{T}()"/> should return a logger whose category is the full name of <typeparamref name="T"/>.
        /// </summary>
        [Fact]
        public void ForT_ReturnsTypedLogger_WithTypeCategory()
        {
            var logger = Logger.For<FakeGenericAIProvider>();

            Assert.NotNull(logger);
            Assert.Equal(typeof(FakeGenericAIProvider).FullName, logger.Category);
        }

        /// <summary>
        /// <see cref="Logger.For{T}()"/> should return the same instance for the same type.
        /// </summary>
        [Fact]
        public void ForT_ReturnsSameInstance_ForSameType()
        {
            var first = Logger.For<FakeGenericAIProvider>();
            var second = Logger.For<FakeGenericAIProvider>();

            Assert.Same(first, second);
        }

        /// <summary>
        /// <see cref="Logger.For(Type)"/> and <see cref="Logger.For{T}()"/> should return the same
        /// underlying logger instance for the same type.
        /// </summary>
        [Fact]
        public void ForType_ReturnsSameInstance_AsForT()
        {
            var typed = Logger.For<FakeGenericAIProvider>();
            var untyped = Logger.For(typeof(FakeGenericAIProvider));

            Assert.Same((object)typed, (object)untyped);
        }

        /// <summary>
        /// <see cref="ILogger{T}.Log"/> should forward to the host logger using the type category.
        /// </summary>
        [Fact]
        public void ForT_Log_ForwardsWithCategory()
        {
            var collector = new CollectingLogger();
            ProviderSdkHost.Logger = collector;

            Logger.For<FakeGenericAIProvider>().Log(ProviderLogLevel.Info, "hello");

            Assert.Single(collector.Entries);
            var entry = collector.Entries[0];
            Assert.Equal(ProviderLogLevel.Info, entry.Level);
            Assert.Equal(typeof(FakeGenericAIProvider).FullName, entry.Category);
            Assert.Equal("hello", entry.Message);
        }

        /// <summary>
        /// <see cref="ILogger{T}.Trace"/> should forward a trace message with the type category.
        /// </summary>
        [Theory]
        [InlineData(nameof(ILogger<FakeGenericAIProvider>.Trace), ProviderLogLevel.Trace)]
        [InlineData(nameof(ILogger<FakeGenericAIProvider>.Info), ProviderLogLevel.Info)]
        [InlineData(nameof(ILogger<FakeGenericAIProvider>.Warning), ProviderLogLevel.Warning)]
        [InlineData(nameof(ILogger<FakeGenericAIProvider>.Error), ProviderLogLevel.Error)]
        public void ForT_SeverityHelpers_ForwardCorrectLevel(string methodName, ProviderLogLevel expectedLevel)
        {
            var collector = new CollectingLogger();
            ProviderSdkHost.Logger = collector;

            var logger = Logger.For<FakeGenericAIProvider>();
            var method = typeof(ILogger<FakeGenericAIProvider>).GetMethod(methodName, new[] { typeof(string) });
            Assert.NotNull(method);
            method!.Invoke(logger, new object[] { "msg" });

            Assert.Single(collector.Entries);
            Assert.Equal(expectedLevel, collector.Entries[0].Level);
            Assert.Equal(typeof(FakeGenericAIProvider).FullName, collector.Entries[0].Category);
            Assert.Equal("msg", collector.Entries[0].Message);
        }

        /// <summary>
        /// <see cref="ILogger{T}.LogException"/> should forward to the host logger with the type category.
        /// </summary>
        [Fact]
        public void ForT_LogException_ForwardsWithCategoryAndException()
        {
            var collector = new CollectingLogger();
            ProviderSdkHost.Logger = collector;

            var exception = new InvalidOperationException("boom");
            Logger.For<FakeGenericAIProvider>().LogException(exception, "context");

            Assert.Single(collector.Exceptions);
            var captured = collector.Exceptions[0];
            Assert.Equal(typeof(FakeGenericAIProvider).FullName, captured.Category);
            Assert.Same(exception, captured.Exception);
            Assert.Equal("context", captured.Context);
        }

        /// <summary>
        /// When cast to <see cref="ILogger"/> the underlying <see cref="TypedLogger{T}"/>
        /// still supports the low-level category-parameterized contract.
        /// </summary>
        [Fact]
        public void ForT_CanBeUsedAsILogger_WithExplicitCategory()
        {
            var collector = new CollectingLogger();
            ProviderSdkHost.Logger = collector;

            var typed = Logger.For<FakeGenericAIProvider>();
            var ilogger = (ILogger)(object)typed;

            ilogger.Log(ProviderLogLevel.Warning, "OtherCategory", "msg");

            Assert.Single(collector.Entries);
            Assert.Equal("OtherCategory", collector.Entries[0].Category);
            Assert.Equal("msg", collector.Entries[0].Message);
        }

        /// <summary>
        /// <see cref="AIProvider{T}.Log"/> is initialized and uses the provider type as category.
        /// </summary>
        [Fact]
        public void AIProviderT_Log_IsTypedLoggerWithProviderCategory()
        {
            var collector = new CollectingLogger();
            ProviderSdkHost.Logger = collector;

            var provider = FakeGenericAIProvider.Instance;

            Assert.NotNull(provider.ExposedLog);
            Assert.Equal(typeof(FakeGenericAIProvider).FullName, provider.ExposedLog.Category);

            provider.ExposedLog.Info("provider message");

            var entry = Assert.Single(collector.Entries);
            Assert.Equal(typeof(FakeGenericAIProvider).FullName, entry.Category);
            Assert.Equal(ProviderLogLevel.Info, entry.Level);
            Assert.Equal("provider message", entry.Message);
        }

        /// <summary>
        /// <see cref="ILogger{T}.IsEnabled"/> should delegate to the host logger.
        /// </summary>
        [Theory]
        [InlineData(ProviderLogLevel.Trace, false)]
        [InlineData(ProviderLogLevel.Info, true)]
        [InlineData(ProviderLogLevel.Error, true)]
        public void ForT_IsEnabled_DelegatesToHostLogger(ProviderLogLevel level, bool enabled)
        {
            ProviderSdkHost.Logger = new FilteringLogger(level => level >= ProviderLogLevel.Info);

            var logger = Logger.For<FakeGenericAIProvider>();
            Assert.Equal(enabled, logger.IsEnabled(level));
        }

        private sealed class CollectingLogger : ILogger
        {
            public List<LogEntry> Entries { get; } = new List<LogEntry>();

            public List<ExceptionEntry> Exceptions { get; } = new List<ExceptionEntry>();

            public bool IsEnabled(ProviderLogLevel level) => true;

            public IDisposable BeginScope(IReadOnlyDictionary<string, object> state) => NullScope.Instance;

            public void Log(ProviderLogLevel level, string category, string message)
            {
                this.Entries.Add(new LogEntry(level, category, message));
            }

            public void LogException(string category, Exception exception, string? context = null)
            {
                this.Exceptions.Add(new ExceptionEntry(category, exception, context));
            }
        }

        private sealed class FilteringLogger : ILogger
        {
            private readonly Predicate<ProviderLogLevel> _predicate;

            public FilteringLogger(Predicate<ProviderLogLevel> predicate)
            {
                this._predicate = predicate;
            }

            public bool IsEnabled(ProviderLogLevel level) => this._predicate(level);

            public IDisposable BeginScope(IReadOnlyDictionary<string, object> state) => NullScope.Instance;

            public void Log(ProviderLogLevel level, string category, string message)
            {
            }

            public void LogException(string category, Exception exception, string? context = null)
            {
            }
        }

        private readonly struct LogEntry
        {
            public LogEntry(ProviderLogLevel level, string category, string message)
            {
                this.Level = level;
                this.Category = category;
                this.Message = message;
            }

            public ProviderLogLevel Level { get; }

            public string Category { get; }

            public string Message { get; }
        }

        private readonly struct ExceptionEntry
        {
            public ExceptionEntry(string category, Exception exception, string? context)
            {
                this.Category = category;
                this.Exception = exception;
                this.Context = context;
            }

            public string Category { get; }

            public Exception Exception { get; }

            public string? Context { get; }
        }
    }
}
