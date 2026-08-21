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

namespace SmartHopper.ProviderSdk.Tests.AICall.Core.Interactions
{
    using SmartHopper.ProviderSdk.AICall.Core.Base;
    using SmartHopper.ProviderSdk.AICall.Core.Interactions;
    using SmartHopper.ProviderSdk.AICall.Metrics;
    using SmartHopper.ProviderSdk.AIModels;
    using SmartHopper.ProviderSdk.Tests.TestHelpers;
    using Xunit;

    /// <summary>
    /// Tests for <see cref="AIBody"/>.
    /// </summary>
    [Collection("ProviderSdk")]
    public class AIBodyTests
    {
#if NET7_WINDOWS
        [Fact(DisplayName = "Metrics_CombinesInteractions [Windows]")]
#else
        [Fact(DisplayName = "Metrics_CombinesInteractions [Core]")]
#endif
        public void Metrics_CombinesInteractions()
        {
            ProviderSdkTestHelper.ResetCapabilityRegistry();

            var first = new AIMetrics
            {
                Provider = "openai",
                Model = "gpt-test",
                InputTokensPrompt = 100,
                OutputTokensGeneration = 50,
            };

            var second = new AIMetrics
            {
                Provider = "openai",
                Model = "gpt-test",
                InputTokensPrompt = 50,
                OutputTokensGeneration = 25,
            };

            var body = AIBodyBuilder.Create()
                .AddText(AIAgent.Assistant, "first", first)
                .AddText(AIAgent.Assistant, "second", second)
                .Build();

            var metrics = body.Metrics;

            Assert.Equal(150, metrics.InputTokensPrompt);
            Assert.Equal(75, metrics.OutputTokensGeneration);
            Assert.Equal(225, metrics.TotalTokens);
            Assert.Equal("openai", metrics.Provider);
            Assert.Equal("gpt-test", metrics.Model);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "Metrics_CombinesEstimatedCost [Windows]")]
#else
        [Fact(DisplayName = "Metrics_CombinesEstimatedCost [Core]")]
#endif
        public void Metrics_CombinesEstimatedCost()
        {
            ProviderSdkTestHelper.ResetCapabilityRegistry();
            AIModelCapabilityRegistry.Instance.SetCapabilities(new AIModelCapabilities
            {
                Provider = "openai",
                Model = "gpt-test",
                Pricing = new AIModelPricing { Prompt = 0.000001m, Completion = 0.000004m },
            });

            var first = new AIMetrics
            {
                Provider = "openai",
                Model = "gpt-test",
                InputTokensPrompt = 100,
                OutputTokensGeneration = 50,
            };

            var second = new AIMetrics
            {
                Provider = "openai",
                Model = "gpt-test",
                InputTokensPrompt = 50,
                OutputTokensGeneration = 25,
            };

            var body = AIBodyBuilder.Create()
                .AddText(AIAgent.Assistant, "first", first)
                .AddText(AIAgent.Assistant, "second", second)
                .Build();

            var expected = (150m * 0.000001m) + (75m * 0.000004m);
            Assert.Equal(expected, body.Metrics.EstimatedCost);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "GetEffectiveTokenCount_UsesMaxOfActualAndEstimated [Windows]")]
#else
        [Fact(DisplayName = "GetEffectiveTokenCount_UsesMaxOfActualAndEstimated [Core]")]
#endif
        public void GetEffectiveTokenCount_UsesMaxOfActualAndEstimated()
        {
            var metrics = new AIMetrics
            {
                InputTokensPrompt = 100,
                OutputTokensGeneration = 50,
                EstimatedInputTokens = 500,
                EstimatedOutputTokens = 100,
            };

            var body = AIBodyBuilder.Create()
                .AddText(AIAgent.Assistant, "test", metrics)
                .Build();

            Assert.Equal(600, body.GetEffectiveTokenCount());
        }
    }
}
