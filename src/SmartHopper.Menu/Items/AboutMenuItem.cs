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

using System.Reflection;
using System.Windows.Forms;
using SmartHopper.Menu.Dialogs;

namespace SmartHopper.Menu.Items
{
    /// <summary>
    /// Creates and manages the About menu item
    /// </summary>
    internal static class AboutMenuItem
    {
        /// <summary>
        /// Creates a new About menu item that shows the AboutDialog when clicked
        /// </summary>
        /// <returns>A ToolStripMenuItem configured to show the about dialog</returns>
        public static ToolStripMenuItem Create()
        {
            string version = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion ?? "Unknown";
            var item = new ToolStripMenuItem("About");
            item.Click += (sender, e) =>
            {
                var dialog = new AboutDialog(version);
                dialog.ShowModal(Rhino.UI.RhinoEtoApp.MainWindow);
            };
            return item;
        }
    }
}
