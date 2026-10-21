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

        private ModelCatalogueService NewService(Func<string, (int status, string body)> route) =>
            new ModelCatalogueService(
                baseUrl: "https://test.invalid",
                cache: new ModelCatalogueCache(this.cacheDir),
                trustedKeys: this.Trusted(),
                httpFactory: new RoutingFactory(route));

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
            var bytes = Encoding.UTF8.GetBytes(CatalogueJson(provider: "smarthoppercloud", modelId: "openai/gpt-5.6-terra"));
            var doc = CatalogueWire.ParseDocument(bytes);
            var mapped = CatalogueWire.ToRegistryEntries(doc);
            Assert.True(mapped.ContainsKey("smarthopper cloud"));
            Assert.Equal("smarthopper cloud", mapped["smarthopper cloud"][0].Provider);
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
    }
}
