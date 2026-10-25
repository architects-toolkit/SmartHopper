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
using System.IO;
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
        private const int MaxFetchAttempts = 3;

        private static readonly TimeSpan HttpTimeout = TimeSpan.FromSeconds(15);
        private static readonly TimeSpan RetryBaseDelay = TimeSpan.FromMilliseconds(750);

        private static readonly Lazy<ModelCatalogueService> _instance =
            new Lazy<ModelCatalogueService>(() => new ModelCatalogueService());

        private readonly string _baseUrl;
        private readonly ModelCatalogueCache _cache;
        private readonly IProviderHttpClientFactory _httpFactory;
        private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);
        private readonly Dictionary<string, string> _trustedKeys;
        private int _floor;

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

        /// <summary>
        /// Raised for every progress line the service emits ("Info: applied
        /// remote catalogue v3", "Warn: GET /api/v1/manifest → 503 — retrying").
        /// Raised on the refreshing thread — UI subscribers must marshal.
        /// </summary>
        public event EventHandler<ModelCatalogueLogEventArgs>? RefreshLog;

        /// <summary>Gets the process-wide singleton used by the plugin host.</summary>
        public static ModelCatalogueService Instance => _instance.Value;

        /// <summary>Gets the catalogue version currently applied to the registry (0 = none).</summary>
        public int AppliedVersion { get; private set; }

        /// <summary>
        /// Gets the anti-downgrade ratchet: the highest catalogue version this
        /// installation has ever accepted, persisted across sessions. New remote
        /// documents at or below it are rejected even when nothing is cached.
        /// </summary>
        public int AppliedFloor => this._floor;

        /// <summary>Gets where the applied catalogue came from.</summary>
        public ModelCatalogueSource Source { get; private set; } = ModelCatalogueSource.None;

        /// <summary>Gets the last failure message, or null.</summary>
        public string LastError { get; private set; }

        /// <summary>
        /// Refreshes the catalogue: trusted cache first (instant), then a remote
        /// update when a newer signed version exists. Safe to call repeatedly;
        /// concurrent calls collapse onto a single refresh.
        /// </summary>
        /// <inheritdoc cref="RefreshAsync(bool, CancellationToken)"/>
        public Task RefreshAsync(CancellationToken cancellationToken = default) =>
            this.RefreshAsync(false, cancellationToken);

        /// <summary>
        /// Refreshes the catalogue. When <paramref name="force"/> is true the
        /// verified cache is re-applied even when its version is already
        /// applied — needed after providers re-register and clobber
        /// catalogue-managed entries with their seed tables.
        /// </summary>
        [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Catalogue refresh must never break plugin startup or AI calls")]
        public async Task RefreshAsync(bool force, CancellationToken cancellationToken = default)
        {
            if (!await this._gate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
            {
                return;
            }

            try
            {
                this.Log(ProviderLogLevel.Info, "starting catalogue refresh");

                // Seed the anti-rollback floor from disk before anything remote
                // is evaluated: the ratchet survives cache deletion/corruption.
                var floor = this._cache.ReadFloor();
                if (floor > this._floor)
                {
                    this._floor = floor;
                    this.Log(ProviderLogLevel.Info, $"persisted catalogue floor: v{floor}");
                }

                this.LoadTrustedCache(force);
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

        private static bool IsRetryableStatus(int status) =>
            status == 408 || status == 429 || status >= 500;

        private static bool IsRetryableException(Exception ex, CancellationToken cancellationToken) =>
            !cancellationToken.IsCancellationRequested &&
            (ex is HttpRequestException || ex is IOException || ex is System.Net.Sockets.SocketException ||
             ex is TaskCanceledException || ex is OperationCanceledException);

        /// <summary>
        /// Applies the signed local cache when it is valid and newer than what
        /// is already applied.
        /// </summary>
        private void LoadTrustedCache(bool force = false)
        {
            if (!this._cache.TryLoad(out var cached))
            {
                return;
            }

            if (cached.Version < this._floor)
            {
                // A genuine but older cache cannot undo the ratchet.
                this.Log(ProviderLogLevel.Warning, $"cached catalogue v{cached.Version} is below the persisted floor v{this._floor} — ignoring");
                return;
            }

            if (!force && cached.Version <= this.AppliedVersion)
            {
                return;
            }

            try
            {
                var document = this.VerifyAndParse(cached.CatalogueBytes, cached.SignatureBase64, cached.KeyId);
                var applied = this.MergeIntoRegistry(document);
                this.AppliedVersion = document.Version;
                this.Source = ModelCatalogueSource.Cache;
                this._floor = Math.Max(this._floor, document.Version);

                // Re-establish the ratchet when the floor file was lost but the
                // cached artefact survived — the applied version is authentic.
                this._cache.WriteFloor(document.Version);
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
                if (manifest == null)
                {
                    this.Log(ProviderLogLevel.Warning, "catalogue manifest unreachable — keeping applied metadata");
                    return;
                }

                if (manifest.LatestVersion <= this._floor)
                {
                    this.Log(ProviderLogLevel.Info, $"catalogue already up to date (floor v{this._floor}, remote latest v{manifest.LatestVersion})");
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

                if (document.Version <= this._floor)
                {
                    // A genuine but older release cannot undo the ratchet.
                    throw new CatalogueProtocolException($"catalogue_version {document.Version} is not newer than the persisted floor v{this._floor}");
                }

                var applied = this.MergeIntoRegistry(document);
                this.AppliedVersion = document.Version;
                this._floor = document.Version;
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
        /// validates the document. Both steps are fail-closed. Version/floor
        /// policy belongs to the callers — the cache path re-applies equal
        /// versions on force while the remote path must reject replay.
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

            return document;
        }

        /// <summary>
        /// Merges catalogue entries into the capability registry. The catalogue
        /// wins over the embedded seed for the same provider/model key — it is
        /// the fresher, human-validated source.
        /// </summary>
        /// <summary>
        /// The catalogue is the source of truth for the providers it covers:
        /// entries are applied verbatim (including their <c>deprecated</c> flag),
        /// and every registry model absent from the document is removed —
        /// whether it shadowed a catalogue-declared alias or was dropped from
        /// the catalogue by human review. Removal is the honest signal: a model
        /// that left the API is treated as unknown, not flagged-and-callable.
        /// Providers the document does not cover are untouched — local
        /// providers keep their own tables.
        /// </summary>
        private int MergeIntoRegistry(CatalogueDocument document)
        {
            var entries = CatalogueWire.ToRegistryEntries(document);
            var docModels = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            foreach (var kv in entries)
            {
                var models = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var model in kv.Value)
                {
                    models.Add(model.Model);
                }

                docModels[kv.Key] = models;
            }

            var registry = AIModelCapabilityRegistry.Instance;
            var count = 0;
            foreach (var kv in entries)
            {
                foreach (var model in kv.Value)
                {
                    registry.SetCapabilities(model);
                    count++;
                }
            }

            var removed = 0;
            foreach (var provider in docModels.Keys)
            {
                foreach (var existing in registry.GetProviderModels(provider))
                {
                    if (!docModels[provider].Contains(existing.Model) &&
                        registry.Models.TryRemove(existing.GetKey(), out _))
                    {
                        removed++;
                    }
                }
            }

            if (removed > 0)
            {
                this.Log(ProviderLogLevel.Info, $"catalogue source-of-truth pass: removed {removed} model(s) absent from the document");
            }

            return count;
        }

        /// <summary>
        /// Fetches the discovery manifest and authenticates it: when the server
        /// provides <c>manifest.sig</c>, the detached signature is verified
        /// against trusted keys before any manifest field is trusted — an
        /// invalid signature fails closed. A <em>missing</em> signature is
        /// tolerated during rollout (pre-signing servers): the manifest is then
        /// used only as an update hint and the catalogue signature still gates
        /// what gets applied.
        /// </summary>
        private async Task<CatalogueManifest> GetManifestAsync(HttpClient client, CancellationToken cancellationToken)
        {
            var bytes = await this.GetBytesAsync(client, "/api/v1/manifest", cancellationToken).ConfigureAwait(false);
            if (bytes == null)
            {
                return null;
            }

            // Fields are untrusted until the signature verifies, but the parsed
            // key id may legitimately name a rotated key we have not adopted —
            // the keyset artefact verifies independently against trusted keys.
            var manifest = CatalogueWire.ParseManifest(System.Text.Encoding.UTF8.GetString(bytes));
            if (!this._trustedKeys.ContainsKey(manifest.KeyId))
            {
                await this.RefreshTrustedKeysAsync(client, cancellationToken).ConfigureAwait(false);
            }

            var signature = await this.GetTextAsync(client, "/api/v1/manifest.sig", cancellationToken).ConfigureAwait(false);
            if (signature == null)
            {
                this.Log(ProviderLogLevel.Info, "manifest served unsigned — treating it as an unauthenticated hint (pre-sig server)");
            }
            else if (!CatalogueSignature.VerifyWithTrustedKeys(bytes, signature, null, this._trustedKeys))
            {
                throw new CatalogueProtocolException("manifest signature verification failed");
            }
            else
            {
                this.Log(ProviderLogLevel.Info, "verified manifest signature");
            }

            return manifest;
        }

        /// <summary>
        /// Fetches an artefact with bounded retries: transient failures (network
        /// errors, timeouts, 408/429/5xx) are retried up to
        /// <see cref="MaxFetchAttempts"/> times with exponential backoff; other
        /// HTTP statuses and content problems fail immediately. Returns null on
        /// exhaustion — callers treat it as "unreachable", never as valid data.
        /// </summary>
        private async Task<byte[]> GetBytesAsync(HttpClient client, string path, CancellationToken cancellationToken)
        {
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    using (var response = await client.GetAsync(this._baseUrl + path, cancellationToken).ConfigureAwait(false))
                    {
                        if (!response.IsSuccessStatusCode)
                        {
                            var status = (int)response.StatusCode;
                            if (attempt < MaxFetchAttempts && IsRetryableStatus(status))
                            {
                                await this.DelayBeforeRetryAsync(path, $"HTTP {status}", attempt, cancellationToken).ConfigureAwait(false);
                                continue;
                            }

                            this.Log(ProviderLogLevel.Warning, $"GET {path} → {status}");
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
                catch (Exception ex) when (IsRetryableException(ex, cancellationToken) && attempt < MaxFetchAttempts)
                {
                    await this.DelayBeforeRetryAsync(path, ex.GetType().Name, attempt, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (IsRetryableException(ex, cancellationToken))
                {
                    this.Log(ProviderLogLevel.Warning, $"GET {path} failed after {MaxFetchAttempts} attempts: {ex.Message}");
                    return null;
                }
            }
        }

        private async Task DelayBeforeRetryAsync(string path, string reason, int attempt, CancellationToken cancellationToken)
        {
            var delay = TimeSpan.FromMilliseconds(RetryBaseDelay.TotalMilliseconds * Math.Pow(2, attempt - 1));
            this.Log(ProviderLogLevel.Info, $"GET {path}: {reason} — retrying ({attempt + 1}/{MaxFetchAttempts}) in {delay.TotalMilliseconds:0}ms");
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }

        private async Task<string> GetTextAsync(HttpClient client, string path, CancellationToken cancellationToken)
        {
            var bytes = await this.GetBytesAsync(client, path, cancellationToken).ConfigureAwait(false);
            return bytes == null ? null : System.Text.Encoding.UTF8.GetString(bytes);
        }

        [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "A misbehaving log listener must never break catalogue refresh")]
        private void Log(ProviderLogLevel level, string message)
        {
            ProviderSdkHost.Logger?.Log(level, ProviderName, message);
            try
            {
                this.RefreshLog?.Invoke(this, new ModelCatalogueLogEventArgs($"{level}: {message}"));
            }
            catch (Exception)
            {
            }
        }
    }

    /// <summary>
    /// Carries a single human-readable progress line emitted by
    /// <see cref="ModelCatalogueService.RefreshLog"/>.
    /// </summary>
    public sealed class ModelCatalogueLogEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ModelCatalogueLogEventArgs"/> class.
        /// </summary>
        /// <param name="message">The log line.</param>
        public ModelCatalogueLogEventArgs(string message)
        {
            this.Message = message;
        }

        /// <summary>Gets the log line.</summary>
        public string Message { get; }
    }
}
