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

using Newtonsoft.Json.Linq;

namespace SmartHopper.Infrastructure.AICall.Core.Interactions
{
    /// <summary>
    /// Convenience extensions for safely accessing <see cref="AIInteractionToolCall"/> data.
    /// </summary>
    public static class AIInteractionToolCallExtensions
    {
        /// <summary>
        /// Returns the tool call arguments, or an empty <see cref="JObject"/> when the tool call
        /// or its arguments are <c>null</c>.
        /// </summary>
        /// <param name="toolCall">The tool call to read arguments from.</param>
        /// <returns>The tool call arguments, or an empty <see cref="JObject"/>.</returns>
        public static JObject GetArgumentsOrEmpty(this AIInteractionToolCall toolCall)
        {
            return toolCall?.Arguments ?? new JObject();
        }
    }
}
