/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using SmartHopper.Infrastructure.Configuration;
using SmartHopper.Infrastructure.Managers;
using Rhino;

namespace SmartHopper.Infrastructure.Initialization
{
    /// <summary>
    /// Handles safe initialization of SmartHopper components to avoid circular dependencies
    /// </summary>
    public static class SmartHopperInitializer
    {
        private static bool _isInitialized = false;
        private static readonly object _lockObject = new object();

        /// <summary>
        /// Safely initializes the SmartHopper system in the correct order to avoid circular dependencies
        /// </summary>
        public static void Initialize()
        {
            if (_isInitialized) return;

            lock (_lockObject)
            {
                if (_isInitialized) return;
                
                try
                {
                    Debug.WriteLine("[SmartHopperInitializer] Starting initialization sequence");
                    
                    // Step 1: Load settings first (but don't refresh providers yet)
                    var settings = SmartHopperSettings.Instance;
                    Debug.WriteLine("[SmartHopperInitializer] Settings loaded");
                    
                    // Step 2: Access the ProviderManager to initialize it
                    var providerManager = ProviderManager.Instance;
                    Debug.WriteLine("[SmartHopperInitializer] Provider manager initialized");
                    
                    // Step 3: Now that both are initialized independently, refresh providers with settings
                    RhinoApp.InvokeOnUiThread(() => 
                    {
                        Debug.WriteLine("[SmartHopperInitializer] Refreshing providers on UI thread");
                        
                        // RefreshProviders will internally refresh settings once as providers are registered
                        providerManager.RefreshProviders();
                        
                        // No need to call settings.RefreshProvidersLocalStorage() again here
                        // as it's already done inside RefreshProviders
                        
                        Debug.WriteLine("[SmartHopperInitializer] Initialization complete");
                    });
                    
                    _isInitialized = true;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[SmartHopperInitializer] Initialization error: {ex.Message}");
                    Debug.WriteLine($"[SmartHopperInitializer] Stack trace: {ex.StackTrace}");
                }
            }
        }
        
        /// <summary>
        /// Force a reload of all settings and providers
        /// </summary>
        public static void Reinitialize()
        {
            Task.Run(() => {
                try 
                {
                    Debug.WriteLine("[SmartHopperInitializer] Reinitializing SmartHopper");
                    RhinoApp.InvokeOnUiThread(() => 
                    {
                        var providerManager = ProviderManager.Instance;
                        var settings = SmartHopperSettings.Instance;
                        
                        // Refresh providers
                        providerManager.RefreshProviders();
                        
                        // Apply settings to providers
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
