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
using System.Drawing;

namespace SmartHopper.Core.Grasshopper.Utils.Canvas
{
    /// <summary>
    /// Pure world↔control projection math for <c>GH_Viewport</c>. Kept free of
    /// Grasshopper types so the fit/target transforms can be unit-tested without a
    /// Rhino runtime; callers apply the results through the public viewport API
    /// and then call <c>ComputeProjection()</c> so subsequent reads observe the
    /// applied state rather than a cached region.
    /// </summary>
    public static class ViewportProjection
    {
        /// <summary>Control-pixel padding applied on each side when fitting bounds.</summary>
        public const float FramePaddingPixels = 24f;

        /// <summary>
        /// Computes the viewport <c>Target</c> (canvas-space control offset) that
        /// places the world-space point (<paramref name="centerX"/>,
        /// <paramref name="centerY"/>) at the middle of the viewport. The applied
        /// projection maps world→control as <c>control = world * zoom + target</c>,
        /// so <c>target = viewSize / 2 − center * zoom</c>, rounded to control pixels.
        /// </summary>
        public static Point TargetForCenter(
            float viewWidth,
            float viewHeight,
            float centerX,
            float centerY,
            float zoom)
        {
            var tx = (int)Math.Round(viewWidth / 2f - centerX * zoom);
            var ty = (int)Math.Round(viewHeight / 2f - centerY * zoom);
            return new Point(tx, ty);
        }

        /// <summary>
        /// Computes the zoom factor that fits <paramref name="boundsWidth"/> ×
        /// <paramref name="boundsHeight"/> into the viewport minus
        /// <paramref name="padding"/> control pixels on each side, clamped to
        /// [<paramref name="minZoom"/>, <paramref name="maxZoom"/>]. Degenerate
        /// bounds dimensions and tiny viewports use at least one unit so the fit
        /// stays finite.
        /// </summary>
        public static float FitZoom(
            float viewWidth,
            float viewHeight,
            float boundsWidth,
            float boundsHeight,
            float padding,
            float minZoom,
            float maxZoom)
        {
            var w = Math.Max(boundsWidth, 1f);
            var h = Math.Max(boundsHeight, 1f);
            var availW = Math.Max(viewWidth - 2f * padding, 1f);
            var availH = Math.Max(viewHeight - 2f * padding, 1f);
            var zoom = Math.Min(availW / w, availH / h);
            return Math.Max(minZoom, Math.Min(maxZoom, zoom));
        }
    }
}
