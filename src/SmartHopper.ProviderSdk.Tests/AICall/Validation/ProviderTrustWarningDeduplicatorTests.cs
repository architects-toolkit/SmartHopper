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

namespace SmartHopper.ProviderSdk.Tests.AICall.Validation
{
    using SmartHopper.ProviderSdk.AICall.Validation;
    using SmartHopper.ProviderSdk.Diagnostics;
    using Xunit;

    /// <summary>
    /// Tests for <see cref="ProviderTrustWarningDeduplicator"/>. The deduplicator keeps
    /// process-wide state, so each test resets it up front to stay deterministic inside
    /// the serial "ProviderSdk" collection.
    /// </summary>
    [Collection("ProviderSdk")]
    public class ProviderTrustWarningDeduplicatorTests
    {
        public ProviderTrustWarningDeduplicatorTests()
        {
            ProviderTrustWarningDeduplicator.Reset();
        }

        [Fact]
        public void ShouldSurface_TrustWarning_SurfacesOnce()
        {
            var warning = CreateTrustWarning("Provider 'A' could not be verified");

            Assert.True(ProviderTrustWarningDeduplicator.ShouldSurface(warning));
            Assert.False(ProviderTrustWarningDeduplicator.ShouldSurface(warning));
            Assert.False(ProviderTrustWarningDeduplicator.ShouldSurface(warning));
        }

        [Fact]
        public void ShouldSurface_DistinctWarningsForSameProvider_SurfaceEachOnce()
        {
            var first = CreateTrustWarning("Provider 'A' is unsigned. Use it only if you trust its source.");
            var second = CreateTrustWarning("Provider 'A' is a community provider, not signed by SmartHopper.");

            Assert.True(ProviderTrustWarningDeduplicator.ShouldSurface(first));
            Assert.True(ProviderTrustWarningDeduplicator.ShouldSurface(second));
            Assert.False(ProviderTrustWarningDeduplicator.ShouldSurface(first));
            Assert.False(ProviderTrustWarningDeduplicator.ShouldSurface(second));
        }

        [Fact]
        public void ShouldSurface_DifferentProviders_SurfaceIndependently()
        {
            var warningA = CreateTrustWarning("Provider 'A' could not be verified");
            var warningB = CreateTrustWarning("Provider 'B' could not be verified");

            Assert.True(ProviderTrustWarningDeduplicator.ShouldSurface(warningA));
            Assert.True(ProviderTrustWarningDeduplicator.ShouldSurface(warningB));
        }

        [Fact]
        public void ShouldSurface_NonTrustMessages_AlwaysSurface()
        {
            var otherWarning = new SHRuntimeMessage(
                SHRuntimeMessageSeverity.Warning,
                SHRuntimeMessageOrigin.Return,
                SHMessageCode.Unknown,
                "Finish reason missing; defaulted to 'stop'.");

            var trustBlock = new SHRuntimeMessage(
                SHRuntimeMessageSeverity.Error,
                SHRuntimeMessageOrigin.Validation,
                SHMessageCode.ProviderTrustBlocked,
                "Provider 'A' blocked by integrity policy");

            Assert.True(ProviderTrustWarningDeduplicator.ShouldSurface(otherWarning));
            Assert.True(ProviderTrustWarningDeduplicator.ShouldSurface(otherWarning));
            Assert.True(ProviderTrustWarningDeduplicator.ShouldSurface(trustBlock));
            Assert.True(ProviderTrustWarningDeduplicator.ShouldSurface(trustBlock));
        }

        [Fact]
        public void Reset_AllowsWarningToSurfaceAgain()
        {
            var warning = CreateTrustWarning("Provider 'A' could not be verified");

            Assert.True(ProviderTrustWarningDeduplicator.ShouldSurface(warning));

            ProviderTrustWarningDeduplicator.Reset();

            Assert.True(ProviderTrustWarningDeduplicator.ShouldSurface(warning));
        }

        private static SHRuntimeMessage CreateTrustWarning(string text)
        {
            return new SHRuntimeMessage(
                SHRuntimeMessageSeverity.Warning,
                SHRuntimeMessageOrigin.Validation,
                SHMessageCode.ProviderTrustWarning,
                text);
        }
    }
}
