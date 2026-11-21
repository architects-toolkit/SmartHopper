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
    /// must use a .png extension, must not cross directory links, and existing
    /// files are never overwritten.
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
        /// <returns>The normalized absolute path and its form relative to the export directory.</returns>
        public static (string FullPath, string RelativePath) ResolveExportPath(string savePath, string exportDirectory)
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

            // Ordinal on purpose: a '..' traversal that re-enters a case-variant
            // sibling (e.g. 'Screenshots' vs 'screenshots') must still be rejected
            // on case-sensitive volumes, where it would be a different directory.
            if (!fullPath.StartsWith(rootWithSeparator, StringComparison.Ordinal))
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

            return (fullPath, relative.Replace(Path.DirectorySeparatorChar, '/'));
        }

        /// <summary>
        /// Writes PNG bytes to the path resolved from <paramref name="savePath"/>,
        /// creating missing directories inside the export folder and refusing to
        /// overwrite an existing file or to cross directory links below the export
        /// root. Returns the written path relative to the export directory.
        /// </summary>
        /// <param name="pngBytes">Encoded PNG payload.</param>
        /// <param name="savePath">File name or relative path requested by the caller.</param>
        /// <param name="exportDirectory">Sandbox root the write must stay inside.</param>
        public static string SavePng(byte[] pngBytes, string savePath, string exportDirectory)
        {
            ArgumentNullException.ThrowIfNull(pngBytes);

            var (fullPath, relativePath) = ResolveExportPath(savePath, exportDirectory);
            EnsureNoLinkedDirectories(fullPath, exportDirectory);

            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Write to a sibling temp file, then move it into place: a failed or
            // partial write never leaves a truncated PNG at the target, which
            // would also block a same-name retry since existing files are kept.
            // The temp name is independent of the (possibly near-limit) target name.
            var tempPath = Path.Combine(directory!, "." + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                File.WriteAllBytes(tempPath, pngBytes);
                File.Move(tempPath, fullPath, overwrite: false);
            }
            catch (IOException ex) when (File.Exists(fullPath))
            {
                throw new InvalidOperationException(
                    $"Refusing to overwrite existing file '{fullPath}'. Choose a different savePath.",
                    ex);
            }
            finally
            {
                try
                {
                    if (File.Exists(tempPath))
                    {
                        File.Delete(tempPath);
                    }
                }
                catch
                {
                    // Best effort cleanup; the temp name is unique per call.
                }
            }

            return relativePath;
        }

        /// <summary>
        /// Writes the captured PNG to <paramref name="savePath"/> inside the default
        /// export folder when provided. Returns the written path relative to the
        /// export folder, or <see langword="null"/> when no savePath was given.
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

        /// <summary>
        /// Rejects <paramref name="fullPath"/> when any existing directory between
        /// the export root and the file is a reparse point (junction/symlink), so
        /// a link inside the export folder cannot redirect a write outside it.
        /// </summary>
        private static void EnsureNoLinkedDirectories(string fullPath, string exportDirectory)
        {
            var rootFull = Path.GetFullPath(exportDirectory);
            var relative = Path.GetRelativePath(rootFull, fullPath);
            var segments = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            var current = rootFull;
            for (int i = 0; i < segments.Length - 1; i++)
            {
                current = Path.Combine(current, segments[i]);
                if (!Directory.Exists(current))
                {
                    return;
                }

                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                {
                    throw new InvalidOperationException(
                        $"savePath crosses the linked directory '{current}'; writes through directory links are not allowed.");
                }
            }
        }
    }
}
