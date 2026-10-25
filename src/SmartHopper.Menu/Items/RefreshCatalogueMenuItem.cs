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
using Rhino;
using SmartHopper.Infrastructure.Dialogs;

namespace SmartHopper.Menu.Items
{
    /// <summary>
    /// Menu item that manually refreshes the signed model catalogue from
    /// models.smarthopper.xyz and reports progress in a dedicated dialog.
    /// </summary>
    internal static class RefreshCatalogueMenuItem
    {
        /// <summary>
        /// Creates a menu item that opens the catalogue refresh dialog.
        /// </summary>
        public static ToolStripMenuItem Create()
        {
            var item = new ToolStripMenuItem("Refresh Model Catalogue");
            item.Click += (sender, e) =>
            {
                RhinoApp.InvokeOnUiThread(new Action(() =>
                {
                    CatalogueRefreshDialog.Show();
                }));
            };
            return item;
        }
    }
}
