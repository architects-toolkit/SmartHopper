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
using System.Threading;
using System.Threading.Tasks;
using SmartHopper.Infrastructure.Networking;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AICall.Core.Requests;
using Xunit;

namespace SmartHopper.Infrastructure.Tests
{
    /// <summary>Network-free permission, persistence, redirect and response-boundary regressions.</summary>
    public sealed class ExternalSourceAccessTests : IDisposable
    {
        private readonly string directory = Path.Combine(Path.GetTempPath(), "smarthopper-source-tests-" + Guid.NewGuid().ToString("N"));
        private readonly ExternalSourceAllowlist store;

        /// <summary>Initializes an isolated permissions file, never the user's real settings.</summary>
        public ExternalSourceAccessTests()
        {
            Directory.CreateDirectory(this.directory);
            this.store = new ExternalSourceAllowlist(Path.Combine(this.directory, "permissions.json"));
        }

        /// <summary>Always grants persist only canonical origins, survive restart and can be revoked.</summary>
        [Fact]
        public async Task AlwaysAllow_PersistsOnlyExactOrigin()
        {
            var uri = new Uri("https://EXAMPLE.com:443/private?token=not-a-real-secret#fragment");
            await ExternalSourceAccess.RequireAsync(uri, this.store,
                (_, _) => Task.FromResult(ExternalSourceDecision.AlwaysAllow), default);
            Assert.Equal(new[] { "https://example.com" }, this.store.GetOrigins());
            var saved = File.ReadAllText(Path.Combine(this.directory, "permissions.json"));
            Assert.DoesNotContain("private", saved);
            Assert.DoesNotContain("token", saved);
            var reopened = new ExternalSourceAllowlist(Path.Combine(this.directory, "permissions.json"));
            Assert.True(reopened.Contains(new Uri("https://example.com/another")));
            Assert.False(reopened.Contains(new Uri("http://example.com")));
            Assert.False(reopened.Contains(new Uri("https://example.com:8443")));
            Assert.False(reopened.Contains(new Uri("https://sub.example.com")));
            reopened.Revoke(new[] { "https://example.com" });
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                ExternalSourceAccess.RequireAsync(uri, this.store, null, default));
        }

        /// <summary>One-call grants do not persist, while nested retrieval helpers share them.</summary>
        [Fact]
        public async Task AllowOnce_IsScopedToOneRetrieval()
        {
            var count = 0;
            Task<ExternalSourceDecision> Prompt(string _, CancellationToken __)
            {
                count++;
                return Task.FromResult(ExternalSourceDecision.AllowOnce);
            }

            var uri = new Uri("https://example.com/");
            using (ExternalSourceAccess.BeginScope())
            {
                await ExternalSourceAccess.RequireAsync(uri, this.store, Prompt, default);
                using (ExternalSourceAccess.BeginScope())
                {
                    await ExternalSourceAccess.RequireAsync(uri, this.store, Prompt, default);
                }
            }

            Assert.Equal(1, count);
            using (ExternalSourceAccess.BeginScope())
            {
                await ExternalSourceAccess.RequireAsync(uri, this.store, Prompt, default);
            }

            Assert.Equal(2, count);
            Assert.Empty(this.store.GetOrigins());
        }

        /// <summary>Unavailable approval prevents the transport from receiving even the first request.</summary>
        [Fact]
        public async Task NoPresenter_DeniesBeforeTransport()
        {
            var transport = new FakeTransport(_ => new HttpResponseMessage(HttpStatusCode.OK));
            using var client = new HttpClient(new ExternalDataHttpClient.SourceHandler(transport,
                (uri, token) => ExternalSourceAccess.RequireAsync(uri, this.store, null, token)));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => client.GetAsync("https://example.com/"));
            Assert.Equal(0, transport.Count);
        }

        /// <summary>Every redirect origin is authorized; denied destinations receive no request.</summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Redirect_RequiresDestinationPermissionAndDoesNotForwardSecrets(bool approve)
        {
            var targets = new List<string>();
            var transport = new FakeTransport(request =>
            {
                if (request.RequestUri!.Host == "first.example")
                {
                    var redirect = new HttpResponseMessage(HttpStatusCode.Found);
                    redirect.Headers.Location = new Uri("http://127.0.0.1:8765/private");
                    return redirect;
                }

                Assert.False(request.Headers.Contains("Authorization"));
                Assert.False(request.Headers.Contains("Cookie"));
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("approved") };
            });
            using var client = new HttpClient(new ExternalDataHttpClient.SourceHandler(transport, (uri, _) =>
            {
                targets.Add(ExternalSourceAllowlist.Origin(uri));
                return uri.Host == "first.example" || approve ? Task.CompletedTask :
                    Task.FromException(new UnauthorizedAccessException());
            }));
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://first.example/start");
            request.Headers.TryAddWithoutValidation("Authorization", "Bearer synthetic");
            request.Headers.TryAddWithoutValidation("Cookie", "synthetic=1");
            if (approve)
            {
                using var response = await client.SendAsync(request);
                Assert.Equal("approved", await response.Content.ReadAsStringAsync());
            }
            else
            {
                await Assert.ThrowsAsync<UnauthorizedAccessException>(() => client.SendAsync(request));
            }

            Assert.Equal(new[] { "https://first.example", "http://127.0.0.1:8765" }, targets);
            Assert.Equal(approve ? 2 : 1, transport.Count);
        }

        /// <summary>Fresh server-session cookies work within one origin, never across origin/port boundaries.</summary>
        [Theory]
        [InlineData("https://example.com/download", true)]
        [InlineData("https://example.com:8443/download", false)]
        [InlineData("https://other.example.com/download", false)]
        public async Task Redirect_IsolatesServerCookies(string destination, bool expectedCookie)
        {
            var transport = new FakeTransport(request =>
            {
                if (request.RequestUri!.AbsolutePath == "/start")
                {
                    var redirect = new HttpResponseMessage(HttpStatusCode.Found);
                    redirect.Headers.Location = new Uri(destination);
                    redirect.Headers.TryAddWithoutValidation("Set-Cookie", "session=synthetic; Path=/; Domain=example.com; Secure");
                    return redirect;
                }

                Assert.Equal(expectedCookie, request.Headers.Contains("Cookie"));
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("approved") };
            });
            using var client = new HttpClient(new ExternalDataHttpClient.SourceHandler(transport, (_, _) => Task.CompletedTask));
            using var response = await client.GetAsync("https://example.com/start");
            Assert.Equal(2, transport.Count);
        }

        /// <summary>Both declared and streamed responses are bounded before normal HttpClient buffering.</summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task OversizedResponse_IsRejected(bool unknownLength)
        {
            var bytes = new byte[ExternalDataHttpClient.MaximumResponseBytes + 1];
            var transport = new FakeTransport(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = unknownLength ? new StreamContent(new NonSeekableStream(bytes)) : new ByteArrayContent(bytes),
            });
            using var client = new HttpClient(new ExternalDataHttpClient.SourceHandler(transport, (_, _) => Task.CompletedTask));
            await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync("https://example.com"));
        }

        /// <summary>Outgoing URL images become inline data; providers never receive a fetchable URL.</summary>
        [Fact]
        public async Task Materializer_InlinesImagesAndPreservesBodyMetadata()
        {
            var original = new AIInteractionImage().WithVisionInput("https://example.com/private?token=synthetic");
            var body = AIBody.Empty with
            {
                Interactions = new[] { original },
                InteractionsNew = new[] { 0 },
                ToolFilter = "allowed-tool",
            };
            var request = new AIRequestCall { Body = body };
            await ExternalImageMaterializer.ApplyAsync(request, () =>
            {
                return new HttpClient(new ExternalDataHttpClient.SourceHandler(new FakeTransport(_ =>
                {
                    var png = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 };
                    var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(png) };
                    response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
                    return response;
                }), (_, _) => Task.CompletedTask));
            });
            var image = Assert.IsType<AIInteractionImage>(Assert.Single(request.Body.Interactions));
            Assert.Null(image.ImageUrl);
            Assert.Equal(Convert.ToBase64String(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }), image.ImageData);
            Assert.Equal("image/png", image.MimeType);
            Assert.Equal(body.ToolFilter, request.Body.ToolFilter);
            Assert.Equal(body.InteractionsNew, request.Body.InteractionsNew);
            Assert.NotNull(original.ImageUrl);
        }

        /// <summary>Materialization denial is fatal and never partially replaces the request body.</summary>
        [Fact]
        public async Task Materializer_DenialFailsClosed()
        {
            var request = new AIRequestCall
            {
                Body = AIBody.Empty with { Interactions = new[] { new AIInteractionImage().WithVisionInput("https://example.com/") } },
            };
            var body = request.Body;
            var transport = new FakeTransport(_ => throw new InvalidOperationException("Transport must not run."));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => ExternalImageMaterializer.ApplyAsync(request,
                () => new HttpClient(new ExternalDataHttpClient.SourceHandler(transport,
                    (_, _) => Task.FromException(new UnauthorizedAccessException())))));
            Assert.Same(body, request.Body);
            Assert.Equal(0, transport.Count);
        }

        /// <summary>Origin canonicalization rejects executable/file schemes and embedded credentials.</summary>
        [Theory]
        [InlineData("file:///private/image.png")]
        [InlineData("ftp://example.com/image.png")]
        [InlineData("https://user:password@example.com/image.png")]
        public void InvalidOrigin_IsRejected(string uri) =>
            Assert.Throws<ArgumentException>(() => ExternalSourceAllowlist.Origin(new Uri(uri)));

        /// <summary>Private and mapped addresses are never mistaken for ordinary public destinations.</summary>
        [Theory]
        [InlineData("127.0.0.1", true)]
        [InlineData("10.1.2.3", true)]
        [InlineData("172.16.0.1", true)]
        [InlineData("192.168.1.1", true)]
        [InlineData("169.254.169.254", true)]
        [InlineData("100.64.0.1", true)]
        [InlineData("::1", true)]
        [InlineData("::ffff:127.0.0.1", true)]
        [InlineData("64:ff9b::a00:1", true)]
        [InlineData("64:ff9b::808:808", false)]
        [InlineData("fc00::1", true)]
        [InlineData("fe80::1", true)]
        [InlineData("2002:7f00:1::", true)]
        [InlineData("8.8.8.8", false)]
        [InlineData("2606:4700:4700::1111", false)]
        public void PrivateAddressClassification_IsConservative(string address, bool expected) =>
            Assert.Equal(expected, ExternalDataHttpClient.IsPrivateAddress(IPAddress.Parse(address)));

        /// <summary>Metadata aliases remain forbidden even if an origin has been approved.</summary>
        [Theory]
        [InlineData("169.254.169.254")]
        [InlineData("::ffff:169.254.169.254")]
        [InlineData("64:ff9b::a9fe:a9fe")]
        [InlineData("fd00:ec2::254")]
        public void MetadataAliases_AreForbidden(string address) =>
            Assert.True(ExternalDataHttpClient.IsForbiddenAddress(IPAddress.Parse(address)));

        /// <inheritdoc/>
        public void Dispose() => Directory.Delete(this.directory, true);

        /// <summary>An explicit non-image Content-Type fails closed BEFORE any bytes are read.</summary>
        [Fact]
        public async Task Materializer_HtmlDeclaredPngSignature_IsRejected()
        {
            var png = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 };
            var stream = new TrackingStream(png);
            var image = new AIInteractionImage().WithVisionInput("https://example.com/attack");
            var transport = new FakeTransport(_ =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(stream) };
                response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/html");
                return response;
            });

            // Bare client so only the materializer's own pre-read MIME guard runs —
            // the bounded SourceHandler legitimately prefetches for its own checks.
            await Assert.ThrowsAsync<InvalidDataException>(() => ExternalImageMaterializer.MaterializeAsync(
                image, default,
                () => new HttpClient(transport)));
            Assert.Equal(0, stream.BytesRead);
        }

        /// <summary>Absent Content-Type is acceptable only when the signature matches.</summary>
        [Fact]
        public async Task Materializer_MissingMimeWithValidPng_IsAccepted()
        {
            var png = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10, 1, 2, 3 };
            var image = new AIInteractionImage().WithVisionInput("https://example.com/ok");
            var transport = new FakeTransport(_ =>
                new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(png) });

            var materialized = await ExternalImageMaterializer.MaterializeAsync(
                image, default,
                () => new HttpClient(new ExternalDataHttpClient.SourceHandler(transport, (_, _) => Task.CompletedTask)));

            Assert.Equal("image/png", materialized.MimeType);
            Assert.Equal(Convert.ToBase64String(png), materialized.ImageData);
        }

        /// <summary>Unsupported magic bytes are rejected even with no/ignored MIME.</summary>
        [Fact]
        public async Task Materializer_UnsupportedMagic_IsRejected()
        {
            var image = new AIInteractionImage().WithVisionInput("https://example.com/exe");
            var transport = new FakeTransport(_ =>
                new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[] { 77, 90, 1, 2, 3, 4 }) });

            await Assert.ThrowsAsync<InvalidDataException>(() => ExternalImageMaterializer.MaterializeAsync(
                image, default,
                () => new HttpClient(new ExternalDataHttpClient.SourceHandler(transport, (_, _) => Task.CompletedTask))));
        }

        /// <summary>Streaming reads stop at Max+1 even without a declared ContentLength.</summary>
        [Fact]
        public async Task Materializer_OversizeStream_IsRejected()
        {
            // PNG signature + filler beyond the 16 MiB cap.
            var bytes = new byte[16 * 1024 * 1024 + 2];
            bytes[0] = 137; bytes[1] = 80; bytes[2] = 78; bytes[3] = 71;
            var image = new AIInteractionImage().WithVisionInput("https://example.com/big");
            var stream = new TrackingStream(bytes) { CanSeekOverride = false };
            var transport = new FakeTransport(_ =>
            {
                var content = new StreamContent(stream);
                Assert.Null(content.Headers.ContentLength);
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
            });

            // Bare client exercises the materializer's own streaming bound — the
            // bounded SourceHandler path fails even earlier at the transport.
            await Assert.ThrowsAsync<InvalidDataException>(() => ExternalImageMaterializer.MaterializeAsync(
                image, default,
                () => new HttpClient(transport)));
            Assert.True(stream.BytesRead <= 16 * 1024 * 1024 + 1, $"BytesRead={stream.BytesRead}");
        }

        /// <summary>Cancellation propagates through materialization.</summary>
        [Fact]
        public async Task Materializer_Cancellation_IsPropagated()
        {
            var png = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 };
            var image = new AIInteractionImage().WithVisionInput("https://example.com/cancel");
            var cts = new CancellationTokenSource();
            cts.Cancel();
            var transport = new FakeTransport(_ =>
                new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(png) });

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ExternalImageMaterializer.MaterializeAsync(
                image, cts.Token,
                () => new HttpClient(new ExternalDataHttpClient.SourceHandler(transport, (_, _) => Task.CompletedTask))));
        }

        /// <summary>Cancellation requested mid-read aborts the stream copy.</summary>
        [Fact]
        public async Task Materializer_CancellationDuringRead_IsPropagated()
        {
            var png = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 };
            var image = new AIInteractionImage().WithVisionInput("https://example.com/cancel-mid");
            var cts = new CancellationTokenSource();
            var stream = new CancellingStream(png, cts);
            var transport = new FakeTransport(_ =>
                new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(stream) });

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ExternalImageMaterializer.MaterializeAsync(
                image, cts.Token,
                () => new HttpClient(transport)));
        }

        private sealed class TrackingStream : MemoryStream
        {
            internal TrackingStream(byte[] buffer) : base(buffer) { }

            internal bool CanSeekOverride { get; set; } = true;

            internal int BytesRead { get; private set; }

            // MemoryStream.ReadAsync(Memory<byte>) dispatches through the array
            // overload — guard so a single physical read is counted once.
            private bool counting;

            public override bool CanSeek => this.CanSeekOverride;

            public override int Read(byte[] buffer, int offset, int count)
            {
                var read = base.Read(buffer, offset, count);
                if (!this.counting)
                {
                    this.BytesRead += read;
                }

                return read;
            }

            public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                var read = await base.ReadAsync(buffer, offset, count, cancellationToken);
                if (!this.counting)
                {
                    this.BytesRead += read;
                }

                return read;
            }

            public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            {
                this.counting = true;
                try
                {
                    var read = await base.ReadAsync(buffer, cancellationToken);
                    this.BytesRead += read;
                    return read;
                }
                finally
                {
                    this.counting = false;
                }
            }
        }

        private sealed class CancellingStream : MemoryStream
        {
            private readonly CancellationTokenSource cts;

            internal CancellingStream(byte[] buffer, CancellationTokenSource cts) : base(buffer) => this.cts = cts;

            public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            {
                this.cts.Cancel();
                cancellationToken.ThrowIfCancellationRequested();
                return base.ReadAsync(buffer, cancellationToken);
            }
        }

        private sealed class NonSeekableStream : MemoryStream
        {
            internal NonSeekableStream(byte[] buffer) : base(buffer) { }

            public override bool CanSeek => false;
        }

        private sealed class FakeTransport : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> respond;

            internal FakeTransport(Func<HttpRequestMessage, HttpResponseMessage> respond) => this.respond = respond;

            internal int Count { get; private set; }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                this.Count++;
                return Task.FromResult(this.respond(request));
            }
        }
    }
}
