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

using System.Threading.Tasks;
using SmartHopper.Infrastructure.Settings;
using SmartHopper.ProviderSdk.AICall.Core;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.Settings;

namespace SmartHopper.Infrastructure.AICall.Policies.Request
{
    /// <summary>
    /// Normalizes the per-request timeout on <see cref="AIRequestBase"/> derivatives.
    /// Applies a default when unset and clamps to a safe range.
    /// Adds a lightweight diagnostic as a system interaction when adjustments are made.
    /// </summary>
    public sealed class RequestTimeoutPolicy : IRequestPolicy
    {
        // Default fallback values when settings cannot be read.
        // Sourced from TimeoutDefaults so all layers (policy, provider HTTP, tool execution) stay aligned.
        private const int DefaultTimeout = TimeoutDefaults.DefaultTimeoutSeconds;

        // Bounds (seconds)
        private const int MinTimeout = TimeoutDefaults.MinTimeoutSeconds;
        private const int MaxTimeout = TimeoutDefaults.MaxTimeoutSeconds;

        public Task ApplyAsync(PolicyContext context)
        {
            var rq = context?.Request;
            if (rq == null)
            {
                return Task.CompletedTask;
            }

            int? original = rq.TimeoutSeconds;
            int normalized;

            // If timeout is not set (null), resolve from settings
            if (original == null)
            {
                // Read response generation timeout from settings
                var settingValue = SmartHopperSettings.Instance.GetSetting("Global", "TimeoutSeconds");
                normalized = settingValue is int timeout ? timeout : DefaultTimeout;
                rq.TimeoutSeconds = normalized;

                // Applying the settings default is routine — emit as a non-surfaceable debug
                // diagnostic so it is captured in logs/analytics without being shown to end users.
                if (rq.Body != null)
                {
                    rq.Body = AIBodyBuilder.FromImmutable(rq.Body)
                        .AddDebug($"Timeout applied: {normalized}s (from settings)")
                        .Build();
                }

                return Task.CompletedTask;
            }

            // Use the explicitly set timeout value
            normalized = original.Value;

            // Clamp to bounds
            if (normalized < MinTimeout)
            {
                rq.TimeoutSeconds = MinTimeout;
                if (rq.Body != null)
                {
                    // Surface as a warning: the request's explicit timeout was adjusted to the allowed minimum.
                    rq.Body = AIBodyBuilder.FromImmutable(rq.Body)
                        .AddWarning($"Timeout increased from {original}s to {MinTimeout}s (minimum)")
                        .Build();
                }
            }
            else if (normalized > MaxTimeout)
            {
                rq.TimeoutSeconds = MaxTimeout;
                if (rq.Body != null)
                {
                    // Surface as a warning: the request's explicit timeout was clamped to the allowed maximum.
                    rq.Body = AIBodyBuilder.FromImmutable(rq.Body)
                        .AddWarning($"Timeout reduced from {original}s to {MaxTimeout}s (maximum)")
                        .Build();
                }
            }

            return Task.CompletedTask;
        }
    }
}
