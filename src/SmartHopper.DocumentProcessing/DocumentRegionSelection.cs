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

namespace SmartHopper.DocumentProcessing
{
    /// <summary>Bounds for a normalized detail-crop region on a visible page.</summary>
    public sealed record DocumentRegionSelection
    {
        /// <summary>1-based page number the region applies to (must be a selected page).</summary>
        public int Page { get; init; }

        /// <summary>Normalized top-left x coordinate [0,1].</summary>
        public double X { get; init; }

        /// <summary>Normalized top-left y coordinate [0,1].</summary>
        public double Y { get; init; }

        /// <summary>Normalized width (0,1]; X + Width must not exceed 1.</summary>
        public double Width { get; init; }

        /// <summary>Normalized height (0,1]; Y + Height must not exceed 1.</summary>
        public double Height { get; init; }
    }
}
