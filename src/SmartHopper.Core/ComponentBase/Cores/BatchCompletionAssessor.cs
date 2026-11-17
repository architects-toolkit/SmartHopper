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

using System;
using System.Collections.Generic;
using System.Linq;

namespace SmartHopper.Core.ComponentBase.Cores
{
    /// <summary>
    /// Target lifecycle outcome of a batch completion, decided by
    /// <see cref="BatchCompletionAssessor"/> and mapped onto
    /// <see cref="State.ComponentState"/> by the component.
    /// </summary>
    internal enum BatchCompletionTarget
    {
        /// <summary>Every expected item reached a terminal outcome (resolved or provider-failed).</summary>
        Completed,

        /// <summary>Some expected items remain retryable — they re-execute on the next run.</summary>
        NeedsRun,

        /// <summary>No successful work exists, or the completion pipeline itself failed.</summary>
        Error,
    }

    /// <summary>
    /// Per-item outcome accounting and the resulting lifecycle target for a
    /// completed batch. Pure value — computed once by
    /// <see cref="BatchCompletionAssessor.Assess"/>.
    /// </summary>
    internal sealed class BatchCompletionAssessment
    {
        /// <summary>Expected ids whose sentinel was actually resolved inside its unit fragment.</summary>
        public int ResolvedCount { get; set; }

        /// <summary>
        /// Expected ids with no result or only a local decode failure — eligible for
        /// re-execution on the next run. Provider-failed ids are excluded: the provider
        /// answered definitively, so resubmitting them would only re-charge.
        /// </summary>
        public int UnresolvedRetryableCount { get; set; }

        /// <summary>True when the run produced no successful work at all → <see cref="BatchCompletionTarget.Error"/>.</summary>
        public bool AllFailed { get; set; }

        /// <summary>True when at least one expected item remains retryable → <see cref="BatchCompletionTarget.NeedsRun"/>.</summary>
        public bool HasUnresolvedSentinels => this.UnresolvedRetryableCount > 0;

        /// <summary>The lifecycle target the completion should transition to.</summary>
        public BatchCompletionTarget Target { get; set; }
    }

    /// <summary>
    /// Pure decision logic for <c>CompleteBatchAndTransition</c>: given the set of
    /// expected sentinel ids, per-id resolution/failure facts and the delivered
    /// result count, computes resolved/retryable counts and the lifecycle target.
    /// Kept Grasshopper-free so the accounting rules are directly unit-testable —
    /// the component only maps the target onto the state machine and surfaces
    /// diagnostics.
    /// </summary>
    internal static class BatchCompletionAssessor
    {
        /// <summary>
        /// Classifies a completed batch.
        /// </summary>
        /// <param name="expectedIds">
        /// Sentinel customIds submitted upstream, or <c>null</c> when the
        /// expectation is unknown — then delivered-body counting applies and no
        /// item is classified retryable.
        /// </param>
        /// <param name="isResolved">Predicate: did this expected id's sentinel get replaced inside its unit fragment?</param>
        /// <param name="providerFailedIds">Ids the provider reported as terminally failed; never retried.</param>
        /// <param name="resultCount">Delivered result bodies.</param>
        /// <param name="hasProviderErrors">Any provider/message severity-Error present.</param>
        /// <param name="decodeErrorCount">Locally recorded decode/finalization failures.</param>
        /// <param name="completionFailed">The completion pipeline itself crashed.</param>
        /// <returns>The assessment driving state transition and message severity.</returns>
        public static BatchCompletionAssessment Assess(
            IReadOnlyCollection<string> expectedIds,
            Func<string, bool> isResolved,
            IReadOnlyCollection<string> providerFailedIds,
            int resultCount,
            bool hasProviderErrors,
            int decodeErrorCount,
            bool completionFailed)
        {
            var hasResults = resultCount > 0;
            var hasErrors = hasProviderErrors || decodeErrorCount > 0;

            int resolvedCount;
            int unresolvedRetryableCount;
            if (expectedIds != null)
            {
                resolvedCount = expectedIds.Count(id => isResolved(id));
                unresolvedRetryableCount = expectedIds.Count(id =>
                    !isResolved(id) && !(providerFailedIds?.Contains(id) ?? false));
            }
            else
            {
                resolvedCount = Math.Max(0, resultCount - decodeErrorCount);
                unresolvedRetryableCount = 0;
            }

            var allFailed = completionFailed
                || (!hasResults && hasErrors)
                || (expectedIds?.Count > 0 && resolvedCount == 0 && hasErrors);

            var target = allFailed
                ? BatchCompletionTarget.Error
                : unresolvedRetryableCount > 0
                    ? BatchCompletionTarget.NeedsRun
                    : BatchCompletionTarget.Completed;

            return new BatchCompletionAssessment
            {
                ResolvedCount = resolvedCount,
                UnresolvedRetryableCount = unresolvedRetryableCount,
                AllFailed = allFailed,
                Target = target,
            };
        }
    }
}
