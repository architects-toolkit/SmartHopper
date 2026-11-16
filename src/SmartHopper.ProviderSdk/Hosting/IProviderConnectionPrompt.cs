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
using SmartHopper.ProviderSdk.AIProviders;

namespace SmartHopper.ProviderSdk.Hosting
{
    /// <summary>
    /// The user's answer to a "provider requires a connection" prompt.
    /// </summary>
    public enum ProviderConnectionPromptResult
    {
        /// <summary>Open the provider's sign-in flow and continue the call once connected.</summary>
        Connect,

        /// <summary>Proceed with the call without connecting (it may still fail server-side).</summary>
        Retry,

        /// <summary>Abort the call so the invoking component/tool fails cleanly.</summary>
        Cancel,
    }

    /// <summary>
    /// Interactive prompt the host can offer when a provider call is attempted while
    /// the provider reports no usable credentials. Registered through
    /// <see cref="ProviderSdkHost.ConnectionPrompt"/> by interactive hosts only;
    /// headless surfaces (MCP servers, hosted services) leave it unset and providers
    /// must let the call fail normally instead of prompting.
    /// </summary>
    public interface IProviderConnectionPrompt
    {
        /// <summary>
        /// Presents the connection-required prompt for <paramref name="provider"/>.
        /// Implementations marshal to the UI thread themselves and must honor
        /// cancellation by throwing <see cref="System.OperationCanceledException"/>.
        /// </summary>
        /// <param name="provider">Provider that needs a connection.</param>
        /// <param name="cancellationToken">Cancellation for the prompt.</param>
        /// <returns>The user's choice.</returns>
        Task<ProviderConnectionPromptResult> PromptAsync(IAIProvider provider, CancellationToken cancellationToken);

        /// <summary>
        /// Presents the connection-required prompt for a named feature that is not an
        /// <see cref="IAIProvider"/> — e.g. a component pack that shares the provider's
        /// OAuth session. Implementations marshal to the UI thread themselves and must
        /// honor cancellation by throwing <see cref="System.OperationCanceledException"/>.
        /// </summary>
        /// <param name="displayName">Feature or service name shown to the user (e.g. "SmartHopper Library").</param>
        /// <param name="cancellationToken">Cancellation for the prompt.</param>
        /// <returns>The user's choice.</returns>
        Task<ProviderConnectionPromptResult> PromptAsync(string displayName, CancellationToken cancellationToken);
    }
}
