/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using System.Runtime.CompilerServices;

namespace SmartHopper.Core.UI
{
    internal static class CanvasButtonBootstrap
    {
        /// <summary>
        /// Module initializer to auto-run CanvasButton.EnsureInitialized at assembly load.
        /// </summary>
        [ModuleInitializer]
        public static void Init()
        {
            CanvasButton.EnsureInitialized();
        }
    }
}
