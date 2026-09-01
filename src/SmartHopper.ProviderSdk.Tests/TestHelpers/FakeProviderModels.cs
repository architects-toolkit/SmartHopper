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
    using System.Threading.Tasks;
    using SmartHopper.ProviderSdk.AIModels;

    /// <summary>
    /// In-memory model metadata source used by <see cref="FakeAIProvider"/>.
    /// </summary>
    public sealed class FakeProviderModels : IAIProviderModels
    {
        /// <inheritdoc />
        public Task<List<AIModelCapabilities>> RetrieveModels()
        {
            return Task.FromResult(new List<AIModelCapabilities>
            {
                new AIModelCapabilities
                {
                    Provider = FakeAIProvider.ProviderName,
                    Model = "fake-model",
                    Capabilities = AICapability.Text2Text,
                    Default = AICapability.Text2Text,
                },
            });
        }

        /// <inheritdoc />
        public Task<List<string>> RetrieveApiModels()
        {
            return Task.FromResult(new List<string> { "fake-model" });
        }
    }
}
