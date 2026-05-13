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
using SmartHopper.ProviderSdk.AICall.Core.Requests;
using SmartHopper.ProviderSdk.AICall.Core.Returns;

namespace SmartHopper.ProviderSdk.Hosting
{
    /// <summary>
    /// Surface that the SDK consumes to invoke the host's request and response policy
    /// pipeline. The SmartHopper host registers its <c>PolicyPipeline</c> with
    /// <see cref="ProviderSdkHost.PolicyPipeline"/> at startup so DTO-side execution paths
    /// can apply policies without referencing host orchestration types directly.
    /// </summary>
    public interface IPolicyPipelineHost
    {
        /// <summary>
        /// Apply all configured request-side policies to <paramref name="request"/>
        /// before it leaves the host.
        /// </summary>
        Task ApplyRequestPoliciesAsync(AIRequestCall request);

        /// <summary>
        /// Apply all configured response-side policies to <paramref name="response"/>
        /// after the provider returns.
        /// </summary>
        Task ApplyResponsePoliciesAsync(AIReturn response);
    }

    /// <summary>
    /// Default no-op policy pipeline used when no host has been registered. Lets SDK
    /// code execute end-to-end in tests and stand-alone tools without any orchestration.
    /// </summary>
    public sealed class NullPolicyPipelineHost : IPolicyPipelineHost
    {
        /// <inheritdoc />
        public Task ApplyRequestPoliciesAsync(AIRequestCall request) => Task.CompletedTask;

        /// <inheritdoc />
        public Task ApplyResponsePoliciesAsync(AIReturn response) => Task.CompletedTask;
    }
}
