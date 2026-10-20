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
using System.Windows.Forms;
using SmartHopper.Infrastructure.Dialogs;

namespace SmartHopper.Menu.Items
{
    /// <summary>
    /// Creates and manages the Visit Website menu item
    /// </summary>
    internal static class VisitWebsiteMenuItem
    {
        private const string WebsiteUrl = "https://smarthopper.xyz";

        /// <summary>
        /// Creates a new Visit Website menu item that opens the SmartHopper website when clicked
        /// </summary>
        /// <returns>A ToolStripMenuItem configured to open the SmartHopper website</returns>
        public static ToolStripMenuItem Create()
        {
            var item = new ToolStripMenuItem("Visit Website");
            item.Click += (sender, e) =>
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = WebsiteUrl,
                        UseShellExecute = true,
                    });
                }
                catch (Exception ex)
                {
                    StyledMessageDialog.ShowError($"Failed to open URL: {ex.Message}", "Error");
                }
            };
            return item;
        }
    }
}
