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
using System.Threading;
using System.Threading.Tasks;
using SmartHopper.Infrastructure.Consent;
using Xunit;

namespace SmartHopper.Infrastructure.Tests.Consent
{
    /// <summary>
    /// Tests single-use consent lifecycle behavior without Rhino dependencies.
    /// </summary>
    public class ConsentGateTests
    {
        [Fact]
        public async Task RequestAsync_UsesInvocationPresenter()
        {
            var presenter = new StubPresenter(new ConsentDecision { Status = ConsentDecisionStatus.Approved });
            var context = new MutationInvocationContext { Presenter = presenter };

            var result = await ConsentGate.RequestAsync(new StubProposal(), context);

            Assert.True(result.IsApproved);
            Assert.Equal(1, presenter.CallCount);
            Assert.False(ConsentGate.IsPending(context.InvocationId));
        }

        [Fact]
        public async Task RequestAsync_CancellationClearsPendingRequest()
        {
            var presenter = new BlockingPresenter();
            var context = new MutationInvocationContext { Presenter = presenter };
            using var cancellation = new CancellationTokenSource();
            var request = ConsentGate.RequestAsync(new StubProposal(), context, cancellation.Token);
            cancellation.Cancel();

            var result = await request;

            Assert.Equal(ConsentDecisionStatus.Cancelled, result.Status);
            Assert.False(ConsentGate.IsPending(context.InvocationId));
        }

        private sealed class StubProposal : IConsentProposal
        {
            public string Id { get; } = Guid.NewGuid().ToString("N");

            public string Kind => "test";

            public string Title => "Test";

            public IReadOnlyList<string> ItemKeys { get; } = new[] { "item" };
        }

        private sealed class StubPresenter : IConsentPresenter
        {
            private readonly ConsentDecision decision;

            public StubPresenter(ConsentDecision decision)
            {
                this.decision = decision;
            }

            public int CallCount { get; private set; }

            public bool CanPresent(IConsentProposal proposal, MutationInvocationContext context) => true;

            public Task<ConsentDecision> PresentAsync(
                IConsentProposal proposal,
                MutationInvocationContext context,
                CancellationToken cancellationToken)
            {
                this.CallCount++;
                return Task.FromResult(this.decision);
            }
        }

        private sealed class BlockingPresenter : IConsentPresenter
        {
            public bool CanPresent(IConsentProposal proposal, MutationInvocationContext context) => true;

            public async Task<ConsentDecision> PresentAsync(
                IConsentProposal proposal,
                MutationInvocationContext context,
                CancellationToken cancellationToken)
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
                return new ConsentDecision { Status = ConsentDecisionStatus.Approved };
            }
        }
    }
}
