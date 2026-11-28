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

namespace SmartHopper.Infrastructure.AICall.Subagents
{
    /// <summary>
    /// How <c>send_to_subagent</c> delivers an instruction while the worker is mid-run.
    /// </summary>
    public enum SubagentSendMode
    {
        /// <summary>
        /// Hold the instruction in the subagent's pending queue; the host delivers it
        /// automatically as a new run when the current run finishes. This is the
        /// default — a queued instruction is never rejected or lost.
        /// </summary>
        Queue,

        /// <summary>
        /// Deliver immediately into the live run ("steering"): the instruction is
        /// appended to the worker session so the next provider call reads it. When
        /// the history tail cannot accept a user message (a tool call is pending),
        /// the instruction falls back to <see cref="Queue"/> semantics.
        /// </summary>
        OnTheFly,
    }
}
