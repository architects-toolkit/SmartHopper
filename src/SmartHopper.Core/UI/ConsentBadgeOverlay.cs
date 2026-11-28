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

/*
 * ConsentBadgeOverlay.cs
 * Draws a SmartHopper-green triangle badge above components that are processing
 * while an external-source consent prompt is awaiting an answer.
 */

using System;
using System.Diagnostics;
using System.Drawing;
using Grasshopper;
using Grasshopper.GUI.Canvas;
using SmartHopper.Core.ComponentBase;
using SmartHopper.Core.ComponentBase.State;
using SmartHopper.Infrastructure.FileSystem;
using SmartHopper.Infrastructure.Networking;
using SmartHopper.ProviderSdk.Logging;

namespace SmartHopper.Core.UI
{
    /// <summary>
    /// Canvas post-paint overlay that marks components still processing while
    /// <see cref="ExternalSourceAccess.IsAwaitingConsent"/> is true. Consent prompts
    /// are serialized, so at most one prompt is open at a time; the badge flags every
    /// processing component since the exact waiter cannot be identified from the gate.
    /// </summary>
    public static class ConsentBadgeOverlay
    {
        private const float BadgeSize = 14f;
        private const float FloatOffset = 4f;

        private static bool isHooked;

        [Conditional("DEBUG")]
        private static void DebugLog(string message)
        {
            Logger.Trace(typeof(ConsentBadgeOverlay).FullName, message);
        }

        /// <summary>
        /// Hooks the canvas post-paint overlay on the active and future canvases.
        /// Idempotent — safe to call on every assembly load.
        /// </summary>
        public static void EnsureInitialized()
        {
            if (isHooked)
            {
                return;
            }

            try
            {
                Instances.CanvasCreated += HookCanvas;

                if (Instances.ActiveCanvas != null)
                {
                    HookCanvas(Instances.ActiveCanvas);
                }

                isHooked = true;
            }
            catch (Exception ex)
            {
                DebugLog($"Error hooking consent badge overlay: {ex.Message}");
            }
        }

        private static void HookCanvas(GH_Canvas canvas)
        {
            if (canvas == null)
            {
                return;
            }

            canvas.CanvasPostPaintOverlay -= OnCanvasPostPaintOverlay;
            canvas.CanvasPostPaintOverlay += OnCanvasPostPaintOverlay;
        }

        private static void OnCanvasPostPaintOverlay(GH_Canvas canvas)
        {
            if (canvas?.Document == null || canvas.Graphics == null
                || (!ExternalSourceAccess.IsAwaitingConsent && !LocalFileAccess.IsAwaitingConsent))
            {
                return;
            }

            foreach (var obj in canvas.Document.Objects)
            {
                if (obj is not StatefulComponentBase stateful ||
                    stateful.CurrentState != ComponentState.Processing)
                {
                    continue;
                }

                var bounds = obj.Attributes?.Bounds ?? RectangleF.Empty;
                if (bounds.IsEmpty)
                {
                    continue;
                }

                var topCenter = canvas.Viewport.ProjectPoint(
                    new PointF(bounds.X + bounds.Width / 2f, bounds.Top));

                var savedTransform = canvas.Graphics.Transform;
                canvas.Graphics.ResetTransform();

                try
                {
                    DrawBadge(canvas.Graphics, topCenter.X - BadgeSize / 2f, topCenter.Y - FloatOffset - BadgeSize);
                }
                finally
                {
                    canvas.Graphics.Transform = savedTransform;
                }
            }
        }

        /// <summary>
        /// Draws a SmartHopper-green upward triangle with a white exclamation mark.
        /// </summary>
        private static void DrawBadge(Graphics g, float x, float y)
        {
            using (var bg = new SolidBrush(Color.FromArgb(32, 152, 72)))
            using (var pen = new Pen(Color.White, 1.5f))
            {
                var points = new[]
                {
                    new PointF(x + BadgeSize / 2f, y + 1f),
                    new PointF(x + BadgeSize - 1f, y + BadgeSize - 1.5f),
                    new PointF(x + 1f, y + BadgeSize - 1.5f),
                };
                g.FillPolygon(bg, points);
                g.DrawPolygon(new Pen(Color.FromArgb(120, Color.White), 1f), points);

                var cx = x + BadgeSize / 2f;
                g.DrawLine(pen, cx, y + BadgeSize * 0.34f, cx, y + BadgeSize * 0.58f);
                float dotRadius = BadgeSize * 0.08f;
                float dotY = y + BadgeSize * 0.70f;
                g.FillEllipse(Brushes.White, cx - dotRadius, dotY - dotRadius, dotRadius * 2, dotRadius * 2);
            }
        }
    }
}
