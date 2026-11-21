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
using Rhino;

namespace SmartHopper.Infrastructure.FileSystem
{
    /// <summary>
    /// Applies explicit human approval to local file access by AI tools; no presenter means deny.
    /// Mirrors <see cref="Networking.ExternalSourceAccess"/> for filesystem paths instead of origins.
    /// </summary>
    public static class LocalFileAccess
    {
        private static readonly AsyncLocal<AccessScope?> CurrentScope = new AsyncLocal<AccessScope?>();
        private static readonly SemaphoreSlim PromptLock = new SemaphoreSlim(1, 1);
        private static int awaitingConsent;

        /// <summary>Gets a value indicating whether at least one consent prompt is open and unanswered.</summary>
        public static bool IsAwaitingConsent => Volatile.Read(ref awaitingConsent) > 0;

        /// <summary>Raised when <see cref="IsAwaitingConsent"/> transitions between false and true, so UI can repaint a waiting indicator.</summary>
        public static event EventHandler? AwaitingConsentChanged;

        /// <summary>Gets or sets the interactive host prompt. It receives a canonicalized request.</summary>
        public static Func<LocalFileAccessRequest, CancellationToken, Task<LocalFileAccessDecision>>? PromptAsync { get; set; }

        /// <summary>Starts a logical tool invocation; nested helpers share one-call approvals.</summary>
        public static IDisposable BeginScope()
        {
            var previous = CurrentScope.Value;
            var current = new AccessScope(previous);
            CurrentScope.Value = current;
            return current;
        }

        /// <summary>
        /// Grants a path for the remainder of the current scope without prompting.
        /// Used by trusted internal flows (e.g. files the user attached themselves).
        /// No-op without an active scope.
        /// </summary>
        public static void GrantForCurrentScope(string path, LocalFileCapability capability)
        {
            var grants = CurrentScope.Value?.Grants;
            if (grants == null || !LocalPath.TryCanonicalize(path, out var canonical, out _))
            {
                return;
            }

            grants.TryAdd(Key(canonical, capability), LocalFileAccessDecision.AllowOnce);
        }

        /// <summary>
        /// Checks, without prompting, whether authorizing this path would need a prompt.
        /// Lets synchronous call sites on the UI thread refuse instead of deadlocking.
        /// </summary>
        public static bool IsPromptRequired(string canonicalPath, LocalFileCapability capability)
        {
            if (LocalPath.IsDenied(canonicalPath) || LocalPath.IsPreAuthorized(canonicalPath, capability))
            {
                return false;
            }

            var grants = CurrentScope.Value?.Grants;
            if (LocalFileAllowlist.Current.IsGranted(canonicalPath, capability))
            {
                return false;
            }

            return grants == null || !grants.TryGetValue(Key(canonicalPath, capability), out _);
        }

        /// <summary>
        /// Blocking variant of <see cref="RequireAsync"/> for tool call sites that cannot await.
        /// Refuses on the Rhino UI thread, where waiting on an unanswered prompt would deadlock,
        /// so callers fail closed instead of hanging the host.
        /// </summary>
        public static void RequireBlocking(string path, LocalFileCapability capability, string? toolName = null)
        {
            if (!LocalPath.TryCanonicalize(path, out var canonical, out var error))
            {
                throw new UnauthorizedAccessException($"Access to '{path}' was denied: {error}");
            }

            if (!RhinoApp.InvokeRequired && IsPromptRequired(canonical, capability))
            {
                throw new UnauthorizedAccessException($"Access to '{canonical}' requires your approval.");
            }

            RequireAsync(canonical, capability, toolName).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        /// <summary>Authorizes a path before a tool touches it. No mutation-bypass policy applies.</summary>
        public static Task RequireAsync(
            string path,
            LocalFileCapability capability,
            string? toolName = null,
            CancellationToken cancellationToken = default) =>
            RequireAsync(path, capability, toolName, LocalFileAllowlist.Current, PromptAsync, cancellationToken);

        /// <summary>Authorizes against explicit dependencies for deterministic, filesystem-free contract tests.</summary>
        internal static async Task RequireAsync(
            string path,
            LocalFileCapability capability,
            string? toolName,
            LocalFileAllowlist allowlist,
            Func<LocalFileAccessRequest, CancellationToken, Task<LocalFileAccessDecision>>? prompt,
            CancellationToken cancellationToken)
        {
            if (!LocalPath.TryCanonicalize(path, out var canonical, out var error))
            {
                throw new UnauthorizedAccessException($"Access to '{path}' was denied: {error}");
            }

            if (capability == LocalFileCapability.None
                || (capability & ~(LocalFileCapability.Read | LocalFileCapability.Write | LocalFileCapability.Execute)) != 0)
            {
                throw new ArgumentException("A single valid file capability is required.", nameof(capability));
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (LocalPath.IsDenied(canonical))
            {
                throw new UnauthorizedAccessException($"Access to '{canonical}' is not allowed: the location is protected.");
            }

            if (LocalPath.IsPreAuthorized(canonical, capability) || allowlist.IsGranted(canonical, capability))
            {
                return;
            }

            var grants = CurrentScope.Value?.Grants;
            var key = Key(canonical, capability);

            await PromptLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (allowlist.IsGranted(canonical, capability))
                {
                    return;
                }

                if (grants != null && grants.TryGetValue(key, out var previous))
                {
                    if (previous == LocalFileAccessDecision.Deny)
                    {
                        throw new UnauthorizedAccessException($"Access to '{canonical}' was denied.");
                    }

                    return;
                }

                LocalFileAccessDecision decision;
                if (prompt == null)
                {
                    decision = LocalFileAccessDecision.Deny;
                }
                else
                {
                    var request = new LocalFileAccessRequest
                    {
                        CanonicalPath = canonical,
                        Capability = capability,
                        ToolName = toolName,
                        WillOverwrite = capability == LocalFileCapability.Write && System.IO.File.Exists(canonical),
                    };
                    EnterAwaitingConsent();
                    try
                    {
                        decision = await prompt(request, cancellationToken).ConfigureAwait(false);
                    }
                    finally
                    {
                        ExitAwaitingConsent();
                    }
                }
                cancellationToken.ThrowIfCancellationRequested();
                if (decision == LocalFileAccessDecision.AlwaysAllow || decision == LocalFileAccessDecision.AlwaysAllowFolder)
                {
                    // A failed persistent write fails closed instead of pretending a grant was stored.
                    var grantPath = decision == LocalFileAccessDecision.AlwaysAllowFolder
                        ? System.IO.Path.GetDirectoryName(canonical) ?? canonical
                        : canonical;
                    allowlist.Allow(new LocalFileGrant
                    {
                        Scope = decision == LocalFileAccessDecision.AlwaysAllowFolder
                            ? LocalFileGrantScope.Directory
                            : LocalFileGrantScope.File,
                        Path = grantPath,
                        Capabilities = capability,
                        GrantedUtc = DateTimeOffset.UtcNow,
                    });
                }
                else if (decision == LocalFileAccessDecision.NoResponse)
                {
                    grants?.TryAdd(key, LocalFileAccessDecision.Deny);
                    throw new UnauthorizedAccessException(
                        $"Access to '{canonical}' requires your approval; the consent prompt was not answered.");
                }
                else if (decision != LocalFileAccessDecision.AllowOnce)
                {
                    grants?.TryAdd(key, LocalFileAccessDecision.Deny);
                    throw new UnauthorizedAccessException($"Access to '{canonical}' requires your approval.");
                }
                else
                {
                    grants?.TryAdd(key, decision);
                }
            }
            finally
            {
                PromptLock.Release();
            }
        }

        private static string Key(string canonical, LocalFileCapability capability) =>
            ((int)capability).ToString() + "|" + canonical;

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
                this.Grants = previous?.Grants ?? new ConcurrentDictionary<string, LocalFileAccessDecision>(LocalPath.Comparer);
            }

            internal ConcurrentDictionary<string, LocalFileAccessDecision> Grants { get; }

            public void Dispose() => CurrentScope.Value = this.previous;
        }
    }
}
