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

#if NET7_WINDOWS

namespace SmartHopper.ProviderSdk.Tests.Providers.MistralAI
{
    using System.Threading.Tasks;
    using SmartHopper.Providers.MistralAI;
    using SmartHopper.ProviderSdk.AICall.Batch;
    using SmartHopper.ProviderSdk.AICall.Core.Requests;
    using SmartHopper.ProviderSdk.AIModels;
    using Xunit;

    /// <summary>
    /// Unit tests for the MistralAI batch provider and <c>:batch</c> alias resolution.
    /// </summary>
    [Collection("ProviderSdk")]
    public class MistralAIBatchProviderTests
    {
        /// <summary>
        /// The MistralAI provider instance must implement <see cref="IAIBatchProvider"/>.
        /// </summary>
        [Fact]
        public void Provider_ImplementsBatchInterface()
        {
            var provider = MistralAIProvider.Instance;

            Assert.NotNull(provider);
            Assert.IsAssignableFrom<IAIBatchProvider>(provider);
        }

        /// <summary>
        /// <see cref="MistralAIProvider.PreCall(AIRequestCall)"/> must strip the
        /// <c>:batch</c> suffix from the model slug so the same model works for both
        /// chat and batch endpoints.
        /// </summary>
        [Fact]
        public void PreCall_StripsBatchSuffix()
        {
            var provider = MistralAIProvider.Instance;
            var request = new AIRequestCall
            {
                Provider = "MistralAI",
                Model = "mistral-small-2603:batch",
                Capability = SmartHopper.ProviderSdk.AIModels.AICapability.None,
            };

            var prepared = provider.PreCall(request);

            Assert.Equal("mistral-small-2603", prepared.Model);
        }

        /// <summary>
        /// Resolving a model through the <c>:batch</c> alias should return the canonical
        /// model with batch support.
        /// </summary>
        [Fact]
        public async Task Registry_ResolvesBatchAliasToCanonicalModel()
        {
            var provider = MistralAIProvider.Instance;
            var registry = AIModelCapabilityRegistry.Instance;
            var models = await provider.Models.RetrieveModels().ConfigureAwait(false);

            foreach (var model in models)
            {
                registry.SetCapabilities(model);
            }

            var viaAlias = registry.GetCapabilities("MistralAI", "mistral-small-2603:batch");
            var viaBase = registry.GetCapabilities("MistralAI", "mistral-small-2603");

            Assert.NotNull(viaAlias);
            Assert.NotNull(viaBase);
            Assert.Same(viaAlias, viaBase);
            Assert.True(viaAlias.SupportsBatch);
            Assert.NotNull(viaAlias.BatchPricing);
        }
    }
}

#endif
