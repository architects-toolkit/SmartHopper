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
using SmartHopper.ProviderSdk.AICall.Core.Interactions;

namespace SmartHopper.Core.UI.Chat
{
    /// <summary>
    /// Tracks the Grasshopper document a chat is working on and produces
    /// <see cref="AIInteractionDocumentEvent"/> entries for the conversation history.
    /// Implementations must be resilient: failures return no events instead of throwing.
    /// </summary>
    public interface IChatDocumentTracker : IDisposable
    {
        /// <summary>
        /// Captures the initial baseline of the active document.
        /// </summary>
        /// <returns>An "Active document" event, or no events when no document is active.</returns>
        IReadOnlyList<AIInteractionDocumentEvent> Start();

        /// <summary>
        /// Compares the active document against the baseline captured at the last AI response.
        /// Emits a switch event when the active document changed (and rebaselines without diffing),
        /// or a canvas-change event when the same document has structural changes.
        /// </summary>
        /// <returns>The events to append before the next user message; empty when nothing changed.</returns>
        IReadOnlyList<AIInteractionDocumentEvent> CollectChanges();

        /// <summary>
        /// Captures the active document as the new baseline (called after each AI response).
        /// </summary>
        void Rebaseline();
    }

    /// <summary>
    /// Registration point for the chat document tracker. SmartHopper.Core cannot serialize canvases
    /// (GhJSON.Grasshopper lives in SmartHopper.Core.Grasshopper), so the Grasshopper layer registers
    /// a factory at plugin load.
    /// </summary>
    public static class ChatDocumentTracking
    {
        /// <summary>
        /// Gets or sets the factory that creates one tracker per chat dialog. Null disables tracking.
        /// </summary>
        public static Func<IChatDocumentTracker>? TrackerFactory { get; set; }

        /// <summary>
        /// Creates a tracker using <see cref="TrackerFactory"/>.
        /// </summary>
        /// <returns>A new tracker, or null when tracking is not available.</returns>
        public static IChatDocumentTracker? CreateTracker()
        {
            try
            {
                return TrackerFactory?.Invoke();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ChatDocumentTracking] Failed to create tracker: {ex.Message}");
                return null;
            }
        }
    }
}
