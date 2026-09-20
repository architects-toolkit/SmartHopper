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

#pragma warning disable CA1416

namespace SmartHopper.Core.Grasshopper.Utils.Internal
{
    using System;
    using System.Drawing;
    using System.Drawing.Imaging;
    using System.Threading.Tasks;
    using global::Grasshopper;
    using global::Grasshopper.GUI.Canvas;
    using global::Grasshopper.Kernel;
    using global::Rhino;

    /// <summary>
    /// Renders an arbitrary region of the Grasshopper canvas to a high-resolution PNG
    /// by tiling <see cref="GH_Canvas.GenerateHiResImageTile(GH_Viewport, Color)"/> calls,
    /// the same renderer used by File &gt; Export Hi-Res Image. Rendering happens on
    /// Rhino's UI thread.
    /// </summary>
    public sealed class CanvasHiResCaptureService : ICanvasHiResCaptureService
    {
        /// <summary>Default maximum output width or height in pixels.</summary>
        public const int DefaultMaxDimension = 16384;

        /// <summary>Absolute maximum output width or height in pixels.</summary>
        public const int AbsoluteMaxDimension = 30000;

        /// <inheritdoc/>
        public Task<ImageCaptureResult> CaptureHiResAsync(CanvasHiResCaptureRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            float scale = request.Scale;
            if (scale <= 0f || float.IsNaN(scale) || float.IsInfinity(scale))
            {
                throw new ArgumentOutOfRangeException(nameof(request), scale, "Scale must be a positive finite number.");
            }

            int maxDimension = request.MaxDimension;
            if (maxDimension < 1 || maxDimension > AbsoluteMaxDimension)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(request),
                    maxDimension,
                    $"MaxDimension must be between 1 and {AbsoluteMaxDimension} pixels.");
            }

            var completion = new TaskCompletionSource<ImageCaptureResult>(TaskCreationOptions.RunContinuationsAsynchronously);

            void Capture()
            {
                try
                {
                    var canvas = Instances.ActiveCanvas;
                    var document = canvas?.Document;
                    if (document == null)
                    {
                        throw new InvalidOperationException("No active Grasshopper canvas is available.");
                    }

                    // Color.Empty is the sentinel for "use the live canvas background".
                    var background = request.Background.IsEmpty ? canvas.BackColor : request.Background;

                    var bounds = ResolveRegion(document, request);
                    if (bounds.Width <= 0f || bounds.Height <= 0f)
                    {
                        throw new InvalidOperationException("The requested canvas region is empty.");
                    }

                    bounds.Inflate(request.Padding, request.Padding);

                    int totalWidth = Math.Max(1, (int)Math.Ceiling(bounds.Width * scale));
                    int totalHeight = Math.Max(1, (int)Math.Ceiling(bounds.Height * scale));
                    if (totalWidth > maxDimension || totalHeight > maxDimension)
                    {
                        throw new InvalidOperationException(
                            $"The requested export would be {totalWidth}x{totalHeight}px, exceeding the {maxDimension}px limit. " +
                            "Lower 'scale' or narrow the region via scope/guids/bounds.");
                    }

                    var tileSize = GH_Canvas.GH_ImageSettings.TileSize;
                    int tileWidth = Math.Max(1, tileSize.Width);
                    int tileHeight = Math.Max(1, tileSize.Height);
                    int columns = (int)Math.Ceiling(totalWidth / (double)tileWidth);
                    int rows = (int)Math.Ceiling(totalHeight / (double)tileHeight);
                    float tileCanvasWidth = tileWidth / scale;
                    float tileCanvasHeight = tileHeight / scale;

                    using var output = new Bitmap(totalWidth, totalHeight, PixelFormat.Format32bppArgb);
                    using (var graphics = Graphics.FromImage(output))
                    {
                        graphics.Clear(background);
                        for (int row = 0; row < rows; row++)
                        {
                            for (int col = 0; col < columns; col++)
                            {
                                var viewport = new GH_Viewport
                                {
                                    Zoom = scale,
                                    Width = tileWidth,
                                    Height = tileHeight,
                                    MidPoint = new PointF(
                                        bounds.Left + ((col + 0.5f) * tileCanvasWidth),
                                        bounds.Top + ((row + 0.5f) * tileCanvasHeight)),
                                };

                                using var tile = canvas.GenerateHiResImageTile(viewport, background);
                                if (tile == null)
                                {
                                    continue;
                                }

                                var destination = new Rectangle(col * tileWidth, row * tileHeight, tileWidth, tileHeight);
                                destination.Intersect(new Rectangle(0, 0, totalWidth, totalHeight));
                                if (destination.Width <= 0 || destination.Height <= 0)
                                {
                                    continue;
                                }

                                graphics.DrawImage(
                                    tile,
                                    destination,
                                    new Rectangle(0, 0, destination.Width, destination.Height),
                                    GraphicsUnit.Pixel);
                            }
                        }
                    }

                    completion.SetResult(ImageCaptureUtilities.EncodeExactToBase64Png(output));
                }
                catch (Exception ex)
                {
                    completion.SetException(ex);
                }
            }

            if (RhinoApp.InvokeRequired)
            {
                RhinoApp.InvokeOnUiThread((Action)Capture);
            }
            else
            {
                Capture();
            }

            return completion.Task;
        }

        private static RectangleF ResolveRegion(GH_Document document, CanvasHiResCaptureRequest request)
        {
            switch (request.Scope)
            {
                case CanvasHiResScope.Bounds:
                    return request.Bounds;
                case CanvasHiResScope.Selection:
                    return UnionAttributesBounds(document.SelectedObjects(), "selection");
                case CanvasHiResScope.Guids:
                {
                    var objects = new System.Collections.Generic.List<IGH_DocumentObject>();
                    foreach (var guid in request.Guids)
                    {
                        var obj = document.FindObject(guid, true);
                        if (obj != null)
                        {
                            objects.Add(obj);
                        }
                    }

                    return UnionAttributesBounds(objects, "the supplied guids");
                }

                case CanvasHiResScope.Document:
                default:
                    return document.BoundingBox(false);
            }
        }

        private static RectangleF UnionAttributesBounds(System.Collections.Generic.IEnumerable<IGH_DocumentObject> objects, string label)
        {
            var bounds = RectangleF.Empty;
            bool any = false;
            foreach (var obj in objects)
            {
                if (obj?.Attributes == null)
                {
                    continue;
                }

                var b = obj.Attributes.Bounds;
                bounds = any ? RectangleF.Union(bounds, b) : b;
                any = true;
            }

            if (!any)
            {
                throw new InvalidOperationException($"No objects were found for {label}.");
            }

            return bounds;
        }
    }
}
