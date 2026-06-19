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

using Grasshopper.Kernel.Types;

namespace SmartHopper.Core.IO.Codecs
{
    /// <summary>
    /// Interface for codecs that encode individual IGH_Goo instances to/from
    /// canonical string payloads for safe persistence.
    /// </summary>
    public interface IGooCodec
    {
        /// <summary>
        /// Gets the type-hint prefix used in payloads (e.g., "GH_String").
        /// </summary>
        string TypeHint { get; }

        /// <summary>
        /// Gets the resolution priority. Lower values are checked first.
        /// </summary>
        int Priority { get; }

        /// <summary>
        /// Returns true if this codec can encode the given goo instance.
        /// </summary>
        bool CanEncode(IGH_Goo goo);

        /// <summary>
        /// Encodes the goo into a payload string (without the type-hint prefix).
        /// </summary>
        string Encode(IGH_Goo goo);

        /// <summary>
        /// Attempts to decode the payload data into an IGH_Goo.
        /// Never throws.
        /// </summary>
        bool TryDecode(string data, out IGH_Goo goo, out string warning);
    }
}
