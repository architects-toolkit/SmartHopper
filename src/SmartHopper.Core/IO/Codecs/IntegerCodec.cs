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

using System.Globalization;
using Grasshopper.Kernel.Types;

namespace SmartHopper.Core.IO.Codecs
{
    /// <summary>
    /// Codec for GH_Integer goo values.
    /// </summary>
    internal sealed class IntegerCodec : IGooCodec
    {
        /// <inheritdoc/>
        public string TypeHint => "GH_Integer";

        /// <inheritdoc/>
        public int Priority => 0;

        /// <inheritdoc/>
        public bool CanEncode(IGH_Goo goo) => goo is GH_Integer;

        /// <inheritdoc/>
        public string Encode(IGH_Goo goo)
        {
            return ((GH_Integer)goo).Value.ToString(CultureInfo.InvariantCulture);
        }

        /// <inheritdoc/>
        public bool TryDecode(string data, out IGH_Goo goo, out string warning)
        {
            if (int.TryParse(data, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i))
            {
                goo = new GH_Integer(i);
                warning = null;
                return true;
            }

            goo = new GH_String(data);
            warning = "Failed to parse GH_Integer; restored as GH_String.";
            return true;
        }
    }
}
