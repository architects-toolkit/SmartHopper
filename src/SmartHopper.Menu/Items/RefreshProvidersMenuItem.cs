/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
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
