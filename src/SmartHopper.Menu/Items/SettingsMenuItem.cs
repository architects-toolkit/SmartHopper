/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024 Marc Roca Musach
 * 
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using SmartHopper.Menu.Dialogs;

namespace SmartHopper.Menu.Items
{
    internal static class SettingsMenuItem
    {
        /// <summary>
        /// Creates a new Settings menu item that shows the SettingsDialog when clicked
        /// </summary>
        /// <returns>A ToolStripMenuItem configured to show the settings dialog</returns>
        public static System.Windows.Forms.ToolStripMenuItem Create()
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
