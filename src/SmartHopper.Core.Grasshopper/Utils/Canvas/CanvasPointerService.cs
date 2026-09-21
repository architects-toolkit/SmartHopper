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
using System.Threading;
using System.Threading.Tasks;
using Grasshopper;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using Rhino;
using SmartHopper.Infrastructure.Interaction;

namespace SmartHopper.Core.Grasshopper.Utils.Canvas
{
    /// <summary>
    /// Outcome of one <see cref="CanvasPointerService.ShowAsync"/> call.
    /// </summary>
    public sealed class CanvasPointerShowResult
    {
        /// <summary>Gets a value indicating whether the pointer was displayed.</summary>
        public bool Success => this.Error == null;

        /// <summary>Gets or sets the failure reason, or null on success.</summary>
        public string? Error { get; set; }

        /// <summary>Gets the instance GUIDs that were resolved and framed.</summary>
        public List<Guid> FramedGuids { get; } = new List<Guid>();

        /// <summary>Gets the requested GUIDs that could not be found in the document.</summary>
        public List<Guid> MissingGuids { get; } = new List<Guid>();
    }

    /// <summary>
    /// Displays a transient, non-mutating border highlight on the Grasshopper canvas around
    /// one or more document objects or an explicit world-space region, after panning and
    /// zooming the viewport to frame the target. Requests are kept in a bounded registry so
    /// chat surfaces can replay the same pan/zoom/highlight later via <see cref="CanvasPointerBridge"/>.
    /// </summary>
    public static class CanvasPointerService
    {
        /// <summary>Fixed highlight duration per request and replay.</summary>
        public const int DurationSeconds = 8;

        private const int MaxRegistryEntries = 32;
        private const float FrameMargin = 60f;
        private const float MinZoom = 0.01f;
        private const float MaxZoom = 32f;
        private const float HighlightInflate = 8f;
        private const float BorderWidth = 3f;
        private const int RefreshIntervalMs = 250;
        private static readonly Color HighlightColor = Color.FromArgb(230, 9, 105, 218);

        private static readonly object Sync = new object();
        private static readonly Dictionary<string, CanvasPointerRequest> Registry = new Dictionary<string, CanvasPointerRequest>(StringComparer.Ordinal);
        private static readonly Queue<string> RegistryOrder = new Queue<string>();

        private static bool initialized;
        private static CanvasPointerRequest? active;
        private static DateTime expiresUtc;
        private static DateTime shownUtc;
        private static System.Threading.Timer? pulseTimer;

        /// <summary>
        /// Hooks Grasshopper canvas paint events and registers the replay handler on
        /// <see cref="CanvasPointerBridge"/>. Safe to call multiple times.
        /// </summary>
        public static void EnsureInitialized()
        {
            if (initialized)
            {
                return;
            }

            AttachToCanvas(Instances.ActiveCanvas);
            Instances.CanvasCreated += AttachToCanvas;
            CanvasPointerBridge.ReplayHandler = ReplayAsync;
            initialized = true;
        }

        /// <summary>
        /// Stores a request so it can be replayed later by pointer id. The registry keeps
        /// at most the last <see cref="MaxRegistryEntries"/> requests.
        /// </summary>
        /// <param name="request">The request to remember.</param>
        public static void Register(CanvasPointerRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Id))
            {
                return;
            }

            lock (Sync)
            {
                if (!Registry.ContainsKey(request.Id))
                {
                    RegistryOrder.Enqueue(request.Id);
                }

                Registry[request.Id] = request;
                while (RegistryOrder.Count > MaxRegistryEntries)
                {
                    var oldest = RegistryOrder.Dequeue();
                    Registry.Remove(oldest);
                }
            }
        }

        /// <summary>
        /// Looks up a previously registered request by pointer id.
        /// </summary>
        /// <param name="pointerId">The pointer identifier.</param>
        /// <param name="request">The stored request when found.</param>
        /// <returns>True when the request exists.</returns>
        public static bool TryGet(string pointerId, out CanvasPointerRequest? request)
        {
            lock (Sync)
            {
                return Registry.TryGetValue(pointerId ?? string.Empty, out request);
            }
        }

        /// <summary>
        /// Frames the request target in the active canvas viewport and arms the highlight
        /// overlay for <see cref="DurationSeconds"/> seconds. Marshals to the Rhino UI thread.
        /// </summary>
        /// <param name="request">The pointer request to display.</param>
        /// <param name="cancellationToken">Cancellation checked before dispatching to the UI thread.</param>
        /// <returns>The show outcome, including resolved and missing GUIDs.</returns>
        public static Task<CanvasPointerShowResult> ShowAsync(CanvasPointerRequest request, CancellationToken cancellationToken)
        {
            if (request == null)
            {
                return Task.FromResult(new CanvasPointerShowResult { Error = "No pointer request was provided." });
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromResult(new CanvasPointerShowResult { Error = "The pointer request was cancelled." });
            }

            try
            {
                var result = CanvasAccess.RunOnUiThread(() => ShowCore(request));
                return Task.FromResult(result);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CanvasPointerService] Show failed: {ex.Message}");
                return Task.FromResult(new CanvasPointerShowResult { Error = $"Pointer display failed: {ex.Message}" });
            }
        }

        /// <summary>
        /// Clears the currently displayed highlight, if any.
        /// </summary>
        public static void Clear()
        {
            lock (Sync)
            {
                active = null;
            }

            StopTimer();
            RefreshCanvas();
        }

        private static CanvasPointerShowResult ShowCore(CanvasPointerRequest request)
        {
            // Runs on the Rhino UI thread.
            EnsureInitialized();

            var canvas = Instances.ActiveCanvas;
            var document = canvas?.Document;
            if (canvas == null || document == null)
            {
                return new CanvasPointerShowResult { Error = "No active Grasshopper canvas is available." };
            }

            var result = new CanvasPointerShowResult();
            var attributes = new List<IGH_Attributes>();
            foreach (var guid in request.Guids ?? Enumerable.Empty<Guid>())
            {
                var obj = document.FindObject(guid, true);
                if (obj?.Attributes != null)
                {
                    attributes.Add(obj.Attributes);
                    result.FramedGuids.Add(guid);
                }
                else
                {
                    result.MissingGuids.Add(guid);
                }
            }

            RectangleF? region = null;
            if (request.HasRegion)
            {
                region = new RectangleF(request.X!.Value, request.Y!.Value, request.Width!.Value, request.Height!.Value);
            }

            if (attributes.Count == 0 && !region.HasValue)
            {
                result.Error = result.MissingGuids.Count > 0
                    ? "None of the requested GUIDs could be found on the canvas."
                    : "The pointer request carries no highlight target.";
                return result;
            }

            // Frame the viewport: GH's Focus() for attribute-only targets, manual fit otherwise.
            if (region.HasValue)
            {
                RectangleF? union = region;
                foreach (var attr in attributes)
                {
                    union = union.HasValue ? RectangleF.Union(union.Value, attr.Bounds) : attr.Bounds;
                }

                FrameRectangle(canvas, union!.Value);
            }
            else
            {
                canvas.Viewport.Focus(attributes);
                canvas.Refresh();
            }

            // Arm the highlight.
            var duration = request.DurationSeconds > 0 ? request.DurationSeconds : DurationSeconds;
            lock (Sync)
            {
                active = request;
                shownUtc = DateTime.UtcNow;
                expiresUtc = shownUtc.AddSeconds(duration);
            }

            StartTimer();
            canvas.Refresh();
            return result;
        }

        private static async Task<bool> ReplayAsync(string pointerId, CancellationToken cancellationToken)
        {
            // Reachable only after EnsureInitialized assigned this handler to the bridge.
            if (!TryGet(pointerId, out var request) || request == null)
            {
                return false;
            }

            var result = await ShowAsync(request, cancellationToken).ConfigureAwait(false);
            return result.Success;
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

        private static void StartTimer()
        {
            lock (Sync)
            {
                pulseTimer?.Dispose();
                pulseTimer = new System.Threading.Timer(OnPulse, null, RefreshIntervalMs, RefreshIntervalMs);
            }
        }

        private static void StopTimer()
        {
            lock (Sync)
            {
                pulseTimer?.Dispose();
                pulseTimer = null;
            }
        }

        private static void OnPulse(object? state)
        {
            var expired = false;
            lock (Sync)
            {
                if (active == null)
                {
                    return;
                }

                if (DateTime.UtcNow >= expiresUtc)
                {
                    active = null;
                    expired = true;
                }
            }

            if (expired)
            {
                StopTimer();
            }

            // Repaint the canvas so the highlight animates and disappears on expiry.
            RefreshCanvas();
        }

        private static void RefreshCanvas()
        {
            try
            {
                RhinoApp.InvokeOnUiThread(() =>
                {
                    try
                    {
                        Instances.ActiveCanvas?.Refresh();
                    }
                    catch
                    {
                        // Canvas may be gone; nothing to repaint.
                    }
                });
            }
            catch
            {
                // UI thread unavailable (shutdown/headless); nothing to repaint.
            }
        }

        private static void OnCanvasPostPaintOverlay(GH_Canvas canvas)
        {
            CanvasPointerRequest? request;
            DateTime shown;
            lock (Sync)
            {
                request = active;
                shown = shownUtc;
            }

            if (request == null || canvas?.Document == null || canvas.Graphics == null)
            {
                return;
            }

            var bounds = ResolveWorldBounds(canvas, request);
            if (!bounds.HasValue)
            {
                return;
            }

            var graphics = canvas.Graphics;
            var oldTransform = graphics.Transform;
            graphics.ResetTransform();
            graphics.SmoothingMode = SmoothingMode.AntiAlias;

            try
            {
                var projected = ProjectBounds(canvas.Viewport, bounds.Value);
                projected.Inflate(HighlightInflate, HighlightInflate);

                // Gentle pulse so the highlight reads as transient attention, not a selection.
                var elapsed = (DateTime.UtcNow - shown).TotalSeconds;
                var alpha = 200 + (int)(30 * Math.Sin(elapsed * Math.PI * 2.0 / 1.5));
                var color = Color.FromArgb(Math.Max(120, Math.Min(255, alpha)), HighlightColor);

                using (var fill = new SolidBrush(Color.FromArgb(28, HighlightColor)))
                using (var pen = new Pen(color, BorderWidth))
                using (var path = RoundedRectangle(projected, 8f))
                {
                    graphics.FillPath(fill, path);
                    graphics.DrawPath(pen, path);
                }
            }
            finally
            {
                graphics.Transform = oldTransform;
                oldTransform.Dispose();
            }
        }

        /// <summary>
        /// Resolves the union world-space bounds of the request target on the given canvas.
        /// GUID bounds are resolved live so the highlight follows moved objects.
        /// </summary>
        private static RectangleF? ResolveWorldBounds(GH_Canvas canvas, CanvasPointerRequest request)
        {
            RectangleF? union = null;

            var document = canvas.Document;
            if (document != null && request.Guids != null)
            {
                foreach (var guid in request.Guids)
                {
                    var obj = document.FindObject(guid, true);
                    var bounds = obj?.Attributes?.Bounds;
                    if (bounds.HasValue)
                    {
                        union = union.HasValue ? RectangleF.Union(union.Value, bounds.Value) : bounds.Value;
                    }
                }
            }

            if (request.HasRegion)
            {
                var region = new RectangleF(request.X!.Value, request.Y!.Value, request.Width!.Value, request.Height!.Value);
                union = union.HasValue ? RectangleF.Union(union.Value, region) : region;
            }

            return union;
        }

        /// <summary>
        /// Pans and zooms the viewport so the given world-space bounds are visible with margin.
        /// </summary>
        private static bool FrameRectangle(GH_Canvas canvas, RectangleF bounds)
        {
            var viewport = canvas.Viewport;
            if (viewport == null)
            {
                return false;
            }

            var padded = bounds;
            padded.Inflate(FrameMargin, FrameMargin);
            if (padded.Width <= 0f || padded.Height <= 0f)
            {
                return false;
            }

            var zoom = Math.Min(viewport.Width / padded.Width, viewport.Height / padded.Height);
            if (float.IsNaN(zoom) || float.IsInfinity(zoom) || zoom <= 0f)
            {
                return false;
            }

            viewport.Zoom = Math.Max(MinZoom, Math.Min(MaxZoom, zoom));
            viewport.MidPoint = new PointF(
                padded.Left + (padded.Width / 2f),
                padded.Top + (padded.Height / 2f));
            canvas.Refresh();
            return true;
        }

        private static RectangleF ProjectBounds(GH_Viewport viewport, RectangleF bounds)
        {
            var topLeft = new PointF(bounds.Left, bounds.Top);
            var bottomRight = new PointF(bounds.Right, bounds.Bottom);
            viewport.Project(ref topLeft);
            viewport.Project(ref bottomRight);
            return RectangleF.FromLTRB(topLeft.X, topLeft.Y, bottomRight.X, bottomRight.Y);
        }

        private static GraphicsPath RoundedRectangle(RectangleF bounds, float radius)
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
