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

namespace SmartHopper.ProviderSdk.Hosting
{
    /// <summary>
    /// Surface that the SDK consumes to reason about per-provider trust and integrity
    /// state. Implemented by the SmartHopper host and registered with
    /// <see cref="ProviderSdkHost.ProviderTrust"/> at startup.
    /// </summary>
    public interface IProviderTrustHost
    {
        /// <summary>
        /// Effective provider integrity check mode after applying environment overrides
        /// (for example, forcing Soft in DEBUG builds).
        /// </summary>
        ProviderIntegrityCheckMode EffectiveIntegrityCheckMode { get; }

        /// <summary>
        /// Returns true when the provider's local hash does not match the published manifest.
        /// </summary>
        bool IsProviderMismatched(string providerName);

        /// <summary>
        /// Returns true when integrity verification could not be performed (manifest
        /// unreachable, network issue, etc.).
        /// </summary>
        bool IsProviderUnavailable(string providerName);

        /// <summary>
        /// Returns true when the provider has no entry in the official manifest
        /// (custom/third-party provider).
        /// </summary>
        bool IsProviderUnknown(string providerName);

        /// <summary>
        /// Returns true when the provider was classified as community / non-official.
        /// </summary>
        bool IsProviderCommunity(string providerName);

        /// <summary>
        /// Returns true when the provider was loaded without any strong-name signature.
        /// </summary>
        bool IsProviderUnsigned(string providerName);
    }

    /// <summary>
    /// Default no-op implementation used when no host has registered a real trust host.
    /// Treats every provider as official and reports Soft integrity mode so SDK
    /// validation surfaces warnings rather than errors when running outside the host.
    /// </summary>
    public sealed class NullProviderTrustHost : IProviderTrustHost
    {
        /// <inheritdoc />
        public ProviderIntegrityCheckMode EffectiveIntegrityCheckMode => ProviderIntegrityCheckMode.Soft;

        /// <inheritdoc />
        public bool IsProviderMismatched(string providerName) => false;

        /// <inheritdoc />
        public bool IsProviderUnavailable(string providerName) => false;

        /// <inheritdoc />
        public bool IsProviderUnknown(string providerName) => false;

        /// <inheritdoc />
        public bool IsProviderCommunity(string providerName) => false;

        /// <inheritdoc />
        public bool IsProviderUnsigned(string providerName) => false;
    }
}
