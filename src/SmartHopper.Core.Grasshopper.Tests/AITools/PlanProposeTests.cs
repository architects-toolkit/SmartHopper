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

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SmartHopper.Core.Grasshopper.AITools;
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.Infrastructure.Consent;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.Hosting;
using Xunit;

namespace SmartHopper.Core.Grasshopper.Tests.AITools
{
    /// <summary>
    /// Tests the Chat-only planning control tool without Rhino runtime activation.
    /// </summary>
    public class PlanProposeTests
    {
        [Fact]
        public void GetTools_ExposesPlanOnlyToChat()
        {
            var tool = new plan_propose().GetTools().Single();

            Assert.Equal("plan_propose", tool.Name);
            Assert.Equal(AIToolSurface.Chat, tool.Surfaces);
            Assert.False(tool.MutatesCanvas);
        }

        [Fact]
        public async Task Execute_ReturnsApprovalFromConsentPresenter()
        {
            var tool = new plan_propose().GetTools().Single();
            var interaction = new AIInteractionToolCall
            {
                Id = "plan-call",
                Name = tool.Name,
                Arguments = new JObject
                {
                    ["goal"] = "Inspect canvas",
                    ["summary"] = "Read the current definition",
                    ["steps"] = new JArray
                    {
                        new JObject
                        {
                            ["id"] = "inspect",
                            ["description"] = "Inspect the canvas",
                        },
                    },
                },
            };
            var call = new AIToolCall
            {
                ToolSurface = AIToolSurface.Chat,
                InvocationContext = new MutationInvocationContext
                {
                    Surface = AIToolSurface.Chat,
                    Presenter = new ApprovingPresenter(),
                },
            };
            call.FromToolCallInteraction(interaction);

            var result = await tool.Execute(call);
            var payload = result.Body.Interactions.OfType<AIInteractionToolResult>().Last().Result;

            Assert.Equal("Approved", (string?)payload["status"]);
        }

        private sealed class ApprovingPresenter : IConsentPresenter
        {
            public bool CanPresent(IConsentProposal proposal, MutationInvocationContext context) => true;

            public Task<ConsentDecision> PresentAsync(
                IConsentProposal proposal,
                MutationInvocationContext context,
                CancellationToken cancellationToken)
            {
                return Task.FromResult(new ConsentDecision { Status = ConsentDecisionStatus.Approved });
            }
        }
    }
}
