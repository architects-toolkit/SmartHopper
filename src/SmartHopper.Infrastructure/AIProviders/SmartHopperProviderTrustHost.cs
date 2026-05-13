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

using SmartHopper.Infrastructure.Settings;
using SmartHopper.ProviderSdk.Hosting;

namespace SmartHopper.Infrastructure.AIProviders
{
    /// <summary>
    /// Host-side adapter wiring <see cref="ProviderManager"/> and
    /// <see cref="SmartHopperSettings"/> into the SDK's
    /// <see cref="IProviderTrustHost"/> abstraction.
    /// </summary>
    public sealed class SmartHopperProviderTrustHost : IProviderTrustHost
    {
        /// <inheritdoc />
        public ProviderIntegrityCheckMode EffectiveIntegrityCheckMode
            => SmartHopperSettings.Instance.EffectiveProviderIntegrityCheckMode;

        /// <inheritdoc />
        public bool IsProviderMismatched(string providerName)
            => ProviderManager.Instance.IsProviderMismatched(providerName);

        /// <inheritdoc />
        public bool IsProviderUnavailable(string providerName)
            => ProviderManager.Instance.IsProviderUnavailable(providerName);

        /// <inheritdoc />
        public bool IsProviderUnknown(string providerName)
            => ProviderManager.Instance.IsProviderUnknown(providerName);

        /// <inheritdoc />
        public bool IsProviderCommunity(string providerName)
            => ProviderManager.Instance.IsProviderCommunity(providerName);

        /// <inheritdoc />
        public bool IsProviderUnsigned(string providerName)
            => ProviderManager.Instance.IsProviderUnsigned(providerName);
    }
}
