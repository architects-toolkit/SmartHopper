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
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using SmartHopper.Infrastructure.AICall.Core.Requests;
using SmartHopper.Infrastructure.AICall.Core.Returns;
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.Infrastructure.Streaming;

namespace SmartHopper.Infrastructure.AICall.Execution
{
    /// <summary>
    /// Default implementation that delegates to existing Exec() methods and uses reflection
    /// to probe for a provider-specific streaming adapter.
    /// </summary>
    public sealed class DefaultProviderExecutor : IProviderExecutor
    {
        public async Task<AIReturn?> ExecProviderAsync(AIRequestCall request, CancellationToken ct)
        {
            if (request == null) return null;
            ct.ThrowIfCancellationRequested();

            // AIRequestCall.Exec() has its own cancellation handling internally for HTTP timeouts; we pre-check here.
            var res = await request.Exec().ConfigureAwait(false);
            return res;
        }

        public async Task<AIReturn?> ExecToolAsync(AIToolCall toolCall, CancellationToken ct)
        {
            if (toolCall == null) return null;
            ct.ThrowIfCancellationRequested();
            var res = await toolCall.Exec().ConfigureAwait(false);
            return res;
        }

        public IStreamingAdapter? TryGetStreamingAdapter(AIRequestCall request)
        {
            try
            {
                var provider = request?.ProviderInstance;
                var mi = provider?.GetType().GetMethod("GetStreamingAdapter", Type.EmptyTypes);
                var obj = mi?.Invoke(provider, null);
                var adapter = obj as IStreamingAdapter;
                Debug.WriteLine(adapter != null
                    ? $"[DefaultProviderExecutor] Using streaming adapter from provider '{provider?.Name}'"
                    : $"[DefaultProviderExecutor] No streaming adapter available for provider '{provider?.Name}'");
                return adapter;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DefaultProviderExecutor] Error probing streaming adapter: {ex.Message}");
                return null;
            }
        }
    }
}
