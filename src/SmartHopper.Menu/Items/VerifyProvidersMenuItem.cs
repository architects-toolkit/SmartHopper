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
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Rhino;
using SmartHopper.Infrastructure.AIProviders;
using SmartHopper.Infrastructure.Dialogs;
using SmartHopper.Infrastructure.Utils;

namespace SmartHopper.Menu.Items
{
    /// <summary>
    /// Creates and manages the Verify Providers menu item
    /// </summary>
    internal static class VerifyProvidersMenuItem
    {
        /// <summary>
        /// Creates a new Verify Providers menu item that triggers hash verification for all providers
        /// </summary>
        /// <returns>A ToolStripMenuItem configured to verify provider hashes</returns>
        public static ToolStripMenuItem Create()
        {
            var item = new ToolStripMenuItem("Verify Providers Hash");
            item.Click += async (sender, e) =>
            {
                await VerifyProvidersAsync();
            };
            return item;
        }

        private static async Task VerifyProvidersAsync()
        {
            try
            {
                // Get version for display
                string version = VersionHelper.GetDisplayVersion();
                string platform = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) 
                    ? "net7.0-windows" 
                    : "net7.0";

                RhinoApp.WriteLine($"[SmartHopper] Starting provider hash verification...");
                RhinoApp.WriteLine($"[SmartHopper] Version: {version}, Platform: {platform}");

                // Verify all providers using ProviderManager
                var results = await ProviderManager.Instance.VerifyAllProvidersAsync();

                if (results.Count == 0)
                {
                    RhinoApp.InvokeOnUiThread(new Action(() =>
                    {
                        StyledMessageDialog.ShowInfo(
                            $"No provider DLLs were found in the application directory.\n\n" +
                            "Place your SmartHopper.Providers.*.dll files in the same folder as the SmartHopper.Infrastructure.dll (or reinstall the providers) and run verification again.",
                            "Provider Verification - SmartHopper"
                        );
                    }));
                    return;
                }

                // Count results by status
                int matchCount = 0;
                int mismatchCount = 0;
                int unavailableCount = 0;
                int notFoundCount = 0;

                foreach (var result in results.Values)
                {
                    switch (result.Status)
                    {
                        case ProviderVerificationStatus.Match:
                            matchCount++;
                            break;
                        case ProviderVerificationStatus.Mismatch:
                            mismatchCount++;
                            break;
                        case ProviderVerificationStatus.Unavailable:
                            unavailableCount++;
                            break;
                        case ProviderVerificationStatus.NotFound:
                            notFoundCount++;
                            break;
                    }
                }

                // Log summary to console
                RhinoApp.WriteLine($"[SmartHopper] Verification complete: {matchCount} verified, {mismatchCount} mismatched, {unavailableCount} unavailable, {notFoundCount} not found");

                // Show specialized verification dialog with Eto.Forms styling
                RhinoApp.InvokeOnUiThread(new Action(() =>
                {
                    ProviderVerificationDialog.Show(results, version, platform, mismatchCount > 0);
                }));
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"[SmartHopper] Error during provider verification: {ex.Message}");
                RhinoApp.InvokeOnUiThread(new Action(() =>
                {
                    StyledMessageDialog.ShowError(
                        $"Failed to verify providers: {ex.Message}",
                        "Provider Verification - SmartHopper"
                    );
                }));
            }
        }
    }
}
