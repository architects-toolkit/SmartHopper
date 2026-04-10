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
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.Settings;

namespace SmartHopper.Infrastructure.AICall.Policies.Request
{
    /// <summary>
    /// Normalizes the per-request timeout on <see cref="AIRequestBase"/> derivatives.
    /// Applies a default when unset and clamps to a safe range.
    /// Adds a lightweight diagnostic as a system interaction when adjustments are made.
    /// </summary>
    public sealed class RequestTimeoutPolicy : IRequestPolicy
    {
        // Default fallback values when settings cannot be read
        private const int DefaultTimeout = 300;
        
        // Bounds (seconds)
        private const int MinTimeout = 1;
        private const int MaxTimeout = 600; // 10 minutes maximum guard

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

                if (rq.Body != null)
                {
                    rq.Body = AIBodyBuilder.FromImmutable(rq.Body)
                        .AddError($"Timeout applied: {normalized}s (from settings)")
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
                    // Use AIInteractionError to surface as UI-only diagnostic; providers will skip encoding it
                    rq.Body = AIBodyBuilder.FromImmutable(rq.Body)
                        .AddError($"Timeout increased from {original}s to {MinTimeout}s (minimum)")
                        .Build();
                }
            }
            else if (normalized > MaxTimeout)
            {
                rq.TimeoutSeconds = MaxTimeout;
                if (rq.Body != null)
                {
                    // Use AIInteractionError to surface as UI-only diagnostic; providers will skip encoding it
                    rq.Body = AIBodyBuilder.FromImmutable(rq.Body)
                        .AddError($"Timeout reduced from {original}s to {MaxTimeout}s (maximum)")
                        .Build();
                }
            }

            return Task.CompletedTask;
        }
    }
}
