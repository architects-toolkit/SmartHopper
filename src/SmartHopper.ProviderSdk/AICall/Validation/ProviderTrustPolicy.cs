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
using SmartHopper.ProviderSdk.AICall.Core.Requests;
using SmartHopper.ProviderSdk.Diagnostics;
using SmartHopper.ProviderSdk.Hosting;

namespace SmartHopper.ProviderSdk.AICall.Validation
{
    /// <summary>
    /// Verdict produced by <see cref="ProviderTrustPolicy.Evaluate"/>.
    /// </summary>
    public enum ProviderTrustVerdict
    {
        /// <summary>
        /// The provider passed all trust checks and the call may proceed.
        /// </summary>
        Allow,

        /// <summary>
        /// The provider raised a trust concern but the configured mode allows the call
        /// with a warning surfaced to the user.
        /// </summary>
        Warn,

        /// <summary>
        /// The configured mode requires the call to be blocked.
        /// </summary>
        Block,
    }

    /// <summary>
    /// Result of evaluating a provider against the configured integrity/trust policy.
    /// </summary>
    public sealed class ProviderTrustResult
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ProviderTrustResult"/> class.
        /// </summary>
        /// <param name="verdict">The policy verdict.</param>
        /// <param name="messages">Structured diagnostics produced by the evaluation.</param>
        public ProviderTrustResult(ProviderTrustVerdict verdict, IReadOnlyList<SHRuntimeMessage> messages)
        {
            this.Verdict = verdict;
            this.Messages = messages ?? new List<SHRuntimeMessage>();
        }

        /// <summary>
        /// Gets the verdict: whether the call should be allowed, warned, or blocked.
        /// </summary>
        public ProviderTrustVerdict Verdict { get; }

        /// <summary>
        /// Gets the diagnostics produced by the trust evaluation.
        /// Warnings are produced when the provider is allowed with a caution.
        /// Errors are produced when the provider is blocked.
        /// </summary>
        public IReadOnlyList<SHRuntimeMessage> Messages { get; }
    }

    /// <summary>
    /// Centralized provider trust/integrity decision point.
    /// Enforces the configured <see cref="ProviderIntegrityCheckMode"/> against the
    /// provider's mismatch, availability, unknown, community, and unsigned states.
    /// </summary>
    public static class ProviderTrustPolicy
    {
        /// <summary>
        /// Evaluates whether the provider for <paramref name="request"/> is trusted enough to call.
        /// </summary>
        /// <param name="request">The request whose provider should be evaluated.</param>
        /// <param name="trustHost">Optional trust host to use; defaults to <see cref="ProviderSdkHost.ProviderTrust"/>.</param>
        /// <returns>A <see cref="ProviderTrustResult"/> containing the verdict and diagnostics.</returns>
        public static ProviderTrustResult Evaluate(AIRequestCall request, IProviderTrustHost trustHost = null)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            return Evaluate(request.Provider, trustHost);
        }

        /// <summary>
        /// Evaluates whether <paramref name="providerName"/> is trusted enough to call.
        /// </summary>
        /// <param name="providerName">The provider to evaluate.</param>
        /// <param name="trustHost">Optional trust host to use; defaults to <see cref="ProviderSdkHost.ProviderTrust"/>.</param>
        /// <returns>A <see cref="ProviderTrustResult"/> containing the verdict and diagnostics.</returns>
        public static ProviderTrustResult Evaluate(string providerName, IProviderTrustHost trustHost = null)
        {
            trustHost ??= ProviderSdkHost.ProviderTrust ?? new NullProviderTrustHost();

            if (string.IsNullOrEmpty(providerName))
            {
                return new ProviderTrustResult(ProviderTrustVerdict.Allow, new List<SHRuntimeMessage>());
            }

            var effectiveMode = trustHost.EffectiveIntegrityCheckMode;
            var messages = new List<SHRuntimeMessage>();
            var verdict = ProviderTrustVerdict.Allow;

            if (trustHost.IsProviderMismatched(providerName))
            {
                var (conditionVerdict, severity, code) = Decide(Condition.Mismatched, effectiveMode);
                messages.Add(new SHRuntimeMessage(
                    severity,
                    SHRuntimeMessageOrigin.Validation,
                    code,
                    $"Provider '{providerName}' failed SHA-256 integrity verification. " +
                    "The provider's hash does not match the official published hash. " +
                    "This could indicate file corruption or tampering, and your data could be compromised."));
                verdict = UpdateVerdict(verdict, conditionVerdict);
            }

            if (trustHost.IsProviderUnavailable(providerName))
            {
                var (conditionVerdict, severity, code) = Decide(Condition.Unavailable, effectiveMode);
                messages.Add(new SHRuntimeMessage(
                    severity,
                    SHRuntimeMessageOrigin.Validation,
                    code,
                    $"Provider '{providerName}' could not be verified - hash check unavailable due to network issues. " +
                    "Use this provider only if you trust its source."));
                verdict = UpdateVerdict(verdict, conditionVerdict);
            }

            if (trustHost.IsProviderUnknown(providerName))
            {
                var (conditionVerdict, severity, code) = Decide(Condition.Unknown, effectiveMode);
                messages.Add(new SHRuntimeMessage(
                    severity,
                    SHRuntimeMessageOrigin.Validation,
                    code,
                    $"Provider '{providerName}' is not known - it may be a custom or third-party provider. " +
                    "Enable this provider only if you trust its source. " +
                    "Change 'Integrity Check Mode' to 'Hard' or 'Strict' in SmartHopper settings to block unknown providers."));
                verdict = UpdateVerdict(verdict, conditionVerdict);
            }

            if (trustHost.IsProviderCommunity(providerName))
            {
                var (conditionVerdict, severity, code) = Decide(Condition.Community, effectiveMode);
                messages.Add(new SHRuntimeMessage(
                    severity,
                    SHRuntimeMessageOrigin.Validation,
                    code,
                    $"Provider '{providerName}' is a community provider, not signed by SmartHopper. " +
                    "Use it only if you trust its source — community providers run with full plugin privileges."));
                verdict = UpdateVerdict(verdict, conditionVerdict);
            }
            else if (trustHost.IsProviderUnsigned(providerName))
            {
                var (conditionVerdict, severity, code) = Decide(Condition.Unsigned, effectiveMode);
                messages.Add(new SHRuntimeMessage(
                    severity,
                    SHRuntimeMessageOrigin.Validation,
                    code,
                    $"Provider '{providerName}' is unsigned. " +
                    "Use it only if you trust its source."));
                verdict = UpdateVerdict(verdict, conditionVerdict);
            }

            return new ProviderTrustResult(verdict, messages);
        }

        private enum Condition
        {
            Mismatched,
            Unavailable,
            Unknown,
            Community,
            Unsigned,
        }

        private static (ProviderTrustVerdict Verdict, SHRuntimeMessageSeverity Severity, SHMessageCode Code) Decide(Condition condition, ProviderIntegrityCheckMode mode)
        {
            return condition switch
            {
                Condition.Mismatched => mode == ProviderIntegrityCheckMode.Soft
                    ? (ProviderTrustVerdict.Warn, SHRuntimeMessageSeverity.Warning, SHMessageCode.ProviderTrustWarning)
                    : (ProviderTrustVerdict.Block, SHRuntimeMessageSeverity.Error, SHMessageCode.ProviderTrustBlocked),

                Condition.Unavailable => mode == ProviderIntegrityCheckMode.Soft || mode == ProviderIntegrityCheckMode.Hard
                    ? (ProviderTrustVerdict.Warn, SHRuntimeMessageSeverity.Warning, SHMessageCode.ProviderTrustWarning)
                    : (ProviderTrustVerdict.Block, SHRuntimeMessageSeverity.Error, SHMessageCode.ProviderTrustBlocked),

                Condition.Unknown => mode == ProviderIntegrityCheckMode.Soft
                    ? (ProviderTrustVerdict.Warn, SHRuntimeMessageSeverity.Warning, SHMessageCode.ProviderTrustWarning)
                    : (ProviderTrustVerdict.Block, SHRuntimeMessageSeverity.Error, SHMessageCode.ProviderTrustBlocked),

                Condition.Community => (ProviderTrustVerdict.Warn, SHRuntimeMessageSeverity.Warning, SHMessageCode.ProviderTrustWarning),

                Condition.Unsigned => (ProviderTrustVerdict.Warn, SHRuntimeMessageSeverity.Warning, SHMessageCode.ProviderTrustWarning),

                _ => (ProviderTrustVerdict.Warn, SHRuntimeMessageSeverity.Warning, SHMessageCode.ProviderTrustWarning),
            };
        }

        private static ProviderTrustVerdict UpdateVerdict(ProviderTrustVerdict current, ProviderTrustVerdict candidate)
        {
            if (candidate == ProviderTrustVerdict.Block || current == ProviderTrustVerdict.Block)
            {
                return ProviderTrustVerdict.Block;
            }

            if (candidate == ProviderTrustVerdict.Warn || current == ProviderTrustVerdict.Warn)
            {
                return ProviderTrustVerdict.Warn;
            }

            return ProviderTrustVerdict.Allow;
        }
    }
}
