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
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace SmartHopper.Infrastructure.FileSystem
{
    /// <summary>
    /// Canonicalizes model-supplied paths and classifies them into the denied tier,
    /// the pre-authorized roots, or the consent-gated tier.
    /// </summary>
    public static class LocalPath
    {
        private static readonly bool CaseInsensitive =
            !RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

        /// <summary>Comparer matching the case sensitivity of the current filesystem.</summary>
        public static readonly StringComparer Comparer =
            CaseInsensitive ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

        private static readonly StringComparison Comparison =
            CaseInsensitive ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        /// <summary>Directory name segments that always deny access, anywhere in a path.</summary>
        private static readonly string[] DeniedSegments =
        {
            ".ssh", ".aws", ".azure", ".kube", ".gnupg",
        };

        /// <summary>Browser profile trees that hold credential stores, per platform layout.</summary>
        private static readonly string[] DeniedCredentialTrees =
        {
            "google/chrome",
            ".config/google-chrome",
            "microsoft/edge",
            "application support/microsoft edge",
            ".config/microsoft-edge",
            "bravesoftware",
            ".config/brave-browser",
            "chromium/user data",
            "application support/chromium",
            ".config/chromium",
            "mozilla/firefox",
            ".mozilla/firefox",
            "application support/firefox",
            "opera software/opera",
            "com.operasoftware.opera",
            ".config/opera",
        };

        /// <summary>Operating system roots that never allow tool access.</summary>
        private static readonly string[] UnixDeniedRoots =
        {
            "/etc", "/usr", "/bin", "/sbin", "/boot", "/root",
            "/proc", "/sys", "/private/etc", "/System",
        };

        /// <summary>Gets the SmartHopper roaming configuration directory.</summary>
        public static string SmartHopperDataDirectory { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Grasshopper", "SmartHopper");

        /// <summary>Gets the SmartHopper settings file, which holds encrypted provider secrets.</summary>
        public static string SmartHopperSettingsFile { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Grasshopper", "SmartHopper.json");

        /// <summary>Gets the pre-authorized screenshot export directory.</summary>
        public static string ScreenshotsDirectory { get; } = Path.Combine(
            SmartHopperDataDirectory, "Screenshots");

        /// <summary>
        /// Canonicalizes a user- or model-supplied path: must be rooted, normalized by
        /// <see cref="Path.GetFullPath"/>, and link targets resolved when the file exists.
        /// </summary>
        /// <param name="path">The raw path argument.</param>
        /// <param name="canonical">The canonical absolute path when successful.</param>
        /// <param name="error">Why canonicalization failed.</param>
        /// <returns>True when a canonical absolute path was produced.</returns>
        public static bool TryCanonicalize(string? path, out string canonical, out string error)
        {
            canonical = string.Empty;
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(path))
            {
                error = "The path is empty.";
                return false;
            }

            try
            {
                if (!Path.IsPathRooted(path))
                {
                    error = "The path must be absolute.";
                    return false;
                }

                canonical = Path.GetFullPath(path);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException || ex is PathTooLongException)
            {
                error = $"The path is invalid: {ex.Message}";
                return false;
            }

            canonical = canonical.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (canonical.Length == 0 || !Path.IsPathRooted(canonical))
            {
                // Trimming a drive or UNC root (e.g. "C:\") leaves the root; keep it.
                canonical = Path.GetFullPath(path);
            }

            // Extended-length device prefixes (\\?\, \\?\UNC\) preserve the same file
            // under a different spelling; normalize them so denied checks cannot be
            // evaded by an alternate prefix.
            if (canonical.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase))
            {
                canonical = @"\" + canonical.Substring(8);
            }
            else if (canonical.StartsWith(@"\\?\", StringComparison.Ordinal))
            {
                canonical = canonical.Substring(4);
            }

            if (canonical.Length >= 2 && canonical[1] == ':' && char.IsLower(canonical[0]))
            {
                // GetFullPath preserves the drive-letter casing the caller supplied.
                canonical = char.ToUpperInvariant(canonical[0]) + canonical.Substring(1);
            }

            // Ancestor links (junctions/symlinks) must resolve too: a folder grant on a
            // directory containing a link would otherwise cover the link's target tree.
            canonical = ResolveLinkedDirectories(canonical);

            try
            {
                if (File.Exists(canonical))
                {
                    var target = File.ResolveLinkTarget(canonical, returnFinalTarget: true);
                    if (target != null)
                    {
                        canonical = Path.GetFullPath(target.FullName)
                            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    }
                }
            }
            catch (IOException)
            {
                // Unresolvable reparse points fall back to the normalized path; access
                // is still checked against the canonical spelling.
            }

            return true;
        }

        /// <summary>
        /// Rewrites <paramref name="path"/> so every existing ancestor directory is the
        /// physical target of any reparse point in the chain.
        /// </summary>
        private static string ResolveLinkedDirectories(string path, int depth = 0)
        {
            var parent = Path.GetDirectoryName(path);
            if (depth > 64 || string.IsNullOrEmpty(parent))
            {
                return path;
            }

            var candidate = Path.Combine(ResolveLinkedDirectories(parent, depth + 1), Path.GetFileName(path));
            try
            {
                if (Directory.Exists(candidate))
                {
                    var target = Directory.ResolveLinkTarget(candidate, returnFinalTarget: true);
                    if (target != null)
                    {
                        return ResolveLinkedDirectories(target.FullName, depth + 1);
                    }
                }
            }
            catch (IOException)
            {
                // Leave unresolvable reparse points on the canonical spelling.
            }

            return candidate;
        }

        /// <summary>Checks whether <paramref name="path"/> is <paramref name="root"/> or a descendant.</summary>
        public static bool IsUnder(string root, string path)
        {
            if (string.IsNullOrEmpty(root) || string.IsNullOrEmpty(path))
            {
                return false;
            }

            var normalizedRoot = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (string.Equals(path, normalizedRoot, Comparison))
            {
                return true;
            }

            var prefix = normalizedRoot + Path.DirectorySeparatorChar;
            if (path.StartsWith(prefix, Comparison))
            {
                return true;
            }

            var altPrefix = normalizedRoot + Path.AltDirectorySeparatorChar;
            return Path.AltDirectorySeparatorChar != Path.DirectorySeparatorChar
                && path.StartsWith(altPrefix, Comparison);
        }

        /// <summary>
        /// Denied-tier paths are refused outright: credential stores, browser profiles,
        /// SmartHopper's own configuration and system locations never reach the prompt.
        /// </summary>
        public static bool IsDenied(string canonicalPath)
        {
            if (string.IsNullOrEmpty(canonicalPath))
            {
                return true;
            }

            var segments = canonicalPath
                .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Where(segment => segment.Length > 0)
                .ToArray();

            if (segments.Any(segment => DeniedSegments.Contains(segment, Comparer)))
            {
                return true;
            }

            var lowered = canonicalPath
                .Replace(Path.AltDirectorySeparatorChar, '/')
                .Replace(Path.DirectorySeparatorChar, '/');
            if (DeniedCredentialTrees.Any(tree => lowered.IndexOf("/" + tree + "/", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                return true;
            }

            if (IsUnder(SmartHopperDataDirectory, canonicalPath) && !IsUnder(ScreenshotsDirectory, canonicalPath))
            {
                return true;
            }

            if (string.Equals(canonicalPath, SmartHopperSettingsFile, Comparison))
            {
                return true;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                var programW6432 = Environment.GetEnvironmentVariable("ProgramW6432");
                var commonProgramFiles = Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles);

                if (IsUnder(windows, canonicalPath)
                    || IsUnder(programFiles, canonicalPath)
                    || IsUnder(programFilesX86, canonicalPath)
                    || IsUnder(commonProgramFiles, canonicalPath)
                    || (!string.IsNullOrEmpty(programW6432) && IsUnder(programW6432, canonicalPath)))
                {
                    return true;
                }
            }
            else if (UnixDeniedRoots.Any(root => IsUnder(root, canonicalPath)))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Paths SmartHopper produces itself are pre-authorized and never prompt:
        /// the screenshot export directory and SmartHopper temp files.
        /// </summary>
        public static bool IsPreAuthorized(string canonicalPath, LocalFileCapability capability)
        {
            if (string.IsNullOrEmpty(canonicalPath))
            {
                return false;
            }

            // Pre-authorization only ever covers reading and writing SmartHopper's own
            // files — never opening or running them.
            if ((capability & (LocalFileCapability.Read | LocalFileCapability.Write)) == 0)
            {
                return false;
            }

            if (IsUnder(ScreenshotsDirectory, canonicalPath))
            {
                return true;
            }

            // Internal pipeline files (e.g. PDF attachments converted for the chat) live
            // under %TEMP% with a smarthopper prefix.
            var temp = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (IsUnder(temp, canonicalPath)
                && Path.GetFileName(canonicalPath).StartsWith("smarthopper", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }
    }
}
