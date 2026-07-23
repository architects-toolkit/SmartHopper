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

namespace SmartHopper.ProviderSdk.Metadata
{
    /// <summary>
    /// Identifies the SmartHopper Provider SDK version of an assembly.
    ///
    /// <para>
    /// Applied automatically to <c>SmartHopper.ProviderSdk</c> itself (the host queries
    /// this attribute to determine which SDK version it ships with), and intended to be
    /// applied by community provider authors via <see cref="BuiltAgainstSdkAttribute"/>
    /// and <see cref="MinHostSdkAttribute"/> to declare compatibility ranges.
    /// </para>
    ///
    /// <para>
    /// SemVer is used: <c>MAJOR</c> bumps indicate a breaking provider contract change.
    /// Providers are loaded only when <c>BuiltAgainstSdk.MAJOR == HostSdk.MAJOR</c> and
    /// <c>HostSdk &gt;= provider.MinHostSdk</c>. Mismatches classify the provider as
    /// <c>Invalid</c> and block the load with a clear error.
    /// </para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
    public sealed class SmartHopperProviderSdkVersionAttribute : Attribute
    {
        /// <summary>
        /// Gets the SemVer version string identifying the SDK contract.
        /// </summary>
        public string Version { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SmartHopperProviderSdkVersionAttribute"/> class.
        /// </summary>
        /// <param name="version">A SemVer string such as <c>"1.0.0"</c>.</param>
        public SmartHopperProviderSdkVersionAttribute(string version)
        {
            this.Version = version ?? throw new ArgumentNullException(nameof(version));
        }
    }

    /// <summary>
    /// Applied by provider authors to declare which SDK version their assembly was
    /// compiled against. Combined with <see cref="MinHostSdkAttribute"/>, it lets the
    /// host reject providers built against an incompatible SDK MAJOR before any code
    /// from the provider runs.
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
    public sealed class BuiltAgainstSdkAttribute : Attribute
    {
        /// <summary>Gets the SDK version this provider was built against.</summary>
        public string Version { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="BuiltAgainstSdkAttribute"/> class.
        /// </summary>
        /// <param name="version">A SemVer string such as <c>"1.0.0"</c>.</param>
        public BuiltAgainstSdkAttribute(string version)
        {
            this.Version = version ?? throw new ArgumentNullException(nameof(version));
        }
    }

    /// <summary>
    /// Applied by provider authors to declare the minimum host SDK version they require.
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
    public sealed class MinHostSdkAttribute : Attribute
    {
        /// <summary>Gets the minimum host SDK version this provider supports.</summary>
        public string Version { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="MinHostSdkAttribute"/> class.
        /// </summary>
        /// <param name="version">A SemVer string such as <c>"1.0.0"</c>.</param>
        public MinHostSdkAttribute(string version)
        {
            this.Version = version ?? throw new ArgumentNullException(nameof(version));
        }
    }

    /// <summary>
    /// Optional metadata applied by provider authors to declare a stable provider
    /// identifier independent of the assembly name. Used during conflict resolution and
    /// classification but is NOT a security boundary — names are easy to spoof; trust
    /// decisions are still based on cryptographic checks.
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
    public sealed class SmartHopperProviderIdAttribute : Attribute
    {
        /// <summary>Gets the provider identifier declared by the assembly.</summary>
        public string ProviderId { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SmartHopperProviderIdAttribute"/> class.
        /// </summary>
        /// <param name="providerId">The provider identifier (e.g. <c>"openai"</c>).</param>
        public SmartHopperProviderIdAttribute(string providerId)
        {
            this.ProviderId = providerId ?? throw new ArgumentNullException(nameof(providerId));
        }
    }
}
