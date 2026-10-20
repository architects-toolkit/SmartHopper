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
using System.Text;
using SmartHopper.ProviderSdk.AICall.Core.Base;

namespace SmartHopper.ProviderSdk.AICall.Core.Interactions
{
    /// <summary>
    /// Projects conversation history into the shape providers encode. Interactions implementing
    /// <see cref="IAIModelProjectedInteraction"/> are converted into user text: consecutive projected
    /// interactions are prepended to the next user text message (so providers that reject consecutive
    /// user messages keep working), or emitted as a standalone user message when no user text follows.
    /// </summary>
    public static class InteractionModelProjection
    {
        private const string Separator = "\n\n";

        /// <summary>
        /// Returns a provider-ready copy of <paramref name="interactions"/> with projected interactions converted to text.
        /// The input list is not modified.
        /// </summary>
        /// <param name="interactions">The conversation interactions.</param>
        /// <returns>A new list; empty when the input is null.</returns>
        public static List<IAIInteraction> Project(IReadOnlyList<IAIInteraction>? interactions)
        {
            var result = new List<IAIInteraction>();
            if (interactions == null || interactions.Count == 0)
            {
                return result;
            }

            var pending = new StringBuilder();
            IAIInteraction? pendingFirst = null;

            void Flush()
            {
                if (pending.Length > 0)
                {
                    result.Add(new AIInteractionText
                    {
                        Agent = AIAgent.User,
                        Content = pending.ToString(),
                        TurnId = pendingFirst?.TurnId,
                        Time = pendingFirst?.Time ?? DateTime.UtcNow,
                    });
                }

                pending.Clear();
                pendingFirst = null;
            }

            foreach (var interaction in interactions)
            {
                if (interaction is IAIModelProjectedInteraction projected)
                {
                    var text = projected.GetModelContent();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        if (pending.Length > 0)
                        {
                            pending.Append(Separator);
                        }

                        pending.Append(text);
                        pendingFirst ??= interaction;
                    }

                    continue;
                }

                if (pending.Length > 0 && interaction is AIInteractionText userText && userText.Agent == AIAgent.User)
                {
                    var merged = string.IsNullOrEmpty(userText.Content)
                        ? pending.ToString()
                        : pending.ToString() + Separator + userText.Content;
                    result.Add(userText with { Content = merged });
                    pending.Clear();
                    pendingFirst = null;
                    continue;
                }

                Flush();
                result.Add(interaction);
            }

            Flush();
            return result;
        }
    }
}
