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
using System.Runtime.InteropServices;
using Eto.Forms;
using Rhino.UI;

namespace SmartHopper.Infrastructure.Dialogs
{
    /// <summary>
    /// Shared foreground policy for SmartHopper Eto windows. Owned dialogs resurface together
    /// with the Rhino main window when the user clicks the Rhino taskbar icon; when no owner
    /// is available the dialog stays in the taskbar and topmost instead of becoming an
    /// invisible window that keeps a modal workflow blocked behind other windows.
    /// </summary>
    public static class DialogForeground
    {
        /// <summary>
        /// Shows a modal dialog owned by the Rhino main window when available so it is
        /// restored with Rhino; otherwise keeps it visible in the taskbar and topmost.
        /// When the owner exists but the Rhino process is not in the foreground (minimized,
        /// covered, or on another virtual desktop), an owned modal would be pinned to
        /// Rhino's hidden window — so the dialog falls back to the unowned, taskbar-visible,
        /// topmost path where the user can actually see and answer it.
        /// </summary>
        /// <param name="dialog">The dialog to show.</param>
        /// <param name="owner">Optional explicit owner. Falls back to the Rhino main window.</param>
        public static void ShowModalInFront(this Dialog dialog, Window? owner = null)
        {
            var effectiveOwner = owner ?? RhinoEtoApp.MainWindow;
            if (effectiveOwner != null && HostProcessIsForeground())
            {
                dialog.ShowInTaskbar = false;
                dialog.ShowModal(effectiveOwner);
            }
            else
            {
                dialog.ShowInTaskbar = true;
                dialog.Topmost = true;
                dialog.ShowModal();
            }
        }

        /// <summary>
        /// Brings a modeless window to the foreground and focuses it, mirroring the chat
        /// dialog's bring-to-front behavior. When no owner is set the window stays visible
        /// in the taskbar so it cannot be lost behind other windows.
        /// </summary>
        /// <param name="window">The window to bring forward.</param>
        public static void BringToFrontAndFocus(this Form window)
        {
            if (window == null)
            {
                return;
            }

            try
            {
                if (window.Owner == null)
                {
                    window.ShowInTaskbar = true;
                }

                if (!window.Visible)
                {
                    window.Show();
                }
                else
                {
                    window.BringToFront();
                }

                window.Focus();
            }
            catch
            {
                // Foreground placement is best-effort; never break the calling workflow.
            }
        }

        /// <summary>
        /// Returns true when the current foreground window belongs to this process.
        /// Everything Rhino-hosted (Rhino, Grasshopper, Eto) shares the Rhino.exe process,
        /// so a matching PID means some Rhino UI is already in front of the user. Windows
        /// only; other platforms assume foreground so owned-modal behavior is unchanged.
        /// </summary>
        private static bool HostProcessIsForeground()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return true;
            }

            try
            {
                var hwnd = GetForegroundWindow();
                if (hwnd == IntPtr.Zero)
                {
                    return false;
                }

                GetWindowThreadProcessId(hwnd, out var foregroundPid);
                return foregroundPid == Process.GetCurrentProcess().Id;
            }
            catch
            {
                return true;
            }
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    }
}
