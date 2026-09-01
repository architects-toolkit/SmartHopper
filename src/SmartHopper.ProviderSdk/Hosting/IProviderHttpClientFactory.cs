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
        /// Create an <see cref="HttpClient"/> scoped to <paramref name="providerName"/>
        /// with the given total request <paramref name="timeout"/>.
        /// </summary>
        /// <remarks>
        /// The returned <see cref="HttpClient"/> is owned by the caller and should be
        /// disposed after use. Implementations that pool handlers must ensure the
        /// underlying handler is not disposed when the client is disposed.
        /// </remarks>
        HttpClient CreateClient(string providerName, TimeSpan timeout);
    }

    /// <summary>
    /// Default implementation that returns a fresh <see cref="HttpClient"/> per call
    /// with the requested timeout. The SmartHopper host swaps in a smarter
    /// pooled/factory-based implementation at startup; tests can swap a
    /// <see cref="HttpMessageHandler"/> to intercept calls.
    /// </summary>
    public sealed class DefaultProviderHttpClientFactory : IProviderHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultProviderHttpClientFactory"/> class
        /// with a default <see cref="HttpClientHandler"/>.
        /// </summary>
        public DefaultProviderHttpClientFactory()
            : this(new HttpClientHandler())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultProviderHttpClientFactory"/> class
        /// with the specified <paramref name="handler"/>.
        /// </summary>
        /// <param name="handler">The handler to use for all created clients.</param>
        public DefaultProviderHttpClientFactory(HttpMessageHandler handler)
        {
            this._handler = handler ?? new HttpClientHandler();
        }

        /// <inheritdoc />
        public HttpClient CreateClient(string providerName, TimeSpan timeout)
        {
            var client = new HttpClient(this._handler, disposeHandler: false);
            if (timeout > TimeSpan.Zero)
            {
                client.Timeout = timeout;
            }

            return client;
        }
    }
}
