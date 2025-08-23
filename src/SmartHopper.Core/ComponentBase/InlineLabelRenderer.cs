/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

/*
 * InlineLabelRenderer: Utility to render small inline tooltip-like labels anchored
 * to UI rectangles on the Grasshopper canvas.
 * Purpose: Centralize inline label drawing for provider icons and badges to avoid
 * code duplication and ensure consistent styling.
 */

using System.Drawing;
using Grasshopper.Kernel;

namespace SmartHopper.Core.ComponentBase
{
    /// <summary>
    /// Provides helper methods to draw compact inline labels anchored to a rectangle.
    /// </summary>
    internal static class InlineLabelRenderer
    {
        /// <summary>
        /// Draws an inline tooltip-like label above the given anchor rectangle.
        /// The label uses GH small font, dark background and light border/text.
        /// </summary>
        /// <param name="g">Target graphics.</param>
        /// <param name="anchor">Anchor rectangle the label should relate to.</param>
        /// <param name="text">Text to display inside the label.</param>
        public static void DrawInlineLabel(Graphics g, RectangleF anchor, string text)
        {
            var font = GH_FontServer.Small;
            var size = g.MeasureString(text, font);
            var padding = 4f;
            var width = size.Width + padding * 2f;
            var height = size.Height + padding * 1.5f;
            var x = anchor.Left + (anchor.Width - width) / 2f;
            var y = anchor.Top - height - 4f; // small gap above

            var rect = new RectangleF(x, y, width, height);
            using (var bg = new SolidBrush(Color.FromArgb(220, 30, 30, 30)))
            using (var pen = new Pen(Color.FromArgb(160, 255, 255, 255), 1f))
            using (var fg = new SolidBrush(Color.White))
            {
                g.FillRectangle(bg, rect);
                g.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);
                var tx = rect.X + padding;
                var ty = rect.Y + (rect.Height - size.Height) / 2f;
                g.DrawString(text, font, fg, new PointF(tx, ty));
            }
        }
    }
}
