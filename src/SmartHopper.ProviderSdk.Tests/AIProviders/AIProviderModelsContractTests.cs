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

namespace SmartHopper.ProviderSdk.Tests.AIProviders
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using SmartHopper.ProviderSdk.AIModels;
    using SmartHopper.ProviderSdk.AIProviders;
    using SmartHopper.ProviderSdk.Tests.TestHelpers;
    using Xunit;

    /// <summary>
    /// Contract tests for <see cref="IAIProviderModels.SupportsApiModels"/>: providers
    /// without a live models API declare so explicitly instead of signalling failure
    /// through an empty <see cref="IAIProviderModels.RetrieveApiModels"/> result.
    /// </summary>
    [Collection("ProviderSdk")]
    public sealed class AIProviderModelsContractTests
    {
#if NET7_WINDOWS
        private const string PlatformSuffix = " [Windows]";
#else
        private const string PlatformSuffix = " [Core]";
#endif

        [Fact(DisplayName = nameof(Base_SupportsApiModels_DefaultsFalse) + PlatformSuffix)]
        public void Base_SupportsApiModels_DefaultsFalse()
        {
            var models = new NoApiProviderModels(new FakeAIProvider());

            Assert.False(models.SupportsApiModels);
        }

        [Fact(DisplayName = nameof(Base_RetrieveApiModels_ReturnsEmpty) + PlatformSuffix)]
        public async Task Base_RetrieveApiModels_ReturnsEmpty()
        {
            var models = new NoApiProviderModels(new FakeAIProvider());

            var result = await models.RetrieveApiModels();

            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact(DisplayName = nameof(Fake_SupportsApiModels_OptsIn) + PlatformSuffix)]
        public void Fake_SupportsApiModels_OptsIn()
        {
            var models = new FakeProviderModels();

            Assert.True(models.SupportsApiModels);
        }

        /// <summary>
        /// Minimal concrete <see cref="AIProviderModels"/> that keeps the base behavior,
        /// standing in for providers without a live models API (e.g. Ollama, LocalAI).
        /// </summary>
        private sealed class NoApiProviderModels : AIProviderModels
        {
            public NoApiProviderModels(FakeAIProvider provider)
                : base(provider)
            {
            }

            public override Task<List<AIModelCapabilities>> RetrieveModels()
            {
                return Task.FromResult(new List<AIModelCapabilities>());
            }
        }
    }
}
