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

using System;
using System.Net.Http;

namespace SmartHopper.ProviderSdk.Hosting
{
    /// <summary>
    /// Surface that providers use to obtain configured <see cref="HttpClient"/>
    /// instances. The host can centralize timeouts, proxies, and retry behavior here so
    /// individual providers do not need to repeat the boilerplate.
    /// </summary>
    public interface IProviderHttpClientFactory
    {
        /// <summary>
        /// Create or fetch an <see cref="HttpClient"/> scoped to <paramref name="providerName"/>
        /// with the given total request <paramref name="timeout"/>.
        /// </summary>
        HttpClient CreateClient(string providerName, TimeSpan timeout);
    }

    /// <summary>
    /// Default implementation that returns a fresh <see cref="HttpClient"/> per call
    /// with the requested timeout. The SmartHopper host swaps in a smarter
    /// pooled/factory-based implementation at startup.
    /// </summary>
    public sealed class DefaultProviderHttpClientFactory : IProviderHttpClientFactory
    {
        /// <inheritdoc />
        public HttpClient CreateClient(string providerName, TimeSpan timeout)
        {
            var client = new HttpClient();
            if (timeout > TimeSpan.Zero)
            {
                client.Timeout = timeout;
            }

            return client;
        }
    }
}
