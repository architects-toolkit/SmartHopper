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
    /// Tests for <see cref="AICostCalculator"/>.
    /// </summary>
    [Collection("ProviderSdk")]
    public class AICostCalculatorTests
    {
#if NET7_WINDOWS
        [Fact(DisplayName = "Calculate_UnknownPricing_ReturnsZero [Windows]")]
#else
        [Fact(DisplayName = "Calculate_UnknownPricing_ReturnsZero [Core]")]
#endif
        public void Calculate_UnknownPricing_ReturnsZero()
        {
            ProviderSdkTestHelper.ResetCapabilityRegistry();

            var metrics = new AIMetrics
            {
                Provider = "openai",
                Model = "gpt-test",
                InputTokensPrompt = 1000,
                OutputTokensGeneration = 500,
            };

            var cost = AICostCalculator.Calculate(metrics);

            Assert.Equal(0m, cost);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "Calculate_NegativeOrFreePrices_ReturnsZero [Windows]")]
#else
        [Fact(DisplayName = "Calculate_NegativeOrFreePrices_ReturnsZero [Core]")]
#endif
        public void Calculate_NegativeOrFreePrices_ReturnsZero()
        {
            ProviderSdkTestHelper.ResetCapabilityRegistry();
            RegisterPricing("openai", "free-model", new AIModelPricing { Prompt = 0m, Completion = -1m });

            var metrics = new AIMetrics
            {
                Provider = "openai",
                Model = "free-model",
                InputTokensPrompt = 1000,
                OutputTokensGeneration = 500,
            };

            var cost = AICostCalculator.Calculate(metrics);

            Assert.Equal(0m, cost);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "Calculate_PromptAndCompletion_ReturnsExpectedCost [Windows]")]
#else
        [Fact(DisplayName = "Calculate_PromptAndCompletion_ReturnsExpectedCost [Core]")]
#endif
        public void Calculate_PromptAndCompletion_ReturnsExpectedCost()
        {
            ProviderSdkTestHelper.ResetCapabilityRegistry();
            RegisterPricing("openai", "gpt-test", new AIModelPricing
            {
                Prompt = 0.0000025m,
                Completion = 0.000010m,
            });

            var metrics = new AIMetrics
            {
                Provider = "openai",
                Model = "gpt-test",
                InputTokensPrompt = 1000,
                OutputTokensGeneration = 500,
            };

            var cost = AICostCalculator.Calculate(metrics);

            var expected = (1000m * 0.0000025m) + (500m * 0.000010m);
            Assert.Equal(expected, cost);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "Calculate_CacheBuckets_ArePriced [Windows]")]
#else
        [Fact(DisplayName = "Calculate_CacheBuckets_ArePriced [Core]")]
#endif
        public void Calculate_CacheBuckets_ArePriced()
        {
            ProviderSdkTestHelper.ResetCapabilityRegistry();
            RegisterPricing("anthropic", "claude-test", new AIModelPricing
            {
                Prompt = 0.000003m,
                InputCacheRead = 0.0000003m,
                InputCacheWrite = 0.00000375m,
                Completion = 0.000015m,
            });

            var metrics = new AIMetrics
            {
                Provider = "anthropic",
                Model = "claude-test",
                InputTokensPrompt = 100,
                InputTokensCached = 50,
                InputTokensCacheWrite = 20,
                OutputTokensGeneration = 30,
            };

            var cost = AICostCalculator.Calculate(metrics);

            var expected = (100m * 0.000003m)
                + (50m * 0.0000003m)
                + (20m * 0.00000375m)
                + (30m * 0.000015m);
            Assert.Equal(expected, cost);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "Calculate_ReasoningPricedWhenInternalReasoningIsSet [Windows]")]
#else
        [Fact(DisplayName = "Calculate_ReasoningPricedWhenInternalReasoningIsSet [Core]")]
#endif
        public void Calculate_ReasoningPricedWhenInternalReasoningIsSet()
        {
            ProviderSdkTestHelper.ResetCapabilityRegistry();
            RegisterPricing("google", "gemini-test", new AIModelPricing
            {
                Prompt = 0.000001m,
                Completion = 0.000004m,
                InternalReasoning = 0.000008m,
            });

            var metrics = new AIMetrics
            {
                Provider = "google",
                Model = "gemini-test",
                InputTokensPrompt = 1000,
                OutputTokensGeneration = 200,
                OutputTokensReasoning = 50,
            };

            var cost = AICostCalculator.Calculate(metrics);

            var expected = (1000m * 0.000001m)
                + (200m * 0.000004m)
                + (50m * 0.000008m);
            Assert.Equal(expected, cost);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "Calculate_MissingCategoryPrice_ContributesZero [Windows]")]
#else
        [Fact(DisplayName = "Calculate_MissingCategoryPrice_ContributesZero [Core]")]
#endif
        public void Calculate_MissingCategoryPrice_ContributesZero()
        {
            ProviderSdkTestHelper.ResetCapabilityRegistry();
            RegisterPricing("openai", "gpt-test", new AIModelPricing
            {
                Prompt = 0.000002m,
                Completion = 0.000006m,
            });

            var metrics = new AIMetrics
            {
                Provider = "openai",
                Model = "gpt-test",
                InputTokensPrompt = 100,
                InputTokensCached = 50,
                InputTokensCacheWrite = 20,
                OutputTokensGeneration = 30,
                OutputTokensReasoning = 10,
            };

            var cost = AICostCalculator.Calculate(metrics);

            var expected = (100m * 0.000002m) + (30m * 0.000006m);
            Assert.Equal(expected, cost);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "Calculate_EstimatedInputTokensFallback_WhenNoActualInput [Windows]")]
#else
        [Fact(DisplayName = "Calculate_EstimatedInputTokensFallback_WhenNoActualInput [Core]")]
#endif
        public void Calculate_EstimatedInputTokensFallback_WhenNoActualInput()
        {
            ProviderSdkTestHelper.ResetCapabilityRegistry();
            RegisterPricing("openai", "gpt-test", new AIModelPricing { Prompt = 0.000001m });

            var metrics = new AIMetrics
            {
                Provider = "openai",
                Model = "gpt-test",
                EstimatedInputTokens = 1234,
            };

            var cost = AICostCalculator.Calculate(metrics);

            Assert.Equal(1234m * 0.000001m, cost);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "Calculate_EstimatedOutputTokensFallback_WhenNoActualOutput [Windows]")]
#else
        [Fact(DisplayName = "Calculate_EstimatedOutputTokensFallback_WhenNoActualOutput [Core]")]
#endif
        public void Calculate_EstimatedOutputTokensFallback_WhenNoActualOutput()
        {
            ProviderSdkTestHelper.ResetCapabilityRegistry();
            RegisterPricing("openai", "gpt-test", new AIModelPricing { Completion = 0.000005m });

            var metrics = new AIMetrics
            {
                Provider = "openai",
                Model = "gpt-test",
                EstimatedOutputTokens = 567,
            };

            var cost = AICostCalculator.Calculate(metrics);

            Assert.Equal(567m * 0.000005m, cost);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "Calculate_FallsBackPerSide_IndependentBuckets [Windows]")]
#else
        [Fact(DisplayName = "Calculate_FallsBackPerSide_IndependentBuckets [Core]")]
#endif
        public void Calculate_FallsBackPerSide_IndependentBuckets()
        {
            ProviderSdkTestHelper.ResetCapabilityRegistry();
            RegisterPricing("openai", "gpt-test", new AIModelPricing
            {
                Prompt = 0.000001m,
                Completion = 0.000004m,
            });

            var metrics = new AIMetrics
            {
                Provider = "openai",
                Model = "gpt-test",
                InputTokensPrompt = 100,
                EstimatedOutputTokens = 200,
            };

            var cost = AICostCalculator.Calculate(metrics);

            var expected = (100m * 0.000001m) + (200m * 0.000004m);
            Assert.Equal(expected, cost);
        }

        private static void RegisterPricing(string provider, string model, AIModelPricing pricing)
        {
            AIModelCapabilityRegistry.Instance.SetCapabilities(new AIModelCapabilities
            {
                Provider = provider,
                Model = model,
                Pricing = pricing,
            });
        }
    }
}
