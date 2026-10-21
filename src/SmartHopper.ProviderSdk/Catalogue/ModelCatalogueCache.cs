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
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Newtonsoft.Json.Linq;
namespace SmartHopper.ProviderSdk.Catalogue
{
    /// <summary>
    /// A locally cached catalogue artefact: the exact served bytes, its detached
    /// signature, and the bookkeeping needed for conditional refreshes.
    /// </summary>
    public sealed class CachedCatalogue
    {
        /// <summary>Gets the catalogue version.</summary>
        public int Version { get; internal set; }

        /// <summary>Gets the canonical JSON bytes exactly as served.</summary>
        public byte[] CatalogueBytes { get; internal set; }

        /// <summary>Gets the detached base64 signature.</summary>
        public string SignatureBase64 { get; internal set; }

        /// <summary>Gets the key id that signed the artefact.</summary>
        public string KeyId { get; internal set; }

        /// <summary>Gets the sha256 hex of the bytes (ETag for conditional fetch).</summary>
        public string Sha256 { get; internal set; }

        /// <summary>Gets when this artefact was fetched (UTC).</summary>
        public DateTime FetchedAt { get; internal set; }
    }

    /// <summary>
    /// Durable local cache for the signed catalogue, beside
    /// <c>SmartHopper.json</c> under <c>%APPDATA%/Grasshopper</c>:
    /// <c>models-catalogue.json</c>, <c>.sig</c> and <c>.meta</c>. The cache is
    /// kept indefinitely — a months-old signed artefact stays valid until
    /// atomically replaced by a newer one. Corrupt or partial files are ignored,
    /// never repaired in place.
    /// </summary>
    public sealed class ModelCatalogueCache
    {
        private readonly string _directory;

        /// <summary>
        /// Initializes a new instance of the <see cref="ModelCatalogueCache"/> class.
        /// </summary>
        /// <param name="directory">Cache directory; defaults to <c>%APPDATA%/Grasshopper</c>.</param>
        public ModelCatalogueCache(string directory = null)
        {
            this._directory = directory ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Grasshopper");
        }

        private string CataloguePath => Path.Combine(this._directory, "models-catalogue.json");

        private string SignaturePath => Path.Combine(this._directory, "models-catalogue.sig");

        private string MetaPath => Path.Combine(this._directory, "models-catalogue.meta");

        /// <summary>
        /// Reads the cached artefact if all three files exist and the metadata is
        /// coherent. The signature is NOT verified here — callers verify it the
        /// same way as remote bytes before trusting the content.
        /// </summary>
        [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "A corrupt cache must never break the plugin")]
        public bool TryLoad(out CachedCatalogue cached)
        {
            cached = null;
            try
            {
                if (!File.Exists(this.CataloguePath) || !File.Exists(this.SignaturePath) || !File.Exists(this.MetaPath))
                {
                    return false;
                }

                var meta = JObject.Parse(File.ReadAllText(this.MetaPath));
                var version = meta["version"]?.Value<int>() ?? 0;
                var sha256 = meta["sha256"]?.Value<string>();
                var keyId = meta["key_id"]?.Value<string>();
                var fetchedAt = meta["fetched_at"]?.Value<DateTime>() ?? DateTime.MinValue;
                var bytes = File.ReadAllBytes(this.CataloguePath);
                var signature = File.ReadAllText(this.SignaturePath).Trim();

                if (version <= 0 || bytes.Length == 0 || bytes.Length > CatalogueWire.MaxDocumentBytes ||
                    string.IsNullOrEmpty(signature) || string.IsNullOrEmpty(sha256))
                {
                    return false;
                }

                cached = new CachedCatalogue
                {
                    Version = version,
                    CatalogueBytes = bytes,
                    SignatureBase64 = signature,
                    KeyId = keyId,
                    Sha256 = sha256,
                    FetchedAt = fetchedAt,
                };
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Atomically replaces the cache: temp files in the same directory then
        /// rename — readers never observe a half-written artefact.
        /// </summary>
        /// <param name="catalogueBytes">Exact served bytes.</param>
        /// <param name="signatureBase64">Detached base64 signature.</param>
        /// <param name="keyId">Signing key id.</param>
        /// <param name="sha256Hex">SHA-256 hex of the bytes.</param>
        /// <param name="version">Catalogue version.</param>
        [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "A failing cache write must never break the plugin")]
        public void Save(byte[] catalogueBytes, string signatureBase64, string keyId, string sha256Hex, int version)
        {
            try
            {
                Directory.CreateDirectory(this._directory);
                var meta = new JObject
                {
                    ["version"] = version,
                    ["sha256"] = sha256Hex,
                    ["key_id"] = keyId,
                    ["fetched_at"] = DateTime.UtcNow.ToString("O"),
                };

                this.WriteAtomic(this.CataloguePath, catalogueBytes);
                this.WriteAtomic(this.SignaturePath, System.Text.Encoding.ASCII.GetBytes(signatureBase64 + "\n"));
                this.WriteAtomic(this.MetaPath, System.Text.Encoding.UTF8.GetBytes(meta.ToString()));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ModelCatalogueCache] cache write failed: {ex.Message}");
            }
        }

        private void WriteAtomic(string path, byte[] bytes)
        {
            var tmp = $"{path}.tmp-{Guid.NewGuid():N}";
            File.WriteAllBytes(tmp, bytes);
            File.Move(tmp, path, overwrite: true);
        }
    }
}
