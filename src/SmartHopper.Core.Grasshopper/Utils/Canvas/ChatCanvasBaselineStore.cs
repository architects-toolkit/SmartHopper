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

using System;
using System.Diagnostics;
using System.IO;

namespace SmartHopper.Core.Grasshopper.Utils.Canvas
{
    /// <summary>
    /// Disk-backed storage for one chat's canvas baseline (a compact GhJSON snapshot), so large
    /// snapshots do not stay in memory between turns. Files live under
    /// <c>%APPDATA%/Grasshopper/SmartHopper/ChatCanvasBaselines</c>. All operations are best-effort.
    /// </summary>
    public sealed class ChatCanvasBaselineStore
    {
        /// <summary>
        /// Baselines older than this are removed when a new store is created (left over from crashed sessions).
        /// </summary>
        public static readonly TimeSpan StaleAge = TimeSpan.FromDays(3);

        private const string Extension = ".ghjson";

        /// <summary>
        /// Initializes a new instance of the <see cref="ChatCanvasBaselineStore"/> class.
        /// </summary>
        /// <param name="folder">Folder that holds baseline files; defaults to <see cref="DefaultFolder"/>.</param>
        public ChatCanvasBaselineStore(string? folder = null)
        {
            this.Folder = folder ?? DefaultFolder;
            this.FilePath = Path.Combine(this.Folder, Guid.NewGuid().ToString("N") + Extension);
            PruneStale(this.Folder, StaleAge);
        }

        /// <summary>
        /// Gets the default baseline folder.
        /// </summary>
        public static string DefaultFolder => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Grasshopper",
            "SmartHopper",
            "ChatCanvasBaselines");

        /// <summary>
        /// Gets the folder that holds baseline files.
        /// </summary>
        public string Folder { get; }

        /// <summary>
        /// Gets the file used by this store.
        /// </summary>
        public string FilePath { get; }

        /// <summary>
        /// Writes the baseline JSON.
        /// </summary>
        /// <param name="json">The GhJSON snapshot.</param>
        /// <returns>True when written.</returns>
        public bool Save(string json)
        {
            try
            {
                Directory.CreateDirectory(this.Folder);
                File.WriteAllText(this.FilePath, json ?? string.Empty);
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ChatCanvasBaselineStore] Save failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Reads the baseline JSON.
        /// </summary>
        /// <returns>The stored JSON, or null when missing or unreadable.</returns>
        public string? Load()
        {
            try
            {
                return File.Exists(this.FilePath) ? File.ReadAllText(this.FilePath) : null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ChatCanvasBaselineStore] Load failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Deletes the baseline file.
        /// </summary>
        public void Delete()
        {
            try
            {
                if (File.Exists(this.FilePath))
                {
                    File.Delete(this.FilePath);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ChatCanvasBaselineStore] Delete failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Deletes baseline files in <paramref name="folder"/> last written more than <paramref name="maxAge"/> ago.
        /// </summary>
        /// <param name="folder">The baseline folder.</param>
        /// <param name="maxAge">Maximum age to keep.</param>
        public static void PruneStale(string folder, TimeSpan maxAge)
        {
            try
            {
                if (!Directory.Exists(folder))
                {
                    return;
                }

                var cutoff = DateTime.UtcNow - maxAge;
                foreach (var file in Directory.GetFiles(folder, "*" + Extension))
                {
                    try
                    {
                        if (File.GetLastWriteTimeUtc(file) < cutoff)
                        {
                            File.Delete(file);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[ChatCanvasBaselineStore] Prune failed for '{file}': {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ChatCanvasBaselineStore] Prune failed: {ex.Message}");
            }
        }
    }
}
