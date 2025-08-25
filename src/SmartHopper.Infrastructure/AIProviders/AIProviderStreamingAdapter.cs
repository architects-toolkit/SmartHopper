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
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SmartHopper.Infrastructure.AICall.Core.Requests;
using SmartHopper.Infrastructure.Streaming;

namespace SmartHopper.Infrastructure.AIProviders
{
    /// <summary>
    /// Base class providing shared helpers for provider-specific streaming adapters.
    /// Keeps adapters lightweight by centralizing HTTP setup, URL handling and SSE reading utilities.
    /// </summary>
    public abstract class AIProviderStreamingAdapter
    {
        /// <summary>
        /// Gets the owning provider instance.
        /// </summary>
        protected AIProvider Provider { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="AIProviderStreamingAdapter"/> class.
        /// </summary>
        /// <param name="provider">The owning provider.</param>
        protected AIProviderStreamingAdapter(AIProvider provider)
        {
            this.Provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        /// <summary>
        /// Runs the provider's PreCall pipeline for the request.
        /// </summary>
        /// <param name="request">The request to prepare.</param>
        /// <returns>The prepared request.</returns>
        protected AIRequestCall Prepare(AIRequestCall request)
        {
            return this.Provider.PreCall(request);
        }

        /// <summary>
        /// Builds an absolute URL for the given endpoint using the provider's default server URL when needed.
        /// </summary>
        protected string BuildFullUrl(string endpoint)
        {
            if (string.IsNullOrWhiteSpace(endpoint))
            {
                throw new ArgumentException("Endpoint cannot be null or empty", nameof(endpoint));
            }

            if (Uri.IsWellFormedUriString(endpoint, UriKind.Absolute))
            {
                return endpoint;
            }

            var baseUrl = this.Provider.DefaultServerUrl?.TrimEnd('/') ?? string.Empty;
            var path = endpoint.StartsWith("/", StringComparison.Ordinal) ? endpoint : "/" + endpoint;
            return baseUrl + path;
        }

        /// <summary>
        /// Creates a new HttpClient instance with common defaults.
        /// </summary>
        protected HttpClient CreateHttpClient()
        {
            var client = new HttpClient();
            return client;
        }

        /// <summary>
        /// Applies authentication headers to the provided HttpClient.
        /// </summary>
        /// <param name="client">The target HttpClient.</param>
        /// <param name="authentication">Authentication scheme (e.g., "bearer").</param>
        /// <param name="apiKey">API key or token if required by the scheme.</param>
        protected void ApplyAuthentication(HttpClient client, string authentication, string apiKey)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));

            if (string.IsNullOrWhiteSpace(authentication))
            {
                return; // no auth
            }

            switch (authentication.Trim().ToLowerInvariant())
            {
                case "bearer":
                    if (string.IsNullOrWhiteSpace(apiKey))
                    {
                        throw new InvalidOperationException($"{this.Provider.Name} API key is not configured or is invalid.");
                    }
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                    break;
                default:
                    throw new NotSupportedException($"Authentication method '{authentication}' is not supported. Only 'bearer' is currently supported.");
            }
        }

        /// <summary>
        /// Creates a POST request configured for SSE consumption.
        /// </summary>
        protected HttpRequestMessage CreateSsePost(string url, string body, string contentType = "application/json")
        {
            var req = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(body ?? string.Empty, Encoding.UTF8, contentType),
            };

            // Request SSE response
            req.Headers.Accept.Clear();
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
            return req;
        }

        /// <summary>
        /// Sends an HTTP request expecting a streaming response and returns the response message.
        /// Caller is responsible for disposing the response.
        /// </summary>
        protected Task<HttpResponseMessage> SendForStreamAsync(HttpClient client, HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }

        /// <summary>
        /// Reads Server-Sent Events (SSE) from an HTTP response and yields each data payload line.
        /// - Yields the content following the 'data:' prefix.
        /// - Stops when a '[DONE]' event is received.
        /// </summary>
        protected async IAsyncEnumerable<string> ReadSseDataAsync(HttpResponseMessage response, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            if (response == null) throw new ArgumentNullException(nameof(response));

            if (!response.IsSuccessStatusCode)
            {
                string error = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                throw new HttpRequestException($"Streaming request failed: {(int)response.StatusCode} {response.ReasonPhrase} - {error}");
            }

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var reader = new StreamReader(stream, Encoding.UTF8);

            while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync().ConfigureAwait(false);
                if (line == null)
                {
                    break;
                }

                if (line.Length == 0)
                {
                    continue; // ignore keep-alive blank lines
                }

                const string dataPrefix = "data:";
                if (line.StartsWith(dataPrefix, StringComparison.Ordinal))
                {
                    var payload = line.Substring(dataPrefix.Length).TrimStart();
                    if (payload == "[DONE]")
                    {
                        yield break;
                    }

                    yield return payload;
                }
            }
        }
    }
}
