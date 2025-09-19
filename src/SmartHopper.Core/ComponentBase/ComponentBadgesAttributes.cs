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
 * ComponentBadgesAttributes: Custom Grasshopper component attributes that render
 * AI model status badges as floating circles above the component.
 *
 * UI policy:
 * - Show at most ONE primary badge for clarity, with priority: Replaced > Invalid > Verified.
 * - Deprecated may co-exist with any primary badge (or be shown alone if no primary).
 *
 * Purpose: Extend component UI to show model state directly on the component.
 * - Uses last used model from metrics when available; otherwise falls back to the
 *   configured (input/default) model.
 * - Queries ModelManager for AIModelCapabilities to determine flags.
 * - Designed to be extensible for future badges (e.g., automatic model replacement).
 */

using System;
using System.Collections.Generic;
using System.Drawing;
using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using Timer = System.Timers.Timer;

namespace SmartHopper.Core.ComponentBase
{
    /// <summary>
    /// Custom attributes for AI components that display model badges (verified/deprecated)
    /// as floating circles centered above the component (not overlapping the body).
    /// </summary>
    public class ComponentBadgesAttributes : AIProviderComponentAttributes
    {
        private readonly AIProviderComponentBase owner;

        // Layout constants
        private const int BADGE_SIZE = 16;               // Size of badges
        private const float MIN_ZOOM_THRESHOLD = 0.5f;   // Minimum zoom to render badges
        private const int BADGE_GAP = 6;                 // Gap between badges
        private const int FLOAT_OFFSET = -10;              // Vertical offset above component

        // Hover/interaction state for inline labels (generalized)
        private readonly List<RectangleF> badgeRects = new List<RectangleF>();
        private readonly List<string> badgeLabels = new List<string>();
        private int hoverBadgeIndex = -1;

        // Timer-based auto-hide for inline badge labels (disappears after 5s even if still hovered)
        // Purpose: avoid sticky labels when the cursor remains stationary over a badge.
        private Timer? badgeLabelTimer;
        private bool badgeLabelAutoHidden = false;

        /// <summary>
        /// Creates a new instance of <see cref="ComponentBadgesAttributes"/>.
        /// </summary>
        /// <param name="owner">The AI component that owns these attributes.</param>
        public ComponentBadgesAttributes(AIProviderComponentBase owner)
            : base(owner)
        {
            this.owner = owner;
        }

        /// <summary>
        /// Keep default component bounds; badges are drawn floating above.
        /// </summary>
        protected override void Layout()
        {
            base.Layout();

            // Ensure the attribute bounds include the floating badges region above the component
            // so that Grasshopper dispatches mouse events when hovering the badges.
            var bounds = this.Bounds;
            float extendTop = FLOAT_OFFSET + BADGE_SIZE;
            bounds.Y -= extendTop;
            bounds.Height += extendTop;
            this.Bounds = bounds;

            // Reset hover state when layout changes
            this.badgeRects.Clear();
            this.badgeLabels.Clear();
            this.hoverBadgeIndex = -1;
        }

        /// <summary>
        /// Renders model state badges as floating circles above the component.
        /// Uses cached flags from the owner to avoid heavy lookups during panning.
        /// </summary>
        protected override void Render(GH_Canvas canvas, Graphics graphics, GH_CanvasChannel channel)
        {
            // First render base visuals (including provider icon at the bottom strip)
            base.Render(canvas, graphics, channel);

            if (channel != GH_CanvasChannel.Objects)
            {
                return;
            }

            if (canvas.Viewport.Zoom < MIN_ZOOM_THRESHOLD)
            {
                return;
            }

            if (this.owner is not AIStatefulAsyncComponentBase stateful)
            {
                return;
            }

            if (!stateful.TryGetCachedBadgeFlags(out bool showVerified, out bool showDeprecated, out bool showInvalid, out bool showReplaced))
            {
                return;
            }

            // Collect badges using single-primary policy for built-ins.
            // Priority: Replaced > Invalid > Verified. Deprecated can co-exist.
            // In addition, render any custom badges contributed by derived attributes
            // via GetAdditionalBadges().
            var items = new List<(System.Action<Graphics, float, float> draw, string label)>();

            // Determine primary badge by priority
            (System.Action<Graphics, float, float> draw, string label)? primary = null;
            if (showReplaced)
            {
                primary = (DrawReplacedBadge, "Model replaced with a capable one");
            }
            else if (showInvalid)
            {
                primary = (DrawInvalidBadge, "Invalid model");
            }
            else if (showVerified)
            {
                primary = (DrawVerifiedBadge, "Verified model");
            }

            if (primary.HasValue)
            {
                items.Add(primary.Value);
            }

            // Deprecated can be shown alongside any primary (or alone if no primary)
            if (showDeprecated)
            {
                items.Add((DrawDeprecatedBadge, "Deprecated model"));
            }

            // Append any additional custom badges provided by derived classes.
            // The single-primary policy applies only to built-in badges above; custom
            // badges are additive and will always render if provided.
            foreach (var extra in GetAdditionalBadges())
            {
                items.Add(extra);
            }

            if (items.Count == 0)
            {
                this.badgeRects.Clear();
                this.badgeLabels.Clear();
                this.hoverBadgeIndex = -1;
                return;
            }

            var bounds = this.Bounds;

            int count = items.Count;
            float totalWidth = count * BADGE_SIZE + (count - 1) * BADGE_GAP;
            float originX = bounds.Left + (bounds.Width - totalWidth) / 2f;
            float y = bounds.Top - FLOAT_OFFSET - BADGE_SIZE;

            // Reset rects before drawing
            this.badgeRects.Clear();
            this.badgeLabels.Clear();

            float x = originX;
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                item.draw(graphics, x, y);
                this.badgeRects.Add(new RectangleF(x, y, BADGE_SIZE, BADGE_SIZE));
                this.badgeLabels.Add(item.label);
                x += BADGE_SIZE + BADGE_GAP;
            }

            // Draw inline labels last so they appear on top
            if (this.hoverBadgeIndex >= 0 && this.hoverBadgeIndex < this.badgeRects.Count && !this.badgeLabelAutoHidden)
            {
                var rect = this.badgeRects[this.hoverBadgeIndex];
                var text = this.badgeLabels[this.hoverBadgeIndex];
                InlineLabelRenderer.DrawInlineLabel(graphics, rect, text);
            }
        }

        // Model resolution moved to owner cache to avoid work during Render.

        /// <summary>
        /// Draw a simple green check-circle for Verified.
        /// </summary>
        private static void DrawVerifiedBadge(Graphics g, float x, float y)
        {
            using (var bg = new SolidBrush(Color.FromArgb(32, 152, 72))) // green
            using (var pen = new Pen(Color.White, 1.5f))
            {
                var rect = new RectangleF(x, y, BADGE_SIZE, BADGE_SIZE);
                g.FillEllipse(bg, rect);

                // check mark
                var p1 = new PointF(x + BADGE_SIZE * 0.28f, y + BADGE_SIZE * 0.55f);
                var p2 = new PointF(x + BADGE_SIZE * 0.45f, y + BADGE_SIZE * 0.72f);
                var p3 = new PointF(x + BADGE_SIZE * 0.75f, y + BADGE_SIZE * 0.30f);
                g.DrawLines(pen, new[] { p1, p2, p3 });
            }
        }

        /// <summary>
        /// Draw a simple orange warning triangle for Deprecated.
        /// </summary>
        private static void DrawDeprecatedBadge(Graphics g, float x, float y)
        {
            using (var bg = new SolidBrush(Color.FromArgb(230, 126, 34))) // orange
            using (var pen = new Pen(Color.White, 1.5f))
            {
                var cx = x + BADGE_SIZE / 2f;
                var cy = y + BADGE_SIZE / 2f;
                var r = BADGE_SIZE * 0.45f;

                // Triangle points
                var p1 = new PointF(cx, cy - r);
                var p2 = new PointF(cx - r * 0.866f, cy + r * 0.5f);
                var p3 = new PointF(cx + r * 0.866f, cy + r * 0.5f);

                var path = new System.Drawing.Drawing2D.GraphicsPath();
                path.AddPolygon(new[] { p1, p2, p3 });
                g.FillPath(bg, path);

                // Exclamation mark
                var lineTop = new PointF(cx, cy - r * 0.2f);
                var lineBottom = new PointF(cx, cy + r * 0.3f);
                g.DrawLine(pen, lineTop, lineBottom);
                g.DrawEllipse(pen, cx - 1.5f, cy + r * 0.4f, 3f, 3f);
            }
        }

        /// <summary>
        /// Draw a red circle with a white cross for Invalid/Incompatible model.
        /// </summary>
        private static void DrawInvalidBadge(Graphics g, float x, float y)
        {
            using (var bg = new SolidBrush(Color.FromArgb(192, 57, 43))) // red
            using (var pen = new Pen(Color.White, 1.5f))
            {
                var rect = new RectangleF(x, y, BADGE_SIZE, BADGE_SIZE);
                g.FillEllipse(bg, rect);

                // cross
                float inset = BADGE_SIZE * 0.28f;
                var p1 = new PointF(x + inset, y + inset);
                var p2 = new PointF(x + BADGE_SIZE - inset, y + BADGE_SIZE - inset);
                var p3 = new PointF(x + BADGE_SIZE - inset, y + inset);
                var p4 = new PointF(x + inset, y + BADGE_SIZE - inset);
                g.DrawLine(pen, p1, p2);
                g.DrawLine(pen, p3, p4);
            }
        }

        /// <summary>
        /// Draw a blue circle with a white refresh arrow for Replaced/Fallback model.
        /// </summary>
        private static void DrawReplacedBadge(Graphics g, float x, float y)
        {
            using (var bg = new SolidBrush(Color.FromArgb(52, 152, 219))) // blue
            using (var pen = new Pen(Color.White, 1.5f))
            {
                var rect = new RectangleF(x, y, BADGE_SIZE, BADGE_SIZE);
                g.FillEllipse(bg, rect);

                // refresh arrow (arc + arrow head)
                var cx = x + BADGE_SIZE / 2f;
                var cy = y + BADGE_SIZE / 2f;
                float r = BADGE_SIZE * 0.3f;

                using (var path = new System.Drawing.Drawing2D.GraphicsPath())
                {
                    var startAngle = -40f;
                    var sweep = 260f;
                    path.AddArc(cx - r, cy - r, 2 * r, 2 * r, startAngle, sweep);
                    g.DrawPath(pen, path);
                }

                // arrow head at end of arc
                double endRad = (Math.PI / 180.0) * ( -40f + 260f );

                // Calculate direction vector
                var dir = new PointF(
                    (float)(-Math.Sin(endRad)),
                    (float)(Math.Cos(endRad)));
                
                // Arrow head dimensions
                float ah = BADGE_SIZE * 0.25f;
                float arrowWidth = ah * 0.6f;
                
                // Calculate the base point (where arrow connects to the arc)
                var basePt = new PointF(
                    (float)(cx + r * Math.Cos(endRad - 0.1f)),
                    (float)(cy + r * Math.Sin(endRad - 0.1f)));
                
                // Calculate the tip point (moved forward along the direction vector)
                var tipPt = new PointF(
                    basePt.X + dir.X * ah,
                    basePt.Y + dir.Y * ah);
                
                // Calculate the two base points for the arrow head
                var a1 = tipPt;
                var a2 = new PointF(
                    basePt.X + dir.Y * arrowWidth,
                    basePt.Y - dir.X * arrowWidth);
                var a3 = new PointF(
                    basePt.X - dir.Y * arrowWidth,
                    basePt.Y + dir.X * arrowWidth);

                using (var pathHead = new System.Drawing.Drawing2D.GraphicsPath())
                {
                    pathHead.AddPolygon(new[] { a1, a2, a3 });
                    g.FillPath(Brushes.White, pathHead);
                }
            }
        }

        /// <summary>
        /// Extension point to allow derived attributes to contribute additional badges.
        /// Each badge provides a draw function and the hover label text.
        /// </summary>
        /// <returns>Sequence of additional badge descriptors.</returns>
        protected virtual IEnumerable<(System.Action<Graphics, float, float> draw, string label)> GetAdditionalBadges()
        {
            yield break;
        }

        /// <summary>
        /// Track mouse hover over badges to trigger inline label rendering.
        /// </summary>
        public override Grasshopper.GUI.Canvas.GH_ObjectResponse RespondToMouseMove(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            int prevIndex = this.hoverBadgeIndex;

            if (sender?.Viewport.Zoom < MIN_ZOOM_THRESHOLD)
            {
                this.hoverBadgeIndex = -1;
            }
            else
            {
                var pt = e.CanvasLocation;
                int newIndex = -1;
                for (int i = 0; i < this.badgeRects.Count; i++)
                {
                    if (this.badgeRects[i].Contains(pt))
                    {
                        newIndex = i;
                        break;
                    }
                }

                this.hoverBadgeIndex = newIndex;
            }

            if (prevIndex != this.hoverBadgeIndex)
            {
                // Start/stop 5s auto-hide timer based on hover transitions
                if (this.hoverBadgeIndex >= 0)
                {
                    this.badgeLabelAutoHidden = false;
                    StartBadgeLabelTimer();
                }
                else
                {
                    StopBadgeLabelTimer();
                    this.badgeLabelAutoHidden = false; // reset for next hover
                }

                this.owner.OnDisplayExpired(false);
            }

            return base.RespondToMouseMove(sender, e);
        }

        /// <summary>
        /// Starts a one-shot 5s timer to auto-hide the inline badge label and request a repaint.
        /// </summary>
        private void StartBadgeLabelTimer()
        {
            StopBadgeLabelTimer();
            this.badgeLabelTimer = new Timer(5000) { AutoReset = false };
            this.badgeLabelTimer.Elapsed += (_, __) =>
            {
                this.badgeLabelAutoHidden = true;
                try { this.owner?.OnDisplayExpired(false); } catch { /* ignore */ }
                StopBadgeLabelTimer();
            };
            this.badgeLabelTimer.Start();
        }

        /// <summary>
        /// Stops and disposes the badge label timer if active.
        /// </summary>
        private void StopBadgeLabelTimer()
        {
            if (this.badgeLabelTimer != null)
            {
                try { this.badgeLabelTimer.Stop(); } catch { /* ignore */ }
                try { this.badgeLabelTimer.Dispose(); } catch { /* ignore */ }
                this.badgeLabelTimer = null;
            }
        }
    }
}
