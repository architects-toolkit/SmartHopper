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
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Grasshopper.Kernel.Types;
using Newtonsoft.Json.Linq;
using SmartHopper.Core.Models;
using SmartHopper.Core.Types;
using SmartHopper.ProviderSdk.AICall.Core;

namespace SmartHopper.Core.DataTree
{
    /// <summary>
    /// Central canonical fingerprinting for component inputs and processing-unit identity.
    /// </summary>
    /// <remarks>
    /// Produces deterministic, unambiguous string tokens for values flowing through
    /// Grasshopper data trees. Two modes exist:
    /// <list type="bullet">
    ///   <item><b>Stable mode</b> (<paramref name="strict"/> = false): used by input
    ///   change detection. Values that cannot be fingerprinted deterministically fall
    ///   back to their display string, matching the historical behaviour — this may
    ///   under-detect changes but never thrashes.</item>
    ///   <item><b>Strict mode</b> (<paramref name="strict"/> = true): used for
    ///   processing-unit result reuse. Values without a deterministic identity receive
    ///   a runtime-instance token, so they can never collide with a stored result and
    ///   are simply recomputed.</item>
    /// </list>
    /// In both modes <see cref="AIRequestParameters"/> (and its goo wrapper) are
    /// fingerprinted by canonical content — including <see cref="AIRequestParameters.Extras"/> —
    /// rather than by their display string.
    /// <para>
    /// The raw-value surface lives in <see cref="CanonicalValues"/> (Grasshopper-free)
    /// and is delegated here for a single public entry point.
    /// </para>
    /// </remarks>
    public static class SemanticFingerprint
    {
        /// <summary>Token emitted for <c>null</c> items so null handling is total (no NRE).</summary>
        public const string NullToken = CanonicalValues.NullToken;

        /// <summary>
        /// Canonical fingerprint for a single item (goo or raw value).
        /// </summary>
        /// <param name="item">The item to fingerprint; may be null.</param>
        /// <param name="strict">When true, non-deterministic values get a runtime-unique token.</param>
        /// <returns>A deterministic fingerprint string.</returns>
        public static string Of(object item, bool strict)
        {
            if (item == null)
            {
                return NullToken;
            }

            if (item is IGH_Goo goo)
            {
                return OfGoo(goo, strict);
            }

            var adapted = OfAdapter(item, strict);
            if (adapted != null)
            {
                return adapted;
            }

            if (item is Rhino.Geometry.GeometryBase rawGeometry)
            {
                return GeometryToken(rawGeometry, item, strict);
            }

            return CanonicalValues.OfValue(item, strict);
        }

        /// <summary>
        /// Canonical fingerprint for a raw (non-goo) value.
        /// Delegates to <see cref="CanonicalValues.OfValue"/>.
        /// </summary>
        public static string OfValue(object value, bool strict) => CanonicalValues.OfValue(value, strict);

        /// <summary>
        /// Canonical serialization of <see cref="AIRequestParameters"/>.
        /// Delegates to <see cref="CanonicalValues.CanonicalParameters"/>.
        /// </summary>
        public static string CanonicalParameters(AIRequestParameters parameters, bool executionFieldsOnly)
            => CanonicalValues.CanonicalParameters(parameters, executionFieldsOnly);

        /// <summary>
        /// Recursively canonicalizes a <see cref="JToken"/>.
        /// Delegates to <see cref="CanonicalValues.Canonicalize"/>.
        /// </summary>
        public static string Canonicalize(JToken token) => CanonicalValues.Canonicalize(token);

        /// <summary>Short deterministic hash helper (16-hex SHA-256 prefix).</summary>
        /// <param name="value">Input string; null treated as empty.</param>
        /// <returns>Lowercase 16-char hex hash.</returns>
        public static string ComputeShortHash(string value) => CanonicalValues.ComputeShortHash(value);

        /// <summary>
        /// Canonical fingerprint for a goo item.
        /// </summary>
        public static string OfGoo(IGH_Goo goo, bool strict)
        {
            if (goo == null)
            {
                return NullToken;
            }

            // Domain types with canonical content identity.
            if (goo is GH_AIRequestParameters settingsGoo)
            {
                return "req:" + CanonicalValues.CanonicalParameters(settingsGoo.Value, executionFieldsOnly: false);
            }

            if (goo is GH_AIInputPayload payloadGoo)
            {
                // Canonical content only — ToString() mixes volatile Time/TurnId into
                // its checksum, which would make every unit key unique per run.
                return "pay:" + CanonicalValues.CanonicalPayload(payloadGoo.Value);
            }

            if (goo is GH_VersatileImage imgGoo)
            {
                var v = imgGoo.Value;
                if (v == null)
                {
                    return "img:null";
                }

                if (v.Kind == VersatileImageKind.Bitmap)
                {
                    return OpaqueToken(goo, strict);
                }

                return $"img:{v.Kind}:{CanonicalValues.ComputeShortHash(v.RawValue ?? string.Empty)}";
            }

            if (goo is GH_VersatileAudio audioGoo)
            {
                var v = audioGoo.Value;
                return v == null ? "aud:null" : $"aud:{v.Kind}:{CanonicalValues.ComputeShortHash(v.RawValue ?? string.Empty)}";
            }

            // ScriptVariable unwrap may expose richer types (e.g. AIRequestParameters in generic goo).
            object inner = null;
            try
            {
                if (goo.IsValid)
                {
                    inner = goo.ScriptVariable();
                }
            }
            catch
            {
                inner = null;
            }

            if (inner is AIRequestParameters parameters)
            {
                return "req:" + CanonicalValues.CanonicalParameters(parameters, executionFieldsOnly: false);
            }

            if (inner is AIInputPayload payload)
            {
                return "pay:" + CanonicalValues.CanonicalPayload(payload);
            }

            if (inner != null && !ReferenceEquals(inner, goo))
            {
                var adapted = OfAdapter(inner, strict);
                if (adapted != null)
                {
                    return adapted;
                }

                if (inner is Rhino.Geometry.GeometryBase innerGeometry)
                {
                    return GeometryToken(innerGeometry, goo, strict);
                }

                return CanonicalValues.OfValue(inner, strict) ?? OpaqueToken(goo, strict);
            }

            // Unknown goo kinds: strict mode never trusts an arbitrary ToString()
            // summary — two distinct values can print identical descriptions.
            var display = CanonicalValues.SafeDisplay(goo);
            var typeName = goo.GetType().FullName;
            return strict
                ? $"opq:{typeName}:{RuntimeHelpers.GetHashCode(goo)}"
                : $"t:{typeName}:{display}";
        }

        /// <summary>
        /// Canonical fingerprint for a matched input dictionary — the effective
        /// inputs a processing unit function receives. Parameter names are sorted;
        /// item order is significant.
        /// </summary>
        /// <typeparam name="T">Goo item type.</typeparam>
        /// <param name="inputs">Input name → matched items.</param>
        /// <param name="strict">Forwarded to per-item fingerprinting.</param>
        /// <returns>A canonical fingerprint covering every effective input.</returns>
        public static string OfInputs<T>(Dictionary<string, List<T>> inputs, bool strict)
            where T : IGH_Goo
        {
            if (inputs == null || inputs.Count == 0)
            {
                return "{}";
            }

            var sb = new StringBuilder();
            sb.Append('{');
            var first = true;
            foreach (var kvp in inputs.OrderBy(k => k.Key, StringComparer.Ordinal))
            {
                if (!first)
                {
                    sb.Append(';');
                }

                first = false;
                sb.Append(kvp.Key);
                sb.Append("=[");
                var list = kvp.Value;
                if (list != null)
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        if (i > 0)
                        {
                            sb.Append(',');
                        }

                        sb.Append(Of(list[i], strict));
                    }
                }

                sb.Append(']');
            }

            sb.Append('}');
            return sb.ToString();
        }

        private static string OpaqueToken(IGH_Goo goo, bool strict)
        {
            var typeName = goo.GetType().FullName;
            return strict
                ? $"opq:{typeName}:{RuntimeHelpers.GetHashCode(goo)}"
                : $"t:{typeName}:{CanonicalValues.SafeDisplay(goo)}";
        }

        /// <summary>
        /// External content-fingerprint adapters. Custom data types that want
        /// deterministic reuse register a provider returning a complete canonical
        /// token (or null when the value is not theirs / cannot be fingerprinted).
        /// Consulted before the built-in fallbacks; Rhino geometry is handled
        /// natively by <see cref="GeometryToken"/>.
        /// </summary>
        private static readonly List<Func<object, string>> ValueFingerprinters = new List<Func<object, string>>();

        /// <summary>
        /// Registers a content-fingerprint adapter for custom value types.
        /// The delegate receives the unwrapped value (raw item or the result of
        /// <see cref="IGH_Goo.ScriptVariable"/>) and must return a complete
        /// canonical token, or null to pass the value to the next handler.
        /// </summary>
        /// <param name="fingerprinter">Adapter delegate; null is ignored.</param>
        public static void RegisterValueFingerprinter(Func<object, string> fingerprinter)
        {
            if (fingerprinter == null)
            {
                return;
            }

            lock (ValueFingerprinters)
            {
                ValueFingerprinters.Add(fingerprinter);
            }
        }

        /// <summary>
        /// Runs registered external adapters over a value.
        /// </summary>
        /// <param name="value">The unwrapped value.</param>
        /// <param name="strict">Reserved for adapter context; adapters decide their own token content.</param>
        /// <returns>The first non-null adapter token, or null when none apply.</returns>
        private static string OfAdapter(object value, bool strict)
        {
            Func<object, string>[] snapshot;
            lock (ValueFingerprinters)
            {
                if (ValueFingerprinters.Count == 0)
                {
                    return null;
                }

                snapshot = ValueFingerprinters.ToArray();
            }

            foreach (var adapter in snapshot)
            {
                try
                {
                    var token = adapter(value);
                    if (!string.IsNullOrEmpty(token))
                    {
                        return token;
                    }
                }
                catch
                {
                    // A failing adapter must not break fingerprinting; fall through.
                }
            }

            return null;
        }

        /// <summary>
        /// Content fingerprint for Rhino geometry. Serializes the geometry via
        /// <c>GeometryBase.ToJSON</c> and hashes the result, so edits to control
        /// points, trims, or vertex data change the identity even when the
        /// display description ("Closed Planar Brep") stays identical. When
        /// serialization fails the value degrades to opaque/display tokens —
        /// conservative recompute rather than false reuse.
        /// </summary>
        /// <param name="geometry">The geometry instance.</param>
        /// <param name="owner">The containing object (goo or the geometry itself), used for opaque identity.</param>
        /// <param name="strict">Fingerprint mode.</param>
        private static string GeometryToken(Rhino.Geometry.GeometryBase geometry, object owner, bool strict)
        {
            if (geometry == null)
            {
                return NullToken;
            }

            try
            {
                var json = geometry.ToJSON(new Rhino.FileIO.SerializationOptions());
                if (!string.IsNullOrEmpty(json))
                {
                    return $"geo:{geometry.GetType().Name}:{CanonicalValues.ComputeShortHash(json)}";
                }
            }
            catch
            {
                // Fall through to conservative tokens.
            }

            var typeName = owner?.GetType().FullName ?? geometry.GetType().FullName;
            return strict
                ? $"opq:{typeName}:{RuntimeHelpers.GetHashCode(owner)}"
                : $"t:{typeName}:{CanonicalValues.SafeDisplay(owner)}";
        }
    }
}
