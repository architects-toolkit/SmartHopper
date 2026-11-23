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

namespace SmartHopper.Infrastructure.Dialogs
{
    /// <summary>
    /// Indirection that lets Infrastructure UI surface "open SmartHopper Settings"
    /// affordances without a backwards project reference to SmartHopper.Menu (which
    /// owns <c>SettingsDialog</c>). The Menu assembly registers the real opener when
    /// it builds the application menu. A request made before registration is kept
    /// pending and flushed by <see cref="RegisterOpenSettings"/>, so affordances
    /// built earlier (e.g. the startup integrity report) still work. Callers must
    /// be on the UI thread.
    /// </summary>
    internal static class SettingsLauncher
    {
        /// <summary>Tab selector that opens the Security tab (integrity + trust controls).</summary>
        internal const string SecurityTab = "Security";

        private static Action<string?>? _openSettings;
        private static bool _hasPending;
        private static string? _pendingTab;

        /// <summary>Registers the opener. Invoked once by SmartHopper.Menu at menu build.</summary>
        internal static void RegisterOpenSettings(Action<string?> openSettings)
        {
            _openSettings = openSettings;
            if (_hasPending)
            {
                _hasPending = false;
                var tab = _pendingTab;
                _pendingTab = null;
                InvokeSafely(openSettings, tab);
            }
        }

        /// <summary>True once the Menu assembly has registered an opener.</summary>
        internal static bool IsAvailable => _openSettings != null;

        /// <summary>
        /// Opens SmartHopper Settings, optionally focused on the named tab. When no
        /// opener is registered yet the request pends until registration. Opener
        /// failures are traced and swallowed: this method runs inside message-loop
        /// dispatch (menu clicks, deferred invokes) where an escaping exception
        /// would die silently, and this class must stay UI-free for headless
        /// contexts — visible error UX is the registered opener's responsibility.
        /// </summary>
        /// <param name="tab">Optional tab selector such as <see cref="SecurityTab"/>; unknown names open the default tab.</param>
        internal static void Open(string? tab = null)
        {
            var open = _openSettings;
            if (open == null)
            {
                _pendingTab = tab;
                _hasPending = true;
                return;
            }

            InvokeSafely(open, tab);
        }

        private static void InvokeSafely(Action<string?> open, string? tab)
        {
            try
            {
                open(tab);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SmartHopper] Settings open failed: {ex}");
            }
        }

        /// <summary>Clears the registered opener and any pending request. Test-isolation seam for unit tests.</summary>
        internal static void ResetForTests()
        {
            _openSettings = null;
            _pendingTab = null;
            _hasPending = false;
        }
    }
}
