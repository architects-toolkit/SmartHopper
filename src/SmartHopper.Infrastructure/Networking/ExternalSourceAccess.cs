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
using System.Threading.Tasks;

namespace SmartHopper.Infrastructure.Networking
{
    /// <summary>A source access decision, independent of canvas mutation approvals.</summary>
    public enum ExternalSourceDecision
    {
        /// <summary>Do not retrieve from this origin.</summary>
        Deny,
        /// <summary>Allow this origin for the current retrieval/tool invocation only.</summary>
        AllowOnce,
        /// <summary>Persist approval for this exact origin.</summary>
        AlwaysAllow,
        /// <summary>The prompt elapsed without an answer; treated as a refusal, never a grant.</summary>
        NoResponse,
    }

    /// <summary>Applies explicit human approval to external data reads; no presenter means deny.</summary>
    public static class ExternalSourceAccess
    {
        private static readonly AsyncLocal<AccessScope?> CurrentScope = new AsyncLocal<AccessScope?>();
        private static readonly SemaphoreSlim PromptLock = new SemaphoreSlim(1, 1);
        private static int awaitingConsent;

        /// <summary>Gets a value indicating whether at least one consent prompt is open and unanswered.</summary>
        public static bool IsAwaitingConsent => Volatile.Read(ref awaitingConsent) > 0;

        /// <summary>Raised when <see cref="IsAwaitingConsent"/> transitions between false and true, so UI can repaint a waiting indicator.</summary>
        public static event EventHandler? AwaitingConsentChanged;

        /// <summary>Gets or sets the interactive host prompt. It receives only a canonical origin.</summary>
        public static Func<string, CancellationToken, Task<ExternalSourceDecision>>? PromptAsync { get; set; }

        /// <summary>Starts a logical retrieval; nested helpers share one-call approvals.</summary>
        public static IDisposable BeginScope()
        {
            var previous = CurrentScope.Value;
            var current = new AccessScope(previous);
            CurrentScope.Value = current;
            return current;
        }

        /// <summary>Authorizes an origin before issuing its request. No mutation-bypass policy applies.</summary>
        public static Task RequireAsync(Uri uri, CancellationToken cancellationToken = default) =>
            RequireAsync(uri, ExternalSourceAllowlist.Current, PromptAsync, cancellationToken);

        /// <summary>Authorizes against explicit dependencies for deterministic, network-free contract tests.</summary>
        internal static Task RequireAsync(
            Uri uri,
            ExternalSourceAllowlist allowlist,
            Func<string, CancellationToken, Task<ExternalSourceDecision>>? prompt,
            CancellationToken cancellationToken) =>
            RequireCoreAsync(uri, allowlist, prompt, CurrentScope.Value?.Grants, cancellationToken);

        /// <summary>Captures one-call grants for transport callbacks that suppress ExecutionContext flow.</summary>
        internal static Func<Uri, CancellationToken, Task> CaptureAuthorization()
        {
            var grants = CurrentScope.Value?.Grants;
            return (uri, token) => RequireCoreAsync(uri, ExternalSourceAllowlist.Current, PromptAsync, grants, token);
        }

        private static async Task RequireCoreAsync(
            Uri uri,
            ExternalSourceAllowlist allowlist,
            Func<string, CancellationToken, Task<ExternalSourceDecision>>? prompt,
            ConcurrentDictionary<string, ExternalSourceDecision>? grants,
            CancellationToken cancellationToken)
        {
            var origin = ExternalSourceAllowlist.Origin(uri);
            cancellationToken.ThrowIfCancellationRequested();
            if (allowlist.Contains(uri))
            {
                return;
            }

            await PromptLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (allowlist.Contains(uri))
                {
                    return;
                }

                if (grants != null && grants.TryGetValue(origin, out var previous))
                {
                    if (previous == ExternalSourceDecision.Deny)
                    {
                        throw new UnauthorizedAccessException($"Retrieval from {origin} was denied.");
                    }

                    return;
                }

                ExternalSourceDecision decision;
                if (prompt == null)
                {
                    decision = ExternalSourceDecision.Deny;
                }
                else
                {
                    EnterAwaitingConsent();
                    try
                    {
                        decision = await prompt(origin, cancellationToken).ConfigureAwait(false);
                    }
                    finally
                    {
                        ExitAwaitingConsent();
                    }
                }
                cancellationToken.ThrowIfCancellationRequested();
                if (decision == ExternalSourceDecision.AlwaysAllow)
                {
                    // A failed persistent write fails closed instead of pretending a grant was stored.
                    allowlist.Allow(uri);
                }
                else if (decision == ExternalSourceDecision.NoResponse)
                {
                    grants?.TryAdd(origin, ExternalSourceDecision.Deny);
                    throw new UnauthorizedAccessException(
                        $"Retrieval from {origin} requires your approval; the consent prompt was not answered.");
                }
                else if (decision != ExternalSourceDecision.AllowOnce)
                {
                    grants?.TryAdd(origin, ExternalSourceDecision.Deny);
                    throw new UnauthorizedAccessException($"Retrieval from {origin} requires your approval.");
                }
                else
                {
                    grants?.TryAdd(origin, decision);
                }
            }
            finally
            {
                PromptLock.Release();
            }
        }

        private static void EnterAwaitingConsent()
        {
            if (Interlocked.Increment(ref awaitingConsent) == 1)
            {
                AwaitingConsentChanged?.Invoke(null, EventArgs.Empty);
            }
        }

        private static void ExitAwaitingConsent()
        {
            if (Interlocked.Decrement(ref awaitingConsent) == 0)
            {
                AwaitingConsentChanged?.Invoke(null, EventArgs.Empty);
            }
        }

        private sealed class AccessScope : IDisposable
        {
            private readonly AccessScope? previous;

            internal AccessScope(AccessScope? previous)
            {
                this.previous = previous;
                this.Grants = previous?.Grants ?? new ConcurrentDictionary<string, ExternalSourceDecision>(StringComparer.Ordinal);
            }

            internal ConcurrentDictionary<string, ExternalSourceDecision> Grants { get; }

            public void Dispose() => CurrentScope.Value = this.previous;
        }
    }
}
