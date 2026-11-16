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
using System.Security.Cryptography;

namespace SmartHopper.Infrastructure.Mcp
{
    /// <summary>
    /// Bounded in-memory registry of logical MCP sessions for one server
    /// generation. Sessions die with the server (a restart is a new generation
    /// and cannot replay), are evicted after an idle window, and are never
    /// evicted while they still own running operations. A session is a
    /// correlation scope, not an authentication credential.
    /// </summary>
    internal sealed class McpSessionRegistry
    {
        /// <summary>Maximum concurrent sessions per server generation.</summary>
        internal const int MaxSessions = 128;

        /// <summary>Idle lifetime after which an operation-free session may be evicted.</summary>
        internal static readonly TimeSpan IdleLifetime = TimeSpan.FromMinutes(30);

        private readonly Dictionary<string, McpSession> sessions = new Dictionary<string, McpSession>(StringComparer.Ordinal);
        private readonly object sync = new object();
        private readonly Func<DateTime> utcNow;

        internal McpSessionRegistry()
            : this(() => DateTime.UtcNow)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="McpSessionRegistry"/> class
        /// with an injectable clock for tests.
        /// </summary>
        internal McpSessionRegistry(Func<DateTime> utcNow)
        {
            this.utcNow = utcNow ?? (() => DateTime.UtcNow);
        }

        /// <summary>Gets the current session count.</summary>
        internal int Count
        {
            get
            {
                lock (this.sync)
                {
                    return this.sessions.Count;
                }
            }
        }

        /// <summary>
        /// Allocates a new session with an opaque cryptographically random id.
        /// Idle-expired sessions without active operations are swept first;
        /// excess creation is rejected rather than evicting owned work.
        /// </summary>
        /// <returns>The new session, or <c>null</c> when the registry is full.</returns>
        internal McpSession? TryCreate()
        {
            lock (this.sync)
            {
                this.SweepIdleExpired();
                if (this.sessions.Count >= MaxSessions)
                {
                    return null;
                }

                var id = NewSessionId();
                var session = new McpSession(id);
                this.sessions[id] = session;
                return session;
            }
        }

        /// <summary>
        /// Resolves a session by id and records activity on it. Unknown or
        /// expired-and-swept ids return <c>null</c>.
        /// </summary>
        internal McpSession? TryGet(string? sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                return null;
            }

            lock (this.sync)
            {
                this.SweepIdleExpired();
                if (!this.sessions.TryGetValue(sessionId!, out var session))
                {
                    return null;
                }

                session.Touch();
                return session;
            }
        }

        /// <summary>Returns whether the session id is currently registered.</summary>
        internal bool Contains(string? sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                return false;
            }

            lock (this.sync)
            {
                return this.sessions.ContainsKey(sessionId!);
            }
        }

        /// <summary>Removes every session. For tests and server shutdown.</summary>
        internal void Clear()
        {
            lock (this.sync)
            {
                this.sessions.Clear();
            }
        }

        private static string NewSessionId()
        {
            var bytes = new byte[16];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }

            return Convert.ToHexString(bytes).ToUpperInvariant();
        }

        /// <summary>
        /// Removes idle-expired sessions that own no active operations. Sessions
        /// with owned running work are retained regardless of idle time — evicting
        /// them would orphan the operation's identity.
        /// </summary>
        private void SweepIdleExpired()
        {
            var now = this.utcNow();
            var expired = new List<string>();
            foreach (var pair in this.sessions)
            {
                var session = pair.Value;
                if (session.ActiveOperations > 0)
                {
                    continue;
                }

                if (now - session.LastSeenUtc > IdleLifetime)
                {
                    expired.Add(pair.Key);
                }
            }

            foreach (var key in expired)
            {
                this.sessions.Remove(key);
            }
        }
    }
}
