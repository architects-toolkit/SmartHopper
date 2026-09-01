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

namespace SmartHopper.ProviderSdk.Tests.TestHelpers
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Newtonsoft.Json.Linq;
    using SmartHopper.ProviderSdk.Hosting;

    /// <summary>
    /// Test implementation of <see cref="IProviderHttpClientFactory"/> that routes
    /// all provider HTTP calls to a single <see cref="HttpMessageHandler"/>.
    /// This lets tests verify request encoding and supply fake responses without
    /// touching the network.
    /// </summary>
    public sealed class TestProviderHttpClientFactory : IProviderHttpClientFactory
    {
        private readonly HttpMessageHandler handler;

        /// <summary>
        /// Initializes a new instance of the <see cref="TestProviderHttpClientFactory"/> class
        /// with the specified <paramref name="handler"/>.
        /// </summary>
        /// <param name="handler">The handler used to intercept provider HTTP calls.</param>
        public TestProviderHttpClientFactory(HttpMessageHandler handler)
        {
            this.handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        /// <summary>
        /// Creates an in-memory test factory that always returns the given
        /// HTTP status code and JSON body.
        /// </summary>
        /// <param name="statusCode">The HTTP status code to return.</param>
        /// <param name="body">The JSON response body.</param>
        /// <returns>A configured <see cref="TestProviderHttpClientFactory"/>.</returns>
        public static TestProviderHttpClientFactory WithResponse(HttpStatusCode statusCode, JToken body)
        {
            var handler = new FixedResponseHandler(statusCode, body?.ToString() ?? string.Empty);
            return new TestProviderHttpClientFactory(handler);
        }

        /// <summary>
        /// Creates an in-memory test factory that returns the provided
        /// <see cref="HttpResponseMessage"/> for every request.
        /// </summary>
        /// <param name="responseFactory">A factory that creates the response for each request.</param>
        /// <returns>A configured <see cref="TestProviderHttpClientFactory"/>.</returns>
        public static TestProviderHttpClientFactory WithResponse(
            Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responseFactory)
        {
            var handler = new DynamicResponseHandler(responseFactory);
            return new TestProviderHttpClientFactory(handler);
        }

        /// <inheritdoc />
        public HttpClient CreateClient(string providerName, TimeSpan timeout)
        {
            var client = new HttpClient(this.handler, disposeHandler: false);
            if (timeout > TimeSpan.Zero)
            {
                client.Timeout = timeout;
            }

            return client;
        }

        private sealed class FixedResponseHandler : HttpMessageHandler
        {
            private readonly HttpStatusCode statusCode;
            private readonly string body;

            public FixedResponseHandler(HttpStatusCode statusCode, string body)
            {
                this.statusCode = statusCode;
                this.body = body ?? string.Empty;
            }

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                var response = new HttpResponseMessage(this.statusCode)
                {
                    Content = new StringContent(this.body),
                };

                return Task.FromResult(response);
            }
        }

        private sealed class DynamicResponseHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responseFactory;

            public DynamicResponseHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responseFactory)
            {
                this.responseFactory = responseFactory ?? throw new ArgumentNullException(nameof(responseFactory));
            }

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                return this.responseFactory(request, cancellationToken);
            }
        }
    }
}
