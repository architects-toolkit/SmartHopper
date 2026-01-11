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
 * along with this library; if not, write to the Free Software Foundation,
 * Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301, USA.
 */

/*
 * AIContextBootstrapper
 * Purpose: Centralized, idempotent initializer for context providers that should be globally available.
 * Currently registers SelectionContextProvider so it can be used by both AIChat and CanvasButton scenarios.
 */

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using SmartHopper.Infrastructure.AIContext;

namespace SmartHopper.Core.AIContext
{
    /// <summary>
    /// Centralized initializer for context providers that must be globally available.
    /// </summary>
    public static class AIContextBootstrapper
    {
        private static readonly object _lock = new object();
        private static bool _initialized;

        /// <summary>
        /// Module initializer to ensure providers are registered when the assembly loads.
        /// </summary>
        [ModuleInitializer]
        public static void Init()
        {
            EnsureInitialized();
        }

        /// <summary>
        /// Ensures global context providers are registered exactly once.
        /// Safe to call multiple times.
        /// </summary>
        public static void EnsureInitialized()
        {
            if (_initialized) return;
            lock (_lock)
            {
                if (_initialized) return;
                try
                {
                    // Register global context providers (idempotent by ProviderId in AIContextManager)
                    AIContextManager.RegisterProvider(new TimeContextProvider());
                    AIContextManager.RegisterProvider(new EnvironmentContextProvider());
                    AIContextManager.RegisterProvider(new FileContextProvider());
                    _initialized = true;
                    Debug.WriteLine("[AIContextBootstrapper] Context providers initialized");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[AIContextBootstrapper] Initialization error: {ex.Message}");
                }
            }
        }
    }
}
