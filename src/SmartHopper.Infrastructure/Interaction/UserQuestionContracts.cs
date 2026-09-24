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
using System.Threading;
using System.Threading.Tasks;

namespace SmartHopper.Infrastructure.Interaction
{
    /// <summary>
    /// Describes one question the model wants to ask the user while working.
    /// The presenter always appends a free-text answer field on top of <see cref="Options"/>.
    /// </summary>
    public sealed class UserQuestionRequest
    {
        /// <summary>Gets or sets the unique question identifier.</summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>Gets or sets the user-facing question text.</summary>
        public string Question { get; set; } = string.Empty;

        /// <summary>Gets or sets the predefined answer options (2 to 4).</summary>
        public IReadOnlyList<string> Options { get; set; } = new List<string>();
    }

    /// <summary>
    /// Represents the user's terminal answer to a <see cref="UserQuestionRequest"/>.
    /// </summary>
    public sealed class UserQuestionAnswer
    {
        /// <summary>Gets or sets a value indicating whether the user provided an answer.</summary>
        public bool Answered { get; set; }

        /// <summary>Gets or sets a value indicating whether the answer came from the free-text field rather than a predefined option.</summary>
        public bool IsFreeText { get; set; }

        /// <summary>Gets or sets the selected option text or the typed free-text answer.</summary>
        public string? Answer { get; set; }

        /// <summary>Gets a shared unanswered result used when the invocation is cancelled.</summary>
        public static UserQuestionAnswer Cancelled => new UserQuestionAnswer { Answered = false };
    }

    /// <summary>
    /// Presents a <see cref="UserQuestionRequest"/> to the user and returns one terminal answer.
    /// Unlike <see cref="Consent.IConsentPresenter"/>, answering a question grants no permissions;
    /// it only feeds text back to the model.
    /// </summary>
    public interface IUserQuestionPresenter
    {
        /// <summary>Presents the question and waits for the user's answer.</summary>
        /// <param name="request">The question and predefined options to render.</param>
        /// <param name="cancellationToken">Cancellation for the pending question.</param>
        /// <returns>The user's answer, or an unanswered result when cancelled.</returns>
        Task<UserQuestionAnswer> AskAsync(UserQuestionRequest request, CancellationToken cancellationToken);
    }
}
