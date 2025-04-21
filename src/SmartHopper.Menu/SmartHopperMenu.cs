/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024 Marc Roca Musach
 * 
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using Grasshopper;
using Grasshopper.Kernel;
using SmartHopper.Menu.Items;
using System;

using System.Windows.Forms;

namespace SmartHopper.Menu
{
    public class SmartHopperMenu : GH_AssemblyPriority
    {
        private Timer _timer;

        public override GH_LoadingInstruction PriorityLoad()
        {
            // Start a timer to check for the editor
            _timer = new Timer();
            _timer.Interval = 1000; // Check every second
            _timer.Tick += Timer_Tick;
            _timer.Start();

            return GH_LoadingInstruction.Proceed;
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (Instances.DocumentEditor != null)
            {
                _timer.Stop();
                AddToMainMenu();
            }
        }

        private void AddToMainMenu()
        {
            try
            {
                var editor = Instances.DocumentEditor;
                if (editor?.MainMenuStrip != null)
                {
                    var mainMenu = editor.MainMenuStrip;
                    if (!MenuExists(mainMenu))
                    {
                        var menuItem = CreateMenuItem();
                        mainMenu.Items.Add(menuItem);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to add menu: {ex.Message}");
            }
        }

        private bool MenuExists(MenuStrip menuStrip)
        {
            foreach (ToolStripItem item in menuStrip.Items)
            {
                if (item.Text == "SmartHopper")
                    return true;
            }
            return false;
        }

        private ToolStripMenuItem CreateMenuItem()
        {
            var menu = new ToolStripMenuItem("SmartHopper");

            menu.DropDownItems.AddRange(new ToolStripItem[]
            {
                SettingsMenuItem.Create(),
                new ToolStripSeparator(),
                AboutMenuItem.Create()
            });

            return menu;
        }
    }
}
