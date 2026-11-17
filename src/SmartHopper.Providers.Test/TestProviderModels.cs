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
using System.Threading.Tasks;
using SmartHopper.ProviderSdk.AIModels;
using SmartHopper.ProviderSdk.AIProviders;

namespace SmartHopper.Providers.Test
{
    /// <summary>
    /// Static model catalogue for <see cref="TestProvider"/>: a single
    /// batch-capable text model so lifecycle scenarios never depend on live
    /// provider catalogues.
    /// </summary>
    public class TestProviderModels : AIProviderModels
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TestProviderModels"/> class.
        /// </summary>
        /// <param name="provider">The test provider instance.</param>
        public TestProviderModels(TestProvider provider)
            : base(provider)
        {
        }

        /// <inheritdoc/>
        public override Task<List<AIModelCapabilities>> RetrieveModels()
        {
            var models = new List<AIModelCapabilities>
            {
                new AIModelCapabilities
                {
                    Provider = this.Provider.Name,
                    Model = TestProvider.BatchModel,
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput,
                    Default = AICapability.Text2Text | AICapability.Text2Json,
                    SupportsBatch = true,
                    Verified = true,
                    Rank = 0,
                },
            };

            return Task.FromResult(models);
        }
    }
}
