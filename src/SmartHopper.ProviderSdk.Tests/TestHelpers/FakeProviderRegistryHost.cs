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

namespace SmartHopper.ProviderSdk.Tests.TestHelpers
{
    using System.Collections.Generic;
    using SmartHopper.ProviderSdk.AIProviders;
    using SmartHopper.ProviderSdk.Hosting;
    using SmartHopper.ProviderSdk.Settings;

    /// <summary>
    /// Test registry that returns a configurable provider and an empty settings descriptor set.
    /// </summary>
    public sealed class FakeProviderRegistryHost : IProviderRegistryHost
    {
        private readonly AIProvider provider;

        /// <summary>
        /// Initializes a new instance of the <see cref="FakeProviderRegistryHost"/> class.
        /// </summary>
        /// <param name="provider">The provider to return for lookups.</param>
        public FakeProviderRegistryHost(AIProvider provider)
        {
            this.provider = provider;
        }

        /// <inheritdoc />
        public IAIProvider GetProvider(string providerName)
        {
            return this.provider;
        }

        /// <inheritdoc />
        public IAIProviderSettings GetProviderSettings(string providerName)
        {
            return new EmptyProviderSettings();
        }

        private sealed class EmptyProviderSettings : IAIProviderSettings
        {
            public bool EnableStreaming => true;

            public IEnumerable<SettingDescriptor> GetSettingDescriptors()
            {
                return new List<SettingDescriptor>();
            }

            public bool ValidateSettings(Dictionary<string, object> settings)
            {
                return true;
            }
        }
    }
}
