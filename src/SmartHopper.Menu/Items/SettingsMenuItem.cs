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
using System.Windows.Forms;
using SmartHopper.Infrastructure.Dialogs;
using SmartHopper.Menu.Dialogs;

namespace SmartHopper.Menu.Items
{
    internal static class SettingsMenuItem
    {
        /// <summary>
        /// Creates a new Settings menu item that shows the SettingsDialog when clicked
        /// </summary>
        /// <returns>A ToolStripMenuItem configured to show the settings dialog</returns>
        public static ToolStripMenuItem Create()
        {
            // Register the opener so Infrastructure UI (integrity report, assistant
            // no-provider errors) can deep-link into Settings without a backwards
            // project reference to SmartHopper.Menu. Registration is idempotent.
            SettingsLauncher.RegisterOpenSettings(tab =>
            {
                try
                {
                    var initialTab = string.Equals(tab, SettingsLauncher.SecurityTab, StringComparison.OrdinalIgnoreCase)
                        ? SettingsTab.Security
                        : SettingsTab.General;
                    var dialog = new SettingsDialog(initialTab);
                    dialog.ShowModalInFront(Rhino.UI.RhinoEtoApp.MainWindow);
                }
                catch (Exception ex)
                {
                    // Construction or display failures must surface: the click path
                    // runs inside message-loop dispatch where an escaping exception
                    // would be swallowed silently.
                    StyledMessageDialog.ShowError(
                        $"SmartHopper Settings failed to open: {ex.Message}",
                        "SmartHopper Settings");
                }
            });

            var item = new System.Windows.Forms.ToolStripMenuItem("Settings");
            item.Click += (sender, e) => SettingsLauncher.Open();
            return item;
        }
    }
}
