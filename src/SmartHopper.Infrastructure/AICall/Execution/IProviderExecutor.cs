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

using System.Threading;
using System.Threading.Tasks;
using SmartHopper.Infrastructure.AICall.Core.Requests;
using SmartHopper.Infrastructure.AICall.Core.Returns;
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.Infrastructure.Streaming;

namespace SmartHopper.Infrastructure.AICall.Execution
{
    /// <summary>
    /// Abstraction for executing provider and tool calls, and optionally providing a streaming adapter.
    /// </summary>
    public interface IProviderExecutor
    {
        /// <summary>
        /// Executes a single provider call for the given request.
        /// </summary>
        Task<AIReturn?> ExecProviderAsync(AIRequestCall request, CancellationToken ct);

        /// <summary>
        /// Executes a single tool call.
        /// </summary>
        Task<AIReturn?> ExecToolAsync(AIToolCall toolCall, CancellationToken ct);

        /// <summary>
        /// Optionally returns a provider-specific streaming adapter if supported.
        /// </summary>
        IStreamingAdapter? TryGetStreamingAdapter(AIRequestCall request);
    }
}
