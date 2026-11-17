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

using System.Collections;
using System.Linq;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

namespace SmartHopper.Components.Test.DataProcessor
{
    /// <summary>
    /// Helpers for building <see cref="GH_Structure{GH_String}"/> instances whose
    /// branches contain literal <see langword="null"/> elements — the input shape
    /// that previously crashed <c>DataTreeProcessor.GetBranchesKey</c> (D21).
    /// </summary>
    internal static class NullTreeBuilder
    {
        /// <summary>
        /// Appends a literal null element to the branch at <paramref name="path"/>.
        /// Tries the public <c>Append</c> API first and falls back to mutating the
        /// underlying branch <see cref="IList"/> when the append is ignored.
        /// </summary>
        /// <returns>Null element count observed in the branch after injection.</returns>
        public static int AppendNull(GH_Structure<GH_String> tree, GH_Path path)
        {
            tree.Append(null, path);
            if (CountNulls(tree, path) > 0)
            {
                return CountNulls(tree, path);
            }

            tree.EnsurePath(path);
            if (tree.get_Branch(path) is IList branch)
            {
                branch.Add(null);
            }

            return CountNulls(tree, path);
        }

        /// <summary>
        /// Counts literal null elements in the branch at <paramref name="path"/>;
        /// returns 0 when the path does not exist.
        /// </summary>
        public static int CountNulls(GH_Structure<GH_String> tree, GH_Path path)
        {
            if (tree == null || !tree.PathExists(path) || tree.get_Branch(path) is not IList branch)
            {
                return 0;
            }

            return branch.Cast<object>().Count(item => item == null);
        }
    }
}
