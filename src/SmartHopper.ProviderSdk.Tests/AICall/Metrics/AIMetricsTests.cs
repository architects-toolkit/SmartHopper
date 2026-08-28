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

namespace SmartHopper.ProviderSdk.Tests.AICall.Metrics
{
    using SmartHopper.ProviderSdk.AICall.Metrics;
    using SmartHopper.ProviderSdk.AIModels;
    using SmartHopper.ProviderSdk.Tests.TestHelpers;
    using Xunit;

    /// <summary>
    /// Tests for <see cref="AIMetrics"/>.
    /// </summary>
    [Collection("ProviderSdk")]
    public class AIMetricsTests
    {
#if NET7_WINDOWS
        [Fact(DisplayName = "InputTokens_SumsInputBuckets [Windows]")]
#else
        [Fact(DisplayName = "InputTokens_SumsInputBuckets [Core]")]
#endif
        public void InputTokens_SumsInputBuckets()
        {
            var metrics = new AIMetrics
            {
                InputTokensPrompt = 100,
                InputTokensCached = 50,
                InputTokensCacheWrite = 25,
            };

            Assert.Equal(175, metrics.InputTokens);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "OutputTokens_SumsOutputBuckets [Windows]")]
#else
        [Fact(DisplayName = "OutputTokens_SumsOutputBuckets [Core]")]
#endif
        public void OutputTokens_SumsOutputBuckets()
        {
            var metrics = new AIMetrics
            {
                OutputTokensGeneration = 80,
                OutputTokensReasoning = 20,
            };

            Assert.Equal(100, metrics.OutputTokens);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "TotalTokens_SumsInputAndOutput [Windows]")]
#else
        [Fact(DisplayName = "TotalTokens_SumsInputAndOutput [Core]")]
#endif
        public void TotalTokens_SumsInputAndOutput()
        {
            var metrics = new AIMetrics
            {
                InputTokensPrompt = 100,
                OutputTokensGeneration = 50,
            };

            Assert.Equal(150, metrics.TotalTokens);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "EffectiveTotalTokens_UsesMaximum [Windows]")]
#else
        [Fact(DisplayName = "EffectiveTotalTokens_UsesMaximum [Core]")]
#endif
        public void EffectiveTotalTokens_UsesMaximum()
        {
            var metrics = new AIMetrics
            {
                InputTokensPrompt = 100,
                OutputTokensGeneration = 50,
                EstimatedInputTokens = 500,
                EstimatedOutputTokens = 100,
            };

            Assert.Equal(600, metrics.EffectiveTotalTokens);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "IsValid_ValidMetrics [Windows]")]
#else
        [Fact(DisplayName = "IsValid_ValidMetrics [Core]")]
#endif
        public void IsValid_ValidMetrics()
        {
            var metrics = new AIMetrics
            {
                Provider = "openai",
                Model = "gpt-test",
                FinishReason = "stop",
                InputTokensPrompt = 10,
                OutputTokensGeneration = 5,
            };

            Assert.True(metrics.IsValid().IsValid);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "IsValid_InvalidMetrics [Windows]")]
#else
        [Fact(DisplayName = "IsValid_InvalidMetrics [Core]")]
#endif
        public void IsValid_InvalidMetrics()
        {
            var metrics = new AIMetrics
            {
                Provider = "openai",
                Model = "gpt-test",
                InputTokensPrompt = -1,
            };

            Assert.False(metrics.IsValid().IsValid);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "Combine_MergesAndAddsTokenCounts [Windows]")]
#else
        [Fact(DisplayName = "Combine_MergesAndAddsTokenCounts [Core]")]
#endif
        public void Combine_MergesAndAddsTokenCounts()
        {
            var first = new AIMetrics
            {
                Provider = "openai",
                Model = "gpt-4",
                InputTokensPrompt = 100,
                OutputTokensGeneration = 50,
            };

            var second = new AIMetrics
            {
                Provider = "openai",
                Model = "gpt-4",
                InputTokensPrompt = 50,
                OutputTokensGeneration = 25,
            };

            first = first.WithCombined(second);

            Assert.Equal(150, first.InputTokensPrompt);
            Assert.Equal(75, first.OutputTokensGeneration);
            Assert.Equal(225, first.TotalTokens);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "Combine_SumsEstimatedCost [Windows]")]
#else
        [Fact(DisplayName = "Combine_SumsEstimatedCost [Core]")]
#endif
        public void Combine_SumsEstimatedCost()
        {
            ProviderSdkTestHelper.ResetCapabilityRegistry();
            RegisterPricing("openai", "gpt-test", 0.000001m, 0.000004m);

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

            var firstCost = first.EstimatedCost;
            var secondCost = second.EstimatedCost;

            first = first.WithCombined(second);

            Assert.Equal(firstCost + secondCost, first.EstimatedCost);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "EstimatedCost_CachesAfterFirstAccess [Windows]")]
#else
        [Fact(DisplayName = "EstimatedCost_CachesAfterFirstAccess [Core]")]
#endif
        public void EstimatedCost_CachesAfterFirstAccess()
        {
            ProviderSdkTestHelper.ResetCapabilityRegistry();
            RegisterPricing("openai", "gpt-test", 0.000001m, 0.000004m);

            var metrics = new AIMetrics
            {
                Provider = "openai",
                Model = "gpt-test",
                InputTokensPrompt = 100,
                OutputTokensGeneration = 50,
            };

            var cost1 = metrics.EstimatedCost;
            var cost2 = metrics.EstimatedCost;

            Assert.Equal(cost1, cost2);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "ContextUsagePercent_ComputesFromContextLimit [Windows]")]
#else
        [Fact(DisplayName = "ContextUsagePercent_ComputesFromContextLimit [Core]")]
#endif
        public void ContextUsagePercent_ComputesFromContextLimit()
        {
            ProviderSdkTestHelper.ResetCapabilityRegistry();
            AIModelCapabilityRegistry.Instance.SetCapabilities(new AIModelCapabilities
            {
                Provider = "openai",
                Model = "gpt-test",
                ContextLimit = 10000,
            });

            var metrics = new AIMetrics
            {
                Provider = "openai",
                Model = "gpt-test",
                InputTokensPrompt = 1000,
                OutputTokensGeneration = 500,
            };

            Assert.NotNull(metrics.ContextUsagePercent);
            Assert.Equal(0.15, metrics.ContextUsagePercent.Value, 4);
        }

        private static void RegisterPricing(string provider, string model, decimal prompt, decimal completion)
        {
            AIModelCapabilityRegistry.Instance.SetCapabilities(new AIModelCapabilities
            {
                Provider = provider,
                Model = model,
                Pricing = new AIModelPricing { Prompt = prompt, Completion = completion },
            });
        }
    }
}
