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
    using SmartHopper.ProviderSdk.Logging;
    using Xunit;

    /// <summary>
    /// Tests for <see cref="SmartHopperLogExceptionFormatter"/>.
    /// </summary>
    public sealed class SmartHopperLogExceptionFormatterTests
    {
        [Fact]
        public void FormatException_IncludesTypeAndMessage()
        {
            var ex = new InvalidOperationException("Something went wrong");
            var result = SmartHopperLogExceptionFormatter.FormatException(ex, "context");

            Assert.Contains("InvalidOperationException", result);
            Assert.Contains("Something went wrong", result);
            Assert.Contains("context:", result);
        }

        [Fact]
        public void FormatException_IncludesInnerException()
        {
            var inner = new ArgumentException("Bad arg");
            var ex = new InvalidOperationException("Outer", inner);
            var result = SmartHopperLogExceptionFormatter.FormatException(ex);

            Assert.Contains("InvalidOperationException: Outer", result);
            Assert.Contains("ArgumentException: Bad arg", result);
            Assert.Contains("Inner exception:", result);
        }

        [Fact]
        public void FormatException_IncludesDataEntries()
        {
            var ex = new InvalidOperationException("Failure");
            ex.Data["key"] = "value";
            var result = SmartHopperLogExceptionFormatter.FormatException(ex);

            Assert.Contains("Data:", result);
            Assert.Contains("key=value", result);
        }

        [Fact]
        public void FormatException_NullInput_ReturnsDefaultMessage()
        {
            var result = SmartHopperLogExceptionFormatter.FormatException(null, "init");
            Assert.Contains("init: No exception provided", result);
        }

        [Fact]
        public void FormatException_NullInputWithoutContext_ReturnsDefaultMessage()
        {
            var result = SmartHopperLogExceptionFormatter.FormatException(null);
            Assert.Equal("No exception provided", result);
        }
    }
}
