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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SmartHopper.Infrastructure.Consent
{
    /// <summary>
    /// Coordinates single-use consent requests and prevents late or duplicate decisions.
    /// </summary>
    public static class ConsentGate
    {
        private static readonly ConcurrentDictionary<string, byte> _pending = new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);
        private static readonly object _presentersLock = new object();
        private static readonly List<IConsentPresenter> _presenters = new List<IConsentPresenter>();

        /// <summary>
        /// Registers a fallback presenter.
        /// </summary>
        public static void RegisterPresenter(IConsentPresenter presenter)
        {
            if (presenter == null)
            {
                throw new ArgumentNullException(nameof(presenter));
            }

            lock (_presentersLock)
            {
                if (!_presenters.Contains(presenter))
                {
                    _presenters.Add(presenter);
                }
            }
        }

        /// <summary>
        /// Requests a one-call consent decision.
        /// </summary>
        public static async Task<ConsentDecision> RequestAsync(
            IConsentProposal proposal,
            MutationInvocationContext? context,
            CancellationToken cancellationToken = default)
        {
            if (proposal == null)
            {
                throw new ArgumentNullException(nameof(proposal));
            }

            var actualContext = context ?? new MutationInvocationContext();
            if (!_pending.TryAdd(actualContext.InvocationId, 0))
            {
                return ConsentDecision.Cancelled("A consent request with this invocation ID is already pending.");
            }

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var presenter = ResolvePresenter(proposal, actualContext);
                if (presenter == null)
                {
                    return new ConsentDecision
                    {
                        Status = ConsentDecisionStatus.Unavailable,
                        Reason = "No consent presenter is available for this operation.",
                    };
                }

                var decision = await presenter.PresentAsync(proposal, actualContext, cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                return decision ?? ConsentDecision.Cancelled("The consent presenter returned no decision.");
            }
            catch (OperationCanceledException)
            {
                return ConsentDecision.Cancelled("The consent request was cancelled.");
            }
            finally
            {
                _pending.TryRemove(actualContext.InvocationId, out _);
            }
        }

        /// <summary>
        /// Gets whether an invocation currently has a pending consent request.
        /// </summary>
        internal static bool IsPending(string invocationId)
        {
            return !string.IsNullOrWhiteSpace(invocationId) && _pending.ContainsKey(invocationId);
        }

        private static IConsentPresenter? ResolvePresenter(IConsentProposal proposal, MutationInvocationContext context)
        {
            if (context.Presenter?.CanPresent(proposal, context) == true)
            {
                return context.Presenter;
            }

            lock (_presentersLock)
            {
                return _presenters.LastOrDefault(presenter => presenter.CanPresent(proposal, context));
            }
        }
    }
}
