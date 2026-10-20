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
using SmartHopper.ProviderSdk.AICall.Core.Base;

namespace SmartHopper.ProviderSdk.AICall.Core.Interactions
{
    /// <summary>
    /// Conversation-level record of the document the chat is working on: which document is active
    /// when the chat starts, when the active document switches, and a deterministic summary of canvas
    /// changes since the last AI response. Persisted in history, rendered as a compact card in chat UIs,
    /// and sent to the model as user-side text through <see cref="IAIModelProjectedInteraction"/>.
    /// </summary>
    public sealed record AIInteractionDocumentEvent : AIInteractionBase, IAIKeyedInteraction, IAIRenderInteraction, IAIModelProjectedInteraction
    {
        /// <summary>
        /// Gets the unique identifier of this event. Keeps repeated identical events (e.g. switching back and
        /// forth between two documents) distinct in UI keying and history.
        /// </summary>
        public Guid Id { get; init; } = Guid.NewGuid();

        /// <summary>
        /// Gets the kind of document event.
        /// </summary>
        public AIDocumentEventKind Kind { get; init; }

        /// <summary>
        /// Gets the identifier of the document the event refers to, or null when no document is active.
        /// </summary>
        public string? DocumentId { get; init; }

        /// <summary>
        /// Gets the display name of the document the event refers to, or null when no document is active.
        /// </summary>
        public string? DocumentName { get; init; }

        /// <summary>
        /// Gets the deterministic Markdown text describing the event. The same text is shown in the UI and sent to the model.
        /// </summary>
        public string Content { get; init; }

        /// <summary>
        /// Gets the agent. Document events are user-side observations, so they are always <see cref="AIAgent.User"/>.
        /// The init accessor is intentionally a no-op.
        /// </summary>
        public override AIAgent Agent
        {
            get => AIAgent.User;
            init { /* Fixed agent; init retained for base-class/serialization compatibility. */ }
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return this.Content ?? string.Empty;
        }

        /// <inheritdoc />
        public string GetModelContent()
        {
            return string.IsNullOrWhiteSpace(this.Content) ? string.Empty : $"[Canvas] {this.Content}";
        }

        /// <inheritdoc />
        public string GetStreamKey()
        {
            return $"document-event:{this.Id:N}";
        }

        /// <inheritdoc />
        public string GetDedupKey()
        {
            return this.GetStreamKey();
        }

        /// <summary>
        /// Gets the CSS role class used when rendering. Reuses the collapsible system card style.
        /// </summary>
        /// <returns>The role class.</returns>
        public string GetRoleClassForRender()
        {
            return "system";
        }

        /// <summary>
        /// Gets the header label used when rendering.
        /// </summary>
        /// <returns>The display name.</returns>
        public string GetDisplayNameForRender()
        {
            return "Canvas";
        }

        /// <inheritdoc />
        public string GetRawContentForRender()
        {
            return this.Content ?? string.Empty;
        }

        /// <summary>
        /// Document events do not include a reasoning section.
        /// </summary>
        /// <returns>An empty string.</returns>
        public string GetRawReasoningForRender()
        {
            return string.Empty;
        }
    }
}
