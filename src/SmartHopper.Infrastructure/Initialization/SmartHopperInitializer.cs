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
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Rhino;
using SmartHopper.Infrastructure.AIProviders;
using SmartHopper.Infrastructure.Settings;
using SmartHopper.Infrastructure.Utils;

namespace SmartHopper.Infrastructure.Initialization
{
    /// <summary>
    /// Handles safe initialization of SmartHopper components to avoid circular dependencies.
    /// </summary>
    public static class SmartHopperInitializer
    {
        private static readonly object LockObject = new();
        private static bool isInitialized;

        /// <summary>
        /// Safely initializes the SmartHopper system in the correct order to avoid circular dependencies.
        /// </summary>
        [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Prevent initializer from crashing Grasshopper")]
        public static void Initialize()
        {
            if (isInitialized)
            {
                return;
            }

            lock (LockObject)
            {
                if (isInitialized)
                {
                    return;
                }

                try
                {
                    Debug.WriteLine("[SmartHopperInitializer] Starting initialization sequence");

                    // Step 1: Load settings first (but don't refresh providers yet)
                    var settings = SmartHopperSettings.Instance;
                    Debug.WriteLine("[SmartHopperInitializer] Settings loaded");

                    var displayVersion = VersionHelper.GetDisplayVersion();
                    var fullVersion = VersionHelper.GetFullVersion();
                    Debug.WriteLine($"[SmartHopperInitializer] Version (display): {displayVersion}");
                    Debug.WriteLine($"[SmartHopperInitializer] Version (full): {fullVersion}");
                    RhinoApp.WriteLine($"Loading SmartHopper {displayVersion}");

                    // Step 2: Access the ProviderManager to initialize it
                    var providerManager = ProviderManager.Instance;
                    Debug.WriteLine("[SmartHopperInitializer] Provider manager initialized");

                    // Step 3: Now that both are initialized independently, refresh providers with settings
                    Debug.WriteLine("[SmartHopperInitializer] Refreshing providers asynchronously");

                    // Fire-and-forget async initialization outside the lock
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            // RefreshProvidersAsync will internally refresh settings once as providers are registered
                            await providerManager.RefreshProvidersAsync().ConfigureAwait(false);

                            // Step 4: Now that both settings and providers are fully initialized, run integrity check
                            // Run integrity check on UI thread as it may interact with Rhino/Grasshopper
                            RhinoApp.InvokeOnUiThread(() =>
                            {
                                settings.IntegrityCheck();
                                Debug.WriteLine("[SmartHopperInitializer] Settings integrity check completed");
                            });

                            Debug.WriteLine("[SmartHopperInitializer] Initialization complete");
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[SmartHopperInitializer] Async initialization error: {ex.Message}");
                            Debug.WriteLine($"[SmartHopperInitializer] Stack trace: {ex.StackTrace}");
                        }
                    });

                    isInitialized = true;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[SmartHopperInitializer] Initialization error: {ex.Message}");
                    Debug.WriteLine($"[SmartHopperInitializer] Stack trace: {ex.StackTrace}");
                }
            }
        }

        /// <summary>
        /// Force a reload of all settings and providers.
        /// </summary>
        [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Prevent initializer from crashing Grasshopper")]
        public static void Reinitialize()
        {
            Task.Run(async () =>
            {
                try
                {
                    Debug.WriteLine("[SmartHopperInitializer] Reinitializing SmartHopper");

                    var providerManager = ProviderManager.Instance;
                    var settings = SmartHopperSettings.Instance;

                    // Refresh providers asynchronously
                    await providerManager.RefreshProvidersAsync().ConfigureAwait(false);

                    // Apply settings to providers on UI thread
                    RhinoApp.InvokeOnUiThread(() =>
                    {
                        settings.RefreshProvidersLocalStorage();
                        Debug.WriteLine("[SmartHopperInitializer] Reinitialization complete");
                    });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[SmartHopperInitializer] Reinitialization error: {ex.Message}");
                }
            });
        }
    }
}
