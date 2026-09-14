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
using SmartHopper.Infrastructure.Planning;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AICall.Core.Returns;
using SmartHopper.ProviderSdk.Hosting;
using Xunit;

namespace SmartHopper.Core.Grasshopper.Tests.AITools
{
    /// <summary>
    /// Tests the Chat-only task plan visualization tool without Rhino runtime activation.
    /// </summary>
    public class PlanTasksTests
    {
        [Fact]
        public void GetTools_ExposesPlanTasksOnlyToChat()
        {
            var tool = new plan_tasks().GetTools().Single();

            Assert.Equal("plan_tasks", tool.Name);
            Assert.Equal(AIToolSurface.Chat, tool.Surfaces);
            Assert.False(tool.MutatesCanvas);
        }

        [Fact]
        public async Task Execute_PushesNormalizedPlanToPresenter()
        {
            var presenter = new RecordingPresenter();
            var call = CreateCall(new JObject
            {
                ["planId"] = "plan-1",
                ["goal"] = "Build a roof",
                ["tasks"] = new JArray
                {
                    new JObject { ["id"] = "a", ["description"] = "Inspect", ["status"] = "completed" },
                    new JObject { ["id"] = "b", ["description"] = "Generate", ["status"] = "in_progress" },
                    new JObject { ["id"] = "c", ["description"] = "Verify", ["status"] = "pending" },
                },
            }, presenter);

            var tool = new plan_tasks().GetTools().Single();
            var result = await tool.Execute(call);
            var payload = result.Body.Interactions.OfType<AIInteractionToolResult>().Last().Result;

            Assert.NotNull(presenter.LastPlan);
            Assert.Equal("plan-1", presenter.LastPlan!.Id);
            Assert.Equal(3, presenter.LastPlan.Tasks.Count);
            Assert.Equal(TaskPlanStatus.Completed, presenter.LastPlan.Tasks[0].Status);
            Assert.Equal(TaskPlanStatus.InProgress, presenter.LastPlan.Tasks[1].Status);
            Assert.Equal(TaskPlanStatus.Pending, presenter.LastPlan.Tasks[2].Status);
            Assert.Equal("Updated", (string?)payload["status"]);
            Assert.Equal(true, (bool?)payload["displayed"]);
            Assert.Equal(1, (int?)payload["counts"]?["completed"]);
            Assert.Equal(1, (int?)payload["counts"]?["inProgress"]);
            Assert.Equal(1, (int?)payload["counts"]?["pending"]);
        }

        [Fact]
        public async Task Execute_GeneratesPlanIdAndSucceedsWithoutPresenter()
        {
            var call = CreateCall(new JObject
            {
                ["tasks"] = new JArray
                {
                    new JObject { ["id"] = "a", ["description"] = "Inspect", ["status"] = "pending" },
                },
            }, presenter: null);

            var tool = new plan_tasks().GetTools().Single();
            var result = await tool.Execute(call);
            var payload = result.Body.Interactions.OfType<AIInteractionToolResult>().Last().Result;

            Assert.False(string.IsNullOrWhiteSpace((string?)payload["planId"]));
            Assert.Equal(false, (bool?)payload["displayed"]);
        }

        [Fact]
        public async Task Execute_RejectsDuplicateTaskIds()
        {
            var call = CreateCall(new JObject
            {
                ["tasks"] = new JArray
                {
                    new JObject { ["id"] = "a", ["description"] = "One", ["status"] = "pending" },
                    new JObject { ["id"] = "a", ["description"] = "Two", ["status"] = "pending" },
                },
            }, new RecordingPresenter());

            var tool = new plan_tasks().GetTools().Single();
            var result = await tool.Execute(call);

            AssertToolError(result, "Duplicate task id");
        }

        [Fact]
        public async Task Execute_RejectsMultipleInProgressTasks()
        {
            var call = CreateCall(new JObject
            {
                ["tasks"] = new JArray
                {
                    new JObject { ["id"] = "a", ["description"] = "One", ["status"] = "in_progress" },
                    new JObject { ["id"] = "b", ["description"] = "Two", ["status"] = "in_progress" },
                },
            }, new RecordingPresenter());

            var tool = new plan_tasks().GetTools().Single();
            var result = await tool.Execute(call);

            AssertToolError(result, "at most one in_progress");
        }

        [Fact]
        public async Task Execute_RejectsUnknownStatus()
        {
            var call = CreateCall(new JObject
            {
                ["tasks"] = new JArray
                {
                    new JObject { ["id"] = "a", ["description"] = "One", ["status"] = "done" },
                },
            }, new RecordingPresenter());

            var tool = new plan_tasks().GetTools().Single();
            var result = await tool.Execute(call);

            AssertToolError(result, "Unknown task status");
        }

        private static AIToolCall CreateCall(JObject arguments, ITaskPlanPresenter? presenter)
        {
            var interaction = new AIInteractionToolCall
            {
                Id = "plan-tasks-call",
                Name = "plan_tasks",
                Arguments = arguments,
            };
            var call = new AIToolCall
            {
                ToolSurface = AIToolSurface.Chat,
                InvocationContext = new MutationInvocationContext
                {
                    Surface = AIToolSurface.Chat,
                    TaskPlanPresenter = presenter,
                },
            };
            call.FromToolCallInteraction(interaction);
            return call;
        }

        private static void AssertToolError(AIReturn result, string fragment)
        {
            Assert.Contains(result.Messages, message => message.Message.Contains(fragment));
        }

        private sealed class RecordingPresenter : ITaskPlanPresenter
        {
            public TaskPlan? LastPlan { get; private set; }

            public Task ShowAsync(TaskPlan plan, CancellationToken cancellationToken)
            {
                this.LastPlan = plan;
                return Task.CompletedTask;
            }
        }
    }
}
