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
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public
 * License along with this program. If not, see <https://www.gnu.org/licenses/lgpl-3.0.html>.
 */

namespace SmartHopper.Infrastructure.Tests.AICall
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using SmartHopper.Infrastructure.AICall.Fallback;
    using SmartHopper.ProviderSdk.AICall.Core.Interactions;
    using SmartHopper.ProviderSdk.AIModels;
    using Xunit;

    /// <summary>
    /// Tests for <see cref="FallbackChain"/> step execution semantics.
    /// </summary>
    public class FallbackChainTests
    {
        /// <summary>
        /// A fallback that records the provider/model it was invoked with,
        /// so tests can verify per-step resolution instead of chain-level values.
        /// </summary>
        private sealed class RecordingFallback : IModalityFallback
        {
            public string RecordedProvider { get; private set; }

            public string RecordedModel { get; private set; }

            public string Name => "Recording";

            public AICapability Handles => AICapability.ImageInput;

            public AICapability RequiresCapability => AICapability.Image2Text;

            public AICapability ResultsIn => AICapability.TextInput;

            public string Description => "records invocation";

            public bool IsAvailable(string providerName) => true;

            public Task<ModalityFallbackResult> ApplyAsync(AIBody body, string providerName, string modelName, CancellationToken ct)
            {
                this.RecordedProvider = providerName;
                this.RecordedModel = modelName;
                return Task.FromResult(new ModalityFallbackResult { TransformedBody = body });
            }
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "ApplyAsync_RunsEachStepOnItsResolvedProviderAndModel [Windows]")]
#else
        [Fact(DisplayName = "ApplyAsync_RunsEachStepOnItsResolvedProviderAndModel [Core]")]
#endif
        public async Task ApplyAsync_RunsEachStepOnItsResolvedProviderAndModel()
        {
            var firstStep = new RecordingFallback();
            var secondStep = new RecordingFallback();
            var chain = new FallbackChain(
                new List<FallbackStep>
                {
                    new FallbackStep(firstStep, "provider-a", "model-a"),
                    new FallbackStep(secondStep, "provider-b", "model-b"),
                },
                "image and audio converted",
                AICapability.TextInput);

            var result = await chain.ApplyAsync(AIBody.Empty, CancellationToken.None);

            Assert.Equal("provider-a", firstStep.RecordedProvider);
            Assert.Equal("model-a", firstStep.RecordedModel);
            Assert.Equal("provider-b", secondStep.RecordedProvider);
            Assert.Equal("model-b", secondStep.RecordedModel);
            Assert.NotNull(result.TransformedBody);
        }
    }
}
