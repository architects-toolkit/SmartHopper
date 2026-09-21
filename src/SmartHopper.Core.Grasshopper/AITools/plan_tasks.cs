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
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.Infrastructure.AITools;
using SmartHopper.Infrastructure.Planning;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AICall.Core.Returns;
using SmartHopper.ProviderSdk.Hosting;

namespace SmartHopper.Core.Grasshopper.AITools
{
    /// <summary>
    /// Provides the Chat-only copilot task plan visualization tool.
    /// </summary>
    public sealed class plan_tasks : IAIToolProvider
    {
        private const int MaxTasks = 30;
        private const int MaxTextLength = 4000;
        private const int MaxPlanIdLength = 128;

        /// <inheritdoc/>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                "plan_tasks",
                "Create or update the visible task plan for multi-step work. Send the complete task list on every call and reuse the returned planId to update the same plan. Keep at most one task in_progress, mark finished work completed, and leave upcoming work pending.",
                "Planning",
                @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""planId"": { ""type"": ""string"", ""description"": ""Stable plan identifier returned by a previous call. Omit to create a new plan."" },
                        ""goal"": { ""type"": ""string"" },
                        ""tasks"": {
                            ""type"": ""array"",
                            ""minItems"": 1,
                            ""maxItems"": 30,
                            ""items"": {
                                ""type"": ""object"",
                                ""properties"": {
                                    ""id"": { ""type"": ""string"" },
                                    ""description"": { ""type"": ""string"" },
                                    ""status"": { ""type"": ""string"", ""enum"": [""pending"", ""in_progress"", ""completed""] }
                                },
                                ""required"": [""id"", ""description"", ""status""]
                            }
                        }
                    },
                    ""required"": [""tasks""]
                }",
                this.ExecuteAsync,
                mutatesCanvas: false,
                tags: new[] { "planning", "progress", "read-only" },
                outputSchema: @"{ ""type"": ""object"", ""properties"": { ""planId"": { ""type"": ""string"" }, ""status"": { ""type"": ""string"" }, ""displayed"": { ""type"": ""boolean"" }, ""counts"": { ""type"": ""object"" }, ""tasks"": { ""type"": ""array"" } } }",
                surfaces: AIToolSurface.Chat);
        }

        private async Task<AIReturn> ExecuteAsync(AIToolCall toolCall)
        {
            var output = new AIReturn { Request = toolCall };
            try
            {
                toolCall.SkipMetricsValidation = true;
                var args = toolCall.GetToolCall().GetArgumentsOrEmpty();
                var planId = ReadBounded(args, "planId", required: false);
                if (planId.Length > MaxPlanIdLength)
                {
                    throw new ArgumentException($"Task plan 'planId' exceeds {MaxPlanIdLength} characters.");
                }

                if (string.IsNullOrWhiteSpace(planId))
                {
                    planId = $"plan-{Guid.NewGuid():N}";
                }

                var goal = ReadBounded(args, "goal", required: false);
                var tasksToken = args["tasks"] as JArray ?? throw new ArgumentException("A task plan requires a tasks array.");
                if (tasksToken.Count == 0 || tasksToken.Count > MaxTasks)
                {
                    throw new ArgumentException($"A task plan must contain between 1 and {MaxTasks} tasks.");
                }

                if (tasksToken.Any(token => token is not JObject))
                {
                    throw new ArgumentException("Every task plan task must be a JSON object.");
                }

                var tasks = new List<TaskPlanTask>();
                var ids = new HashSet<string>(StringComparer.Ordinal);
                var inProgressCount = 0;
                foreach (var token in tasksToken.OfType<JObject>())
                {
                    var id = ReadBounded(token, "id", required: true);
                    if (!ids.Add(id))
                    {
                        throw new ArgumentException($"Duplicate task id '{id}'.");
                    }

                    var status = ParseStatus(token["status"]?.ToString());
                    if (status == TaskPlanStatus.InProgress)
                    {
                        inProgressCount++;
                    }

                    tasks.Add(new TaskPlanTask
                    {
                        Id = id,
                        Description = ReadBounded(token, "description", required: true),
                        Status = status,
                    });
                }

                if (inProgressCount > 1)
                {
                    throw new ArgumentException("A task plan can have at most one in_progress task.");
                }

                var plan = new TaskPlan(planId, goal, tasks);
                var displayed = false;
                var presenter = toolCall.InvocationContext?.TaskPlanPresenter;
                if (presenter != null)
                {
                    await presenter.ShowAsync(plan, toolCall.CancellationToken).ConfigureAwait(false);
                    displayed = true;
                }

                var result = new JObject
                {
                    ["planId"] = plan.Id,
                    ["status"] = "Updated",
                    ["displayed"] = displayed,
                    ["counts"] = new JObject
                    {
                        ["pending"] = plan.PendingCount,
                        ["inProgress"] = plan.InProgressCount,
                        ["completed"] = plan.CompletedCount,
                    },
                    ["tasks"] = new JArray(tasks.Select(task => new JObject
                    {
                        ["id"] = task.Id,
                        ["description"] = task.Description,
                        ["status"] = ToWire(task.Status),
                    })),
                };
                output.CreateSuccess(AIBodyBuilder.Create()
                    .AddToolResult(result, id: toolCall.GetToolCall().Id, name: "plan_tasks")
                    .Build(), toolCall);
                return output;
            }
            catch (Exception ex)
            {
                output.CreateToolError(ex.Message, toolCall);
                return output;
            }
        }

        private static string ReadBounded(JObject source, string name, bool required)
        {
            var value = source[name]?.ToString().Trim() ?? string.Empty;
            if (required && string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException($"Task plan field '{name}' is required.");
            }

            if (value.Length > MaxTextLength)
            {
                throw new ArgumentException($"Task plan field '{name}' exceeds {MaxTextLength} characters.");
            }

            return value;
        }

        private static TaskPlanStatus ParseStatus(string? raw)
        {
            var value = raw?.Trim();
            if (string.Equals(value, "pending", StringComparison.OrdinalIgnoreCase))
            {
                return TaskPlanStatus.Pending;
            }

            if (string.Equals(value, "in_progress", StringComparison.OrdinalIgnoreCase))
            {
                return TaskPlanStatus.InProgress;
            }

            if (string.Equals(value, "completed", StringComparison.OrdinalIgnoreCase))
            {
                return TaskPlanStatus.Completed;
            }

            throw new ArgumentException($"Unknown task status '{raw}'. Use 'pending', 'in_progress', or 'completed'.");
        }

        private static string ToWire(TaskPlanStatus status)
        {
            return status switch
            {
                TaskPlanStatus.InProgress => "in_progress",
                TaskPlanStatus.Completed => "completed",
                _ => "pending",
            };
        }
    }
}
