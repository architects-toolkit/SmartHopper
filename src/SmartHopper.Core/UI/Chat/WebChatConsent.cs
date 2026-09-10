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
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SmartHopper.Infrastructure.Consent;

namespace SmartHopper.Core.UI.Chat
{
    internal partial class WebChatDialog
    {
        private readonly ConcurrentDictionary<string, TaskCompletionSource<ConsentDecision>> _pendingPlanConsents =
            new ConcurrentDictionary<string, TaskCompletionSource<ConsentDecision>>(StringComparer.Ordinal);

        private async Task<ConsentDecision> ShowPlanConsentAsync(
            PlanConsentProposal proposal,
            CancellationToken cancellationToken)
        {
            var completion = new TaskCompletionSource<ConsentDecision>(TaskCreationOptions.RunContinuationsAsynchronously);
            if (!this._pendingPlanConsents.TryAdd(proposal.Id, completion))
            {
                return ConsentDecision.Cancelled("This plan is already awaiting review.");
            }

            using var registration = cancellationToken.Register(() =>
                completion.TrySetResult(ConsentDecision.Cancelled("Plan review was cancelled.")));
            var payload = new
            {
                id = proposal.Id,
                goal = proposal.Goal,
                summary = proposal.Summary,
                steps = proposal.Steps.Select(step => new
                {
                    id = step.Id,
                    description = step.Description,
                    tool = step.Tool,
                    mutatesCanvas = step.MutatesCanvas,
                }),
                assumptions = proposal.Assumptions,
                successCriteria = proposal.SuccessCriteria,
            };
            this.RunWhenWebViewReady(() => this.ExecuteScript(
                $"showPlanConsent({JsonConvert.SerializeObject(payload)});"));

            try
            {
                return await completion.Task.ConfigureAwait(false);
            }
            finally
            {
                this._pendingPlanConsents.TryRemove(proposal.Id, out _);
                this.RunWhenWebViewReady(() => this.ExecuteScript(
                    $"resolvePlanConsent({JsonConvert.SerializeObject(proposal.Id)});"));
            }
        }

        private void ResolvePlanConsent(string requestId, bool approved)
        {
            if (!this._pendingPlanConsents.TryGetValue(requestId, out var completion))
            {
                return;
            }

            completion.TrySetResult(new ConsentDecision
            {
                Status = approved ? ConsentDecisionStatus.Approved : ConsentDecisionStatus.Rejected,
                AcceptedItemKeys = approved ? new[] { requestId } : Array.Empty<string>(),
                Reason = approved ? null : "The user rejected the proposed plan.",
            });
        }

        private sealed class WebChatPlanConsentPresenter : IConsentPresenter
        {
            private readonly WebChatDialog dialog;

            public WebChatPlanConsentPresenter(WebChatDialog dialog)
            {
                this.dialog = dialog;
            }

            public bool CanPresent(IConsentProposal proposal, MutationInvocationContext context)
            {
                return proposal is PlanConsentProposal;
            }

            public Task<ConsentDecision> PresentAsync(
                IConsentProposal proposal,
                MutationInvocationContext context,
                CancellationToken cancellationToken)
            {
                return this.dialog.ShowPlanConsentAsync((PlanConsentProposal)proposal, cancellationToken);
            }
        }
    }
}
