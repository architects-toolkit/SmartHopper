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

namespace SmartHopper.ProviderSdk.Tests.AICall.Core.Interactions
{
    using System.Collections.Generic;
    using SmartHopper.ProviderSdk.AICall.Core.Base;
    using SmartHopper.ProviderSdk.AICall.Core.Interactions;
    using Xunit;

    /// <summary>
    /// Tests for <see cref="InteractionModelProjection"/> and <see cref="AIInteractionDocumentEvent"/>.
    /// </summary>
    [Collection("ProviderSdk")]
    public class InteractionModelProjectionTests
    {
#if NET7_WINDOWS
        [Fact(DisplayName = "Project_MergesDocumentEventsIntoNextUserText [Windows]")]
#else
        [Fact(DisplayName = "Project_MergesDocumentEventsIntoNextUserText [Core]")]
#endif
        public void Project_MergesDocumentEventsIntoNextUserText()
        {
            var interactions = new List<IAIInteraction>
            {
                new AIInteractionText { Agent = AIAgent.System, Content = "system" },
                new AIInteractionDocumentEvent { Kind = AIDocumentEventKind.SwitchedDocument, Content = "Switched to **b.gh**" },
                new AIInteractionDocumentEvent { Kind = AIDocumentEventKind.CanvasChanged, Content = "Canvas changed." },
                new AIInteractionText { Agent = AIAgent.User, Content = "hello", TurnId = "t1" },
            };

            var projected = InteractionModelProjection.Project(interactions);

            Assert.Equal(2, projected.Count);
            var user = Assert.IsType<AIInteractionText>(projected[1]);
            Assert.Equal(AIAgent.User, user.Agent);
            Assert.Equal("t1", user.TurnId);
            Assert.Equal("[Canvas] Switched to **b.gh**\n\n[Canvas] Canvas changed.\n\nhello", user.Content);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "Project_EmitsStandaloneUserTextWhenNoUserTextFollows [Windows]")]
#else
        [Fact(DisplayName = "Project_EmitsStandaloneUserTextWhenNoUserTextFollows [Core]")]
#endif
        public void Project_EmitsStandaloneUserTextWhenNoUserTextFollows()
        {
            var interactions = new List<IAIInteraction>
            {
                new AIInteractionDocumentEvent { Kind = AIDocumentEventKind.ActiveDocument, Content = "Active document: **a.gh**" },
                new AIInteractionText { Agent = AIAgent.Assistant, Content = "hi" },
            };

            var projected = InteractionModelProjection.Project(interactions);

            Assert.Equal(2, projected.Count);
            var standalone = Assert.IsType<AIInteractionText>(projected[0]);
            Assert.Equal(AIAgent.User, standalone.Agent);
            Assert.Equal("[Canvas] Active document: **a.gh**", standalone.Content);
            Assert.Same(interactions[1], projected[1]);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "Project_LeavesInputUnchanged [Windows]")]
#else
        [Fact(DisplayName = "Project_LeavesInputUnchanged [Core]")]
#endif
        public void Project_LeavesInputUnchanged()
        {
            var documentEvent = new AIInteractionDocumentEvent { Content = "Canvas changed." };
            var interactions = new List<IAIInteraction> { documentEvent };

            InteractionModelProjection.Project(interactions);

            Assert.Single(interactions);
            Assert.Same(documentEvent, interactions[0]);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "DocumentEvent_HasUserAgentAndUniqueKeys [Windows]")]
#else
        [Fact(DisplayName = "DocumentEvent_HasUserAgentAndUniqueKeys [Core]")]
#endif
        public void DocumentEvent_HasUserAgentAndUniqueKeys()
        {
            var first = new AIInteractionDocumentEvent { Content = "Switched to **a.gh**", Agent = AIAgent.System };
            var second = new AIInteractionDocumentEvent { Content = "Switched to **a.gh**" };

            Assert.Equal(AIAgent.User, first.Agent);
            Assert.Equal("system", first.GetRoleClassForRender());
            Assert.NotEqual(first.GetDedupKey(), second.GetDedupKey());
        }
    }
}
