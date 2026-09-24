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
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using GhJSON.Core.SchemaModels;
using Grasshopper;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;

namespace SmartHopper.Core.Grasshopper.Utils.Canvas
{
    /// <summary>
    /// Paints a non-mutating visual representation of staged changes over the actual Grasshopper canvas.
    /// </summary>
    public static class CanvasChangePreviewOverlay
    {
        private const float FrameMargin = 60f;
        private const float MinFrameZoom = 0.01f;
        private const float MaxFrameZoom = 32f;
        private static readonly Color AddedColor = Color.FromArgb(230, 45, 164, 78);
        private static readonly Color ModifiedColor = Color.FromArgb(230, 191, 135, 0);
        private static readonly Color RemovedColor = Color.FromArgb(230, 207, 34, 46);
        private static readonly Color GroupColor = Color.FromArgb(230, 130, 80, 223);
        private static readonly Color WireColor = Color.FromArgb(230, 9, 105, 218);
        private static bool initialized;
        private static CanvasChangeReviewSession? activeSession;
        private static string? highlightedKey;

        /// <summary>
        /// Hooks Grasshopper canvas paint events. Safe to call multiple times.
        /// </summary>
        public static void EnsureInitialized()
        {
            if (initialized)
            {
                return;
            }

            AttachToCanvas(Instances.ActiveCanvas);
            Instances.CanvasCreated += AttachToCanvas;
            initialized = true;
        }

        /// <summary>
        /// Starts displaying a staged review session.
        /// </summary>
        /// <param name="session">Session to display.</param>
        public static void Begin(CanvasChangeReviewSession session)
        {
            EnsureInitialized();
            End();
            activeSession = session ?? throw new ArgumentNullException(nameof(session));
            activeSession.SelectionChanged += OnSelectionChanged;
            RefreshCanvas();
        }

        /// <summary>
        /// Stops displaying the current staged review session.
        /// </summary>
        public static void End()
        {
            if (activeSession != null)
            {
                activeSession.SelectionChanged -= OnSelectionChanged;
            }

            activeSession = null;
            highlightedKey = null;
            RefreshCanvas();
        }

        /// <summary>
        /// Stops displaying a staged review session only when it is the one currently shown.
        /// </summary>
        /// <param name="session">Session that is ending.</param>
        public static void End(CanvasChangeReviewSession session)
        {
            if (ReferenceEquals(activeSession, session))
            {
                End();
            }
        }

        /// <summary>
        /// Highlights one review item on the canvas.
        /// </summary>
        /// <param name="key">Item key, or <c>null</c> to clear the highlight.</param>
        public static void Highlight(string? key)
        {
            highlightedKey = key;
            RefreshCanvas();
        }

        /// <summary>
        /// Pans and zooms the canvas viewport so the union of every staged change
        /// bounds is visible.
        /// </summary>
        /// <param name="canvas">Canvas whose viewport should be adjusted.</param>
        /// <param name="session">Review session whose items are measured.</param>
        /// <param name="onlyWhenNotFullyVisible">
        /// When <c>true</c>, the view is left untouched if all staged bounds already fit.
        /// </param>
        /// <returns><c>true</c> when the viewport was adjusted.</returns>
        public static bool FrameChanges(GH_Canvas? canvas, CanvasChangeReviewSession session, bool onlyWhenNotFullyVisible = false)
        {
            return FrameBounds(canvas, GetReviewWorldBounds(canvas, session), onlyWhenNotFullyVisible);
        }

        /// <summary>
        /// Pans and zooms the canvas viewport so the bounds of one staged change are visible.
        /// </summary>
        /// <param name="canvas">Canvas whose viewport should be adjusted.</param>
        /// <param name="session">Review session that owns the item.</param>
        /// <param name="item">Change to frame.</param>
        /// <returns><c>true</c> when the viewport was adjusted.</returns>
        public static bool FrameItem(GH_Canvas? canvas, CanvasChangeReviewSession session, CanvasChangeReviewItem item)
        {
            return item != null && FrameBounds(canvas, GetItemWorldBounds(canvas, session, item), false);
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

        private static void OnSelectionChanged(object? sender, EventArgs e)
        {
            RefreshCanvas();
        }

        private static void RefreshCanvas()
        {
            try
            {
                Instances.ActiveCanvas?.Refresh();
            }
            catch (InvalidOperationException ex)
            {
                Debug.WriteLine($"[CanvasChangePreviewOverlay] Canvas refresh skipped: {ex.Message}");
            }
        }

        private static void OnCanvasPostPaintOverlay(GH_Canvas canvas)
        {
            var session = activeSession;
            if (session == null || canvas?.Document == null || canvas.Graphics == null)
            {
                return;
            }

            var graphics = canvas.Graphics;
            var oldTransform = graphics.Transform;
            graphics.ResetTransform();
            graphics.SmoothingMode = SmoothingMode.AntiAlias;

            try
            {
                foreach (var item in session.Items.OrderBy(GetPaintOrder))
                {
                    DrawItem(graphics, canvas, session, item);
                }
            }
            finally
            {
                graphics.Transform = oldTransform;
                oldTransform.Dispose();
            }
        }

        private static int GetPaintOrder(CanvasChangeReviewItem item)
        {
            return item.Kind switch
            {
                CanvasChangeKind.GroupAdded or CanvasChangeKind.GroupModified or CanvasChangeKind.GroupRemoved => 0,
                CanvasChangeKind.ConnectionAdded or CanvasChangeKind.ConnectionRemoved => 1,
                _ => 2,
            };
        }

        private static void DrawItem(
            Graphics graphics,
            GH_Canvas canvas,
            CanvasChangeReviewSession session,
            CanvasChangeReviewItem item)
        {
            var effective = session.IsEffectivelyAccepted(item);
            var highlighted = string.Equals(highlightedKey, item.Key, StringComparison.Ordinal);
            var alpha = effective ? 230 : 55;
            var color = Color.FromArgb(alpha, GetColor(item.Kind));
            var width = highlighted ? 4f : 2.2f;

            switch (item.Kind)
            {
                case CanvasChangeKind.ComponentAdded:
                    DrawAddedComponent(graphics, canvas, session, item, color, width);
                    break;
                case CanvasChangeKind.ComponentModified:
                case CanvasChangeKind.ComponentRemoved:
                    DrawExistingComponent(graphics, canvas, session, item, color, width);
                    break;
                case CanvasChangeKind.ConnectionAdded:
                case CanvasChangeKind.ConnectionRemoved:
                    DrawConnection(graphics, canvas, session, item, color, width);
                    break;
                case CanvasChangeKind.GroupAdded:
                case CanvasChangeKind.GroupModified:
                case CanvasChangeKind.GroupRemoved:
                    DrawGroup(graphics, canvas, session, item, color, width);
                    break;
            }
        }

        private static void DrawAddedComponent(
            Graphics graphics,
            GH_Canvas canvas,
            CanvasChangeReviewSession session,
            CanvasChangeReviewItem item,
            Color color,
            float width)
        {
            var component = FindComponent(session, item.ComponentId);
            if (component?.Pivot == null)
            {
                return;
            }

            var bounds = ProjectProposedBounds(canvas, session, component);
            using var fill = new SolidBrush(Color.FromArgb(Math.Min((int)color.A, 45), color));
            using var pen = CreatePen(color, width);
            graphics.FillRoundedRectangle(fill, bounds, 6f);
            graphics.DrawRoundedRectangle(pen, bounds, 6f);
            DrawLabel(graphics, bounds, component.NickName ?? component.Name ?? "Component", color);
        }

        private static void DrawExistingComponent(
            Graphics graphics,
            GH_Canvas canvas,
            CanvasChangeReviewSession session,
            CanvasChangeReviewItem item,
            Color color,
            float width)
        {
            if (!item.ExistingInstanceGuid.HasValue)
            {
                return;
            }

            var obj = canvas.Document.FindObject(item.ExistingInstanceGuid.Value, false);
            if (obj?.Attributes == null)
            {
                return;
            }

            var bounds = ProjectBounds(canvas.Viewport, obj.Attributes.Bounds);
            bounds.Inflate(4f, 4f);
            using var fill = new SolidBrush(Color.FromArgb(Math.Min((int)color.A, 35), color));
            using var pen = CreatePen(color, width);
            graphics.FillRoundedRectangle(fill, bounds, 6f);
            graphics.DrawRoundedRectangle(pen, bounds, 6f);

            var proposed = FindComponent(session, item.ComponentId);
            if (item.Kind == CanvasChangeKind.ComponentModified && proposed?.Pivot != null)
            {
                var targetBounds = ProjectProposedBounds(canvas, session, proposed);
                var currentCenter = new PointF(bounds.Left + (bounds.Width / 2f), bounds.Top + (bounds.Height / 2f));
                var targetCenter = new PointF(targetBounds.Left + (targetBounds.Width / 2f), targetBounds.Top + (targetBounds.Height / 2f));
                if (Math.Abs(currentCenter.X - targetCenter.X) > 4f || Math.Abs(currentCenter.Y - targetCenter.Y) > 4f)
                {
                    graphics.DrawRoundedRectangle(pen, targetBounds, 6f);
                    graphics.DrawLine(pen, currentCenter, targetCenter);
                    DrawLabel(graphics, targetBounds, "Proposed position", color);
                }
            }

            if (item.Kind == CanvasChangeKind.ComponentRemoved)
            {
                graphics.DrawLine(pen, bounds.Left, bounds.Top, bounds.Right, bounds.Bottom);
                graphics.DrawLine(pen, bounds.Right, bounds.Top, bounds.Left, bounds.Bottom);
            }

            DrawLabel(graphics, bounds, item.Kind == CanvasChangeKind.ComponentModified ? "Modified" : "Removed", color);
        }

        private static void DrawConnection(
            Graphics graphics,
            GH_Canvas canvas,
            CanvasChangeReviewSession session,
            CanvasChangeReviewItem item,
            Color color,
            float width)
        {
            if (!item.ConnectionIndex.HasValue || session.ProposedDocument.Connections == null ||
                item.ConnectionIndex.Value >= session.ProposedDocument.Connections.Count)
            {
                return;
            }

            var connection = session.ProposedDocument.Connections[item.ConnectionIndex.Value];
            if (!TryGetAnchor(canvas, session, connection.From.Id, true, out var from) ||
                !TryGetAnchor(canvas, session, connection.To.Id, false, out var to))
            {
                return;
            }

            var offset = Math.Max(30f, Math.Abs(to.X - from.X) * 0.45f);
            using var path = new GraphicsPath();
            path.AddBezier(from, new PointF(from.X + offset, from.Y), new PointF(to.X - offset, to.Y), to);
            using var pen = CreatePen(color, width);
            graphics.DrawPath(pen, path);
        }

        private static void DrawGroup(
            Graphics graphics,
            GH_Canvas canvas,
            CanvasChangeReviewSession session,
            CanvasChangeReviewItem item,
            Color color,
            float width)
        {
            if (item.ExistingInstanceGuid.HasValue &&
                canvas.Document.FindObject(item.ExistingInstanceGuid.Value, false)?.Attributes is IGH_Attributes existingAttributes)
            {
                var existingBounds = ProjectBounds(canvas.Viewport, existingAttributes.Bounds);
                existingBounds.Inflate(4f, 4f);
                using var existingPen = CreatePen(color, width);
                graphics.DrawRoundedRectangle(existingPen, existingBounds, 8f);
                DrawLabel(graphics, existingBounds, item.Title, color);
                return;
            }

            if (!item.GroupIndex.HasValue || session.ProposedDocument.Groups == null ||
                item.GroupIndex.Value >= session.ProposedDocument.Groups.Count)
            {
                return;
            }

            var group = session.ProposedDocument.Groups[item.GroupIndex.Value];
            RectangleF? groupBounds = null;
            foreach (var memberId in group.Members)
            {
                var component = session.ProposedDocument.Components.FirstOrDefault(candidate => candidate.Id == memberId);
                if (component == null)
                {
                    continue;
                }

                var memberWorldBounds = GetComponentWorldBounds(canvas, session, component);
                if (!memberWorldBounds.HasValue)
                {
                    continue;
                }

                var bounds = ProjectBounds(canvas.Viewport, memberWorldBounds.Value);
                groupBounds = groupBounds.HasValue ? RectangleF.Union(groupBounds.Value, bounds) : bounds;
            }

            if (!groupBounds.HasValue)
            {
                return;
            }

            var result = groupBounds.Value;
            result.Inflate(16f, 18f);
            using var pen = CreatePen(color, width);
            graphics.DrawRoundedRectangle(pen, result, 8f);
            DrawLabel(graphics, result, group.Name ?? "Group", color);
        }

        private static bool TryGetAnchor(
            GH_Canvas canvas,
            CanvasChangeReviewSession session,
            int componentId,
            bool output,
            out PointF anchor)
        {
            if (!TryGetWorldAnchor(canvas, session, componentId, output, out anchor))
            {
                return false;
            }

            canvas.Viewport.Project(ref anchor);
            return true;
        }

        private static bool TryGetWorldAnchor(
            GH_Canvas canvas,
            CanvasChangeReviewSession session,
            int componentId,
            bool output,
            out PointF anchor)
        {
            anchor = PointF.Empty;
            var component = session.ProposedDocument.Components.FirstOrDefault(candidate => candidate.Id == componentId);
            if (component == null)
            {
                return false;
            }

            var bounds = GetComponentWorldBounds(canvas, session, component);
            if (!bounds.HasValue)
            {
                return false;
            }

            anchor = new PointF(
                output ? bounds.Value.Right : bounds.Value.Left,
                bounds.Value.Top + (bounds.Value.Height / 2f));
            return true;
        }

        private static GhJsonComponent? FindComponent(CanvasChangeReviewSession session, int? componentId)
        {
            return componentId.HasValue
                ? session.ProposedDocument.Components.FirstOrDefault(component => component.Id == componentId.Value)
                : null;
        }

        /// <summary>
        /// Computes the union of world-space bounds for every item in the session,
        /// regardless of acceptance state.
        /// </summary>
        private static RectangleF? GetReviewWorldBounds(GH_Canvas? canvas, CanvasChangeReviewSession session)
        {
            if (canvas?.Document == null || session == null)
            {
                return null;
            }

            RectangleF? union = null;
            foreach (var item in session.Items)
            {
                var itemBounds = GetItemWorldBounds(canvas, session, item);
                if (itemBounds.HasValue)
                {
                    union = union.HasValue ? RectangleF.Union(union.Value, itemBounds.Value) : itemBounds.Value;
                }
            }

            return union;
        }

        private static RectangleF? GetItemWorldBounds(
            GH_Canvas? canvas,
            CanvasChangeReviewSession? session,
            CanvasChangeReviewItem? item)
        {
            if (canvas?.Document == null || session == null || item == null)
            {
                return null;
            }

            switch (item.Kind)
            {
                case CanvasChangeKind.ComponentAdded:
                {
                    var component = FindComponent(session, item.ComponentId);
                    return component == null ? null : GetComponentWorldBounds(canvas, session, component);
                }

                case CanvasChangeKind.ComponentModified:
                case CanvasChangeKind.ComponentRemoved:
                {
                    RectangleF? result = null;
                    if (item.ExistingInstanceGuid.HasValue &&
                        canvas.Document.FindObject(item.ExistingInstanceGuid.Value, false)?.Attributes is IGH_Attributes attributes)
                    {
                        result = attributes.Bounds;
                    }

                    if (item.Kind == CanvasChangeKind.ComponentModified &&
                        FindComponent(session, item.ComponentId) is GhJsonComponent proposed &&
                        GetComponentWorldBounds(canvas, session, proposed) is RectangleF proposedBounds)
                    {
                        result = result.HasValue ? RectangleF.Union(result.Value, proposedBounds) : proposedBounds;
                    }

                    return result;
                }

                case CanvasChangeKind.ConnectionAdded:
                case CanvasChangeKind.ConnectionRemoved:
                {
                    if (!item.ConnectionIndex.HasValue || session.ProposedDocument.Connections == null ||
                        item.ConnectionIndex.Value >= session.ProposedDocument.Connections.Count)
                    {
                        return null;
                    }

                    var connection = session.ProposedDocument.Connections[item.ConnectionIndex.Value];
                    if (!TryGetWorldAnchor(canvas, session, connection.From.Id, true, out var from) ||
                        !TryGetWorldAnchor(canvas, session, connection.To.Id, false, out var to))
                    {
                        return null;
                    }

                    var result = RectangleF.FromLTRB(
                        Math.Min(from.X, to.X),
                        Math.Min(from.Y, to.Y),
                        Math.Max(from.X, to.X),
                        Math.Max(from.Y, to.Y));
                    result.Inflate(Math.Max(60f, result.Width * 0.5f), 30f);
                    return result;
                }

                case CanvasChangeKind.GroupAdded:
                case CanvasChangeKind.GroupModified:
                case CanvasChangeKind.GroupRemoved:
                {
                    if (item.ExistingInstanceGuid.HasValue &&
                        canvas.Document.FindObject(item.ExistingInstanceGuid.Value, false)?.Attributes is IGH_Attributes existing)
                    {
                        return existing.Bounds;
                    }

                    if (!item.GroupIndex.HasValue || session.ProposedDocument.Groups == null ||
                        item.GroupIndex.Value >= session.ProposedDocument.Groups.Count)
                    {
                        return null;
                    }

                    var group = session.ProposedDocument.Groups[item.GroupIndex.Value];
                    RectangleF? result = null;
                    foreach (var memberId in group.Members)
                    {
                        var component = session.ProposedDocument.Components.FirstOrDefault(candidate => candidate.Id == memberId);
                        if (component == null)
                        {
                            continue;
                        }

                        var memberBounds = GetComponentWorldBounds(canvas, session, component);
                        if (memberBounds.HasValue)
                        {
                            result = result.HasValue ? RectangleF.Union(result.Value, memberBounds.Value) : memberBounds.Value;
                        }
                    }

                    if (result.HasValue)
                    {
                        var padded = result.Value;
                        padded.Inflate(16f, 18f);
                        result = padded;
                    }

                    return result;
                }

                default:
                    return null;
            }
        }

        /// <summary>
        /// Resolves a proposed component's world-space bounds: its proposed bounds when a
        /// pivot is available, otherwise the live object's current bounds.
        /// </summary>
        private static RectangleF? GetComponentWorldBounds(
            GH_Canvas canvas,
            CanvasChangeReviewSession session,
            GhJsonComponent component)
        {
            if (component.Pivot != null)
            {
                return GetProposedWorldBounds(canvas, session, component);
            }

            if (component.InstanceGuid.HasValue &&
                canvas.Document.FindObject(component.InstanceGuid.Value, false)?.Attributes is IGH_Attributes attributes)
            {
                return attributes.Bounds;
            }

            return null;
        }

        private static bool FrameBounds(GH_Canvas? canvas, RectangleF? bounds, bool onlyWhenNotFullyVisible)
        {
            var viewport = canvas?.Viewport;
            if (viewport == null || !bounds.HasValue)
            {
                return false;
            }

            var padded = bounds.Value;
            padded.Inflate(FrameMargin, FrameMargin);
            if (padded.Width <= 0f || padded.Height <= 0f)
            {
                return false;
            }

            if (onlyWhenNotFullyVisible && IsFullyVisible(viewport, padded))
            {
                return false;
            }

            var zoom = Math.Min(viewport.Width / padded.Width, viewport.Height / padded.Height);
            if (float.IsNaN(zoom) || float.IsInfinity(zoom) || zoom <= 0f)
            {
                return false;
            }

            viewport.Zoom = Math.Max(MinFrameZoom, Math.Min(MaxFrameZoom, zoom));
            viewport.MidPoint = new PointF(
                padded.Left + (padded.Width / 2f),
                padded.Top + (padded.Height / 2f));
            canvas!.Refresh();
            return true;
        }

        private static bool IsFullyVisible(GH_Viewport viewport, RectangleF bounds)
        {
            var region = viewport.VisibleRegion;
            return region.Left <= bounds.Left && region.Top <= bounds.Top &&
                   region.Right >= bounds.Right && region.Bottom >= bounds.Bottom;
        }

        /// <summary>
        /// Projects a proposed component's bounds to screen space. Resolution order:
        /// bounds measured at review time (already world-space), then the live object's
        /// bounds translated so its pivot-relative offset lands on the proposed pivot,
        /// then a fixed-size estimate centered on the pivot. Grasshopper pivots are
        /// bounds centers, not top-left corners.
        /// </summary>
        private static RectangleF ProjectProposedBounds(
            GH_Canvas canvas,
            CanvasChangeReviewSession session,
            GhJsonComponent component)
        {
            return ProjectBounds(canvas.Viewport, GetProposedWorldBounds(canvas, session, component));
        }

        private static RectangleF GetProposedWorldBounds(
            GH_Canvas canvas,
            CanvasChangeReviewSession session,
            GhJsonComponent component)
        {
            if (component.Id.HasValue &&
                session.TryGetProposedComponentBounds(component.Id.Value, out var measuredBounds))
            {
                return measuredBounds;
            }

            var pivot = new PointF((float)component.Pivot!.X, (float)component.Pivot.Y);

            var liveAttributes = component.InstanceGuid.HasValue
                ? canvas.Document.FindObject(component.InstanceGuid.Value, false)?.Attributes
                : null;
            if (liveAttributes?.Bounds is RectangleF liveBounds && liveBounds.Width > 0f && liveBounds.Height > 0f)
            {
                var livePivot = liveAttributes.Pivot;
                return new RectangleF(
                    pivot.X + (liveBounds.Left - livePivot.X),
                    pivot.Y + (liveBounds.Top - livePivot.Y),
                    liveBounds.Width,
                    liveBounds.Height);
            }

            return new RectangleF(pivot.X - 70f, pivot.Y - 26f, 140f, 52f);
        }

        private static RectangleF ProjectBounds(GH_Viewport viewport, RectangleF bounds)
        {
            var topLeft = new PointF(bounds.Left, bounds.Top);
            var bottomRight = new PointF(bounds.Right, bounds.Bottom);
            viewport.Project(ref topLeft);
            viewport.Project(ref bottomRight);
            return RectangleF.FromLTRB(topLeft.X, topLeft.Y, bottomRight.X, bottomRight.Y);
        }

        private static Pen CreatePen(Color color, float width)
        {
            return new Pen(color, width)
            {
                DashStyle = DashStyle.Dash,
            };
        }

        private static Color GetColor(CanvasChangeKind kind)
        {
            return kind switch
            {
                CanvasChangeKind.ComponentAdded => AddedColor,
                CanvasChangeKind.ComponentModified => ModifiedColor,
                CanvasChangeKind.ComponentRemoved => RemovedColor,
                CanvasChangeKind.ConnectionAdded => WireColor,
                CanvasChangeKind.ConnectionRemoved => RemovedColor,
                _ => GroupColor,
            };
        }

        private static void DrawLabel(Graphics graphics, RectangleF bounds, string text, Color color)
        {
            using var font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            var size = graphics.MeasureString(text, font);
            var labelBounds = new RectangleF(bounds.Left, bounds.Top - size.Height - 3f, size.Width + 10f, size.Height + 2f);
            using var brush = new SolidBrush(color);
            using var textBrush = new SolidBrush(Color.White);
            graphics.FillRoundedRectangle(brush, labelBounds, 5f);
            graphics.DrawString(text, font, textBrush, labelBounds.Left + 5f, labelBounds.Top + 1f);
        }

        private static void FillRoundedRectangle(this Graphics graphics, Brush brush, RectangleF bounds, float radius)
        {
            using var path = CreateRoundedRectangle(bounds, radius);
            graphics.FillPath(brush, path);
        }

        private static void DrawRoundedRectangle(this Graphics graphics, Pen pen, RectangleF bounds, float radius)
        {
            using var path = CreateRoundedRectangle(bounds, radius);
            graphics.DrawPath(pen, path);
        }

        private static GraphicsPath CreateRoundedRectangle(RectangleF bounds, float radius)
        {
            var diameter = radius * 2f;
            var path = new GraphicsPath();
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180f, 90f);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270f, 90f);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0f, 90f);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90f, 90f);
            path.CloseFigure();
            return path;
        }
    }
}
