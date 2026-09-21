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
using GhJSON.Core.SchemaModels;
using SmartHopper.Core.Grasshopper.Utils.Canvas;
using Xunit;

namespace SmartHopper.Core.Grasshopper.Tests.Utils.Canvas
{
    /// <summary>
    /// Tests the proposed-bounds store that feeds the canvas change preview overlay.
    /// </summary>
    public sealed class CanvasChangeReviewSessionTests
    {
        /// <summary>
        /// Verifies that measured bounds can be stored per component ID and read back unchanged.
        /// </summary>
        [Fact]
        public void ProposedComponentBounds_RoundTripsStoredValue()
        {
            var session = CreateSession();
            var expected = new RectangleF(12f, 34f, 140f, 52f);

            session.SetProposedComponentBounds(7, expected);

            Assert.True(session.TryGetProposedComponentBounds(7, out var actual));
            Assert.Equal(expected, actual);
        }

        /// <summary>
        /// Verifies that unknown component IDs report no stored bounds so the overlay falls back.
        /// </summary>
        [Fact]
        public void ProposedComponentBounds_UnknownIdReturnsFalse()
        {
            var session = CreateSession();
            session.SetProposedComponentBounds(3, new RectangleF(0f, 0f, 10f, 10f));

            Assert.False(session.TryGetProposedComponentBounds(99, out _));
        }

        private static CanvasChangeReviewSession CreateSession()
        {
            return new CanvasChangeReviewSession(
                "Review",
                "test",
                new GhJsonDocument(),
                Array.Empty<CanvasChangeReviewItem>());
        }
    }
}
