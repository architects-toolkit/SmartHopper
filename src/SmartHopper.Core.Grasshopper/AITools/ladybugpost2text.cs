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
using SmartHopper.Infrastructure.AITools;

namespace SmartHopper.Core.Grasshopper.AITools
{
    /// <summary>
    /// Provides AI tools for fetching and summarizing Ladybug Tools Discourse forum posts.
    /// </summary>
    public class ladybugpost2text : DiscourseToolsBase
    {
        /// <inheritdoc/>
        protected override string BaseUrl => "https://discourse.ladybug.tools";

        /// <inheritdoc/>
        protected override string ForumName => "Ladybug Tools";

        /// <inheritdoc/>
        protected override string ToolPrefix => "ladybug";

        /// <inheritdoc/>
        public override IEnumerable<AITool> GetTools()
        {
            // Return only post-related tools from base implementation
            foreach (var tool in base.GetTools())
            {
                string name = tool.Name;
                if (name.Contains("_post_") || name == $"{this.ToolPrefix}_forum_search")
                {
                    yield return tool;
                }
            }
        }
    }
}
