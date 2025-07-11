/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
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
            var fullVersion = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "";
            var version = fullVersion.Split('+')[0]; // Take only the part before the '+'
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
