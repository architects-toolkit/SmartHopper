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
using System.Runtime.CompilerServices;
using Rhino;
using SmartHopper.Infrastructure.Settings;

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
            // Skip initialization when Grasshopper assemblies are unavailable (e.g., unit test runs).
            if (!IsGrasshopperRuntimeAvailable())
            {
                return;
            }

            // Only trigger initialization process if setting is enabled (defaults to true if unset)
            try
            {
                if (SmartHopperSettings.Instance?.SmartHopperAssistant?.EnableCanvasButton ?? true)
                {
                    CanvasButton.EnsureInitialized();
                }

                // Subscribe to settings saved to dynamically toggle the canvas button visibility
                SmartHopperSettings.Instance.SettingsSaved += OnSettingsSaved;
            }
            catch
            {
                // Be permissive on errors: initialize by default
                CanvasButton.EnsureInitialized();
            }
        }

        /// <summary>
        /// Detects whether Grasshopper runtime assemblies are available to avoid initializing UI elements during headless runs (e.g., unit tests).
        /// </summary>
        private static bool IsGrasshopperRuntimeAvailable()
        {
            try
            {
                return Type.GetType("Grasshopper.Instances, Grasshopper") != null;
            }
            catch
            {
                return false;
            }
        }

        private static void OnSettingsSaved(object? sender, System.EventArgs e)
        {
            // Always marshal to Rhino UI thread for UI operations
            RhinoApp.InvokeOnUiThread(() =>
            {
                CanvasButton.UpdateEnabledStateFromSettings();
            });
        }
    }
}
