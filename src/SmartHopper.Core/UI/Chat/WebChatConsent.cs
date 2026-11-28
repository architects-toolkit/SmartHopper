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
using System.Collections.Generic;
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

        /// <summary>
        /// Resolves the autonomy-continue consent shown when a run stops on the
        /// autonomous time/token budgets. The card always freezes; on approval the
        /// authorization is appended to session history as a real user message
        /// (<see cref="AutonomyContinueMarker"/>) and the conversation re-runs as a
        /// normal turn — <c>ConversationSession</c> starts every run with the full
        /// configured budget, so each approval grants one more window.
        /// </summary>
        private void ResolveAutonomyConsent(string requestId, bool approved)
        {
            if (string.IsNullOrEmpty(requestId))
            {
                return;
            }

            this.RunWhenWebViewReady(() => this.ExecuteScript(
                $"resolveAutonomyContinue({JsonConvert.SerializeObject(requestId)});"));

            if (!string.Equals(this._pendingAutonomyConsentId, requestId, StringComparison.Ordinal))
            {
                // Stale or superseded card — the freeze above is all it gets.
                return;
            }

            this._pendingAutonomyConsentId = null;
            if (!approved)
            {
                return;
            }

            this.SendMessage(AutonomyContinueMarker);
        }

        /// <summary>
        /// Called after each run finishes: when the session stopped on an autonomy
        /// budget, pushes the autonomy-continue approval card. Skipped while a run
        /// is still live, after a normal stop/cancel, and once the dialog is closed.
        /// </summary>
        private void MaybePromptAutonomyContinue()
        {
            if (this._dialogClosed)
            {
                return;
            }

            var usage = this._currentSession?.GetAutonomyUsage();
            if (usage == null || usage.IsRunning || !usage.IsExhausted)
            {
                return;
            }

            var reached = new List<string>();
            if (usage.MaxSeconds > 0 && usage.ElapsedSeconds >= usage.MaxSeconds)
            {
                reached.Add($"the {TimeSpan.FromSeconds(usage.MaxSeconds):mm\\:ss} time limit");
            }

            if (usage.MaxTokens > 0 && usage.Tokens >= usage.MaxTokens)
            {
                reached.Add($"the {usage.MaxTokens:N0}-token limit");
            }

            var detail = reached.Count > 0 ? string.Join(" and ", reached) : "the configured limits";
            if (this._pendingAutonomyConsentId != null)
            {
                // A still-pending card from an earlier stop freezes — at most one
                // continue prompt is actionable at a time.
                var supersededId = this._pendingAutonomyConsentId;
                this.RunWhenWebViewReady(() => this.ExecuteScript(
                    $"resolveAutonomyContinue({JsonConvert.SerializeObject(supersededId)});"));
            }

            var id = $"autonomy-{Interlocked.Increment(ref this._autonomyConsentSeq)}";
            this._pendingAutonomyConsentId = id;
            var payload = new
            {
                id,
                title = "Autonomous run limit reached",
                description = $"The agent stopped because it reached {detail} for autonomous runs. Approve to let it continue working — a fresh run starts with the same limits.",
                approveLabel = "Continue working",
                rejectLabel = "Stop",
            };
            this.RunWhenWebViewReady(() => this.ExecuteScript(
                $"showAutonomyContinue({JsonConvert.SerializeObject(payload)});"));
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
