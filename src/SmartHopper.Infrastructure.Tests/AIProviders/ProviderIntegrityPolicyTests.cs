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

namespace SmartHopper.Infrastructure.Tests
{
    using SmartHopper.Infrastructure.AIProviders;
    using SmartHopper.ProviderSdk.Hosting;
    using Xunit;

    /// <summary>
    /// Covers the live integrity-mode enforcement matrix applied at provider
    /// resolution: mismatch and manifest-unknown are denied by Hard and Strict,
    /// hash-unavailable is denied by Strict alone, and Soft denies nothing.
    /// </summary>
    public class ProviderIntegrityPolicyTests
    {
        [Fact(DisplayName = "IsDenied_SoftMode_DeniesNothing")]
        public void IsDenied_SoftMode_DeniesNothing()
        {
            Assert.False(ProviderIntegrityPolicy.IsDenied(
                ProviderIntegrityCheckMode.Soft, mismatched: true, hashUnavailable: true, manifestUnknown: true));
        }

        [Fact(DisplayName = "IsDenied_CleanOutcome_NeverDenied")]
        public void IsDenied_CleanOutcome_NeverDenied()
        {
            Assert.False(ProviderIntegrityPolicy.IsDenied(
                ProviderIntegrityCheckMode.Hard, mismatched: false, hashUnavailable: false, manifestUnknown: false));
            Assert.False(ProviderIntegrityPolicy.IsDenied(
                ProviderIntegrityCheckMode.Strict, mismatched: false, hashUnavailable: false, manifestUnknown: false));
        }

        [Fact(DisplayName = "IsDenied_Mismatch_DeniedInHardAndStrict")]
        public void IsDenied_Mismatch_DeniedInHardAndStrict()
        {
            Assert.True(ProviderIntegrityPolicy.IsDenied(
                ProviderIntegrityCheckMode.Hard, mismatched: true, hashUnavailable: false, manifestUnknown: false));
            Assert.True(ProviderIntegrityPolicy.IsDenied(
                ProviderIntegrityCheckMode.Strict, mismatched: true, hashUnavailable: false, manifestUnknown: false));
        }

        [Fact(DisplayName = "IsDenied_ManifestUnknown_DeniedInHardAndStrict")]
        public void IsDenied_ManifestUnknown_DeniedInHardAndStrict()
        {
            Assert.True(ProviderIntegrityPolicy.IsDenied(
                ProviderIntegrityCheckMode.Hard, mismatched: false, hashUnavailable: false, manifestUnknown: true));
            Assert.True(ProviderIntegrityPolicy.IsDenied(
                ProviderIntegrityCheckMode.Strict, mismatched: false, hashUnavailable: false, manifestUnknown: true));
        }

        [Fact(DisplayName = "IsDenied_HashUnavailable_DeniedOnlyInStrict")]
        public void IsDenied_HashUnavailable_DeniedOnlyInStrict()
        {
            Assert.False(ProviderIntegrityPolicy.IsDenied(
                ProviderIntegrityCheckMode.Hard, mismatched: false, hashUnavailable: true, manifestUnknown: false));
            Assert.True(ProviderIntegrityPolicy.IsDenied(
                ProviderIntegrityCheckMode.Strict, mismatched: false, hashUnavailable: true, manifestUnknown: false));
        }
    }
}
