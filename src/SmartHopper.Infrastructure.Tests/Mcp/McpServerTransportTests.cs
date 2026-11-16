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

namespace SmartHopper.Infrastructure.Tests.Mcp
{
    using System;
    using System.IO;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading.Tasks;
    using SmartHopper.Infrastructure.Mcp;
    using Xunit;

    /// <summary>
    /// Exercises the <see cref="McpServer"/> raw-socket transport end to end: status
    /// codes, bearer auth, cross-origin/host guards, and request-size limits. Each
    /// test binds its own ephemeral port, so no test shares listener state.
    /// </summary>
    public class McpServerTransportTests
    {
        private static int FreePort()
        {
            var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            var port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            return port;
        }

        private static McpServer StartServer(McpServerOptions options, out int port)
        {
            var dispatcher = new JsonRpcDispatcher(options, new AIToolMcpAdapter(options));
            var server = new McpServer(options, dispatcher);
            server.Start();
            port = options.Port;
            return server;
        }

        /// <summary>
        /// Sends a raw HTTP request and reads the whole response. The server always
        /// answers with <c>Connection: close</c>, so end-of-stream means done.
        /// </summary>
        private static async Task<string> SendRawAsync(int port, string request)
        {
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, port);
            var stream = client.GetStream();
            var bytes = Encoding.ASCII.GetBytes(request);
            await stream.WriteAsync(bytes, 0, bytes.Length);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            return await reader.ReadToEndAsync();
        }

        private static string Post(string path, string? body = null, string? extraHeaders = null, int port = 0)
        {
            var headers = $"POST {path} HTTP/1.1\r\nHost: 127.0.0.1:{port}\r\n";
            if (extraHeaders != null)
            {
                headers += extraHeaders;
            }

            if (body != null)
            {
                headers += $"Content-Length: {Encoding.UTF8.GetByteCount(body)}\r\n";
            }

            return headers + "\r\n" + (body ?? string.Empty);
        }

        [Fact]
        public async Task Health_ReturnsOk()
        {
            var port = FreePort();
            var options = new McpServerOptions { Port = port };
            using var server = StartServer(options, out _);

            var response = await SendRawAsync(port, $"GET /health HTTP/1.1\r\nHost: 127.0.0.1:{port}\r\n\r\n");

            Assert.StartsWith("HTTP/1.1 200", response);
            Assert.Contains("{\"status\":\"ok\"}", response);
        }

        [Fact]
        public async Task Ping_RoundTrips_ThroughDispatcher()
        {
            var port = FreePort();
            var options = new McpServerOptions { Port = port };
            using var server = StartServer(options, out _);

            var response = await SendRawAsync(
                port,
                Post("/mcp", "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"ping\"}", port: port));

            Assert.StartsWith("HTTP/1.1 200", response);
            Assert.Contains("\"id\":1", response);
        }

        [Fact]
        public async Task Get_OnMcp_Returns405()
        {
            var port = FreePort();
            var options = new McpServerOptions { Port = port };
            using var server = StartServer(options, out _);

            var response = await SendRawAsync(port, $"GET /mcp HTTP/1.1\r\nHost: 127.0.0.1:{port}\r\n\r\n");

            Assert.StartsWith("HTTP/1.1 405", response);
        }

        [Fact]
        public async Task Post_UnknownPath_Returns404()
        {
            var port = FreePort();
            var options = new McpServerOptions { Port = port };
            using var server = StartServer(options, out _);

            var response = await SendRawAsync(port, Post("/nope", "{}", port: port));

            Assert.StartsWith("HTTP/1.1 404", response);
        }

        [Fact]
        public async Task MissingBearer_Returns401_WhenTokenConfigured()
        {
            var port = FreePort();
            var options = new McpServerOptions { Port = port, BearerToken = "secret" };
            using var server = StartServer(options, out _);

            var noAuth = await SendRawAsync(port, Post("/mcp", "{}", port: port));
            var wrongAuth = await SendRawAsync(
                port,
                Post("/mcp", "{}", "Authorization: Bearer wrong\r\n", port));

            Assert.StartsWith("HTTP/1.1 401", noAuth);
            Assert.StartsWith("HTTP/1.1 401", wrongAuth);
        }

        [Fact]
        public async Task NonLoopbackOrigin_Returns403_EvenWithoutToken()
        {
            var port = FreePort();
            var options = new McpServerOptions { Port = port };
            using var server = StartServer(options, out _);

            var response = await SendRawAsync(
                port,
                Post("/mcp", "{}", "Origin: https://evil.example\r\n", port));

            Assert.StartsWith("HTTP/1.1 403", response);
        }

        [Fact]
        public async Task NonLoopbackHost_Returns403()
        {
            var port = FreePort();
            var options = new McpServerOptions { Port = port };
            using var server = StartServer(options, out _);

            var request = "POST /mcp HTTP/1.1\r\nHost: evil.example\r\nContent-Length: 2\r\n\r\n{}";
            var response = await SendRawAsync(port, request);

            Assert.StartsWith("HTTP/1.1 403", response);
        }

        [Fact]
        public async Task MissingContentLength_Returns411()
        {
            var port = FreePort();
            var options = new McpServerOptions { Port = port };
            using var server = StartServer(options, out _);

            var response = await SendRawAsync(
                port,
                $"POST /mcp HTTP/1.1\r\nHost: 127.0.0.1:{port}\r\n\r\n{{}}");

            Assert.StartsWith("HTTP/1.1 411", response);
        }

        [Fact]
        public async Task OversizedContentLength_Returns413_WithoutReadingBody()
        {
            var port = FreePort();
            var options = new McpServerOptions { Port = port };
            using var server = StartServer(options, out _);

            var request = $"POST /mcp HTTP/1.1\r\nHost: 127.0.0.1:{port}\r\nContent-Length: {256 * 1024 + 1}\r\n\r\n";
            var response = await SendRawAsync(port, request);

            Assert.StartsWith("HTTP/1.1 413", response);
        }

        [Fact]
        public async Task MalformedRequestLine_Returns400()
        {
            var port = FreePort();
            var options = new McpServerOptions { Port = port };
            using var server = StartServer(options, out _);

            var response = await SendRawAsync(port, "GARBAGE\r\n\r\n");

            Assert.StartsWith("HTTP/1.1 400", response);
        }

        [Fact]
        public async Task Listener_IsAlwaysBoundToLiteralLoopback()
        {
            var port = FreePort();
            var options = new McpServerOptions { Port = port, BearerToken = "synthetic-token" };
            using var server = StartServer(options, out _);

            Assert.Equal(IPAddress.Loopback, Assert.IsType<IPEndPoint>(server.BoundEndpoint).Address);
            Assert.Null(server.ExternalUrl);
            var health = await SendRawAsync(port, $"GET /health HTTP/1.1\r\nHost: 127.0.0.1:{port}\r\n\r\n");
            Assert.StartsWith("HTTP/1.1 200", health);

            var unauthenticated = await SendRawAsync(port, Post("/mcp", "{}", port: port));
            Assert.StartsWith("HTTP/1.1 401", unauthenticated);
        }

        [Theory]
        [InlineData("192.168.1.42")]
        [InlineData("203.0.113.1")]
        [InlineData("workstation.invalid")]
        public async Task NonLoopbackHost_IsRejected(string host)
        {
            var port = FreePort();
            var options = new McpServerOptions { Port = port, BearerToken = "synthetic-token" };
            using var server = StartServer(options, out _);

            var response = await SendRawAsync(
                port,
                $"POST /mcp HTTP/1.1\r\nHost: {host}:{port}\r\nContent-Length: 2\r\n\r\n{{}}");
            Assert.StartsWith("HTTP/1.1 403", response);
        }

        [Fact]
        public async Task Stop_ThenRestartOnSamePort_Works()
        {
            var port = FreePort();
            var options = new McpServerOptions { Port = port };

            using (var first = StartServer(options, out _))
            {
                var alive = await SendRawAsync(port, $"GET /health HTTP/1.1\r\nHost: 127.0.0.1:{port}\r\n\r\n");
                Assert.StartsWith("HTTP/1.1 200", alive);
                Assert.True(first.IsRunning);
            }

            using (var second = StartServer(options, out _))
            {
                var alive = await SendRawAsync(port, $"GET /health HTTP/1.1\r\nHost: 127.0.0.1:{port}\r\n\r\n");
                Assert.StartsWith("HTTP/1.1 200", alive);
            }
        }
    }
}
