/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Attributes;

namespace SmartHopper.Components.Img
{
    /// <summary>
    /// Custom attributes for the ImageViewerComponent that provides a large square display area for images.
    /// </summary>
    public class ImageViewerAttributes : GH_ComponentAttributes
    {
        private const int ImageDisplaySize = 200; // Size of the square image display area
        private const int Padding = 8;

        private RectangleF _imageDisplayBounds;

        /// <summary>
        /// Initializes a new instance of the ImageViewerAttributes class.
        /// </summary>
        /// <param name="owner">The component that owns these attributes.</param>
        public ImageViewerAttributes(IGH_Component owner) : base(owner)
        {
        }

        /// <summary>
        /// Gets the ImageViewerComponent as a typed reference.
        /// </summary>
        private ImageViewerComponent ImageViewerComponent => this.Owner as ImageViewerComponent;

        /// <summary>
        /// Layout the component bounds.
        /// </summary>
        protected override void Layout()
        {
            // First, calculate what the bounds should be after expansion
            // Call base to get initial bounds calculation
            base.Layout();
            var baseBounds = this.Bounds;

            // Calculate expanded bounds to include image area
            var expandedHeight = ImageDisplaySize + (Padding * 2);
            var expandedWidth = Math.Max(baseBounds.Width, ImageDisplaySize + (Padding * 4));

            // Set the correct final bounds before any parameter layout
            this.Bounds = new RectangleF(
                baseBounds.X + (baseBounds.Width - expandedWidth) / 2, // Center horizontally
                baseBounds.Y,
                expandedWidth,
                expandedHeight);

            // Position image display area below the original component area
            var imageAreaY = baseBounds.Y + Padding;
            this._imageDisplayBounds = new RectangleF(
                this.Bounds.X + (this.Bounds.Width - ImageDisplaySize) / 2,
                imageAreaY,
                ImageDisplaySize,
                ImageDisplaySize);

            // Now layout parameters with the final bounds so grips align correctly
            GH_ComponentAttributes.LayoutInputParams(this.Owner, this._imageDisplayBounds);
            GH_ComponentAttributes.LayoutOutputParams(this.Owner, this._imageDisplayBounds);
        }

        /// <summary>
        /// Render the component.
        /// </summary>
        /// <param name="canvas">The canvas to render on.</param>
        /// <param name="graphics">The graphics object.</param>
        /// <param name="channel">The render channel.</param>
        protected override void Render(GH_Canvas canvas, Graphics graphics, GH_CanvasChannel channel)
        {
            // Let base handle all default rendering (component box, inputs, outputs, etc.)
            base.Render(canvas, graphics, channel);
            
            // Only add our custom image display if rendering objects
            if (channel == GH_CanvasChannel.Objects)
            {
                // Set high-quality rendering for image
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                // Draw the image display area
                this.RenderImageDisplay(graphics);
            }
        }

        /// <summary>
        /// Renders the image display area.
        /// </summary>
        /// <param name="graphics">The graphics object.</param>
        private void RenderImageDisplay(Graphics graphics)
        {
            // Draw background for image area
            using (var brush = new SolidBrush(Color.White))
            {
                graphics.FillRectangle(brush, this._imageDisplayBounds);
            }

            // Draw border around image area
            using (var pen = new Pen(Color.Gray, 1))
            {
                graphics.DrawRectangle(pen, Rectangle.Round(this._imageDisplayBounds));
            }

            // Get and display the bitmap
            var bitmap = this.ImageViewerComponent?.GetDisplayBitmap();
            if (bitmap != null)
            {
                try
                {
                    // Calculate the scaling to fit the image within the display bounds while maintaining aspect ratio
                    var scaleX = this._imageDisplayBounds.Width / bitmap.Width;
                    var scaleY = this._imageDisplayBounds.Height / bitmap.Height;
                    var scale = Math.Min(scaleX, scaleY);

                    var scaledWidth = bitmap.Width * scale;
                    var scaledHeight = bitmap.Height * scale;

                    // Center the image within the display bounds
                    var imageRect = new RectangleF(
                        this._imageDisplayBounds.X + (this._imageDisplayBounds.Width - scaledWidth) / 2,
                        this._imageDisplayBounds.Y + (this._imageDisplayBounds.Height - scaledHeight) / 2,
                        scaledWidth,
                        scaledHeight);

                    // Draw the bitmap
                    graphics.DrawImage(bitmap, imageRect);
                }
                catch (Exception ex)
                {
                    // If image drawing fails, show error text
                    this.DrawCenteredText(graphics, this._imageDisplayBounds, $"Error: {ex.Message}", Color.Red);
                }
            }
            else
            {
                // Show placeholder text when no image is available
                this.DrawCenteredText(graphics, this._imageDisplayBounds, "No Image", Color.Gray);
            }
        }



        /// <summary>
        /// Draws centered text within the specified rectangle.
        /// </summary>
        /// <param name="graphics">The graphics object.</param>
        /// <param name="bounds">The bounds to center the text within.</param>
        /// <param name="text">The text to draw.</param>
        /// <param name="color">The text color.</param>
        private void DrawCenteredText(Graphics graphics, RectangleF bounds, string text, Color color)
        {
            using (var font = new Font("Arial", 9))
            using (var brush = new SolidBrush(color))
            {
                var format = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };

                graphics.DrawString(text, font, brush, bounds, format);
            }
        }
    }
}
