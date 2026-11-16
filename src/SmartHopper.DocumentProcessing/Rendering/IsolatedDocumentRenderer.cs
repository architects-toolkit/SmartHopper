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
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading;

namespace SmartHopper.DocumentProcessing.Rendering
{
    /// <summary>
    /// Loads <c>SmartHopper.DocumentRendering</c> through ONE long-lived dedicated
    /// <see cref="AssemblyLoadContext"/> per renderer directory and invokes its BCL-only
    /// boundary via reflection. Renderer managed/native dependencies resolve strictly
    /// from the packaged directory — never the default context — so a host-bundled
    /// older SkiaSharp (e.g. Rhino's 2.88) cannot shadow the renderer's own copy, and a
    /// missing dependency fails loudly instead of binding whatever the host loaded.
    /// Native entry is serialized with a semaphore; cancellation is cooperative
    /// before AND after native work, not an unsafe attempt to abort a native thread.
    /// </summary>
    public sealed class IsolatedDocumentRenderer
    {
        /// <summary>Serializes native PDFium entry (one native worker at a time).</summary>
        private static readonly SemaphoreSlim NativeGate = new (1, 1);

        /// <summary>One ALC per canonical renderer directory — never one per document.</summary>
        private static readonly ConcurrentDictionary<string, Lazy<IsolatedDocumentRenderer>> Cache =
            new (StringComparer.OrdinalIgnoreCase);

        /// <summary>Dependency names that MUST resolve from the packaged folder. A miss is a
        /// hard failure — falling back to the default context could bind an incompatible
        /// host version (Rhino bundles SkiaSharp 2.88.x).</summary>
        private static readonly string[] RequiredOwnDependencies = { "PDFtoImage", "SkiaSharp" };

        private readonly MethodInfo renderMethod;
        private readonly MethodInfo getSizesMethod;
        private readonly string unavailableReason;

        private IsolatedDocumentRenderer(MethodInfo render, MethodInfo sizes, string unavailableReason)
        {
            this.renderMethod = render;
            this.getSizesMethod = sizes;
            this.unavailableReason = unavailableReason;
        }

        /// <summary>True when the renderer assembly loaded successfully.</summary>
        public bool IsAvailable => this.unavailableReason == null;

        /// <summary>Reason the renderer is unavailable, if any (actionable diagnostic).</summary>
        public string UnavailableReason => this.unavailableReason;

        /// <summary>
        /// The packaged renderer directory: a fixed <c>DocumentRendering</c> folder next to
        /// THIS assembly (plugin install/output location, never a document-controlled path).
        /// </summary>
        public static string PackagedDirectory =>
            Path.Combine(
                Path.GetDirectoryName(typeof(IsolatedDocumentRenderer).Assembly.Location) ?? AppContext.BaseDirectory,
                "DocumentRendering");

        /// <summary>
        /// Gets (creating if needed) the renderer rooted at a directory. The packaged
        /// <see cref="PackagedDirectory"/> is trusted implicitly; a caller-supplied custom
        /// directory is untrusted code and MUST pass <paramref name="trustVerifier"/>
        /// (e.g. a hash/Authenticode classifier) or the facade reports itself unavailable.
        /// </summary>
        /// <param name="rendererDirectory">Directory containing
        /// SmartHopper.DocumentRendering.dll and its runtime assets, or null for the
        /// packaged default.</param>
        /// <param name="trustVerifier">Verifier invoked with the renderer DLL path for
        /// non-packaged directories; must return true to trust it.</param>
        /// <returns>The renderer facade; <see cref="IsAvailable"/> reports load failure.</returns>
        public static IsolatedDocumentRenderer Create(string rendererDirectory = null, Func<string, bool> trustVerifier = null)
        {
            var canonical = Path.GetFullPath(rendererDirectory ?? PackagedDirectory)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            var isPackaged = string.Equals(canonical, Path.GetFullPath(PackagedDirectory)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);

            // Trust runs before the cache lookup so a rejected directory can never reach
            // (or reuse) a previously loaded foreign ALC.
            if (trustVerifier == null && !isPackaged)
            {
                return new IsolatedDocumentRenderer(null, null,
                    $"Renderer directory '{canonical}' is outside the packaged install and no trust verifier was supplied; refusing to load foreign code.");
            }

            if (trustVerifier != null)
            {
                var dll = Path.Combine(canonical, "SmartHopper.DocumentRendering.dll");
                if (!trustVerifier(dll))
                {
                    return new IsolatedDocumentRenderer(null, null,
                        $"Renderer assembly '{dll}' failed trust verification; refusing to load it.");
                }
            }

            // Lazy ensures exactly one noncollectible ALC per directory even when callers
            // race to create it.
            return Cache
                .GetOrAdd(canonical, dir => new Lazy<IsolatedDocumentRenderer>(
                    () => CreateUncached(dir), LazyThreadSafetyMode.ExecutionAndPublication))
                .Value;
        }

        private static IsolatedDocumentRenderer CreateUncached(string rendererDirectory)
        {
            try
            {
                var dll = Path.Combine(rendererDirectory, "SmartHopper.DocumentRendering.dll");
                if (!File.Exists(dll))
                {
                    return new IsolatedDocumentRenderer(null, null,
                        $"Document renderer assembly not found at '{dll}'. Install the document-rendering runtime assets for this platform.");
                }

                var context = new RendererLoadContext(dll, rendererDirectory);
                var assembly = context.LoadFromAssemblyName(new AssemblyName("SmartHopper.DocumentRendering"));
                var type = assembly.GetType("SmartHopper.DocumentRendering.DocumentRenderer");
                var render = type?.GetMethod("Render", BindingFlags.Public | BindingFlags.Static);
                var sizes = type?.GetMethod("GetPageSizes", BindingFlags.Public | BindingFlags.Static);
                if (render == null || sizes == null)
                {
                    return new IsolatedDocumentRenderer(null, null,
                        "Document renderer boundary methods are missing; the packaged renderer assembly is incompatible.");
                }

                return new IsolatedDocumentRenderer(render, sizes, null);
            }
            catch (Exception ex)
            {
                return new IsolatedDocumentRenderer(null, null,
                    $"Document renderer failed to load from '{rendererDirectory}': {ex.Message}");
            }
        }

        /// <summary>
        /// Renders one page to PNG bytes, serialized through the native gate. Cancellation
        /// is checked before entry AND after the native call returns (a cancellation
        /// landing during native work still discards the result cooperatively).
        /// </summary>
        public byte[] Render(string formatId, byte[] source, int zeroBasedPage, int dpi, int maximumSide, double[] normalizedRegion, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            // Reflection boundary contract: a region is either null/empty (full page)
            // or exactly four finite normalized coordinates — never forward a
            // malformed shape into the native renderer.
            if (normalizedRegion != null && normalizedRegion.Length != 0 && normalizedRegion.Length != 4)
            {
                throw new ArgumentException(
                    $"A detail region must contain exactly 4 normalized coordinates, got {normalizedRegion.Length}.",
                    nameof(normalizedRegion));
            }

            if (normalizedRegion != null && normalizedRegion.Any(v => !double.IsFinite(v)))
            {
                throw new ArgumentException("Detail region coordinates must be finite.", nameof(normalizedRegion));
            }

            if (this.renderMethod == null)
            {
                throw new InvalidDataException(this.unavailableReason ?? "Document renderer is unavailable.");
            }

            NativeGate.Wait(ct);
            try
            {
                var result = (byte[])this.renderMethod.Invoke(null, new object[] { formatId, source, zeroBasedPage, dpi, maximumSide, normalizedRegion });
                ct.ThrowIfCancellationRequested();
                return result;
            }
            finally
            {
                NativeGate.Release();
            }
        }

        /// <summary>
        /// Returns flattened visible page dimensions in points [w0,h0,w1,h1,...].
        /// </summary>
        public double[] GetPageSizes(string formatId, byte[] source, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (this.getSizesMethod == null)
            {
                throw new InvalidDataException(this.unavailableReason ?? "Document renderer is unavailable.");
            }

            NativeGate.Wait(ct);
            try
            {
                var result = (double[])this.getSizesMethod.Invoke(null, new object[] { formatId, source });
                ct.ThrowIfCancellationRequested();
                return result;
            }
            finally
            {
                NativeGate.Release();
            }
        }

        /// <summary>
        /// Dedicated load context: managed deps resolve via
        /// <see cref="AssemblyDependencyResolver"/> from the packaged folder; renderer
        /// dependencies that fail to resolve are a hard error (never bound from the
        /// default context); only BCL/System assemblies may default-resolve. Unmanaged
        /// libs load by FULL PATH so an already-loaded same-named native never wins.
        /// </summary>
        private sealed class RendererLoadContext : AssemblyLoadContext
        {
            private readonly AssemblyDependencyResolver resolver;
            private readonly string baseDir;

            public RendererLoadContext(string mainAssemblyPath, string baseDir)
                : base("SmartHopper.DocumentRendering", isCollectible: false)
            {
                this.resolver = new AssemblyDependencyResolver(mainAssemblyPath);
                this.baseDir = baseDir;
            }

            protected override Assembly Load(AssemblyName assemblyName)
            {
                var path = this.resolver.ResolveAssemblyToPath(assemblyName);
                if (path != null)
                {
                    return this.LoadFromAssemblyPath(path);
                }

                // BCL/System assemblies may resolve from the default context; own renderer
                // dependencies must come from the packaged folder or fail explicitly.
                var name = assemblyName.Name ?? string.Empty;
                foreach (var dep in RequiredOwnDependencies)
                {
                    if (name.StartsWith(dep, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new FileNotFoundException(
                            $"Renderer dependency '{name}' was not found in '{this.baseDir}'; the packaged renderer assets are incomplete.", name);
                    }
                }

                return null;
            }

            protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
            {
                var path = this.resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
                if (path != null)
                {
                    return this.LoadUnmanagedDllFromPath(path);
                }

                var rids = OperatingSystem.IsWindows()
                    ? new[] { "win-x64" }
                    : (OperatingSystem.IsMacOS()
                        ? (System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture == System.Runtime.InteropServices.Architecture.Arm64
                            ? new[] { "osx-arm64", "osx" }   // 'osx' universal folder (SkiaSharp 4 macOS asset)
                            : new[] { "osx-x64", "osx" })
                        : Array.Empty<string>());

                foreach (var name in new[]
                {
                    unmanagedDllName,
                    unmanagedDllName + ".dll",
                    "lib" + unmanagedDllName + ".dylib",
                    "lib" + unmanagedDllName + ".so",
                })
                {
                    var hit = Path.Combine(this.baseDir, name);
                    if (!File.Exists(hit))
                    {
                        foreach (var rid in rids)
                        {
                            hit = Path.Combine(this.baseDir, "runtimes", rid, "native", name);
                            if (File.Exists(hit))
                            {
                                break;
                            }
                        }
                    }

                    if (File.Exists(hit))
                    {
                        return this.LoadUnmanagedDllFromPath(hit);
                    }
                }

                return IntPtr.Zero;
            }
        }
    }
}
