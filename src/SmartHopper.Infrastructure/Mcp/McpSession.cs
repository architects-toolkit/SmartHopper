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
using System.Threading;
using Newtonsoft.Json.Linq;

namespace SmartHopper.Infrastructure.Mcp
{
    /// <summary>
    /// One logical MCP client session, allocated by <c>initialize</c> and carried
    /// on every subsequent request through the <c>Mcp-Session-Id</c> header. A
    /// session scopes in-flight request ids (for <c>notifications/cancelled</c>)
    /// and mutation <c>requestId</c> reservations; it is not authentication —
    /// bearer checks still apply per request.
    /// </summary>
    internal sealed class McpSession
    {
        private long lastSeenTicks;
        private int activeOperations;

        internal McpSession(string id)
        {
            this.Id = id;
            this.CreatedUtc = DateTime.UtcNow;
            this.lastSeenTicks = this.CreatedUtc.Ticks;
        }

        /// <summary>Gets the opaque session identifier.</summary>
        public string Id { get; }

        /// <summary>Gets when the session was created.</summary>
        public DateTime CreatedUtc { get; }

        /// <summary>Gets the per-session in-flight JSON-RPC request registry.</summary>
        public McpRequestRegistry Requests { get; } = new McpRequestRegistry();

        /// <summary>Gets when the session was last used (UTC, relaxed atomic).</summary>
        public DateTime LastSeenUtc => new DateTime(Interlocked.Read(ref this.lastSeenTicks), DateTimeKind.Utc);

        /// <summary>
        /// Gets the number of owned operations (in-flight requests plus running
        /// mutation reservations) attached to this session. A session with
        /// active operations is never idle-evicted.
        /// </summary>
        public int ActiveOperations => this.Requests.InFlight + Interlocked.CompareExchange(ref this.activeOperations, 0, 0);

        /// <summary>Records activity.</summary>
        public void Touch()
        {
            Interlocked.Exchange(ref this.lastSeenTicks, DateTime.UtcNow.Ticks);
        }

        /// <summary>Increments the owned long-running operation count (mutation reservations).</summary>
        public void AddOwnedOperation()
        {
            Interlocked.Increment(ref this.activeOperations);
        }

        /// <summary>Decrements the owned long-running operation count.</summary>
        public void RemoveOwnedOperation()
        {
            Interlocked.Decrement(ref this.activeOperations);
        }
    }

    /// <summary>
    /// Per-session registry of in-flight JSON-RPC requests. Keys requests by
    /// their <em>typed</em> id — string <c>"1"</c> and numeric <c>1</c> are
    /// distinct — so <c>notifications/cancelled</c> cancels exactly the intended
    /// request inside its own session and can never reach another client's
    /// request. Concurrent duplicate ids are rejected; ids may be reused after
    /// completion.
    /// </summary>
    internal sealed class McpRequestRegistry
    {
        private readonly Dictionary<string, McpRequestScope> inFlight = new Dictionary<string, McpRequestScope>(StringComparer.Ordinal);
        private readonly object sync = new object();

        /// <summary>Gets the current in-flight request count.</summary>
        public int InFlight
        {
            get
            {
                lock (this.sync)
                {
                    return this.inFlight.Count;
                }
            }
        }

        /// <summary>
        /// Registers an in-flight request for <paramref name="id"/>.
        /// </summary>
        /// <param name="id">The JSON-RPC request id token.</param>
        /// <param name="linkedToken">Caller/server cancellation token to link.</param>
        /// <param name="scope">The registered scope; dispose it when the request completes.</param>
        /// <returns><c>false</c> when the same typed id is already in flight.</returns>
        public bool TryBegin(JToken id, CancellationToken linkedToken, out McpRequestScope? scope)
        {
            var key = Key(id);
            lock (this.sync)
            {
                if (this.inFlight.ContainsKey(key))
                {
                    scope = null;
                    return false;
                }

                var entry = new McpRequestScope(key, linkedToken, this);
                this.inFlight[key] = entry;
                scope = entry;
                return true;
            }
        }

        /// <summary>
        /// Requests cancellation of the in-flight request with the typed
        /// <paramref name="id"/>. Unknown or already-finished ids are harmless.
        /// </summary>
        /// <returns><c>true</c> when an in-flight request was found and cancelled.</returns>
        public bool Cancel(JToken? id)
        {
            if (id == null)
            {
                return false;
            }

            McpRequestScope? entry;
            var key = Key(id);
            lock (this.sync)
            {
                this.inFlight.TryGetValue(key, out entry);
            }

            if (entry == null)
            {
                return false;
            }

            entry.RequestCancel();
            return true;
        }

        internal void Complete(string key)
        {
            lock (this.sync)
            {
                this.inFlight.Remove(key);
            }
        }

        private static string Key(JToken id)
        {
            // Type tag first: "1" (string) and 1 (number) are distinct JSON-RPC ids.
            return id.Type.ToString() + "\n" + id.ToString(Newtonsoft.Json.Formatting.None);
        }
    }

    /// <summary>
    /// Handle for one registered in-flight request. Owns the linked cancellation
    /// source so <c>notifications/cancelled</c> can stop the matching request
    /// cooperatively. Disposing unregisters the id — it happens only after the
    /// real execution released its observers.
    /// </summary>
    internal sealed class McpRequestScope : IDisposable
    {
        private readonly string key;
        private readonly McpRequestRegistry owner;
        private readonly CancellationTokenSource cts;
        private int disposed;

        internal McpRequestScope(string key, CancellationToken linkedToken, McpRequestRegistry owner)
        {
            this.key = key;
            this.owner = owner;
            this.cts = CancellationTokenSource.CreateLinkedTokenSource(linkedToken);
        }

        /// <summary>Gets the cancellation token propagated into tool execution.</summary>
        public CancellationToken Token => this.cts.Token;

        /// <inheritdoc/>
        public void Dispose()
        {
            if (Interlocked.CompareExchange(ref this.disposed, 1, 0) != 0)
            {
                return;
            }

            this.owner.Complete(this.key);
            this.cts.Dispose();
        }

        /// <summary>Requests cooperative cancellation of this request.</summary>
        internal void RequestCancel()
        {
            try
            {
                this.cts.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // Completion raced the cancel — the request already ended.
            }
        }
    }
}
