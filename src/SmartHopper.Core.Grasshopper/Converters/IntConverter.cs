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
using System.Linq;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

namespace SmartHopper.Core.Grasshopper.Converters
{
    public class IntConverter
    {
        public static GH_Integer IntToGHInteger(int value)
        {
            return new GH_Integer(value);
        }

        public static GH_Structure<GH_Integer> IntToGHInteger(List<int> list)
        {
            if (list == null) return new GH_Structure<GH_Integer>();

            var structure = new GH_Structure<GH_Integer>();
            var path = new GH_Path(0);
            structure.AppendRange(list.Select(IntToGHInteger), path);
            return structure;
        }





        public static int GHIntegerToInt(GH_Integer ghInteger)
        {
            return ghInteger?.Value ?? 0;
        }

        public static List<int> GHIntegerToInt(GH_Structure<GH_Integer> structure)
        {
            if (structure == null) return new List<int>();
            return structure.AllData(true).Select(gh => GHIntegerToInt(gh as GH_Integer)).ToList();
        }


    }
}
