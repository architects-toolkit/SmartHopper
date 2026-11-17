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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace SmartHopper.Infrastructure.Networking
{
    /// <summary>Creates consent-gated, bounded HTTP transports for AI-controlled external data reads.</summary>
    public static class ExternalDataHttpClient
    {
        private static readonly HttpRequestOptionsKey<Func<Uri, CancellationToken, Task>> ScopedAuthorization =
            new HttpRequestOptionsKey<Func<Uri, CancellationToken, Task>>("SmartHopper.SourceAuthorization");
        /// <summary>Gets the maximum decompressed response size accepted by external retrievals.</summary>
        public const int MaximumResponseBytes = 16 * 1024 * 1024;

        /// <summary>Gets or sets the host's configurable per-response limit; tests default to 16 MiB.</summary>
        public static Func<long> ResponseLimitBytes { get; set; } = () => MaximumResponseBytes;

        /// <summary>
        /// Creates a client with explicit redirect approval, no ambient cookies/proxy credentials,
        /// and socket connections pinned to the actual address checked by the transport.
        /// Provider API, login and other configured service traffic do not use this factory.
        /// </summary>
        public static HttpClient Create()
        {
            var sockets = new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                UseCookies = false,
                UseProxy = false,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                PooledConnectionLifetime = TimeSpan.FromMinutes(1),
                ConnectCallback = ConnectAsync,
            };
            return new HttpClient(new SourceHandler(sockets)) { Timeout = TimeSpan.FromMinutes(5) };
        }

        private static async ValueTask<Stream> ConnectAsync(SocketsHttpConnectionContext context, CancellationToken cancellationToken)
        {
            var uri = context.InitialRequestMessage.RequestUri!;
            // Request-origin permission is checked before DNS by SourceHandler. Resolve once
            // here and connect to that exact IP, never re-resolving after validation.
            var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host)
                .WaitAsync(cancellationToken).ConfigureAwait(false);
            foreach (var address in addresses.OrderBy(IsPrivateAddress))
            {
                if (IsForbiddenAddress(address))
                {
                    continue;
                }

                if (IsPrivateAddress(address))
                {
                    // A hostname approval never implicitly grants arbitrary private IPs.
                    // Show a separate exact endpoint approval if its DNS moves into a LAN.
                    var endpoint = new UriBuilder(uri.Scheme, address.ToString(), uri.Port).Uri;
                    var authorize = context.InitialRequestMessage.Options.TryGetValue(ScopedAuthorization, out var captured)
                        ? captured : ExternalSourceAccess.RequireAsync;
                    await authorize(endpoint, cancellationToken).ConfigureAwait(false);
                }

                var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                try
                {
                    using var connectTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    connectTimeout.CancelAfter(TimeSpan.FromSeconds(30));
                    await socket.ConnectAsync(new IPEndPoint(address, context.DnsEndPoint.Port), connectTimeout.Token).ConfigureAwait(false);
                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch (SocketException)
                {
                    socket.Dispose();
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    socket.Dispose();
                }
                catch
                {
                    socket.Dispose();
                    throw;
                }
            }

            throw new HttpRequestException("The external source has no permitted network destination.");
        }

        /// <summary>Classifies destinations that require their own explicit private endpoint grant.</summary>
        internal static bool IsPrivateAddress(IPAddress address)
        {
            var embedded = EmbeddedIPv4(address);
            if (embedded != null)
            {
                return IsPrivateAddress(embedded);
            }

            var bytes = address.GetAddressBytes();
            if (bytes.Length == 4)
            {
                return bytes[0] == 0 || bytes[0] == 10 || bytes[0] == 127 ||
                    (bytes[0] == 100 && bytes[1] >= 64 && bytes[1] <= 127) ||
                    (bytes[0] == 169 && bytes[1] == 254) ||
                    (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) ||
                    (bytes[0] == 192 && (bytes[1] == 168 || bytes[1] == 0)) ||
                    (bytes[0] == 198 && (bytes[1] == 18 || bytes[1] == 19 || bytes[1] == 51)) ||
                    (bytes[0] == 203 && bytes[1] == 0 && bytes[2] == 113) || bytes[0] >= 224;
            }

            return (bytes[0] & 0xe0) != 0x20 ||
                (bytes[0] == 0x20 && bytes[1] == 0x02) ||
                (bytes[0] == 0x20 && bytes[1] == 0x01 && bytes[2] == 0 && bytes[3] == 0) ||
                (bytes[0] == 0x20 && bytes[1] == 0x01 && bytes[2] == 0x0d && bytes[3] == 0xb8);
        }

        /// <summary>Never permits unspecified, multicast or known instance-metadata endpoints.</summary>
        internal static bool IsForbiddenAddress(IPAddress address)
        {
            var embedded = EmbeddedIPv4(address);
            if (embedded != null)
            {
                return IsForbiddenAddress(embedded);
            }

            var bytes = address.GetAddressBytes();
            return address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any) ||
                address.IsIPv6Multicast ||
                (bytes.Length == 4 && (bytes[0] >= 224 ||
                    address.Equals(IPAddress.Parse("169.254.169.254")) ||
                    address.Equals(IPAddress.Parse("100.100.100.200")) ||
                    address.Equals(IPAddress.Parse("168.63.129.16")))) ||
                address.Equals(IPAddress.Parse("fd00:ec2::254"));
        }

        /// <summary>Normalizes mapped addresses and the standard DNS64/NAT64 IPv4 prefix.</summary>
        private static IPAddress? EmbeddedIPv4(IPAddress address)
        {
            if (address.IsIPv4MappedToIPv6)
            {
                return address.MapToIPv4();
            }

            var bytes = address.GetAddressBytes();
            if (bytes.Length == 16 && bytes[0] == 0 && bytes[1] == 0x64 &&
                bytes[2] == 0xff && bytes[3] == 0x9b && bytes.Skip(4).Take(8).All(value => value == 0))
            {
                return new IPAddress(bytes.Skip(12).ToArray());
            }

            return null;
        }

        /// <summary>Handler boundary also used with a synthetic transport by network-free tests.</summary>
        internal sealed class SourceHandler : DelegatingHandler
        {
            private readonly Func<Uri, CancellationToken, Task> authorize;
            private readonly object cookieLock = new object();
            private readonly Dictionary<string, CookieContainer> cookies = new Dictionary<string, CookieContainer>(StringComparer.Ordinal);

            internal SourceHandler(HttpMessageHandler transport, Func<Uri, CancellationToken, Task>? authorize = null)
                : base(transport)
            {
                this.authorize = authorize ?? ExternalSourceAccess.RequireAsync;
            }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                if (request.Method != HttpMethod.Get && request.Method != HttpMethod.Head)
                {
                    throw new HttpRequestException("External data retrieval supports GET and HEAD only.");
                }

                using var scope = ExternalSourceAccess.BeginScope();
                var scopedAuthorization = ExternalSourceAccess.CaptureAuthorization();
                var current = request;
                try
                {
                    for (var redirect = 0; redirect <= 5; redirect++)
                    {
                        await this.authorize(current.RequestUri!, cancellationToken).ConfigureAwait(false);
                        current.Options.Set(ScopedAuthorization, scopedAuthorization);
                        var cookieJar = this.CookieJar(current.RequestUri!);
                        var cookieHeader = cookieJar?.GetCookieHeader(current.RequestUri!);
                        if (!current.Headers.Contains("Cookie") && !string.IsNullOrEmpty(cookieHeader))
                        {
                            current.Headers.TryAddWithoutValidation("Cookie", cookieHeader);
                        }

                        var response = await base.SendAsync(current, cancellationToken).ConfigureAwait(false);
                        if (cookieJar != null && response.Headers.TryGetValues("Set-Cookie", out var responseCookies))
                        {
                            foreach (var cookie in responseCookies)
                            {
                                try
                                {
                                    cookieJar.SetCookies(current.RequestUri!, cookie);
                                }
                                catch (CookieException)
                                {
                                    // Match normal client behavior: malformed server cookies are ignored.
                                }
                            }
                        }
                        var status = (int)response.StatusCode;
                        if (status == 301 || status == 302 || status == 303 || status == 307 || status == 308)
                        {
                            var location = response.Headers.Location;
                            response.Dispose();
                            if (location == null || redirect == 5)
                            {
                                throw new HttpRequestException("External source redirect is missing or exceeds the redirect limit.");
                            }

                            var next = new Uri(current.RequestUri!, location);
                            // Never forward cookies, authorization, referer or other request headers.
                            if (current != request)
                            {
                                current.Dispose();
                            }

                            current = new HttpRequestMessage(request.Method, next);
                            continue;
                        }

                        try
                        {
                            await BoundContentAsync(response, cancellationToken).ConfigureAwait(false);
                            return response;
                        }
                        catch
                        {
                            response.Dispose();
                            throw;
                        }
                    }
                }
                finally
                {
                    if (current != request)
                    {
                        current.Dispose();
                    }
                }

                throw new HttpRequestException("External source redirect limit exceeded.");
            }

            private CookieContainer? CookieJar(Uri uri)
            {
                var origin = ExternalSourceAllowlist.Origin(uri);
                lock (this.cookieLock)
                {
                    if (this.cookies.TryGetValue(origin, out var jar))
                    {
                        return jar;
                    }

                    if (this.cookies.Count >= 32)
                    {
                        return null;
                    }

                    jar = new CookieContainer(32, 32, 4096);
                    this.cookies.Add(origin, jar);
                    return jar;
                }
            }

            private static async Task BoundContentAsync(HttpResponseMessage response, CancellationToken cancellationToken)
            {
                var limit = ResponseLimitBytes();
                if (limit < 1024 * 1024 || limit > 1024L * 1024 * 1024)
                {
                    throw new HttpRequestException("The configured external download limit is invalid.");
                }

                var original = response.Content;
                if (original.Headers.ContentLength > limit)
                {
                    throw new HttpRequestException("External response exceeds the download limit.");
                }

                using var content = await original.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                using var buffer = new MemoryStream();
                var chunk = new byte[32 * 1024];
                int count;
                while ((count = await content.ReadAsync(chunk, cancellationToken).ConfigureAwait(false)) != 0)
                {
                    if (buffer.Length + count > limit)
                    {
                        throw new HttpRequestException("External response exceeds the download limit.");
                    }

                    buffer.Write(chunk, 0, count);
                }

                var replacement = new ByteArrayContent(buffer.ToArray());
                foreach (var header in original.Headers.Where(header => header.Key != "Content-Length"))
                {
                    replacement.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }

                response.Content = replacement;
                original.Dispose();
            }
        }
    }
}
