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
using SmartHopper.Infrastructure.AICall.Logging;
using SmartHopper.ProviderSdk.Logging;
using Xunit;

namespace SmartHopper.Infrastructure.Tests.AICall.Logging
{
    /// <summary>
    /// Tests for <see cref="SmartHopperLogFormatter"/>.
    /// </summary>
    public class SmartHopperLogFormatterTests
    {
        /// <summary>
        /// The formatter should include an ISO timestamp, severity, category, and message.
        /// </summary>
        [Fact]
        public void Format_BasicEntry_ContainsTimestampLevelCategoryAndMessage()
        {
            var entry = new SmartHopperLogEntry(
                new DateTimeOffset(2026, 9, 3, 14, 32, 0, TimeSpan.Zero),
                ProviderLogLevel.Info,
                "Test",
                "hello",
                null);

            var formatter = new SmartHopperLogFormatter();
            var line = formatter.Format(entry);

            Assert.Contains("2026-09-03T14:32:00", line);
            Assert.Contains("[Info]", line);
            Assert.Contains("[Test]", line);
            Assert.Contains("hello", line);
        }

        /// <summary>
        /// The formatter should include scope pairs in order.
        /// </summary>
        [Fact]
        public void Format_EntryWithScope_IncludesScopePairs()
        {
            var scope = new Dictionary<string, object>
            {
                { "RequestId", "42" },
                { "CorrelationId", "abc" },
            };

            var entry = new SmartHopperLogEntry(
                new DateTimeOffset(2026, 9, 3, 14, 32, 0, TimeSpan.Zero),
                ProviderLogLevel.Trace,
                "SmartHopper",
                "message",
                scope);

            var formatter = new SmartHopperLogFormatter();
            var line = formatter.Format(entry);

            Assert.Contains("RequestId=42", line);
            Assert.Contains("CorrelationId=abc", line);
        }

        /// <summary>
        /// The formatter should redact an API key in the message.
        /// </summary>
        [Fact]
        public void Format_MessageWithApiKey_RedactsToken()
        {
            var entry = new SmartHopperLogEntry(
                DateTimeOffset.UtcNow,
                ProviderLogLevel.Info,
                "Provider",
                "Authorization: Bearer sk-1234567890abcdef",
                null);

            var formatter = new SmartHopperLogFormatter();
            var line = formatter.Format(entry);

            Assert.Contains("<redacted>", line);
            Assert.DoesNotContain("sk-1234567890abcdef", line);
        }

        /// <summary>
        /// The formatter should redact a sensitive scope value.
        /// </summary>
        [Fact]
        public void Format_ScopeWithApiKey_RedactsValue()
        {
            var scope = new Dictionary<string, object>
            {
                { "x-api-key", "super-secret" },
            };

            var entry = new SmartHopperLogEntry(
                DateTimeOffset.UtcNow,
                ProviderLogLevel.Info,
                "Provider",
                "message",
                scope);

            var formatter = new SmartHopperLogFormatter();
            var line = formatter.Format(entry);

            Assert.Contains("x-api-key=<redacted>", line);
            Assert.DoesNotContain("super-secret", line);
        }
    }
}
