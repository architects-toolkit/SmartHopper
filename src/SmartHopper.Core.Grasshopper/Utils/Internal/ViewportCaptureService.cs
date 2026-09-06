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

namespace SmartHopper.Core.Grasshopper.Utils.Internal
{
    using System;
    using System.Threading.Tasks;
    using global::Rhino;
    using global::Rhino.Display;

    /// <summary>
    /// Captures the active or named Rhino viewport on Rhino's UI thread.
    /// </summary>
    public sealed class ViewportCaptureService : IViewportCaptureService
    {
        /// <summary>Default maximum capture width.</summary>
        public const int DefaultWidth = 1024;

        /// <summary>Default maximum capture height.</summary>
        public const int DefaultHeight = 1024;

        /// <inheritdoc/>
        public Task<ImageCaptureResult> CaptureViewportAsync(string? viewName, int? width, int? height)
        {
            int resolvedWidth = ImageCaptureUtilities.ValidateDimension(width ?? DefaultWidth, nameof(width));
            int resolvedHeight = ImageCaptureUtilities.ValidateDimension(height ?? DefaultHeight, nameof(height));
            var completion = new TaskCompletionSource<ImageCaptureResult>(TaskCreationOptions.RunContinuationsAsynchronously);

            void Capture()
            {
                try
                {
                    var document = RhinoDoc.ActiveDoc;
                    if (document == null)
                    {
                        throw new InvalidOperationException("No active Rhino document is available.");
                    }

                    var view = string.IsNullOrWhiteSpace(viewName)
                        ? document.Views.ActiveView
                        : document.Views.Find(viewName, compareCase: false);
                    if (view == null)
                    {
                        throw new InvalidOperationException(string.IsNullOrWhiteSpace(viewName)
                            ? "No active Rhino viewport is available."
                            : $"Rhino viewport '{viewName}' was not found.");
                    }

                    var capture = new ViewCapture();
                    using var bitmap = capture.CaptureToBitmap(view);
                    if (bitmap == null)
                    {
                        throw new InvalidOperationException("The Rhino viewport could not be captured.");
                    }

                    var encoded = ImageCaptureUtilities.EncodeToBase64Png(bitmap, resolvedWidth, resolvedHeight);
                    completion.SetResult(new ImageCaptureResult(
                        encoded.ImageBase64,
                        encoded.Width,
                        encoded.Height,
                        view.MainViewport.Name));
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
    }
}
