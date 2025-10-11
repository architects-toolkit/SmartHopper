/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using System;

namespace SmartHopper.Infrastructure.AICall.Core.Interactions
{
    /// <summary>
    /// Provides stable identity keys for chat rendering logic.
    /// Implement on interaction types to avoid type-specific switches in UI observers.
    /// </summary>
    public interface IAIKeyedInteraction
    {
        /// <summary>
        /// Gets a stable stream grouping key for this interaction.
        /// Interactions with the same key will be coalesced during streaming (e.g., assistant text).
        /// </summary>
        /// <returns>Stream group key.</returns>
        string GetStreamKey();

        /// <summary>
        /// Gets a stable de-duplication key for this interaction.
        /// Used to prevent re-adding the same logical item to the UI.
        /// </summary>
        /// <returns>De-duplication key.</returns>
        string GetDedupKey();
    }
}
