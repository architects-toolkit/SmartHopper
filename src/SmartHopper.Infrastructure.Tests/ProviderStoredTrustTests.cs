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
    using Newtonsoft.Json;
    using SmartHopper.Infrastructure.AIProviders;
    using SmartHopper.Infrastructure.Settings;
    using SmartHopper.ProviderSdk.Hosting;
    using Xunit;
    using Decision = SmartHopper.Infrastructure.AIProviders.ProviderManager.StoredTrustDecision;

    /// <summary>
    /// Trust matrix for <see cref="ProviderManager.EvaluateStoredTrust"/> — the pure
    /// function deciding whether stored consent lets a provider file load. Consent binds
    /// to the bytes consented to, not the assembly name: verified binaries follow
    /// official updates silently, unverified changes re-prompt, and an official-name
    /// file that fails verification is treated as tampering.
    /// </summary>
    public class ProviderStoredTrustTests
    {
        private const string ConsentedHash = "AAAA";
        private const string OtherHash = "BBBB";

        private static ProviderTrustRecord Record(string? hash = ConsentedHash, bool verified = false)
        {
            return new ProviderTrustRecord { ConsentedHash = hash, VerifiedTrust = verified };
        }

#if NET7_WINDOWS
        [Theory(DisplayName = "Match loads verified in every mode [Windows]")]
#else
        [Theory(DisplayName = "Match loads verified in every mode [Core]")]
#endif
        [InlineData(ProviderIntegrityCheckMode.Soft)]
        [InlineData(ProviderIntegrityCheckMode.Hard)]
        [InlineData(ProviderIntegrityCheckMode.Strict)]
        public void Match_ReturnsLoadVerified(ProviderIntegrityCheckMode mode)
        {
            // A legitimate official update changes the bytes but still matches the
            // published hash — consent follows silently, no reprompt.
            var decision = ProviderManager.EvaluateStoredTrust(
                ProviderVerificationStatus.Match, OtherHash, Record(), mode, debugBuild: false);
            Assert.Equal(Decision.LoadVerified, decision);
        }

#if NET7_WINDOWS
        [Theory(DisplayName = "Mismatch on trusted file blocks as tampering [Windows]")]
#else
        [Theory(DisplayName = "Mismatch on trusted file blocks as tampering [Core]")]
#endif
        [InlineData(ProviderIntegrityCheckMode.Soft)]
        [InlineData(ProviderIntegrityCheckMode.Hard)]
        [InlineData(ProviderIntegrityCheckMode.Strict)]
        public void Mismatch_ReturnsBlockTampered(ProviderIntegrityCheckMode mode)
        {
            // Even in Soft, an official-name file failing verification never loads.
            var decision = ProviderManager.EvaluateStoredTrust(
                ProviderVerificationStatus.Mismatch, OtherHash, Record(), mode, debugBuild: false);
            Assert.Equal(Decision.BlockTampered, decision);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "Mismatch blocked even with matching record [Windows]")]
#else
        [Fact(DisplayName = "Mismatch blocked even with matching record [Core]")]
#endif
        public void Mismatch_WithMatchingRecord_StillBlocks()
        {
            // A forged record naming the tampered bytes must not bypass the block.
            var decision = ProviderManager.EvaluateStoredTrust(
                ProviderVerificationStatus.Mismatch, ConsentedHash, Record(ConsentedHash, verified: true),
                ProviderIntegrityCheckMode.Soft, debugBuild: false);
            Assert.Equal(Decision.BlockTampered, decision);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "Mismatch loads on debug builds [Windows]")]
#else
        [Fact(DisplayName = "Mismatch loads on debug builds [Core]")]
#endif
        public void Mismatch_Debug_LoadsUnverified()
        {
            // Developer rebuilds of official-name providers mismatch by definition —
            // DEBUG keeps trust so the development loop is preserved.
            var decision = ProviderManager.EvaluateStoredTrust(
                ProviderVerificationStatus.Mismatch, OtherHash, Record(),
                ProviderIntegrityCheckMode.Soft, debugBuild: true);
            Assert.Equal(Decision.LoadUnverified, decision);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "Unchanged unverified bytes keep consent [Windows]")]
#else
        [Fact(DisplayName = "Unchanged unverified bytes keep consent [Core]")]
#endif
        public void NotFound_UnchangedBytes_LoadsUnverified()
        {
            var decision = ProviderManager.EvaluateStoredTrust(
                ProviderVerificationStatus.NotFound, ConsentedHash, Record(),
                ProviderIntegrityCheckMode.Soft, debugBuild: false);
            Assert.Equal(Decision.LoadUnverified, decision);
        }

#if NET7_WINDOWS
        [Theory(DisplayName = "Unverified update reprompts in Soft [Windows]")]
#else
        [Theory(DisplayName = "Unverified update reprompts in Soft [Core]")]
#endif
        [InlineData(ProviderVerificationStatus.NotFound)]
        [InlineData(ProviderVerificationStatus.Unavailable)]
        [InlineData(null)]
        public void ChangedUnverifiedBytes_Soft_NeedsReconsent(ProviderVerificationStatus? status)
        {
            // Changed bytes are never silently accepted — not even when the catalogue
            // is unreachable or the check itself failed.
            var decision = ProviderManager.EvaluateStoredTrust(
                status, OtherHash, Record(), ProviderIntegrityCheckMode.Soft, debugBuild: false);
            Assert.Equal(Decision.NeedsReconsent, decision);
        }

#if NET7_WINDOWS
        [Theory(DisplayName = "Unverified update blocks in Hard and Strict [Windows]")]
#else
        [Theory(DisplayName = "Unverified update blocks in Hard and Strict [Core]")]
#endif
        [InlineData(ProviderIntegrityCheckMode.Hard)]
        [InlineData(ProviderIntegrityCheckMode.Strict)]
        public void ChangedUnverifiedBytes_StrictModes_Blocked(ProviderIntegrityCheckMode mode)
        {
            var decision = ProviderManager.EvaluateStoredTrust(
                ProviderVerificationStatus.NotFound, OtherHash, Record(), mode, debugBuild: false);
            Assert.Equal(Decision.BlockUnverifiedChanged, decision);
        }

#if NET7_WINDOWS
        [Theory(DisplayName = "Missing consent record reprompts [Windows]")]
#else
        [Theory(DisplayName = "Missing consent record reprompts [Core]")]
#endif
        [InlineData(ProviderVerificationStatus.NotFound)]
        [InlineData(ProviderVerificationStatus.Unavailable)]
        public void NoRecord_Soft_NeedsReconsent(ProviderVerificationStatus status)
        {
            // A bare TrustedProviders entry (forged or pre-records legacy) cannot prove
            // the bytes were consented — Soft re-asks instead of silently trusting.
            var decision = ProviderManager.EvaluateStoredTrust(
                status, ConsentedHash, record: null, ProviderIntegrityCheckMode.Soft, debugBuild: false);
            Assert.Equal(Decision.NeedsReconsent, decision);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "Missing consent record blocks in Hard [Windows]")]
#else
        [Fact(DisplayName = "Missing consent record blocks in Hard [Core]")]
#endif
        public void NoRecord_Hard_Blocked()
        {
            var decision = ProviderManager.EvaluateStoredTrust(
                ProviderVerificationStatus.NotFound, ConsentedHash, record: null,
                ProviderIntegrityCheckMode.Hard, debugBuild: false);
            Assert.Equal(Decision.BlockUnverifiedChanged, decision);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "Empty record hash reprompts [Windows]")]
#else
        [Fact(DisplayName = "Empty record hash reprompts [Core]")]
#endif
        public void EmptyConsentedHash_Soft_NeedsReconsent()
        {
            var decision = ProviderManager.EvaluateStoredTrust(
                ProviderVerificationStatus.NotFound, ConsentedHash, Record(hash: string.Empty),
                ProviderIntegrityCheckMode.Soft, debugBuild: false);
            Assert.Equal(Decision.NeedsReconsent, decision);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "Unavailable with unchanged bytes keeps consent [Windows]")]
#else
        [Fact(DisplayName = "Unavailable with unchanged bytes keeps consent [Core]")]
#endif
        public void Unavailable_UnchangedBytes_LoadsUnverified()
        {
            // Offline verification still honors the consent baseline — unchanged bytes
            // keep working without a network round-trip.
            var decision = ProviderManager.EvaluateStoredTrust(
                ProviderVerificationStatus.Unavailable, ConsentedHash, Record(),
                ProviderIntegrityCheckMode.Soft, debugBuild: false);
            Assert.Equal(Decision.LoadUnverified, decision);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "Unknown file hash reprompts [Windows]")]
#else
        [Fact(DisplayName = "Unknown file hash reprompts [Core]")]
#endif
        public void NullLocalHash_Soft_NeedsReconsent()
        {
            // Cannot prove bytes unchanged when the file cannot be hashed.
            var decision = ProviderManager.EvaluateStoredTrust(
                ProviderVerificationStatus.NotFound, localHash: null, Record(),
                ProviderIntegrityCheckMode.Soft, debugBuild: false);
            Assert.Equal(Decision.NeedsReconsent, decision);
        }

#if NET7_WINDOWS
        [Theory(DisplayName = "Debug builds never reprompt [Windows]")]
#else
        [Theory(DisplayName = "Debug builds never reprompt [Core]")]
#endif
        [InlineData(ProviderVerificationStatus.NotFound)]
        [InlineData(ProviderVerificationStatus.Unavailable)]
        [InlineData(null)]
        public void ChangedBytes_Debug_LoadsUnverified(ProviderVerificationStatus? status)
        {
            // Local rebuilds change bytes constantly — DEBUG keeps trust silently.
            var decision = ProviderManager.EvaluateStoredTrust(
                status, OtherHash, Record(), ProviderIntegrityCheckMode.Soft, debugBuild: true);
            Assert.Equal(Decision.LoadUnverified, decision);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "Debug without record keeps trust [Windows]")]
#else
        [Fact(DisplayName = "Debug without record keeps trust [Core]")]
#endif
        public void NoRecord_Debug_LoadsUnverified()
        {
            var decision = ProviderManager.EvaluateStoredTrust(
                ProviderVerificationStatus.NotFound, ConsentedHash, record: null,
                ProviderIntegrityCheckMode.Soft, debugBuild: true);
            Assert.Equal(Decision.LoadUnverified, decision);
        }

        [Fact(DisplayName = "Trust record serializes consent snapshot")]
        public void ProviderTrustRecord_JsonRoundTrip()
        {
            var record = new ProviderTrustRecord { ConsentedHash = ConsentedHash, VerifiedTrust = true };
            var json = JsonConvert.SerializeObject(record);
            var restored = JsonConvert.DeserializeObject<ProviderTrustRecord>(json);

            Assert.Equal(ConsentedHash, restored!.ConsentedHash);
            Assert.True(restored.VerifiedTrust);
            Assert.Contains("ConsentedHash", json);
            Assert.Contains("VerifiedTrust", json);
        }
    }
}
