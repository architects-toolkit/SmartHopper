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
#if WINDOWS
using System.Windows.Forms;
#else
using Eto.Forms;
#endif

namespace SmartHopper.Menu.Items
{
    internal static class SettingsMenuItem
    {
        /// <summary>
        /// Creates a new Settings menu item that shows the SettingsDialog when clicked
        /// </summary>
        /// <returns>A ToolStripMenuItem configured to show the settings dialog</returns>

#if WINDOWS
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
#else
        public static MenuItem Create()
        {
            var item = new MenuItem { Text = "Settings" };
            item.Click += (sender, e) => new SettingsDialog().ShowModal(Rhino.UI.RhinoEtoApp.MainWindow);
            return item;
        }
#endif
    }
}
