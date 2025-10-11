/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using System;
using System.Diagnostics;
using System.Windows.Forms;
using Grasshopper;
using Grasshopper.Kernel;
using SmartHopper.Infrastructure.Initialization;
using SmartHopper.Menu.Items;

namespace SmartHopper.Menu
{
    public class SmartHopperMenu : GH_AssemblyPriority
    {
        private Timer _timer;

        public override GH_LoadingInstruction PriorityLoad()
        {
            // Initialize the SmartHopper system before we do anything else
            Debug.WriteLine("SmartHopper initializing during assembly load");
            try
            {
                SmartHopperInitializer.Initialize();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error initializing SmartHopper: {ex.Message}");
            }

            // Start a timer to check for the editor
            this._timer = new Timer();
            this._timer.Interval = 1000; // Check every second
            this._timer.Tick += this.Timer_Tick;
            this._timer.Start();

            return GH_LoadingInstruction.Proceed;
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (Instances.DocumentEditor != null)
            {
                this._timer.Stop();
                this.AddToMainMenu();
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
                    if (!this.MenuExists(mainMenu))
                    {
                        var menuItem = this.CreateMenuItem();
                        mainMenu.Items.Add(menuItem);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to add menu: {ex.Message}");
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
                RefreshProvidersMenuItem.Create(),
                new ToolStripSeparator(),
                AboutMenuItem.Create(),
            });

            return menu;
        }
    }
}
