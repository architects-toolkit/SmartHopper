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

            Assert.Empty(cost);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "Calculate_NegativeOrFreePrices_ReturnsZero [Windows]")]
#else
        [Fact(DisplayName = "Calculate_NegativeOrFreePrices_ReturnsZero [Core]")]
#endif
        public void Calculate_NegativeOrFreePrices_ReturnsZero()
        {
            ProviderSdkTestHelper.ResetCapabilityRegistry();
            RegisterPricing("openai", "free-model", new AIModelPricing { TextInput = 0m, TextOutput = -1m });

            var metrics = new AIMetrics
            {
                Provider = "openai",
                Model = "free-model",
                InputTokensPrompt = 1000,
                OutputTokensGeneration = 500,
            };

            var cost = AICostCalculator.Calculate(metrics);

            Assert.Empty(cost);
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
                TextInput = 0.0000025m,
                TextOutput = 0.000010m,
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
            Assert.Equal(expected, cost["USD"]);
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
                TextInput = 0.000003m,
                CacheRead = 0.0000003m,
                CacheWrite = 0.00000375m,
                TextOutput = 0.000015m,
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
            Assert.Equal(expected, cost["USD"]);
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
                TextInput = 0.000001m,
                TextOutput = 0.000004m,
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
            Assert.Equal(expected, cost["USD"]);
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
                TextInput = 0.000002m,
                TextOutput = 0.000006m,
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
            Assert.Equal(expected, cost["USD"]);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "Calculate_EstimatedInputTokensFallback_WhenNoActualInput [Windows]")]
#else
        [Fact(DisplayName = "Calculate_EstimatedInputTokensFallback_WhenNoActualInput [Core]")]
#endif
        public void Calculate_EstimatedInputTokensFallback_WhenNoActualInput()
        {
            ProviderSdkTestHelper.ResetCapabilityRegistry();
            RegisterPricing("openai", "gpt-test", new AIModelPricing { TextInput = 0.000001m });

            var metrics = new AIMetrics
            {
                Provider = "openai",
                Model = "gpt-test",
                EstimatedInputTokens = 1234,
            };

            var cost = AICostCalculator.Calculate(metrics);

            Assert.Equal(1234m * 0.000001m, cost["USD"]);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "Calculate_EstimatedOutputTokensFallback_WhenNoActualOutput [Windows]")]
#else
        [Fact(DisplayName = "Calculate_EstimatedOutputTokensFallback_WhenNoActualOutput [Core]")]
#endif
        public void Calculate_EstimatedOutputTokensFallback_WhenNoActualOutput()
        {
            ProviderSdkTestHelper.ResetCapabilityRegistry();
            RegisterPricing("openai", "gpt-test", new AIModelPricing { TextOutput = 0.000005m });

            var metrics = new AIMetrics
            {
                Provider = "openai",
                Model = "gpt-test",
                EstimatedOutputTokens = 567,
            };

            var cost = AICostCalculator.Calculate(metrics);

            Assert.Equal(567m * 0.000005m, cost["USD"]);
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
                TextInput = 0.000001m,
                TextOutput = 0.000004m,
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
            Assert.Equal(expected, cost["USD"]);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "Calculate_BatchMetrics_UsesBatchPricing [Windows]")]
#else
        [Fact(DisplayName = "Calculate_BatchMetrics_UsesBatchPricing [Core]")]
#endif
        public void Calculate_BatchMetrics_UsesBatchPricing()
        {
            ProviderSdkTestHelper.ResetCapabilityRegistry();
            RegisterPricing("mistralai", "mistral-large-4", new AIModelPricing
            {
                TextInput = 0.0000007m,
                TextOutput = 0.0000021m,
            },
            new AIModelPricing
            {
                TextInput = 0.00000035m,
                TextOutput = 0.00000105m,
            });

            var metrics = new AIMetrics
            {
                Provider = "mistralai",
                Model = "mistral-large-4",
                IsBatch = true,
                InputTokensPrompt = 1000,
                OutputTokensGeneration = 500,
            };

            var cost = AICostCalculator.Calculate(metrics);

            var expected = (1000m * 0.00000035m) + (500m * 0.00000105m);
            Assert.Equal(expected, cost["USD"]);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "Calculate_NonBatchMetrics_UsesRegularPricing [Windows]")]
#else
        [Fact(DisplayName = "Calculate_NonBatchMetrics_UsesRegularPricing [Core]")]
#endif
        public void Calculate_NonBatchMetrics_UsesRegularPricing()
        {
            ProviderSdkTestHelper.ResetCapabilityRegistry();
            RegisterPricing("mistralai", "mistral-large-4", new AIModelPricing
            {
                TextInput = 0.0000007m,
                TextOutput = 0.0000021m,
            },
            new AIModelPricing
            {
                TextInput = 0.00000035m,
                TextOutput = 0.00000105m,
            });

            var nullFlag = new AIMetrics
            {
                Provider = "mistralai",
                Model = "mistral-large-4",
                InputTokensPrompt = 1000,
                OutputTokensGeneration = 500,
            };
            var falseFlag = nullFlag with { IsBatch = false };

            var expected = (1000m * 0.0000007m) + (500m * 0.0000021m);
            Assert.Equal(expected, AICostCalculator.Calculate(nullFlag)["USD"]);
            Assert.Equal(expected, AICostCalculator.Calculate(falseFlag)["USD"]);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "Calculate_BatchMetrics_WithoutBatchPricing_ReturnsEmpty [Windows]")]
#else
        [Fact(DisplayName = "Calculate_BatchMetrics_WithoutBatchPricing_ReturnsEmpty [Core]")]
#endif
        public void Calculate_BatchMetrics_WithoutBatchPricing_ReturnsEmpty()
        {
            ProviderSdkTestHelper.ResetCapabilityRegistry();
            RegisterPricing("openai", "no-batch-model", new AIModelPricing
            {
                TextInput = 0.000001m,
                TextOutput = 0.000002m,
            });

            var metrics = new AIMetrics
            {
                Provider = "openai",
                Model = "no-batch-model",
                IsBatch = true,
                InputTokensPrompt = 1000,
                OutputTokensGeneration = 500,
            };

            // Strict separation: a batch call must never fall back to regular
            // pricing — the estimate stays empty instead of billing full price.
            var cost = AICostCalculator.Calculate(metrics);

            Assert.Empty(cost);
        }

        private static void RegisterPricing(string provider, string model, AIModelPricing pricing, AIModelPricing batchPricing = null)
        {
            AIModelCapabilityRegistry.Instance.SetCapabilities(new AIModelCapabilities
            {
                Provider = provider,
                Model = model,
                Pricing = pricing,
                BatchPricing = batchPricing,
            });
        }
    }
}
