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
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Newtonsoft.Json.Linq;
using SmartHopper.Core.Models;
using SmartHopper.ProviderSdk.AICall.Core;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AICall.Utilities;

namespace SmartHopper.Core.DataTree
{
    /// <summary>
    /// Grasshopper-free canonical value serialization used by
    /// <see cref="SemanticFingerprint"/>. Kept in a separate type so the logic is
    /// unit-testable without loading Grasshopper assemblies.
    /// </summary>
    /// <remarks>
    /// See <see cref="SemanticFingerprint"/> for the strict/stable mode contract.
    /// </remarks>
    public static class CanonicalValues
    {
        /// <summary>Token emitted for <c>null</c> values so null handling is total.</summary>
        public const string NullToken = "∅";

        /// <summary>
        /// Canonical fingerprint for a raw (non-goo) value.
        /// </summary>
        /// <param name="value">The value to fingerprint; may be null.</param>
        /// <param name="strict">When true, non-deterministic values get a runtime-unique token.</param>
        /// <returns>A deterministic fingerprint string.</returns>
        public static string OfValue(object value, bool strict)
        {
            switch (value)
            {
                case null:
                    return NullToken;
                case string s:
                    return "s:" + JToken.FromObject(s).ToString(Newtonsoft.Json.Formatting.None);
                case bool b:
                    return b ? "b:1" : "b:0";
                case int i:
                    return "i:" + i.ToString(CultureInfo.InvariantCulture);
                case long l:
                    return "l:" + l.ToString(CultureInfo.InvariantCulture);
                case double d:
                    return "d:" + d.ToString("R", CultureInfo.InvariantCulture);
                case float f:
                    return "f:" + f.ToString("R", CultureInfo.InvariantCulture);
                case decimal m:
                    return "m:" + m.ToString(CultureInfo.InvariantCulture);
                case Guid g:
                    return "g:" + g.ToString("D");
                case DateTime dt:
                    return "dt:" + dt.ToString("o", CultureInfo.InvariantCulture);
                case TimeSpan ts:
                    return "ts:" + ts.Ticks.ToString(CultureInfo.InvariantCulture);
                case AIRequestParameters p:
                    return "req:" + CanonicalParameters(p, executionFieldsOnly: false);
                case AIInputPayload payload:
                    return "pay:" + CanonicalPayload(payload);
                case JToken jt:
                    return "j:" + Canonicalize(jt);
                case Enum e:
                    return "e:" + value.GetType().FullName + ":" + e.ToString();
                default:
                    // Unknown value kinds: strict mode never trusts an arbitrary
                    // ToString() summary as content identity — two different
                    // objects can print the same description. Opaque identity
                    // forces conservative recalculation instead of false reuse.
                    // Stable mode keeps the display token so the input-change
                    // gate stays deterministic across solves.
                    var display = SafeDisplay(value);
                    return strict
                        ? $"opq:{value.GetType().FullName}:{RuntimeHelpers.GetHashCode(value)}"
                        : $"t:{value.GetType().FullName}:{display}";
            }
        }

        /// <summary>
        /// Canonical serialization of <see cref="AIRequestParameters"/> for fingerprinting.
        /// </summary>
        /// <param name="parameters">The parameters; null serializes as an empty object.</param>
        /// <param name="executionFieldsOnly">
        /// When true, transport/policy fields that do not change produced output
        /// (<see cref="AIRequestParameters.TimeoutSeconds"/>, <see cref="AIRequestParameters.BatchTier"/>,
        /// <see cref="AIRequestParameters.ShareIdenticalResults"/>) are omitted — used for
        /// processing-unit keys so changing them does not re-execute cached work.
        /// </param>
        /// <returns>Canonical JSON string with deterministically sorted keys.</returns>
        public static string CanonicalParameters(AIRequestParameters parameters, bool executionFieldsOnly)
        {
            var obj = new JObject();
            if (parameters == null)
            {
                return obj.ToString(Newtonsoft.Json.Formatting.None);
            }

            if (!string.IsNullOrEmpty(parameters.Model))
            {
                obj["model"] = parameters.Model;
            }

            if (parameters.Temperature.HasValue)
            {
                obj["temperature"] = parameters.Temperature.Value;
            }

            if (parameters.MaxTokens.HasValue)
            {
                obj["maxTokens"] = parameters.MaxTokens.Value;
            }

            if (parameters.TopP.HasValue)
            {
                obj["topP"] = parameters.TopP.Value;
            }

            if (parameters.Seed.HasValue)
            {
                obj["seed"] = parameters.Seed.Value;
            }

            if (!executionFieldsOnly)
            {
                if (parameters.TimeoutSeconds.HasValue)
                {
                    obj["timeout"] = parameters.TimeoutSeconds.Value;
                }

                if (parameters.BatchTier)
                {
                    obj["batchTier"] = true;
                }

                if (!parameters.ShareIdenticalResults)
                {
                    obj["shareIdentical"] = false;
                }
            }

            if (parameters.Extras != null && parameters.Extras.Count > 0)
            {
                obj["extras"] = JToken.Parse(Canonicalize(JObject.FromObject(
                    parameters.Extras.ToDictionary(k => k.Key, k => k.Value))));
            }

            return obj.ToString(Newtonsoft.Json.Formatting.None);
        }

        /// <summary>
        /// Canonical content serialization of an <see cref="AIInputPayload"/> — payload
        /// type, source capability, hint, and per-interaction semantic content.
        /// Volatile fields (<c>TurnId</c>, <c>Time</c>) are excluded: interactions are
        /// rebuilt with fresh timestamps on every run, so including them makes every
        /// unit key unique and silently disables result reuse. <c>SourceReference</c>
        /// is kept — it is stable provenance across runs.
        /// </summary>
        /// <param name="payload">The payload; null serializes as <see cref="NullToken"/>.</param>
        /// <returns>A deterministic canonical string.</returns>
        public static string CanonicalPayload(AIInputPayload payload)
        {
            if (payload == null)
            {
                return NullToken;
            }

            var sb = new StringBuilder();
            sb.Append(payload.PayloadType).Append(';');
            sb.Append((int)payload.InputCapabilityAtSource).Append(';');
            sb.Append(payload.Hint ?? string.Empty).Append(';');

            var interactions = payload.Interactions;
            sb.Append(interactions?.Count ?? 0).Append(';');
            if (interactions != null)
            {
                foreach (var interaction in interactions)
                {
                    if (interaction == null)
                    {
                        sb.Append(NullToken).Append(';');
                        continue;
                    }

                    sb.Append(interaction.GetType().Name).Append(':');
                    sb.Append((int)interaction.Agent).Append(':');
                    sb.Append(DescribeSource(interaction.SourceReference)).Append(':');
                    switch (interaction)
                    {
                        case AIInteractionText text:
                            sb.Append(text.Content ?? string.Empty).Append(':');
                            sb.Append(text.Reasoning ?? string.Empty);
                            break;
                        case AIInteractionImage img:
                            sb.Append(img.ImageUrl?.ToString() ?? string.Empty).Append(':');
                            sb.Append(img.ImageData ?? string.Empty).Append(':');
                            sb.Append(img.OriginalPrompt ?? string.Empty).Append(':');
                            sb.Append(img.MimeType ?? string.Empty);
                            break;
                        case AIInteractionAudio aud:
                            sb.Append(aud.FilePath ?? string.Empty).Append(':');
                            sb.Append(aud.MimeType ?? string.Empty).Append(':');
                            sb.Append(aud.Data?.Length ?? 0);
                            break;
                        case AIInteractionToolResult tr:
                            sb.Append(tr.Id ?? string.Empty).Append(':');
                            sb.Append(tr.Name ?? string.Empty).Append(':');
                            sb.Append(tr.Result != null ? Canonicalize(tr.Result) : string.Empty).Append(':');
                            sb.Append(tr.Reasoning ?? string.Empty).Append(':');
                            if (tr.Images != null)
                            {
                                foreach (var image in tr.Images)
                                {
                                    sb.Append(image?.ImageData ?? string.Empty).Append('|');
                                    sb.Append(image?.MimeType ?? string.Empty).Append('|');
                                    sb.Append(image?.SendToModel == true ? '1' : '0').Append('|');
                                    sb.Append(DescribeSource(image?.SourceReference)).Append('|');
                                }
                            }

                            break;
                        case AIInteractionToolCall tc:
                            sb.Append(tc.Id ?? string.Empty).Append(':');
                            sb.Append(tc.Name ?? string.Empty).Append(':');
                            sb.Append(tc.Arguments != null ? Canonicalize(tc.Arguments) : string.Empty).Append(':');
                            sb.Append(tc.Reasoning ?? string.Empty);
                            break;
                        case AIInteractionRuntimeMessage msg:
                            sb.Append((int)msg.Severity).Append(':');
                            sb.Append(msg.Content ?? string.Empty);
                            break;
                    }

                    sb.Append(';');
                }
            }

            return sb.ToString();
        }

        private static string DescribeSource(SourceContentReference source)
        {
            if (source == null)
            {
                return string.Empty;
            }

            return string.Join(
                ",",
                source.DocumentId ?? string.Empty,
                source.SourceName ?? string.Empty,
                source.PageNumber?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                source.AssetId ?? string.Empty,
                source.FigureLabel ?? string.Empty,
                source.Caption ?? string.Empty,
                source.ContextText ?? string.Empty,
                ((int)source.Kind).ToString(CultureInfo.InvariantCulture),
                source.Region == null
                    ? string.Empty
                    : string.Join(
                        ",",
                        source.Region.X.ToString(CultureInfo.InvariantCulture),
                        source.Region.Y.ToString(CultureInfo.InvariantCulture),
                        source.Region.Width.ToString(CultureInfo.InvariantCulture),
                        source.Region.Height.ToString(CultureInfo.InvariantCulture)));
        }

        /// <summary>
        /// Recursively canonicalizes a <see cref="JToken"/>: object properties are
        /// sorted by name, arrays preserve order.
        /// </summary>
        /// <param name="token">The token to canonicalize; may be null.</param>
        /// <returns>Canonical compact JSON.</returns>
        public static string Canonicalize(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null)
            {
                return "null";
            }

            switch (token.Type)
            {
                case JTokenType.Object:
                    var obj = (JObject)token;
                    var sb = new StringBuilder("{");
                    var first = true;
                    foreach (var prop in obj.Properties().OrderBy(p => p.Name, StringComparer.Ordinal))
                    {
                        if (!first)
                        {
                            sb.Append(',');
                        }

                        first = false;
                        sb.Append(JToken.FromObject(prop.Name).ToString(Newtonsoft.Json.Formatting.None));
                        sb.Append(':');
                        sb.Append(Canonicalize(prop.Value));
                    }

                    return sb.Append('}').ToString();
                case JTokenType.Array:
                    var arr = (JArray)token;
                    return "[" + string.Join(",", arr.Select(Canonicalize)) + "]";
                default:
                    return token.ToString(Newtonsoft.Json.Formatting.None);
            }
        }

        /// <summary>Short deterministic hash helper (16-hex SHA-256 prefix).</summary>
        /// <param name="value">Input string; null treated as empty.</param>
        /// <returns>Lowercase 16-char hex hash.</returns>
        public static string ComputeShortHash(string value) => HashUtility.ComputeShortHash(value);

        internal static string SafeDisplay(object value)
        {
            try
            {
                return value?.ToString() ?? NullToken;
            }
            catch
            {
                return NullToken;
            }
        }

        internal static bool IsDefaultToString(object value, string display)
        {
            var type = value.GetType();
            return string.IsNullOrEmpty(display)
                || display == type.FullName
                || display == type.Name
                || display == type.ToString();
        }
    }
}
