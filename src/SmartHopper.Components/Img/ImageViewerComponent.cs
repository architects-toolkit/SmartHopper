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

        // Execution throttling fields
        private DateTime _lastSaveTime = DateTime.MinValue;
        private string _lastSavedPath = string.Empty;
        private readonly TimeSpan _minSaveInterval = TimeSpan.FromMilliseconds(500); // Minimum 500ms between saves

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("F8E7D6C5-B4A3-9281-7065-43E1F2A9B8C7");

        /// <summary>
        /// Gets the icon for this component.
        /// </summary>
        protected override Bitmap Icon => Resources.imgviewer;

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
            pManager.AddTextParameter("File Path", "F", "File path to save the image.\nAdd one of the compatible extensions to the file name: .png, .jpg, .bmp, .gif or .tiff", GH_ParamAccess.item, string.Empty);
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

                // Handle saving if requested and path is provided
                if (run && !string.IsNullOrWhiteSpace(filePath))
                {
                    // Execution throttling: prevent rapid successive saves
                    var currentTime = DateTime.Now;
                    var timeSinceLastSave = currentTime - this._lastSaveTime;
                    var isSameFile = string.Equals(filePath, this._lastSavedPath, StringComparison.OrdinalIgnoreCase);

                    if (isSameFile && timeSinceLastSave < this._minSaveInterval)
                    {
                        this.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, $"Save throttled. Wait {(this._minSaveInterval - timeSinceLastSave).TotalMilliseconds:F0}ms before next save.");
                        return;
                    }

                    // Ensure directory exists with race condition handling
                    var directory = Path.GetDirectoryName(filePath);
                    if (!EnsureDirectoryExists(directory))
                    {
                        this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Failed to create directory: {directory}");
                        return;
                    }

                    // Check if target file is locked
                    if (IsFileLocked(filePath))
                    {
                        this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"File is currently locked by another process: {filePath}");
                        return;
                    }

                    // Use temp file strategy for safe saving
                    var tempFilePath = GetTempFilePath(filePath);
                    var extension = Path.GetExtension(filePath).ToLowerInvariant();
                    var format = GetImageFormat(extension);

                    try
                    {
                        // Save to temp file first
                        using (var clone = bitmap.Clone() as Bitmap)
                        {
                            if (clone == null)
                            {
                                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Failed to clone bitmap for saving");
                                return;
                            }

                            clone.Save(tempFilePath, format);
                        }

                        // Move temp file to final location (atomic operation)
                        if (File.Exists(filePath))
                        {
                            File.Delete(filePath);
                        }
                        File.Move(tempFilePath, filePath);

                        // Update throttling state
                        this._lastSaveTime = currentTime;
                        this._lastSavedPath = filePath;

                        this.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, $"Image saved successfully to: {filePath}");
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        this.CleanupTempFile(tempFilePath);
                        this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Access denied saving image: {ex.Message}. Check file permissions.");
                    }
                    catch (DirectoryNotFoundException ex)
                    {
                        this.CleanupTempFile(tempFilePath);
                        this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Directory not found: {ex.Message}");
                    }
                    catch (IOException ex)
                    {
                        this.CleanupTempFile(tempFilePath);
                        this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"I/O error saving image: {ex.Message}. File may be in use.");
                    }
                    catch (System.Runtime.InteropServices.ExternalException ex)
                    {
                        this.CleanupTempFile(tempFilePath);
                        this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"GDI+ error saving image: {ex.Message}. Check image format and file path.");
                    }
                    catch (Exception ex)
                    {
                        this.CleanupTempFile(tempFilePath);
                        this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Unexpected error saving image: {ex.Message}");
                    }
                }
                else if (run && string.IsNullOrWhiteSpace(filePath))
                {
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Run triggered but no file path provided");
                }
            }
            else if (imageGoo != null)
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Input is not a valid bitmap image");
                this.SetDisplayBitmap(null);
            }
            else
            {
                this.SetDisplayBitmap(null);
            }

            // Set outputs
        }

        /// <summary>
        /// Checks if a file is currently locked by another process.
        /// </summary>
        /// <param name="filePath">Path to the file to check.</param>
        /// <returns>True if file is locked, false otherwise.</returns>
        private static bool IsFileLocked(string filePath)
        {
            if (!File.Exists(filePath))
                return false;

            try
            {
                // succeeded ⇒ not locked
                using (var stream = File.Open(
                    filePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.None))
                {
                    return false;
                }
            }
            catch (IOException)
            {
                // open failed ⇒ locked
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                return true;
            }
        }

        /// <summary>
        /// Generates a safe temporary file path in the same directory as the target file.
        /// </summary>
        /// <param name="targetPath">The final target file path.</param>
        /// <returns>A temporary file path.</returns>
        private static string GetTempFilePath(string targetPath)
        {
            var directory = Path.GetDirectoryName(targetPath) ?? string.Empty;
            var fileName = Path.GetFileNameWithoutExtension(targetPath);
            var extension = Path.GetExtension(targetPath);
            var randomPart = Path.GetRandomFileName().Replace(".", "");
            var tempFileName = $"{fileName}_temp_{randomPart}{extension}";
            return Path.Combine(directory, tempFileName);
        }

        /// <summary>
        /// Safely creates directory with proper error handling for race conditions.
        /// </summary>
        /// <param name="directoryPath">Directory path to create.</param>
        /// <returns>True if directory exists or was created successfully.</returns>
        private static bool EnsureDirectoryExists(string directoryPath)
        {
            if (string.IsNullOrEmpty(directoryPath))
                return true;

            try
            {
                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }
                return true;
            }
            catch (IOException)
            {
                // Directory might have been created by another thread
                return Directory.Exists(directoryPath);
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        /// <summary>
        /// Safely cleans up a temporary file if it exists.
        /// </summary>
        /// <param name="tempFilePath">Path to the temporary file to clean up.</param>
        private void CleanupTempFile(string tempFilePath)
        {
            if (string.IsNullOrEmpty(tempFilePath))
            {
                return;
            }

            try
            {
                if (File.Exists(tempFilePath))
                {
                    File.Delete(tempFilePath);
                }
            }
            catch (Exception ex)
            {
                // Log cleanup failure but don't throw - this is cleanup code
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, 
                    $"Failed to cleanup temp file {tempFilePath}: {ex.Message}");
            }
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
