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
using System.Collections.Generic;

namespace SmartHopper.Infrastructure.Mcp
{
    /// <summary>
    /// Tracks instance GUIDs that the user has explicitly locked from MCP-driven
    /// canvas mutations. This is separate from <see cref="ICanvasProtectedComponent"/>
    /// implementations that protect themselves dynamically (e.g. the SmartHopper MCP
    /// server while enabled).
    /// </summary>
    public static class McpCanvasLockState
    {
        private static readonly HashSet<Guid> LockedGuids = new();

        /// <summary>
        /// Raised whenever a GUID is locked or unlocked.
        /// </summary>
        public static event EventHandler? LockChanged;

        /// <summary>
        /// Locks the given instance GUID from MCP mutations.
        /// </summary>
        public static void Lock(Guid instanceGuid)
        {
            if (instanceGuid == Guid.Empty)
            {
                return;
            }

            bool added;
            lock (LockedGuids)
            {
                added = LockedGuids.Add(instanceGuid);
            }

            if (added)
            {
                LockChanged?.Invoke(null, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Unlocks the given instance GUID.
        /// </summary>
        public static void Unlock(Guid instanceGuid)
        {
            bool removed;
            lock (LockedGuids)
            {
                removed = LockedGuids.Remove(instanceGuid);
            }

            if (removed)
            {
                LockChanged?.Invoke(null, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Toggles the lock state of the given instance GUID and returns the new state.
        /// </summary>
        public static bool Toggle(Guid instanceGuid)
        {
            bool isLocked;
            lock (LockedGuids)
            {
                isLocked = LockedGuids.Contains(instanceGuid);
                if (isLocked)
                {
                    LockedGuids.Remove(instanceGuid);
                }
                else
                {
                    LockedGuids.Add(instanceGuid);
                }
            }

            LockChanged?.Invoke(null, EventArgs.Empty);
            return !isLocked;
        }

        /// <summary>
        /// Returns whether the given instance GUID is currently user-locked.
        /// </summary>
        public static bool IsLocked(Guid instanceGuid)
        {
            if (instanceGuid == Guid.Empty)
            {
                return false;
            }

            lock (LockedGuids)
            {
                return LockedGuids.Contains(instanceGuid);
            }
        }

        /// <summary>
        /// Gets a snapshot of the currently locked GUIDs.
        /// </summary>
        public static IReadOnlySet<Guid> GetLockedGuids()
        {
            lock (LockedGuids)
            {
                return new HashSet<Guid>(LockedGuids);
            }
        }
    }
}
