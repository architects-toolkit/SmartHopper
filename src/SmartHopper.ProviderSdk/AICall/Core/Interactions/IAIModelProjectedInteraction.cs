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

namespace SmartHopper.ProviderSdk.AICall.Core.Interactions
{
    /// <summary>
    /// Interaction that is stored, rendered and persisted with its own type, but is sent to
    /// AI providers as plain user-side text. Providers obtain that text through
    /// <see cref="InteractionModelProjection"/>, so new projected interaction types need no
    /// provider-specific encoding.
    /// </summary>
    public interface IAIModelProjectedInteraction
    {
        /// <summary>
        /// Gets the text that represents this interaction in the model conversation.
        /// </summary>
        /// <returns>The model-facing text, or an empty string to omit the interaction.</returns>
        string GetModelContent();
    }
}
