/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024 Marc Roca Musach
 * 
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using System.Windows.Forms;
using System.Reflection;

namespace SmartHopper.Menu.Items
{
    internal static class AboutMenuItem
    {
        public static ToolStripMenuItem Create()
        {
            var fullVersion = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "";
            var version = fullVersion.Split('+')[0]; // Take only the part before the '+'
            var item = new ToolStripMenuItem("About");
            item.Click += (sender, e) =>
            {
                MessageBox.Show(
                    "SmartHopper\n" +
                    "An AI-powered assistant for Grasshopper3D\n\n" +
                    $"Version: {version}\n" +
                    "Copyright © 2024 Marc Roca Musach\n" +
                    "Licensed under GNU Lesser General Public License v3.0\n" +
                    "Supported by: Architect's Toolkit (RKTK.tools) and the SmartHopper Community\n\n" +
                    "SmartHopper is an open-source project that implements third-party AI APIs to provide advanced features for Grasshopper.\n" +
                    "It currently supports MistralAI and OpenAI.\n\n" +
                    "KEEP IN MIND THAT SMARTHOPPER IS STILL IN ITS EARLY STAGES OF DEVELOPMENT AND MAY HAVE BUGS OR LIMITATIONS.\n" +
                    "To contact the developers or report bugs, feel free to open an issue on https://github.com/architects-toolkit/SmartHopper or visit https://rktk.tools",
                    "About SmartHopper",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
            };
            return item;
        }
    }
}
