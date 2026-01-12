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

using System;
using System.Collections.Generic;
using System.Linq;
using GhJSON.Grasshopper.Canvas;

namespace SmartHopper.Core.Grasshopper.Utils.Internal
{
    /// <summary>
    /// Parses string filter tokens into strongly-typed filter objects for ghjson-dotnet.
    /// </summary>
    internal static class FilterParser
    {
        private static readonly Dictionary<string, string> TypeSynonyms = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["params"] = "params",
            ["param"] = "params",
            ["parameters"] = "params",
            ["components"] = "components",
            ["component"] = "components",
            ["startnodes"] = "startnodes",
            ["startnode"] = "startnodes",
            ["endnodes"] = "endnodes",
            ["endnode"] = "endnodes",
            ["middlenodes"] = "middlenodes",
            ["middlenode"] = "middlenodes",
            ["isolatednodes"] = "isolatednodes",
            ["isolatednode"] = "isolatednodes",
        };

        private static readonly Dictionary<string, string> AttrSynonyms = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["selected"] = "selected",
            ["unselected"] = "unselected",
            ["enabled"] = "enabled",
            ["unlocked"] = "enabled",
            ["disabled"] = "disabled",
            ["locked"] = "disabled",
            ["error"] = "error",
            ["errors"] = "error",
            ["warning"] = "warning",
            ["warnings"] = "warning",
            ["warn"] = "warning",
            ["remark"] = "remark",
            ["remarks"] = "remark",
            ["info"] = "remark",
            ["previewcapable"] = "previewcapable",
            ["notpreviewcapable"] = "notpreviewcapable",
            ["previewon"] = "previewon",
            ["visible"] = "previewon",
            ["previewoff"] = "previewoff",
            ["hidden"] = "previewoff",
        };

        private static readonly Dictionary<string, string> CategorySynonyms = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["math"] = "Maths",
            ["maths"] = "Maths",
            ["vector"] = "Vector",
            ["curve"] = "Curve",
            ["surface"] = "Surface",
            ["mesh"] = "Mesh",
            ["intersect"] = "Intersect",
            ["transform"] = "Transform",
            ["sets"] = "Sets",
            ["display"] = "Display",
            ["params"] = "Params",
            ["rhino"] = "Rhino",
            ["script"] = "Script",
        };

        /// <summary>
        /// Parses attribute filter tokens into a strongly-typed AttributeFilter.
        /// </summary>
        public static AttributeFilter? ParseAttributeFilter(IEnumerable<string>? tokens)
        {
            if (tokens == null || !tokens.Any())
            {
                return null;
            }

            var (include, exclude) = ParseIncludeExclude(tokens, AttrSynonyms);
            if (include.Count == 0 && exclude.Count == 0)
            {
                return null;
            }

            var filter = new AttributeFilter();

            foreach (var token in include)
            {
                filter.Include |= token.ToLowerInvariant() switch
                {
                    "selected" => GetAttributes.Selected,
                    "unselected" => GetAttributes.Unselected,
                    "enabled" => GetAttributes.Enabled,
                    "disabled" => GetAttributes.Disabled,
                    "error" => GetAttributes.HasError,
                    "warning" => GetAttributes.HasWarning,
                    "remark" => GetAttributes.HasRemark,
                    "previewon" => GetAttributes.PreviewOn,
                    "previewoff" => GetAttributes.PreviewOff,
                    "previewcapable" => GetAttributes.PreviewCapable,
                    "notpreviewcapable" => GetAttributes.NotPreviewCapable,
                    _ => 0,
                };
            }

            foreach (var token in exclude)
            {
                filter.Exclude |= token.ToLowerInvariant() switch
                {
                    "selected" => GetAttributes.Selected,
                    "unselected" => GetAttributes.Unselected,
                    "enabled" => GetAttributes.Enabled,
                    "disabled" => GetAttributes.Disabled,
                    "error" => GetAttributes.HasError,
                    "warning" => GetAttributes.HasWarning,
                    "remark" => GetAttributes.HasRemark,
                    "previewon" => GetAttributes.PreviewOn,
                    "previewoff" => GetAttributes.PreviewOff,
                    "previewcapable" => GetAttributes.PreviewCapable,
                    "notpreviewcapable" => GetAttributes.NotPreviewCapable,
                    _ => 0,
                };
            }

            return filter;
        }

        /// <summary>
        /// Parses type filter tokens into a strongly-typed TypeFilter.
        /// </summary>
        public static TypeFilter? ParseTypeFilter(IEnumerable<string>? tokens)
        {
            if (tokens == null || !tokens.Any())
            {
                return null;
            }

            var (include, exclude) = ParseIncludeExclude(tokens, TypeSynonyms);
            if (include.Count == 0 && exclude.Count == 0)
            {
                return null;
            }

            var filter = new TypeFilter();

            foreach (var token in include)
            {
                switch (token.ToLowerInvariant())
                {
                    case "params":
                        filter.Include |= GetObjectKinds.Params;
                        break;
                    case "components":
                        filter.Include |= GetObjectKinds.Components;
                        break;
                    case "startnodes":
                        filter.IncludeRoles |= GetNodeRoles.StartNodes;
                        break;
                    case "endnodes":
                        filter.IncludeRoles |= GetNodeRoles.EndNodes;
                        break;
                    case "middlenodes":
                        filter.IncludeRoles |= GetNodeRoles.MiddleNodes;
                        break;
                    case "isolatednodes":
                        filter.IncludeRoles |= GetNodeRoles.IsolatedNodes;
                        break;
                }
            }

            foreach (var token in exclude)
            {
                switch (token.ToLowerInvariant())
                {
                    case "params":
                        filter.Exclude |= GetObjectKinds.Params;
                        break;
                    case "components":
                        filter.Exclude |= GetObjectKinds.Components;
                        break;
                    case "startnodes":
                        filter.ExcludeRoles |= GetNodeRoles.StartNodes;
                        break;
                    case "endnodes":
                        filter.ExcludeRoles |= GetNodeRoles.EndNodes;
                        break;
                    case "middlenodes":
                        filter.ExcludeRoles |= GetNodeRoles.MiddleNodes;
                        break;
                    case "isolatednodes":
                        filter.ExcludeRoles |= GetNodeRoles.IsolatedNodes;
                        break;
                }
            }

            return filter;
        }

        /// <summary>
        /// Parses category filter tokens into a strongly-typed CategoryFilter.
        /// </summary>
        public static CategoryFilter? ParseCategoryFilter(IEnumerable<string>? tokens)
        {
            if (tokens == null || !tokens.Any())
            {
                return null;
            }

            var (include, exclude) = ParseIncludeExclude(tokens, CategorySynonyms);
            if (include.Count == 0 && exclude.Count == 0)
            {
                return null;
            }

            return new CategoryFilter(include, exclude);
        }

        private static (HashSet<string> include, HashSet<string> exclude) ParseIncludeExclude(
            IEnumerable<string> tokens,
            Dictionary<string, string>? synonyms = null)
        {
            var include = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var exclude = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var raw in tokens)
            {
                var token = raw?.Trim();
                if (string.IsNullOrWhiteSpace(token))
                {
                    continue;
                }

                var isExclude = token.StartsWith("-", StringComparison.Ordinal);
                var isInclude = token.StartsWith("+", StringComparison.Ordinal);
                if (isExclude || isInclude)
                {
                    token = token.Substring(1);
                }

                token = token.Trim();
                if (string.IsNullOrWhiteSpace(token))
                {
                    continue;
                }

                if (synonyms != null && synonyms.TryGetValue(token, out var canonical))
                {
                    token = canonical;
                }

                if (isExclude)
                {
                    exclude.Add(token);
                }
                else
                {
                    include.Add(token);
                }
            }

            return (include, exclude);
        }
    }
}
