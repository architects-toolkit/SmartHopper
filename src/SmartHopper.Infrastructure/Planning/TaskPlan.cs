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

namespace SmartHopper.Infrastructure.Planning
{
    /// <summary>
    /// Identifies the execution state of one task in a <see cref="TaskPlan"/>.
    /// </summary>
    public enum TaskPlanStatus
    {
        /// <summary>The task has not started yet.</summary>
        Pending,

        /// <summary>The task is currently being worked on.</summary>
        InProgress,

        /// <summary>The task is finished.</summary>
        Completed,
    }

    /// <summary>
    /// Renders a <see cref="TaskPlan"/> snapshot in a host UI without owning its lifecycle.
    /// Unlike <see cref="Consent.IConsentPresenter"/>, presenting a task plan requires no
    /// user decision; the tool call returns immediately after the update is queued.
    /// </summary>
    public interface ITaskPlanPresenter
    {
        /// <summary>Presents the task plan snapshot to the user.</summary>
        /// <param name="plan">The complete plan snapshot to render.</param>
        /// <param name="cancellationToken">Cancellation for the presentation request.</param>
        Task ShowAsync(TaskPlan plan, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Represents one task in a <see cref="TaskPlan"/>.
    /// </summary>
    public sealed class TaskPlanTask
    {
        /// <summary>Gets or sets the stable task identifier.</summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>Gets or sets the user-facing task description.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Gets or sets the task execution state.</summary>
        public TaskPlanStatus Status { get; set; } = TaskPlanStatus.Pending;
    }

    /// <summary>
    /// Represents the current snapshot of a copilot task plan.
    /// Plans are full-state snapshots: every update carries the complete task list
    /// so a presenter can render the plan without keeping incremental state.
    /// </summary>
    public sealed class TaskPlan
    {
        /// <summary>Initializes a new task plan snapshot.</summary>
        /// <param name="id">The stable plan identifier used to update the same rendered plan.</param>
        /// <param name="goal">The user-facing plan goal.</param>
        /// <param name="tasks">The complete ordered task list.</param>
        public TaskPlan(string id, string goal, IReadOnlyList<TaskPlanTask> tasks)
        {
            this.Id = id;
            this.Goal = goal;
            this.Tasks = tasks;
            this.UpdatedAtUtc = DateTime.UtcNow;
        }

        /// <summary>Gets the stable plan identifier.</summary>
        public string Id { get; }

        /// <summary>Gets the user-facing plan goal.</summary>
        public string Goal { get; }

        /// <summary>Gets the complete ordered task list.</summary>
        public IReadOnlyList<TaskPlanTask> Tasks { get; }

        /// <summary>Gets when this snapshot was produced.</summary>
        public DateTime UpdatedAtUtc { get; }

        /// <summary>Gets the number of completed tasks.</summary>
        public int CompletedCount => this.Tasks.Count(task => task.Status == TaskPlanStatus.Completed);

        /// <summary>Gets the number of in-progress tasks.</summary>
        public int InProgressCount => this.Tasks.Count(task => task.Status == TaskPlanStatus.InProgress);

        /// <summary>Gets the number of pending tasks.</summary>
        public int PendingCount => this.Tasks.Count(task => task.Status == TaskPlanStatus.Pending);
    }
}
