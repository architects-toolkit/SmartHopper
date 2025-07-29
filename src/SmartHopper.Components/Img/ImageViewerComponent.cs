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
using System.IO;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Attributes;
using Grasshopper.Kernel.Types;
using SmartHopper.Components.Properties;

namespace SmartHopper.Components.Img
{
    /// <summary>
    /// Grasshopper component for viewing and saving bitmap images.
    /// </summary>
    public class ImageViewerComponent : GH_Component
    {
        private Bitmap _displayBitmap;
        private readonly object _bitmapLock = new object();

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("F8E7D6C5-B4A3-9281-7065-43E1F2A9B8C7");

        /// <summary>
        /// Gets the icon for this component.
        /// </summary>
        protected override Bitmap Icon => Resources.smarthopper;

        /// <summary>
        /// Gets the exposure level of this component in the ribbon.
        /// </summary>
        public override GH_Exposure Exposure => GH_Exposure.primary;

        /// <summary>
        /// Initializes a new instance of the ImageViewerComponent class.
        /// </summary>
        public ImageViewerComponent()
            : base(
                  "Image Viewer",
                  "ImgView",
                  "Display bitmap images on the canvas and save them to disk.",
                  "SmartHopper", "Img")
        {
        }

        /// <summary>
        /// Creates the custom attributes for this component.
        /// </summary>
        public override void CreateAttributes()
        {
            this.m_attributes = new ImageViewerAttributes(this);
        }

        /// <summary>
        /// Registers input parameters for this component.
        /// </summary>
        /// <param name="pManager">The parameter manager to register inputs with.</param>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Image", "I", "Bitmap image to display", GH_ParamAccess.item);
            pManager.AddTextParameter("File Path", "F", "File path to save the image (optional)", GH_ParamAccess.item, string.Empty);
            pManager.AddBooleanParameter("Save?", "S", "Trigger to save the image to the specified file path", GH_ParamAccess.item, false);

            // Make parameters optional
            pManager[1].Optional = true;
            pManager[2].Optional = true;
        }

        /// <summary>
        /// Registers output parameters for this component.
        /// </summary>
        /// <param name="pManager">The parameter manager to register outputs with.</param>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Status", "St", "Status of the image viewer and save operations", GH_ParamAccess.item);
            pManager.AddTextParameter("Saved Path", "SP", "Path where the image was saved (if applicable)", GH_ParamAccess.item);
        }

        /// <summary>
        /// Main solving method for the component.
        /// </summary>
        /// <param name="DA">Data access object.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Get inputs
            IGH_Goo imageGoo = null;
            string filePath = string.Empty;
            bool run = false;

            DA.GetData(0, ref imageGoo);
            DA.GetData(1, ref filePath);
            DA.GetData(2, ref run);

            Bitmap bitmap = null;
            string status = "No image";
            string savedPath = string.Empty;

            // Extract bitmap from input
            if (imageGoo != null)
            {
                // Try to get bitmap from GH_ObjectWrapper
                if (imageGoo is GH_ObjectWrapper wrapper && wrapper.Value is Bitmap bmp)
                {
                    bitmap = bmp;
                }
                // Try direct bitmap
                else if (imageGoo.ScriptVariable() is Bitmap directBmp)
                {
                    bitmap = directBmp;
                }
            }

            if (bitmap != null)
            {
                // Always update display bitmap
                this.SetDisplayBitmap(bitmap);
                status = $"Displaying image ({bitmap.Width}x{bitmap.Height})";

                // Handle saving if requested and path is provided
                if (run && !string.IsNullOrWhiteSpace(filePath))
                {
                    try
                    {
                        // Ensure directory exists
                        var directory = Path.GetDirectoryName(filePath);
                        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                        {
                            Directory.CreateDirectory(directory);
                        }

                        // Determine format from extension
                        var extension = Path.GetExtension(filePath).ToLowerInvariant();
                        var format = GetImageFormat(extension);

                        // Save the image
                        using (var clone = bitmap.Clone() as Bitmap)
                        {
                            clone?.Save(filePath, format);
                        }

                        savedPath = filePath;
                        status += $" - Saved to: {Path.GetFileName(filePath)}";
                        this.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, $"Image saved successfully to: {filePath}");
                    }
                    catch (Exception saveEx)
                    {
                        this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"Failed to save image: {saveEx.Message}");
                        status += " - Save failed";
                    }
                }
                else if (run && string.IsNullOrWhiteSpace(filePath))
                {
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Run triggered but no file path provided");
                    status += " - No save path";
                }
            }
            else if (imageGoo != null)
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Input is not a valid bitmap image");
                status = "Invalid image format";
                this.SetDisplayBitmap(null);
            }
            else
            {
                this.SetDisplayBitmap(null);
            }

            // Set outputs
            DA.SetData(0, status);
            DA.SetData(1, savedPath);
        }

        /// <summary>
        /// Gets the appropriate image format based on file extension.
        /// </summary>
        /// <param name="extension">File extension (with or without dot).</param>
        /// <returns>System.Drawing.Imaging.ImageFormat.</returns>
        private static System.Drawing.Imaging.ImageFormat GetImageFormat(string extension)
        {
            switch (extension.TrimStart('.').ToLowerInvariant())
            {
                case "png":
                    return System.Drawing.Imaging.ImageFormat.Png;
                case "jpg":
                case "jpeg":
                    return System.Drawing.Imaging.ImageFormat.Jpeg;
                case "bmp":
                    return System.Drawing.Imaging.ImageFormat.Bmp;
                case "gif":
                    return System.Drawing.Imaging.ImageFormat.Gif;
                case "tiff":
                case "tif":
                    return System.Drawing.Imaging.ImageFormat.Tiff;
                default:
                    return System.Drawing.Imaging.ImageFormat.Png; // Default to PNG
            }
        }

        /// <summary>
        /// Gets the current display bitmap.
        /// </summary>
        /// <returns>The bitmap currently being displayed, or null if none.</returns>
        public Bitmap GetDisplayBitmap()
        {
            lock (this._bitmapLock)
            {
                return this._displayBitmap?.Clone() as Bitmap;
            }
        }

        /// <summary>
        /// Sets the display bitmap.
        /// </summary>
        /// <param name="bitmap">The bitmap to display.</param>
        internal void SetDisplayBitmap(Bitmap bitmap)
        {
            lock (this._bitmapLock)
            {
                this._displayBitmap?.Dispose();
                this._displayBitmap = bitmap?.Clone() as Bitmap;
            }

            // Update the display
            this.OnDisplayExpired(false);
        }
    }
}
