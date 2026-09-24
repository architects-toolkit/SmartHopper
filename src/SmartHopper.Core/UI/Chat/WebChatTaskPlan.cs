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
using Newtonsoft.Json;
using SmartHopper.Infrastructure.Planning;

namespace SmartHopper.Core.UI.Chat
{
    internal partial class WebChatDialog
    {
        private static string ToJsStatus(TaskPlanStatus status)
        {
            return status switch
            {
                TaskPlanStatus.InProgress => "in_progress",
                TaskPlanStatus.Completed => "completed",
                _ => "pending",
            };
        }

        /// <summary>
        /// Queues a task plan snapshot for rendering in the WebView.
        /// Presentation is fire-and-forget: updates are serialized through the DOM
        /// update queue and rendered by the <c>updateTaskPlan</c> script function.
        /// </summary>
        /// <param name="plan">The complete plan snapshot to render.</param>
        /// <param name="cancellationToken">Cancellation for the presentation request.</param>
        private Task ShowTaskPlanAsync(TaskPlan plan, CancellationToken cancellationToken)
        {
            var payload = new
            {
                id = plan.Id,
                goal = plan.Goal,
                tasks = plan.Tasks.Select(task => new
                {
                    id = task.Id,
                    description = task.Description,
                    status = ToJsStatus(task.Status),
                }),
            };

            this.RunWhenWebViewReady(() =>
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    this.ExecuteScript($"updateTaskPlan({JsonConvert.SerializeObject(payload)});");
                }
            });

            return Task.CompletedTask;
        }

        private sealed class WebChatTaskPlanPresenter : ITaskPlanPresenter
        {
            private readonly WebChatDialog dialog;

            public WebChatTaskPlanPresenter(WebChatDialog dialog)
            {
                this.dialog = dialog;
            }

            public Task ShowAsync(TaskPlan plan, CancellationToken cancellationToken)
            {
                return this.dialog.ShowTaskPlanAsync(plan, cancellationToken);
            }
        }
    }
}
