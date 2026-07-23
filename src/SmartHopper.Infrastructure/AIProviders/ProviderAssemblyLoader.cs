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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using SmartHopper.ProviderSdk.AIProviders;
using SmartHopper.ProviderSdk.Metadata;

namespace SmartHopper.Infrastructure.AIProviders
{
    /// <summary>
    /// Custom <see cref="AssemblyLoadContext"/> used to load provider plug-in assemblies in
    /// isolation while still binding their references to the host's copy of
    /// <see cref="SmartHopper.ProviderSdk"/> and other shared dependencies.
    ///
    /// <para>
    /// Loading strategy:
    /// <list type="bullet">
    ///   <item>
    ///     Assemblies whose simple name is present in <see cref="SharedAssemblyNames"/>
    ///     are delegated to <see cref="AssemblyLoadContext.Default"/>, so a provider's
    ///     <c>IAIProvider</c> resolves to the SAME type identity as the host expects.
    ///     This is what allows reflection-based plug-in dispatch to work across the ALC
    ///     boundary.
    ///   </item>
    ///   <item>
    ///     Any other DLL co-located with the provider (provider-private dependencies)
    ///     is loaded into this ALC, isolated from the host's load context.
    ///   </item>
    ///   <item>
    ///     Native libraries are also resolved relative to the provider directory.
    ///   </item>
    /// </list>
    /// </para>
    ///
    /// <para>
    /// Combined with the type-identity check in <see cref="ProviderAssemblyLoader"/>,
    /// this fulfils plan §4.5 (Type identity &amp; dependency loading): providers built
    /// against a mismatched copy of the SDK are rejected with a clear error rather than
    /// silently failing at activation time.
    /// </para>
    /// </summary>
    internal sealed class ProviderLoadContext : AssemblyLoadContext
    {
        /// <summary>
        /// Simple assembly names that MUST resolve to the host's copy (default ALC).
        /// </summary>
        private static readonly HashSet<string> SharedAssemblyNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "SmartHopper.ProviderSdk",
            "SmartHopper.Infrastructure",
            "Newtonsoft.Json",
            "System.Drawing.Common",
        };

        private readonly AssemblyDependencyResolver _resolver;
        private readonly string _providerDirectory;

        public ProviderLoadContext(string providerAssemblyPath)
            : base(name: $"ProviderLoadContext::{Path.GetFileNameWithoutExtension(providerAssemblyPath)}", isCollectible: false)
        {
            this._resolver = new AssemblyDependencyResolver(providerAssemblyPath);
            this._providerDirectory = Path.GetDirectoryName(providerAssemblyPath)
                ?? throw new ArgumentException("Provider assembly path has no directory.", nameof(providerAssemblyPath));
        }

        /// <inheritdoc/>
        protected override Assembly Load(AssemblyName assemblyName)
        {
            // Shared types MUST come from the default ALC so type identity matches host.
            if (assemblyName.Name != null && SharedAssemblyNames.Contains(assemblyName.Name))
            {
                return null!;
            }

            var resolved = this._resolver.ResolveAssemblyToPath(assemblyName);
            if (!string.IsNullOrEmpty(resolved) && File.Exists(resolved))
            {
                return this.LoadFromAssemblyPath(resolved);
            }

            // Fallback: look in the provider directory.
            var candidate = Path.Combine(this._providerDirectory, assemblyName.Name + ".dll");
            if (File.Exists(candidate))
            {
                return this.LoadFromAssemblyPath(candidate);
            }

            return null!;
        }

        /// <inheritdoc/>
        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
        {
            var path = this._resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
            return string.IsNullOrEmpty(path) ? IntPtr.Zero : this.LoadUnmanagedDllFromPath(path);
        }
    }

    /// <summary>
    /// Outcome of loading a provider assembly via <see cref="ProviderAssemblyLoader"/>.
    /// </summary>
    internal enum ProviderLoadOutcome
    {
        /// <summary>Assembly loaded successfully and SDK type identity matched the host.</summary>
        Loaded,

        /// <summary>The file is not a valid managed assembly.</summary>
        InvalidAssembly,

        /// <summary>The assembly was loaded but does not expose any <c>IAIProviderFactory</c>.</summary>
        NoFactory,

        /// <summary>
        /// The provider's SDK type identity does not match the host's SDK copy
        /// (it was likely built against a different SDK version).
        /// </summary>
        SdkTypeIdentityMismatch,

        /// <summary>
        /// The provider's <see cref="BuiltAgainstSdkAttribute"/> / <see cref="MinHostSdkAttribute"/>
        /// metadata is incompatible with the host's SDK version.
        /// </summary>
        SdkVersionIncompatible,
    }

    /// <summary>
    /// Loads a provider assembly into an isolated <see cref="ProviderLoadContext"/>,
    /// verifies that its SDK type identity matches the host's copy, and returns the
    /// loaded assembly plus an outcome value the caller can act on.
    /// </summary>
    internal static class ProviderAssemblyLoader
    {
        /// <summary>
        /// Try to load <paramref name="assemblyPath"/> into a per-provider ALC and validate
        /// it against the host's SDK type identity.
        /// </summary>
        /// <param name="assemblyPath">Full path to the provider DLL.</param>
        /// <param name="assembly">The loaded assembly on success; otherwise <c>null</c>.</param>
        /// <param name="diagnostic">A user-facing diagnostic message describing the outcome.</param>
        /// <returns>The outcome of the load attempt.</returns>
        public static ProviderLoadOutcome TryLoad(
            string assemblyPath,
            out Assembly? assembly,
            out string diagnostic)
        {
            assembly = null;
            diagnostic = string.Empty;

            try
            {
                var alc = new ProviderLoadContext(assemblyPath);
                assembly = alc.LoadFromAssemblyPath(assemblyPath);
            }
            catch (BadImageFormatException ex)
            {
                diagnostic = $"Provider '{Path.GetFileName(assemblyPath)}' is not a valid managed assembly: {ex.Message}";
                return ProviderLoadOutcome.InvalidAssembly;
            }
            catch (FileLoadException ex)
            {
                diagnostic = $"Provider '{Path.GetFileName(assemblyPath)}' failed to load: {ex.Message}";
                return ProviderLoadOutcome.InvalidAssembly;
            }
            catch (Exception ex)
            {
                diagnostic = $"Unexpected error loading provider '{Path.GetFileName(assemblyPath)}': {ex.Message}";
                return ProviderLoadOutcome.InvalidAssembly;
            }

            // Type-identity check: any IAIProviderFactory exposed by the provider must
            // refer to the SAME runtime type as the one in the host's loaded SDK.
            Type[] exportedTypes;
            try
            {
                exportedTypes = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                exportedTypes = ex.Types.Where(t => t != null).ToArray()!;
                Debug.WriteLine($"[ProviderAssemblyLoader] ReflectionTypeLoadException in {Path.GetFileName(assemblyPath)}: {ex.Message}");
            }

            var factoryTypeFromHost = typeof(IAIProviderFactory);
            var factoryFromAssembly = exportedTypes
                .FirstOrDefault(t => t is not null && t.GetInterfaces().Any(i =>
                    string.Equals(i.FullName, factoryTypeFromHost.FullName, StringComparison.Ordinal)));

            if (factoryFromAssembly is null)
            {
                diagnostic = $"Provider '{Path.GetFileName(assemblyPath)}' does not expose any type implementing IAIProviderFactory and will be ignored.";
                return ProviderLoadOutcome.NoFactory;
            }

            var sdkInterfaceSeenByProvider = factoryFromAssembly
                .GetInterfaces()
                .FirstOrDefault(i => string.Equals(i.FullName, factoryTypeFromHost.FullName, StringComparison.Ordinal));

            if (sdkInterfaceSeenByProvider is null
                || sdkInterfaceSeenByProvider.Assembly != factoryTypeFromHost.Assembly)
            {
                var hostSdk = factoryTypeFromHost.Assembly.GetName();
                var providerSdk = sdkInterfaceSeenByProvider?.Assembly.GetName();
                diagnostic =
                    $"Provider '{Path.GetFileName(assemblyPath)}' was built against a different SmartHopper.ProviderSdk and cannot be loaded.\n" +
                    $"Host SDK: {hostSdk.Name} v{hostSdk.Version}\n" +
                    $"Provider SDK: {(providerSdk == null ? "(unresolved)" : $"{providerSdk.Name} v{providerSdk.Version}")}";
                return ProviderLoadOutcome.SdkTypeIdentityMismatch;
            }

            // SemVer-based compatibility check using BuiltAgainstSdk/MinHostSdk attributes.
            // MissingMetadata is tolerated (logged via diagnostic but not blocking) because
            // first-party providers ship without these attributes set explicitly today.
            var compatResult = SdkCompatibility.Check(assembly, out var compatDiagnostic);
            switch (compatResult)
            {
                case SdkCompatibility.CompatibilityResult.MajorMismatch:
                case SdkCompatibility.CompatibilityResult.HostTooOld:
                    diagnostic = compatDiagnostic;
                    return ProviderLoadOutcome.SdkVersionIncompatible;
                case SdkCompatibility.CompatibilityResult.MissingMetadata:
                    Debug.WriteLine($"[ProviderAssemblyLoader] {compatDiagnostic}");
                    break;
            }

            return ProviderLoadOutcome.Loaded;
        }
    }
}
