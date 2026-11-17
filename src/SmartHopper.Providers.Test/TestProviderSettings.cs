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

using System.Collections.Generic;
using System.Linq;
using SmartHopper.ProviderSdk.AIProviders;
using SmartHopper.ProviderSdk.Settings;

namespace SmartHopper.Providers.Test
{
    /// <summary>
    /// Settings for <see cref="TestProvider"/>: the provider needs no
    /// configuration (no credentials, no endpoint), so the descriptor surface
    /// is empty and validation always succeeds.
    /// </summary>
    public class TestProviderSettings : AIProviderSettings
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TestProviderSettings"/> class.
        /// </summary>
        /// <param name="provider">The test provider instance.</param>
        public TestProviderSettings(TestProvider provider)
            : base(provider)
        {
        }

        /// <inheritdoc/>
        public override IEnumerable<SettingDescriptor> GetSettingDescriptors()
            => Enumerable.Empty<SettingDescriptor>();

        /// <inheritdoc/>
        public override bool ValidateSettings(Dictionary<string, object> settings) => true;
    }
}
