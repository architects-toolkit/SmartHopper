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

using SmartHopper.ProviderSdk.AIProviders;

namespace SmartHopper.ProviderSdk.Hosting
{
    /// <summary>
    /// Surface that the SDK consumes to look up provider instances and their settings
    /// objects without depending on the host's <c>ProviderManager</c>. The SmartHopper
    /// host registers a real implementation with <see cref="ProviderSdkHost.ProviderRegistry"/>.
    /// </summary>
    public interface IProviderRegistryHost
    {
        /// <summary>
        /// Resolve a registered provider by name, or null if the host has not loaded one.
        /// </summary>
        IAIProvider GetProvider(string providerName);

        /// <summary>
        /// Resolve the live <see cref="IAIProviderSettings"/> bound to the named provider, or
        /// null when the provider is not registered.
        /// </summary>
        IAIProviderSettings GetProviderSettings(string providerName);
    }

    /// <summary>
    /// Null implementation that always returns <c>null</c>. Used when SDK code runs
    /// outside a host (for example, unit tests covering pure DTO behavior).
    /// </summary>
    public sealed class NullProviderRegistryHost : IProviderRegistryHost
    {
        /// <inheritdoc />
        public IAIProvider GetProvider(string providerName) => null;

        /// <inheritdoc />
        public IAIProviderSettings GetProviderSettings(string providerName) => null;
    }
}
