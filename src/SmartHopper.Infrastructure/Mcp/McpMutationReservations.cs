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
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace SmartHopper.Infrastructure.Mcp
{
    /// <summary>
    /// Atomic in-flight reservation registry for mutating MCP <c>tools/call</c>
    /// invocations. A <c>requestId</c> reserves its operation <em>before</em>
    /// execution; a retry with the same key joins or replays the recorded outcome
    /// — it can never start a second review, paid generation or commit.
    /// </summary>
    /// <remarks>
    /// Keys are scoped to one server generation and one session — a server
    /// restart ends the replay guarantee (documented tightening; the operation
    /// itself may still have applied). Entries are bound to the document identity
    /// resolved at reservation time, so a retry cannot drift onto another
    /// document. Completed outcomes are retained briefly so connection-loss
    /// retries can fetch the real terminal result; running entries are never
    /// evicted.
    /// </remarks>
    internal sealed class McpMutationReservations
    {
        /// <summary>Maximum completed entries retained per session.</summary>
        internal const int MaxCompletedPerSession = 512;

        /// <summary>How long a completed entry stays replayable.</summary>
        internal static readonly TimeSpan CompletedLifetime = TimeSpan.FromMinutes(10);

        private readonly object sync = new object();
        private readonly Func<DateTime> utcNow;

        /// <summary>
        /// Full entries keyed by (session, tool, requestId, document binding). The
        /// binding is part of the key so the same retry intent can never produce
        /// two document-scoped operations.
        /// </summary>
        private readonly Dictionary<EntryKey, Reservation> entries = new Dictionary<EntryKey, Reservation>();

        /// <summary>
        /// First-reserved document binding per (session, tool, requestId). Lets a
        /// retry find its operation without needing the (possibly busy) UI to
        /// resolve the ambient document again, and detects binding drift.
        /// </summary>
        private readonly Dictionary<BindingKey, string> bindings = new Dictionary<BindingKey, string>();

        internal McpMutationReservations()
            : this(() => DateTime.UtcNow)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="McpMutationReservations"/> class
        /// with an injectable clock for tests.
        /// </summary>
        internal McpMutationReservations(Func<DateTime> utcNow)
        {
            this.utcNow = utcNow ?? (() => DateTime.UtcNow);
        }

        /// <summary>
        /// Canonical fingerprint of business arguments: object properties sorted,
        /// array order and scalar types preserved. <c>requestId</c> is transport
        /// metadata and must be stripped by the caller before fingerprinting.
        /// </summary>
        internal static string Fingerprint(JObject? arguments)
        {
            if (arguments == null)
            {
                return string.Empty;
            }

            var builder = new StringBuilder(arguments.Count * 16);
            WriteCanonical(arguments, builder);
            return builder.ToString();
        }

        private static void WriteCanonical(JToken token, StringBuilder builder)
        {
            switch (token)
            {
                case JObject obj:
                    builder.Append('{');
                    var first = true;
                    foreach (var property in obj.Properties().OrderBy(p => p.Name, StringComparer.Ordinal))
                    {
                        if (!first)
                        {
                            builder.Append(',');
                        }

                        first = false;
                        builder.Append('"').Append(property.Name).Append("\":");
                        WriteCanonical(property.Value, builder);
                    }

                    builder.Append('}');
                    break;
                case JArray array:
                    builder.Append('[');
                    for (var i = 0; i < array.Count; i++)
                    {
                        if (i > 0)
                        {
                            builder.Append(',');
                        }

                        WriteCanonical(array[i], builder);
                    }

                    builder.Append(']');
                    break;
                default:
                    builder.Append(token.ToString(Newtonsoft.Json.Formatting.None));
                    break;
            }
        }

        /// <summary>Result of <see cref="TryReserve"/>.</summary>
        internal enum Attempt
        {
            /// <summary>The caller owns a new reservation and must execute then complete it.</summary>
            Owned,

            /// <summary>An existing entry was found; the caller may join/observe it.</summary>
            Joined,

            /// <summary>The same key was reserved with different arguments (<c>idempotency_conflict</c>).</summary>
            Conflict,

            /// <summary>The recorded document binding differs from the retry's resolution (<c>document_changed</c>).</summary>
            DocumentChanged,
        }

        /// <summary>
        /// Atomically reserves (session, tool, requestId). The first caller wins
        /// ownership; retries join or replay. The recorded fingerprint guards
        /// argument drift and the first-reserved document binding guards target
        /// drift: a retry resolved against a different document gets
        /// <see cref="Attempt.DocumentChanged"/> instead of a second
        /// document-scoped operation, while a retry that still resolves to the
        /// recorded binding joins or replays the recorded outcome.
        /// </summary>
        /// <param name="session">Owning session (required).</param>
        /// <param name="toolName">Mutating tool name.</param>
        /// <param name="requestId">Caller-supplied idempotency key.</param>
        /// <param name="fingerprint">Canonical argument fingerprint (requestId stripped).</param>
        /// <param name="documentBinding">
        /// Document identity resolved for this call, or <c>null</c> when resolution
        /// was unavailable — retries then rely on the recorded binding.
        /// </param>
        /// <param name="entry">The existing or newly-owned reservation.</param>
        internal Attempt TryReserve(
            McpSession session,
            string toolName,
            string requestId,
            string fingerprint,
            string? documentBinding,
            out Reservation? entry)
        {
            var bindingKey = new BindingKey(session.Id, toolName, requestId);
            lock (this.sync)
            {
                this.SweepExpired(session.Id);

                // The recorded binding wins when it exists — including the empty
                // marker for an unresolved first resolution — so a retry can find
                // its entry without needing the (possibly busy) UI again.
                var hasRecorded = this.bindings.TryGetValue(bindingKey, out var recorded);
                if (hasRecorded
                    && recorded!.Length > 0
                    && documentBinding != null
                    && !string.Equals(recorded, documentBinding, StringComparison.Ordinal))
                {
                    entry = null;
                    return Attempt.DocumentChanged;
                }

                var effectiveBinding = hasRecorded ? recorded! : (documentBinding ?? string.Empty);
                var entryKey = new EntryKey(session.Id, toolName, requestId, effectiveBinding);
                if (this.entries.TryGetValue(entryKey, out var existing))
                {
                    entry = existing;
                    return string.Equals(existing.Fingerprint, fingerprint, StringComparison.Ordinal)
                        ? Attempt.Joined
                        : Attempt.Conflict;
                }

                var created = new Reservation(this.utcNow(), fingerprint);
                this.entries[entryKey] = created;
                this.bindings[bindingKey] = effectiveBinding;
                session.AddOwnedOperation();
                entry = created;
                return Attempt.Owned;
            }
        }

        /// <summary>
        /// Completes an owned reservation with its real terminal result (success,
        /// refusal, validation failure, compensated or partial failure — all are
        /// terminal outcomes for the key).
        /// </summary>
        internal void Complete(
            McpSession session,
            Reservation reservation,
            McpToolCallResult result)
        {
            lock (this.sync)
            {
                reservation.Complete(result, this.utcNow() + CompletedLifetime);
                session.RemoveOwnedOperation();
                this.EnforceSessionCap(session.Id);
            }
        }

        /// <summary>
        /// Removes every entry. For tests and server shutdown; running entries keep
        /// their completion task so observers can still observe the real outcome.
        /// </summary>
        internal void Clear()
        {
            lock (this.sync)
            {
                this.entries.Clear();
                this.bindings.Clear();
            }
        }

        /// <summary>Expires completed entries past their lifetime; running entries are never evicted.</summary>
        private void SweepExpired(string sessionId)
        {
            var now = this.utcNow();
            var expired = new List<EntryKey>();
            foreach (var pair in this.entries)
            {
                if (pair.Key.SessionId == sessionId
                    && !pair.Value.IsRunning
                    && pair.Value.ExpiresUtc <= now)
                {
                    expired.Add(pair.Key);
                }
            }

            foreach (var entryKey in expired)
            {
                this.entries.Remove(entryKey);
            }

            // Drop recorded bindings whose entries all expired.
            var deadBindings = this.bindings.Keys
                .Where(b => b.SessionId == sessionId
                    && !this.entries.Keys.Any(e => e.SessionId == b.SessionId && e.ToolName == b.ToolName && e.RequestId == b.RequestId))
                .ToList();
            foreach (var key in deadBindings)
            {
                this.bindings.Remove(key);
            }
        }

        /// <summary>
        /// Bounds completed entries per session. Running entries are exempt and
        /// never count against the cap.
        /// </summary>
        private void EnforceSessionCap(string sessionId)
        {
            var completed = this.entries
                .Where(p => p.Key.SessionId == sessionId && !p.Value.IsRunning)
                .OrderBy(p => p.Value.ExpiresUtc)
                .ToList();
            var excess = completed.Count - MaxCompletedPerSession;
            for (var i = 0; i < excess; i++)
            {
                var entryKey = completed[i].Key;
                this.entries.Remove(entryKey);

                var bindingKey = new BindingKey(entryKey.SessionId, entryKey.ToolName, entryKey.RequestId);
                if (!this.entries.Keys.Any(e => e.SessionId == bindingKey.SessionId && e.ToolName == bindingKey.ToolName && e.RequestId == bindingKey.RequestId))
                {
                    this.bindings.Remove(bindingKey);
                }
            }
        }

        /// <summary>(session, tool, requestId) — the binding lookup key.</summary>
        private readonly struct BindingKey : IEquatable<BindingKey>
        {
            internal readonly string SessionId;
            internal readonly string ToolName;
            internal readonly string RequestId;

            internal BindingKey(string sessionId, string toolName, string requestId)
            {
                this.SessionId = sessionId;
                this.ToolName = toolName;
                this.RequestId = requestId;
            }

            public bool Equals(BindingKey other)
            {
                return this.SessionId == other.SessionId
                    && this.ToolName == other.ToolName
                    && this.RequestId == other.RequestId;
            }

            public override bool Equals(object? obj)
            {
                return obj is BindingKey other && this.Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(this.SessionId, this.ToolName, this.RequestId);
            }
        }

        /// <summary>(session, tool, requestId, document binding) — the full operation key.</summary>
        private readonly struct EntryKey : IEquatable<EntryKey>
        {
            internal readonly string SessionId;
            internal readonly string ToolName;
            internal readonly string RequestId;
            internal readonly string Binding;

            internal EntryKey(string sessionId, string toolName, string requestId, string binding)
            {
                this.SessionId = sessionId;
                this.ToolName = toolName;
                this.RequestId = requestId;
                this.Binding = binding;
            }

            public bool Equals(EntryKey other)
            {
                return this.SessionId == other.SessionId
                    && this.ToolName == other.ToolName
                    && this.RequestId == other.RequestId
                    && this.Binding == other.Binding;
            }

            public override bool Equals(object? obj)
            {
                return obj is EntryKey other && this.Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(this.SessionId, this.ToolName, this.RequestId, this.Binding);
            }
        }

        /// <summary>
        /// One reserved operation: its fingerprint, document binding and owned
        /// completion task. The task completes once with the real terminal result;
        /// observers awaiting it get the cloned outcome.
        /// </summary>
        internal sealed class Reservation
        {
            private readonly TaskCompletionSource<McpToolCallResult> completion =
                new TaskCompletionSource<McpToolCallResult>(TaskCreationOptions.RunContinuationsAsynchronously);

            internal Reservation(DateTime createdUtc, string fingerprint)
            {
                this.CreatedUtc = createdUtc;
                this.Fingerprint = fingerprint;
            }

            /// <summary>Gets the operation identifier reported in <c>operation_in_progress</c> payloads.</summary>
            internal string OperationId { get; } = Guid.NewGuid().ToString("N");

            /// <summary>Gets the canonical argument fingerprint recorded at reservation.</summary>
            internal string Fingerprint { get; }

            /// <summary>Gets when the reservation was created.</summary>
            internal DateTime CreatedUtc { get; }

            /// <summary>Gets when a completed entry stops being replayable.</summary>
            internal DateTime ExpiresUtc { get; private set; } = DateTime.MaxValue;

            /// <summary>Gets a value indicating whether the operation is still running.</summary>
            internal bool IsRunning => !this.completion.Task.IsCompleted;

            /// <summary>Gets the owned completion task carrying the terminal result.</summary>
            internal Task<McpToolCallResult> Completion => this.completion.Task;

            /// <summary>
            /// Gets the recorded terminal result (only valid once <see cref="IsRunning"/> is false).
            /// </summary>
            internal McpToolCallResult? Result { get; private set; }

            /// <summary>Completes the reservation with the real terminal result.</summary>
            internal void Complete(McpToolCallResult result, DateTime expiresUtc)
            {
                this.Result = result;
                this.ExpiresUtc = expiresUtc;
                this.completion.TrySetResult(result);
            }
        }
    }
}
