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

namespace SmartHopper.Infrastructure.Tests.AICall.Utilities
{
    using System.Collections.Generic;
    using System.Linq;
    using SmartHopper.ProviderSdk.AICall.Core.Base;
    using SmartHopper.ProviderSdk.AICall.Core.Interactions;
    using SmartHopper.ProviderSdk.AICall.Utilities;
    using Xunit;

    /// <summary>
    /// Unit tests for the <see cref="InteractionUtility"/> class.
    /// </summary>
    public class InteractionUtilityTests
    {
        #region GenerateTurnId

#if NET7_WINDOWS
        [Fact(DisplayName = "InteractionUtility GenerateTurnId returns non-empty 32-char hex [Windows]")]
#else
        [Fact(DisplayName = "InteractionUtility GenerateTurnId returns non-empty 32-char hex [Core]")]
#endif
        public void GenerateTurnId_ReturnsValidHexString()
        {
            var turnId = InteractionUtility.GenerateTurnId();
            Assert.False(string.IsNullOrWhiteSpace(turnId));
            Assert.Equal(32, turnId.Length);
            Assert.All(turnId, c => Assert.True("0123456789abcdef".Contains(c)));
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "InteractionUtility GenerateTurnId produces unique values [Windows]")]
#else
        [Fact(DisplayName = "InteractionUtility GenerateTurnId produces unique values [Core]")]
#endif
        public void GenerateTurnId_ProducesUniqueValues()
        {
            var ids = new HashSet<string>();
            for (int i = 0; i < 100; i++)
            {
                ids.Add(InteractionUtility.GenerateTurnId());
            }

            Assert.Equal(100, ids.Count);
        }

        #endregion

        #region EnsureTurnId

#if NET7_WINDOWS
        [Fact(DisplayName = "InteractionUtility EnsureTurnId assigns turnId to all interactions [Windows]")]
#else
        [Fact(DisplayName = "InteractionUtility EnsureTurnId assigns turnId to all interactions [Core]")]
#endif
        public void EnsureTurnId_AssignsToAllInteractions()
        {
            var interactions = new List<IAIInteraction>
            {
                new AIInteractionText { Agent = AIAgent.User, Content = "hello" },
                new AIInteractionText { Agent = AIAgent.Assistant, Content = "hi" },
            };

            InteractionUtility.EnsureTurnId(interactions, "turn-123");

            Assert.All(interactions, i => Assert.Equal("turn-123", i.TurnId));
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "InteractionUtility EnsureTurnId null collection is no-op [Windows]")]
#else
        [Fact(DisplayName = "InteractionUtility EnsureTurnId null collection is no-op [Core]")]
#endif
        public void EnsureTurnId_NullCollection_IsNoOp()
        {
            // Should not throw
            InteractionUtility.EnsureTurnId(null, "turn-123");
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "InteractionUtility EnsureTurnId null turnId is no-op [Windows]")]
#else
        [Fact(DisplayName = "InteractionUtility EnsureTurnId null turnId is no-op [Core]")]
#endif
        public void EnsureTurnId_NullTurnId_IsNoOp()
        {
            var interaction = new AIInteractionText { Agent = AIAgent.User, Content = "test" };
            var interactions = new List<IAIInteraction> { interaction };

            InteractionUtility.EnsureTurnId(interactions, null);

            Assert.True(string.IsNullOrEmpty(interaction.TurnId));
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "InteractionUtility EnsureTurnId skips null interactions [Windows]")]
#else
        [Fact(DisplayName = "InteractionUtility EnsureTurnId skips null interactions [Core]")]
#endif
        public void EnsureTurnId_NullInteractionsInCollection_AreSkipped()
        {
            var valid = new AIInteractionText { Agent = AIAgent.User, Content = "test" };
            var interactions = new List<IAIInteraction> { valid, null };

            InteractionUtility.EnsureTurnId(interactions, "turn-123");

            Assert.Equal("turn-123", valid.TurnId);
        }

        #endregion
    }
}
