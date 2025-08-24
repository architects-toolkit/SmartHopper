/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using System;
using System.Threading.Tasks;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AICall.Policies;

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
                    rq.Body = AIBodyBuilder.FromImmutable(rq.Body)
                        .AddSystem($"Timeout applied: {normalized}s (default)")
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
                    rq.Body = AIBodyBuilder.FromImmutable(rq.Body)
                        .AddSystem($"Timeout increased from {original}s to {MinTimeout}s (minimum)")
                        .Build();
                }
            }
            else if (normalized > MaxTimeout)
            {
                rq.TimeoutSeconds = MaxTimeout;
                if (rq.Body != null)
                {
                    rq.Body = AIBodyBuilder.FromImmutable(rq.Body)
                        .AddSystem($"Timeout reduced from {original}s to {MaxTimeout}s (maximum)")
                        .Build();
                }
            }

            return Task.CompletedTask;
        }
    }
}
