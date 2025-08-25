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
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using SmartHopper.Infrastructure.AICall.Core.Base;

namespace SmartHopper.Infrastructure.AICall.Policies.Response
{
    /// <summary>
    /// Temporary compatibility policy that decodes the provider raw JSON using the provider's current Decode method.
    /// This preserves existing behavior until centralized mappers replace provider-specific decode logic.
    /// </summary>
    public sealed class CompatibilityDecodeResponsePolicy : IResponsePolicy
    {
        public Task ApplyAsync(PolicyContext context)
        {
            if (context == null || context.Response == null)
            {
                return Task.CompletedTask;
            }

            try
            {
                // Skip if already has interactions or no raw payload
                var body = context.Response.Body;
                if (body != null && body.Interactions != null && body.Interactions.Count > 0)
                {
                    return Task.CompletedTask;
                }

                var raw = context.Response.GetRaw();
                if (raw == null)
                {
                    return Task.CompletedTask;
                }

                var provider = context.Request?.ProviderInstance;
                if (provider == null)
                {
                    // No provider to decode with; leave as-is
                    return Task.CompletedTask;
                }

                var decoded = provider.Decode(raw.ToString());
                if (decoded != null && decoded.Count > 0)
                {
                    context.Response.SetBody(decoded);
                }
            }
            catch (Exception ex)
            {
                // Non-fatal: attach as warning on the response
                context.Response?.AddRuntimeMessage(
                    AIRuntimeMessageSeverity.Warning,
                    AIRuntimeMessageOrigin.Return,
                    $"Compatibility decode failed: {ex.Message}");
                Debug.WriteLine($"[CompatibilityDecodeResponsePolicy] Exception: {ex}");
            }

            return Task.CompletedTask;
        }
    }
}
