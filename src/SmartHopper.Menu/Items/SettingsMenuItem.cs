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
 * along with this library; if not, write to the Free Software Foundation,
 * Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301, USA.
 */

using System.Windows.Forms;
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
            var item = new System.Windows.Forms.ToolStripMenuItem("Settings");
            item.Click += (sender, e) =>
            {
                var dialog = new SettingsDialog();
                dialog.ShowModal(Rhino.UI.RhinoEtoApp.MainWindow);
            };
            return item;
        }
    }
}
