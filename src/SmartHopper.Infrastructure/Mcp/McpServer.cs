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

/*
 * HTTP/JSON-RPC transport skeleton structurally adapted from Cordyceps
 * (https://github.com/brookstalley/cordyceps, McpServer.cs), simplified for
 * SmartHopper: loopback bind, no SSE, single shared dispatcher.
 * Copyright (c) 2026 Brooks Talley. Licensed under the MIT License.
 */

using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SmartHopper.Infrastructure.Mcp
{
    /// <summary>
    /// Local HTTP MCP server. Binds to <c>http://127.0.0.1:&lt;port&gt;/mcp</c>,
    /// rejects non-loopback origins, and forwards every JSON-RPC request to a
    /// <see cref="JsonRpcDispatcher"/>.
    /// </summary>
    public sealed class McpServer : IDisposable
    {
        private const string Endpoint = "/mcp";
        private const string HealthEndpoint = "/health";

        private readonly McpServerOptions options;
        private readonly JsonRpcDispatcher dispatcher;
        private HttpListener? listener;
        private CancellationTokenSource? cancellation;
        private Task? acceptLoop;
        private int started;

        /// <summary>
        /// Initializes a new instance of the <see cref="McpServer"/> class.
        /// </summary>
        public McpServer(McpServerOptions options)
            : this(options, new JsonRpcDispatcher(options, new AIToolMcpAdapter(options)))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="McpServer"/> class with a custom dispatcher.
        /// </summary>
        public McpServer(McpServerOptions options, JsonRpcDispatcher dispatcher)
        {
            this.options = options ?? throw new ArgumentNullException(nameof(options));
            this.dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        }

        /// <summary>Gets a value indicating whether the server is currently accepting requests.</summary>
        public bool IsRunning => this.listener?.IsListening ?? false;

        /// <summary>Gets the configured loopback URL the server listens on.</summary>
        public string Url => $"http://127.0.0.1:{this.options.Port}{Endpoint}";

        /// <summary>
        /// Starts the server. Subsequent calls are no-ops.
        /// </summary>
        public void Start()
        {
            if (Interlocked.Exchange(ref this.started, 1) == 1)
            {
                return;
            }

            this.listener = new HttpListener();
            this.listener.Prefixes.Add($"http://127.0.0.1:{this.options.Port}/");
            try
            {
                this.listener.Start();
            }
            catch (Exception ex)
            {
                this.listener.Close();
                this.listener = null;
                Interlocked.Exchange(ref this.started, 0);
                throw new InvalidOperationException($"Failed to start MCP HTTP listener on port {this.options.Port}: {ex.Message}", ex);
            }

            this.cancellation = new CancellationTokenSource();
            this.acceptLoop = Task.Run(() => this.AcceptLoopAsync(this.cancellation.Token));
            Debug.WriteLine($"[Mcp] Server listening on {this.Url}");
        }

        /// <summary>
        /// Stops the server. Subsequent calls are no-ops.
        /// </summary>
        public void Stop()
        {
            if (Interlocked.Exchange(ref this.started, 0) == 0)
            {
                return;
            }

            try
            {
                this.cancellation?.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }

            try
            {
                this.listener?.Stop();
                this.listener?.Close();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Mcp] Error stopping listener: {ex.Message}");
            }

            try
            {
                this.acceptLoop?.Wait(TimeSpan.FromSeconds(2));
            }
            catch (AggregateException)
            {
            }

            this.listener = null;
            this.cancellation?.Dispose();
            this.cancellation = null;
            this.acceptLoop = null;
            Debug.WriteLine("[Mcp] Server stopped");
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.Stop();
            this.dispatcher.Dispose();
        }

        private async Task AcceptLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && this.listener != null)
            {
                HttpListenerContext context;
                try
                {
                    context = await this.listener.GetContextAsync().ConfigureAwait(false);
                }
                catch (HttpListenerException) when (ct.IsCancellationRequested)
                {
                    return;
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Mcp] Accept error: {ex.Message}");
                    continue;
                }

                _ = Task.Run(() => this.HandleRequestAsync(context, ct));
            }
        }

        private async Task HandleRequestAsync(HttpListenerContext context, CancellationToken ct)
        {
            var request = context.Request;
            var response = context.Response;
            try
            {
                // Reject non-loopback peers as a defence-in-depth measure even though the
                // listener is already bound to 127.0.0.1.
                if (request.RemoteEndPoint == null || !IPAddress.IsLoopback(request.RemoteEndPoint.Address))
                {
                    await WriteStatusAsync(response, HttpStatusCode.Forbidden, "Non-loopback origin").ConfigureAwait(false);
                    return;
                }

                if (string.Equals(request.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(request.Url?.AbsolutePath, HealthEndpoint, StringComparison.OrdinalIgnoreCase))
                {
                    await WriteJsonAsync(response, HttpStatusCode.OK, "{\"status\":\"ok\"}").ConfigureAwait(false);
                    return;
                }

                if (!string.Equals(request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
                {
                    await WriteStatusAsync(response, HttpStatusCode.MethodNotAllowed, "POST required").ConfigureAwait(false);
                    return;
                }

                if (!string.Equals(request.Url?.AbsolutePath, Endpoint, StringComparison.OrdinalIgnoreCase))
                {
                    await WriteStatusAsync(response, HttpStatusCode.NotFound, "Not found").ConfigureAwait(false);
                    return;
                }

                if (!this.AuthorizeRequest(request, response, out var authError))
                {
                    await WriteStatusAsync(response, HttpStatusCode.Unauthorized, authError).ConfigureAwait(false);
                    return;
                }

                string body;
                using (var reader = new StreamReader(request.InputStream, request.ContentEncoding ?? Encoding.UTF8))
                {
                    body = await reader.ReadToEndAsync().ConfigureAwait(false);
                }

                if (body.Length > 256 * 1024)
                {
                    await WriteStatusAsync(response, HttpStatusCode.RequestEntityTooLarge, "Request body too large").ConfigureAwait(false);
                    return;
                }

                var responseBody = await this.dispatcher.DispatchAsync(body, ct).ConfigureAwait(false);
                if (responseBody == null)
                {
                    // JSON-RPC notification — return 204.
                    response.StatusCode = (int)HttpStatusCode.NoContent;
                    response.Close();
                    return;
                }

                await WriteJsonAsync(response, HttpStatusCode.OK, responseBody).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Mcp] Request error: {ex.Message}");
                try
                {
                    await WriteStatusAsync(response, HttpStatusCode.InternalServerError, "Internal server error").ConfigureAwait(false);
                }
                catch
                {
                    // Response stream may already be closed.
                }
            }
        }

        private bool AuthorizeRequest(HttpListenerRequest request, HttpListenerResponse response, out string error)
        {
            error = string.Empty;
            var expected = this.options.BearerToken;
            if (string.IsNullOrEmpty(expected))
            {
                return true;
            }

            var header = request.Headers["Authorization"];
            if (string.IsNullOrEmpty(header) || !header.StartsWith("Bearer ", StringComparison.Ordinal))
            {
                error = "Bearer token required";
                return false;
            }

            var presented = header.Substring("Bearer ".Length).Trim();
            if (!string.Equals(presented, expected, StringComparison.Ordinal))
            {
                error = "Invalid bearer token";
                return false;
            }

            return true;
        }

        private static async Task WriteJsonAsync(HttpListenerResponse response, HttpStatusCode status, string body)
        {
            var bytes = Encoding.UTF8.GetBytes(body ?? string.Empty);
            response.StatusCode = (int)status;
            response.ContentType = "application/json; charset=utf-8";
            response.ContentLength64 = bytes.Length;
            await response.OutputStream.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
            response.OutputStream.Flush();
            response.Close();
        }

        private static async Task WriteStatusAsync(HttpListenerResponse response, HttpStatusCode status, string message)
        {
            var payload = $"{{\"error\":\"{Escape(message)}\"}}";
            await WriteJsonAsync(response, status, payload).ConfigureAwait(false);
        }

        private static string Escape(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            return text.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
