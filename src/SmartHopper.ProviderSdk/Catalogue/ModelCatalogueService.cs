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
using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using SmartHopper.ProviderSdk.AIModels;
using SmartHopper.ProviderSdk.Hosting;
namespace SmartHopper.ProviderSdk.Catalogue
{
    /// <summary>
    /// Where the currently applied catalogue came from.
    /// </summary>
    public enum ModelCatalogueSource
    {
        /// <summary>Nothing applied — registry holds only the embedded seed.</summary>
        None = 0,

        /// <summary>Applied the verified local cache (offline / stale remote).</summary>
        Cache = 1,

        /// <summary>Applied a freshly fetched, signature-verified remote artefact.</summary>
        Remote = 2,
    }

    /// <summary>
    /// Non-blocking catalogue refresh: verifies and applies the signed local
    /// cache first, then fetches the remote manifest and — only when it is
    /// newer — the catalogue + signature, verifies them before parsing, merges
    /// entries into <see cref="AIModelCapabilityRegistry"/>, and atomically
    /// rewrites the cache. Every failure path degrades silently: the plugin
    /// keeps working on whatever metadata was already registered.
    /// </summary>
    public sealed class ModelCatalogueService
    {
        private const string ProviderName = "modelscatalogue";

        private static readonly TimeSpan HttpTimeout = TimeSpan.FromSeconds(15);

        private static readonly Lazy<ModelCatalogueService> _instance =
            new Lazy<ModelCatalogueService>(() => new ModelCatalogueService());

        private readonly string _baseUrl;
        private readonly ModelCatalogueCache _cache;
        private readonly IProviderHttpClientFactory _httpFactory;
        private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);
        private readonly Dictionary<string, string> _trustedKeys;

        /// <summary>
        /// Initializes a new instance of the <see cref="ModelCatalogueService"/> class.
        /// </summary>
        /// <param name="baseUrl">API base URL; defaults to <c>models.smarthopper.xyz</c>.</param>
        /// <param name="cache">Cache store; defaults to the standard Grasshopper settings dir.</param>
        /// <param name="trustedKeys">Trusted key set (key_id → base64 SPKI); defaults to <see cref="ModelCatalogueTrust.EmbeddedKeys"/>.</param>
        /// <param name="httpFactory">HTTP client factory; defaults to <see cref="ProviderSdkHost.HttpClientFactory"/> resolved at call time.</param>
        public ModelCatalogueService(
            string baseUrl = null,
            ModelCatalogueCache cache = null,
            IReadOnlyDictionary<string, string> trustedKeys = null,
            IProviderHttpClientFactory httpFactory = null)
        {
            this._baseUrl = (baseUrl ?? ModelCatalogueTrust.DefaultBaseUrl).TrimEnd('/');
            this._cache = cache ?? new ModelCatalogueCache();
            this._httpFactory = httpFactory;
            this._trustedKeys = new Dictionary<string, string>(trustedKeys ?? ModelCatalogueTrust.EmbeddedKeys, StringComparer.Ordinal);
        }

        /// <summary>Gets the process-wide singleton used by the plugin host.</summary>
        public static ModelCatalogueService Instance => _instance.Value;

        /// <summary>Gets the catalogue version currently applied to the registry (0 = none).</summary>
        public int AppliedVersion { get; private set; }

        /// <summary>Gets where the applied catalogue came from.</summary>
        public ModelCatalogueSource Source { get; private set; } = ModelCatalogueSource.None;

        /// <summary>Gets the last failure message, or null.</summary>
        public string LastError { get; private set; }

        /// <summary>
        /// Refreshes the catalogue: trusted cache first (instant), then a remote
        /// update when a newer signed version exists. Safe to call repeatedly;
        /// concurrent calls collapse onto a single refresh.
        /// </summary>
        [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Catalogue refresh must never break plugin startup or AI calls")]
        public async Task RefreshAsync(CancellationToken cancellationToken = default)
        {
            if (!await this._gate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
            {
                return;
            }

            try
            {
                this.LoadTrustedCache();
                await this.RefreshFromRemoteAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                this.LastError = ex.Message;
                this.Log(ProviderLogLevel.Warning, $"catalogue refresh failed: {ex.Message}");
            }
            finally
            {
                this._gate.Release();
            }
        }

        /// <summary>
        /// Applies the signed local cache when it is valid and newer than what
        /// is already applied.
        /// </summary>
        private void LoadTrustedCache()
        {
            if (!this._cache.TryLoad(out var cached) || cached.Version <= this.AppliedVersion)
            {
                return;
            }

            try
            {
                var document = this.VerifyAndParse(cached.CatalogueBytes, cached.SignatureBase64, cached.KeyId);
                var applied = this.MergeIntoRegistry(document);
                this.AppliedVersion = document.Version;
                this.Source = ModelCatalogueSource.Cache;
                this.Log(ProviderLogLevel.Info, $"applied cached catalogue v{document.Version} ({applied} models)");
            }
            catch (Exception ex)
            {
                // Corrupt or tampered cache is ignored — never repaired in place.
                this.Log(ProviderLogLevel.Warning, $"ignoring unusable catalogue cache: {ex.Message}");
            }
        }

        /// <summary>
        /// Fetches the manifest, downloads a newer catalogue when available,
        /// verifies its detached signature before parsing, merges into the
        /// registry and persists the artefact for offline starts.
        /// </summary>
        private async Task RefreshFromRemoteAsync(CancellationToken cancellationToken)
        {
            var factory = this._httpFactory ?? ProviderSdkHost.HttpClientFactory;
            if (factory == null)
            {
                return;
            }

            CatalogueManifest manifest;
            using (var client = factory.CreateClient(ProviderName, HttpTimeout))
            {
                manifest = await this.GetManifestAsync(client, cancellationToken).ConfigureAwait(false);
                if (manifest == null || manifest.LatestVersion <= this.AppliedVersion)
                {
                    return;
                }

                var catalogueBytes = await this.GetBytesAsync(client, "/api/v1/catalogue", cancellationToken).ConfigureAwait(false);
                var signature = await this.GetTextAsync(client, "/api/v1/catalogue.sig", cancellationToken).ConfigureAwait(false);
                if (catalogueBytes == null || signature == null)
                {
                    return;
                }

                // Resolve the signing key — fetch the rotation keyset when the
                // manifest names a key we do not yet trust.
                if (!this._trustedKeys.ContainsKey(manifest.KeyId))
                {
                    await this.RefreshTrustedKeysAsync(client, cancellationToken).ConfigureAwait(false);
                }

                var document = this.VerifyAndParse(catalogueBytes, signature, manifest.KeyId);
                if (document.Version > manifest.LatestVersion)
                {
                    // Served bytes must never claim a version the manifest did not advertise.
                    throw new CatalogueProtocolException($"catalogue_version {document.Version} exceeds manifest {manifest.LatestVersion}");
                }

                var applied = this.MergeIntoRegistry(document);
                this.AppliedVersion = document.Version;
                this.Source = ModelCatalogueSource.Remote;
                this.LastError = null;

                var sha256 = Convert.ToHexString(SHA256.HashData(catalogueBytes)).ToLowerInvariant();
                this._cache.Save(catalogueBytes, signature, manifest.KeyId, sha256, document.Version);
                this.Log(ProviderLogLevel.Info, $"applied remote catalogue v{document.Version} ({applied} models)");
            }
        }

        /// <summary>
        /// Fetches and adopts the signed rotation keyset: keys.sig must verify
        /// against an already-trusted key before any new key is trusted.
        /// </summary>
        private async Task RefreshTrustedKeysAsync(HttpClient client, CancellationToken cancellationToken)
        {
            var keysetBytes = await this.GetBytesAsync(client, "/api/v1/keys", cancellationToken).ConfigureAwait(false);
            var keysetSig = await this.GetTextAsync(client, "/api/v1/keys.sig", cancellationToken).ConfigureAwait(false);
            if (keysetBytes == null || keysetSig == null)
            {
                return;
            }

            if (!CatalogueSignature.VerifyWithTrustedKeys(keysetBytes, keysetSig, null, this._trustedKeys))
            {
                throw new CatalogueProtocolException("keyset signature does not verify against any trusted key");
            }

            var keysetJson = System.Text.Encoding.UTF8.GetString(keysetBytes);
            var now = DateTime.UtcNow;
            foreach (var key in CatalogueWire.ParseKeySet(keysetJson))
            {
                var inWindow = key.NotBefore <= now && (!key.NotAfter.HasValue || key.NotAfter.Value > now);
                if (key.Status != "revoked" && inWindow)
                {
                    this._trustedKeys[key.KeyId] = key.PublicKeyBase64;
                }
            }
        }

        /// <summary>
        /// Verifies the signature over the exact bytes, then strictly parses and
        /// validates the document. Both steps are fail-closed.
        /// </summary>
        private CatalogueDocument VerifyAndParse(byte[] bytes, string signatureBase64, string keyId)
        {
            if (!CatalogueSignature.VerifyWithTrustedKeys(bytes, signatureBase64, keyId, this._trustedKeys))
            {
                throw new CatalogueProtocolException("catalogue signature verification failed");
            }

            var document = CatalogueWire.ParseDocument(bytes);
            if (document.MinClientProtocol > 1)
            {
                throw new CatalogueProtocolException($"catalogue requires client protocol {document.MinClientProtocol}");
            }

            if (document.Version <= this.AppliedVersion)
            {
                throw new CatalogueProtocolException($"catalogue_version {document.Version} is not newer than applied {this.AppliedVersion}");
            }

            return document;
        }

        /// <summary>
        /// Merges catalogue entries into the capability registry. The catalogue
        /// wins over the embedded seed for the same provider/model key — it is
        /// the fresher, human-validated source.
        /// </summary>
        private int MergeIntoRegistry(CatalogueDocument document)
        {
            var count = 0;
            foreach (var kv in CatalogueWire.ToRegistryEntries(document))
            {
                foreach (var model in kv.Value)
                {
                    AIModelCapabilityRegistry.Instance.SetCapabilities(model);
                    count++;
                }
            }

            return count;
        }

        private async Task<CatalogueManifest> GetManifestAsync(HttpClient client, CancellationToken cancellationToken)
        {
            var json = await this.GetTextAsync(client, "/api/v1/manifest", cancellationToken).ConfigureAwait(false);
            return json == null ? null : CatalogueWire.ParseManifest(json);
        }

        private async Task<byte[]> GetBytesAsync(HttpClient client, string path, CancellationToken cancellationToken)
        {
            using (var response = await client.GetAsync(this._baseUrl + path, cancellationToken).ConfigureAwait(false))
            {
                if (!response.IsSuccessStatusCode)
                {
                    this.Log(ProviderLogLevel.Warning, $"GET {path} → {(int)response.StatusCode}");
                    return null;
                }

                if (response.Content.Headers.ContentLength > CatalogueWire.MaxDocumentBytes)
                {
                    this.Log(ProviderLogLevel.Warning, $"GET {path} exceeded {CatalogueWire.MaxDocumentBytes} bytes");
                    return null;
                }

                var bytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                return bytes.Length > CatalogueWire.MaxDocumentBytes ? null : bytes;
            }
        }

        private async Task<string> GetTextAsync(HttpClient client, string path, CancellationToken cancellationToken)
        {
            var bytes = await this.GetBytesAsync(client, path, cancellationToken).ConfigureAwait(false);
            return bytes == null ? null : System.Text.Encoding.UTF8.GetString(bytes);
        }

        private void Log(ProviderLogLevel level, string message)
        {
            ProviderSdkHost.Logger?.Log(level, ProviderName, message);
        }
    }
}
