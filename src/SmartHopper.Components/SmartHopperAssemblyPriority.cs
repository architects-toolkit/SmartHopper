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
using SmartHopper.Components.Properties;
using SmartHopper.Core.UI;
using SmartHopper.Infrastructure.Dialogs;

namespace SmartHopper.Components
{
    public class SmartHopperAssemblyPriority : GH_AssemblyPriority
    {
        public override GH_LoadingInstruction PriorityLoad()
        {
            // Add SmartHopper category icon and settings
            Instances.ComponentServer.AddCategoryIcon("SmartHopper", Resources.smarthopper);
            Instances.ComponentServer.AddCategoryShortName("SmartHopper", "SH");
            Instances.ComponentServer.AddCategorySymbolName("SmartHopper", 'S');

            // Register the canvas link callback for StyledMessageDialog
            StyledMessageDialog.RegisterCanvasLinkCallback = (dialog, guid, color) =>
            {
                DialogCanvasLink.RegisterLink(dialog, guid, color);
            };

            return GH_LoadingInstruction.Proceed;
        }
    }
}
