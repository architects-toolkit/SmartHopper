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

using SmartHopper.Infrastructure.AIProviders;
using SmartHopper.Infrastructure.Security;
using Xunit;

namespace SmartHopper.Infrastructure.Tests.Security
{
    /// <summary>Strict renderer trust predicate: Official alone is not sufficient —
    /// strong-name AND (Windows Authenticode OR hash-manifest match) must corroborate.
    /// </summary>
    public sealed class RendererIntegrityGateTests
    {
        private static ProviderClassificationResult Result(
            ProviderClassification classification,
            bool strongName = true,
            bool auth = false,
            ProviderVerificationStatus hash = ProviderVerificationStatus.NotFound)
            => new ProviderClassificationResult
            {
                Classification = classification,
                StrongNameMatchesHost = strongName,
                AuthenticodeMatchesHost = auth,
                HashStatus = hash,
            };

        [Fact]
        public void WindowsAuthenticodeGood_IsTrusted()
        {
            Assert.True(RendererIntegrityGate.IsTrusted(
                Result(ProviderClassification.Official, strongName: true, auth: true),
                isWindows: true));
        }

        [Fact]
        public void WindowsTokenOnly_IsRejected()
        {
            // Official + strong-name but neither Authenticode nor hash corroboration.
            Assert.False(RendererIntegrityGate.IsTrusted(
                Result(ProviderClassification.Official, strongName: true),
                isWindows: true));
        }

        [Fact]
        public void HashMatchOnly_IsRejectedWithoutStrongName()
        {
            Assert.False(RendererIntegrityGate.IsTrusted(
                Result(ProviderClassification.Official, strongName: false, hash: ProviderVerificationStatus.Match),
                isWindows: false));
        }

        [Fact]
        public void MacHashGood_IsTrusted()
        {
            Assert.True(RendererIntegrityGate.IsTrusted(
                Result(ProviderClassification.Official, strongName: true, hash: ProviderVerificationStatus.Match),
                isWindows: false));
        }

        [Fact]
        public void HashMismatch_IsRejected()
        {
            Assert.False(RendererIntegrityGate.IsTrusted(
                Result(ProviderClassification.Official, strongName: true, hash: ProviderVerificationStatus.Mismatch),
                isWindows: false));
        }

        [Theory]
        [InlineData(ProviderClassification.OfficialTampered)]
        [InlineData(ProviderClassification.Invalid)]
        public void TamperedOrInvalid_IsRejected(ProviderClassification classification)
        {
            Assert.False(RendererIntegrityGate.IsTrusted(
                Result(classification, strongName: true, auth: true, hash: ProviderVerificationStatus.Match),
                isWindows: true,
                isPackagedInstallPath: true));
        }

        [Fact]
        public void NullResult_IsRejected()
        {
            Assert.False(RendererIntegrityGate.IsTrusted(null, isWindows: true));
        }

        [Fact]
        public void CommunityForeignDirectory_IsRejected()
        {
            Assert.False(RendererIntegrityGate.IsTrusted(
                Result(ProviderClassification.Community),
                isWindows: true,
                isPackagedInstallPath: false));
        }
    }
}
