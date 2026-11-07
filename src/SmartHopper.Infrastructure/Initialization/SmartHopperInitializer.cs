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
using SmartHopper.Infrastructure.Hosting;
using SmartHopper.Infrastructure.Settings;
using SmartHopper.Infrastructure.Utils;
using SmartHopper.ProviderSdk.AIProviders;
using SmartHopper.ProviderSdk.Catalogue;
using SmartHopper.ProviderSdk.Hosting;
using SmartHopper.ProviderSdk.Logging;
using SmartHopper.ProviderSdk.Settings;
using SmartHopper.ProviderSdk.Utils;
namespace SmartHopper.Infrastructure.Initialization
{
    /// <summary>
    /// Handles safe initialization of SmartHopper components to avoid circular dependencies.
    /// </summary>
    public static class SmartHopperInitializer
    {
        private static readonly object LockObject = new ();
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
                    Logger.Trace(typeof(SmartHopperInitializer).FullName, "[SmartHopperInitializer] Starting initialization sequence");

                    // Wire Provider SDK host adapters so SDK code paths delegate to SmartHopper infrastructure.
                    ProviderSdkHost.ProviderTrust = new SmartHopperProviderTrustHost();
                    ProviderSdkHost.ProviderRegistry = new SmartHopperProviderRegistryHost();
                    ProviderSdkHost.PolicyPipeline = new SmartHopperPolicyPipelineHost();
                    ProviderSdkHost.ContextProvider = new SmartHopperContextProviderHost();
                    ProviderSdkHost.ToolRegistry = new SmartHopperToolRegistryHost();
                    ProviderSdkHost.Diagnostics = new SmartHopperProviderDiagnosticsHost();
                    ProviderSdkHost.Logger = new SmartHopperLogger();
                    ProviderSdkHost.HttpClientFactory = new SmartHopperProviderHttpClientFactory();
                    ProviderSdkHost.SettingsStoreFactory = SmartHopperProviderSettingsStoreFactory.Create;

                    // Step 1: Load settings first (but don't refresh providers yet)
                    var settings = SmartHopperSettings.Instance;
                    Logger.Trace(typeof(SmartHopperInitializer).FullName, "[SmartHopperInitializer] Settings loaded");

                    var displayVersion = VersionHelper.GetDisplayVersion();
                    var fullVersion = VersionHelper.GetFullVersion();
                    Logger.Trace(typeof(SmartHopperInitializer).FullName, $"[SmartHopperInitializer] Version (display): {displayVersion}");
                    Logger.Trace(typeof(SmartHopperInitializer).FullName, $"[SmartHopperInitializer] Version (full): {fullVersion}");
                    RhinoApp.WriteLine($"Loading SmartHopper {displayVersion}");

                    // Step 2: Access the ProviderManager to initialize it
                    var providerManager = ProviderManager.Instance;
                    Logger.Trace(typeof(SmartHopperInitializer).FullName, "[SmartHopperInitializer] Provider manager initialized");

                    // Step 3: Now that both are initialized independently, refresh providers with settings
                    Logger.Trace(typeof(SmartHopperInitializer).FullName, "[SmartHopperInitializer] Refreshing providers asynchronously");

                    // Fire-and-forget async initialization outside the lock
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            // RefreshProvidersAsync will internally refresh settings once as providers are registered
                            await providerManager.RefreshProvidersAsync().ConfigureAwait(false);

                            // Catalogue override layer: apply the verified local cache, then fetch a
                            // newer signed catalogue when available. Never blocks startup beyond this
                            // background task and never throws — failures degrade to the seed lists.
                            await ModelCatalogueService.Instance.RefreshAsync().ConfigureAwait(false);

                            // Step 4: Migrate any secrets still stored with the legacy AES-CBC
                            // prefixes (SH02:, SH03:) to the current SH04: AES-GCM format. Runs on
                            // every platform; on macOS an unrecoverable SH02: value simply fails to
                            // decrypt, stays in place, and is still cleared by the menu warning flow.
                            settings.MigrateLegacySecretsToSH04();
                            Logger.Trace(typeof(SmartHopperInitializer).FullName, ("[SmartHopperInitializer] Legacy secrets to SH04: migration check completed");

                            // Step 5: Now that both settings and providers are fully initialized, run integrity check
                            // Run integrity check on UI thread as it may interact with Rhino/Grasshopper
                            RhinoApp.InvokeOnUiThread(() =>
                            {
                                settings.IntegrityCheck();
                                Logger.Trace(typeof(SmartHopperInitializer).FullName, "[SmartHopperInitializer] Settings integrity check completed");
                            });

                            Logger.Trace(typeof(SmartHopperInitializer).FullName, ("[SmartHopperInitializer] Initialization complete");
                        }
                        catch (Exception ex)
                        {
                            Logger.Exception(typeof(SmartHopperInitializer).FullName, ex, $"[SmartHopperInitializer] Async initialization error: {ex.Message}");
                            Logger.Exception(typeof(SmartHopperInitializer).FullName, ex, $"[SmartHopperInitializer] Stack trace: {ex.StackTrace}");
                        }
                    });

                    isInitialized = true;
                }
                catch (Exception ex)
                {
                    Logger.Exception(typeof(SmartHopperInitializer).FullName, ex, $"[SmartHopperInitializer] Initialization error: {ex.Message}");
                    Logger.Exception(typeof(SmartHopperInitializer).FullName, ex, $"[SmartHopperInitializer] Stack trace: {ex.StackTrace}");
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
                    Logger.Trace(typeof(SmartHopperInitializer).FullName, "[SmartHopperInitializer] Reinitializing SmartHopper");

                    var providerManager = ProviderManager.Instance;
                    var settings = SmartHopperSettings.Instance;

                    // Refresh providers asynchronously
                    await providerManager.RefreshProvidersAsync().ConfigureAwait(false);

                    // Provider re-registration repopulates the registry from embedded
                    // seed tables — force-apply the verified catalogue so it keeps
                    // authority over covered providers (works offline via the cache).
                    await ModelCatalogueService.Instance.RefreshAsync(force: true).ConfigureAwait(false);

                    // Apply settings to providers on UI thread
                    RhinoApp.InvokeOnUiThread(() =>
                    {
                        settings.RefreshProvidersLocalStorage();
                        Logger.Trace(typeof(SmartHopperInitializer).FullName, "[SmartHopperInitializer] Reinitialization complete");
                    });
                }
                catch (Exception ex)
                {
                    Logger.Exception(typeof(SmartHopperInitializer).FullName, ex, $"[SmartHopperInitializer] Reinitialization error: {ex.Message}");
                }
            });
        }
    }
}
