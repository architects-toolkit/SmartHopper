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
using System.Runtime.InteropServices;
using System.Windows.Forms;
using SmartHopper.Infrastructure.Diagnostics;
using SmartHopper.Infrastructure.Dialogs;
using SmartHopper.ProviderSdk.Logging;

namespace SmartHopper.Menu.Items
{
    /// <summary>
    /// Creates and manages the Diagnostics flyout menu and its sub-items.
    /// </summary>
    internal static class DiagnosticsMenuItem
    {
        /// <summary>
        /// Creates a new Diagnostics flyout menu.
        /// </summary>
        /// <returns>A ToolStripMenuItem configured with diagnostic actions.</returns>
        public static ToolStripMenuItem Create()
        {
            var menu = new ToolStripMenuItem("Diagnostics");

            var openLogFolder = new ToolStripMenuItem("Open Log Folder");
            openLogFolder.Click += (sender, e) => OpenLogFolder();

            // Only expose direct log folder access in Debug builds.
#if !DEBUG
            openLogFolder.Visible = false;
#endif

            var copyDiagnostics = new ToolStripMenuItem("Copy Diagnostics for GitHub");
            copyDiagnostics.Click += (sender, e) => CopyDiagnostics();

            var generateDiagnosticZip = new ToolStripMenuItem("Generate Diagnostic Zip");
            generateDiagnosticZip.Click += (sender, e) => GenerateDiagnosticZip();

            menu.DropDownItems.Add(openLogFolder);
            menu.DropDownItems.Add(copyDiagnostics);
            menu.DropDownItems.Add(generateDiagnosticZip);

            return menu;
        }

        private static void OpenLogFolder()
        {
            try
            {
                SmartHopperDiagnostics.OpenLogFolder();
            }
            catch (Exception ex)
            {
                Logger.Exception(typeof(DiagnosticsMenuItem).FullName ?? "SmartHopper.Menu.Diagnostics", ex, "Failed to open log folder");
                StyledMessageDialog.ShowError(
                    $"Could not open the log folder: {ex.Message}",
                    "Diagnostics - SmartHopper");
            }
        }

        private static void CopyDiagnostics()
        {
            try
            {
                var report = SmartHopperDiagnostics.GenerateReport();
                SmartHopperDiagnostics.CopyToClipboard(report);
                StyledMessageDialog.ShowInfo(
                    "Diagnostics report copied to the clipboard.",
                    "Diagnostics - SmartHopper");
            }
            catch (ExternalException ex) when (ex.ErrorCode == SmartHopperDiagnostics.ClipboardInUseErrorCode)
            {
                Logger.Exception(typeof(DiagnosticsMenuItem).FullName ?? "SmartHopper.Menu.Diagnostics", ex, "Failed to copy diagnostics");
                StyledMessageDialog.ShowError(
                    "The clipboard is in use by another application (e.g. Remote Desktop or a clipboard manager)." +
                    $"{Environment.NewLine}Wait a moment and try again, or use 'Generate Diagnostic Zip' instead.",
                    "Diagnostics - SmartHopper");
            }
            catch (Exception ex)
            {
                Logger.Exception(typeof(DiagnosticsMenuItem).FullName ?? "SmartHopper.Menu.Diagnostics", ex, "Failed to copy diagnostics");
                StyledMessageDialog.ShowError(
                    $"Could not copy diagnostics: {ex.Message}",
                    "Diagnostics - SmartHopper");
            }
        }

        private static void GenerateDiagnosticZip()
        {
            try
            {
                var path = SmartHopperDiagnostics.GenerateDiagnosticZip();
                StyledMessageDialog.ShowInfo(
                    $"Diagnostic zip created at:{Environment.NewLine}{Environment.NewLine}{path}",
                    "Diagnostics - SmartHopper");
            }
            catch (Exception ex)
            {
                Logger.Exception(typeof(DiagnosticsMenuItem).FullName ?? "SmartHopper.Menu.Diagnostics", ex, "Failed to generate diagnostic zip");
                StyledMessageDialog.ShowError(
                    $"Could not generate diagnostic zip: {ex.Message}",
                    "Diagnostics - SmartHopper");
            }
        }
    }
}
