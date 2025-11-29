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
 * DialogCanvasLink.cs
 * Manages visual connections between dialogs and Grasshopper canvas components.
 * Draws a line from a linked component to the dialog window, similar to the script editor anchor.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using Eto.Forms;
using Grasshopper;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using Rhino;

namespace SmartHopper.Core.UI
{
    /// <summary>
    /// Manages visual connections between dialogs and Grasshopper canvas components.
    /// When a dialog is linked to a component, draws a visual anchor line from the component
    /// to the dialog window, similar to the script editor connection.
    /// </summary>
    public static class DialogCanvasLink
    {
        private static readonly object LockObject = new object();
        private static readonly Dictionary<Window, LinkInfo> ActiveLinks = new Dictionary<Window, LinkInfo>();
        private static bool isHooked;

        /// <summary>
        /// Information about a dialog-component link.
        /// </summary>
        private class LinkInfo
        {
            public Guid InstanceGuid { get; set; }

            public Color LineColor { get; set; }

            public float LineWidth { get; set; }
        }

        /// <summary>
        /// Callback to center the canvas view on a component. Set by SmartHopper.Core.Grasshopper at initialization.
        /// Signature: (Guid instanceGuid, float horizontalPosition) => bool (returns true if canvas was panned).
        /// horizontalPosition: 0 = left edge, 0.5 = center, 1 = right edge.
        /// </summary>
        public static Func<Guid, float, bool> CenterCanvasOnComponentCallback { get; set; }

        /// <summary>
        /// Registers a dialog to be visually linked to a component on the canvas.
        /// </summary>
        /// <param name="dialog">The dialog window to link.</param>
        /// <param name="instanceGuid">The GUID of the component to link to.</param>
        /// <param name="lineColor">Optional custom line color. Defaults to a semi-transparent SmartHopper green.</param>
        /// <param name="lineWidth">Optional line width. Defaults to 3.</param>
        /// <param name="centerCanvas">If true, centers the canvas view on the component before showing the dialog.</param>
        public static void RegisterLink(Window dialog, Guid instanceGuid, Color? lineColor = null, float lineWidth = 3f, bool centerCanvas = true)
        {
            Debug.WriteLine($"[DialogCanvasLink] RegisterLink called: dialog={dialog != null}, guid={instanceGuid}");

            if (dialog == null)
            {
                Debug.WriteLine("[DialogCanvasLink] RegisterLink aborted: dialog is null");
                return;
            }

            if (instanceGuid == Guid.Empty)
            {
                Debug.WriteLine("[DialogCanvasLink] RegisterLink aborted: instanceGuid is empty");
                return;
            }

            // Center canvas on the component if requested (before showing dialog)
            // Position at 1/3 from left so the dialog (which appears on the right) doesn't hide the component
            if (centerCanvas && CenterCanvasOnComponentCallback != null)
            {
                CenterCanvasOnComponentCallback(instanceGuid, 1f / 3f);
            }

            lock (LockObject)
            {
                // Ensure canvas events are hooked
                EnsureHooked();

                // Store link info
                ActiveLinks[dialog] = new LinkInfo
                {
                    InstanceGuid = instanceGuid,
                    LineColor = lineColor ?? Color.FromArgb(200, 32, 152, 72), // Semi-transparent SmartHopper green
                    LineWidth = lineWidth,
                };

                // Subscribe to dialog events
                dialog.Closed += OnDialogClosed;
                dialog.LocationChanged += OnDialogLocationChanged;
                dialog.SizeChanged += OnDialogSizeChanged;

                Debug.WriteLine($"[DialogCanvasLink] Registered link: Dialog → Component {instanceGuid}, ActiveLinks count: {ActiveLinks.Count}");

                // Trigger canvas redraw to show the link
                RefreshCanvas();
            }
        }

        /// <summary>
        /// Unregisters a dialog link.
        /// </summary>
        /// <param name="dialog">The dialog to unregister.</param>
        public static void UnregisterLink(Window dialog)
        {
            if (dialog == null)
            {
                return;
            }

            lock (LockObject)
            {
                if (ActiveLinks.Remove(dialog))
                {
                    dialog.Closed -= OnDialogClosed;
                    dialog.LocationChanged -= OnDialogLocationChanged;
                    dialog.SizeChanged -= OnDialogSizeChanged;
                    Debug.WriteLine("[DialogCanvasLink] Unregistered link");
                    RefreshCanvas();
                }
            }
        }

        /// <summary>
        /// Checks if a dialog is currently linked.
        /// </summary>
        /// <param name="dialog">The dialog to check.</param>
        /// <returns>True if the dialog has an active link.</returns>
        public static bool IsLinked(Window dialog)
        {
            lock (LockObject)
            {
                return dialog != null && ActiveLinks.ContainsKey(dialog);
            }
        }

        /// <summary>
        /// Gets the linked component GUID for a dialog.
        /// </summary>
        /// <param name="dialog">The dialog to query.</param>
        /// <returns>The linked component GUID, or Guid.Empty if not linked.</returns>
        public static Guid GetLinkedGuid(Window dialog)
        {
            lock (LockObject)
            {
                if (dialog != null && ActiveLinks.TryGetValue(dialog, out var info))
                {
                    return info.InstanceGuid;
                }

                return Guid.Empty;
            }
        }

        private static void EnsureHooked()
        {
            Debug.WriteLine($"[DialogCanvasLink] EnsureHooked called, isHooked={isHooked}");

            if (isHooked)
            {
                Debug.WriteLine("[DialogCanvasLink] Already hooked, skipping");
                return;
            }

            try
            {
                // Hook into canvas created event for new canvases
                Instances.CanvasCreated += OnCanvasCreated;
                Debug.WriteLine("[DialogCanvasLink] Subscribed to CanvasCreated event");

                // Hook into existing canvas if available
                if (Instances.ActiveCanvas != null)
                {
                    Debug.WriteLine("[DialogCanvasLink] ActiveCanvas found, hooking...");
                    HookCanvas(Instances.ActiveCanvas);
                }
                else
                {
                    Debug.WriteLine("[DialogCanvasLink] No ActiveCanvas found");
                }

                isHooked = true;
                Debug.WriteLine("[DialogCanvasLink] Canvas events hooked successfully");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DialogCanvasLink] Error hooking canvas: {ex.Message}");
            }
        }

        private static void OnCanvasCreated(GH_Canvas canvas)
        {
            HookCanvas(canvas);
        }

        private static void HookCanvas(GH_Canvas canvas)
        {
            if (canvas == null)
            {
                Debug.WriteLine("[DialogCanvasLink] HookCanvas: canvas is null");
                return;
            }

            Debug.WriteLine("[DialogCanvasLink] HookCanvas: Hooking canvas paint event");

            // Use CanvasPostPaintOverlay to draw on top of everything
            canvas.CanvasPostPaintOverlay -= OnCanvasPostPaintOverlay;
            canvas.CanvasPostPaintOverlay += OnCanvasPostPaintOverlay;

            Debug.WriteLine("[DialogCanvasLink] HookCanvas: Canvas hooked successfully");
        }

        private static void OnCanvasPostPaintOverlay(GH_Canvas canvas)
        {
            lock (LockObject)
            {
                if (ActiveLinks.Count == 0)
                {
                    return;
                }

                Debug.WriteLine($"[DialogCanvasLink] OnCanvasPostPaintOverlay: ActiveLinks={ActiveLinks.Count}");

                var doc = canvas.Document;
                if (doc == null)
                {
                    Debug.WriteLine("[DialogCanvasLink] OnCanvasPostPaintOverlay: Document is null");
                    return;
                }

                foreach (var kvp in ActiveLinks)
                {
                    var dialog = kvp.Key;
                    var linkInfo = kvp.Value;

                    // Find the component
                    var component = doc.FindObject(linkInfo.InstanceGuid, true);
                    if (component == null)
                    {
                        Debug.WriteLine($"[DialogCanvasLink] Component not found: {linkInfo.InstanceGuid}");
                        continue;
                    }

                    // Get component bounds in canvas coordinates
                    var componentBounds = component.Attributes.Bounds;
                    var componentCenter = new PointF(
                        componentBounds.X + (componentBounds.Width / 2),
                        componentBounds.Y + (componentBounds.Height / 2));

                    // Get dialog window position in screen coordinates
                    var dialogScreenPos = GetDialogScreenPosition(dialog);
                    if (!dialogScreenPos.HasValue)
                    {
                        Debug.WriteLine("[DialogCanvasLink] Could not get dialog screen position");
                        continue;
                    }

                    Debug.WriteLine($"[DialogCanvasLink] Drawing link: component={componentCenter}, dialogScreen={dialogScreenPos.Value}");

                    // Convert dialog screen position to canvas coordinates
                    var dialogCanvasPos = canvas.Viewport.UnprojectPoint(
                        canvas.PointToClient(new System.Drawing.Point(
                            (int)dialogScreenPos.Value.X,
                            (int)dialogScreenPos.Value.Y)));

                    Debug.WriteLine($"[DialogCanvasLink] Canvas positions: component={componentCenter}, dialog={dialogCanvasPos}");

                    // Draw the connection line
                    DrawConnectionLine(
                        canvas.Graphics,
                        canvas,
                        componentCenter,
                        dialogCanvasPos,
                        linkInfo.LineColor,
                        linkInfo.LineWidth);

                    // Draw a dot at the component end
                    DrawAnchorDot(
                        canvas.Graphics,
                        canvas,
                        componentCenter,
                        linkInfo.LineColor);
                }
            }
        }

        private static Eto.Drawing.PointF? GetDialogScreenPosition(Window dialog)
        {
            try
            {
                // Get the dialog's location on screen
                var location = dialog.Location;
                var size = dialog.Size;

                // Return the center point of the dialog
                return new Eto.Drawing.PointF(
                    location.X + (size.Width / 2),
                    location.Y + (size.Height / 2));
            }
            catch
            {
                return null;
            }
        }

        private static void DrawConnectionLine(
            Graphics graphics,
            GH_Canvas canvas,
            PointF componentPos,
            PointF dialogPos,
            Color color,
            float width)
        {
            try
            {
                // Convert canvas coordinates to screen/client coordinates for drawing
                var startClient = canvas.Viewport.ProjectPoint(componentPos);
                var endClient = canvas.Viewport.ProjectPoint(dialogPos);

                // Save transform and reset for screen-space drawing
                var savedTransform = graphics.Transform;
                graphics.ResetTransform();

                try
                {
                    // Create a bezier curve for a smooth connection
                    using (var path = new GraphicsPath())
                    {
                        // Calculate control points for a nice curve
                        var dx = endClient.X - startClient.X;
                        var controlOffset = Math.Min(Math.Abs(dx) * 0.5f, 150f);

                        var control1 = new PointF(startClient.X + controlOffset, startClient.Y);
                        var control2 = new PointF(endClient.X - controlOffset, endClient.Y);

                        path.AddBezier(startClient, control1, control2, endClient);

                        // Draw shadow
                        using (var shadowPen = new Pen(Color.FromArgb(50, Color.Black), width + 2))
                        {
                            shadowPen.LineJoin = LineJoin.Round;
                            shadowPen.StartCap = LineCap.Round;
                            shadowPen.EndCap = LineCap.Round;
                            graphics.DrawPath(shadowPen, path);
                        }

                        // Draw main line
                        using (var pen = new Pen(color, width))
                        {
                            pen.LineJoin = LineJoin.Round;
                            pen.StartCap = LineCap.Round;
                            pen.EndCap = LineCap.Round;
                            graphics.DrawPath(pen, path);
                        }
                    }
                }
                finally
                {
                    graphics.Transform = savedTransform;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DialogCanvasLink] Error drawing connection: {ex.Message}");
            }
        }

        private static void DrawAnchorDot(
            Graphics graphics,
            GH_Canvas canvas,
            PointF componentPos,
            Color color)
        {
            try
            {
                // Convert to client coordinates
                var clientPos = canvas.Viewport.ProjectPoint(componentPos);

                // Save transform
                var savedTransform = graphics.Transform;
                graphics.ResetTransform();

                try
                {
                    const float dotRadius = 6f;
                    var dotBounds = new RectangleF(
                        clientPos.X - dotRadius,
                        clientPos.Y - dotRadius,
                        dotRadius * 2,
                        dotRadius * 2);

                    // Draw outer glow
                    using (var glowBrush = new SolidBrush(Color.FromArgb(80, color)))
                    {
                        var glowBounds = new RectangleF(
                            dotBounds.X - 3,
                            dotBounds.Y - 3,
                            dotBounds.Width + 6,
                            dotBounds.Height + 6);
                        graphics.FillEllipse(glowBrush, glowBounds);
                    }

                    // Draw main dot
                    using (var brush = new SolidBrush(color))
                    {
                        graphics.FillEllipse(brush, dotBounds);
                    }

                    // Draw highlight
                    using (var highlightBrush = new SolidBrush(Color.FromArgb(150, Color.White)))
                    {
                        var highlightBounds = new RectangleF(
                            dotBounds.X + 2,
                            dotBounds.Y + 2,
                            dotBounds.Width * 0.4f,
                            dotBounds.Height * 0.4f);
                        graphics.FillEllipse(highlightBrush, highlightBounds);
                    }
                }
                finally
                {
                    graphics.Transform = savedTransform;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DialogCanvasLink] Error drawing anchor dot: {ex.Message}");
            }
        }

        private static void OnDialogClosed(object sender, EventArgs e)
        {
            if (sender is Window dialog)
            {
                UnregisterLink(dialog);
            }
        }

        private static void OnDialogLocationChanged(object sender, EventArgs e)
        {
            // Refresh canvas when dialog moves so the link follows
            RefreshCanvas();
        }

        private static void OnDialogSizeChanged(object sender, EventArgs e)
        {
            // Refresh canvas when dialog resizes so the link points to the new center
            RefreshCanvas();
        }

        private static void RefreshCanvas()
        {
            try
            {
                RhinoApp.InvokeOnUiThread(() =>
                {
                    Instances.ActiveCanvas?.Refresh();
                });
            }
            catch
            {
                // Ignore refresh errors
            }
        }
    }
}
