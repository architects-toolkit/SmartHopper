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
using SmartHopper.Infrastructure.Dialogs;
using SmartHopper.Infrastructure.Initialization;

namespace SmartHopper.Menu.Items
{
    internal static class RefreshProvidersMenuItem
    {
        /// <summary>
        /// Creates a menu item to manually refresh AI provider discovery.
        /// </summary>
        public static ToolStripMenuItem Create()
        {
            var item = new ToolStripMenuItem("Refresh Providers");
            item.Click += (sender, e) =>
            {
                // Use the new initializer to safely refresh everything
                SmartHopperInitializer.Reinitialize();

                StyledMessageDialog.ShowInfo("AI provider discovery and settings refresh has been triggered.", "SmartHopper");
            };
            return item;
        }
    }
}
