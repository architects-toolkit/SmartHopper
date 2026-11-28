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
using System.Threading;
using System.Threading.Tasks;
using SmartHopper.ProviderSdk.AICall.Core;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AICall.Metrics;
using SmartHopper.ProviderSdk.AIModels;
using SmartHopper.ProviderSdk.Diagnostics;
using SmartHopper.ProviderSdk.Logging;
namespace SmartHopper.Infrastructure.AICall.Fallback
{
    /// <summary>
    /// An ordered list of fallback steps that convert unsupported modalities
    /// into supported ones. Each step carries the provider and model resolved
    /// for it, so a multi-modality chain may span providers.
    /// </summary>
    public sealed class FallbackChain
    {
        /// <summary>
        /// Maximum attempts per step: a failed candidate may be retried on the next
        /// resolved candidate exactly once. Selection alternates are resolution-time
        /// data; the retry bound is an execution policy kept in one place.
        /// </summary>
        public const int MaxAttemptsPerStep = 2;

        /// <summary>
        /// Initializes a new instance of the <see cref="FallbackChain"/> class.
        /// </summary>
        public FallbackChain(
            IReadOnlyList<FallbackStep> steps,
            string description,
            AICapability effectiveCapability)
        {
            this.Steps = steps;
            this.Description = description;
            this.EffectiveCapability = effectiveCapability;
        }

        /// <summary>Ordered fallback steps, each with its own resolved provider/model.</summary>
        public IReadOnlyList<FallbackStep> Steps { get; }

        /// <summary>Joined step descriptions for the warning message.</summary>
        public string Description { get; }

        /// <summary>True when at least one step runs on a provider different from the
        /// component's configured one.</summary>
        public bool UsesAltProvider { get; init; }

        /// <summary>Capability after all steps applied.</summary>
        public AICapability EffectiveCapability { get; }

        /// <summary>
        /// Applies all steps in order, transforming the body and collecting metrics.
        /// Each step runs on its own resolved provider/model candidates.
        /// </summary>
        /// <param name="body">The canonical body to transform.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <param name="requestParameters">Optional request parameters propagated to each step's conversion calls.</param>
        /// <param name="context">Optional execution-scoped context shared across steps.</param>
        public async Task<ModalityFallbackResult> ApplyAsync(AIBody body, CancellationToken ct, AIRequestParameters requestParameters = null, ModalityFallbackContext context = null)
        {
            var combinedResult = new ModalityFallbackResult { TransformedBody = body };

            foreach (var step in this.Steps)
            {
                ModalityFallbackResult stepResult;
                try
                {
                    stepResult = await ApplyStepAsync(
                        step,
                        combinedResult.TransformedBody,
                        ct,
                        requestParameters,
                        context).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // A later step must not discard metrics and messages already charged by
                    // earlier steps/images: surface them on the returned result with a
                    // blocking cancellation diagnostic instead of propagating bare.
                    if (combinedResult.ExtraMetricsList.Count == 0 && combinedResult.Messages.Count == 0)
                    {
                        throw;
                    }

                    combinedResult.Messages.Add(new SHRuntimeMessage(
                        SHRuntimeMessageSeverity.Error,
                        SHRuntimeMessageOrigin.Provider,
                        SHMessageCode.ConversionFailed,
                        "Modality fallback was cancelled after paid helper work; the partial result is not submittable."));
                    return combinedResult;
                }

                combinedResult.TransformedBody = stepResult.TransformedBody ?? combinedResult.TransformedBody;
                combinedResult.ExtraMetricsList.AddRange(stepResult.ExtraMetricsList);
                combinedResult.Messages.AddRange(stepResult.Messages);
            }

            return combinedResult;
        }

        /// <summary>
        /// Message codes that attribute a failure to the input itself rather than to
        /// the candidate provider/model. These fail identically on every candidate, so
        /// executing another attempt would just spend quota for the same verdict.
        /// </summary>
        private static readonly HashSet<SHMessageCode> NonRetryableCodes = new HashSet<SHMessageCode>
        {
            SHMessageCode.InputMissing,
            SHMessageCode.InputInvalid,
            SHMessageCode.BodyInvalid,
        };

        /// <summary>
        /// Executes one step against its ordered candidates with bounded failover:
        /// a candidate whose result carries a retryable error is retried once on the
        /// next candidate, up to <see cref="MaxAttemptsPerStep"/> total attempts.
        /// Metrics and diagnostics from every attempt are preserved; output items come
        /// only from the standing attempt so failed-attempt fragments are never mixed
        /// into the chain result. A success appends a surfaceable remark naming the
        /// target actually used.
        /// </summary>
        /// <param name="step">The step to execute.</param>
        /// <param name="body">The body the step must transform (each attempt sees this same input).</param>
        /// <param name="ct">Cancellation token.</param>
        /// <param name="requestParameters">Optional request parameters propagated to the conversion call.</param>
        /// <param name="context">Optional execution-scoped context (run memoization).</param>
        /// <returns>The standing attempt's result with prior attempts' cost/diagnostics prepended.</returns>
        public static async Task<ModalityFallbackResult> ApplyStepAsync(
            FallbackStep step,
            AIBody body,
            CancellationToken ct,
            AIRequestParameters requestParameters = null,
            ModalityFallbackContext context = null)
        {
            var candidates = step.Candidates;
            var maxAttempts = Math.Min(MaxAttemptsPerStep, candidates.Count);
            var accruedMetrics = new List<AIMetrics>();
            var accruedMessages = new List<SHRuntimeMessage>();

            for (var attempt = 0; attempt < maxAttempts; attempt++)
            {
                var candidate = candidates[attempt];
                ModalityFallbackResult result;
                try
                {
                    result = await step.Fallback.ApplyAsync(
                        body,
                        candidate.Provider,
                        candidate.Model,
                        ct,
                        requestParameters,
                        context).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Work charged by earlier attempts of this same step must be returned,
                    // not thrown away; with nothing accrued, propagate the cancellation bare.
                    if (accruedMetrics.Count == 0 && accruedMessages.Count == 0)
                    {
                        throw;
                    }

                    var cancelled = new ModalityFallbackResult
                    {
                        TransformedBody = body,
                        ExtraMetricsList = accruedMetrics,
                        Messages = accruedMessages,
                    };
                    cancelled.Messages.Add(new SHRuntimeMessage(
                        SHRuntimeMessageSeverity.Error,
                        SHRuntimeMessageOrigin.Provider,
                        SHMessageCode.ConversionFailed,
                        "Modality fallback was cancelled after paid helper work; the partial result is not submittable."));
                    return cancelled;
                }

                var failed = result?.Messages?.Any(m => m != null && m.Severity == SHRuntimeMessageSeverity.Error) ?? true;
                var retryable = failed && result.Messages.Any(m =>
                    m != null && m.Severity == SHRuntimeMessageSeverity.Error && !NonRetryableCodes.Contains(m.Code));
                var isLastAttempt = attempt + 1 >= maxAttempts;
                if (!failed || !retryable || isLastAttempt || ct.IsCancellationRequested)
                {
                    result ??= new ModalityFallbackResult { TransformedBody = body };
                    result.ExtraMetricsList.InsertRange(0, accruedMetrics);
                    result.Messages.InsertRange(0, accruedMessages);
                    if (!failed)
                    {
                        Logger.For<FallbackChain>().Info($"[FallbackChain] '{step.Fallback.Name}' applied via {candidate.Provider}/{candidate.Model}");
                        result.Messages.Add(new SHRuntimeMessage(
                            SHRuntimeMessageSeverity.Info,
                            SHRuntimeMessageOrigin.Provider,
                            SHMessageCode.ModalityFallbackApplied,
                            $"Modality fallback applied: {step.Fallback.Description} — executed by {candidate.Provider}/{candidate.Model}."));
                    }

                    return result;
                }

                // Failed attempt: accrue its cost and diagnostics, then try the next candidate.
                // Its error-severity messages are demoted to warnings — they describe a
                // superseded attempt, not the standing verdict, so a later success is not
                // misread as a failure by downstream error inspection. Its converted
                // items are deliberately discarded: only the standing attempt's output
                // may enter the transformed body.
                accruedMetrics.AddRange(result.ExtraMetricsList);
                foreach (var m in result.Messages)
                {
                    if (m == null)
                    {
                        continue;
                    }

                    accruedMessages.Add(m.Severity == SHRuntimeMessageSeverity.Error
                        ? new SHRuntimeMessage(SHRuntimeMessageSeverity.Warning, m.Origin, m.Code, m.Message, m.Surfaceable)
                        : m);
                }
                var next = candidates[attempt + 1];
                Logger.For<FallbackChain>().Info($"[FallbackChain] '{step.Fallback.Name}' failed on {candidate.Provider}/{candidate.Model}; retrying on {next.Provider}/{next.Model}");
                accruedMessages.Add(new SHRuntimeMessage(
                    SHRuntimeMessageSeverity.Warning,
                    SHRuntimeMessageOrigin.Provider,
                    SHMessageCode.ConversionFailed,
                    $"Modality fallback '{step.Fallback.Name}' failed on {candidate.Provider}/{candidate.Model}; retrying with {next.Provider}/{next.Model}."));
            }

            // Unreachable: the loop always returns; kept for definite-assignment analysis.
            return new ModalityFallbackResult { TransformedBody = body };
        }
    }
}
