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
using SmartHopper.Infrastructure.Consent;

namespace SmartHopper.Core.Grasshopper.Utils.Canvas
{
    /// <summary>
    /// Adapts a graphical canvas change review session to the generic consent pipeline.
    /// </summary>
    public sealed class CanvasMutationConsentProposal : IConsentProposal
    {
        /// <summary>Initializes a new canvas mutation proposal.</summary>
        public CanvasMutationConsentProposal(CanvasChangeReviewSession session)
        {
            this.Session = session ?? throw new ArgumentNullException(nameof(session));
            this.Id = Guid.NewGuid().ToString("N");
            this.ItemKeys = session.Items.Select(item => item.Key).ToList();
        }

        /// <inheritdoc/>
        public string Id { get; }

        /// <inheritdoc/>
        public string Kind => "canvas-change";

        /// <inheritdoc/>
        public string Title => this.Session.Title;

        /// <inheritdoc/>
        public IReadOnlyList<string> ItemKeys { get; }

        /// <summary>Gets the graphical review session.</summary>
        public CanvasChangeReviewSession Session { get; }
    }

    /// <summary>
    /// Presents canvas consent through the shared graphical diff dialog.
    /// </summary>
    public sealed class CanvasChangeConsentPresenter : IConsentPresenter
    {
        /// <summary>Gets the shared presenter instance.</summary>
        public static CanvasChangeConsentPresenter Instance { get; } = new CanvasChangeConsentPresenter();

        private CanvasChangeConsentPresenter()
        {
        }

        /// <inheritdoc/>
        public bool CanPresent(IConsentProposal proposal, MutationInvocationContext context)
        {
            return proposal is CanvasMutationConsentProposal;
        }

        /// <inheritdoc/>
        public async Task<ConsentDecision> PresentAsync(
            IConsentProposal proposal,
            MutationInvocationContext context,
            CancellationToken cancellationToken)
        {
            var canvasProposal = (CanvasMutationConsentProposal)proposal;
            var applied = await CanvasChangeReviewService.ShowReviewDialogAsync(
                canvasProposal.Session,
                cancellationToken).ConfigureAwait(false);
            if (!applied)
            {
                return ConsentDecision.Cancelled("The user cancelled the staged canvas changes.");
            }

            var accepted = canvasProposal.Session.Items
                .Where(canvasProposal.Session.IsEffectivelyAccepted)
                .Select(item => item.Key)
                .ToList();
            return new ConsentDecision
            {
                Status = accepted.Count == 0
                    ? ConsentDecisionStatus.Rejected
                    : accepted.Count == canvasProposal.Session.Items.Count
                        ? ConsentDecisionStatus.Approved
                        : ConsentDecisionStatus.PartiallyApproved,
                AcceptedItemKeys = accepted,
            };
        }
    }
}
