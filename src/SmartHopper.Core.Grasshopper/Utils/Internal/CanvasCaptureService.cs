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
    using global::Grasshopper;
    using global::Grasshopper.GUI.Canvas;
    using global::Rhino;

    /// <summary>
    /// Captures the visible Grasshopper canvas on Rhino's UI thread.
    /// </summary>
    public sealed class CanvasCaptureService : ICanvasCaptureService
    {
        /// <summary>Default maximum capture width.</summary>
        public const int DefaultMaxWidth = 1920;

        /// <summary>Default maximum capture height.</summary>
        public const int DefaultMaxHeight = 1080;

        /// <inheritdoc/>
        public Task<ImageCaptureResult> CaptureCanvasAsync(int? maxWidth, int? maxHeight)
        {
            int resolvedWidth = ImageCaptureUtilities.ValidateDimension(maxWidth ?? DefaultMaxWidth, nameof(maxWidth));
            int resolvedHeight = ImageCaptureUtilities.ValidateDimension(maxHeight ?? DefaultMaxHeight, nameof(maxHeight));
            var completion = new TaskCompletionSource<ImageCaptureResult>(TaskCreationOptions.RunContinuationsAsynchronously);

            void Capture()
            {
                try
                {
                    var canvas = Instances.ActiveCanvas;
                    if (canvas == null)
                    {
                        throw new InvalidOperationException("No active Grasshopper canvas is available.");
                    }

                    using var bitmap = canvas.GetCanvasScreenBuffer(GH_CanvasMode.Export);
                    if (bitmap == null)
                    {
                        throw new InvalidOperationException("The active Grasshopper canvas could not be captured.");
                    }

                    completion.SetResult(ImageCaptureUtilities.EncodeToBase64Png(bitmap, resolvedWidth, resolvedHeight));
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
