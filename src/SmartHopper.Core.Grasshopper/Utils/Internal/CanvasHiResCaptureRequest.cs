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

namespace SmartHopper.Core.Grasshopper.Utils.Internal
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;

    /// <summary>
    /// Selects which canvas region a hi-res capture renders.
    /// </summary>
    public enum CanvasHiResScope
    {
        /// <summary>Whole document bounding box.</summary>
        Document = 0,

        /// <summary>Union of the bounds of the currently selected objects.</summary>
        Selection = 1,

        /// <summary>Union of the bounds of the objects listed in <see cref="CanvasHiResCaptureRequest.Guids"/>.</summary>
        Guids = 2,

        /// <summary>Explicit canvas-space rectangle from <see cref="CanvasHiResCaptureRequest.Bounds"/>.</summary>
        Bounds = 3,
    }

    /// <summary>
    /// Parameters for a single hi-res canvas capture.
    /// </summary>
    public sealed class CanvasHiResCaptureRequest
    {
        /// <summary>Which canvas region to render.</summary>
        public CanvasHiResScope Scope { get; set; } = CanvasHiResScope.Document;

        /// <summary>Instance GUIDs to frame when <see cref="Scope"/> is <see cref="CanvasHiResScope.Guids"/>.</summary>
        public IReadOnlyList<Guid> Guids { get; set; } = Array.Empty<Guid>();

        /// <summary>Explicit canvas-space rectangle when <see cref="Scope"/> is <see cref="CanvasHiResScope.Bounds"/>.</summary>
        public RectangleF Bounds { get; set; }

        /// <summary>Padding in canvas units added around the resolved region.</summary>
        public float Padding { get; set; } = 20f;

        /// <summary>Render zoom factor; 1.0 matches on-screen size.</summary>
        public float Scale { get; set; } = 1f;

        /// <summary>Background colour; use <see cref="Color.Transparent"/> for an alpha channel.</summary>
        public Color Background { get; set; } = Color.Transparent;

        /// <summary>Maximum allowed output width or height in pixels.</summary>
        public int MaxDimension { get; set; } = CanvasHiResCaptureService.DefaultMaxDimension;
    }
}
