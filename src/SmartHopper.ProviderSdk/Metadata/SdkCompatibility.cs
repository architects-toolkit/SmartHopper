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
using System.Linq;
using System.Reflection;

namespace SmartHopper.ProviderSdk.Metadata
{
    /// <summary>
    /// Helpers for reading provider SDK version metadata and checking SemVer
    /// compatibility between provider assemblies and the host's SDK copy.
    /// </summary>
    public static class SdkCompatibility
    {
        /// <summary>
        /// The version of <see cref="SmartHopperProviderSdkVersionAttribute"/> declared by
        /// the currently loaded <c>SmartHopper.ProviderSdk</c> assembly.
        /// </summary>
        public static string HostSdkVersion { get; } =
            typeof(SdkCompatibility).Assembly
                .GetCustomAttributes<SmartHopperProviderSdkVersionAttribute>()
                .FirstOrDefault()
                ?.Version
            ?? "0.0.0";

        /// <summary>
        /// Outcome of a <see cref="Check"/> call.
        /// </summary>
        public enum CompatibilityResult
        {
            /// <summary>The provider is compatible with the host SDK.</summary>
            Compatible,

            /// <summary>The provider's <c>BuiltAgainstSdk</c> MAJOR does not match the host SDK MAJOR.</summary>
            MajorMismatch,

            /// <summary>The host SDK version is lower than the provider's declared <c>MinHostSdk</c>.</summary>
            HostTooOld,

            /// <summary>The provider does not declare its <c>BuiltAgainstSdk</c> at all.</summary>
            MissingMetadata,
        }

        /// <summary>
        /// Check whether the provider assembly is compatible with the host SDK based on
        /// <see cref="BuiltAgainstSdkAttribute"/> and <see cref="MinHostSdkAttribute"/>.
        /// </summary>
        /// <param name="providerAssembly">The loaded provider assembly to inspect.</param>
        /// <param name="diagnostic">A user-facing diagnostic on failure; empty on success.</param>
        public static CompatibilityResult Check(Assembly providerAssembly, out string diagnostic)
        {
            if (providerAssembly is null)
            {
                throw new ArgumentNullException(nameof(providerAssembly));
            }

            var builtAgainst = providerAssembly
                .GetCustomAttributes<BuiltAgainstSdkAttribute>()
                .FirstOrDefault()
                ?.Version;
            var minHost = providerAssembly
                .GetCustomAttributes<MinHostSdkAttribute>()
                .FirstOrDefault()
                ?.Version;

            if (string.IsNullOrWhiteSpace(builtAgainst))
            {
                diagnostic = $"Provider '{providerAssembly.GetName().Name}' does not declare [BuiltAgainstSdk]; SDK compatibility cannot be verified.";
                return CompatibilityResult.MissingMetadata;
            }

            if (GetMajor(builtAgainst) != GetMajor(HostSdkVersion))
            {
                diagnostic = $"Provider '{providerAssembly.GetName().Name}' was built against SDK {builtAgainst}; host SDK is {HostSdkVersion}. Major versions must match.";
                return CompatibilityResult.MajorMismatch;
            }

            if (!string.IsNullOrWhiteSpace(minHost) && Compare(HostSdkVersion, minHost!) < 0)
            {
                diagnostic = $"Provider '{providerAssembly.GetName().Name}' requires host SDK >= {minHost}, but host SDK is {HostSdkVersion}.";
                return CompatibilityResult.HostTooOld;
            }

            diagnostic = string.Empty;
            return CompatibilityResult.Compatible;
        }

        private static int GetMajor(string semVer)
        {
            if (string.IsNullOrWhiteSpace(semVer))
            {
                return 0;
            }

            var idx = semVer.IndexOf('.');
            var head = idx > 0 ? semVer.Substring(0, idx) : semVer;
            return int.TryParse(head, out var major) ? major : 0;
        }

        private static int Compare(string a, string b)
        {
            int[] pa = Parse(a);
            int[] pb = Parse(b);
            for (int i = 0; i < 3; i++)
            {
                if (pa[i] != pb[i])
                {
                    return pa[i].CompareTo(pb[i]);
                }
            }

            return 0;
        }

        private static int[] Parse(string v)
        {
            var parts = (v ?? string.Empty).Split('.', '-', '+');
            var result = new int[3];
            for (int i = 0; i < 3 && i < parts.Length; i++)
            {
                int.TryParse(parts[i], out result[i]);
            }

            return result;
        }
    }
}
