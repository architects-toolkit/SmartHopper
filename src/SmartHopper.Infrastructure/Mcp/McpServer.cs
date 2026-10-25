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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
namespace SmartHopper.Infrastructure.Mcp
{
    /// <summary>
    /// Local HTTP MCP server. Serves <c>http://127.0.0.1:&lt;port&gt;/mcp</c> over a raw
    /// <see cref="TcpListener"/> bound to <see cref="IPAddress.Loopback"/>, rejects
    /// non-loopback peers and cross-origin browser requests, and forwards every
    /// JSON-RPC request to a <see cref="JsonRpcDispatcher"/>. When
    /// <see cref="McpServerOptions.AllowExternalConnections"/> is set it binds all
    /// interfaces instead — allowed only together with a bearer token — and serves
    /// other machines on the network via <see cref="ExternalUrl"/>.
    /// </summary>
    /// <remarks>
    /// A raw socket is used instead of <see cref="HttpListener"/> on purpose: on
    /// Windows every <see cref="HttpListener"/> in the process shares the same
    /// http.sys request-queue handle, so stopping any unrelated listener faults
    /// this server's pending accepts (and stopping this server would fault theirs).
    /// The raw listener has no shared process state and needs no URL ACL.
    /// </remarks>
    public sealed class McpServer : IDisposable
    {
        private const string Endpoint = "/mcp";
        private const string HealthEndpoint = "/health";
        private const int MaxHeaderBytes = 16 * 1024;
        private const int MaxBodyBytes = 256 * 1024;
        private const int MaxConcurrentRequests = 16;
        private const int MaxConsecutiveAcceptErrors = 20;
        private const int AcceptErrorBackoffMs = 100;
        private const int IoTimeoutMs = 15 * 1000;
        private static readonly byte[] HeaderTerminator = { (byte)'\r', (byte)'\n', (byte)'\r', (byte)'\n' };

        private readonly McpServerOptions options;
        private readonly JsonRpcDispatcher dispatcher;
        private readonly SemaphoreSlim requestSlots = new SemaphoreSlim(MaxConcurrentRequests, MaxConcurrentRequests);
        private TcpListener? listener;
        private CancellationTokenSource? cancellation;
        private Task? acceptLoop;
        private int started;
        private int acceptErrors;
        private volatile bool running;
        private HashSet<IPAddress>? localAddresses;

        /// <summary>
        /// Initializes a new instance of the <see cref="McpServer"/> class.
        /// </summary>
        public McpServer(McpServerOptions options)
            : this(options, BuildDefaultDispatcher(options))
        {
        }

        /// <summary>
        /// Builds the default <see cref="JsonRpcDispatcher"/> with static MCP resources and prompts.
        /// </summary>
        private static JsonRpcDispatcher BuildDefaultDispatcher(McpServerOptions options)
        {
            var adapter = new AIToolMcpAdapter(options);
            var resourceProvider = new StaticMcpResourceProvider();
            var promptProvider = new StaticMcpPromptProvider();
            return new JsonRpcDispatcher(options, adapter, resourceProvider, promptProvider);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="McpServer"/> class with a custom dispatcher.
        /// </summary>
        public McpServer(McpServerOptions options, JsonRpcDispatcher dispatcher)
        {
            this.options = options ?? throw new ArgumentNullException(nameof(options));
            this.dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        }

        /// <summary>
        /// Gets a value indicating whether the server is currently accepting requests.
        /// Reflects the accept loop's real state — a loop that exited abnormally
        /// reports <c>false</c> instead of leaving a dead server looking alive.
        /// </summary>
        public bool IsRunning => this.started == 1 && this.running;

        /// <summary>Gets the configured loopback URL the server listens on.</summary>
        public string Url => $"http://127.0.0.1:{this.options.Port}{Endpoint}";

        /// <summary>
        /// Gets the LAN-reachable URL when <see cref="McpServerOptions.AllowExternalConnections"/>
        /// is set and a non-loopback IPv4 address is available; <c>null</c> otherwise.
        /// The loopback <see cref="Url"/> keeps working in either mode.
        /// </summary>
        public string? ExternalUrl
        {
            get
            {
                if (!this.options.AllowExternalConnections)
                {
                    return null;
                }

                var address = PrimaryLanAddress();
                return address == null ? null : $"http://{address}:{this.options.Port}{Endpoint}";
            }
        }

        /// <summary>Gets the options this server was started with (read-only copy).</summary>
        internal McpServerOptions Options => this.options;

        /// <summary>
        /// Starts the server. Subsequent calls are no-ops.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the port is unavailable, or when external connections are
        /// allowed without a bearer token — remote access without authentication is
        /// never permitted.
        /// </exception>
        public void Start()
        {
            if (Interlocked.Exchange(ref this.started, 1) == 1)
            {
                return;
            }

            if (this.options.AllowExternalConnections && string.IsNullOrEmpty(this.options.BearerToken))
            {
                Interlocked.Exchange(ref this.started, 0);
                throw new InvalidOperationException(
                    "Allow external connections requires a Bearer Token — remote access without authentication is not permitted.");
            }

            try
            {
                this.cancellation = new CancellationTokenSource();
                this.localAddresses = this.options.AllowExternalConnections ? BuildLocalAddresses() : null;
                var bindAddress = this.options.AllowExternalConnections ? IPAddress.Any : IPAddress.Loopback;
                this.listener = new TcpListener(bindAddress, this.options.Port);
                this.listener.Start();
            }
            catch (Exception ex)
            {
                this.listener = null;
                this.cancellation?.Dispose();
                this.cancellation = null;
                Interlocked.Exchange(ref this.started, 0);
                throw new InvalidOperationException($"Failed to start MCP listener on port {this.options.Port}: {ex.Message}", ex);
            }

            Debug.WriteLine($"[Mcp] Server listening on {this.Url}");
            this.acceptLoop = Task.Run(() => this.AcceptLoopAsync(this.cancellation.Token));
        }

        /// <summary>
        /// Stops the server. Subsequent calls are no-ops.
        /// </summary>
        /// <remarks>
        /// Never blocks: <see cref="TcpListener.Stop"/> faults the pending accept
        /// immediately and the accept loop exits on its own. This matters because
        /// <see cref="McpServerLifecycle.Release"/> invokes this on the Grasshopper
        /// solve thread.
        /// </remarks>
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
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Mcp] Error stopping listener: {ex.Message}");
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
            this.running = true;
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    TcpClient client;
                    try
                    {
                        client = await this.listener!.AcceptTcpClientAsync(ct).ConfigureAwait(false);
                    }
                    catch (ObjectDisposedException)
                    {
                        break;
                    }
                    catch (SocketException) when (ct.IsCancellationRequested || this.started == 0)
                    {
                        break;
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        var errors = Interlocked.Increment(ref this.acceptErrors);
                        Debug.WriteLine($"[Mcp] Accept error: {ex.Message}");
                        if (errors >= MaxConsecutiveAcceptErrors)
                        {
                            Debug.WriteLine("[Mcp] Accept loop stopped after repeated errors; disable and re-enable the component to restart the server.");
                            break;
                        }

                        try
                        {
                            await Task.Delay(AcceptErrorBackoffMs, ct).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }

                        continue;
                    }

                    Interlocked.Exchange(ref this.acceptErrors, 0);

                    if (!this.requestSlots.Wait(0))
                    {
                        _ = Task.Run(() => RejectRequestAsync(client));
                        continue;
                    }

                    _ = Task.Run(() => this.HandleRequestAsync(client, ct));
                }
            }
            finally
            {
                this.running = false;
            }
        }

        private async Task HandleRequestAsync(TcpClient client, CancellationToken ct)
        {
            try
            {
                var head = await ReadRequestHeadAsync(client, ct).ConfigureAwait(false);
                if (head == null)
                {
                    await WriteStatusAsync(client, 400, "Malformed request", ct).ConfigureAwait(false);
                    return;
                }

                // Reject non-loopback peers as a defence-in-depth measure even though the
                // listener is already bound to 127.0.0.1.
                var remote = (client.Client.RemoteEndPoint as IPEndPoint)?.Address;
                if (remote == null || !IPAddress.IsLoopback(remote))
                {
                    Debug.WriteLine($"[Mcp] Rejected non-loopback request from {remote}");
                    await WriteStatusAsync(client, 403, "Non-loopback origin", ct).ConfigureAwait(false);
                    return;
                }

                // A non-loopback Host header indicates DNS rebinding; a non-loopback
                // Origin/Referer indicates a browser cross-origin fetch. MCP clients
                // send neither — or loopback values only.
                if (!IsAllowedHost(head) || !IsAllowedOrigin(head))
                {
                    Debug.WriteLine("[Mcp] Rejected cross-origin request");
                    await WriteStatusAsync(client, 403, "Cross-origin requests are not allowed", ct).ConfigureAwait(false);
                    return;
                }

                var path = RequestPath(head.Target);
                if (string.Equals(head.Method, "GET", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(path, HealthEndpoint, StringComparison.OrdinalIgnoreCase))
                {
                    await WriteJsonAsync(client, 200, "{\"status\":\"ok\"}", ct).ConfigureAwait(false);
                    return;
                }

                if (!string.Equals(head.Method, "POST", StringComparison.OrdinalIgnoreCase))
                {
                    await WriteStatusAsync(client, 405, "POST required", ct).ConfigureAwait(false);
                    return;
                }

                if (!string.Equals(path, Endpoint, StringComparison.OrdinalIgnoreCase))
                {
                    await WriteStatusAsync(client, 404, "Not found", ct).ConfigureAwait(false);
                    return;
                }

                if (!this.AuthorizeRequest(head, out var authError))
                {
                    Debug.WriteLine($"[Mcp] Rejected unauthorized request: {authError}");
                    await WriteStatusAsync(client, 401, authError, ct).ConfigureAwait(false);
                    return;
                }

                if (head.ContentLength < 0)
                {
                    // Requiring an explicit length avoids read-until-close hangs on
                    // clients that never finish the body.
                    await WriteStatusAsync(client, 411, "Content-Length required", ct).ConfigureAwait(false);
                    return;
                }

                if (head.ContentLength > MaxBodyBytes)
                {
                    await WriteStatusAsync(client, 413, "Request body too large", ct).ConfigureAwait(false);
                    return;
                }

                var body = await ReadBodyAsync(client, head, ct).ConfigureAwait(false);
                if (body == null)
                {
                    await WriteStatusAsync(client, 400, "Malformed request body", ct).ConfigureAwait(false);
                    return;
                }

                var responseBody = await this.dispatcher.DispatchAsync(body, ct).ConfigureAwait(false);
                if (responseBody == null)
                {
                    // JSON-RPC notification — 204 with an empty body.
                    await WriteJsonAsync(client, 204, string.Empty, ct).ConfigureAwait(false);
                    return;
                }

                await WriteJsonAsync(client, 200, responseBody, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Server stopping, or the client stalled past the I/O timeout —
                // either way the connection is torn down in finally.
                Debug.WriteLine(ct.IsCancellationRequested
                    ? "[Mcp] Request aborted: server stopping"
                    : "[Mcp] Request aborted: client I/O timed out");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Mcp] Request error: {ex.Message}");
                try
                {
                    await WriteStatusAsync(client, 500, "Internal server error", CancellationToken.None).ConfigureAwait(false);
                }
                catch
                {
                    // Socket may already be unusable.
                }
            }
            finally
            {
                this.requestSlots.Release();
                try
                {
                    client.Dispose();
                }
                catch
                {
                }
            }
        }

        /// <summary>
        /// Writes a 503 to a connection accepted while every request slot was in use.
        /// Runs outside a request slot — deliberately never touches
        /// <see cref="requestSlots"/>.
        /// </summary>
        private static async Task RejectRequestAsync(TcpClient client)
        {
            try
            {
                await WriteStatusAsync(client, 503, "Server busy", CancellationToken.None).ConfigureAwait(false);
            }
            catch
            {
            }
            finally
            {
                try
                {
                    client.Dispose();
                }
                catch
                {
                }
            }
        }

        private bool AuthorizeRequest(HttpRequestHead head, out string error)
        {
            error = string.Empty;
            var expected = this.options.BearerToken;
            if (string.IsNullOrEmpty(expected))
            {
                return true;
            }

            if (!head.Headers.TryGetValue("Authorization", out var header)
                || string.IsNullOrEmpty(header)
                || !header.StartsWith("Bearer ", StringComparison.Ordinal))
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

        /// <summary>
        /// DNS-rebinding guard: a <c>Host</c> header must identify this machine —
        /// a loopback address, <c>localhost</c>, the machine hostname, or (when
        /// external connections are allowed) one of the machine's own interface
        /// addresses. Raw clients that omit Host pass.
        /// </summary>
        private bool IsAllowedHost(HttpRequestHead head)
        {
            if (!head.Headers.TryGetValue("Host", out var host) || string.IsNullOrWhiteSpace(host))
            {
                return true;
            }

            if (!Uri.TryCreate("http://" + host.Trim(), UriKind.Absolute, out var uri))
            {
                return false;
            }

            var name = uri.Host;
            if (IPAddress.TryParse(name, out var ip))
            {
                if (IPAddress.IsLoopback(ip))
                {
                    return true;
                }

                var mapped = ip.IsIPv4MappedToIPv6 ? ip.MapToIPv4() : ip;
                return this.localAddresses != null && this.localAddresses.Contains(mapped);
            }

            return string.Equals(name, "localhost", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, Dns.GetHostName(), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Browser-CSRF guard: <c>fetch()</c> POSTs always carry an <c>Origin</c>
        /// (and navigations a <c>Referer</c>); MCP clients send neither. Any explicit
        /// non-loopback origin is rejected, so a website cannot drive the tool surface
        /// even when no bearer token is configured.
        /// </summary>
        private static bool IsAllowedOrigin(HttpRequestHead head)
        {
            if (head.Headers.TryGetValue("Origin", out var origin) && !IsLoopbackUri(origin))
            {
                return false;
            }

            if (head.Headers.TryGetValue("Referer", out var referer) && !IsLoopbackUri(referer))
            {
                return false;
            }

            return true;
        }

        private static bool IsLoopbackUri(string value)
        {
            return Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.IsLoopback;
        }

        /// <summary>
        /// The machine's own IPv4 interface addresses, computed once at start —
        /// used by the Host guard to decide whether a <c>Host</c> header names this
        /// machine when external connections are allowed.
        /// </summary>
        private static HashSet<IPAddress> BuildLocalAddresses()
        {
            var addresses = new HashSet<IPAddress> { IPAddress.Loopback, IPAddress.IPv6Loopback };
            try
            {
                foreach (var address in Dns.GetHostAddresses(Dns.GetHostName()))
                {
                    addresses.Add(address);
                }
            }
            catch
            {
                // Name resolution unavailable — only literal loopback Hosts pass.
            }

            return addresses;
        }

        /// <summary>
        /// Resolves the primary outbound IPv4 address for <see cref="ExternalUrl"/>.
        /// A UDP "connect" picks the default-route interface without sending traffic;
        /// falls back to the first non-loopback DNS address when there is no route.
        /// </summary>
        private static IPAddress? PrimaryLanAddress()
        {
            try
            {
                using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                socket.Connect("8.8.8.8", 53);
                return (socket.LocalEndPoint as IPEndPoint)?.Address;
            }
            catch
            {
            }

            try
            {
                return Array.Find(
                    Dns.GetHostAddresses(Dns.GetHostName()),
                    a => a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a));
            }
            catch
            {
                return null;
            }
        }

        private static string RequestPath(string target)
        {
            if (Uri.TryCreate(target, UriKind.Absolute, out var uri))
            {
                return uri.AbsolutePath;
            }

            var query = target.IndexOf('?');
            var path = query >= 0 ? target.Substring(0, query) : target;
            return path.Length == 0 ? "/" : path;
        }

        /// <summary>
        /// Reads the request line and headers up to <see cref="HeaderTerminator"/>
        /// (bounded by <see cref="MaxHeaderBytes"/>). Returns <c>null</c> for malformed
        /// or truncated input. Bytes already received past the terminator are kept in
        /// <see cref="HttpRequestHead.BodyPrefix"/> for the body read.
        /// </summary>
        private static async Task<HttpRequestHead?> ReadRequestHeadAsync(TcpClient client, CancellationToken ct)
        {
            using var io = IoTimeout(ct);
            var stream = client.GetStream();
            var chunk = new byte[8192];
            var data = new List<byte>(8192);
            var marker = -1;
            while (data.Count <= MaxHeaderBytes)
            {
                int read;
                try
                {
                    read = await stream.ReadAsync(chunk.AsMemory(0, chunk.Length), io.Token).ConfigureAwait(false);
                }
                catch (IOException)
                {
                    return null;
                }

                if (read <= 0)
                {
                    break;
                }

                data.AddRange(new ArraySegment<byte>(chunk, 0, read));
                marker = IndexOf(data, HeaderTerminator);
                if (marker >= 0)
                {
                    break;
                }
            }

            if (marker < 0)
            {
                return null;
            }

            var head = ParseRequestHead(Encoding.ASCII.GetString(data.GetRange(0, marker).ToArray()));
            if (head != null)
            {
                var prefixStart = marker + HeaderTerminator.Length;
                var prefixLen = data.Count - prefixStart;
                if (prefixLen > 0)
                {
                    head.BodyPrefix = data.GetRange(prefixStart, prefixLen).ToArray();
                }
            }

            return head;
        }

        private static HttpRequestHead? ParseRequestHead(string text)
        {
            var lines = text.Split(new[] { "\r\n" }, StringSplitOptions.None);
            if (lines.Length == 0)
            {
                return null;
            }

            var parts = lines[0].Split(' ');
            if (parts.Length < 3 || parts[0].Length == 0 || parts[1].Length == 0)
            {
                return null;
            }

            var head = new HttpRequestHead
            {
                Method = parts[0].ToUpperInvariant(),
                Target = parts[1],
            };
            for (var i = 1; i < lines.Length; i++)
            {
                var line = lines[i];
                if (line.Length == 0)
                {
                    break;
                }

                var colon = line.IndexOf(':');
                if (colon <= 0)
                {
                    continue;
                }

                head.Headers[line.Substring(0, colon).Trim()] = line.Substring(colon + 1).Trim();
            }

            if (head.Headers.TryGetValue("Content-Length", out var lengthText)
                && long.TryParse(lengthText, out var length)
                && length >= 0)
            {
                head.ContentLength = length;
            }

            return head;
        }

        /// <summary>
        /// Reads exactly <see cref="HttpRequestHead.ContentLength"/> body bytes,
        /// accounting for the bytes already buffered past the header terminator.
        /// </summary>
        private static async Task<string?> ReadBodyAsync(TcpClient client, HttpRequestHead head, CancellationToken ct)
        {
            using var io = IoTimeout(ct);
            var length = (int)head.ContentLength;
            var body = new byte[length];
            var prefixLen = Math.Min(head.BodyPrefix.Length, length);
            Array.Copy(head.BodyPrefix, 0, body, 0, prefixLen);
            var offset = prefixLen;
            while (offset < length)
            {
                int read;
                try
                {
                    read = await client.GetStream().ReadAsync(body.AsMemory(offset, length - offset), io.Token).ConfigureAwait(false);
                }
                catch (IOException)
                {
                    return null;
                }

                if (read <= 0)
                {
                    return null;
                }

                offset += read;
            }

            return Encoding.UTF8.GetString(body);
        }

        /// <summary>
        /// Cancellation token that fires on either server shutdown or the per-request
        /// I/O timeout. Async socket operations ignore <c>Socket.ReceiveTimeout</c>,
        /// so the timeout must come through cancellation.
        /// </summary>
        private static CancellationTokenSource IoTimeout(CancellationToken ct)
        {
            var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
            linked.CancelAfter(IoTimeoutMs);
            return linked;
        }

        private static int IndexOf(List<byte> data, byte[] pattern)
        {
            for (var i = 0; i <= data.Count - pattern.Length; i++)
            {
                var match = true;
                for (var j = 0; j < pattern.Length; j++)
                {
                    if (data[i + j] != pattern[j])
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                {
                    return i;
                }
            }

            return -1;
        }

        private static async Task WriteJsonAsync(TcpClient client, int statusCode, string body, CancellationToken ct)
        {
            using var io = IoTimeout(ct);
            var bodyBytes = Encoding.UTF8.GetBytes(body ?? string.Empty);
            var header = Encoding.ASCII.GetBytes(
                $"HTTP/1.1 {statusCode} {StatusText(statusCode)}\r\n" +
                "Content-Type: application/json; charset=utf-8\r\n" +
                $"Content-Length: {bodyBytes.Length}\r\n" +
                "Connection: close\r\n\r\n");
            var stream = client.GetStream();
            await stream.WriteAsync(header.AsMemory(0, header.Length), io.Token).ConfigureAwait(false);
            await stream.WriteAsync(bodyBytes.AsMemory(0, bodyBytes.Length), io.Token).ConfigureAwait(false);
        }

        private static Task WriteStatusAsync(TcpClient client, int statusCode, string message, CancellationToken ct)
        {
            return WriteJsonAsync(client, statusCode, $"{{\"error\":\"{Escape(message)}\"}}", ct);
        }

        private static string StatusText(int statusCode)
        {
            switch (statusCode)
            {
                case 200: return "OK";
                case 204: return "No Content";
                case 400: return "Bad Request";
                case 401: return "Unauthorized";
                case 403: return "Forbidden";
                case 404: return "Not Found";
                case 405: return "Method Not Allowed";
                case 411: return "Length Required";
                case 413: return "Payload Too Large";
                case 500: return "Internal Server Error";
                case 503: return "Service Unavailable";
                default: return "Status";
            }
        }

        private static string Escape(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            return text.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private sealed class HttpRequestHead
        {
            public string Method { get; set; } = string.Empty;

            public string Target { get; set; } = string.Empty;

            public Dictionary<string, string> Headers { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            public long ContentLength { get; set; } = -1;

            public byte[] BodyPrefix { get; set; } = Array.Empty<byte>();
        }
    }
}
