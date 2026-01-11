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
                if (provider == null)
                {
                    Debug.WriteLine("[DefaultProviderExecutor] No provider instance available");
                    return null;
                }

                // Use the cached GetStreamingAdapter method from AIProvider base class
                var adapter = provider.GetStreamingAdapter();
                Debug.WriteLine(adapter != null
                    ? $"[DefaultProviderExecutor] Using cached streaming adapter from provider '{provider.Name}'"
                    : $"[DefaultProviderExecutor] No streaming adapter available for provider '{provider.Name}'");
                return adapter;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DefaultProviderExecutor] Error getting streaming adapter: {ex.Message}");
                return null;
            }
        }
    }
}
