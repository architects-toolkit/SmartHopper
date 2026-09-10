/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024-2026 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using System;
using System.Threading;
using System.Threading.Tasks;

namespace SmartHopper.Infrastructure.Consent
{
    /// <summary>
    /// Provides the shared prepare-review-apply boundary for consent-controlled operations.
    /// </summary>
    public static class MutatingOperationExecutor
    {
        /// <summary>
        /// Reviews a prepared proposal without applying state changes.
        /// </summary>
        public static Task<ConsentDecision> ReviewAsync(
            IConsentProposal proposal,
            MutationInvocationContext? context,
            CancellationToken cancellationToken = default)
        {
            return ConsentGate.RequestAsync(proposal, context, cancellationToken);
        }

        /// <summary>
        /// Reviews a prepared proposal and invokes apply only when at least one item is approved.
        /// </summary>
        public static async Task<TResult> ExecuteAsync<TResult>(
            IConsentProposal proposal,
            MutationInvocationContext? context,
            Func<ConsentDecision, CancellationToken, Task<TResult>> applyAsync,
            Func<ConsentDecision, TResult> rejectedResult,
            CancellationToken cancellationToken = default)
        {
            if (applyAsync == null)
            {
                throw new ArgumentNullException(nameof(applyAsync));
            }

            if (rejectedResult == null)
            {
                throw new ArgumentNullException(nameof(rejectedResult));
            }

            var decision = await ReviewAsync(proposal, context, cancellationToken).ConfigureAwait(false);
            if (!decision.IsApproved)
            {
                return rejectedResult(decision);
            }

            cancellationToken.ThrowIfCancellationRequested();
            return await applyAsync(decision, cancellationToken).ConfigureAwait(false);
        }
    }
}
