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
    using System.IO;

    /// <summary>
    /// Resolves and writes model-supplied screenshot save paths inside a dedicated
    /// export folder so AI tools can never write outside it. Because screenshot
    /// tools run without the consent flow used by mutating tools, all writes are
    /// sandboxed: the target must stay under <see cref="DefaultExportDirectory"/>,
    /// must use a .png extension, and existing files are never overwritten.
    /// </summary>
    public static class ScreenshotExportUtilities
    {
        /// <summary>
        /// Gets the folder where screenshot tools are allowed to write PNG files.
        /// </summary>
        public static string DefaultExportDirectory => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Grasshopper",
            "SmartHopper",
            "Screenshots");

        /// <summary>
        /// Resolves a model-supplied <paramref name="savePath"/> to an absolute path
        /// inside <paramref name="exportDirectory"/>. Relative subfolders are allowed;
        /// absolute paths and traversal that escapes the export folder are rejected,
        /// as are non-.png extensions and invalid file-name characters.
        /// </summary>
        /// <param name="savePath">File name or relative path requested by the caller.</param>
        /// <param name="exportDirectory">Sandbox root the result must stay inside.</param>
        /// <returns>The normalized absolute path inside the export directory.</returns>
        public static string ResolveExportPath(string savePath, string exportDirectory)
        {
            if (string.IsNullOrWhiteSpace(savePath))
            {
                throw new ArgumentException("savePath must not be empty.", nameof(savePath));
            }

            var trimmed = savePath.Trim();
            if (Path.IsPathRooted(trimmed))
            {
                throw new ArgumentException(
                    "savePath must be a file name or relative path inside the SmartHopper screenshot export folder, not an absolute path.",
                    nameof(savePath));
            }

            var rootFull = Path.GetFullPath(exportDirectory);
            var rootWithSeparator = rootFull.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            var fullPath = Path.GetFullPath(Path.Combine(rootWithSeparator, trimmed));

            var comparison = IsCaseInsensitiveFileSystem()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            if (!fullPath.StartsWith(rootWithSeparator, comparison))
            {
                throw new ArgumentException(
                    $"savePath '{savePath}' resolves outside the screenshot export folder '{rootFull}'.",
                    nameof(savePath));
            }

            var relative = fullPath.Substring(rootWithSeparator.Length);
            var segments = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            foreach (var segment in segments)
            {
                if (segment.Length == 0
                    || segment == "."
                    || segment == ".."
                    || segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                {
                    throw new ArgumentException(
                        $"savePath '{savePath}' contains an invalid path segment '{segment}'.",
                        nameof(savePath));
                }
            }

            var fileName = segments[segments.Length - 1];
            if (!string.Equals(Path.GetExtension(fileName), ".png", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    $"savePath '{savePath}' must end with a .png extension.",
                    nameof(savePath));
            }

            return fullPath;
        }

        /// <summary>
        /// Writes PNG bytes to the path resolved from <paramref name="savePath"/>,
        /// creating missing directories inside the export folder and refusing to
        /// overwrite an existing file. Returns the absolute path written.
        /// </summary>
        /// <param name="pngBytes">Encoded PNG payload.</param>
        /// <param name="savePath">File name or relative path requested by the caller.</param>
        /// <param name="exportDirectory">Sandbox root the write must stay inside.</param>
        public static string SavePng(byte[] pngBytes, string savePath, string exportDirectory)
        {
            ArgumentNullException.ThrowIfNull(pngBytes);

            var fullPath = ResolveExportPath(savePath, exportDirectory);
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            try
            {
                // FileMode.CreateNew fails when the file exists, so model-supplied
                // paths can never overwrite user files.
                using var stream = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                stream.Write(pngBytes, 0, pngBytes.Length);
            }
            catch (IOException ex) when (File.Exists(fullPath))
            {
                throw new InvalidOperationException(
                    $"Refusing to overwrite existing file '{fullPath}'. Choose a different savePath.",
                    ex);
            }

            return fullPath;
        }

        /// <summary>
        /// Writes the captured PNG to <paramref name="savePath"/> inside the default
        /// export folder when provided. Returns the absolute path written, or
        /// <see langword="null"/> when no savePath was given.
        /// </summary>
        public static string? SavePng(ImageCaptureResult capture, string? savePath)
        {
            if (string.IsNullOrWhiteSpace(savePath))
            {
                return null;
            }

            var bytes = Convert.FromBase64String(capture.ImageBase64);
            return SavePng(bytes, savePath, DefaultExportDirectory);
        }

        private static bool IsCaseInsensitiveFileSystem()
        {
            return OperatingSystem.IsWindows() || OperatingSystem.IsMacOS();
        }
    }
}
