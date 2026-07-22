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
using System.Drawing.Drawing2D;
using System.Linq;
using Grasshopper;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using SmartHopper.Infrastructure.Mcp;

namespace SmartHopper.Core.Grasshopper.Utils.Canvas
{
    /// <summary>
    /// Draws a subtle but visible overlay on canvas objects that are protected from
    /// MCP-driven mutations, including the SmartHopper MCP server while enabled and
    /// any component the user has explicitly locked.
    /// </summary>
    public static class CanvasProtectionOverlay
    {
        private static bool initialized;

        /// <summary>
        /// Hooks the active Grasshopper canvas paint events. Safe to call multiple times.
        /// </summary>
        public static void EnsureInitialized()
        {
            if (initialized)
            {
                return;
            }

            AttachToCanvas(Instances.ActiveCanvas);
            Instances.CanvasCreated += OnCanvasCreated;
            McpCanvasLockState.LockChanged += (_, _) => RefreshCanvas();
            initialized = true;
        }

        private static void OnCanvasCreated(GH_Canvas canvas)
        {
            AttachToCanvas(canvas);
        }

        private static void AttachToCanvas(GH_Canvas? canvas)
        {
            if (canvas == null)
            {
                return;
            }

            canvas.CanvasPostPaintOverlay -= OnCanvasPostPaintOverlay;
            canvas.CanvasPostPaintOverlay += OnCanvasPostPaintOverlay;
        }

        /// <summary>
        /// Redraws the canvas if the active canvas is available.
        /// </summary>
        public static void RefreshCanvas()
        {
            try
            {
                Instances.ActiveCanvas?.Refresh();
            }
            catch
            {
                // Ignore if the canvas is not ready.
            }
        }

        private static void OnCanvasPostPaintOverlay(GH_Canvas canvas)
        {
            if (canvas?.Document == null || canvas.Graphics == null)
            {
                return;
            }

            // The canvas graphics object still has the viewport projection applied when
            // post-paint overlay events are raised. We want to draw the lock overlay in
            // screen pixels, so we temporarily reset the transform and project component
            // bounds to control coordinates ourselves. Without this reset the bounds are
            // projected twice and the overlay is drawn offset from the actual component.
            var graphics = canvas.Graphics;
            var oldTransform = graphics.Transform;
            graphics.ResetTransform();

            try
            {
                using var overlayPen = new Pen(Color.FromArgb(230, 255, 140, 0), 2f)
                {
                    DashStyle = DashStyle.Solid,
                };

                using var iconPen = new Pen(Color.FromArgb(230, 255, 140, 0), 1.5f);
                using var iconBrush = new SolidBrush(Color.FromArgb(230, 255, 140, 0));

                foreach (var obj in canvas.Document.Objects.OfType<IGH_DocumentObject>())
                {
                    if (!CanvasProtection.IsProtected(obj))
                    {
                        continue;
                    }

                    if (obj.Attributes == null)
                    {
                        continue;
                    }

                    var bounds = obj.Attributes.Bounds;
                    if (bounds.IsEmpty)
                    {
                        continue;
                    }

                    var screenBounds = ProjectBounds(canvas.Viewport, bounds);
                    if (screenBounds.Width < 4 || screenBounds.Height < 4)
                    {
                        continue;
                    }

                    DrawBorder(graphics, screenBounds, overlayPen);
                    DrawLockIcon(graphics, screenBounds, iconPen, iconBrush);
                }
            }
            finally
            {
                graphics.Transform = oldTransform;
                oldTransform?.Dispose();
            }
        }

        private static RectangleF ProjectBounds(GH_Viewport viewport, RectangleF bounds)
        {
            var topLeft = new PointF(bounds.Left, bounds.Top);
            var bottomRight = new PointF(bounds.Right, bounds.Bottom);
            viewport.Project(ref topLeft);
            viewport.Project(ref bottomRight);
            return RectangleF.FromLTRB(topLeft.X, topLeft.Y, bottomRight.X, bottomRight.Y);
        }

        private static void DrawBorder(Graphics graphics, RectangleF bounds, Pen pen)
        {
            // Slightly inflate so the border sits just outside the component capsule.
            var border = RectangleF.Inflate(bounds, 2f, 2f);
            graphics.DrawRectangle(pen, border.X, border.Y, border.Width, border.Height);
        }

        private static void DrawLockIcon(Graphics graphics, RectangleF bounds, Pen pen, Brush brush)
        {
            const float IconSize = 14f;
            const float Padding = 3f;

            float x = bounds.Right - IconSize - Padding;
            float y = bounds.Top + Padding;

            // Keep the icon inside the component bounds.
            if (x < bounds.Left)
            {
                x = bounds.Left + Padding;
            }

            if (y + IconSize > bounds.Bottom)
            {
                y = bounds.Bottom - IconSize - Padding;
            }

            // Draw shackle.
            float shackleWidth = IconSize * 0.6f;
            float shackleHeight = IconSize * 0.55f;
            float shackleX = x + (IconSize - shackleWidth) / 2f;
            float shackleY = y;
            graphics.DrawArc(pen, shackleX, shackleY, shackleWidth, shackleHeight, 180f, 180f);

            // Draw body.
            float bodyHeight = IconSize * 0.55f;
            float bodyY = y + shackleHeight / 2f;
            var bodyRect = new RectangleF(x + 1, bodyY, IconSize - 2, bodyHeight);
            graphics.FillRectangle(brush, bodyRect);
            graphics.DrawRectangle(pen, bodyRect.X, bodyRect.Y, bodyRect.Width, bodyRect.Height);

            // Draw keyhole.
            using var keyholeBrush = new SolidBrush(Color.White);
            float keyholeX = x + IconSize / 2f;
            float keyholeY = bodyY + bodyHeight / 2f;
            graphics.FillEllipse(keyholeBrush, keyholeX - 1.5f, keyholeY - 2.5f, 3f, 3f);
            graphics.FillRectangle(keyholeBrush, keyholeX - 0.75f, keyholeY, 1.5f, 3f);
        }
    }
}
