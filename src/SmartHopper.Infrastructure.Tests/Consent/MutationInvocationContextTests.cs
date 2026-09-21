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

using SmartHopper.Infrastructure.Consent;
using SmartHopper.Infrastructure.Interaction;
using SmartHopper.ProviderSdk.Hosting;
using Xunit;

namespace SmartHopper.Infrastructure.Tests.Consent
{
    /// <summary>
    /// Tests <see cref="MutationInvocationContext.ForTool"/> defensive-copy behavior.
    /// </summary>
    public class MutationInvocationContextTests
    {
        [Fact]
        public void ForTool_CopiesPresentersAndSurface_StampsToolIdentity()
        {
            var pointer = new StubPointerPresenter();
            var question = new StubQuestionPresenter();
            var context = new MutationInvocationContext
            {
                Source = MutationInvocationSource.WebChat,
                OwnerId = "owner-1",
                Surface = AIToolSurface.Chat,
                CanvasPointerPresenter = pointer,
                UserQuestionPresenter = question,
            };

            var copy = context.ForTool("call-1", "ask_user");

            Assert.NotEqual(context.InvocationId, copy.InvocationId);
            Assert.Equal("call-1", copy.ToolCallId);
            Assert.Equal("ask_user", copy.ToolName);
            Assert.Equal(MutationInvocationSource.WebChat, copy.Source);
            Assert.Equal("owner-1", copy.OwnerId);
            Assert.Equal(AIToolSurface.Chat, copy.Surface);
            Assert.Same(pointer, copy.CanvasPointerPresenter);
            Assert.Same(question, copy.UserQuestionPresenter);
            Assert.Null(context.ToolCallId);
        }

        private sealed class StubPointerPresenter : ICanvasPointerPresenter
        {
            public System.Threading.Tasks.Task ShowAsync(CanvasPointerRequest request, System.Threading.CancellationToken cancellationToken)
            {
                return System.Threading.Tasks.Task.CompletedTask;
            }
        }

        private sealed class StubQuestionPresenter : IUserQuestionPresenter
        {
            public System.Threading.Tasks.Task<UserQuestionAnswer> AskAsync(UserQuestionRequest request, System.Threading.CancellationToken cancellationToken)
            {
                return System.Threading.Tasks.Task.FromResult(UserQuestionAnswer.Cancelled);
            }
        }
    }
}
