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

using SmartHopper.ProviderSdk.Hosting;

namespace SmartHopper.Infrastructure.AIProviders
{
    /// <summary>
    /// Pure policy over the provider-integrity matrix: given a provider's
    /// verification outcome (recorded at discovery) and an integrity check mode,
    /// decides whether the provider may be used. Discovery consults the same
    /// matrix when registering providers, and <see cref="ProviderManager"/> applies
    /// it again per resolution so raising the mode mid-session (e.g. Soft→Hard)
    /// denies already-registered providers immediately instead of only at the
    /// next rescan.
    /// </summary>
    internal static class ProviderIntegrityPolicy
    {
        /// <summary>
        /// Returns <see langword="true"/> when a provider with the given
        /// verification outcome is denied under <paramref name="mode"/>.
        /// Mirrors the load-time matrix: hash mismatch and manifest-unknown are
        /// denied by both Hard and Strict; a missing hash repository is tolerated
        /// by Hard and denied by Strict; Soft denies nothing.
        /// </summary>
        /// <param name="mode">The effective integrity check mode.</param>
        /// <param name="mismatched">The provider's hash does not match the published manifest.</param>
        /// <param name="hashUnavailable">Verification data could not be retrieved (offline / repository unreachable).</param>
        /// <param name="manifestUnknown">The provider is absent from the official manifest (custom/third-party).</param>
        internal static bool IsDenied(
            ProviderIntegrityCheckMode mode,
            bool mismatched,
            bool hashUnavailable,
            bool manifestUnknown)
        {
            if (mode == ProviderIntegrityCheckMode.Soft)
            {
                return false;
            }

            if (mismatched || manifestUnknown)
            {
                return true;
            }

            return mode == ProviderIntegrityCheckMode.Strict && hashUnavailable;
        }
    }
}
