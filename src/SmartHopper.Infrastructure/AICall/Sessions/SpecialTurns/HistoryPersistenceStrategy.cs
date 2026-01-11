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
 * along with this library; if not, write to the Free Software Foundation,
 * Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301, USA.
 */

namespace SmartHopper.Infrastructure.AICall.Sessions.SpecialTurns
{
    /// <summary>
    /// Defines how special turn interactions are persisted to conversation history.
    /// </summary>
    public enum HistoryPersistenceStrategy
    {
        /// <summary>
        /// Persist only the result interaction(s) to history.
        /// Input interactions from the special turn are discarded.
        /// </summary>
        PersistResult,

        /// <summary>
        /// Persist all interactions (input + result) to history.
        /// Use PersistenceFilter to control which interaction types are included.
        /// </summary>
        PersistAll,

        /// <summary>
        /// Execute the turn but don't persist anything to history.
        /// Useful for internal processing without disturbing UI/history.
        /// </summary>
        Ephemeral,

        /// <summary>
        /// Replace all previous interactions with the result of this turn.
        /// Use PersistenceFilter to control which types are replaced vs preserved.
        /// </summary>
        ReplaceAbove,
    }
}
