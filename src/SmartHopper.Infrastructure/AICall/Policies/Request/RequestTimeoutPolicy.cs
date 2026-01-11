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
 * along with this library; if not, write to the Free Software Foundation,
 * Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301, USA.
 */

using System.Threading.Tasks;
using SmartHopper.Infrastructure.AICall.Core.Interactions;

namespace SmartHopper.Infrastructure.AICall.Policies.Request
{
    /// <summary>
    /// Normalizes the per-request timeout on <see cref="AIRequestBase"/> derivatives.
    /// Applies a default when unset and clamps to a safe range.
    /// Adds a lightweight diagnostic as a system interaction when adjustments are made.
    /// </summary>
    public sealed class RequestTimeoutPolicy : IRequestPolicy
    {
        // Default and bounds (seconds)
        private const int DefaultTimeout = 120;
        private const int MinTimeout = 1;
        private const int MaxTimeout = 600; // 10 minutes maximum guard

        public Task ApplyAsync(PolicyContext context)
        {
            var rq = context?.Request;
            if (rq == null)
            {
                return Task.CompletedTask;
            }

            int original = rq.TimeoutSeconds;
            int normalized = original;

            if (normalized <= 0)
            {
                normalized = DefaultTimeout;
                rq.TimeoutSeconds = normalized;
                if (rq.Body != null)
                {
                    // Use AIInteractionError to surface as UI-only diagnostic; providers will skip encoding it
                    rq.Body = AIBodyBuilder.FromImmutable(rq.Body)
                        .AddError($"Timeout applied: {normalized}s (default)")
                        .Build();
                }

                return Task.CompletedTask;
            }

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
