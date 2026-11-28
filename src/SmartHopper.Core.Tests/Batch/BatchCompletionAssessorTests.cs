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

namespace SmartHopper.Core.Tests.Batch
{
    using System.Collections.Generic;
    using SmartHopper.Core.ComponentBase.Cores;
    using Xunit;

    /// <summary>
    /// Unit tests for <see cref="BatchCompletionAssessor"/> — the pure
    /// per-outcome accounting and lifecycle-target decision behind
    /// <c>CompleteBatchAndTransition</c>.
    /// </summary>
    public class BatchCompletionAssessorTests
    {
        private static readonly IReadOnlyCollection<string> Expected = new List<string> { "a", "b", "c" }.AsReadOnly();

#if NET7_WINDOWS
        [Fact(DisplayName = "Assessor returns Completed when every expected id resolved [Windows]")]
#else
        [Fact(DisplayName = "Assessor returns Completed when every expected id resolved [Core]")]
#endif
        public void Assess_AllResolved_Completed()
        {
            var resolved = new HashSet<string> { "a", "b", "c" };

            var result = BatchCompletionAssessor.Assess(
                Expected, resolved.Contains, new HashSet<string>(),
                resultCount: 3, hasProviderErrors: false, decodeErrorCount: 0, completionFailed: false);

            Assert.Equal(3, result.ResolvedCount);
            Assert.Equal(0, result.UnresolvedRetryableCount);
            Assert.False(result.AllFailed);
            Assert.Equal(BatchCompletionTarget.Completed, result.Target);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "Assessor returns NeedsRun for missing ids [Windows]")]
#else
        [Fact(DisplayName = "Assessor returns NeedsRun for missing ids [Core]")]
#endif
        public void Assess_MissingIds_NeedsRun()
        {
            var resolved = new HashSet<string> { "a", "b" };

            var result = BatchCompletionAssessor.Assess(
                Expected, resolved.Contains, new HashSet<string>(),
                resultCount: 2, hasProviderErrors: false, decodeErrorCount: 0, completionFailed: false);

            Assert.Equal(2, result.ResolvedCount);
            Assert.Equal(1, result.UnresolvedRetryableCount);
            Assert.True(result.HasUnresolvedSentinels);
            Assert.Equal(BatchCompletionTarget.NeedsRun, result.Target);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "Assessor treats provider-failed ids as terminal, not retryable [Windows]")]
#else
        [Fact(DisplayName = "Assessor treats provider-failed ids as terminal, not retryable [Core]")]
#endif
        public void Assess_ProviderFailedIds_AreTerminalNotRetryable()
        {
            var resolved = new HashSet<string> { "a", "b" };
            var providerFailed = new HashSet<string> { "c" };

            var result = BatchCompletionAssessor.Assess(
                Expected, resolved.Contains, providerFailed,
                resultCount: 2, hasProviderErrors: true, decodeErrorCount: 0, completionFailed: false);

            Assert.Equal(2, result.ResolvedCount);
            Assert.Equal(0, result.UnresolvedRetryableCount);
            Assert.False(result.AllFailed);
            Assert.Equal(BatchCompletionTarget.Completed, result.Target);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "Assessor returns Error when the whole batch failed [Windows]")]
#else
        [Fact(DisplayName = "Assessor returns Error when the whole batch failed [Core]")]
#endif
        public void Assess_AllFailed_Error()
        {
            var resolved = new HashSet<string>();

            var result = BatchCompletionAssessor.Assess(
                Expected, resolved.Contains, new HashSet<string>(),
                resultCount: 0, hasProviderErrors: true, decodeErrorCount: 0, completionFailed: false);

            Assert.True(result.AllFailed);
            Assert.Equal(BatchCompletionTarget.Error, result.Target);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "Assessor returns Error when every expected id failed at the provider [Windows]")]
#else
        [Fact(DisplayName = "Assessor returns Error when every expected id failed at the provider [Core]")]
#endif
        public void Assess_EveryExpectedIdFailed_Error()
        {
            // Provider reported errors and nothing resolved: expected>0 &&
            // resolved==0 && hasErrors → all-failed, even though each item has
            // a terminal outcome.
            var resolved = new HashSet<string>();
            var providerFailed = new HashSet<string> { "a", "b", "c" };

            var result = BatchCompletionAssessor.Assess(
                Expected, resolved.Contains, providerFailed,
                resultCount: 0, hasProviderErrors: true, decodeErrorCount: 0, completionFailed: false);

            Assert.True(result.AllFailed);
            Assert.Equal(BatchCompletionTarget.Error, result.Target);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "Assessor returns Error when the completion pipeline crashed [Windows]")]
#else
        [Fact(DisplayName = "Assessor returns Error when the completion pipeline crashed [Core]")]
#endif
        public void Assess_CompletionFailed_Error()
        {
            var resolved = new HashSet<string> { "a", "b", "c" };

            var result = BatchCompletionAssessor.Assess(
                Expected, resolved.Contains, new HashSet<string>(),
                resultCount: 3, hasProviderErrors: false, decodeErrorCount: 0, completionFailed: true);

            Assert.True(result.AllFailed);
            Assert.Equal(BatchCompletionTarget.Error, result.Target);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "Assessor counts delivered bodies when expectation is unknown [Windows]")]
#else
        [Fact(DisplayName = "Assessor counts delivered bodies when expectation is unknown [Core]")]
#endif
        public void Assess_NullExpectation_DeliveredBodyCounting()
        {
            var result = BatchCompletionAssessor.Assess(
                null, _ => false, new HashSet<string>(),
                resultCount: 3, hasProviderErrors: false, decodeErrorCount: 1, completionFailed: false);

            Assert.Equal(2, result.ResolvedCount);
            Assert.Equal(0, result.UnresolvedRetryableCount);
            Assert.False(result.AllFailed);
            Assert.Equal(BatchCompletionTarget.Completed, result.Target);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "Assessor retries an empty batch with no errors [Windows]")]
#else
        [Fact(DisplayName = "Assessor retries an empty batch with no errors [Core]")]
#endif
        public void Assess_EmptyBatchNoErrors_NeedsRun()
        {
            var result = BatchCompletionAssessor.Assess(
                Expected,
                _ => false,
                new HashSet<string>(),
                resultCount: 0,
                hasProviderErrors: false,
                decodeErrorCount: 0,
                completionFailed: false);

            // expected>0, resolved==0 but NO errors → not all-failed; the
            // unresolved items are retryable → NeedsRun, not Completed.
            Assert.False(result.AllFailed);
            Assert.Equal(3, result.UnresolvedRetryableCount);
            Assert.Equal(BatchCompletionTarget.NeedsRun, result.Target);
        }
    }
}
