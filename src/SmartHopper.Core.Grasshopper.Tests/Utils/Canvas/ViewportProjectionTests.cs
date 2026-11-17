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

using SmartHopper.Core.Grasshopper.Utils.Canvas;
using Xunit;

namespace SmartHopper.Core.Grasshopper.Tests.Utils.Canvas
{
    /// <summary>
    /// Pure projection-math tests for <see cref="ViewportProjection"/>: target
    /// computation and bounds-fit zoom, without a Rhino runtime.
    /// </summary>
    public sealed class ViewportProjectionTests
    {
        /// <summary>
        /// The target formula is the inverse of the world→control projection:
        /// center * zoom + target must land on the view midpoint (within one pixel).
        /// </summary>
        [Fact]
        public void TargetForCenter_MapsCenterToViewMidpoint()
        {
            var target = ViewportProjection.TargetForCenter(800, 600, centerX: 150, centerY: -40, zoom: 2f);

            Assert.Equal(800 / 2f - 150 * 2f, target.X, 0.5f);
            Assert.Equal(600 / 2f - (-40f) * 2f, target.Y, 0.5f);
        }

        /// <summary>
        /// Target values are rounded to whole control pixels; the residual center
        /// error stays within half a pixel per axis (~0.5/zoom world units).
        /// </summary>
        [Fact]
        public void TargetForCenter_RoundsToControlPixels()
        {
            var target = ViewportProjection.TargetForCenter(801, 599, centerX: 33.3f, centerY: 7.7f, zoom: 0.4f);

            // Point is integer-typed: the result is already a whole-pixel target.
            Assert.Equal(System.Math.Round(801 / 2f - 33.3f * 0.4f), target.X);
            Assert.Equal(System.Math.Round(599 / 2f - 7.7f * 0.4f), target.Y);
        }

        /// <summary>
        /// Fit zoom picks the tighter of the two dimensions and respects padding.
        /// </summary>
        [Fact]
        public void FitZoom_PicksTighterDimensionWithPadding()
        {
            // 800x600 view, 24px padding → available 752x552. Bounds 752x276 →
            // width fits at zoom 1, height needs 552/276 = 2 → tighter is width.
            var zoom = ViewportProjection.FitZoom(800, 600, 752, 276, 24, 0.01f, 32f);

            Assert.Equal(1f, zoom, 0.001f);
        }

        /// <summary>
        /// Oversized bounds zoom out below 1 but never below the minimum clamp.
        /// </summary>
        [Fact]
        public void FitZoom_ClampsToMinimum()
        {
            var zoom = ViewportProjection.FitZoom(100, 100, 1_000_000, 1_000_000, 24, 0.01f, 32f);

            Assert.Equal(0.01f, zoom);
        }

        /// <summary>
        /// Degenerate bounds dimensions and tiny viewports stay finite — at least
        /// one canvas unit / one available pixel is used.
        /// </summary>
        [Fact]
        public void FitZoom_DegenerateInputs_StayFinite()
        {
            var zero = ViewportProjection.FitZoom(800, 600, 0, 0, 24, 0.01f, 32f);
            var tiny = ViewportProjection.FitZoom(1, 1, 100, 100, 24, 0.01f, 32f);

            Assert.False(float.IsNaN(zero) || float.IsInfinity(zero));
            Assert.False(float.IsNaN(tiny) || float.IsInfinity(tiny));
            Assert.Equal(32f, zero);
            Assert.Equal(0.01f, tiny);
        }
    }
}
