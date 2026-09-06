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
    using System.Drawing.Drawing2D;
    using System.Drawing.Imaging;
    using System.IO;

    /// <summary>
    /// Encodes screenshot bitmaps as bounded base64 PNG payloads.
    /// </summary>
    public static class ImageCaptureUtilities
    {
        /// <summary>Maximum accepted width or height for a screenshot result.</summary>
        public const int MaximumDimension = 4096;

        /// <summary>
        /// Resizes a bitmap to fit inside the requested dimensions without upscaling, then encodes it as PNG.
        /// </summary>
        public static ImageCaptureResult EncodeToBase64Png(Bitmap bitmap, int? maxWidth, int? maxHeight)
        {
            ArgumentNullException.ThrowIfNull(bitmap);

            int boundedWidth = ValidateDimension(maxWidth ?? bitmap.Width, nameof(maxWidth));
            int boundedHeight = ValidateDimension(maxHeight ?? bitmap.Height, nameof(maxHeight));
            double scale = Math.Min(1d, Math.Min(
                boundedWidth / (double)bitmap.Width,
                boundedHeight / (double)bitmap.Height));
            int targetWidth = Math.Max(1, (int)Math.Floor(bitmap.Width * scale));
            int targetHeight = Math.Max(1, (int)Math.Floor(bitmap.Height * scale));

            Bitmap? resized = null;
            Bitmap imageToEncode = bitmap;
            try
            {
                if (targetWidth != bitmap.Width || targetHeight != bitmap.Height)
                {
                    resized = new Bitmap(targetWidth, targetHeight);
                    using var graphics = Graphics.FromImage(resized);
                    graphics.CompositingMode = CompositingMode.SourceCopy;
                    graphics.CompositingQuality = CompositingQuality.HighQuality;
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    graphics.SmoothingMode = SmoothingMode.HighQuality;
                    graphics.DrawImage(bitmap, 0, 0, targetWidth, targetHeight);
                    imageToEncode = resized;
                }

                using var stream = new MemoryStream();
                imageToEncode.Save(stream, ImageFormat.Png);
                return new ImageCaptureResult(
                    Convert.ToBase64String(stream.ToArray()),
                    imageToEncode.Width,
                    imageToEncode.Height);
            }
            finally
            {
                resized?.Dispose();
            }
        }

        /// <summary>
        /// Validates an externally supplied screenshot dimension.
        /// </summary>
        public static int ValidateDimension(int value, string parameterName)
        {
            if (value < 1 || value > MaximumDimension)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    $"Screenshot dimensions must be between 1 and {MaximumDimension} pixels.");
            }

            return value;
        }
    }
}
