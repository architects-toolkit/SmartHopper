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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using SmartHopper.ProviderSdk.AIModels;
namespace SmartHopper.ProviderSdk.Catalogue
{
    /// <summary>
    /// Exception thrown when catalogue wire data violates the
    /// <c>smarthopper-models/1</c> schema. Callers must reject the whole
    /// document — never apply it partially.
    /// </summary>
    public class CatalogueProtocolException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CatalogueProtocolException"/> class.
        /// </summary>
        /// <param name="message">What violated the wire contract.</param>
        public CatalogueProtocolException(string message)
            : base(message)
        {
        }
    }

    /// <summary>
    /// A parsed and validated catalogue document: per-provider model
    /// capabilities plus the document-level metadata needed for caching.
    /// </summary>
    public sealed class CatalogueDocument
    {
        /// <summary>Gets the monotonically increasing catalogue version.</summary>
        public int Version { get; internal set; }

        /// <summary>Gets when the catalogue was published (UTC).</summary>
        public DateTime PublishedAt { get; internal set; }

        /// <summary>Gets the minimum client protocol version required.</summary>
        public int MinClientProtocol { get; internal set; } = 1;

        /// <summary>Gets provider sections keyed by wire provider id.</summary>
        public Dictionary<string, List<AIModelCapabilities>> Providers { get; } = new Dictionary<string, List<AIModelCapabilities>>(StringComparer.Ordinal);
    }

    /// <summary>
    /// Strict parser for the <c>smarthopper-models/1</c> wire format.
    /// Every field is re-validated client-side (fail closed): closed property
    /// sets, regex-bound identifiers, bounded strings, decimal-string money and
    /// no URL-capable fields — the same rules the server enforces at publish.
    /// </summary>
    public static class CatalogueWire
    {
        /// <summary>Protocol identifier this client understands.</summary>
        public const string ProtocolId = "smarthopper-models/1";

        /// <summary>Hard size cap for catalogue payloads (2 MB).</summary>
        public const int MaxDocumentBytes = 2 * 1024 * 1024;

        private static readonly Regex ModelIdRegex = new (@"^[a-z0-9][a-z0-9._:/-]{0,127}$", RegexOptions.Compiled);
        private static readonly Regex ProviderIdRegex = new (@"^[a-z][a-z0-9]{0,31}$", RegexOptions.Compiled);
        private static readonly Regex CapabilityTokenRegex = new (@"^[A-Za-z][A-Za-z0-9_]{0,39}$", RegexOptions.Compiled);
        private static readonly Regex MoneyRegex = new (@"^(0|[1-9][0-9]*)(\.[0-9]+)?$", RegexOptions.Compiled);

        private static readonly HashSet<string> DocumentProperties = new (StringComparer.Ordinal)
        {
            "protocol", "catalogue_version", "published_at", "min_client_protocol", "providers",
        };

        private static readonly HashSet<string> SectionProperties = new (StringComparer.Ordinal)
        {
            "models",
        };

        private static readonly HashSet<string> ModelProperties = new (StringComparer.Ordinal)
        {
            "model", "display_name", "capabilities", "default_capability", "verified", "rank",
            "deprecated", "aliases", "context_limit", "created", "supports_streaming",
            "supports_batch", "supports_prompt_caching", "discouraged_for_tools",
            "cache_key_strategy", "pricing", "batch_pricing",
        };

        private static readonly HashSet<string> PricingProperties = new (StringComparer.Ordinal)
        {
            "currency", "prompt", "completion", "input_cache_read",
        };

        private static readonly HashSet<string> KeySetProperties = new (StringComparer.Ordinal)
        {
            "protocol", "keys", "key_id",
        };

        private static readonly HashSet<string> KeyProperties = new (StringComparer.Ordinal)
        {
            "key_id", "alg", "public_key", "not_before", "not_after", "status",
        };

        private static readonly HashSet<string> ManifestProperties = new (StringComparer.Ordinal)
        {
            "protocol", "latest_version", "published_at", "key_id", "catalogue_url", "signature_url", "keys_url",
        };

        /// <summary>
        /// Parses raw catalogue bytes into a validated document.
        /// </summary>
        /// <param name="utf8Bytes">Raw bytes exactly as served (signature verification happens before this call).</param>
        /// <returns>The validated document.</returns>
        /// <exception cref="CatalogueProtocolException">Any schema violation — the whole document is untrusted.</exception>
        public static CatalogueDocument ParseDocument(byte[] utf8Bytes)
        {
            if (utf8Bytes == null || utf8Bytes.Length == 0)
            {
                throw new CatalogueProtocolException("empty catalogue payload");
            }

            if (utf8Bytes.Length > MaxDocumentBytes)
            {
                throw new CatalogueProtocolException($"catalogue exceeds {MaxDocumentBytes} bytes");
            }

            JObject root;
            try
            {
                root = ParseJson(System.Text.Encoding.UTF8.GetString(utf8Bytes), "catalogue");
            }
            catch (CatalogueProtocolException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new CatalogueProtocolException($"catalogue is not valid JSON: {ex.Message}");
            }

            RejectUnknownProperties(root, DocumentProperties, "document");
            RequireString(root, "protocol", ProtocolId, "document");

            var version = RequireInt(root, "catalogue_version", "document");
            if (version <= 0)
            {
                throw new CatalogueProtocolException("catalogue_version must be positive");
            }

            var document = new CatalogueDocument
            {
                Version = version,
                PublishedAt = RequireDateTime(root, "published_at", "document"),
                MinClientProtocol = OptionalInt(root, "min_client_protocol") ?? 1,
            };

            var providers = RequireObject(root, "providers", "document");
            foreach (var sectionProp in providers.Properties())
            {
                var providerId = sectionProp.Name;
                if (!ProviderIdRegex.IsMatch(providerId))
                {
                    throw new CatalogueProtocolException($"provider id '{providerId}' violates the provider id pattern");
                }

                var section = ExpectObject(sectionProp.Value, $"providers.{providerId}");
                RejectUnknownProperties(section, SectionProperties, $"providers.{providerId}");
                var models = RequireArray(section, "models", $"providers.{providerId}");
                var entries = new List<AIModelCapabilities>(models.Count);
                foreach (var modelToken in models)
                {
                    entries.Add(ParseModel(ExpectObject(modelToken, $"providers.{providerId}.models[]"), providerId));
                }

                document.Providers[providerId] = entries;
            }

            return document;
        }

        /// <summary>
        /// Parses the manifest discovery document.
        /// </summary>
        /// <param name="json">Raw manifest JSON text.</param>
        public static CatalogueManifest ParseManifest(string json)
        {
            JObject root;
            try
            {
                root = ParseJson(json, "manifest");
            }
            catch (CatalogueProtocolException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new CatalogueProtocolException($"manifest is not valid JSON: {ex.Message}");
            }

            RejectUnknownProperties(root, ManifestProperties, "manifest");
            RequireString(root, "protocol", ProtocolId, "manifest");
            RequireString(root, "catalogue_url", "manifest");
            RequireString(root, "signature_url", "manifest");
            RequireString(root, "keys_url", "manifest");
            var version = RequireInt(root, "latest_version", "manifest");
            if (version <= 0)
            {
                throw new CatalogueProtocolException("manifest.latest_version must be positive");
            }

            var keyId = RequireString(root, "key_id", "manifest");
            if (!Regex.IsMatch(keyId, @"^[0-9a-f]{16}$"))
            {
                throw new CatalogueProtocolException("manifest.key_id is malformed");
            }

            return new CatalogueManifest
            {
                LatestVersion = version,
                PublishedAt = RequireDateTime(root, "published_at", "manifest"),
                KeyId = keyId,
            };
        }

        /// <summary>
        /// Parses the keyset document (the payload authenticated by keys.sig).
        /// </summary>
        /// <param name="json">Raw keyset JSON text.</param>
        public static List<CatalogueKeyInfo> ParseKeySet(string json)
        {
            JObject root;
            try
            {
                root = ParseJson(json, "keyset");
            }
            catch (CatalogueProtocolException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new CatalogueProtocolException($"keyset is not valid JSON: {ex.Message}");
            }

            RejectUnknownProperties(root, KeySetProperties, "keyset");
            RequireString(root, "protocol", ProtocolId, "keyset");
            var keys = RequireArray(root, "keys", "keyset");
            var result = new List<CatalogueKeyInfo>(keys.Count);
            foreach (var token in keys)
            {
                var obj = ExpectObject(token, "keyset.keys[]");
                RejectUnknownProperties(obj, KeyProperties, "keyset.keys[]");
                var keyId = RequireString(obj, "key_id", "keyset.keys[]");
                var alg = RequireString(obj, "alg", "keyset.keys[]");
                var publicKey = RequireString(obj, "public_key", "keyset.keys[]");
                var status = RequireString(obj, "status", "keyset.keys[]");
                if (!Regex.IsMatch(keyId, @"^[0-9a-f]{16}$"))
                {
                    throw new CatalogueProtocolException("keyset key_id is malformed");
                }

                if (!string.Equals(alg, "ES256", StringComparison.Ordinal))
                {
                    throw new CatalogueProtocolException($"unsupported key alg '{alg}'");
                }

                if (status != "active" && status != "retiring" && status != "revoked")
                {
                    throw new CatalogueProtocolException($"unsupported key status '{status}'");
                }

                result.Add(new CatalogueKeyInfo
                {
                    KeyId = keyId,
                    PublicKeyBase64 = publicKey,
                    Status = status,
                    NotBefore = RequireDateTime(obj, "not_before", "keyset.keys[]"),
                    NotAfter = OptionalDateTime(obj, "not_after"),
                });
            }

            return result;
        }

        /// <summary>
        /// Maps a validated wire document into registry-ready
        /// <see cref="AIModelCapabilities"/> entries, keyed by plugin provider
        /// name (e.g. wire <c>openai</c> → plugin <c>openai</c>).
        /// Unknown providers and capability tokens are skipped (forward compatibility).
        /// </summary>
        public static Dictionary<string, List<AIModelCapabilities>> ToRegistryEntries(CatalogueDocument document)
        {
            var mapped = new Dictionary<string, List<AIModelCapabilities>>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in document.Providers)
            {
                var providerName = ModelCatalogueTrust.ToPluginProviderName(kv.Key);
                if (providerName == null)
                {
                    continue;
                }

                var list = new List<AIModelCapabilities>(kv.Value.Count);
                foreach (var m in kv.Value)
                {
                    m.Provider = providerName;
                    list.Add(m);
                }

                mapped[providerName] = list;
            }

            return mapped;
        }

        private static AIModelCapabilities ParseModel(JObject obj, string providerId)
        {
            var path = $"providers.{providerId}.models[]";
            RejectUnknownProperties(obj, ModelProperties, path);

            var model = RequireString(obj, "model", path);
            if (!ModelIdRegex.IsMatch(model))
            {
                throw new CatalogueProtocolException($"{path}: model id '{model}' violates the model id pattern");
            }

            var displayName = OptionalString(obj, "display_name");
            if (displayName != null)
            {
                RequireSafeText(displayName, $"{path}.display_name");
            }

            var capabilities = ParseCapabilityFlags(RequireArray(obj, "capabilities", path), $"{path}.capabilities");

            var defaultCapability = OptionalString(obj, "default_capability");
            var defaults = AICapability.None;
            if (defaultCapability != null)
            {
                foreach (var name in defaultCapability.Split('|'))
                {
                    if (!CapabilityTokenRegex.IsMatch(name) || !Enum.TryParse(name, out AICapability parsed) || !Enum.IsDefined(typeof(AICapability), parsed))
                    {
                        throw new CatalogueProtocolException($"{path}.default_capability: unknown capability '{name}'");
                    }

                    defaults |= parsed;
                }
            }

            var deprecated = RequireBool(obj, "deprecated", path);
            if (deprecated && defaults != AICapability.None)
            {
                throw new CatalogueProtocolException($"{path}: deprecated models cannot carry default_capability");
            }

            var aliases = RequireArray(obj, "aliases", path);
            var aliasList = new List<string>(aliases.Count);
            foreach (var token in aliases)
            {
                var alias = ExpectString(token, $"{path}.aliases[]");
                if (!ModelIdRegex.IsMatch(alias))
                {
                    throw new CatalogueProtocolException($"{path}.aliases: '{alias}' violates the model id pattern");
                }

                aliasList.Add(alias);
            }

            var discouraged = RequireArray(obj, "discouraged_for_tools", path);
            var discouragedList = new List<string>(discouraged.Count);
            foreach (var token in discouraged)
            {
                var tool = ExpectString(token, $"{path}.discouraged_for_tools[]");
                RequireSafeText(tool, $"{path}.discouraged_for_tools[]");
                discouragedList.Add(tool);
            }

            var cacheKeyStrategy = OptionalString(obj, "cache_key_strategy");
            if (cacheKeyStrategy != null)
            {
                RequireSafeText(cacheKeyStrategy, $"{path}.cache_key_strategy");
            }

            return new AIModelCapabilities
            {
                Provider = providerId,
                Model = model,
                Capabilities = capabilities,
                Default = defaults,
                Verified = RequireBool(obj, "verified", path),
                Rank = RequireInt(obj, "rank", path),
                Deprecated = deprecated,
                Aliases = aliasList,
                ContextLimit = OptionalInt(obj, "context_limit"),
                Created = OptionalDateTime(obj, "created"),
                SupportsStreaming = RequireBool(obj, "supports_streaming", path),
                SupportsBatch = OptionalBool(obj, "supports_batch"),
                SupportsPromptCaching = RequireBool(obj, "supports_prompt_caching", path),
                DiscouragedForTools = discouragedList,
                CacheKeyStrategy = cacheKeyStrategy ?? string.Empty,
                Pricing = ParsePricing(obj["pricing"], $"{path}.pricing"),
                BatchPricing = ParsePricing(obj["batch_pricing"], $"{path}.batch_pricing"),
            };
        }

        private static AICapability ParseCapabilityFlags(JArray array, string path)
        {
            var flags = AICapability.None;
            foreach (var token in array)
            {
                var raw = ExpectString(token, path);
                if (!CapabilityTokenRegex.IsMatch(raw))
                {
                    throw new CatalogueProtocolException($"{path}: invalid capability token '{raw}'");
                }

                if (ModelCatalogueTrust.ShortTokenCapabilities.TryGetValue(raw, out var mapped))
                {
                    flags |= mapped;
                }
                else if (Enum.TryParse(raw, out AICapability parsed) && Enum.IsDefined(typeof(AICapability), parsed))
                {
                    flags |= parsed;
                }

                // Unknown tokens are ignored for forward compatibility.
            }

            return flags;
        }

        private static AIModelPricing ParsePricing(JToken token, string path)
        {
            if (token == null || token.Type == JTokenType.Null)
            {
                return null;
            }

            var obj = ExpectObject(token, path);
            RejectUnknownProperties(obj, PricingProperties, path);
            var currency = RequireString(obj, "currency", path);
            if (currency != "USD" && currency != "EUR")
            {
                throw new CatalogueProtocolException($"{path}.currency: unsupported '{currency}'");
            }

            return new AIModelPricing
            {
                Currency = currency,
                Prompt = ParseMoney(obj["prompt"], $"{path}.prompt"),
                Completion = ParseMoney(obj["completion"], $"{path}.completion"),
                InputCacheRead = ParseMoney(obj["input_cache_read"], $"{path}.input_cache_read"),
            };
        }

        private static decimal? ParseMoney(JToken token, string path)
        {
            if (token == null || token.Type == JTokenType.Null)
            {
                return null;
            }

            var raw = ExpectString(token, path);
            if (!MoneyRegex.IsMatch(raw) || !decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
            {
                throw new CatalogueProtocolException($"{path}: '{raw}' is not a non-negative decimal string");
            }

            return value;
        }

        private static void RequireSafeText(string value, string path)
        {
            // Rejects control chars, markup metacharacters and URL-ish substrings —
            // the same safe-text rule the wire schema enforces server-side.
            if (value.Length > 64 || value.Length == 0 || value.Any(ContainsUnsafeChar) ||
                value.IndexOf("http:", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("https:", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("www.", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                throw new CatalogueProtocolException($"{path}: unsafe or oversized text rejected");
            }
        }

        private static bool ContainsUnsafeChar(char c)
        {
            return c <= '\u001f' || c == '<' || c == '>' || c == '{' || c == '}' || c == '\\';
        }

        // Parses with DateParseHandling.None so every scalar keeps its exact wire
        // type — date-times arrive as strings and get validated as strings.
        private static JObject ParseJson(string json, string what)
        {
            using (var reader = new Newtonsoft.Json.JsonTextReader(new StringReader(json))
            {
                DateParseHandling = Newtonsoft.Json.DateParseHandling.None,
                FloatParseHandling = Newtonsoft.Json.FloatParseHandling.Decimal,
            })
            {
                var token = JToken.ReadFrom(reader);
                if (token is JObject obj)
                {
                    return obj;
                }

                throw new CatalogueProtocolException($"{what} is not a JSON object");
            }
        }

        private static void RejectUnknownProperties(JObject obj, HashSet<string> allowed, string path)
        {
            foreach (var prop in obj.Properties())
            {
                if (!allowed.Contains(prop.Name))
                {
                    throw new CatalogueProtocolException($"{path}: unknown property '{prop.Name}'");
                }
            }
        }

        private static JObject ExpectObject(JToken token, string path)
        {
            if (token is JObject obj)
            {
                return obj;
            }

            throw new CatalogueProtocolException($"{path}: expected object");
        }

        private static string ExpectString(JToken token, string path)
        {
            if (token != null && token.Type == JTokenType.String)
            {
                return (string)token;
            }

            throw new CatalogueProtocolException($"{path}: expected string");
        }

        private static string RequireString(JObject obj, string name, string path)
        {
            return ExpectString(obj[name], $"{path}.{name}");
        }

        private static void RequireString(JObject obj, string name, string expected, string path)
        {
            var value = RequireString(obj, name, path);
            if (!string.Equals(value, expected, StringComparison.Ordinal))
            {
                throw new CatalogueProtocolException($"{path}.{name}: expected '{expected}', got '{value}'");
            }
        }

        private static string OptionalString(JObject obj, string name)
        {
            var token = obj[name];
            return token == null || token.Type == JTokenType.Null ? null : ExpectString(token, name);
        }

        private static JObject RequireObject(JObject obj, string name, string path)
        {
            return ExpectObject(obj[name], $"{path}.{name}");
        }

        private static JArray RequireArray(JObject obj, string name, string path)
        {
            if (obj[name] is JArray array)
            {
                return array;
            }

            throw new CatalogueProtocolException($"{path}.{name}: expected array");
        }

        private static int RequireInt(JObject obj, string name, string path)
        {
            var token = obj[name];
            if (token != null && token.Type == JTokenType.Integer)
            {
                return (int)token;
            }

            throw new CatalogueProtocolException($"{path}.{name}: expected integer");
        }

        private static int? OptionalInt(JObject obj, string name)
        {
            var token = obj[name];
            if (token == null || token.Type == JTokenType.Null)
            {
                return null;
            }

            if (token.Type != JTokenType.Integer)
            {
                throw new CatalogueProtocolException($"{name}: expected integer");
            }

            return (int)token;
        }

        private static bool RequireBool(JObject obj, string name, string path)
        {
            var token = obj[name];
            if (token != null && token.Type == JTokenType.Boolean)
            {
                return (bool)token;
            }

            throw new CatalogueProtocolException($"{path}.{name}: expected boolean");
        }

        private static bool? OptionalBool(JObject obj, string name)
        {
            var token = obj[name];
            if (token == null || token.Type == JTokenType.Null)
            {
                return null;
            }

            if (token.Type != JTokenType.Boolean)
            {
                throw new CatalogueProtocolException($"{name}: expected boolean");
            }

            return (bool)token;
        }

        private static DateTime RequireDateTime(JObject obj, string name, string path)
        {
            var raw = RequireString(obj, name, path);
            if (!DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt))
            {
                throw new CatalogueProtocolException($"{path}.{name}: '{raw}' is not a date-time");
            }

            return dt;
        }

        private static DateTime? OptionalDateTime(JObject obj, string name)
        {
            var raw = OptionalString(obj, name);
            if (raw == null)
            {
                return null;
            }

            if (!DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt))
            {
                throw new CatalogueProtocolException($"{name}: '{raw}' is not a date-time");
            }

            return dt;
        }
    }

    /// <summary>
    /// The manifest discovery payload fields the client consumes.
    /// </summary>
    public sealed class CatalogueManifest
    {
        /// <summary>Gets the latest published catalogue version.</summary>
        public int LatestVersion { get; internal set; }

        /// <summary>Gets when that version was published.</summary>
        public DateTime PublishedAt { get; internal set; }

        /// <summary>Gets the key id that signed the latest catalogue.</summary>
        public string KeyId { get; internal set; }
    }

    /// <summary>
    /// One entry of the signed keyset used for rotation.
    /// </summary>
    public sealed class CatalogueKeyInfo
    {
        /// <summary>Gets the key identifier (16 hex chars).</summary>
        public string KeyId { get; internal set; }

        /// <summary>Gets the base64 SPKI DER public key.</summary>
        public string PublicKeyBase64 { get; internal set; }

        /// <summary>Gets <c>active</c>, <c>retiring</c>, or <c>revoked</c>.</summary>
        public string Status { get; internal set; }

        /// <summary>Gets when the key becomes valid.</summary>
        public DateTime NotBefore { get; internal set; }

        /// <summary>Gets when the key stops being valid (null = unbounded).</summary>
        public DateTime? NotAfter { get; internal set; }
    }
}
