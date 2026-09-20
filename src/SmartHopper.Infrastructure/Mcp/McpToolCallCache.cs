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
using System.Collections.Concurrent;
using System.Threading;
using Newtonsoft.Json.Linq;

namespace SmartHopper.Infrastructure.Mcp
{
    /// <summary>
    /// Short-lived in-memory cache that makes mutating MCP <c>tools/call</c> invocations
    /// idempotent. Callers attach an optional <c>requestId</c> argument; a call whose
    /// (tool name, requestId) pair was already served returns the cached result without
    /// re-executing the tool. Entries expire after <see cref="EntryLifetime"/>.
    /// </summary>
    /// <remarks>
    /// The cache is intentionally global: retrying the same requestId against any running
    /// server instance must not re-apply the mutation. Only the adapter consults it; other
    /// surfaces ignore <c>requestId</c>.
    /// </remarks>
    internal static class McpToolCallCache
    {
        private static readonly TimeSpan EntryLifetime = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(1);
        private static readonly ConcurrentDictionary<string, Entry> Entries = new ConcurrentDictionary<string, Entry>(StringComparer.Ordinal);
        private static long nextSweepTicks;

        /// <summary>
        /// Returns the cached result for (toolName, requestId) when present and not expired.
        /// </summary>
        public static bool TryGet(string toolName, string requestId, out McpToolCallResult? result)
        {
            result = null;
            if (string.IsNullOrWhiteSpace(toolName) || string.IsNullOrWhiteSpace(requestId))
            {
                return false;
            }

            var now = DateTime.UtcNow;
            SweepExpired(now);
            var key = Key(toolName, requestId);
            if (!Entries.TryGetValue(key, out var entry))
            {
                return false;
            }

            if (entry.ExpiresUtc <= now)
            {
                Entries.TryRemove(key, out _);
                return false;
            }

            result = Clone(entry.Result);
            return true;
        }

        /// <summary>
        /// Stores the result for (toolName, requestId) for <see cref="EntryLifetime"/>.
        /// </summary>
        public static void Store(string toolName, string requestId, McpToolCallResult result)
        {
            if (string.IsNullOrWhiteSpace(toolName) || string.IsNullOrWhiteSpace(requestId) || result == null)
            {
                return;
            }

            var now = DateTime.UtcNow;
            Entries[Key(toolName, requestId)] = new Entry(Clone(result), now.Add(EntryLifetime));
            SweepExpired(now);
        }

        /// <summary>Removes every cached entry. For test use.</summary>
        internal static void Clear()
        {
            Entries.Clear();
        }

        private static string Key(string toolName, string requestId)
        {
            return toolName + "\n" + requestId;
        }

        private static void SweepExpired(DateTime now)
        {
            var observed = Interlocked.Read(ref nextSweepTicks);
            if (now.Ticks < observed
                || Interlocked.CompareExchange(ref nextSweepTicks, now.Add(SweepInterval).Ticks, observed) != observed)
            {
                return;
            }

            foreach (var pair in Entries)
            {
                if (pair.Value.ExpiresUtc <= now)
                {
                    Entries.TryRemove(pair.Key, out _);
                }
            }
        }

        private static McpToolCallResult Clone(McpToolCallResult result)
        {
            var payload = result.Payload?.DeepClone();
            return result.IsError
                ? McpToolCallResult.Error(result.ErrorMessage ?? string.Empty, payload as JObject)
                : McpToolCallResult.Ok(payload ?? new JObject());
        }

        private sealed class Entry
        {
            public Entry(McpToolCallResult result, DateTime expiresUtc)
            {
                this.Result = result;
                this.ExpiresUtc = expiresUtc;
            }

            public McpToolCallResult Result { get; }

            public DateTime ExpiresUtc { get; }
        }
    }
}
