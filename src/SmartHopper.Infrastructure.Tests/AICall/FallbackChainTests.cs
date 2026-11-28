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

namespace SmartHopper.Infrastructure.Tests.AICall
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using SmartHopper.Infrastructure.AICall.Fallback;
    using SmartHopper.ProviderSdk.AICall.Core;
    using SmartHopper.ProviderSdk.AICall.Core.Interactions;
    using SmartHopper.ProviderSdk.AIModels;
    using SmartHopper.ProviderSdk.Diagnostics;
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

            public Task<ModalityFallbackResult> ApplyAsync(AIBody body, string providerName, string modelName, CancellationToken ct, AIRequestParameters requestParameters = null, ModalityFallbackContext context = null)
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

        /// <summary>
        /// A fallback that fails on configured candidates (error-severity message)
        /// and succeeds elsewhere; records every attempted provider/model pair.
        /// </summary>
        private sealed class SelectiveFallback : IModalityFallback
        {
            private readonly HashSet<string> failing;
            private readonly SHMessageCode failCode;

            public SelectiveFallback(params string[] failingProviders)
                : this(SHMessageCode.ConversionFailed, failingProviders)
            {
            }

            public SelectiveFallback(SHMessageCode failCode, params string[] failingProviders)
            {
                this.failing = new HashSet<string>(failingProviders, StringComparer.OrdinalIgnoreCase);
                this.failCode = failCode;
            }

            public List<(string Provider, string Model)> Attempts { get; } = new List<(string, string)>();

            public string Name => "Selective";

            public AICapability Handles => AICapability.ImageInput;

            public AICapability RequiresCapability => AICapability.Image2Text;

            public AICapability ResultsIn => AICapability.TextInput;

            public string Description => "converts images";

            public bool IsAvailable(string providerName) => true;

            public Task<ModalityFallbackResult> ApplyAsync(AIBody body, string providerName, string modelName, CancellationToken ct, AIRequestParameters requestParameters = null, ModalityFallbackContext context = null)
            {
                this.Attempts.Add((providerName, modelName));
                if (this.failing.Contains(providerName))
                {
                    return Task.FromResult(new ModalityFallbackResult
                    {
                        TransformedBody = body,
                        Messages = new List<SHRuntimeMessage>
                        {
                            new SHRuntimeMessage(
                                SHRuntimeMessageSeverity.Error,
                                SHRuntimeMessageOrigin.Provider,
                                this.failCode,
                                "model unavailable"),
                        },
                    });
                }

                return Task.FromResult(new ModalityFallbackResult { TransformedBody = body });
            }
        }

        private static FallbackStep Step(IModalityFallback fallback, params (string Provider, string Model)[] candidates)
            => new FallbackStep(fallback, candidates);

#if NET7_WINDOWS
        [Fact(DisplayName = "ApplyStepAsync_RetriesOnceOnNextCandidateAfterFailure [Windows]")]
#else
        [Fact(DisplayName = "ApplyStepAsync_RetriesOnceOnNextCandidateAfterFailure [Core]")]
#endif
        public async Task ApplyStepAsync_RetriesOnceOnNextCandidateAfterFailure()
        {
            var fallback = new SelectiveFallback("provider-a");
            var step = Step(fallback, ("provider-a", "m1"), ("provider-b", "m2"), ("provider-c", "m3"));

            var result = await FallbackChain.ApplyStepAsync(step, AIBody.Empty, CancellationToken.None);

            Assert.Equal(2, fallback.Attempts.Count);
            Assert.Equal("provider-a", fallback.Attempts[0].Provider);
            Assert.Equal("provider-b", fallback.Attempts[1].Provider);
            Assert.Contains(result.Messages, m => m.Code == SHMessageCode.ModalityFallbackApplied);
            Assert.DoesNotContain(result.Messages, m => m.Severity == SHRuntimeMessageSeverity.Error);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "ApplyStepAsync_FailsAfterTwoCandidates [Windows]")]
#else
        [Fact(DisplayName = "ApplyStepAsync_FailsAfterTwoCandidates [Core]")]
#endif
        public async Task ApplyStepAsync_FailsAfterTwoCandidates()
        {
            var fallback = new SelectiveFallback("provider-a", "provider-b");
            var step = Step(fallback, ("provider-a", "m1"), ("provider-b", "m2"), ("provider-c", "m3"));

            var result = await FallbackChain.ApplyStepAsync(step, AIBody.Empty, CancellationToken.None);

            // Third candidate is never reached: execution failover is bounded to two.
            Assert.Equal(2, fallback.Attempts.Count);
            Assert.Contains(result.Messages, m => m.Severity == SHRuntimeMessageSeverity.Error);
            Assert.DoesNotContain(result.Messages, m => m.Code == SHMessageCode.ModalityFallbackApplied);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "ApplyStepAsync_PreservesFailedAttemptMessages [Windows]")]
#else
        [Fact(DisplayName = "ApplyStepAsync_PreservesFailedAttemptMessages [Core]")]
#endif
        public async Task ApplyStepAsync_PreservesFailedAttemptMessages()
        {
            var fallback = new SelectiveFallback("provider-a");
            var step = Step(fallback, ("provider-a", "m1"), ("provider-b", "m2"));

            var result = await FallbackChain.ApplyStepAsync(step, AIBody.Empty, CancellationToken.None);

            Assert.Contains(result.Messages, m => m.Severity == SHRuntimeMessageSeverity.Warning);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "ApplyStepAsync_DoesNotRetryInputAttributableFailure [Windows]")]
#else
        [Fact(DisplayName = "ApplyStepAsync_DoesNotRetryInputAttributableFailure [Core]")]
#endif
        public async Task ApplyStepAsync_DoesNotRetryInputAttributableFailure()
        {
            // A failure attributed to the input itself fails identically on every
            // candidate — no second attempt should be spent on it.
            var fallback = new SelectiveFallback(SHMessageCode.InputInvalid, "provider-a");
            var step = Step(fallback, ("provider-a", "m1"), ("provider-b", "m2"));

            var result = await FallbackChain.ApplyStepAsync(step, AIBody.Empty, CancellationToken.None);

            Assert.Single(fallback.Attempts);
            Assert.DoesNotContain(result.Messages, m => m.Code == SHMessageCode.ModalityFallbackApplied);
        }
    }
}
