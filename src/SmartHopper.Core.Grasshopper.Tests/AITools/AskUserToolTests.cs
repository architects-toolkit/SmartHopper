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

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SmartHopper.Core.Grasshopper.AITools;
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.Infrastructure.Consent;
using SmartHopper.Infrastructure.Interaction;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AICall.Core.Returns;
using SmartHopper.ProviderSdk.Hosting;
using Xunit;

namespace SmartHopper.Core.Grasshopper.Tests.AITools
{
    /// <summary>
    /// Tests the Chat-only ask_user tool without Rhino runtime activation.
    /// </summary>
    public class AskUserToolTests
    {
        [Fact]
        public void GetTools_ExposesAskUserOnlyToChatInPlanningCategory()
        {
            var tool = new ask_user().GetTools().Single();

            Assert.Equal("ask_user", tool.Name);
            Assert.Equal("Planning", tool.Category);
            Assert.Equal(AIToolSurface.Chat, tool.Surfaces);
            Assert.False(tool.MutatesCanvas);
        }

        [Fact]
        public async Task Execute_ReturnsOptionAnswer()
        {
            var presenter = new RecordingPresenter(new UserQuestionAnswer
            {
                Answered = true,
                IsFreeText = false,
                Answer = "Option B",
            });
            var call = CreateCall(new JObject
            {
                ["question"] = "Which one?",
                ["options"] = new JArray("Option A", "Option B", "Option C"),
            }, presenter);

            var tool = new ask_user().GetTools().Single();
            var result = await tool.Execute(call);
            var payload = result.Body.Interactions.OfType<AIInteractionToolResult>().Last().Result;

            Assert.NotNull(presenter.LastRequest);
            Assert.Equal("Which one?", presenter.LastRequest!.Question);
            Assert.Equal(3, presenter.LastRequest.Options.Count);
            Assert.Equal(true, (bool?)payload["answered"]);
            Assert.Equal("Option B", (string?)payload["answer"]);
            Assert.Equal(false, (bool?)payload["isFreeText"]);
        }

        [Fact]
        public async Task Execute_ReturnsFreeTextAnswer()
        {
            var presenter = new RecordingPresenter(new UserQuestionAnswer
            {
                Answered = true,
                IsFreeText = true,
                Answer = "Something custom",
            });
            var call = CreateCall(new JObject
            {
                ["question"] = "How many?",
                ["options"] = new JArray("One", "Two"),
            }, presenter);

            var tool = new ask_user().GetTools().Single();
            var result = await tool.Execute(call);
            var payload = result.Body.Interactions.OfType<AIInteractionToolResult>().Last().Result;

            Assert.Equal(true, (bool?)payload["answered"]);
            Assert.Equal(true, (bool?)payload["isFreeText"]);
            Assert.Equal("Something custom", (string?)payload["answer"]);
        }

        [Fact]
        public async Task Execute_ReturnsUnansweredWhenCancelled()
        {
            var presenter = new RecordingPresenter(UserQuestionAnswer.Cancelled);
            var call = CreateCall(new JObject
            {
                ["question"] = "Proceed?",
                ["options"] = new JArray("Yes", "No"),
            }, presenter);

            var tool = new ask_user().GetTools().Single();
            var result = await tool.Execute(call);
            var payload = result.Body.Interactions.OfType<AIInteractionToolResult>().Last().Result;

            Assert.Equal(false, (bool?)payload["answered"]);
        }

        [Fact]
        public async Task Execute_FailsWithoutPresenter()
        {
            var call = CreateCall(new JObject
            {
                ["question"] = "Proceed?",
                ["options"] = new JArray("Yes", "No"),
            }, presenter: null);

            var tool = new ask_user().GetTools().Single();
            var result = await tool.Execute(call);

            AssertToolError(result, "interactive chat surface");
        }

        [Fact]
        public async Task Execute_RejectsMissingQuestion()
        {
            var call = CreateCall(new JObject
            {
                ["options"] = new JArray("Yes", "No"),
            }, new RecordingPresenter(UserQuestionAnswer.Cancelled));

            var tool = new ask_user().GetTools().Single();
            var result = await tool.Execute(call);

            AssertToolError(result, "'question' field is required");
        }

        [Fact]
        public async Task Execute_RejectsTooFewOptions()
        {
            var call = CreateCall(new JObject
            {
                ["question"] = "Proceed?",
                ["options"] = new JArray("Yes"),
            }, new RecordingPresenter(UserQuestionAnswer.Cancelled));

            var tool = new ask_user().GetTools().Single();
            var result = await tool.Execute(call);

            AssertToolError(result, "between 2 and 4");
        }

        [Fact]
        public async Task Execute_RejectsTooManyOptions()
        {
            var call = CreateCall(new JObject
            {
                ["question"] = "Proceed?",
                ["options"] = new JArray("A", "B", "C", "D", "E"),
            }, new RecordingPresenter(UserQuestionAnswer.Cancelled));

            var tool = new ask_user().GetTools().Single();
            var result = await tool.Execute(call);

            AssertToolError(result, "between 2 and 4");
        }

        [Fact]
        public async Task Execute_RejectsEmptyOption()
        {
            var call = CreateCall(new JObject
            {
                ["question"] = "Proceed?",
                ["options"] = new JArray("Yes", "  "),
            }, new RecordingPresenter(UserQuestionAnswer.Cancelled));

            var tool = new ask_user().GetTools().Single();
            var result = await tool.Execute(call);

            AssertToolError(result, "cannot be empty");
        }

        private static AIToolCall CreateCall(JObject arguments, IUserQuestionPresenter? presenter)
        {
            var interaction = new AIInteractionToolCall
            {
                Id = "ask-user-call",
                Name = "ask_user",
                Arguments = arguments,
            };
            var call = new AIToolCall
            {
                ToolSurface = AIToolSurface.Chat,
                InvocationContext = new MutationInvocationContext
                {
                    Surface = AIToolSurface.Chat,
                    UserQuestionPresenter = presenter,
                },
            };
            call.FromToolCallInteraction(interaction);
            return call;
        }

        private static void AssertToolError(AIReturn result, string fragment)
        {
            Assert.Contains(result.Messages, message => message.Message.Contains(fragment));
        }

        private sealed class RecordingPresenter : IUserQuestionPresenter
        {
            private readonly UserQuestionAnswer response;

            public RecordingPresenter(UserQuestionAnswer response)
            {
                this.response = response;
            }

            public UserQuestionRequest? LastRequest { get; private set; }

            public Task<UserQuestionAnswer> AskAsync(UserQuestionRequest request, CancellationToken cancellationToken)
            {
                this.LastRequest = request;
                return Task.FromResult(this.response);
            }
        }
    }
}
