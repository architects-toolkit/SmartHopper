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
using System.Diagnostics;

namespace SmartHopper.Infrastructure.Mcp
{
    /// <summary>
    /// Ref-counted singleton manager for <see cref="McpServer"/> instances. Multiple
    /// components (potentially across multiple Grasshopper documents) can call
    /// <see cref="Acquire"/> with the same port; only the first acquisition starts
    /// the server and only the last <see cref="Release"/> stops it.
    /// </summary>
    /// <remarks>
    /// Configuration mutations after the first acquisition are not applied: the
    /// shared server keeps the options it was started with. Components should
    /// <see cref="Release"/> and re-<see cref="Acquire"/> when their inputs change.
    /// </remarks>
    public static class McpServerLifecycle
    {
        private static readonly object Sync = new object();
        private static readonly Dictionary<int, ServerSlot> Slots = new Dictionary<int, ServerSlot>();

        /// <summary>
        /// Acquires (and starts if necessary) the shared server for the given port.
        /// </summary>
        /// <param name="key">Caller key. Used so the same caller can re-acquire without double-counting.</param>
        /// <param name="options">Options used to start the server. Ignored on subsequent acquisitions.</param>
        /// <returns>The running <see cref="McpServer"/>.</returns>
        public static McpServer Acquire(object key, McpServerOptions options)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            lock (Sync)
            {
                if (!Slots.TryGetValue(options.Port, out var slot))
                {
                    var server = new McpServer(options.Clone());
                    server.Start();
                    slot = new ServerSlot(server);
                    Slots[options.Port] = slot;
                }

                slot.Holders.Add(key);
                return slot.Server;
            }
        }

        /// <summary>
        /// Releases a previous acquisition. The server stops when the last holder releases it.
        /// </summary>
        public static void Release(object key, int port)
        {
            if (key == null)
            {
                return;
            }

            lock (Sync)
            {
                if (!Slots.TryGetValue(port, out var slot))
                {
                    return;
                }

                slot.Holders.Remove(key);
                if (slot.Holders.Count > 0)
                {
                    return;
                }

                try
                {
                    slot.Server.Stop();
                    slot.Server.Dispose();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Mcp] Error disposing server on port {port}: {ex.Message}");
                }

                Slots.Remove(port);
            }
        }

        /// <summary>
        /// Returns the currently running server for a port, or <c>null</c>.
        /// </summary>
        public static McpServer? Find(int port)
        {
            lock (Sync)
            {
                return Slots.TryGetValue(port, out var slot) ? slot.Server : null;
            }
        }

        private sealed class ServerSlot
        {
            public ServerSlot(McpServer server)
            {
                this.Server = server;
            }

            public McpServer Server { get; }

            public HashSet<object> Holders { get; } = new HashSet<object>();
        }
    }
}
