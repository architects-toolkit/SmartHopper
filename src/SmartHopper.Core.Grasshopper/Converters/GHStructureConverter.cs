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

using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

namespace SmartHopper.Core.Grasshopper.Converters
{
    /// <summary>
    /// Provides utility methods for converting GH_Structure types.
    /// </summary>
    public static class GHStructureConverter
    {
        /// <summary>
        /// Converts a GH_Structure of a specific type to GH_Structure of IGH_Goo.
        /// This is useful for preparing input trees with mixed Grasshopper data types
        /// for heterogeneous processing pipelines.
        /// </summary>
        /// <typeparam name="T">The concrete IGH_Goo type of the source structure.</typeparam>
        /// <param name="tree">The source tree to convert. Can be null.</param>
        /// <returns>A new GH_Structure&lt;IGH_Goo&gt; containing all items from the source.</returns>
        public static GH_Structure<IGH_Goo> ConvertToGooTree<T>(GH_Structure<T> tree)
            where T : IGH_Goo
        {
            var result = new GH_Structure<IGH_Goo>();
            if (tree == null) return result;

            foreach (var path in tree.Paths)
            {
                var branch = tree.get_Branch(path);
                if (branch == null) continue;

                foreach (var item in branch)
                {
                    if (item is IGH_Goo goo)
                    {
                        result.Append(goo, path);
                    }
                }
            }

            return result;
        }
    }
}
