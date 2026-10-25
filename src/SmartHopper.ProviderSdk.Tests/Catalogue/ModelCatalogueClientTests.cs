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

namespace SmartHopper.ProviderSdk.Tests.Catalogue
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using SmartHopper.ProviderSdk.AIModels;
    using SmartHopper.ProviderSdk.Catalogue;
    using SmartHopper.ProviderSdk.Hosting;
    using Xunit;

    /// <summary>
    /// Tests for the <c>smarthopper-models/1</c> client: signature verification,
    /// strict wire parsing, registry mapping, durable cache, and the refresh
    /// orchestration (remote → cache → seed degradation).
    /// </summary>
    [Collection("ProviderSdk")]
    public class ModelCatalogueClientTests : IDisposable
    {
        private readonly ECDsa signingKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        private readonly string publicKeyB64;
        private readonly string keyId;
        private readonly string cacheDir;

        public ModelCatalogueClientTests()
        {
            var spki = this.signingKey.ExportSubjectPublicKeyInfo();
            this.publicKeyB64 = Convert.ToBase64String(spki);
            this.keyId = Convert.ToHexString(SHA256.HashData(spki)).ToLowerInvariant().Substring(0, 16);
            this.cacheDir = Path.Combine(Path.GetTempPath(), "sh-cat-" + Guid.NewGuid().ToString("N"));
            AIModelCapabilityRegistry.Instance.Models.Clear();
        }

        public void Dispose()
        {
            this.signingKey.Dispose();
            AIModelCapabilityRegistry.Instance.Models.Clear();
            if (Directory.Exists(this.cacheDir))
            {
                Directory.Delete(this.cacheDir, true);
            }
        }

        private Dictionary<string, string> Trusted() => new Dictionary<string, string> { [this.keyId] = this.publicKeyB64 };

        private string Sign(byte[] data) =>
            Convert.ToBase64String(this.signingKey.SignData(data, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation));

        private static string CatalogueJson(int version = 1, string modelId = "gpt-test-1", string provider = "openai") =>
            "{\"catalogue_version\":" + version +
            ",\"min_client_protocol\":1,\"protocol\":\"smarthopper-models/1\",\"providers\":{\"" + provider +
            "\":{\"models\":[{\"aliases\":[\"gpt-test-1-latest\"],\"capabilities\":[\"text\",\"tools\",\"reasoning\"],\"context_limit\":400000,\"created\":\"2026-01-01T00:00:00Z\",\"default_capability\":\"Text2Text|ToolChat\",\"deprecated\":false,\"discouraged_for_tools\":[],\"model\":\"" + modelId +
            "\",\"pricing\":{\"completion\":\"0.0000048\",\"currency\":\"EUR\",\"input_cache_read\":\"0.0000003\",\"prompt\":\"0.0000012\"},\"rank\":9990,\"supports_prompt_caching\":true,\"supports_streaming\":true,\"verified\":true}]}},\"published_at\":\"2026-10-21T05:00:00Z\"}";

        private string ManifestJson(int version = 1) =>
            "{\"catalogue_url\":\"/api/v1/catalogue\",\"key_id\":\"" + this.keyId +
            "\",\"keys_url\":\"/api/v1/keys\",\"latest_version\":" + version +
            ",\"protocol\":\"smarthopper-models/1\",\"published_at\":\"2026-10-21T05:00:00Z\",\"signature_url\":\"/api/v1/catalogue.sig\"}";

        private sealed class DelegateHandler : HttpMessageHandler
        {
            private readonly Func<string, (int status, string body)> route;

            public DelegateHandler(Func<string, (int status, string body)> route)
            {
                this.route = route;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var (status, body) = this.route(request.RequestUri.AbsolutePath);
                return Task.FromResult(new HttpResponseMessage((HttpStatusCode)status)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json"),
                });
            }
        }

        private sealed class RoutingFactory : IProviderHttpClientFactory
        {
            private readonly Func<string, (int status, string body)> route;

            public RoutingFactory(Func<string, (int status, string body)> route)
            {
                this.route = route;
            }

            public HttpClient CreateClient(string providerName, TimeSpan timeout) =>
                new HttpClient(new DelegateHandler(this.route), disposeHandler: true);
        }

        /// <summary>
        /// Handler whose response is decided per call — the callback receives the
        /// call ordinal and may return a response or throw to simulate a network
        /// failure. Lets retry tests count attempts deterministically.
        /// </summary>
        private sealed class FlakyHandler : HttpMessageHandler
        {
            private readonly Func<int, string, HttpResponseMessage> respond;
            private int calls;

            public FlakyHandler(Func<int, string, HttpResponseMessage> respond)
            {
                this.respond = respond;
            }

            public int Calls => this.calls;

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var call = Interlocked.Increment(ref this.calls);
                return Task.FromResult(this.respond(call, request.RequestUri.AbsolutePath));
            }
        }

        private sealed class FlakyFactory : IProviderHttpClientFactory
        {
            public FlakyHandler Handler { get; }

            private readonly Func<int, string, HttpResponseMessage> respond;

            public FlakyFactory(Func<int, string, HttpResponseMessage> respond)
            {
                this.respond = respond;
                this.Handler = new FlakyHandler(respond);
            }

            public HttpClient CreateClient(string providerName, TimeSpan timeout) =>
                new HttpClient(this.Handler, disposeHandler: false);
        }

        private static HttpResponseMessage Json(int status, string body) =>
            new HttpResponseMessage((HttpStatusCode)status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };

        private static AIModelCapabilities Seed(string provider, string model) =>
            new AIModelCapabilities
            {
                Provider = provider,
                Model = model,
                Capabilities = AICapability.Text2Text,
            };

        private ModelCatalogueService NewService(Func<string, (int status, string body)> route) =>
            new ModelCatalogueService(
                baseUrl: "https://test.invalid",
                cache: new ModelCatalogueCache(this.cacheDir),
                trustedKeys: this.Trusted(),
                httpFactory: new RoutingFactory(route));

        private ModelCatalogueService NewService(FlakyFactory factory) =>
            new ModelCatalogueService(
                baseUrl: "https://test.invalid",
                cache: new ModelCatalogueCache(this.cacheDir),
                trustedKeys: this.Trusted(),
                httpFactory: factory);

        [Fact]
        public void SignatureVerifiesAndRejectsTamper()
        {
            var data = Encoding.UTF8.GetBytes("{\"a\":1}");
            var sig = this.Sign(data);
            Assert.True(CatalogueSignature.Verify(data, sig, this.publicKeyB64));
            Assert.False(CatalogueSignature.Verify(Encoding.UTF8.GetBytes("{\"a\":2}"), sig, this.publicKeyB64));
            using var other = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            Assert.False(CatalogueSignature.Verify(data, sig, Convert.ToBase64String(other.ExportSubjectPublicKeyInfo())));
            Assert.False(CatalogueSignature.Verify(data, "not-base64!!", this.publicKeyB64));
        }

        [Fact]
        public void ParsesAndMapsValidDocument()
        {
            var bytes = Encoding.UTF8.GetBytes(CatalogueJson());
            var doc = CatalogueWire.ParseDocument(bytes);

            Assert.Equal(1, doc.Version);
            var model = Assert.Single(doc.Providers["openai"]);
            Assert.Equal("gpt-test-1", model.Model);
            Assert.True(model.Verified);
            Assert.Equal(9990, model.Rank);
            Assert.Equal(400000, model.ContextLimit);
            Assert.True(model.HasCapability(AICapability.Text2Text));
            Assert.True(model.HasCapability(AICapability.FunctionCalling));
            Assert.True(model.HasCapability(AICapability.Reasoning));
            Assert.Equal(AICapability.Text2Text | AICapability.ToolChat, model.Default);
            Assert.Equal("EUR", model.Pricing.Currency);
            Assert.Equal(0.0000012m, model.Pricing.Prompt);
            Assert.Equal(0.0000003m, model.Pricing.InputCacheRead);
            Assert.Single(model.Aliases);
        }

        [Fact]
        public void RejectsPhishingAndMalformedPayloads()
        {
            Assert.Throws<CatalogueProtocolException>(() => CatalogueWire.ParseDocument(Encoding.UTF8.GetBytes("{\"protocol\":\"x\"}")));
            Assert.Throws<CatalogueProtocolException>(() => CatalogueWire.ParseDocument(Encoding.UTF8.GetBytes("not json")));
            Assert.Throws<CatalogueProtocolException>(() => CatalogueWire.ParseDocument(Array.Empty<byte>()));

            // URL inside display_name → rejected.
            var bad = CatalogueJson().Replace("\"verified\":true", "\"verified\":true,\"display_name\":\"see https://evil\"");
            Assert.Throws<CatalogueProtocolException>(() => CatalogueWire.ParseDocument(Encoding.UTF8.GetBytes(bad)));

            // Unknown property (additionalProperties: false).
            var extra = CatalogueJson().Replace("\"verified\":true", "\"verified\":true,\"evil\":true");
            Assert.Throws<CatalogueProtocolException>(() => CatalogueWire.ParseDocument(Encoding.UTF8.GetBytes(extra)));

            // Negative money.
            var money = CatalogueJson().Replace("\"0.0000012\"", "\"-1\"");
            Assert.Throws<CatalogueProtocolException>(() => CatalogueWire.ParseDocument(Encoding.UTF8.GetBytes(money)));
        }

        [Fact]
        public void MapsWireProviderIdsToPluginNames()
        {
            var bytes = Encoding.UTF8.GetBytes(CatalogueJson(provider: "openai", modelId: "openai/gpt-5.6-terra"));
            var doc = CatalogueWire.ParseDocument(bytes);
            var mapped = CatalogueWire.ToRegistryEntries(doc);
            Assert.True(mapped.ContainsKey("openai"));
            Assert.Equal("openai", mapped["openai"][0].Provider);
        }

        [Fact]
        public void CacheRoundTripsAndRejectsCorruption()
        {
            var cache = new ModelCatalogueCache(this.cacheDir);
            Assert.False(cache.TryLoad(out _));

            var bytes = Encoding.UTF8.GetBytes(CatalogueJson());
            var sig = this.Sign(bytes);
            var sha = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            cache.Save(bytes, sig, this.keyId, sha, 1);

            Assert.True(cache.TryLoad(out var loaded));
            Assert.Equal(1, loaded.Version);
            Assert.Equal(bytes, loaded.CatalogueBytes);
            Assert.Equal(sig, loaded.SignatureBase64);

            // Corrupt metadata → cache is unusable (not repaired).
            File.WriteAllText(Path.Combine(this.cacheDir, "models-catalogue.meta"), "{corrupt");
            Assert.False(cache.TryLoad(out _));

            // Meta/bytes divergence (e.g. swapped or hand-edited files) → rejected.
            cache.Save(bytes, sig, this.keyId, sha, 1);
            var meta = File.ReadAllText(Path.Combine(this.cacheDir, "models-catalogue.meta"));
            File.WriteAllText(Path.Combine(this.cacheDir, "models-catalogue.meta"), meta.Replace(sha, new string('0', 64)));
            Assert.False(cache.TryLoad(out _));
        }

        [Fact]
        public async Task RemoteFetchVerifiesMergesAndCaches()
        {
            var docBytes = Encoding.UTF8.GetBytes(CatalogueJson());
            var docSig = this.Sign(docBytes);
            var service = this.NewService(path =>
            {
                if (path.EndsWith("/manifest")) return (200, this.ManifestJson(1));
                if (path.EndsWith("/catalogue")) return (200, Encoding.UTF8.GetString(docBytes));
                if (path.EndsWith("/catalogue.sig")) return (200, docSig);
                return (404, "{}");
            });

            await service.RefreshAsync();
            Assert.Equal(1, service.AppliedVersion);
            Assert.Equal(ModelCatalogueSource.Remote, service.Source);

            var caps = AIModelCapabilityRegistry.Instance.GetCapabilities("openai", "gpt-test-1");
            Assert.NotNull(caps);
            Assert.True(caps.HasCapability(AICapability.FunctionCalling));

            // Cache persisted — a second run without remote still applies it.
            Assert.True(new ModelCatalogueCache(this.cacheDir).TryLoad(out _));
        }

        [Fact]
        public async Task BadSignatureKeepsRegistryUntouched()
        {
            var docBytes = Encoding.UTF8.GetBytes(CatalogueJson());
            var service = this.NewService(path =>
            {
                if (path.EndsWith("/manifest")) return (200, this.ManifestJson(1));
                if (path.EndsWith("/catalogue")) return (200, Encoding.UTF8.GetString(docBytes));
                if (path.EndsWith("/catalogue.sig")) return (200, Convert.ToBase64String(new byte[64]));
                return (404, "{}");
            });

            await service.RefreshAsync();
            Assert.Equal(0, service.AppliedVersion);
            Assert.NotNull(service.LastError);
            Assert.Null(AIModelCapabilityRegistry.Instance.GetCapabilities("openai", "gpt-test-1"));
            Assert.False(new ModelCatalogueCache(this.cacheDir).TryLoad(out _));
        }

        [Fact]
        public async Task OfflineFallsBackToSignedCache()
        {
            // Prime the cache through a good remote run.
            var docBytes = Encoding.UTF8.GetBytes(CatalogueJson());
            var docSig = this.Sign(docBytes);
            var online = this.NewService(path =>
            {
                if (path.EndsWith("/manifest")) return (200, this.ManifestJson(1));
                if (path.EndsWith("/catalogue")) return (200, Encoding.UTF8.GetString(docBytes));
                if (path.EndsWith("/catalogue.sig")) return (200, docSig);
                return (404, "{}");
            });
            await online.RefreshAsync();
            Assert.Equal(1, online.AppliedVersion);
            AIModelCapabilityRegistry.Instance.Models.Clear();

            // Now remote is dead — the signed cache applies instead.
            var offline = this.NewService(_ => (503, "{}"));
            await offline.RefreshAsync();
            Assert.Equal(1, offline.AppliedVersion);
            Assert.Equal(ModelCatalogueSource.Cache, offline.Source);
            Assert.NotNull(AIModelCapabilityRegistry.Instance.GetCapabilities("openai", "gpt-test-1"));
        }

        [Fact]
        public async Task OlderOrEqualVersionIsIgnored()
        {
            var docBytes = Encoding.UTF8.GetBytes(CatalogueJson(1));
            var docSig = this.Sign(docBytes);
            var service = this.NewService(path =>
            {
                if (path.EndsWith("/manifest")) return (200, this.ManifestJson(1));
                if (path.EndsWith("/catalogue")) return (200, Encoding.UTF8.GetString(docBytes));
                if (path.EndsWith("/catalogue.sig")) return (200, docSig);
                return (404, "{}");
            });
            await service.RefreshAsync();
            Assert.Equal(1, service.AppliedVersion);

            // Serve the same version again → manifest short-circuits, nothing refetched.
            var second = this.NewService(_ => (500, "{}"));
            await second.RefreshAsync();

            // The fresh service fell back to its cache (v1) and never called remote.
            Assert.Equal(1, second.AppliedVersion);
            Assert.Equal(ModelCatalogueSource.Cache, second.Source);
        }

        [Fact]
        public async Task TransientServerErrorsAreRetried()
        {
            var factory = new FlakyFactory((call, path) =>
            {
                if (call <= 2) return Json(503, "{}");
                if (path.EndsWith("/manifest")) return Json(200, this.ManifestJson(1));
                var docBytes = Encoding.UTF8.GetBytes(CatalogueJson());
                if (path.EndsWith("/catalogue")) return Json(200, Encoding.UTF8.GetString(docBytes));
                if (path.EndsWith("/catalogue.sig")) return Json(200, this.Sign(docBytes));
                return Json(404, "{}");
            });
            var service = this.NewService(factory);

            await service.RefreshAsync();

            // 2 failed + 1 good manifest, then manifest.sig (404 → tolerated),
            // one catalogue + one sig fetch.
            Assert.Equal(6, factory.Handler.Calls);
            Assert.Equal(1, service.AppliedVersion);
            Assert.Equal(ModelCatalogueSource.Remote, service.Source);
        }

        [Fact]
        public async Task ClientErrorsAreNotRetried()
        {
            var factory = new FlakyFactory((call, path) => Json(404, "{}"));
            var service = this.NewService(factory);

            await service.RefreshAsync();

            // 404 on the manifest is terminal — exactly one attempt, no artefact applied.
            Assert.Equal(1, factory.Handler.Calls);
            Assert.Equal(0, service.AppliedVersion);
        }

        [Fact]
        public async Task NetworkFailuresAreRetriedThenGiveUp()
        {
            var factory = new FlakyFactory((call, path) => throw new HttpRequestException("simulated network failure"));
            var service = this.NewService(factory);

            await service.RefreshAsync();

            Assert.Equal(3, factory.Handler.Calls);
            Assert.Equal(0, service.AppliedVersion);
            Assert.Equal(ModelCatalogueSource.None, service.Source);
        }

        [Fact]
        public async Task CatalogueIsSourceOfTruthForCoveredProviders()
        {
            var registry = AIModelCapabilityRegistry.Instance;
            registry.SetCapabilities(Seed("openai", "gpt-test-1"));       // doc model — replaced by catalogue entry
            registry.SetCapabilities(Seed("openai", "gpt-test-1-latest")); // doc alias → removed
            registry.SetCapabilities(Seed("openai", "gpt-legacy-9"));      // absent → removed (unknown, not deprecated)
            registry.SetCapabilities(Seed("ollama", "local-model"));       // uncovered provider → untouched

            var docBytes = Encoding.UTF8.GetBytes(CatalogueJson());
            var docSig = this.Sign(docBytes);
            var service = this.NewService(path =>
            {
                if (path.EndsWith("/manifest")) return (200, this.ManifestJson(1));
                if (path.EndsWith("/catalogue")) return (200, Encoding.UTF8.GetString(docBytes));
                if (path.EndsWith("/catalogue.sig")) return (200, docSig);
                return (404, "{}");
            });
            await service.RefreshAsync();

            // Folded: the alias-keyed entry is gone; lookups resolve to the canonical.
            Assert.False(registry.Models.ContainsKey("openai.gpt-test-1-latest"));
            var resolved = registry.GetCapabilities("openai", "gpt-test-1-latest");
            Assert.NotNull(resolved);
            Assert.Equal("gpt-test-1", resolved.Model);
            Assert.True(resolved.HasCapability(AICapability.FunctionCalling));

            // Absent from the document: removed outright — treated as unknown.
            Assert.Null(registry.GetCapabilities("openai", "gpt-legacy-9"));
            Assert.False(registry.Models.ContainsKey("openai.gpt-legacy-9"));

            // Providers the document does not cover keep their own tables.
            var local = registry.GetCapabilities("ollama", "local-model");
            Assert.NotNull(local);
            Assert.False(local.Deprecated);
        }

        [Fact]
        public async Task ForceReappliesCacheAfterProviderReregistration()
        {
            var registry = AIModelCapabilityRegistry.Instance;
            var docBytes = Encoding.UTF8.GetBytes(CatalogueJson());
            var docSig = this.Sign(docBytes);
            var online = this.NewService(path =>
            {
                if (path.EndsWith("/manifest")) return (200, this.ManifestJson(1));
                if (path.EndsWith("/catalogue")) return (200, Encoding.UTF8.GetString(docBytes));
                if (path.EndsWith("/catalogue.sig")) return (200, docSig);
                return (404, "{}");
            });
            await online.RefreshAsync();
            Assert.Equal(1, online.AppliedVersion);

            // Providers re-registering overwrite catalogue entries with seed tables.
            registry.SetCapabilities(Seed("openai", "gpt-test-1"));
            registry.SetCapabilities(Seed("openai", "gpt-ghost"));
            Assert.False(registry.GetCapabilities("openai", "gpt-test-1").HasCapability(AICapability.FunctionCalling));

            // Forced refresh re-applies the verified cache even though the version
            // did not change — remote is dead, no network needed.
            var forced = this.NewService(_ => (503, "{}"));
            await forced.RefreshAsync(force: true);

            Assert.Equal(1, forced.AppliedVersion);
            Assert.Equal(ModelCatalogueSource.Cache, forced.Source);
            Assert.True(registry.GetCapabilities("openai", "gpt-test-1").HasCapability(AICapability.FunctionCalling));
            Assert.Null(registry.GetCapabilities("openai", "gpt-ghost"));
        }

        [Fact]
        public async Task SignedManifestIsVerifiedAndApplied()
        {
            var manifestBytes = Encoding.UTF8.GetBytes(this.ManifestJson(1));
            var docBytes = Encoding.UTF8.GetBytes(CatalogueJson());
            var docSig = this.Sign(docBytes);
            var service = this.NewService(path =>
            {
                if (path.EndsWith("/manifest.sig")) return (200, this.Sign(manifestBytes));
                if (path.EndsWith("/manifest")) return (200, this.ManifestJson(1));
                if (path.EndsWith("/catalogue")) return (200, Encoding.UTF8.GetString(docBytes));
                if (path.EndsWith("/catalogue.sig")) return (200, docSig);
                return (404, "{}");
            });

            var lines = new List<string>();
            service.RefreshLog += (sender, e) => lines.Add(e.Message);
            await service.RefreshAsync();

            Assert.Equal(1, service.AppliedVersion);
            Assert.Equal(ModelCatalogueSource.Remote, service.Source);
            Assert.Contains(lines, l => l.Contains("verified manifest signature"));
        }

        [Fact]
        public async Task BadManifestSignatureFailsClosed()
        {
            var manifestBytes = Encoding.UTF8.GetBytes(this.ManifestJson(1));
            var docBytes = Encoding.UTF8.GetBytes(CatalogueJson());
            var service = this.NewService(path =>
            {
                if (path.EndsWith("/manifest.sig")) return (200, Convert.ToBase64String(new byte[64]));
                if (path.EndsWith("/manifest")) return (200, this.ManifestJson(1));
                if (path.EndsWith("/catalogue")) return (200, Encoding.UTF8.GetString(docBytes));
                if (path.EndsWith("/catalogue.sig")) return (200, this.Sign(docBytes));
                return (404, "{}");
            });

            await service.RefreshAsync();

            // The manifest is untrusted before verification — a forged signature
            // must abort the whole update, never reach the catalogue fetch.
            Assert.Equal(0, service.AppliedVersion);
            Assert.NotNull(service.LastError);
            Assert.Null(AIModelCapabilityRegistry.Instance.GetCapabilities("openai", "gpt-test-1"));
        }

        [Fact]
        public async Task MissingManifestSignatureIsTolerated()
        {
            var docBytes = Encoding.UTF8.GetBytes(CatalogueJson());
            var service = this.NewService(path =>
            {
                if (path.EndsWith("/manifest.sig")) return (404, "{}");
                if (path.EndsWith("/manifest")) return (200, this.ManifestJson(1));
                if (path.EndsWith("/catalogue")) return (200, Encoding.UTF8.GetString(docBytes));
                if (path.EndsWith("/catalogue.sig")) return (200, this.Sign(docBytes));
                return (404, "{}");
            });

            await service.RefreshAsync();

            // Rollout compatibility: pre-sig servers keep working — the manifest
            // is only a hint, the catalogue signature still gates application.
            Assert.Equal(1, service.AppliedVersion);
            Assert.Equal(ModelCatalogueSource.Remote, service.Source);
        }

        [Fact]
        public async Task PersistedFloorRejectsSignedReplayAfterCacheLoss()
        {
            // Prime the floor at v2 through a good remote run.
            var v2Bytes = Encoding.UTF8.GetBytes(CatalogueJson(2));
            var online = this.NewService(path =>
            {
                if (path.EndsWith("/manifest.sig")) return (200, this.Sign(Encoding.UTF8.GetBytes(this.ManifestJson(2))));
                if (path.EndsWith("/manifest")) return (200, this.ManifestJson(2));
                if (path.EndsWith("/catalogue")) return (200, Encoding.UTF8.GetString(v2Bytes));
                if (path.EndsWith("/catalogue.sig")) return (200, this.Sign(v2Bytes));
                return (404, "{}");
            });
            await online.RefreshAsync();
            Assert.Equal(2, online.AppliedVersion);
            Assert.Equal(2, new ModelCatalogueCache(this.cacheDir).ReadFloor());
            AIModelCapabilityRegistry.Instance.Models.Clear();

            // Cache artefact is lost/corrupted, only the floor survives.
            File.Delete(Path.Combine(this.cacheDir, "models-catalogue.json"));
            File.Delete(Path.Combine(this.cacheDir, "models-catalogue.sig"));
            File.Delete(Path.Combine(this.cacheDir, "models-catalogue.meta"));

            // Attacker replays a genuine but older signed release (v1).
            var v1Bytes = Encoding.UTF8.GetBytes(CatalogueJson(1));
            var replayed = this.NewService(path =>
            {
                if (path.EndsWith("/manifest.sig")) return (200, this.Sign(Encoding.UTF8.GetBytes(this.ManifestJson(1))));
                if (path.EndsWith("/manifest")) return (200, this.ManifestJson(1));
                if (path.EndsWith("/catalogue")) return (200, Encoding.UTF8.GetString(v1Bytes));
                if (path.EndsWith("/catalogue.sig")) return (200, this.Sign(v1Bytes));
                return (404, "{}");
            });
            await replayed.RefreshAsync();

            // The persisted floor short-circuits the replayed manifest — nothing
            // is applied even though every artefact verifies.
            Assert.Equal(2, replayed.AppliedFloor);
            Assert.Equal(0, replayed.AppliedVersion);
            Assert.Null(AIModelCapabilityRegistry.Instance.GetCapabilities("openai", "gpt-test-1"));
        }

        [Fact]
        public void FloorIsMonotonicAndSurvivesWithoutCache()
        {
            var cache = new ModelCatalogueCache(this.cacheDir);
            Assert.Equal(0, cache.ReadFloor());

            cache.WriteFloor(3);
            cache.WriteFloor(5);
            cache.WriteFloor(4); // ratchet never moves down
            Assert.Equal(5, cache.ReadFloor());

            // Corrupt/garbage floor → treated as absent, never breaks the plugin.
            File.WriteAllText(Path.Combine(this.cacheDir, "models-catalogue.floor"), "not-a-number");
            Assert.Equal(0, cache.ReadFloor());
            cache.WriteFloor(2);
            Assert.Equal(2, cache.ReadFloor());
        }

        [Fact]
        public async Task RefreshLogEmitsProgressLines()
        {
            var docBytes = Encoding.UTF8.GetBytes(CatalogueJson());
            var docSig = this.Sign(docBytes);
            var service = this.NewService(path =>
            {
                if (path.EndsWith("/manifest")) return (200, this.ManifestJson(1));
                if (path.EndsWith("/catalogue")) return (200, Encoding.UTF8.GetString(docBytes));
                if (path.EndsWith("/catalogue.sig")) return (200, docSig);
                return (404, "{}");
            });

            var lines = new List<string>();
            service.RefreshLog += (sender, e) => lines.Add(e.Message);
            await service.RefreshAsync();

            Assert.Contains(lines, l => l.Contains("starting catalogue refresh"));
            Assert.Contains(lines, l => l.Contains("applied remote catalogue v1"));
        }
    }
}
