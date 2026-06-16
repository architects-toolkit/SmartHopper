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

using SmartHopper.Infrastructure.AITools;

namespace SmartHopper.Core.Grasshopper.AITools
{
    /// <summary>
    /// Provides AI tools for the Ladybug Tools Discourse forum (discourse.ladybug.tools).
    /// </summary>
    public class discourse_ladybug_tools : DiscourseToolsBase
    {
        /// <inheritdoc/>
        protected override string? PresetBaseUrl => "https://discourse.ladybug.tools";

        /// <inheritdoc/>
        protected override string ForumName => "Ladybug Tools";

        /// <inheritdoc/>
        protected override string ToolPrefix => "ladybug";
    }
}
