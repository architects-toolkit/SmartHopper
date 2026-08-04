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

namespace SmartHopper.ProviderSdk.Tests.Diagnostics
{
    using SmartHopper.ProviderSdk.Diagnostics;
    using Xunit;

    /// <summary>
    /// Tests for <see cref="SHRuntimeMessage"/>.
    /// </summary>
    [Collection("ProviderSdk")]
    public class SHRuntimeMessageTests
    {
#if NET7_WINDOWS
        private const string PlatformSuffix = " [Windows]";
#else
        private const string PlatformSuffix = " [Core]";
#endif

        [Fact(DisplayName = nameof(Constructor_StoresAllProperties) + PlatformSuffix)]
        public void Constructor_StoresAllProperties()
        {
            var message = new SHRuntimeMessage(
                SHRuntimeMessageSeverity.Warning,
                SHRuntimeMessageOrigin.Tool,
                SHMessageCode.ToolExecutionError,
                "message text",
                false);

            Assert.Equal(SHRuntimeMessageSeverity.Warning, message.Severity);
            Assert.Equal(SHRuntimeMessageOrigin.Tool, message.Origin);
            Assert.Equal(SHMessageCode.ToolExecutionError, message.Code);
            Assert.Equal("message text", message.Message);
            Assert.False(message.Surfaceable);
        }

        [Fact(DisplayName = nameof(Constructor_NullMessageBecomesEmpty) + PlatformSuffix)]
        public void Constructor_NullMessageBecomesEmpty()
        {
            var message = new SHRuntimeMessage(
                SHRuntimeMessageSeverity.Info,
                SHRuntimeMessageOrigin.Return,
                SHMessageCode.Unknown,
                null!);

            Assert.Equal(string.Empty, message.Message);
        }

        [Fact(DisplayName = nameof(Constructor_SurfaceableDefaultsToTrue) + PlatformSuffix)]
        public void Constructor_SurfaceableDefaultsToTrue()
        {
            var message = new SHRuntimeMessage(
                SHRuntimeMessageSeverity.Info,
                SHRuntimeMessageOrigin.Return,
                SHMessageCode.Unknown,
                "message text");

            Assert.True(message.Surfaceable);
        }

        [Fact(DisplayName = nameof(Constructor_SurfaceableCanBeFalse) + PlatformSuffix)]
        public void Constructor_SurfaceableCanBeFalse()
        {
            var message = new SHRuntimeMessage(
                SHRuntimeMessageSeverity.Debug,
                SHRuntimeMessageOrigin.Return,
                SHMessageCode.Unknown,
                "message text",
                false);

            Assert.False(message.Surfaceable);
        }
    }
}
