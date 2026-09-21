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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SmartHopper.Infrastructure.Interaction;

namespace SmartHopper.Core.UI.Chat
{
    internal partial class WebChatDialog
    {
        /// <summary>
        /// Pending user questions keyed by request id. Each entry completes when the user
        /// answers (option click or free-text submit) or is cancelled with the run.
        /// </summary>
        private readonly ConcurrentDictionary<string, PendingQuestion> _pendingQuestions =
            new ConcurrentDictionary<string, PendingQuestion>(StringComparer.Ordinal);

        /// <summary>
        /// Renders a question card in the WebView and waits for the user's answer.
        /// </summary>
        /// <param name="request">The question and predefined options to render.</param>
        /// <param name="cancellationToken">Cancellation for the pending question.</param>
        /// <returns>The user's answer, or an unanswered result when cancelled.</returns>
        private async Task<UserQuestionAnswer> AskUserQuestionAsync(UserQuestionRequest request, CancellationToken cancellationToken)
        {
            var pending = new PendingQuestion
            {
                Completion = new TaskCompletionSource<UserQuestionAnswer>(TaskCreationOptions.RunContinuationsAsynchronously),
                Options = request.Options,
            };

            if (!this._pendingQuestions.TryAdd(request.Id, pending))
            {
                return UserQuestionAnswer.Cancelled;
            }

            using var registration = cancellationToken.Register(() =>
            {
                pending.Completion.TrySetCanceled(cancellationToken);
            });

            var payload = new
            {
                id = request.Id,
                question = request.Question,
                options = request.Options,
            };

            try
            {
                this.RunWhenWebViewReady(() =>
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        this.ExecuteScript($"showUserQuestion({JsonConvert.SerializeObject(payload)});");
                    }
                });

                return await pending.Completion.Task.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return UserQuestionAnswer.Cancelled;
            }
            finally
            {
                this._pendingQuestions.TryRemove(request.Id, out _);
                this.ExecuteScript($"resolveUserQuestion({JsonConvert.SerializeObject(request.Id)});");
            }
        }

        /// <summary>
        /// Resolves a pending question from an <c>sh://event?type=question</c> navigation.
        /// </summary>
        /// <param name="id">The question identifier.</param>
        /// <param name="choice">The selected option index, or -1 when the answer is free text.</param>
        /// <param name="text">The free-text answer, or null when a predefined option was selected.</param>
        private void ResolveUserQuestion(string id, int choice, string? text)
        {
            if (string.IsNullOrWhiteSpace(id) || !this._pendingQuestions.TryGetValue(id, out var pending))
            {
                return;
            }

            if (text != null)
            {
                pending.Completion.TrySetResult(new UserQuestionAnswer { Answered = true, IsFreeText = true, Answer = text });
                return;
            }

            if (choice >= 0 && choice < pending.Options.Count)
            {
                pending.Completion.TrySetResult(new UserQuestionAnswer { Answered = true, IsFreeText = false, Answer = pending.Options[choice] });
            }
        }

        /// <summary>
        /// State for one pending question: the completion source plus the option list used
        /// to resolve option indices arriving from the WebView back to their text.
        /// </summary>
        private sealed class PendingQuestion
        {
            public TaskCompletionSource<UserQuestionAnswer> Completion { get; set; } =
                new TaskCompletionSource<UserQuestionAnswer>(TaskCreationOptions.RunContinuationsAsynchronously);

            public IReadOnlyList<string> Options { get; set; } = new List<string>();
        }

        private sealed class WebChatUserQuestionPresenter : IUserQuestionPresenter
        {
            private readonly WebChatDialog dialog;

            public WebChatUserQuestionPresenter(WebChatDialog dialog)
            {
                this.dialog = dialog;
            }

            public Task<UserQuestionAnswer> AskAsync(UserQuestionRequest request, CancellationToken cancellationToken)
            {
                return this.dialog.AskUserQuestionAsync(request, cancellationToken);
            }
        }
    }
}
