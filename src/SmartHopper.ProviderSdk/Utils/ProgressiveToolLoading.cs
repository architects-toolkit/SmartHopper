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

namespace SmartHopper.ProviderSdk.Utils
{
    /// <summary>
    /// Progressive tool loading: when the eligible tool set outgrows
    /// <see cref="InlineThreshold"/>, providers only send the core tools plus
    /// <see cref="SearchToolName"/> inline; the model discovers the rest on
    /// demand and calls them by name — execution is name-dispatched, so
    /// undiscovered-but-eligible tools still run. Mirrors the cloud-side
    /// mechanism so per-request tool schema cost stays flat as the registry
    /// grows.
    /// </summary>
    public static class ProgressiveToolLoading
    {
        /// <summary>Name of the discovery tool appended when the set collapses.</summary>
        public const string SearchToolName = "search_tools";

        /// <summary>Category the discovery tool registers under.</summary>
        public const string SearchToolCategory = "Assistant.Meta";

        /// <summary>Eligible-tool count above which the inline set collapses.</summary>
        public const int InlineThreshold = 12;

        /// <summary>Max tool definitions a single search_tools call returns.</summary>
        public const int ResultLimit = 6;

        /// <summary>
        /// Curated default tools for inspection, creation, user interaction,
        /// local guidance and a complete forum search/read loop. Names, not
        /// categories, keep duplicate wrappers and future category additions
        /// from silently increasing the per-request schema cost.
        /// Only enabled tools inside the request's scope and surface qualify;
        /// search_tools is the existing scope-independent discovery exception.
        /// Other eligible tools remain callable after discovery (or by name):
        /// this presentation policy is not an execution authorization boundary.
        /// </summary>
        public static readonly IReadOnlySet<string> AlwaysInlineToolNames = new HashSet<string>(StringComparer.Ordinal)
        {
            SearchToolName,
            "ask_user",
            "canvas_point",
            "canvas_view",
            "gh_generate",
            "gh_generate_and_place_on_canvas",
            "gh_get",
            "gh_group_selected",
            "gh_list_categories",
            "gh_list_components",
            "gh_report",
            "mcneel_forum_post_get",
            "mcneel_forum_search",
            "show_ghjson",
            "smarthopper_readme",
            "smarthopper_workflows",
        };
    }
}
